using System.Numerics;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Scenarios;

/// <summary>
/// The multi-day "Great Storm" from design section 13: warning, inspect the
/// house, secure outdoor things, prepare food, water and flashlights, make
/// sure everyone is home, survive the night, then inspect and repair.
/// </summary>
public sealed class GreatStormScenario(GameSession s) : Scenario(s, ScenarioKind.GreatStorm)
{
    private enum Phase
    {
        Prepare,
        Storm,
        Recover,
    }

    private Phase _phase = Phase.Prepare;
    private readonly HashSet<RoomId> _inspected = [];
    private readonly HashSet<int> _secured = [];
    private int _stormDay;
    private bool _windowBroken;
    private bool _powerCut;
    private string? _brokenWindowKey;
    private Vector2 _brokenWindowAt;
    private bool _windowFixed;

    public override string Title => Loc.T("Badai Besar", "The Great Storm");

    public override string Icon => "🌀";

    public override Rarity Rarity => Rarity.Rare;

    /// <summary>Only the storm night itself is a major (failable) emergency.</summary>
    public override bool Major => _phase == Phase.Storm;

    public override bool Dangerous => true;

    public override bool InterruptOnStart(FamilyMember m) => false;

    public string PhaseName => _phase switch
    {
        Phase.Prepare => Loc.T("Persiapan", "Preparation"),
        Phase.Storm => Loc.T("Badai datang!", "The storm hits!"),
        _ => Loc.T("Pemulihan", "Recovery"),
    };

    public override void Start()
    {
        _stormDay = State.Weather.StormDay >= 0 ? State.Weather.StormDay : S.Clock.DayIndex + 1;
        State.Weather.StormDay = _stormDay;
        Add("inspect", "Periksa rumah (kunjungi 4 ruangan)", "Inspect the house (visit 4 rooms)", "🔍");
        Add("secure", "Amankan 3 barang di halaman", "Secure 3 things in the yard", "🪢");
        Add("food", "Siapkan makanan (5 porsi) & air (2 galon)", "Prepare food (5 servings) & water (2 bottles)", "🍱");
        Add("lights", "Siapkan senter & baterai", "Prepare a flashlight & batteries", "🔦");
        Add("home", "Pastikan semua di rumah sebelum badai (16:00)", "Make sure everyone is home before the storm (16:00)", "🏠");
        S.Bus.Notice(Loc.T("PERINGATAN BADAI! Badai besar akan datang besok sore. Ayo bersiap!", "STORM WARNING! A severe storm arrives tomorrow afternoon. Let's prepare!"), "🌀", NoticeKind.Danger);
        if (State.Member(MemberId.Father) is { Away: false } dad)
        {
            S.Say(dad, Loc.T("Semua masuk rumah! Badai akan datang!", "Everyone inside! A storm is coming!"), "dad_storm");
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        switch (_phase)
        {
            case Phase.Prepare:
                TickPrepare();
                break;
            case Phase.Storm:
                TickStorm(minutes, realDt);
                break;
            case Phase.Recover:
                TickRecover();
                break;
        }
    }

    private void TickPrepare()
    {
        if (House.RoomAt(S.Controlled.Position) is { } room && Rooms.Get(room).Indoor && _inspected.Add(room) && _inspected.Count == 4)
        {
            Done("inspect");
        }

        if (_secured.Count >= 3 || House.OutdoorItemsSecured)
        {
            House.OutdoorItemsSecured = true;
            Done("secure");
        }

        if (State.MealServings >= 5 && State.Inventory.Count("water") >= 2)
        {
            Done("food");
        }

        if (State.Inventory.Has("flashlight") && State.Inventory.Has("batteries"))
        {
            Done("lights");
        }

        bool stormStarts = S.Clock.DayIndex > _stormDay || (S.Clock.DayIndex == _stormDay && S.Hour >= 16f);
        if (!stormStarts)
        {
            return;
        }

        // Everyone home? The ones who are away hurry back.
        foreach (FamilyMember m in State.Members.Where(m => m.Away))
        {
            m.AwayUntil = S.Now;
        }

        if (State.Members.All(m => Rooms.AtHome(m.Position) || m.Away))
        {
            Done("home");
        }

        BeginStorm();
    }

    private void BeginStorm()
    {
        _phase = Phase.Storm;
        S.SetWeather(WeatherKind.SevereStorm);
        S.Bus.Publish(new MusicEvent(MusicMood.Tense));
        S.Bus.Notice(Loc.T("Badai datang! Tetap di dalam rumah!", "The storm is here! Stay indoors!"), "🌀", NoticeKind.Danger);
        int prepared = Objectives.Count(o => o.Done);
        Objectives.Clear();
        Add("power", "Bertahan saat listrik padam", "Get through the power cut", "🔦");
        Add("together", "Kumpulkan semua di ruang keluarga", "Gather everyone in the living room", "👪", target: RoomArea(RoomId.LivingRoom).Center);
        Add("comfort", "Hibur Kak Nara yang ketakutan", "Comfort Nara, who is frightened", "💗");
        Add("through", "Lewati malam badai", "Make it through the stormy night", "🌙");

        // Good preparation gives more time when things go wrong.
        _prepBonus = 1f + (prepared * 0.15f);
        foreach (FamilyMember m in State.Members.Where(m => !m.Away && m.Id != State.Controlled))
        {
            S.CancelTask(m);
        }

        // Unsecured yard things become flying debris.
        if (!House.OutdoorItemsSecured)
        {
            for (int i = 0; i < 3; i++)
            {
                House.AddHazard(HazardKind.Debris, new Vector2(S.Random.Range(-12f, 14f), S.Random.Range(-14f, -8f)), 0.6f);
            }
        }
    }

    private float _prepBonus = 1f;
    private double _stormEndsAt = -1;

    private void TickStorm(float minutes, float realDt)
    {
        if (_stormEndsAt < 0)
        {
            _stormEndsAt = S.Now + 240;
        }

        double elapsed = S.Now - (_stormEndsAt - 240);
        if (!_powerCut && elapsed > 25)
        {
            _powerCut = true;
            House.PowerOn = false;
            S.Bus.Sound("power-down");
            S.Bus.Notice(Loc.T("Listrik padam karena badai!", "The storm knocked the power out!"), "🔌", NoticeKind.Warning);
            if (!State.Inventory.Has("flashlight"))
            {
                _prepBonus *= 0.85f;
            }
        }

        if (_powerCut && S.FlashlightOn)
        {
            Done("power");
        }

        if (!_windowBroken && elapsed > 45)
        {
            BreakWindow();
        }

        UpdateRescues(realDt, p => RoomArea(RoomId.LivingRoom).Contains(p) || (House.Has(RoomId.SecretRoom) && RoomArea(RoomId.SecretRoom).Contains(p)));
        if (S.ScenarioFailed)
        {
            return;
        }

        int present = Present.Count();
        if (Present.Count(m => InRoom(m, RoomId.LivingRoom, RoomId.SecretRoom)) >= present && !AnyoneInDanger)
        {
            Done("together");
        }

        if (S.Now >= _stormEndsAt && !AnyoneInDanger)
        {
            Done("through");
            Done("power");
            House.PowerOn = true;
            S.FlashlightOn = false;
            S.SetWeather(WeatherKind.Rain);
            _phase = Phase.Recover;
            Objectives.Clear();
            Add("damage", "Periksa kerusakan", "Inspect the damage", "🔍", target: _brokenWindowAt);
            Add("clean", "Bersihkan puing & pecahan kaca", "Clear debris & broken glass", "🧹");
            Add("window", "Perbaiki jendela yang pecah", "Repair the broken window", "🪟", target: _brokenWindowAt);
            S.Bus.Notice(Loc.T("Badai sudah lewat. Saatnya memperbaiki rumah.", "The storm has passed. Time to repair the house."), "🌤", NoticeKind.Good);
            S.Bus.Publish(new MusicEvent(MusicMood.Emotional));
            if (State.Member(MemberId.Father) is { Away: false } dad)
            {
                S.Say(dad, Loc.T("Semua aman. Kita sekeluarga, kita kuat.", "Everyone's safe. Together, we're strong."), "dad_safe");
            }
        }
    }

    private void BreakWindow()
    {
        _windowBroken = true;
        RoomId room = House.Has(RoomId.GirlsBedroom) ? RoomId.GirlsBedroom : RoomId.KidsBedroom;
        WallSegment? wall = House.Walls.Where(w => w.WindowWidth > 0 && (w.NegativeSide == room || w.PositiveSide == room)).FirstOrDefault()
            ?? House.Walls.FirstOrDefault(w => w.WindowWidth > 0);
        if (wall is not null)
        {
            _brokenWindowKey = House.WallKey(wall);
            House.BrokenWindows.Add(_brokenWindowKey);
            Vector2 dir = Vector2.Normalize(wall.End - wall.Start);
            _brokenWindowAt = wall.Start + (dir * wall.WindowCenter);
        }
        else
        {
            _brokenWindowAt = RoomArea(room).Center;
        }

        Vector2 inside = RoomArea(room).Center;
        House.AddHazard(HazardKind.BrokenGlass, Vector2.Lerp(_brokenWindowAt, inside, 0.3f), 0.7f);
        House.AddHazard(HazardKind.Debris, Vector2.Lerp(_brokenWindowAt, inside, 0.5f), 0.7f);
        S.Bus.Sound("glass");
        S.Bus.Publish(new ShakeEvent(S.Settings.ReducedIntensity ? 0f : 0.5f));
        S.Bus.Notice(Loc.T("Dahan pohon memecahkan jendela kamar!", "A tree branch smashed a bedroom window!"), "🪟", NoticeKind.Danger);

        // Dinda is trapped behind the fallen branch; Nara is hiding, frightened.
        FamilyMember dinda = State.Member(MemberId.YoungerSister);
        if (!dinda.Away && State.Controlled != MemberId.YoungerSister)
        {
            Danger(dinda, State.Mode == GameMode.Cozy ? SafetyState.NeedsHelp : SafetyState.Trapped, 100f * _prepBonus, inside + new Vector2(0.4f, 0.9f));
            Add("rescue", "Selamatkan Dinda dari kamar", "Rescue Dinda from the bedroom", "🆘", target: dinda.Position);
        }

        FamilyMember nara = State.Member(MemberId.OlderSister);
        if (!nara.Away && State.Controlled != MemberId.OlderSister)
        {
            nara.Mood.Add("scared", Loc.T("Takut badai", "Scared of the storm"), -25, MoodKind.Scared, S.Now, 240);
            S.Say(nara, Loc.T("Aku takut... gelap sekali.", "I'm scared... it's so dark."), "os_scared");
        }
    }

    public override void OnComforted(FamilyMember by, FamilyMember other)
    {
        if (other.Id == MemberId.OlderSister)
        {
            Done("comfort");
        }
    }

    public override void OnRescued(FamilyMember by, FamilyMember other)
    {
        if (other.Id == MemberId.YoungerSister)
        {
            S.Say(other, Loc.T("Terima kasih, Kakak!", "Thank you!"), "ys_thanks");
        }
    }

    private void TickRecover()
    {
        if (Vector2.Distance(S.Controlled.Position, _brokenWindowAt) < 3f)
        {
            Done("damage");
        }

        if (!House.Hazards.Any(h => h.Kind is HazardKind.Debris or HazardKind.BrokenGlass))
        {
            Done("clean");
        }

        if (_windowFixed)
        {
            Done("window");
        }

        if (RequiredDone)
        {
            State.AddStat(Progression.Stat.GreatStormSurvived);
            State.Flags.Add("great-storm-done");
            State.Weather.StormDay = -1;
            S.SetWeather(WeatherKind.Sunny);
            Memory("Badai Besar", "The Great Storm",
                "Badai terbesar tahun ini. Listrik padam, jendela pecah, tapi kami bersiap, saling menjaga, dan tidak ada yang tertinggal.",
                "The biggest storm of the year. The power failed and a window broke, but we prepared, looked after each other, and nobody was left behind.",
                EmotionalOutcome.Heartwarming, 5f);
            S.Bus.Effect(EffectKind.Sparkles, S.Controlled.Position, 2f);
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        if (_phase == Phase.Prepare)
        {
            foreach (FurnitureItem item in House.Furniture.Where(f => f.Def.Outdoor && f.DefId != "tree" && !_secured.Contains(f.Uid)))
            {
                if (Vector2.Distance(item.Bounds.Closest(me.Position), me.Position) < 1.8f)
                {
                    yield return At($"secure:{item.Uid}", item.Def.Name, "🪢", item.Position, item.Def.Height + 0.3f,
                    [
                        new("secure", Loc.T("Amankan / ikat", "Secure / tie down"), "🪢", () =>
                        {
                            if (me.Stamina.TrySpend(6f))
                            {
                                _secured.Add(item.Uid);
                                S.Bus.Sound("rope");
                            }
                        }, me.Stamina.CanDoDemanding),
                    ]);
                }
            }

            // Fewer outdoor items than needed: tie down the bins and pots by the door instead.
            if (Vector2.Distance(Rooms.FrontDoor, me.Position) < 2.5f && _secured.Count < 3)
            {
                yield return At("secure-misc", Loc.T("Pot & tempat sampah", "Pots & bins"), "🪴", Rooms.FrontDoor + new Vector2(1.5f, 1f), 1f,
                [
                    new("secure", Loc.T("Masukkan ke garasi/rumah", "Move them indoors"), "📦", () =>
                    {
                        if (me.Stamina.TrySpend(6f))
                        {
                            _secured.Add(-1 - _secured.Count);
                        }
                    }, me.Stamina.CanDoDemanding),
                ]);
            }
        }

        if (_phase == Phase.Storm && _powerCut && !S.FlashlightOn)
        {
            yield return Here("flashlight", Loc.T("Senter", "Flashlight"), "🔦",
            [
                new("on", Loc.T("Nyalakan senter", "Switch on the flashlight"), "🔦", () => S.FlashlightOn = true,
                    State.Inventory.Has("flashlight") || State.Inventory.Has("candle"), Loc.T("Tidak punya senter!", "No flashlight!")),
            ]);
        }

        if (_phase == Phase.Recover && !_windowFixed && Vector2.Distance(_brokenWindowAt, me.Position) < 2.5f)
        {
            yield return At("window", Loc.T("Jendela pecah", "Broken window"), "🪟", _brokenWindowAt, 1.5f,
            [
                new("fix", Loc.T("Pasang papan & perbaiki", "Board up & repair"), "🔨", () =>
                {
                    if (me.Stamina.TrySpend(15f))
                    {
                        _windowFixed = true;
                        if (_brokenWindowKey is not null)
                        {
                            House.BrokenWindows.Remove(_brokenWindowKey);
                        }

                        State.AddStat(Progression.Stat.Repairs);
                        S.Bus.Sound("build");
                    }
                }, me.Stamina.CanDoDemanding),
            ]);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        switch (_phase)
        {
            case Phase.Prepare:
                if (m.Id == MemberId.Mother && State.MealServings < 5 && S.FindFurniture(m, ActivityId.Cook) is { } stove)
                {
                    S.StartTask(m, ActivityId.Cook, stove.Item, stove.Slot);
                    return true;
                }

                if (m.Id == MemberId.Father && _secured.Count < 3)
                {
                    FurnitureItem? item = House.Furniture.FirstOrDefault(f => f.Def.Outdoor && f.DefId != "tree" && !_secured.Contains(f.Uid));
                    if (item is not null)
                    {
                        S.StartTask(m, ActivityId.Repair, null, -1, target: item.ApproachPoint(0), minutes: 10, tag: $"secure:{item.Uid}");
                        return true;
                    }
                }

                return false;
            case Phase.Storm:
                if (m.InDanger)
                {
                    return false;
                }

                if (m.Id == MemberId.Mother && State.Inventory.Has("milk") && !m.Mood.Has("did:MakeDrink") && S.FindFurniture(m, ActivityId.MakeDrink) is { } kitchen)
                {
                    S.StartTask(m, ActivityId.MakeDrink, kitchen.Item, kitchen.Slot);
                    return true;
                }

                if (!InRoom(m, RoomId.LivingRoom, RoomId.SecretRoom))
                {
                    RunTo(m, RoomArea(RoomId.LivingRoom).Center + new Vector2(S.Random.Range(-1.8f, 1.8f), S.Random.Range(-0.5f, 1.5f)), minutes: 60);
                    return true;
                }

                if (m.Id == MemberId.OlderSister && m.Mood.Current == MoodKind.Scared)
                {
                    S.StartTask(m, ActivityId.Hide, null, -1, target: m.Position, minutes: 20);
                    return true;
                }

                S.StartTask(m, ActivityId.Hide, null, -1, target: m.Position, minutes: 15);
                return true;
            default:
                if (m.Id is MemberId.Father or MemberId.Mother && House.Hazards.FirstOrDefault(h => h.Kind is HazardKind.Debris or HazardKind.BrokenGlass) is { } mess)
                {
                    S.StartTask(m, ActivityId.Clean, null, -1, target: mess.Position + new Vector2(0.6f, 0f), minutes: 12);
                    return true;
                }

                if (m.Id == MemberId.Father && !_windowFixed && _brokenWindowKey is not null)
                {
                    S.StartTask(m, ActivityId.Repair, null, -1, target: Vector2.Lerp(_brokenWindowAt, RoomArea(House.RoomAt(_brokenWindowAt + new Vector2(0.3f, 0.3f)) ?? RoomId.KidsBedroom).Center, 0.25f), minutes: 25, tag: "storm-window");
                    return true;
                }

                return false;
        }
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag.StartsWith("secure:", StringComparison.Ordinal) && int.TryParse(task.Tag["secure:".Length..], out int uid))
        {
            _secured.Add(uid);
        }
        else if (task.Tag == "storm-window")
        {
            _windowFixed = true;
            if (_brokenWindowKey is not null)
            {
                House.BrokenWindows.Remove(_brokenWindowKey);
            }
        }
    }
}
