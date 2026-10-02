using System.Numerics;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

/// <summary>
/// Living family AI (design section 4): every autonomous member scores what
/// they could do next from their needs, schedule, personality, hobbies and
/// memories, then walks off and does it. Nobody waits for player commands.
/// </summary>
public sealed partial class GameSession
{
    private static readonly ActivityId[] Candidates =
    [
        ActivityId.Sleep, ActivityId.Nap, ActivityId.Eat, ActivityId.Snack, ActivityId.Cook, ActivityId.MakeDrink,
        ActivityId.Shower, ActivityId.Toilet, ActivityId.WatchTV, ActivityId.Read, ActivityId.Homework, ActivityId.Draw,
        ActivityId.PlayMusic, ActivityId.Play, ActivityId.PlayOutside, ActivityId.Garden, ActivityId.WaterPlants,
        ActivityId.Repair, ActivityId.Craft, ActivityId.Clean, ActivityId.Laundry, ActivityId.Chat, ActivityId.PlayWithPet,
        ActivityId.Computer, ActivityId.Stargaze, ActivityId.SellLemonade, ActivityId.Exercise, ActivityId.GetDressed,
        ActivityId.Relax, ActivityId.Wander, ActivityId.Swim, ActivityId.FirstAid, ActivityId.Errand,
    ];

    /// <summary>True when at least one everyday dish can be cooked with what is in the kitchen.</summary>
    public bool CanCookSomething() =>
        Recipes.All.Any(r => !r.IsDrink && !r.GrillOnly && !r.Celebration && r.CanMake(State.Inventory));

    private bool GroceriesLow() =>
        State.Inventory.Count("rice") + State.Inventory.Count("egg") + State.Inventory.Count("chicken") + State.Inventory.Count("flour") < 4
        || !CanCookSomething();

    private void ChooseNextActivity(FamilyMember m)
    {
        if (Scenario?.Direct(m) == true)
        {
            return;
        }

        // On a trip outside the lot, autonomous members just stay with the group.
        if (!Rooms.Lot.Inflate(2f).Contains(m.Position))
        {
            StartTask(m, ActivityId.Idle, null, -1, target: m.Position);
            return;
        }

        float bestScore = float.MinValue;
        ActivityId best = ActivityId.Idle;
        FurnitureChoice? bestFurniture = null;
        foreach (ActivityId activity in Candidates)
        {
            ActivityDef def = ActivityCatalog.Get(activity);
            if (!def.AllowedFor(m.Id))
            {
                continue;
            }

            FurnitureChoice? furniture = null;
            if (def.NeedsFurniture)
            {
                furniture = FindFurniture(m, activity);
                if (furniture is null)
                {
                    continue;
                }
            }

            float score = Score(m, def, furniture);
            if (score > bestScore)
            {
                bestScore = score;
                best = activity;
                bestFurniture = furniture;
            }
        }

        StartChosen(m, best, bestFurniture);
    }

    private float Score(FamilyMember m, ActivityDef def, FurnitureChoice? furniture)
    {
        Needs needs = m.Needs;
        float hour = Hour;
        GameDate date = Date;
        bool night = hour >= m.Personality.BedHour || hour < m.Personality.WakeHour;
        float score = 0f;

        foreach ((NeedKind need, float rate) in def.Rates)
        {
            if (rate <= 0f)
            {
                continue;
            }

            float gain = MathF.Min(rate * def.Minutes, 100f - needs[need]);
            score += needs.Urgency(need) * gain / 20f;
        }

        // Hobbies and likes.
        if (def.Skill is { } skill && m.Personality.Hobbies.Contains(skill))
        {
            score += 0.35f;
        }

        // Things that only make sense at certain times.
        switch (def.Id)
        {
            case ActivityId.Sleep:
                if (!night && needs[NeedKind.Sleep] > 15f)
                {
                    return float.MinValue;
                }

                score += night ? 4f : 1.5f;
                break;
            case ActivityId.Nap:
                if (night || needs[NeedKind.Sleep] > 35f)
                {
                    return float.MinValue;
                }

                break;
            case ActivityId.Eat:
                if (State.MealServings <= 0 || needs[NeedKind.Hunger] > 88f)
                {
                    return float.MinValue;
                }

                if (IsMealTime(hour))
                {
                    score += 2.2f;
                }

                break;
            case ActivityId.Snack:
                if (!State.Inventory.Has("snack") && !State.Inventory.Has("fruit"))
                {
                    return float.MinValue;
                }

                score *= 0.6f;
                break;
            case ActivityId.Cook:
                if (!CanCookSomething())
                {
                    return float.MinValue;
                }

                score += CookScore(m, hour);
                break;
            case ActivityId.Errand:
                // Somebody grown-up goes shopping when the kitchen runs low.
                if (!GroceriesLow() || hour < 7f || hour > 19.5f || State.Wallet.Money < 150_000
                    || State.Members.Any(o => o.Away && o.AwayActivity == ActivityId.Errand))
                {
                    return float.MinValue;
                }

                score = (m.Id == MemberId.Mother ? 3.2f : 2.4f) + (State.MealServings == 0 ? 1f : 0f);
                break;
            case ActivityId.MakeDrink:
                if (!State.Inventory.Has("milk"))
                {
                    return float.MinValue;
                }

                score = State.Weather.IsRaining && hour > 17f ? 0.9f : -1.5f;
                break;
            case ActivityId.Shower:
                score += hour is > 5.5f and < 7.5f or > 17.5f and < 20f ? 0.6f : 0f;
                break;
            case ActivityId.Homework:
                score += date.IsSchoolDay && hour is > 14.5f and < 18.5f ? 1.1f : -0.5f;
                break;
            case ActivityId.WatchTV:
                if (!State.House.PowerOn)
                {
                    return float.MinValue;
                }

                score += hour > 18.5f ? 0.4f : 0f;
                break;
            case ActivityId.Computer or ActivityId.PlayMusic when !State.House.PowerOn:
                return float.MinValue;
            case ActivityId.PlayOutside or ActivityId.Garden or ActivityId.WaterPlants or ActivityId.SellLemonade or ActivityId.Swim or ActivityId.Exercise:
                if (!State.Weather.OutdoorFriendly || hour < 7f || hour > 18f)
                {
                    return float.MinValue;
                }

                if (def.Id == ActivityId.SellLemonade && (!date.IsWeekend || !State.Inventory.Has("fruit")))
                {
                    return float.MinValue;
                }

                if (def.Id == ActivityId.Garden && m.Id == MemberId.Mother)
                {
                    score += 0.5f;
                }

                if (def.Id == ActivityId.Swim && !State.House.Has(RoomId.Pool))
                {
                    return float.MinValue;
                }

                if (def.Id == ActivityId.Exercise)
                {
                    score += m.Id == MemberId.Father && hour < 9f && date.IsWeekend ? 0.8f : -0.4f;
                }

                break;
            case ActivityId.Stargaze:
                if (hour < 19f || hour > 22f || State.Weather.IsRaining)
                {
                    return float.MinValue;
                }

                score += 0.3f;
                break;
            case ActivityId.Repair:
                // Fixing broken things is mostly Father's job.
                score = furniture is not null ? (m.Id == MemberId.Father ? 2.2f : m.Skills[SkillKind.Repair] > 2.5f ? 1f : -1f) : float.MinValue;
                break;
            case ActivityId.Clean:
                bool mess = State.House.Hazards.Any(h => h.Kind is HazardKind.Puddle or HazardKind.Flour or HazardKind.BrokenGlass or HazardKind.Debris);
                score += mess ? (m.IsChild ? 0.8f : 1.8f) : (m.Id == MemberId.Mother ? 0.15f : -0.4f);
                break;
            case ActivityId.Laundry:
                score += m.Id == MemberId.Mother && date.Weekday is Weekday.Saturday or Weekday.Wednesday && hour < 12f ? 0.9f : -0.6f;
                break;
            case ActivityId.Chat:
                if (FindChatPartner(m) is null)
                {
                    return float.MinValue;
                }

                score += m.Personality.Sociability * 0.4f;
                break;
            case ActivityId.PlayWithPet:
                if (State.Pets.Count == 0)
                {
                    return float.MinValue;
                }

                score += m.IsChild ? 0.4f : 0f;
                break;
            case ActivityId.GetDressed:
                score += m.Id == MemberId.OlderSister && hour < 9f ? 0.5f : -0.2f;
                break;
            case ActivityId.FirstAid:
                if (needs[NeedKind.Health] > 70f || !State.Inventory.Has("bandage"))
                {
                    return float.MinValue;
                }

                break;
            case ActivityId.Wander:
                score = 0.12f;
                break;
            case ActivityId.Relax:
                score += m.Stamina.Value < 50 ? 0.5f : 0f;
                break;
        }

        // Sleepy members prefer quiet things; the sick avoid exertion.
        if (night && def.Id is not (ActivityId.Sleep or ActivityId.Toilet or ActivityId.Snack))
        {
            score -= 1.2f;
        }

        if ((m.Sick || !m.Stamina.CanDoDemanding) && def.Demanding)
        {
            return float.MinValue;
        }

        // Personal touches: Dinda loves to read, Nara to draw, Mom to garden.
        score += (m.Id, def.Id) switch
        {
            (MemberId.YoungerSister, ActivityId.Read) => 0.5f,
            (MemberId.OlderSister, ActivityId.Draw) => 0.5f,
            (MemberId.OlderSister, ActivityId.PlayMusic) => 0.3f,
            (MemberId.Father, ActivityId.Craft) => 0.2f,
            (MemberId.Mother, ActivityId.WaterPlants) => 0.3f,
            _ => 0f,
        };

        // A little distance cost and some randomness so days differ.
        if (furniture is { } f)
        {
            score -= Vector2.Distance(m.Position, f.Item.ApproachPoint(f.Slot)) * 0.015f;
        }

        // Don't repeat the same thing over and over.
        if (m.Mood.Has($"did:{def.Id}"))
        {
            score -= 0.8f;
        }

        return score + Random.Range(0f, 0.25f);
    }

    private static bool IsMealTime(float hour) => hour is >= 6.6f and < 8.2f or >= 12f and < 13.5f or >= 18.2f and < 19.8f;

    private float CookScore(FamilyMember m, float hour)
    {
        bool prepTime = hour is >= 6.0f and < 7.1f or >= 11.2f and < 12.2f or >= 17.3f and < 18.4f;
        int present = State.Members.Count(o => !o.Away);
        bool needFood = State.MealServings < present;
        bool someoneCooking = State.Members.Any(o => o.Id != m.Id && o.Task is { Activity: ActivityId.Cook, Tag: not "helper" });

        // Dinda's favourite activity is helping Mom with breakfast.
        if (m.Id == MemberId.YoungerSister && State.Members.Any(o => o.Id == MemberId.Mother && o.Task is { Activity: ActivityId.Cook }))
        {
            return 3.5f;
        }

        if (!needFood || someoneCooking)
        {
            return float.MinValue / 4;
        }

        float skill = m.Skills[SkillKind.Cooking];
        float bonus = m.Id switch
        {
            MemberId.Mother => 3f,
            MemberId.Father => State.Member(MemberId.Mother).Away ? 2.4f : 0.6f,
            _ => skill >= 1.5f ? 0.3f : -2f,
        };
        bool someoneHungry = State.Members.Any(o => !o.Away && o.Needs[NeedKind.Hunger] < 40f);
        return prepTime ? bonus : bonus - 2.2f + (State.MealServings == 0 && someoneHungry ? 2.4f : 0f);
    }

    private FamilyMember? FindChatPartner(FamilyMember m)
    {
        FamilyMember? best = null;
        float bestScore = float.MinValue;
        foreach (FamilyMember o in State.Members)
        {
            if (o.Id == m.Id || o.Away || o.InDanger || o.Task is { Activity: ActivityId.Sleep or ActivityId.Nap or ActivityId.Shower or ActivityId.Toilet })
            {
                continue;
            }

            if (o.Id == State.Controlled || !Rooms.Lot.Contains(o.Position))
            {
                continue;
            }

            float distance = Vector2.Distance(o.Position, m.Position);
            if (distance > 14f)
            {
                continue;
            }

            float s = State.Relationships[m.Id, o.Id] - (distance * 2f) + (State.Relationships.Arguing(m.Id, o.Id) ? 15f : 0f);
            if (s > bestScore)
            {
                bestScore = s;
                best = o;
            }
        }

        return best;
    }

    private void StartChosen(FamilyMember m, ActivityId activity, FurnitureChoice? furniture)
    {
        m.Mood.Add($"did:{activity}", "", 0, MoodKind.Content, Now, activity is ActivityId.Sleep ? 0 : 90);
        switch (activity)
        {
            case ActivityId.Eat when furniture is { } table:
                {
                    PreparedFood? food = State.Pantry.Where(p => !p.Recipe.IsDrink && p.Servings > 0).OrderByDescending(p => p.Quality).FirstOrDefault();
                    if (food is null)
                    {
                        break;
                    }

                    food.Servings--;
                    State.Pantry.RemoveAll(p => p.Servings <= 0);
                    StartTask(m, ActivityId.Eat, table.Item, table.Slot, tag: $"quality:{food.Quality}", recipe: food.RecipeId);
                    if (IsMealTime(Hour) && m.Id == MemberId.Mother && Random.Chance(0.5f))
                    {
                        Say(m, Hour < 10 ? Loc.T("Sarapan sudah siap! Ayo makan bersama!", "Breakfast is ready! Let's eat together!") : Loc.T("Makan malam siap! Cuci tangan dulu ya.", "Dinner is ready! Wash your hands first."),
                            Hour < 10 ? "mom_breakfast" : "mom_dinner");
                    }

                    return;
                }

            case ActivityId.Cook when furniture is { } stove:
                {
                    bool helping = State.Members.Any(o => o.Id != m.Id && o.Task is { Activity: ActivityId.Cook });
                    StartTask(m, ActivityId.Cook, stove.Item, stove.Slot, tag: helping ? "helper" : "");
                    if (helping && m.Id == MemberId.YoungerSister)
                    {
                        Say(m, Loc.T("Ibu, aku bantu masak ya!", "Mom, can I help cook?"), "ys_cook");
                    }

                    return;
                }

            case ActivityId.Chat:
                {
                    FamilyMember? partner = FindChatPartner(m);
                    if (partner is null)
                    {
                        break;
                    }

                    Vector2 meet = partner.Position + Vector2.Normalize(m.Position - partner.Position + new Vector2(0.01f, 0.01f)) * 1.1f;
                    StartTask(m, ActivityId.Chat, null, -1, target: meet, partner: partner.Id);
                    if (partner.Task is null || partner.Task.Activity is ActivityId.Idle or ActivityId.Wander or ActivityId.Relax)
                    {
                        StartTask(partner, ActivityId.Chat, null, -1, target: partner.Position, partner: m.Id);
                    }

                    m.Task!.TargetYaw = MathF.Atan2(partner.Position.X - meet.X, partner.Position.Y - meet.Y);
                    ChatLine(m, partner);
                    return;
                }

            case ActivityId.PlayWithPet when State.Pets.Count > 0:
                {
                    Pet pet = State.Pets.OrderBy(p => Vector2.Distance(p.Position, m.Position)).First();
                    StartTask(m, ActivityId.PlayWithPet, null, -1, target: pet.Position + new Vector2(0.8f, 0.4f));
                    pet.State = PetState.Play;
                    pet.Target = pet.Position + new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f));
                    pet.StateTimer = 15f;
                    return;
                }

            case ActivityId.Wander:
                {
                    Vector2 target = m.Position;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 candidate = m.Position + new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f));
                        if (Rooms.Lot.Contains(candidate) && !Collision.Blocked(candidate, CharacterRadius))
                        {
                            target = candidate;
                            break;
                        }
                    }

                    StartTask(m, ActivityId.Wander, null, -1, target: target);
                    return;
                }

            case ActivityId.Swim:
                StartTask(m, ActivityId.Swim, null, -1, target: Rooms.Get(RoomId.Pool).Area.Center + new Vector2(0f, 2.6f));
                return;
            case ActivityId.Errand:
                SendAway(m, ActivityId.Errand, Hour + 1.25f);
                Say(m, Loc.T("Bahan makanan habis. Belanja dulu ya!", "We're out of food. Off to the shop!"));
                return;
            case ActivityId.Exercise:
                StartTask(m, ActivityId.Exercise, null, -1, target: new Vector2(4f, -10f));
                return;
            case ActivityId.Sleep when m.IsChild && m.Personality.Fears.Contains("dark") && !State.House.PowerOn:
                Say(m, Loc.T("Gelap sekali... aku takut.", "It's so dark... I'm scared."), m.Id == MemberId.OlderSister ? "os_scared" : null);
                break;
        }

        if (furniture is { } choice)
        {
            StartTask(m, activity, choice.Item, choice.Slot);
            if (activity == ActivityId.Sleep && m.Id == MemberId.Mother && Random.Chance(0.4f))
            {
                Say(m, Loc.T("Sudah malam, waktunya tidur.", "It's late, time for bed."), "mom_sleep");
            }
        }
        else
        {
            StartTask(m, activity == ActivityId.Idle ? ActivityId.Idle : activity, null, -1, target: m.Position);
        }

        MaybeArgue(m);
    }

    /// <summary>Furniture for an activity: nearest usable item with a free slot.</summary>
    public FurnitureChoice? FindFurniture(FamilyMember m, ActivityId activity, bool allowBusy = false)
    {
        FurnitureChoice? best = null;
        float bestScore = float.MaxValue;
        House house = State.House;
        foreach (FurnitureItem item in house.Furniture)
        {
            FurnitureDef def = item.Def;
            bool match = (activity == ActivityId.Repair && item.Broken && def.Slots.Length > 0)
                || def.Activities.Contains(activity)
                || (activity == ActivityId.Bbq && def.Id == "bbq")
                || (activity == ActivityId.Cook && def.Id == "bbq" && m.Task?.RecipeId == "satay");
            if (!match)
            {
                continue;
            }

            if (activity == ActivityId.Repair)
            {
                if (!item.Broken)
                {
                    continue;
                }
            }
            else if (item.Broken)
            {
                continue;
            }

            if (def.NeedsPower && !house.PowerOn && activity is not (ActivityId.Repair or ActivityId.Sleep or ActivityId.Homework or ActivityId.Draw))
            {
                continue;
            }

            if (house.Hazards.Any(h => h.Kind == HazardKind.Fire && h.Room == item.Room))
            {
                continue;
            }

            int slot = item.FreeSlot(m.Id);
            if (slot < 0)
            {
                if (!allowBusy)
                {
                    continue;
                }

                slot = 0;
            }

            float distance = Vector2.Distance(m.Position, item.ApproachPoint(slot));
            float preference = 0f;
            if (activity is ActivityId.Sleep or ActivityId.Nap)
            {
                preference = BedPreference(m, item);
                if (preference > 50f)
                {
                    continue;
                }
            }

            float score = distance + preference;
            if (score < bestScore)
            {
                bestScore = score;
                best = new FurnitureChoice(item, slot);
            }
        }

        return best;
    }

    /// <summary>Parents sleep in the double bed, the sisters in their own room once it exists.</summary>
    private float BedPreference(FamilyMember m, FurnitureItem bed) => (m.Id, bed.DefId) switch
    {
        (MemberId.Father or MemberId.Mother, "double-bed") => -20f,
        (MemberId.Father or MemberId.Mother, _) => 100f,
        (_, "double-bed") => 100f,
        (MemberId.OlderSister or MemberId.YoungerSister, _) when bed.Room == RoomId.GirlsBedroom => -15f,
        (MemberId.Player, _) when bed.Room == RoomId.KidsBedroom => -10f,
        _ => 0f,
    };

    private void ChatLine(FamilyMember m, FamilyMember partner)
    {
        if (State.Relationships.Arguing(m.Id, partner.Id))
        {
            if (Random.Chance(0.5f))
            {
                State.Relationships.Reconcile(m.Id, partner.Id);
                Say(m, Loc.T($"Maaf ya, {partner.Name}...", $"I'm sorry, {partner.Name}..."));
                Reconciled(m.Id, partner.Id);
            }

            return;
        }

        if (!Random.Chance(0.35f))
        {
            return;
        }

        // Memories colour conversations (design section 23).
        FamilyMemory? shared = State.Memories.Where(mem => mem.Involves(m.Id) && mem.Involves(partner.Id)).OrderByDescending(mem => mem.Importance).FirstOrDefault();
        if (shared is not null && Random.Chance(0.4f))
        {
            Say(m, Loc.T($"Ingat waktu \"{shared.Title}\"? Seru ya!", $"Remember \"{shared.Title}\"? That was great!"));
            return;
        }

        string line = (m.Id, Random.Range(0, 3)) switch
        {
            (MemberId.Father, 0) => Loc.T("Akhir pekan kita berkemah, yuk!", "Let's go camping this weekend!"),
            (MemberId.Father, _) => Loc.T("Ada yang rusak? Ayah bisa perbaiki!", "Anything broken? Dad can fix it!"),
            (MemberId.Mother, 0) => Loc.T("Kalian sudah makan?", "Have you eaten?"),
            (MemberId.Mother, _) => Loc.T("Ibu sayang kalian semua.", "I love you all."),
            (MemberId.OlderSister, 0) => Loc.T("Mau lihat gambarku?", "Want to see my drawing?"),
            (MemberId.OlderSister, _) => Loc.T("Aku mau jadi fotografer!", "I want to be a photographer!"),
            (MemberId.YoungerSister, 0) => Loc.T("Aku baru baca buku tentang dinosaurus!", "I just read a book about dinosaurs!"),
            (MemberId.YoungerSister, _) => Loc.T("Ayo bikin pancake lagi!", "Let's make pancakes again!"),
            _ => Loc.T("Ayo main!", "Let's play!"),
        };
        string? voice = (m.Id, line) switch
        {
            (MemberId.Mother, _) when line.Contains("sayang", StringComparison.Ordinal) || line.Contains("love", StringComparison.Ordinal) => "mom_love",
            (MemberId.OlderSister, _) when line.Contains("gambar", StringComparison.Ordinal) || line.Contains("drawing", StringComparison.Ordinal) => "os_greet",
            (MemberId.YoungerSister, _) when line.Contains("pancake", StringComparison.Ordinal) => "ys_pancake",
            _ => null,
        };
        Say(m, line, voice);
    }

    private void MaybeArgue(FamilyMember m)
    {
        if (m.Mood.Score > -15f || !Random.Chance(0.08f))
        {
            return;
        }

        FamilyMember? other = State.Members.FirstOrDefault(o => o.Id != m.Id && !o.Away && o.Mood.Score < 10f
            && Vector2.Distance(o.Position, m.Position) < 4f && !State.Relationships.Arguing(m.Id, o.Id));
        if (other is null)
        {
            return;
        }

        State.Relationships.StartArgument(m.Id, other.Id, Random.Range(60f, 180f));
        Say(m, Loc.T("Hmph! Kamu nyebelin!", "Hmph! You're so annoying!"));
        other.Mood.Add("argument", Loc.T($"Bertengkar dengan {m.Name}", $"Argued with {m.Name}"), -10, MoodKind.Annoyed, Now, 120);
        m.Mood.Add("argument", Loc.T($"Bertengkar dengan {other.Name}", $"Argued with {other.Name}"), -10, MoodKind.Annoyed, Now, 120);
        Bus.Notice(Loc.T($"{m.Name} dan {other.Name} bertengkar.", $"{m.Name} and {other.Name} are arguing."), "💢", NoticeKind.Warning);
        CreateMemory(Loc.T($"{m.Name} dan {other.Name} bertengkar", $"{m.Name} and {other.Name} argued"),
            Loc.T("Kadang keluarga juga bertengkar. Yang penting saling memaafkan.", "Families argue sometimes. What matters is forgiving each other."),
            MemoryKind.Argument, EmotionalOutcome.Sad, [m.Id, other.Id], LocationName(m), 0.6f, $"argue:{m.Id}:{other.Id}", photo: false);
    }

    private void Reconciled(MemberId a, MemberId b)
    {
        State.Relationships.Change(a, b, 3f);
        CreateMemory(Loc.T($"{FamilyNames.Short(a)} dan {FamilyNames.Short(b)} berbaikan", $"{FamilyNames.Short(a)} and {FamilyNames.Short(b)} made up"),
            Loc.T("Mereka saling minta maaf dan berpelukan.", "They apologised and hugged."),
            MemoryKind.Reconcile, EmotionalOutcome.Heartwarming, [a, b], LocationName(State.Member(a)), 1f, $"reconcile:{a}:{b}", photo: false);
    }
}
