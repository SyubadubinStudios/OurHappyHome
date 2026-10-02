using System.Numerics;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Economy;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.School;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

public sealed record InteractionOption(string Id, string Label, string Icon, Action Execute, bool Enabled = true, string? Reason = null);

/// <summary>Something near the player that can be interacted with, and what can be done.</summary>
public sealed record InteractionTarget(string Key, string Title, string Icon, Vector3 Position, float Distance, List<InteractionOption> Options);

public sealed partial class GameSession
{
    public const float InteractRange = 2.3f;

    /// <summary>Everything the controlled member can interact with nearby, nearest first.</summary>
    public List<InteractionTarget> GetInteractions()
    {
        FamilyMember me = Controlled;
        List<InteractionTarget> targets = [];
        Vector2 p = me.Position;

        if (Scenario is not null)
        {
            foreach (InteractionTarget t in Scenario.Targets(me))
            {
                if (t.Distance <= InteractRange + 0.7f)
                {
                    targets.Add(t);
                }
            }
        }

        foreach (FamilyMember other in State.Members)
        {
            if (other.Id == me.Id || other.Away)
            {
                continue;
            }

            float d = Vector2.Distance(other.Position, p);
            if (d <= InteractRange)
            {
                targets.Add(new InteractionTarget($"member:{other.Id}", other.Name, other.InDanger ? "🆘" : Mood.Emoji(other.Mood.Current),
                    new Vector3(other.Position.X, 1.9f, other.Position.Y), d, MemberOptions(me, other)));
            }
        }

        foreach (Pet pet in State.Pets)
        {
            float d = Vector2.Distance(pet.Position, p);
            if (d <= InteractRange)
            {
                targets.Add(new InteractionTarget($"pet:{pet.Id}", pet.Name, pet.Icon, new Vector3(pet.Position.X, 0.8f, pet.Position.Y), d, PetOptions(me, pet)));
            }
        }

        foreach (Hazard hazard in State.House.Hazards)
        {
            float d = Vector2.Distance(hazard.Position, p);
            if (d <= InteractRange + 0.5f && HazardOptions(me, hazard) is { Count: > 0 } options)
            {
                targets.Add(new InteractionTarget($"hazard:{hazard.Id}", HazardName(hazard.Kind), HazardIcon(hazard.Kind),
                    new Vector3(hazard.Position.X, 0.6f, hazard.Position.Y), d, options));
            }
        }

        foreach (FurnitureItem item in State.House.Furniture)
        {
            if (item.Def.Activities.Length == 0 && !item.Broken)
            {
                continue;
            }

            float d = Vector2.Distance(item.Bounds.Closest(p), p);
            if (d <= 1.4f)
            {
                List<InteractionOption> options = FurnitureOptions(me, item);
                if (options.Count > 0)
                {
                    targets.Add(new InteractionTarget($"furniture:{item.Uid}", item.Def.Name + (item.Broken ? Loc.T(" (rusak)", " (broken)") : ""),
                        item.Broken ? "🛠" : ActivityCatalog.Get(item.Def.Activities.FirstOrDefault()).Icon,
                        new Vector3(item.Position.X, item.Def.Height + 0.3f, item.Position.Y), d + 0.1f, options));
                }
            }
        }

        float doorDistance = Vector2.Distance(Rooms.FrontDoor, p);
        if (doorDistance <= 2.2f)
        {
            targets.Add(new InteractionTarget("front-door", Loc.T("Pintu Depan", "Front Door"), "🚪",
                new Vector3(Rooms.FrontDoor.X, 2.2f, Rooms.FrontDoor.Y), doorDistance + 0.2f, DoorOptions()));
        }

        foreach (Npc npc in Npcs)
        {
            float d = Vector2.Distance(npc.Position, p);
            if (d <= InteractRange && npc.Present(Hour))
            {
                targets.Add(new InteractionTarget($"npc:{npc.Id}", npc.Name, "👋", new Vector3(npc.Position.X, npc.Height + 0.3f, npc.Position.Y), d, NpcOptions(npc)));
            }
        }

        foreach (Place place in Map.Places)
        {
            float d = Vector2.Distance(place.Entrance, p);
            if (d <= 4.5f && PlaceOptions(place) is { Count: > 0 } options)
            {
                targets.Add(new InteractionTarget($"place:{place.Id}", place.Name, place.Icon, new Vector3(place.Entrance.X, 2.6f, place.Entrance.Y), d + 0.5f, options));
            }
        }

        foreach (TownFeature feature in Map.Features)
        {
            if (feature.Kind is not (FeatureKind.Campfire or FeatureKind.Tent or FeatureKind.FerrisWheel or FeatureKind.Carousel or FeatureKind.Pond or FeatureKind.Playground or FeatureKind.Sand))
            {
                continue;
            }

            float d = Vector2.Distance(feature.Area.Closest(p), p);
            if (d <= 2.5f && FeatureOptions(feature) is { Count: > 0 } options)
            {
                targets.Add(new InteractionTarget($"feature:{feature.Kind}:{feature.Area.Center}", FeatureName(feature.Kind), "✨",
                    new Vector3(feature.Area.Closest(p).X, 1.5f, feature.Area.Closest(p).Y), d + 0.3f, options));
            }
        }

        targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return targets;
    }

    // ------------------------------------------------------------ family

    private List<InteractionOption> MemberOptions(FamilyMember me, FamilyMember other)
    {
        List<InteractionOption> o = [];
        switch (other.Safety)
        {
            case SafetyState.NeedsHelp:
                o.Add(new("help", Loc.T("Tolong & ajak ke tempat aman", "Help & lead to safety"), "🤝", () => Rescue(me, other)));
                break;
            case SafetyState.Trapped:
                bool strong = me.Stamina.CanDoDemanding;
                o.Add(new("free", Loc.T("Bebaskan (butuh tenaga)", "Free them (needs stamina)"), "💪", () => Rescue(me, other), strong,
                    strong ? null : Loc.T("Terlalu lelah! Minta bantuan Ayah.", "Too exhausted! Ask Dad for help.")));
                break;
            case SafetyState.Down:
                bool canAid = me.Skills[SkillKind.FirstAid] >= 1f || State.Inventory.Has("bandage");
                o.Add(new("aid", Loc.T("Pertolongan pertama", "First aid"), "🩹", () => Rescue(me, other, firstAid: true), canAid,
                    canAid ? null : Loc.T("Butuh perban atau keahlian P3K", "Needs bandages or First Aid skill")));
                o.Add(new("support", Loc.T("Papah ke tempat aman", "Support them to safety"), "🫂", () => Rescue(me, other), me.Stamina.CanDoDemanding));
                break;
            case SafetyState.Following:
                o.Add(new("wait", Loc.T("Tunggu di sini", "Wait here"), "✋", () =>
                {
                    other.Safety = Scenario is null ? SafetyState.Normal : SafetyState.Safe;
                    other.FollowTarget = null;
                }));
                break;
        }

        if (other.InDanger)
        {
            return o;
        }

        o.Add(new("talk", Loc.T("Ngobrol", "Talk"), "💬", () => Talk(me, other)));
        o.Add(new("hug", Loc.T("Peluk", "Hug"), "🤗", () => Hug(me, other)));
        if (other.Mood.Current is MoodKind.Scared or MoodKind.Sad)
        {
            o.Add(new("comfort", Loc.T("Hibur", "Comfort"), "💗", () => StartComfort(me, other)));
        }

        o.Add(new("play", Loc.T("Main bersama", "Play together"), "🎈", () => PlayTogether(me, other), me.Stamina.CanDoDemanding));
        o.Add(new("gift", Loc.T("Beri hadiah", "Give a gift"), "🎁", () => Bus.Publish(new OpenPanelEvent("gift", other.Id.ToString())),
            State.Inventory.Counts.Any(kv => kv.Value > 0 && ItemCatalog.Exists(kv.Key) && ItemCatalog.Get(kv.Key).Category == ItemCategory.Gift)));
        if (FamilyNames.IsAdult(other.Id) || other.Skills[SkillKind.Cooking] >= 1.5f)
        {
            o.Add(new("ask-cook", Loc.T("Minta tolong masak", "Ask to cook"), "🍳", () => AskTo(other, ActivityId.Cook)));
        }

        if (State.House.Furniture.Any(f => f.Broken))
        {
            o.Add(new("ask-repair", Loc.T("Minta tolong perbaiki", "Ask to repair"), "🔧", () => AskTo(other, ActivityId.Repair)));
        }

        bool following = other.Safety == SafetyState.Following || State.Party.Contains(other.Id);
        o.Add(new("follow", following ? Loc.T("Berhenti ikut", "Stop following") : Loc.T("Ajak ikut", "Come with me"), "👣", () => ToggleFollow(me, other)));
        o.Add(new("control", Loc.T($"Kendalikan {other.Name}", $"Play as {other.Name}"), "🎮", () => SwitchControl(other.Id)));
        return o;
    }

    public void Talk(FamilyMember me, FamilyMember other)
    {
        CancelTask(other);
        other.Yaw = MathF.Atan2(me.Position.X - other.Position.X, me.Position.Y - other.Position.Y);
        me.Yaw = MathF.Atan2(other.Position.X - me.Position.X, other.Position.Y - me.Position.Y);
        StartTask(other, ActivityId.Chat, null, -1, target: other.Position, partner: me.Id, minutes: 8);
        StartTask(me, ActivityId.Chat, null, -1, target: me.Position, partner: other.Id, minutes: 8, fromPlayer: true);
        me.Task!.TargetYaw = me.Yaw;
        other.Task!.TargetYaw = other.Yaw;
        State.Relationships.Change(me.Id, other.Id, 2f);
        me.Needs.Add(NeedKind.Social, 12);
        other.Needs.Add(NeedKind.Social, 12);
        if (State.Relationships.Arguing(me.Id, other.Id))
        {
            State.Relationships.Reconcile(me.Id, other.Id);
            Say(me, Loc.T("Maafin aku ya...", "I'm sorry..."));
            Say(other, Loc.T("Iya, aku juga minta maaf.", "Me too, I'm sorry."));
            Reconciled(me.Id, other.Id);
            return;
        }

        ChatLine(other, me);
    }

    public void Hug(FamilyMember me, FamilyMember other)
    {
        State.Relationships.Change(me.Id, other.Id, 3f);
        State.AddStat(Stat.Hugs);
        other.Mood.Add("hug", Loc.T("Dapat pelukan", "Got a hug"), 8, MoodKind.Happy, Now, 120);
        me.Mood.Add("hug", Loc.T("Memeluk keluarga", "Hugged family"), 8, MoodKind.Happy, Now, 120);
        other.Mood.Remove("thunder");
        Bus.Effect(EffectKind.Hearts, (me.Position + other.Position) / 2f, 1.8f);
        Bus.Sound("hug");
        if (other.Id == MemberId.Mother && me.Id == MemberId.Player)
        {
            Say(me, Loc.T("Aku sayang Ibu!", "I love you, Mom!"), "boy_love");
        }
    }

    private void StartComfort(FamilyMember me, FamilyMember other)
    {
        StartTask(me, ActivityId.Comfort, null, -1, target: me.Position, partner: other.Id, fromPlayer: true);
        Hug(me, other);
    }

    private void PlayTogether(FamilyMember me, FamilyMember other)
    {
        foreach (FamilyMember m in new[] { me, other })
        {
            m.Needs.Add(NeedKind.Fun, 20);
            m.Stamina.Value -= 8;
        }

        State.Relationships.Change(me.Id, other.Id, 4f);
        StartTask(other, ActivityId.Play, null, -1, target: other.Position, minutes: 15);
        Bus.Effect(EffectKind.Stars, other.Position, 1.6f);
        Say(other, other.IsChild ? Loc.T("Yeay! Seru banget!", "Yay! So much fun!") : Loc.T("Ayo! Siapa takut!", "You're on!"), other.Id == MemberId.OlderSister ? "os_cheer" : other.Id == MemberId.YoungerSister ? "ys_cheer" : null);
        if (Random.Chance(0.3f))
        {
            CreateMemory(Loc.T($"Bermain bersama {other.Name}", $"Playing with {other.Name}"),
                Loc.T($"{me.Name} dan {other.Name} tertawa lepas saat bermain.", $"{me.Name} and {other.Name} laughed and played together."),
                MemoryKind.Play, EmotionalOutcome.Joyful, [me.Id, other.Id], LocationName(me), 1.2f, $"play:{other.Id}");
        }
    }

    /// <summary>Asks another member to do something; they agree if they can.</summary>
    public void AskTo(FamilyMember other, ActivityId activity)
    {
        if (other.Id == State.Controlled)
        {
            return;
        }

        if (FindFurniture(other, activity) is { } choice)
        {
            StartTask(other, activity, choice.Item, choice.Slot);
            Say(other, activity == ActivityId.Repair && other.Id == MemberId.Father ? Loc.T("Tenang, biar Ayah yang perbaiki!", "Don't worry, Dad will fix it!") : Loc.T("Oke, siap!", "Okay, on it!"),
                activity == ActivityId.Repair && other.Id == MemberId.Father ? "dad_fix" : null);
            State.Relationships.Change(State.Controlled, other.Id, 0.5f);
        }
        else
        {
            Say(other, Loc.T("Hmm, tidak bisa sekarang.", "Hmm, I can't right now."));
        }
    }

    private void ToggleFollow(FamilyMember me, FamilyMember other)
    {
        if (State.Party.Remove(other.Id) || other.Safety == SafetyState.Following)
        {
            other.Safety = SafetyState.Normal;
            other.FollowTarget = null;
            Say(other, Loc.T("Oke, aku di sini saja.", "Okay, I'll stay here."));
            return;
        }

        State.Party.Add(other.Id);
        other.Safety = SafetyState.Following;
        other.FollowTarget = me.Id;
        Say(other, Loc.T("Ayo! Aku ikut!", "Let's go! I'm coming!"));
    }

    /// <summary>Rescue system: help, free or give first aid, then lead the member to safety.</summary>
    public void Rescue(FamilyMember me, FamilyMember other, bool firstAid = false)
    {
        switch (other.Safety)
        {
            case SafetyState.Trapped:
                if (!me.Stamina.TrySpend(25f))
                {
                    Bus.Notice(Loc.T("Kamu kelelahan! Istirahat atau minta bantuan Ayah.", "You're exhausted! Rest or ask Dad for help."), "😮‍💨", NoticeKind.Warning);
                    return;
                }

                Bus.Effect(EffectKind.Dust, other.Position, 0.8f);
                Bus.Sound("debris");
                break;
            case SafetyState.Down when firstAid:
                if (me.Skills[SkillKind.FirstAid] < 1f)
                {
                    State.Inventory.Take("bandage");
                }

                other.Needs.Add(NeedKind.Health, 25f);
                Practice(me, SkillKind.FirstAid, 0.5f);
                break;
            case SafetyState.Down:
                if (!me.Stamina.TrySpend(15f))
                {
                    return;
                }

                break;
        }

        other.Safety = SafetyState.Following;
        other.FollowTarget = me.Id;
        other.RescueTimer = 0f;
        Bus.Notice(Loc.T($"{other.Name} ikut kamu. Bawa ke tempat aman!", $"{other.Name} is with you. Get to safety!"), "🤝", NoticeKind.Good);
        Say(other, Loc.T("Terima kasih! Ayo pergi!", "Thank you! Let's go!"),
            other.Id switch { MemberId.YoungerSister => "ys_thanks", MemberId.OlderSister => "os_thanks", MemberId.Mother => "mom_thanks", MemberId.Father => "dad_thanks", _ => null });
        Say(me, Loc.T("Ketemu! Ayo ke tempat aman!", "Found you! Let's get to safety!"), me.Id == MemberId.Player ? "boy_found" : null);
        Scenario?.OnRescued(me, other);
    }

    /// <summary>Tab: control another family member (they keep their AI while not controlled).</summary>
    public void SwitchControl(MemberId? to = null)
    {
        MemberId target = to ?? NextControllable();
        if (target == State.Controlled || State.Member(target).Away || State.Member(target).InDanger)
        {
            return;
        }

        FamilyMember previous = Controlled;
        CancelTask(previous);
        State.Controlled = target;
        CancelTask(State.Member(target));
        State.Member(target).Safety = SafetyState.Normal;
        Bus.Notice(Loc.T($"Sekarang kamu bermain sebagai {State.Member(target).Name}", $"Now playing as {State.Member(target).Name}"), "🎮", NoticeKind.Info);
    }

    private MemberId NextControllable()
    {
        int start = (int)State.Controlled;
        for (int i = 1; i <= 5; i++)
        {
            MemberId id = (MemberId)((start + i) % 5);
            FamilyMember m = State.Member(id);
            if (!m.Away && !m.InDanger && Vector2.Distance(m.Position, Controlled.Position) < 200f)
            {
                return id;
            }
        }

        return State.Controlled;
    }

    // --------------------------------------------------------------- gifts

    public void GiveGift(MemberId to, string itemId)
    {
        FamilyMember other = State.Member(to);
        if (!State.Inventory.Take(itemId))
        {
            return;
        }

        ItemDef item = ItemCatalog.Get(itemId);
        bool loved = item.Tags.Any(t => other.Personality.Likes.Contains(t));
        float amount = loved ? 10f : 4f;
        State.Relationships.Change(State.Controlled, to, amount);
        other.Mood.Add($"gift:{itemId}", Loc.T($"Dapat hadiah {item.Name}", $"Got a {item.Name}"), loved ? 18 : 8, loved ? MoodKind.Excited : MoodKind.Happy, Now, 300);
        State.AddStat(Stat.Gifts);
        Bus.Effect(EffectKind.Hearts, other.Position, 1.8f, loved ? 1.6f : 1f);
        Say(other, loved ? Loc.T($"Wah, {item.Name}! Aku suka sekali! Terima kasih!", $"Wow, a {item.Name}! I love it! Thank you!") : Loc.T("Terima kasih ya!", "Thank you!"));
        CreateMemory(Loc.T($"Hadiah untuk {other.Name}", $"A gift for {other.Name}"),
            Loc.T($"{Controlled.Name} memberi {other.Name} {item.Name}.", $"{Controlled.Name} gave {other.Name} a {item.Name}."),
            MemoryKind.Gift, loved ? EmotionalOutcome.Heartwarming : EmotionalOutcome.Joyful, [State.Controlled, to], LocationName(other), loved ? 2f : 1f, $"gift:{to}:{itemId}");
    }

    // ----------------------------------------------------------------- pets

    private List<InteractionOption> PetOptions(FamilyMember me, Pet pet) =>
    [
        new("pet", Loc.T("Elus", "Pet"), "🤚", () =>
        {
            pet.Happiness = MathF.Min(100, pet.Happiness + 10);
            pet.Trust = MathF.Min(100, pet.Trust + 3);
            me.Needs.Add(NeedKind.Fun, 6);
            me.Mood.Add("pet", Loc.T($"Mengelus {pet.Name}", $"Petted {pet.Name}"), 6, MoodKind.Happy, Now, 90);
            Bus.Effect(EffectKind.Hearts, pet.Position, 0.9f);
            Bus.Sound(pet.Kind == PetKind.Dog ? "bark-happy" : "meow");
            Practice(me, SkillKind.Animals, 0.15f);
        }),
        new("feed", Loc.T("Beri makan", "Feed"), "🦴", () =>
        {
            if (State.Inventory.Take("pet-food"))
            {
                pet.Hunger = 100;
                pet.Trust = MathF.Min(100, pet.Trust + 5);
                Practice(me, SkillKind.Animals, 0.3f);
                Bus.Notice(Loc.T($"{pet.Name} makan dengan lahap!", $"{pet.Name} gobbles it up!"), pet.Icon, NoticeKind.Good);
            }
        }, State.Inventory.Has("pet-food"), Loc.T("Beli makanan hewan di supermarket", "Buy pet food at the supermarket")),
        new("fetch", Loc.T("Main lempar bola", "Play fetch"), "🎾", () =>
        {
            pet.State = PetState.Play;
            pet.StateTimer = 10f;
            pet.Target = me.Position + (new Vector2(MathF.Sin(me.Yaw), MathF.Cos(me.Yaw)) * 6f);
            pet.Happiness = MathF.Min(100, pet.Happiness + 15);
            pet.Energy = MathF.Max(0, pet.Energy - 10);
            me.Needs.Add(NeedKind.Fun, 12);
            Practice(me, SkillKind.Animals, 0.3f);
        }, me.Stamina.CanDoDemanding),
    ];

    public bool AdoptPet(PetKind kind, string name)
    {
        long cost = Pet.AdoptionCost(kind);
        if (!State.Wallet.Spend(cost, Loc.T($"Adopsi {name}", $"Adopted {name}"), Clock.DayIndex))
        {
            Bus.Notice(Loc.T("Uangnya belum cukup.", "Not enough money yet."), "💸", NoticeKind.Warning);
            return false;
        }

        Pet pet = new() { Id = State.NextPetId++, Kind = kind, Name = string.IsNullOrWhiteSpace(name) ? "Brownie" : name.Trim(), AdoptedDay = Clock.DayIndex, Position = Rooms.FrontDoor + new Vector2(1f, 2f) };
        State.Pets.Add(pet);
        State.AddStat(Stat.PetsAdopted);
        State.Inventory.Add("pet-food", 3);
        if (kind == PetKind.Dog && !State.House.Furniture.Any(f => f.DefId == "dog-bed"))
        {
            State.House.Place("dog-bed", RoomId.LivingRoom, new Vector2(-1.0f, 4.9f), 0);
        }

        CreateMemory(Loc.T($"Selamat datang, {pet.Name}!", $"Welcome home, {pet.Name}!"),
            Loc.T($"Keluarga mengadopsi {pet.KindName.ToLowerInvariant()} bernama {pet.Name}. Semua anak berebut memeluknya!", $"The family adopted a {pet.KindName.ToLowerInvariant()} named {pet.Name}. The kids all wanted to hug it!"),
            MemoryKind.Pet, EmotionalOutcome.Joyful, FamilyNames.All, Rooms.Name(RoomId.LivingRoom), 3f, $"pet:{pet.Id}");
        return true;
    }

    // ------------------------------------------------------------ furniture

    private List<InteractionOption> FurnitureOptions(FamilyMember me, FurnitureItem item)
    {
        List<InteractionOption> o = [];
        if (item.Broken)
        {
            o.Add(new("repair", Loc.T("Perbaiki", "Repair"), "🔧", () => PlayerUse(me, item, ActivityId.Repair), me.Stamina.CanDoDemanding,
                Loc.T("Terlalu lelah", "Too tired")));
            if (FamilyNames.IsChild(me.Id))
            {
                o.Add(new("ask-dad", Loc.T("Minta Ayah memperbaiki", "Ask Dad to fix it"), "👨", () => AskTo(State.Member(MemberId.Father), ActivityId.Repair),
                    !State.Member(MemberId.Father).Away));
            }

            return o;
        }

        foreach (ActivityId activity in item.Def.Activities)
        {
            ActivityDef def = ActivityCatalog.Get(activity);
            if (!def.AllowedFor(me.Id) || activity == ActivityId.Repair)
            {
                continue;
            }

            bool powered = !item.Def.NeedsPower || State.House.PowerOn;
            switch (activity)
            {
                case ActivityId.Cook:
                    o.Add(new("cook", Loc.T("Masak...", "Cook..."), "🍳", () => Bus.Publish(new OpenPanelEvent("recipes")), powered, Loc.T("Listrik padam", "Power is out")));
                    break;
                case ActivityId.Eat:
                    o.Add(new("eat", def.Name, def.Icon, () => PlayerEat(me, item), State.MealServings > 0, Loc.T("Belum ada makanan. Masak dulu!", "No food yet. Cook something!")));
                    break;
                case ActivityId.SellLemonade:
                    o.Add(new("lemonade", def.Name, def.Icon, () => PlayerUse(me, item, activity),
                        State.Inventory.Has("fruit") && State.Inventory.Has("sugar"), Loc.T("Butuh buah lemon & gula", "Needs lemons & sugar")));
                    break;
                case ActivityId.FixPower:
                    o.Add(new("fuse", def.Name, def.Icon, () => PlayerUse(me, item, activity), !State.House.PowerOn, Loc.T("Listrik menyala normal", "Power is fine")));
                    break;
                case ActivityId.CheckCameras:
                    o.Add(new("cameras", def.Name, def.Icon, () => PlayerUse(me, item, activity), powered, Loc.T("Listrik padam", "Power is out")));
                    o.Add(new("alarm", State.House.AlarmActive ? Loc.T("Matikan alarm", "Disarm alarm") : Loc.T("Aktifkan alarm", "Arm the alarm"), "🚨", () =>
                    {
                        State.House.AlarmActive = !State.House.AlarmActive;
                        Bus.Sound("beep");
                        Scenario?.OnSafetyChanged();
                    }, powered));
                    break;
                default:
                    o.Add(new(activity.ToString(), def.Name, def.Icon, () => PlayerUse(me, item, activity), powered || activity is ActivityId.Sleep or ActivityId.Nap or ActivityId.Homework or ActivityId.Draw,
                        Loc.T("Listrik padam", "Power is out")));
                    break;
            }
        }

        if (item.Def.Activities.Contains(ActivityId.Cook))
        {
            o.Add(new("drink", Loc.T("Buat cokelat hangat untuk semua", "Make hot chocolate for everyone"), "☕", () => PlayerUse(me, item, ActivityId.MakeDrink),
                State.Inventory.Has("milk"), Loc.T("Butuh susu", "Needs milk")));
        }

        return o;
    }

    public void PlayerUse(FamilyMember me, FurnitureItem item, ActivityId activity)
    {
        int slot = item.FreeSlot(me.Id);
        if (slot < 0)
        {
            slot = 0;
            if (item.Occupants.Length > 0 && item.Occupants[0].Member is { } other && other != me.Id)
            {
                Bus.Notice(Loc.T("Sedang dipakai.", "Someone is using it."), "⏳", NoticeKind.Info);
                return;
            }
        }

        StartTask(me, activity, item, slot, fromPlayer: true);
    }

    private void PlayerEat(FamilyMember me, FurnitureItem table)
    {
        PreparedFood? food = State.Pantry.Where(p => !p.Recipe.IsDrink && p.Servings > 0).OrderByDescending(p => p.Quality).FirstOrDefault();
        if (food is null)
        {
            return;
        }

        int slot = table.FreeSlot(me.Id);
        if (slot < 0)
        {
            return;
        }

        food.Servings--;
        State.Pantry.RemoveAll(p => p.Servings <= 0);
        StartTask(me, ActivityId.Eat, table, slot, fromPlayer: true, tag: $"quality:{food.Quality}", recipe: food.RecipeId);

        // Calling the family to the table.
        if (IsMealTime(Hour))
        {
            foreach (FamilyMember m in State.Members.Where(m => m.Id != me.Id && !m.Away && !m.InDanger && m.Task?.Activity is not (ActivityId.Eat or ActivityId.Sleep)))
            {
                if (State.MealServings > 0 && FindFurniture(m, ActivityId.Eat) is { } seat && m.Needs[NeedKind.Hunger] < 90)
                {
                    PreparedFood? f = State.Pantry.Where(p => !p.Recipe.IsDrink && p.Servings > 0).OrderByDescending(p => p.Quality).FirstOrDefault();
                    if (f is null)
                    {
                        break;
                    }

                    f.Servings--;
                    State.Pantry.RemoveAll(p => p.Servings <= 0);
                    StartTask(m, ActivityId.Eat, seat.Item, seat.Slot, tag: $"quality:{f.Quality}", recipe: f.RecipeId);
                }
            }
        }
    }

    // --------------------------------------------------------- door & safety

    private List<InteractionOption> DoorOptions()
    {
        House h = State.House;
        List<InteractionOption> o =
        [
            new("lock", h.DoorsLocked ? Loc.T("Buka kunci pintu", "Unlock doors") : Loc.T("Kunci semua pintu", "Lock all doors"), h.DoorsLocked ? "🔓" : "🔒", () =>
            {
                h.DoorsLocked = !h.DoorsLocked;
                Bus.Sound("lock");
                Scenario?.OnSafetyChanged();
            }),
            new("outside-lights", h.ExteriorLightsOn ? Loc.T("Matikan lampu luar", "Turn off outside lights") : Loc.T("Nyalakan lampu luar", "Turn on outside lights"), "💡", () =>
            {
                h.ExteriorLightsOn = !h.ExteriorLightsOn;
                Bus.Sound("switch");
                Scenario?.OnSafetyChanged();
            }, h.PowerOn, Loc.T("Listrik padam", "Power is out")),
            new("windows", h.WindowsClosed ? Loc.T("Buka jendela", "Open windows") : Loc.T("Tutup semua jendela", "Close all windows"), "🪟", () =>
            {
                h.WindowsClosed = !h.WindowsClosed;
                Bus.Sound("window");
                Scenario?.OnSafetyChanged();
            }),
        ];
        return o;
    }

    // ---------------------------------------------------------------- hazards

    private List<InteractionOption> HazardOptions(FamilyMember me, Hazard hazard)
    {
        List<InteractionOption> o = [];
        switch (hazard.Kind)
        {
            case HazardKind.Fire:
                bool extinguisher = State.House.Furniture.Any(f => f.DefId == "extinguisher");
                o.Add(new("extinguish", Loc.T("Semprot alat pemadam", "Use the fire extinguisher"), "🧯", () =>
                {
                    if (!me.Stamina.TrySpend(12f))
                    {
                        Bus.Notice(Loc.T("Terlalu lelah!", "Too exhausted!"), "😮‍💨", NoticeKind.Warning);
                        return;
                    }

                    hazard.Intensity -= FamilyNames.IsAdult(me.Id) ? 0.45f : 0.3f;
                    Bus.Effect(EffectKind.Smoke, hazard.Position, 1.0f, 1.4f);
                    Bus.Sound("extinguisher");
                    Practice(me, SkillKind.Protection, 0.4f);
                    if (hazard.Intensity <= 0f)
                    {
                        State.House.Hazards.Remove(hazard);
                        State.House.AddHazard(HazardKind.Smoke, hazard.Position, 0.5f);
                        Scenario?.OnFireOut(me);
                    }
                }, extinguisher, Loc.T("Tidak ada alat pemadam! Keluar dari rumah!", "No extinguisher! Get out of the house!")));
                break;
            case HazardKind.Puddle or HazardKind.Flour or HazardKind.BrokenGlass or HazardKind.Debris:
                o.Add(new("clean", Loc.T("Bersihkan", "Clean up"), "🧹", () => StartTask(me, ActivityId.Clean, null, -1, target: me.Position, fromPlayer: true, minutes: 8)));
                break;
            case HazardKind.Flood:
                o.Add(new("sandbag", Loc.T("Pasang karung pasir", "Place sandbags"), "🧱", () =>
                {
                    if (State.Inventory.Take("sandbag") && me.Stamina.TrySpend(10f))
                    {
                        hazard.Intensity -= 0.35f;
                        Bus.Effect(EffectKind.Splash, hazard.Position, 0.4f);
                        if (hazard.Intensity <= 0f)
                        {
                            State.House.Hazards.Remove(hazard);
                        }

                        Scenario?.OnSafetyChanged();
                    }
                }, State.Inventory.Has("sandbag"), Loc.T("Beli karung pasir di supermarket", "Buy sandbags at the supermarket")));
                break;
        }

        return o;
    }

    public static string HazardName(HazardKind kind) => kind switch
    {
        HazardKind.Puddle => Loc.T("Genangan air", "Water puddle"),
        HazardKind.Fire => Loc.T("Api!", "Fire!"),
        HazardKind.Smoke => Loc.T("Asap", "Smoke"),
        HazardKind.BrokenGlass => Loc.T("Pecahan kaca", "Broken glass"),
        HazardKind.Debris => Loc.T("Puing-puing", "Debris"),
        HazardKind.Flood => Loc.T("Air banjir", "Flood water"),
        _ => Loc.T("Tumpahan tepung", "Spilled flour"),
    };

    public static string HazardIcon(HazardKind kind) => kind switch
    {
        HazardKind.Puddle or HazardKind.Flood => "💧",
        HazardKind.Fire => "🔥",
        HazardKind.Smoke => "💨",
        HazardKind.BrokenGlass => "🪟",
        HazardKind.Debris => "🪵",
        _ => "🌾",
    };

    // ------------------------------------------------------------- neighbours

    private List<InteractionOption> NpcOptions(Npc npc)
    {
        List<InteractionOption> o =
        [
            new("talk", Loc.T("Sapa & ngobrol", "Say hello"), "👋", () =>
            {
                bool first = State.NeighboursMet.Add(npc.Id);
                if (first)
                {
                    State.AddStat(Stat.NeighboursMet);
                    CreateMemory(Loc.T($"Berkenalan dengan {npc.Name}", $"Meeting {npc.Name}"),
                        Loc.T($"{Controlled.Name} berkenalan dengan {npc.Name}, {npc.Role.ToLowerInvariant()}.", $"{Controlled.Name} met {npc.Name}, our {npc.Role.ToLowerInvariant()}."),
                        MemoryKind.Play, EmotionalOutcome.Joyful, [State.Controlled], LocationName(Controlled), 1f, $"npc:{npc.Id}", photo: false);
                }

                string line = npc.Line(Random.Range(0, 3));
                Bus.Publish(new SpeechEvent(null, npc.Name, line));
                Controlled.Needs.Add(NeedKind.Social, 10);
            }),
        ];

        switch (npc.Id)
        {
            case "grandma":
                o.Add(new("cookies", Loc.T("Terima kue dari Nenek", "Accept Grandma's cookies"), "🍪", () =>
                {
                    State.Inventory.Add("snack", 3);
                    Controlled.Needs.Add(NeedKind.Hunger, 15);
                    Bus.Notice(Loc.T("Nenek Sari memberi kue kering!", "Grandma Sari gave you cookies!"), "🍪", NoticeKind.Good);
                    State.Flags.Add($"cookies:{Clock.DayIndex}");
                }, !State.Flag($"cookies:{Clock.DayIndex}")));
                break;
            case "budi":
                o.Add(new("help-budi", Loc.T("Bantu cat pagar (Rp 40.000)", "Help paint the fence (Rp 40,000)"), "🖌", () =>
                {
                    if (!Controlled.Stamina.TrySpend(20f))
                    {
                        return;
                    }

                    Clock.Advance(45);
                    State.Wallet.Earn(40_000, Loc.T("Bantu Pak Budi", "Helped Mr. Budi"), Clock.DayIndex, Controlled.IsChild);
                    Practice(Controlled, SkillKind.Building, 0.5f);
                    State.Flags.Add($"budi:{Clock.DayIndex}");
                    Bus.Notice(Loc.T("Pak Budi berterima kasih: +Rp 40.000", "Mr. Budi says thanks: +Rp 40,000"), "🖌", NoticeKind.Money);
                }, !State.Flag($"budi:{Clock.DayIndex}") && Controlled.Stamina.CanDoDemanding));
                break;
            case "dimas":
                o.Add(new("football", Loc.T("Main bola bersama", "Play football together"), "⚽", () =>
                {
                    if (!Controlled.Stamina.TrySpend(15f))
                    {
                        return;
                    }

                    Clock.Advance(30);
                    Controlled.Needs.Add(NeedKind.Fun, 35);
                    Controlled.Needs.Add(NeedKind.Social, 25);
                    Practice(Controlled, SkillKind.Sports, 0.8f);
                    Bus.Effect(EffectKind.Stars, Controlled.Position, 1.8f);
                }, Controlled.Stamina.CanDoDemanding));
                break;
        }

        return o;
    }

    // ----------------------------------------------------------------- places

    private List<InteractionOption> PlaceOptions(Place place)
    {
        List<InteractionOption> o = [];
        FamilyMember me = Controlled;
        int people = 1 + State.Party.Count;
        switch (place.Id)
        {
            case PlaceId.School:
                bool open = Date.IsSchoolDay && Hour is >= 6.5f and < 13f;
                bool done = State.School.LessonDay == Clock.DayIndex;
                o.Add(new("lessons", Loc.T("Masuk kelas", "Go to class"), "🏫", () =>
                {
                    PendingMiniGame = new MiniGameRequest("school", "");
                    Bus.Publish(PendingMiniGame);
                }, open && !done && FamilyNames.IsChild(me.Id), done ? Loc.T("Pelajaran hari ini sudah selesai", "Today's lessons are done") : Loc.T("Sekolah buka Senin-Jumat 06:30-13:00", "School runs Mon-Fri 06:30-13:00")));
                break;
            case PlaceId.Supermarket:
                o.Add(new("shop", Loc.T("Belanja", "Shop"), "🛒", () => Bus.Publish(new OpenPanelEvent("shop", ItemCatalog.Supermarket)), Hour is >= 7f and < 22f, Loc.T("Toko tutup", "Closed")));
                break;
            case PlaceId.Mall:
                o.Add(new("shop", Loc.T("Belanja hadiah & hobi", "Shop gifts & hobbies"), "🛍", () => Bus.Publish(new OpenPanelEvent("shop", ItemCatalog.Mall)), Hour is >= 9f and < 22f, Loc.T("Mal tutup", "Closed")));
                o.Add(new("furniture", Loc.T("Toko perabot", "Furniture store"), "🛋", () => Bus.Publish(new OpenPanelEvent("build")), Hour is >= 9f and < 22f));
                o.Add(new("pets", Loc.T("Adopsi hewan peliharaan", "Adopt a pet"), "🐾", () => Bus.Publish(new OpenPanelEvent("adopt")), Hour is >= 9f and < 20f));
                break;
            case PlaceId.Clinic:
                long cost = 100_000;
                o.Add(new("doctor", Loc.T($"Periksa ke dokter ({Loc.Money(cost)})", $"See the doctor ({Loc.Money(cost)})"), "🩺", () =>
                {
                    if (!State.Wallet.Spend(cost, Loc.T("Biaya klinik", "Clinic visit"), Clock.DayIndex))
                    {
                        return;
                    }

                    foreach (FamilyMember m in State.Members.Where(m => m.Id == me.Id || State.Party.Contains(m.Id) || m.Sick))
                    {
                        m.Sick = false;
                        m.Needs[NeedKind.Health] = 100f;
                    }

                    Bus.Notice(Loc.T("Dokter memeriksa semua. Semua sehat kembali!", "The doctor checked everyone. All healthy again!"), "🩺", NoticeKind.Good);
                }, State.Wallet.CanAfford(cost)));
                break;
            case PlaceId.Restaurant:
                long meal = 30_000L * people;
                o.Add(new("eat-out", Loc.T($"Makan bakso ({Loc.Money(meal)})", $"Eat meatball soup ({Loc.Money(meal)})"), "🍜", () =>
                {
                    if (!State.Wallet.Spend(meal, Loc.T("Makan di restoran", "Restaurant meal"), Clock.DayIndex))
                    {
                        return;
                    }

                    Clock.Advance(40);
                    foreach (FamilyMember m in State.Members.Where(m => m.Id == me.Id || State.Party.Contains(m.Id)))
                    {
                        m.Needs[NeedKind.Hunger] = 100f;
                        m.Needs.Add(NeedKind.Social, 20);
                        m.Mood.Add("restaurant", Loc.T("Makan di luar", "Ate out"), 10, MoodKind.Happy, Now, 240);
                    }

                    if (people >= 3)
                    {
                        State.AddStat(Stat.FamilyMeals);
                        CreateMemory(Loc.T("Makan malam di warung bakso", "Dinner at the meatball shop"), Loc.T("Bakso hangat dan cerita seru di meja makan.", "Warm meatball soup and fun stories at the table."),
                            MemoryKind.Meal, EmotionalOutcome.Joyful, [me.Id, .. State.Party], place.Name, 1.5f, "restaurant");
                    }
                }, State.Wallet.CanAfford(meal)));
                break;
            case PlaceId.Arcade:
                o.Add(new("arcade", Loc.T("Main game (Rp 10.000)", "Play a game (Rp 10,000)"), "🕹", () =>
                {
                    if (State.Wallet.Spend(10_000, Loc.T("Koin arkade", "Arcade tokens"), Clock.DayIndex))
                    {
                        PendingMiniGame = new MiniGameRequest("arcade", "");
                        Bus.Publish(PendingMiniGame);
                    }
                }, State.Wallet.CanAfford(10_000)));
                break;
            case PlaceId.Home:
                o.Add(new("map", Loc.T("Buka peta kota", "Open the town map"), "🗺", () => Bus.Publish(new OpenPanelEvent("map"))));
                break;
        }

        return o;
    }

    private List<InteractionOption> FeatureOptions(TownFeature feature)
    {
        FamilyMember me = Controlled;
        List<MemberId> group = [me.Id, .. State.Party];
        List<InteractionOption> o = [];
        switch (feature.Kind)
        {
            case FeatureKind.Campfire:
                o.Add(new("story", Loc.T("Bercerita di api unggun", "Tell stories by the fire"), "🔥", () =>
                {
                    Clock.Advance(40);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
                    {
                        m.Needs.Add(NeedKind.Fun, 25);
                        m.Needs.Add(NeedKind.Social, 30);
                    }

                    Bus.Effect(EffectKind.Sparkles, feature.Area.Center, 1.5f);
                    CreateMemory(Loc.T("Cerita di api unggun", "Campfire stories"), Loc.T("Ayah bercerita lucu, Dinda tertawa sampai bersin!", "Dad told funny stories and Dinda laughed until she sneezed!"),
                        MemoryKind.Trip, EmotionalOutcome.Heartwarming, group, WorldMap.Name(PlaceId.Camping), 3f, "campfire");
                }));
                break;
            case FeatureKind.Tent:
                o.Add(new("tent", Loc.T("Istirahat di tenda", "Rest in the tent"), "⛺", () =>
                {
                    Clock.Advance(60);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
                    {
                        m.Needs.Add(NeedKind.Sleep, 25);
                        m.Stamina.Value += 50;
                    }
                }));
                break;
            case FeatureKind.FerrisWheel or FeatureKind.Carousel:
                long ticket = 40_000L * group.Count;
                o.Add(new("ride", Loc.T($"Naik wahana ({Loc.Money(ticket)})", $"Take a ride ({Loc.Money(ticket)})"), "🎡", () =>
                {
                    if (!State.Wallet.Spend(ticket, Loc.T("Tiket wahana", "Ride tickets"), Clock.DayIndex))
                    {
                        return;
                    }

                    Clock.Advance(20);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
                    {
                        m.Needs.Add(NeedKind.Fun, 40);
                    }

                    Bus.Effect(EffectKind.Confetti, me.Position, 2.5f);
                    Bus.Sound("cheer");
                    CreateMemory(feature.Kind == FeatureKind.FerrisWheel ? Loc.T("Di puncak bianglala", "On top of the Ferris wheel") : Loc.T("Komidi putar", "The carousel"),
                        Loc.T("Kota terlihat kecil dari atas, semua berteriak senang!", "The whole town looked tiny and everyone cheered!"),
                        MemoryKind.Trip, EmotionalOutcome.Joyful, group, WorldMap.Name(PlaceId.ThemePark), 2.5f, $"ride:{feature.Kind}");
                }, State.Wallet.CanAfford(ticket)));
                break;
            case FeatureKind.Sand:
                o.Add(new("sandcastle", Loc.T("Bikin istana pasir", "Build a sandcastle"), "🏰", () =>
                {
                    Clock.Advance(30);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id)))
                    {
                        m.Needs.Add(NeedKind.Fun, 30);
                    }

                    Practice(me, SkillKind.Crafting, 0.5f);
                    CreateMemory(Loc.T("Istana pasir raksasa", "A giant sandcastle"), Loc.T("Istana pasir dengan menara dan parit, sebelum ombak datang!", "A sandcastle with towers and a moat, before the waves came!"),
                        MemoryKind.Trip, EmotionalOutcome.Joyful, group, WorldMap.Name(PlaceId.Beach), 2f, "sandcastle");
                }));
                o.Add(new("swim", Loc.T("Berenang di laut", "Swim in the sea"), "🏊", () =>
                {
                    if (!me.Stamina.TrySpend(15f))
                    {
                        return;
                    }

                    Clock.Advance(30);
                    me.Needs.Add(NeedKind.Fun, 30);
                    Practice(me, SkillKind.Swimming, 0.8f);
                    Bus.Effect(EffectKind.Splash, me.Position, 0.4f, 2f);
                }, me.Stamina.CanDoDemanding));
                break;
            case FeatureKind.Pond:
                o.Add(new("fish", Loc.T("Beri makan ikan", "Feed the fish"), "🐟", () =>
                {
                    me.Needs.Add(NeedKind.Fun, 12);
                    Bus.Effect(EffectKind.Splash, feature.Area.Closest(me.Position), 0.2f);
                }));
                break;
            case FeatureKind.Playground:
                o.Add(new("playground", Loc.T("Main perosotan & ayunan", "Play on the slide & swings"), "🛝", () =>
                {
                    if (!me.Stamina.TrySpend(10f))
                    {
                        return;
                    }

                    Clock.Advance(20);
                    foreach (FamilyMember m in State.Members.Where(m => group.Contains(m.Id) && m.IsChild))
                    {
                        m.Needs.Add(NeedKind.Fun, 30);
                    }

                    Practice(me, SkillKind.Sports, 0.3f);
                }, me.Stamina.CanDoDemanding));
                break;
        }

        return o;
    }

    private static string FeatureName(FeatureKind kind) => kind switch
    {
        FeatureKind.Campfire => Loc.T("Api Unggun", "Campfire"),
        FeatureKind.Tent => Loc.T("Tenda", "Tent"),
        FeatureKind.FerrisWheel => Loc.T("Bianglala", "Ferris Wheel"),
        FeatureKind.Carousel => Loc.T("Komidi Putar", "Carousel"),
        FeatureKind.Pond => Loc.T("Kolam Ikan", "Fish Pond"),
        FeatureKind.Playground => Loc.T("Taman Bermain Anak", "Playground"),
        _ => Loc.T("Pantai Berpasir", "Sandy Beach"),
    };

    // ---------------------------------------------------------------- school

    /// <summary>Results of the school lessons played by the player.</summary>
    public void CompleteSchool(IReadOnlyDictionary<Subject, float> scores)
    {
        PendingMiniGame = null;
        FamilyMember me = Controlled;
        State.School.LessonDay = Clock.DayIndex;
        State.School.Attend(me.Id);
        State.AddStat(Stat.SchoolDays);
        foreach ((Subject subject, float score) in scores)
        {
            State.School.Record(me.Id, subject, score);
            Practice(me, Lessons.Skill(subject), 0.4f + (score / 100f));
            if (score >= 60f)
            {
                State.AddStat(Stat.LessonsPassed);
            }
        }

        float average = scores.Count > 0 ? scores.Values.Average() : 0f;
        me.Needs.Add(NeedKind.Social, 25);
        me.Needs.Add(NeedKind.Fun, average > 70 ? 15 : -5);
        me.Mood.Add("school", average >= 75 ? Loc.T("Nilai bagus di sekolah!", "Great grades at school!") : Loc.T("Belajar di sekolah", "Studied at school"),
            average >= 75 ? 12 : 3, average >= 75 ? MoodKind.Proud : MoodKind.Content, Now, 300);

        // Lessons last until 13:00.
        double end = (Math.Floor(Now / Time.GameClock.MinutesPerDay) * Time.GameClock.MinutesPerDay) + (13 * 60);
        if (end > Now)
        {
            Clock.Advance(end - Now);
        }

        if (average >= 90f)
        {
            CreateMemory(Loc.T("Nilai sempurna!", "Top marks!"), Loc.T($"{me.Name} mendapat nilai A dan dipuji Bu Guru Rina.", $"{me.Name} got an A and Ms. Rina was very proud."),
                MemoryKind.School, EmotionalOutcome.Proud, [me.Id], WorldMap.Name(PlaceId.School), 2f, "top-marks");
            Say(me, Loc.T("Yes! Berhasil!", "Yes! I did it!"), "boy_cheer");
        }
    }

    // ------------------------------------------------------------ shop & build

    public bool Buy(string itemId, int count = 1)
    {
        ItemDef item = ItemCatalog.Get(itemId);
        long cost = item.Price * count;
        if (!State.Wallet.Spend(cost, $"{item.Name} ×{count}", Clock.DayIndex))
        {
            Bus.Notice(Loc.T("Uangnya tidak cukup.", "Not enough money."), "💸", NoticeKind.Warning);
            return false;
        }

        State.Inventory.Add(itemId, count);
        if (item.Category == ItemCategory.Ingredient)
        {
            State.AddStat(Stat.GroceriesBought, count);
        }

        Practice(Controlled, SkillKind.Shopping, 0.05f * count);
        Bus.Sound("register");
        return true;
    }

    public bool BuildRoom(RoomId room)
    {
        RoomDef def = Rooms.Get(room);
        if (!State.House.CanBuild(room, State.Chapter, out string reason))
        {
            Bus.Notice(reason, "🚧", NoticeKind.Warning);
            return false;
        }

        if (!State.Wallet.Spend(def.Cost, Loc.T($"Bangun {def.Name}", $"Built the {def.Name}"), Clock.DayIndex))
        {
            Bus.Notice(Loc.T("Uangnya belum cukup.", "Not enough money yet."), "💸", NoticeKind.Warning);
            return false;
        }

        State.House.Build(room);
        State.AddStat(Stat.RoomsBuilt);
        RebuildCollision();

        // Nobody may end up inside a new wall.
        foreach (FamilyMember m in State.Members.Where(m => !m.Away && Collision.Blocked(m.Position, CharacterRadius * 0.5f)))
        {
            m.Position = Rooms.FrontDoor + new Vector2(0, 1.5f);
        }

        Bus.Sound("build");
        Bus.Effect(EffectKind.Confetti, def.Area.Center, 2f, 2f);
        CreateMemory(Loc.T($"{def.Name} baru!", $"A new {def.Name}!"),
            Loc.T($"Rumah kita bertambah besar: {def.Name} selesai dibangun. Rumah ini sekarang \"{State.House.Title}\".", $"Our home grew: the {def.Name} is finished. It is now a \"{State.House.Title}\"."),
            MemoryKind.Building, EmotionalOutcome.Proud, FamilyNames.All, def.Name, 2.5f, $"room:{room}");
        Practice(State.Member(MemberId.Father), SkillKind.Building, 1f);
        return true;
    }

    public bool BuyFurniture(string defId, RoomId room, Vector2 position, int rotation)
    {
        FurnitureDef def = FurnitureCatalog.Get(defId);
        if (!State.House.CanPlace(def, room, position, rotation))
        {
            Bus.Notice(Loc.T("Tidak muat di sini.", "It doesn't fit here."), "📐", NoticeKind.Warning);
            return false;
        }

        if (!State.Wallet.Spend(def.Price, def.Name, Clock.DayIndex))
        {
            Bus.Notice(Loc.T("Uangnya tidak cukup.", "Not enough money."), "💸", NoticeKind.Warning);
            return false;
        }

        State.House.Place(defId, room, position, rotation);
        State.AddStat(Stat.FurnitureBought);
        Bus.Sound("place");
        Bus.Effect(EffectKind.Sparkles, position, 0.8f);
        RebuildCollision();
        return true;
    }

    public bool MoveFurniture(FurnitureItem item, RoomId room, Vector2 position, int rotation)
    {
        if (!State.House.CanPlace(item.Def, room, position, rotation, item))
        {
            return false;
        }

        foreach (FamilyMember m in State.Members.Where(m => m.Task?.FurnitureUid == item.Uid))
        {
            CancelTask(m);
        }

        item.Room = room;
        item.Position = position;
        item.Rotation = ((rotation % 4) + 4) % 4;
        State.House.TouchFurniture();
        Bus.Sound("place");
        RebuildCollision();
        return true;
    }

    public void SellFurniture(FurnitureItem item)
    {
        foreach (FamilyMember m in State.Members.Where(m => m.Task?.FurnitureUid == item.Uid))
        {
            CancelTask(m);
        }

        long refund = item.Def.Price / 2;
        State.House.Remove(item);
        if (refund > 0)
        {
            State.Wallet.Earn(refund, Loc.T($"Jual {item.Def.Name}", $"Sold {item.Def.Name}"), Clock.DayIndex);
        }

        RebuildCollision();
    }

    public bool PaintRoom(RoomId room, string color)
    {
        if (!State.Wallet.Spend(50_000, Loc.T($"Cat {Rooms.Name(room)}", $"Paint {Rooms.Name(room)}"), Clock.DayIndex))
        {
            return false;
        }

        State.House.WallPaint[room] = color;
        State.House.Touch();
        Bus.Sound("paint");
        return true;
    }

    public bool SetFloor(RoomId room, FloorStyle style)
    {
        if (!State.Wallet.Spend(100_000, Loc.T($"Lantai {Rooms.Name(room)}", $"Floor of {Rooms.Name(room)}"), Clock.DayIndex))
        {
            return false;
        }

        State.House.FloorOverrides[room] = style;
        State.House.Touch();
        Bus.Sound("build");
        return true;
    }
}
