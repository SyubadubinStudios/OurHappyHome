using System.Numerics;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Scenarios;

/// <summary>Common: a stray cat wanders into the living room. Befriend it, and maybe adopt it.</summary>
public sealed class CatVisitorScenario(GameSession s) : Scenario(s, ScenarioKind.CatVisitor)
{
    private ScenarioActor _cat = null!;
    private int _pets;
    private bool _milk;

    public override string Title => Loc.T("Kucing Masuk Rumah", "A Cat Wanders In");

    public override string Icon => "🐱";

    public override Rarity Rarity => Rarity.Common;

    public override void Start()
    {
        _cat = Spawn(ActorKind.Cat, Rooms.FrontDoor + new Vector2(0f, 1.2f), Loc.T("Kucing oranye", "Ginger cat"));
        _cat.Target = new Vector2(-2.8f, 3.2f);
        _cat.Speed = 0.9f;
        Add("milk", "Beri kucing susu", "Give the cat some milk", "🥛", optional: true);
        Add("pet", "Elus kucing sampai jinak", "Pet the cat until it trusts you", "🤚");
        S.Bus.Notice(Loc.T("Ada kucing masuk ke rumah!", "A cat came into the house!"), "🐱", NoticeKind.Info);
        S.Bus.Sound("meow");
        if (State.Member(MemberId.OlderSister) is { Away: false } nara)
        {
            S.Say(nara, Loc.T("Ada kucing! Lucu sekali!", "A kitty! So cute!"), "os_cat");
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        if (!_cat.Moving && S.Random.Chance(0.01f))
        {
            _cat.Target = new Vector2(S.Random.Range(-5.5f, -0.5f), S.Random.Range(0.6f, 5.5f));
        }

        if (S.Now - StartedAt > 150)
        {
            S.Bus.Notice(Loc.T("Kucingnya pergi lagi.", "The cat wandered off again."), "🐱", NoticeKind.Info);
            Complete(_pets > 0);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        List<InteractionOption> options =
        [
            new("milk", Loc.T("Beri susu", "Give milk"), "🥛", () =>
            {
                if (State.Inventory.Take("milk"))
                {
                    _milk = true;
                    _pets++;
                    Done("milk");
                    S.Bus.Effect(EffectKind.Hearts, _cat.Position, 0.6f);
                }
            }, !_milk && State.Inventory.Has("milk")),
            new("pet", Loc.T("Elus kucing", "Pet the cat"), "🤚", () =>
            {
                _pets++;
                S.Bus.Sound("meow");
                S.Bus.Effect(EffectKind.Hearts, _cat.Position, 0.6f);
                me.Needs.Add(NeedKind.Fun, 8);
                if (_pets >= 2)
                {
                    Done("pet");
                }
            }),
            new("adopt", Loc.T("Adopsi kucing ini", "Adopt this cat"), "🏠", () =>
            {
                Pet cat = new() { Id = State.NextPetId++, Kind = PetKind.Cat, Name = "Oyen", AdoptedDay = S.Clock.DayIndex, Position = _cat.Position, Personality = "curious" };
                State.Pets.Add(cat);
                State.AddStat(Progression.Stat.PetsAdopted);
                _cat.Visible = false;
                S.CreateMemory(Loc.T("Oyen jadi keluarga kita", "Oyen joins the family"),
                    Loc.T("Kucing oranye yang tersesat itu kini punya rumah. Namanya Oyen!", "The stray ginger cat has a home now. Her name is Oyen!"),
                    MemoryKind.Pet, EmotionalOutcome.Heartwarming, [.. Present.Select(m => m.Id)], Rooms.Name(RoomId.LivingRoom), 3f, "adopt-cat");
                Complete(true);
            }, _pets >= 2 && !State.Pets.Any(p => p.Kind == PetKind.Cat), Loc.T("Elus dulu sampai jinak", "Pet it until it trusts you")),
            new("outside", Loc.T("Antar keluar", "Lead it outside"), "🚪", () =>
            {
                _cat.Target = Rooms.FrontDoor + new Vector2(0, 6f);
                S.CreateMemory(Loc.T("Tamu berbulu", "A furry visitor"), Loc.T("Seekor kucing mampir ke rumah, minum susu lalu pergi lagi.", "A cat dropped by, drank some milk and left again."),
                    MemoryKind.Pet, EmotionalOutcome.Joyful, [me.Id], Rooms.Name(RoomId.LivingRoom), 1f, "cat-visit");
                Complete(true);
            }),
        ];
        yield return At("cat", Loc.T("Kucing oranye", "Ginger cat"), "🐱", _cat.Position, 0.6f, options);
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.IsChild && m.Id != State.Controlled && S.Random.Chance(0.4f))
        {
            S.StartTask(m, ActivityId.PlayWithPet, null, -1, target: _cat.Position + new Vector2(0.7f, 0.5f), minutes: 10);
            return true;
        }

        return false;
    }
}

/// <summary>Common: a light bulb pops. Sweep up the glass and replace the bulb.</summary>
public sealed class LightBulbScenario(GameSession s) : Scenario(s, ScenarioKind.LightBulb)
{
    private RoomId _room;
    private Vector2 _spot;

    public override string Title => Loc.T("Lampu Pecah", "A Bulb Breaks");

    public override string Icon => "💡";

    public override Rarity Rarity => Rarity.Common;

    public override void Start()
    {
        RoomId[] rooms = [.. House.BuiltRooms.Where(r => Rooms.Get(r).Indoor && r != RoomId.Hall)];
        _room = S.Random.Pick(rooms);
        _spot = RoomArea(_room).Center;
        DarkRooms.Add(_room);
        House.AddHazard(HazardKind.BrokenGlass, _spot + new Vector2(0.4f, 0.3f), 0.5f);
        Add("glass", "Bersihkan pecahan kaca", "Sweep up the broken glass", "🧹", target: _spot);
        Add("bulb", "Pasang bola lampu baru", "Fit a new light bulb", "💡", target: _spot);
        S.Bus.Sound("glass");
        S.Bus.Notice(Loc.T($"Pop! Lampu di {Rooms.Name(_room)} pecah.", $"Pop! The light in the {Rooms.Name(_room)} broke."), "💡", NoticeKind.Warning);
    }

    public override void Tick(float minutes, float realDt)
    {
        if (!House.Hazards.Any(h => h.Kind == HazardKind.BrokenGlass && h.Room == _room))
        {
            Done("glass");
        }

        if (RequiredDone)
        {
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        if (IsDone("bulb"))
        {
            yield break;
        }

        yield return At("bulb", Loc.T("Fiting lampu", "Light fitting"), "💡", _spot, 2.6f,
        [
            new("bulb", Loc.T("Ganti bola lampu", "Replace the bulb"), "💡", () => ReplaceBulb(me), State.Inventory.Has("bulb"), Loc.T("Beli bola lampu di supermarket", "Buy a bulb at the supermarket")),
        ]);
    }

    private void ReplaceBulb(FamilyMember by)
    {
        if (!State.Inventory.Take("bulb"))
        {
            return;
        }

        DarkRooms.Clear();
        Done("bulb");
        S.Bus.Sound("switch");
        S.Bus.Effect(EffectKind.Sparkles, _spot, 2.6f);
        State.AddStat(Progression.Stat.Repairs);
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.Id == MemberId.Father && !IsDone("bulb") && State.Inventory.Has("bulb"))
        {
            S.StartTask(m, ActivityId.Repair, null, -1, target: _spot, minutes: 8, tag: "bulb");
            return true;
        }

        if (m.Id == MemberId.Mother && !IsDone("glass"))
        {
            S.StartTask(m, ActivityId.Clean, null, -1, target: _spot + new Vector2(0.4f, 0.3f), minutes: 10);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag == "bulb")
        {
            ReplaceBulb(m);
        }
    }
}

/// <summary>Common: a faucet leaks and the puddle grows until someone fixes it.</summary>
public sealed class FaucetLeakScenario(GameSession s) : Scenario(s, ScenarioKind.FaucetLeak)
{
    private FurnitureItem? _tap;
    private Hazard? _puddle;

    public override string Title => Loc.T("Keran Bocor", "Leaky Faucet");

    public override string Icon => "🚰";

    public override Rarity Rarity => Rarity.Common;

    public override void Start()
    {
        _tap = House.Furniture.Where(f => f.DefId is "sink" or "kitchen" or "shower").OrderBy(_ => S.Random.NextFloat()).FirstOrDefault();
        if (_tap is null)
        {
            Complete(false);
            return;
        }

        _tap.Broken = true;
        _puddle = House.AddHazard(HazardKind.Puddle, _tap.ApproachPoint(0), 0.2f);
        House.TouchFurniture();
        Add("fix", $"Perbaiki keran ({_tap.Def.Name})", $"Fix the tap ({_tap.Def.Name})", "🔧", target: _tap.Position);
        Add("mop", "Pel genangan air", "Mop up the puddle", "🧹", target: _puddle.Position);
        S.Bus.Notice(Loc.T("Keran bocor! Airnya menggenang.", "A tap is leaking! Water is pooling."), "🚰", NoticeKind.Warning);
        S.Bus.Sound("drip");
    }

    public override void Tick(float minutes, float realDt)
    {
        if (_tap is null)
        {
            return;
        }

        if (_tap.Broken && _puddle is not null && House.Hazards.Contains(_puddle))
        {
            _puddle.Intensity = MathF.Min(1f, _puddle.Intensity + (minutes * 0.004f));
        }

        if (!_tap.Broken)
        {
            Done("fix");
        }

        if (!House.Hazards.Any(h => h.Kind == HazardKind.Puddle))
        {
            if (_tap.Broken)
            {
                // The puddle comes back while the tap still drips.
                if (S.Random.Chance(minutes * 0.02f))
                {
                    _puddle = House.AddHazard(HazardKind.Puddle, _tap.ApproachPoint(0), 0.2f);
                }
            }
            else
            {
                Done("mop");
            }
        }

        if (RequiredDone)
        {
            Complete(true);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.Id == MemberId.Father && _tap is { Broken: true } && S.FindFurniture(m, ActivityId.Repair) is { } fix)
        {
            S.StartTask(m, ActivityId.Repair, fix.Item, fix.Slot);
            return true;
        }

        return false;
    }
}

/// <summary>Common: an appliance stops working (TV, fridge, washer...).</summary>
public sealed class ApplianceScenario(GameSession s) : Scenario(s, ScenarioKind.ApplianceBroken)
{
    private FurnitureItem? _item;

    public override string Title => Loc.T("Barang Rusak", "Something Broke");

    public override string Icon => "🛠";

    public override Rarity Rarity => Rarity.Common;

    public override void Start()
    {
        _item = House.Furniture.Where(f => f.Def.Breakable && !f.Broken && f.DefId is not ("sink" or "kitchen" or "shower")).OrderBy(_ => S.Random.NextFloat()).FirstOrDefault();
        if (_item is null)
        {
            Complete(false);
            return;
        }

        _item.Broken = true;
        House.TouchFurniture();
        Add("fix", $"Perbaiki {_item.Def.Name}", $"Repair the {_item.Def.Name}", "🔧", target: _item.Position);
        S.Bus.Notice(Loc.T($"{_item.Def.Name} tiba-tiba rusak!", $"The {_item.Def.Name} suddenly broke!"), "🛠", NoticeKind.Warning);
        S.Bus.Sound("break");
        if (_item.DefId == "tv")
        {
            foreach (FamilyMember m in Present.Where(m => m.Task?.Activity == ActivityId.WatchTV))
            {
                S.Say(m, Loc.T("Yaaah, TV-nya mati!", "Aww, the TV died!"));
                S.CancelTask(m);
            }
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        if (_item is null || !House.Furniture.Contains(_item) || !_item.Broken)
        {
            Done("fix");
            Complete(true);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.Id == MemberId.Father && _item is { Broken: true } && S.Random.Chance(0.6f) && S.FindFurniture(m, ActivityId.Repair) is { } fix)
        {
            S.StartTask(m, ActivityId.Repair, fix.Item, fix.Slot);
            return true;
        }

        return false;
    }
}

/// <summary>Uncommon: a cheeky monkey sneaks in to steal food. Chase it away!</summary>
public sealed class MonkeyScenario(GameSession s) : Scenario(s, ScenarioKind.MonkeyThief)
{
    private ScenarioActor _monkey = null!;
    private int _scares;
    private bool _stole;
    private bool _leaving;

    public override string Title => Loc.T("Monyet Pencuri Makanan", "The Food-Stealing Monkey");

    public override string Icon => "🐒";

    public override Rarity Rarity => Rarity.Uncommon;

    public override void Start()
    {
        _monkey = Spawn(ActorKind.Monkey, new Vector2(0f, -8f), Loc.T("Monyet nakal", "Cheeky monkey"));
        _monkey.Target = new Vector2(3.0f, 4.4f);
        _monkey.Speed = 2.2f;
        Add("chase", "Usir monyet keluar rumah (kejar 3 kali)", "Chase the monkey out (catch up 3 times)", "🏃");
        Add("food", "Jaga makanan di dapur", "Protect the food in the kitchen", "🍌", optional: true);
        S.Bus.Notice(Loc.T("Ada monyet masuk lewat pintu belakang!", "A monkey got in through the back door!"), "🐒", NoticeKind.Warning);
        S.Bus.Sound("monkey");
        S.PetAlert(_monkey.Position, Loc.T("Ada monyet!", "A monkey!"));
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        FamilyMember me = S.Controlled;
        float distance = Vector2.Distance(me.Position, _monkey.Position);

        if (_leaving)
        {
            if (!_monkey.Moving)
            {
                _monkey.Visible = false;
                Done("chase");
                if (!_stole)
                {
                    Done("food");
                }

                S.CreateMemory(Loc.T("Kejar-kejaran dengan monyet", "Chasing the monkey"),
                    _stole ? Loc.T("Monyet nakal kabur membawa pisang, semua tertawa terbahak-bahak.", "The cheeky monkey escaped with a banana and everyone laughed.")
                           : Loc.T("Kami berhasil mengusir monyet nakal tanpa kehilangan makanan!", "We chased the cheeky monkey out without losing any food!"),
                    MemoryKind.Play, EmotionalOutcome.Funny, [.. Present.Select(m => m.Id)], Rooms.Name(RoomId.Kitchen), 2f, "monkey");
                Complete(true);
            }

            return;
        }

        // At the counter the monkey grabs food.
        if (!_stole && Vector2.Distance(_monkey.Position, new Vector2(3.0f, 4.4f)) < 0.5f && S.Random.Chance(realDt * 0.15f))
        {
            _stole = true;
            if (State.Pantry.Count > 0)
            {
                State.Pantry.RemoveAt(0);
            }
            else
            {
                State.Inventory.Take("fruit");
            }

            S.Bus.Notice(Loc.T("Monyet mengambil makanan!", "The monkey grabbed some food!"), "🍌", NoticeKind.Warning);
        }

        if (distance < 1.3f)
        {
            _scares++;
            S.Bus.Sound("monkey");
            S.Bus.Effect(EffectKind.Dust, _monkey.Position, 0.5f);
            me.Stamina.Value -= 6f;
            if (_scares >= 3)
            {
                _leaving = true;
                _monkey.Target = new Vector2(0f, -14f);
                _monkey.Speed = 4f;
                return;
            }

            // Jump away to another spot in the house.
            Vector2[] spots = [new(-4f, 2f), new(4.5f, 1.5f), new(-3f, -3f), new(4f, -2.5f), new(0f, -3f), new(-2f, 5f)];
            _monkey.Target = S.Random.Pick(spots);
        }
        else if (!_monkey.Moving && S.Random.Chance(realDt * 0.3f))
        {
            _monkey.Target = _monkey.Position + new Vector2(S.Random.Range(-2f, 2f), S.Random.Range(-2f, 2f));
        }

        if (S.Now - StartedAt > 120)
        {
            _leaving = true;
            _monkey.Target = new Vector2(0f, -14f);
        }
    }
}
