using System.Numerics;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Scenarios;

/// <summary>
/// Uncommon: the power goes out. Find flashlights, check the fuse box, gather
/// in the living room with warm drinks (the design's emergent-story example).
/// </summary>
public sealed class PowerOutageScenario(GameSession s) : Scenario(s, ScenarioKind.PowerOutage)
{
    private bool _fuse;
    private double _restoreAt;

    public override string Title => Loc.T("Listrik Padam", "Power Outage");

    public override string Icon => "🔦";

    public override Rarity Rarity => Rarity.Uncommon;

    public override bool Dangerous => true;

    public override bool InterruptOnStart(FamilyMember m) => m.Task?.Activity is not (ActivityId.Sleep or ActivityId.Nap);

    public override void Start()
    {
        House.PowerOn = false;
        _fuse = S.Random.Chance(0.55f);
        _restoreAt = S.Now + S.Random.Range(50f, 110f);
        Add("light", "Nyalakan senter", "Switch on a flashlight", "🔦");
        Add("fuse", "Periksa panel listrik di lorong", "Check the fuse box in the hallway", "⚡", target: new Vector2(-0.8f, -5f));
        Add("gather", "Kumpulkan keluarga di ruang keluarga", "Gather the family in the living room", "👪", target: RoomArea(RoomId.LivingRoom).Center);
        Add("drinks", "Buat minuman hangat", "Make warm drinks", "☕", optional: true);
        S.Bus.Notice(Loc.T("Listrik padam! Rumah gelap gulita.", "The power went out! The house is pitch dark."), "🔌", NoticeKind.Warning);
        S.Bus.Sound("power-down");
        S.Bus.Publish(new MusicEvent(MusicMood.Tense));

        // The youngest gets scared of the dark.
        foreach (FamilyMember m in Present.Where(m => m.Personality.Fears.Contains("dark")))
        {
            m.Mood.Add("dark", Loc.T("Takut gelap", "Afraid of the dark"), -15, MoodKind.Scared, S.Now, 120);
        }

        if (State.Member(MemberId.YoungerSister) is { Away: false } dinda)
        {
            S.Say(dinda, Loc.T("Kakak... gelap sekali, aku takut...", "It's so dark... I'm scared..."), "ys_scared");
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        if (S.FlashlightOn)
        {
            Done("light");
        }

        int present = Present.Count();
        int gathered = Present.Count(m => InRoom(m, RoomId.LivingRoom));
        if (present > 0 && gathered >= present)
        {
            Done("gather");
        }

        if (!_fuse && S.Now >= _restoreAt)
        {
            Restore();
        }

        if (House.PowerOn && IsDone("gather") && IsDone("light"))
        {
            Complete(true);
        }
        else if (House.PowerOn && S.Now - StartedAt > 150)
        {
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        if (!S.FlashlightOn)
        {
            bool has = State.Inventory.Has("flashlight") || State.Inventory.Has("candle");
            yield return Here("flashlight", Loc.T("Senter", "Flashlight"), "🔦",
            [
                new("on", State.Inventory.Has("flashlight") ? Loc.T("Nyalakan senter", "Switch on the flashlight") : Loc.T("Nyalakan lilin", "Light a candle"), "🔦",
                    () => S.FlashlightOn = true, has, Loc.T("Tidak punya senter! Beli di supermarket.", "No flashlight! Buy one at the supermarket.")),
            ]);
        }
    }

    public override void OnPowerFixed(FamilyMember by)
    {
        Done("fuse");
        if (_fuse)
        {
            S.Say(by, Loc.T("Sekringnya turun. Sudah Ayah naikkan!", "The fuse tripped. Fixed it!"));
            Restore();
        }
        else
        {
            S.Bus.Notice(Loc.T("Panel aman. Listrik dari PLN yang padam, tunggu sebentar.", "The fuse box is fine. It's a grid outage, wait a little."), "⚡", NoticeKind.Info);
        }
    }

    public override void OnWarmDrinks(FamilyMember m) => Done("drinks");

    private void Restore()
    {
        if (House.PowerOn)
        {
            return;
        }

        House.PowerOn = true;
        Done("fuse");
        S.FlashlightOn = false;
        S.Bus.Notice(Loc.T("Listrik menyala lagi!", "The power is back!"), "💡", NoticeKind.Good);
        S.Bus.Sound("power-up");
        int gathered = Present.Count(m => InRoom(m, RoomId.LivingRoom));
        if (gathered >= 3)
        {
            Memory("Malam dengan senter", "An evening by flashlight",
                "Listrik padam saat hujan. Kami berkumpul di ruang keluarga dengan senter dan minuman hangat sampai lampu menyala lagi.",
                "The power went out in the rain. We huddled in the living room with flashlights and warm drinks until the lights came back.",
                EmotionalOutcome.Heartwarming);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (House.PowerOn)
        {
            return false;
        }

        switch (m.Id)
        {
            case MemberId.Father when !IsDone("fuse") && S.FindFurniture(m, ActivityId.FixPower) is { } fuse:
                S.StartTask(m, ActivityId.FixPower, fuse.Item, fuse.Slot);
                S.Say(m, Loc.T("Ayah periksa panel listrik dulu.", "I'll check the fuse box."));
                return true;
            case MemberId.Mother when !IsDone("drinks") && State.Inventory.Has("milk") && S.FindFurniture(m, ActivityId.MakeDrink) is { } stove:
                S.StartTask(m, ActivityId.MakeDrink, stove.Item, stove.Slot);
                return true;
            default:
                if (!InRoom(m, RoomId.LivingRoom))
                {
                    if (S.FindFurniture(m, ActivityId.Relax) is { } seat && seat.Item.Room == RoomId.LivingRoom)
                    {
                        S.StartTask(m, ActivityId.Relax, seat.Item, seat.Slot, minutes: 40);
                    }
                    else
                    {
                        S.StartTask(m, ActivityId.Hide, null, -1, target: RoomArea(RoomId.LivingRoom).Center + new Vector2(S.Random.Range(-1.5f, 1.5f), 0.4f), minutes: 40);
                    }

                    return true;
                }

                return false;
        }
    }
}

/// <summary>Uncommon: heavy rain floods the yard; protect the doors and get everyone inside.</summary>
public sealed class FloodScenario(GameSession s) : Scenario(s, ScenarioKind.LocalFlood)
{
    private readonly List<Hazard> _water = [];

    public override string Title => Loc.T("Banjir Kecil", "Local Flooding");

    public override string Icon => "🌊";

    public override Rarity Rarity => Rarity.Uncommon;

    public override bool Major => true;

    public override void Start()
    {
        _water.Add(House.AddHazard(HazardKind.Flood, Rooms.FrontDoor + new Vector2(0f, 0.9f), 0.5f));
        _water.Add(House.AddHazard(HazardKind.Flood, new Vector2(0f, -6.9f), 0.5f));
        Add("doors", "Hadang air di pintu depan & belakang", "Block the water at the front & back doors", "🧱");
        Add("inside", "Semua anggota keluarga masuk rumah", "Get everyone inside", "🏠");
        Add("tv", "Angkat barang elektronik", "Lift the electronics", "📺", optional: true);
        S.Bus.Notice(Loc.T("Air mulai masuk halaman! Hadang di pintu!", "Water is flooding the yard! Block the doors!"), "🌊", NoticeKind.Danger);
        S.Bus.Sound("flood");
        if (State.Weather.Current is WeatherKind.Sunny or WeatherKind.Cloudy)
        {
            S.SetWeather(WeatherKind.Rain);
        }

        // A child playing outside gets stuck on the swing in rising water.
        FamilyMember? outside = Present.FirstOrDefault(m => m.IsChild && m.Id != State.Controlled && !House.IsIndoors(m.Position) && Rooms.Lot.Contains(m.Position));
        if (outside is null && State.Mode != GameMode.Cozy && S.Random.Chance(0.6f))
        {
            outside = Present.Where(m => m.IsChild && m.Id != State.Controlled).OrderBy(_ => S.Random.NextFloat()).FirstOrDefault();
        }

        if (outside is not null)
        {
            Danger(outside, SafetyState.NeedsHelp, 75f, new Vector2(6f, -11f));
            Add("rescue", $"Jemput {outside.Name} dari halaman belakang", $"Bring {outside.Name} in from the backyard", "🆘", target: outside.Position);
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        foreach (Hazard water in _water.Where(h => House.Hazards.Contains(h)))
        {
            water.Intensity = MathF.Min(1f, water.Intensity + (minutes * 0.003f));
        }

        UpdateRescues(realDt, p => House.IsIndoors(p));
        if (S.ScenarioFailed)
        {
            return;
        }

        if (_water.All(h => !House.Hazards.Contains(h)))
        {
            Done("doors");
        }

        if (Present.All(m => House.IsIndoors(m.Position) || !Rooms.Lot.Contains(m.Position)) && !AnyoneInDanger)
        {
            Done("inside");
            Done("rescue");
        }

        if (RequiredDone)
        {
            Memory("Banjir di halaman", "The yard flood",
                "Air hujan menggenangi halaman, tapi kami bekerja sama menghadang air dan semua aman di dalam rumah.",
                "Rain flooded the yard, but we worked together to hold back the water and everyone stayed safe inside.",
                EmotionalOutcome.Relieved);
            S.SetWeather(WeatherKind.Cloudy);
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        foreach (Hazard water in _water.Where(h => House.Hazards.Contains(h)))
        {
            yield return At($"towel:{water.Id}", Loc.T("Celah pintu", "Door gap"), "🌊", water.Position, 0.5f,
            [
                new("towel", Loc.T("Sumbat dengan kain & ember", "Plug it with towels & buckets"), "🧺", () =>
                {
                    if (me.Stamina.TrySpend(10f))
                    {
                        water.Intensity -= 0.25f;
                        S.Bus.Effect(EffectKind.Splash, water.Position, 0.3f);
                        if (water.Intensity <= 0f)
                        {
                            House.Hazards.Remove(water);
                        }
                    }
                }, me.Stamina.CanDoDemanding, Loc.T("Terlalu lelah", "Too tired")),
            ]);
        }

        if (!IsDone("tv") && House.Furniture.FirstOrDefault(f => f.DefId == "tv") is { } tv)
        {
            yield return At("lift-tv", Loc.T("TV", "TV"), "📺", tv.Position, 1.4f,
            [
                new("lift", Loc.T("Angkat ke tempat tinggi", "Lift it up high"), "⬆", () => Done("tv"), me.Stamina.CanDoDemanding),
            ]);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.Id == MemberId.Father && _water.FirstOrDefault(h => House.Hazards.Contains(h)) is { } water)
        {
            S.StartTask(m, ActivityId.Clean, null, -1, target: water.Position + new Vector2(0, -0.8f), minutes: 12, run: true, tag: $"flood:{water.Id}");
            return true;
        }

        if (!House.IsIndoors(m.Position))
        {
            RunTo(m, RoomArea(RoomId.LivingRoom).Center);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag.StartsWith("flood:", StringComparison.Ordinal) && _water.FirstOrDefault(h => $"flood:{h.Id}" == task.Tag) is { } water)
        {
            water.Intensity -= 0.45f;
            if (water.Intensity <= 0f)
            {
                House.Hazards.Remove(water);
            }
        }
    }
}

/// <summary>Uncommon: a snake slithers into the garden. Kids inside, call animal rescue.</summary>
public sealed class SnakeScenario(GameSession s) : Scenario(s, ScenarioKind.DangerousAnimal)
{
    private ScenarioActor _snake = null!;
    private ScenarioActor? _rescuer;
    private double _rescueArrives = -1;

    public override string Title => Loc.T("Ular di Halaman", "A Snake in the Yard");

    public override string Icon => "🐍";

    public override Rarity Rarity => Rarity.Uncommon;

    public override bool Major => true;

    public override void Start()
    {
        _snake = Spawn(ActorKind.Snake, new Vector2(-8f, 10f), Loc.T("Ular", "Snake"));
        _snake.Speed = 0.35f;
        Add("kids", "Bawa anak-anak masuk rumah", "Get the children indoors", "🏠");
        Add("doors", "Tutup & kunci pintu", "Close and lock the doors", "🔒", target: Rooms.FrontDoor);
        Add("call", "Telepon petugas penyelamat hewan", "Call animal rescue", "📞");
        S.Bus.Notice(Loc.T("Ada ular di halaman depan! Jaga jarak!", "There's a snake in the front yard! Keep away!"), "🐍", NoticeKind.Danger);
        S.PetAlert(_snake.Position, Loc.T("Ada ular!", "A snake!"));

        FamilyMember? child = Present.Where(m => m.IsChild && m.Id != State.Controlled).OrderBy(_ => S.Random.NextFloat()).FirstOrDefault();
        if (child is not null && State.Mode != GameMode.Cozy)
        {
            Danger(child, SafetyState.NeedsHelp, 70f, _snake.Position + new Vector2(2.2f, -1.2f));
            Add("rescue", $"Tuntun {child.Name} menjauh dari ular", $"Lead {child.Name} away from the snake", "🆘", target: child.Position);
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        if (!_snake.Moving && _rescuer is null)
        {
            _snake.Target = new Vector2(S.Random.Range(-13f, -4f), S.Random.Range(7.5f, 13.5f));
        }

        UpdateRescues(realDt, p => House.IsIndoors(p));
        if (S.ScenarioFailed)
        {
            return;
        }

        if (Present.Where(m => m.IsChild).All(m => House.IsIndoors(m.Position)))
        {
            Done("kids");
            Done("rescue");
        }

        if (House.DoorsLocked)
        {
            Done("doors");
        }

        if (_rescueArrives > 0 && S.Now >= _rescueArrives && _rescuer is null)
        {
            _rescuer = Spawn(ActorKind.Rescuer, new Vector2(-6f, 21f), Loc.T("Petugas", "Rescuer"));
            _rescuer.Target = _snake.Position + new Vector2(0.8f, 0f);
            _rescuer.Speed = 1.8f;
        }

        if (_rescuer is not null && Vector2.Distance(_rescuer.Position, _snake.Position) < 1.2f)
        {
            _snake.Visible = false;
            _rescuer.Target = new Vector2(-6f, 25f);
            if (RequiredDone)
            {
                Memory("Ular di halaman", "The snake in the yard",
                    "Seekor ular masuk ke halaman. Kami berlindung di dalam rumah sampai petugas datang menangkapnya dengan aman.",
                    "A snake slithered into the yard. We sheltered inside until a rescuer safely caught it.",
                    EmotionalOutcome.Relieved);
                Complete(true);
            }
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        if (!IsDone("call"))
        {
            yield return Here("call", Loc.T("Telepon", "Phone"), "📞",
            [
                new("call", Loc.T("Telepon penyelamat hewan", "Call animal rescue"), "📞", () =>
                {
                    Done("call");
                    _rescueArrives = S.Now + 20;
                    S.Bus.Notice(Loc.T("Petugas akan datang dalam 20 menit.", "A rescuer will arrive in 20 minutes."), "🚐", NoticeKind.Info);
                }),
            ]);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.IsChild && !House.IsIndoors(m.Position))
        {
            RunTo(m, RoomArea(RoomId.LivingRoom).Center);
            return true;
        }

        if (m.Id == MemberId.Father && !House.DoorsLocked && House.IsIndoors(m.Position))
        {
            S.StartTask(m, ActivityId.Idle, null, -1, target: Rooms.FrontDoor - new Vector2(0, 0.9f), tag: "lock", minutes: 2);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag == "lock")
        {
            House.DoorsLocked = true;
            S.Bus.Sound("lock");
        }
    }
}

/// <summary>Uncommon (forest): a wild boar startles the family on the trail.</summary>
public sealed class WildAnimalScenario(GameSession s) : Scenario(s, ScenarioKind.WildAnimal)
{
    private ScenarioActor _boar = null!;

    public override string Title => Loc.T("Babi Hutan!", "Wild Boar!");

    public override string Icon => "🐗";

    public override Rarity Rarity => Rarity.Uncommon;

    public override bool Major => true;

    public override void Start()
    {
        Vector2 p = S.Controlled.Position;
        _boar = Spawn(ActorKind.Boar, p + new Vector2(-9f, 6f), Loc.T("Babi hutan", "Wild boar"));
        _boar.Speed = 1.6f;
        Add("away", "Jauhi babi hutan (lebih dari 25 m)", "Get away from the boar (over 25 m)", "🏃");
        S.Bus.Notice(Loc.T("Babi hutan! Jangan panik, menjauh pelan-pelan.", "A wild boar! Don't panic, back away slowly."), "🐗", NoticeKind.Danger);
        S.Bus.Sound("boar");

        FamilyMember? member = Present.FirstOrDefault(m => m.Id != State.Controlled && State.Party.Contains(m.Id));
        if (member is not null)
        {
            Danger(member, SafetyState.NeedsHelp, 60f, p + new Vector2(-4f, 3f));
            Add("rescue", $"Ajak {member.Name} menjauh", $"Lead {member.Name} away", "🆘");
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        if (!_boar.Moving)
        {
            _boar.Target = _boar.Position + new Vector2(S.Random.Range(-4f, 4f), S.Random.Range(-4f, 4f));
        }

        UpdateRescues(realDt, p => Vector2.Distance(p, _boar.Position) > 22f);
        if (S.ScenarioFailed)
        {
            return;
        }

        bool far = Vector2.Distance(S.Controlled.Position, _boar.Position) > 25f && !Present.Any(m => m.InDanger || m.Safety == SafetyState.Following);
        if (far)
        {
            Done("away");
            Done("rescue");
            Memory("Bertemu babi hutan", "Meeting a wild boar",
                "Di hutan kami bertemu babi hutan. Kami tetap tenang dan menjauh bersama-sama.",
                "In the forest we met a wild boar. We stayed calm and backed away together.",
                EmotionalOutcome.Scary, 2f);
            Complete(true);
        }
    }
}

/// <summary>Rare: a suspicious stranger lurks by the fence. Lights, locks, cameras, a phone call.</summary>
public sealed class StrangerScenario(GameSession s) : Scenario(s, ScenarioKind.SuspiciousStranger)
{
    private ScenarioActor _stranger = null!;
    private bool _leaving;

    public override string Title => Loc.T("Orang Mencurigakan", "Suspicious Stranger");

    public override string Icon => "🕵";

    public override Rarity Rarity => Rarity.Rare;

    public override bool Dangerous => true;

    public override void Start()
    {
        _stranger = Spawn(ActorKind.Stranger, new Vector2(-20f, 16.2f), Loc.T("Orang asing", "Stranger"));
        _stranger.Target = new Vector2(-4f, 16.2f);
        _stranger.Speed = 0.8f;
        Add("lights", "Nyalakan lampu luar", "Turn on the outside lights", "💡", target: Rooms.FrontDoor);
        Add("lock", "Kunci semua pintu", "Lock all doors", "🔒", target: Rooms.FrontDoor);
        Add("watch", "Cek kamera / intip dari jendela", "Check the cameras / peek out the window", "📹");
        Add("call", "Telepon Pak RT", "Call the neighbourhood watch", "📞");
        S.PetAlert(_stranger.Position, Loc.T("Ada orang di luar pagar.", "Someone is outside the fence."));
        S.Bus.Notice(Loc.T("Ada orang asing mondar-mandir di depan pagar...", "A stranger is pacing outside the fence..."), "🕵", NoticeKind.Warning);
        S.Bus.Publish(new MusicEvent(MusicMood.Tense));
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        if (House.ExteriorLightsOn)
        {
            Done("lights");
        }

        if (House.DoorsLocked)
        {
            Done("lock");
        }

        if (!_leaving && !_stranger.Moving)
        {
            _stranger.Target = new Vector2(S.Random.Range(-10f, 8f), 16.2f);
        }

        int done = Objectives.Count(o => o.Done);
        if (!_leaving && done >= 3)
        {
            _leaving = true;
            _stranger.Target = new Vector2(60f, 16.5f);
            _stranger.Speed = 2.5f;
            S.Bus.Notice(Loc.T("Orang itu pergi menjauh.", "The stranger walked away."), "🚶", NoticeKind.Good);
        }

        if (_leaving && !_stranger.Moving)
        {
            Memory("Malam yang menegangkan", "A tense evening",
                "Ada orang mencurigakan di luar. Kami menyalakan lampu, mengunci pintu dan saling menjaga.",
                "A suspicious stranger was outside. We turned on the lights, locked up and looked after each other.",
                EmotionalOutcome.Relieved, 2f);
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        List<InteractionOption> options = [];
        if (!IsDone("call"))
        {
            options.Add(new("call", Loc.T("Telepon Pak RT", "Call the neighbourhood watch"), "📞", () =>
            {
                Done("call");
                S.Bus.Notice(Loc.T("Pak RT akan berpatroli di jalan kita.", "The neighbourhood watch will patrol our street."), "📞", NoticeKind.Info);
            }));
        }

        if (!IsDone("watch"))
        {
            options.Add(new("peek", Loc.T("Intip dari jendela", "Peek out the window"), "🪟", () => Done("watch"), House.IsIndoors(me.Position), Loc.T("Masuk ke rumah dulu", "Go inside first")));
        }

        if (options.Count > 0)
        {
            yield return Here("stranger-actions", Loc.T("Tindakan aman", "Safety actions"), "🛡", options);
        }
    }

    public override void OnCamerasChecked(FamilyMember by) => Done("watch");

    public override bool Direct(FamilyMember m)
    {
        if (m.IsChild && !House.IsIndoors(m.Position))
        {
            RunTo(m, RoomArea(RoomId.LivingRoom).Center);
            return true;
        }

        if (m.Id == MemberId.Father && !House.ExteriorLightsOn && House.PowerOn)
        {
            S.StartTask(m, ActivityId.Idle, null, -1, target: Rooms.FrontDoor - new Vector2(0, 0.9f), tag: "lights", minutes: 2);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag == "lights")
        {
            House.ExteriorLightsOn = true;
            S.Bus.Sound("switch");
        }
    }
}

/// <summary>
/// Rare: a night-time attempted burglary. Wake Dad, lights and alarm, gather
/// the children in the parents' room, call the police. Family friendly: the
/// intruder simply runs off.
/// </summary>
public sealed class BurglaryScenario(GameSession s) : Scenario(s, ScenarioKind.Burglary)
{
    private ScenarioActor _intruder = null!;
    private ScenarioActor? _police;
    private double _policeAt = -1;

    public override string Title => Loc.T("Percobaan Pencurian", "Attempted Burglary");

    public override string Icon => "🚨";

    public override Rarity Rarity => Rarity.Rare;

    public override bool Major => true;

    private static Rect SafeRoom => RoomArea(RoomId.ParentsBedroom);

    public override bool InterruptOnStart(FamilyMember m) => m.Id != MemberId.Father;

    public override void Start()
    {
        _intruder = Spawn(ActorKind.Stranger, new Vector2(8f, -14.5f), Loc.T("Pencuri", "Intruder"));
        _intruder.Target = new Vector2(1.5f, -7.4f);
        _intruder.Speed = 0.6f;
        Add("wake", "Bangunkan Ayah", "Wake Dad", "👨");
        Add("lights", "Nyalakan lampu luar", "Turn on the outside lights", "💡", target: Rooms.FrontDoor);
        Add("secure", "Kunci pintu atau aktifkan alarm", "Lock the doors or arm the alarm", "🔒", target: Rooms.FrontDoor);
        Add("gather", "Kumpulkan anak-anak di kamar Ayah & Ibu", "Gather the children in the parents' room", "👪", target: SafeRoom.Center);
        Add("police", "Telepon polisi", "Call the police", "🚓");
        S.Bus.Sound("creak");
        S.PetAlert(_intruder.Position, Loc.T("Ada suara di pintu belakang!", "A noise at the back door!"));
        S.Bus.Notice(Loc.T("Ada suara mencurigakan di pintu belakang...", "There's a suspicious noise at the back door..."), "🚨", NoticeKind.Danger);

        // Wake the controlled character if asleep.
        if (S.Controlled.Task?.Activity is ActivityId.Sleep)
        {
            S.CancelTask(S.Controlled);
        }

        FamilyMember dad = State.Member(MemberId.Father);
        if (dad.Away || State.Controlled == MemberId.Father || dad.Task?.Activity is not (ActivityId.Sleep or ActivityId.Nap))
        {
            Objectives[0].Done = true;
        }

        FamilyMember nara = State.Member(MemberId.OlderSister);
        if (!nara.Away && State.Controlled != MemberId.OlderSister)
        {
            Danger(nara, SafetyState.NeedsHelp, 90f, RoomArea(RoomId.KidsBedroom).Center + new Vector2(0.5f, 1.2f));
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        UpdateRescues(realDt, p => SafeRoom.Contains(p));
        if (S.ScenarioFailed)
        {
            return;
        }

        if (House.ExteriorLightsOn)
        {
            Done("lights");
        }

        if (House.DoorsLocked || House.AlarmActive)
        {
            Done("secure");
        }

        if (Present.Where(m => m.IsChild && m.Id != State.Controlled).All(m => SafeRoom.Contains(m.Position)) && !AnyoneInDanger)
        {
            Done("gather");
        }

        if (_policeAt > 0 && S.Now >= _policeAt && _police is null)
        {
            _police = Spawn(ActorKind.Police, new Vector2(-2f, 22f), Loc.T("Polisi", "Police"));
            _police.Target = new Vector2(-1.2f, 9f);
            _police.Speed = 2.5f;
            _intruder.Target = new Vector2(30f, -30f);
            _intruder.Speed = 4f;
            S.Bus.Sound("siren");
        }

        if ((IsDone("lights") && IsDone("secure")) || House.AlarmActive)
        {
            // Light and noise scare the intruder off.
            _intruder.Target = new Vector2(30f, -30f);
            _intruder.Speed = 3.5f;
        }

        if (RequiredDone && _police is not null && !_police.Moving)
        {
            Memory("Malam yang menegangkan", "The night of the noise",
                "Ada yang mencoba masuk ke rumah. Kami menyalakan lampu, berkumpul di kamar Ayah & Ibu dan polisi datang. Semua selamat!",
                "Someone tried to break in. We turned on the lights, gathered in the parents' room and the police came. Everyone was safe!",
                EmotionalOutcome.Relieved, 3f);
            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        FamilyMember dad = State.Member(MemberId.Father);
        if (!IsDone("wake") && me.Id != MemberId.Father && Vector2.Distance(dad.Position, me.Position) < 2.5f)
        {
            yield return At("wake-dad", dad.Name, "👨", dad.Position, 1.6f,
            [
                new("wake", Loc.T("Bangunkan Ayah", "Wake Dad"), "⏰", () => WakeDad()),
            ]);
        }

        if (!IsDone("police"))
        {
            yield return Here("police", Loc.T("Telepon", "Phone"), "📞",
            [
                new("call", Loc.T("Telepon polisi (110)", "Call the police"), "🚓", () =>
                {
                    Done("police");
                    _policeAt = S.Now + 12;
                    S.Bus.Notice(Loc.T("Polisi sedang menuju ke rumah!", "The police are on their way!"), "🚓", NoticeKind.Info);
                }),
            ]);
        }
    }

    private void WakeDad()
    {
        FamilyMember dad = State.Member(MemberId.Father);
        S.CancelTask(dad);
        Done("wake");
        S.Say(dad, Loc.T("Semua tenang. Ayah di sini.", "Stay calm. Dad's here."), "dad_storm");
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.Id == MemberId.Father)
        {
            if (!IsDone("wake"))
            {
                return false; // still asleep: let them keep sleeping until woken
            }

            if (!House.ExteriorLightsOn)
            {
                S.StartTask(m, ActivityId.Idle, null, -1, target: Rooms.FrontDoor - new Vector2(0, 0.9f), tag: "lights", minutes: 2, run: true);
                return true;
            }

            if (!House.DoorsLocked)
            {
                S.StartTask(m, ActivityId.Idle, null, -1, target: new Vector2(0f, -5.2f), tag: "lock", minutes: 2, run: true);
                return true;
            }

            return false;
        }

        if (m.Id == MemberId.Mother)
        {
            S.CancelTask(m);
            S.StartTask(m, ActivityId.Hide, null, -1, target: SafeRoom.Center + new Vector2(0.6f, 1.6f), run: true, minutes: 60);
            return true;
        }

        if (m.IsChild && !SafeRoom.Contains(m.Position) && !m.InDanger)
        {
            RunTo(m, SafeRoom.Center + new Vector2(S.Random.Range(-1f, 1f), 1.8f), minutes: 60);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        switch (task.Tag)
        {
            case "lights":
                House.ExteriorLightsOn = true;
                S.Bus.Sound("switch");
                break;
            case "lock":
                House.DoorsLocked = true;
                S.Bus.Sound("lock");
                break;
        }
    }

    public override void OnSafetyChanged()
    {
        if (House.AlarmActive)
        {
            S.Bus.Sound("alarm");
        }
    }
}

/// <summary>
/// Rare: a small kitchen fire. Use the extinguisher or get everyone out to the
/// front yard; a child is trapped by smoke in the bedroom.
/// </summary>
public sealed class FireScenario(GameSession s) : Scenario(s, ScenarioKind.SmallFire)
{
    private readonly List<Hazard> _fires = [];
    private ScenarioActor? _firefighters;
    private double _firefightersAt = -1;

    public override string Title => Loc.T("Kebakaran Kecil", "Small House Fire");

    public override string Icon => "🔥";

    public override Rarity Rarity => Rarity.Rare;

    public override bool Major => true;

    private static readonly Rect Assembly = new(-8f, 8f, 6f, 14.5f);

    public override void Start()
    {
        Vector2 stove = House.Furniture.FirstOrDefault(f => f.DefId == "kitchen")?.Position ?? new Vector2(3f, 5f);
        _fires.Add(House.AddHazard(HazardKind.Fire, stove + new Vector2(0f, -0.9f), 0.45f));
        House.AddHazard(HazardKind.Smoke, stove + new Vector2(0f, -1.5f), 0.9f);
        Add("out", "Padamkan api ATAU keluarkan semua ke halaman depan", "Put out the fire OR get everyone to the front yard", "🧯", target: _fires[0].Position);
        Add("call", "Telepon pemadam kebakaran (113)", "Call the fire brigade", "🚒");
        S.Bus.Notice(Loc.T("Kebakaran di dapur! Tetap tenang!", "Fire in the kitchen! Stay calm!"), "🔥", NoticeKind.Danger);
        S.Bus.Sound("fire-alarm");
        S.Bus.Publish(new ShakeEvent(S.Settings.ReducedIntensity ? 0f : 0.3f));

        FamilyMember? child = Present.Where(m => m.IsChild && m.Id != State.Controlled).OrderBy(_ => S.Random.NextFloat()).FirstOrDefault();
        if (child is not null)
        {
            RoomId room = House.Has(RoomId.GirlsBedroom) && child.Id != MemberId.Player ? RoomId.GirlsBedroom : RoomId.KidsBedroom;
            Danger(child, State.Mode == GameMode.Cozy ? SafetyState.NeedsHelp : SafetyState.Trapped, 80f, RoomArea(room).Center + new Vector2(0f, 1.0f));
            Add("rescue", $"Selamatkan {child.Name} dari kamar berasap", $"Rescue {child.Name} from the smoky bedroom", "🆘", target: child.Position);
            House.AddHazard(HazardKind.Smoke, child.Position, 0.6f);
        }

        foreach (Pet pet in State.Pets)
        {
            pet.Position = Assembly.Center + new Vector2(2f, 0f);
        }
    }

    public override void Tick(float minutes, float realDt)
    {
        base.Tick(minutes, realDt);
        foreach (Hazard fire in _fires.Where(f => House.Hazards.Contains(f)).ToList())
        {
            fire.Intensity = MathF.Min(1f, fire.Intensity + (minutes * (State.Mode == GameMode.Adventure ? 0.025f : 0.015f)));
            if (fire.Intensity > 0.85f && _fires.Count < 3 && S.Random.Chance(minutes * 0.05f))
            {
                _fires.Add(House.AddHazard(HazardKind.Fire, fire.Position + new Vector2(S.Random.Range(-1.6f, 1.6f), S.Random.Range(-1.4f, 0.2f)), 0.35f));
            }
        }

        UpdateRescues(realDt, p => Assembly.Contains(p) || !House.IsIndoors(p));
        if (S.ScenarioFailed)
        {
            return;
        }

        bool fireOut = _fires.All(f => !House.Hazards.Contains(f));
        bool everyoneOut = Present.All(m => !House.IsIndoors(m.Position)) && !AnyoneInDanger;

        if (_firefightersAt > 0 && S.Now >= _firefightersAt && _firefighters is null)
        {
            _firefighters = Spawn(ActorKind.Firefighter, new Vector2(6f, 22f), Loc.T("Pemadam", "Firefighter"));
            _firefighters.Target = Rooms.FrontDoor + new Vector2(0f, 0.8f);
            _firefighters.Speed = 2.6f;
            S.Bus.Sound("siren");
        }

        if (_firefighters is not null && !_firefighters.Moving && !fireOut)
        {
            foreach (Hazard fire in _fires)
            {
                House.Hazards.Remove(fire);
            }

            fireOut = true;
            S.Bus.Notice(Loc.T("Pemadam kebakaran memadamkan api!", "The firefighters put out the fire!"), "🚒", NoticeKind.Good);
        }

        if (fireOut && !AnyoneInDanger)
        {
            Done("out");
            Done("rescue");
        }
        else if (everyoneOut)
        {
            Done("rescue");
        }

        if ((fireOut || (everyoneOut && IsDone("call"))) && !AnyoneInDanger)
        {
            if (!fireOut && _firefighters is null)
            {
                return; // wait outside for the firefighters
            }

            Done("out");
            Memory("Kebakaran kecil di dapur", "The little kitchen fire",
                "Api kecil muncul di dapur. Kami tidak panik, saling menolong dan tidak ada yang tertinggal.",
                "A small fire started in the kitchen. We didn't panic, we helped each other and nobody was left behind.",
                EmotionalOutcome.Relieved, 3.5f);
            foreach (Hazard smoke in House.Hazards.Where(h => h.Kind == HazardKind.Smoke))
            {
                smoke.Intensity = MathF.Min(smoke.Intensity, 0.3f);
            }

            Complete(true);
        }
    }

    public override IEnumerable<InteractionTarget> Targets(FamilyMember me)
    {
        if (!IsDone("call"))
        {
            yield return Here("fire-call", Loc.T("Telepon", "Phone"), "📞",
            [
                new("call", Loc.T("Telepon pemadam kebakaran", "Call the fire brigade"), "🚒", () =>
                {
                    Done("call");
                    _firefightersAt = S.Now + 15;
                    S.Bus.Notice(Loc.T("Pemadam kebakaran segera datang!", "The fire brigade is coming!"), "🚒", NoticeKind.Info);
                }),
            ]);
        }
    }

    public override void OnFireOut(FamilyMember by)
    {
        if (_fires.All(f => !House.Hazards.Contains(f)))
        {
            S.Bus.Notice(Loc.T($"{by.Name} berhasil memadamkan api!", $"{by.Name} put out the fire!"), "🧯", NoticeKind.Good);
            by.Mood.Add("hero", Loc.T("Memadamkan api!", "Put out a fire!"), 15, MoodKind.Proud, S.Now, 300);
        }
    }

    public override bool Direct(FamilyMember m)
    {
        if (m.InDanger)
        {
            return false;
        }

        if (m.Id == MemberId.Father && State.Mode != GameMode.Cozy && House.Furniture.Any(f => f.DefId == "extinguisher")
            && _fires.FirstOrDefault(f => House.Hazards.Contains(f)) is { } fire && m.Stamina.CanDoDemanding)
        {
            S.StartTask(m, ActivityId.Idle, null, -1, target: fire.Position + new Vector2(0f, -1.2f), run: true, tag: $"extinguish:{fire.Id}", minutes: 3);
            return true;
        }

        if (House.IsIndoors(m.Position))
        {
            RunTo(m, Assembly.Center + new Vector2(S.Random.Range(-3f, 3f), S.Random.Range(-1f, 1f)), minutes: 60);
            return true;
        }

        return false;
    }

    public override void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task)
    {
        if (task.Tag.StartsWith("extinguish:", StringComparison.Ordinal) && _fires.FirstOrDefault(f => $"extinguish:{f.Id}" == task.Tag) is { } fire && House.Hazards.Contains(fire))
        {
            fire.Intensity -= 0.5f;
            S.Bus.Effect(EffectKind.Smoke, fire.Position, 1f, 1.4f);
            S.Bus.Sound("extinguisher");
            if (fire.Intensity <= 0f)
            {
                House.Hazards.Remove(fire);
                OnFireOut(m);
            }
        }
    }
}
