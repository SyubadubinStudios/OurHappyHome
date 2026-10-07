using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

public sealed partial class GameSession
{
    /// <summary>
    /// Records a family memory (design section 23): who, where, what, how it
    /// felt and how it changed relationships. The presentation takes a photo
    /// for the album when asked.
    /// </summary>
    public FamilyMemory? CreateMemory(string title, string description, MemoryKind kind, EmotionalOutcome outcome,
        IReadOnlyList<MemberId> participants, string location, float importance = 1f, string tag = "", bool photo = true)
    {
        if (tag.Length > 0 && State.Memories.Any(m => m.Tag == tag && m.DayIndex == Clock.DayIndex))
        {
            return null;
        }

        float change = outcome switch
        {
            EmotionalOutcome.Heartwarming => 3f,
            EmotionalOutcome.Joyful or EmotionalOutcome.Proud or EmotionalOutcome.Funny => 2f,
            EmotionalOutcome.Relieved or EmotionalOutcome.Scary => 1.5f,
            _ => -1f,
        };

        FamilyMemory memory = new()
        {
            Id = State.NextMemoryId++,
            Title = title,
            Description = description,
            Kind = kind,
            Outcome = outcome,
            DayIndex = Clock.DayIndex,
            Minute = Clock.MinuteOfDay,
            Location = location,
            Participants = [.. participants.Distinct()],
            RelationshipChange = change,
            Tag = tag,
            Importance = importance,
        };
        State.Memories.Add(memory);
        foreach (MemberId a in memory.Participants)
        {
            State.Member(a).MemoryIds.Add(memory.Id);
            foreach (MemberId b in memory.Participants)
            {
                if (a < b && kind is not MemoryKind.Argument)
                {
                    State.Relationships.Change(a, b, change);
                }
            }
        }

        State.AddStat(Stat.Memories);
        Bus.Publish(new MemoryEvent(memory, photo));
        Bus.Notice(Loc.T($"Kenangan baru: {title}", $"New memory: {title}"), FamilyMemory.OutcomeIcon(outcome), NoticeKind.Memory);
        return memory;
    }

    /// <summary>The presentation saved a photo for a memory.</summary>
    public void AttachPhoto(int memoryId, string file)
    {
        if (State.Memories.FirstOrDefault(m => m.Id == memoryId) is { } memory)
        {
            memory.PhotoFile = file;
            State.AddStat(Stat.Photos);
        }
    }

    /// <summary>A photo the player takes on purpose (P key) becomes a memory too.</summary>
    public void TakeFamilyPhoto()
    {
        FamilyMember player = Controlled;
        List<MemberId> visible = [.. State.Members.Where(m => !m.Away && System.Numerics.Vector2.Distance(m.Position, player.Position) < 12f).Select(m => m.Id)];
        string place = LocationName(player);
        Bus.Sound("camera");
        CreateMemory(Loc.T($"Foto di {place}", $"Photo at {place}"),
            Loc.T($"Foto kenangan bersama {string.Join(", ", visible.Select(FamilyNames.Short))}.", $"A keepsake photo with {string.Join(", ", visible.Select(FamilyNames.Short))}."),
            MemoryKind.Photo, EmotionalOutcome.Joyful, visible, place, 1.2f, $"photo:{Clock.DayIndex}:{(int)Clock.MinuteOfDay}");
        Practice(player, SkillKind.Photography, 0.3f);
    }

    private void CheckProgress()
    {
        // Chapter goals.
        if (Chapters.Get(State.Chapter) is { } chapter && chapter.Goals.All(g => g.Done(State)))
        {
            State.Wallet.Earn(chapter.Reward, Loc.T($"Hadiah Bab {chapter.Number}", $"Chapter {chapter.Number} reward"), Clock.DayIndex);
            Bus.Publish(new ChapterEvent(chapter.Number, chapter.Title, true));
            Bus.Sound("fanfare");
            Bus.Publish(new MusicEvent(MusicMood.Celebration));
            Bus.Effect(EffectKind.Confetti, Controlled.Position, 2.5f, 2f);
            if (State.Member(MemberId.Father) is { Away: false } father)
            {
                Say(father, Loc.T("Hebat! Ayah bangga sama kamu!", "Great job! Dad is proud of you!"), "dad_cheer");
            }

            CreateMemory(Loc.T($"Bab {chapter.Number}: {chapter.Title}", $"Chapter {chapter.Number}: {chapter.Title}"),
                Loc.T($"Keluarga menyelesaikan bab \"{chapter.Title}\" bersama-sama.", $"The family completed \"{chapter.Title}\" together."),
                MemoryKind.Chapter, EmotionalOutcome.Proud, FamilyNames.All, LocationName(Controlled), 4f, $"chapter:{chapter.Number}");
            State.Chapter++;
            if (Chapters.Get(State.Chapter) is { } next)
            {
                Bus.Publish(new ChapterEvent(next.Number, next.Title, false));
            }
            else
            {
                State.Chapter = Chapters.Sandbox;
                Bus.Publish(new ChapterEvent(Chapters.Sandbox, Chapters.SandboxTitle, false));
            }
        }

        foreach (Achievement achievement in Achievements.All)
        {
            if (!State.Achievements.Contains(achievement.Id) && State.Stat(achievement.Stat) >= achievement.Target)
            {
                if (achievement.Id == "explorer" && (State.Stat(Stat.BeachTrips) < 1 || State.Stat(Stat.ThemeParkTrips) < 1))
                {
                    continue;
                }

                State.Achievements.Add(achievement.Id);
                Bus.Notice(Loc.T($"Pencapaian: {achievement.Title}!", $"Achievement: {achievement.Title}!"), achievement.Icon, NoticeKind.Good);
                Bus.Sound("achievement");
            }
        }
    }

    /// <summary>Arriving somewhere: chapter stats, trips and the music changes.</summary>
    private void OnEnterPlace(PlaceId place)
    {
        Bus.Publish(new MusicEvent(place == PlaceId.Home ? (Clock.IsNight ? MusicMood.Night : MusicMood.Cozy) : MusicMood.Explore));
        string flag = $"visit:{place}:{Clock.DayIndex}";
        if (!State.Flags.Add(flag))
        {
            return;
        }

        if (place != PlaceId.Home && place != PlaceId.Neighborhood)
        {
            Bus.Notice(Loc.T($"Tiba di {WorldMap.Name(place)}", $"Arrived at {WorldMap.Name(place)}"), WorldMap.Icon(place), NoticeKind.Info);
        }

        List<MemberId> group = [Controlled.Id, .. State.Party];
        bool familyTrip = group.Count >= 3;
        switch (place)
        {
            case PlaceId.Park:
                State.AddStat(Stat.VisitedPark);
                if (familyTrip)
                {
                    TripMemory(Loc.T("Piknik di taman kota", "Picnic in the park"), Loc.T("Kami menggelar tikar, makan bekal dan memberi makan ikan di kolam.", "We spread a blanket, ate our packed lunch and fed the fish."), group, place);
                }

                break;
            case PlaceId.Supermarket:
                State.AddStat(Stat.VisitedSupermarket);
                break;
            case PlaceId.Camping:
                if (familyTrip)
                {
                    State.AddStat(Stat.CampingTrips);
                    TripMemory(Loc.T("Kemah keluarga di gunung", "Family camping on the mountain"), Loc.T("Api unggun, cerita seram yang lucu dan langit penuh bintang.", "A campfire, silly spooky stories and a sky full of stars."), group, place);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
                    {
                        Practice(m, SkillKind.Outdoor, 0.6f);
                    }
                }

                break;
            case PlaceId.Beach:
                if (familyTrip)
                {
                    State.AddStat(Stat.BeachTrips);
                    TripMemory(Loc.T("Liburan di pantai", "Beach holiday"), Loc.T("Membangun istana pasir, berenang dan melihat matahari terbenam.", "Building sandcastles, swimming and watching the sunset."), group, place);
                }

                break;
            case PlaceId.ThemePark:
                if (familyTrip)
                {
                    State.AddStat(Stat.ThemeParkTrips);
                    TripMemory(Loc.T("Seharian di taman bermain", "A day at the theme park"), Loc.T("Naik bianglala dan komidi putar sampai pusing!", "Riding the Ferris wheel and carousel until we were dizzy!"), group, place);
                }

                break;
            case PlaceId.Forest:
                if (State.Mode != GameMode.Cozy && Scenario is null && Random.Chance(0.35f * Director.DangerMultiplier(Rarity.Uncommon)))
                {
                    StartScenario(Scenarios.ScenarioKind.WildAnimal);
                }

                break;
        }

        foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
        {
            m.Needs.Add(NeedKind.Fun, familyTrip ? 25f : 8f);
            if (familyTrip)
            {
                m.Mood.Add($"trip:{place}", Loc.T($"Jalan-jalan ke {WorldMap.Name(place)}", $"Trip to {WorldMap.Name(place)}"), 14, MoodKind.Excited, Now, 300);
            }
        }
    }

    private void TripMemory(string title, string description, List<MemberId> group, PlaceId place)
    {
        CreateMemory(title, description, MemoryKind.Trip, EmotionalOutcome.Joyful, group, WorldMap.Name(place), 3f, $"trip:{place}");
        Bus.Effect(EffectKind.Confetti, Controlled.Position, 2.2f);
    }
}
