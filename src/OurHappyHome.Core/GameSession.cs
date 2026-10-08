using System.Numerics;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

/// <summary>
/// The running game: owns the saved <see cref="GameState"/> plus every
/// runtime system (event bus, collision, navigation, AI, event director,
/// active scenario) and advances them all each frame. Every family member is
/// simulated every tick, controlled or not.
/// </summary>
public sealed partial class GameSession
{
    public const float CharacterRadius = 0.26f;

    private float _goalCheckTimer;
    private float _autosaveTimer;
    private string? _snapshot;
    private ScenarioKind? _snapshotKind;

    public GameSession(GameState state, GameSettings settings)
    {
        State = state;
        Settings = settings;
        Map = WorldMap.Generate();
        foreach (TownFeature feature in Map.Features)
        {
            if (feature.Solid)
            {
                Collision.AddStatic(feature.CollisionArea);
            }
        }

        Director = new EventDirector(this);
        Npcs = Npc.CreateNeighbours();
        RebuildCollision();
    }

    public GameState State { get; private set; }

    public GameSettings Settings { get; }

    public EventBus Bus { get; } = new();

    public WorldMap Map { get; }

    public CollisionWorld Collision { get; } = new();

    public NavGrid? HomeNav { get; private set; }

    public EventDirector Director { get; }

    public Scenario? Scenario { get; private set; }

    public List<Npc> Npcs { get; }

    public bool Paused { get; set; }

    /// <summary>Set when a major scenario failed: the UI shows "FAMILY FAILED" and offers a restart.</summary>
    public bool ScenarioFailed { get; private set; }

    public string FailureReason { get; private set; } = "";

    /// <summary>Movement intent from the keyboard / stick, in world space (X, Z), length 0-1.</summary>
    public Vector2 PlayerMove { get; set; }

    public bool PlayerRun { get; set; }

    /// <summary>A mini-game the presentation is running; the task waits for its result.</summary>
    public MiniGameRequest? PendingMiniGame { get; private set; }

    public GameClock Clock => State.Clock;

    public double Now => State.Clock.TotalMinutes;

    public float Hour => State.Clock.Hour;

    public GameDate Date => State.Clock.Date;

    public GameRandom Random => State.Random;

    public IEnumerable<FamilyMember> Members => State.Members;

    public FamilyMember Controlled => State.ControlledMember;

    public bool AllAsleep { get; private set; }

    /// <summary>The effective simulation speed this frame (sleep skips the night).</summary>
    public float EffectiveSpeed { get; private set; } = 1f;

    public static GameSession NewGame(GameMode mode, string playerName, GameSettings settings, ulong seed = 0)
    {
        if (seed == 0)
        {
            seed = (ulong)DateTime.Now.Ticks;
        }

        GameState state = GameState.CreateNew(mode, playerName, seed);
        GameSession session = new(state, settings);
        session.Bus.Publish(new ChapterEvent(1, Chapters.All[0].Title, false));
        return session;
    }

    // ------------------------------------------------------------------ tick

    /// <summary>Advances the whole simulation by <paramref name="realDt"/> real seconds.</summary>
    public void Tick(float realDt)
    {
        if (Paused || ScenarioFailed || PendingMiniGame is not null)
        {
            // Mini-games run on the UI; keep characters animated but freeze time.
            return;
        }

        realDt = MathF.Min(realDt, 0.1f);
        float speed = Clock.Speed;
        AllAsleep = State.Members.Where(m => !m.Away).All(m => m.Task is { Activity: ActivityId.Sleep, Phase: TaskPhase.Performing });
        if (AllAsleep && Scenario is null)
        {
            speed = 40f;
        }
        else if (Scenario is { Major: true })
        {
            speed = 1f;
        }

        EffectiveSpeed = speed;
        float minutes = realDt * speed;

        int newDays = Clock.Advance(minutes);
        ProcessSchedule();
        if (newDays > 0)
        {
            OnNewDay();
        }

        UpdateWeather(minutes);
        RebuildCollisionIfNeeded();

        foreach (FamilyMember member in State.Members)
        {
            UpdateNeeds(member, minutes);
            UpdateMember(member, minutes, realDt, speed);
        }

        UpdatePets(minutes, realDt, speed);
        UpdateNpcs(realDt);

        foreach ((MemberId a, MemberId b) in State.Relationships.Tick(minutes))
        {
            Reconciled(a, b);
        }

        Scenario?.Tick(minutes, realDt);
        if (Scenario is { Finished: true })
        {
            EndScenario();
        }

        Director.Tick(minutes);
        TickHazards(minutes);

        _goalCheckTimer -= realDt;
        if (_goalCheckTimer <= 0f)
        {
            _goalCheckTimer = 1.5f;
            CheckProgress();
        }

        _autosaveTimer += realDt;
    }

    /// <summary>Autosave once per in-game morning when nothing dramatic is going on.</summary>
    public bool WantsAutosave()
    {
        if (Scenario is null && _autosaveTimer > 60f && Hour is > 6.9f and < 7.1f)
        {
            _autosaveTimer = 0f;
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------- schedule

    /// <summary>Fixed daily moments: departures, returns, meals, pay day and bills.</summary>
    private void ProcessSchedule()
    {
        double now = Now;
        while (State.ScheduleCursor < now)
        {
            double next = Math.Min(now, Math.Floor(State.ScheduleCursor) + 1);
            int minuteOfDay = (int)(next % GameClock.MinutesPerDay);
            if (Math.Abs(next - Math.Round(next)) < 1e-6)
            {
                OnMinute(minuteOfDay, (int)(next / GameClock.MinutesPerDay));
            }

            State.ScheduleCursor = next;
        }
    }

    private void OnMinute(int minuteOfDay, int day)
    {
        GameDate date = GameDate.FromDayIndex(day);
        switch (minuteOfDay)
        {
            case 6 * 60:
                MorningAnnouncements(date);
                break;
            case 7 * 60 + 15 when date.IsSchoolDay:
                SendChildrenToSchool();
                break;
            case 8 * 60 when date.IsWorkDay:
                SendToWork(MemberId.Father, 16.5f);
                break;
            case 9 * 60 when date.Weekday == Weekday.Saturday:
                SendOnErrand(MemberId.Mother);
                break;
            case 9 * 60 when date.Weekday == Weekday.Monday:
                PayBills(day);
                break;
            case 17 * 60 when date.Weekday == Weekday.Friday:
                PaySalary(day);
                break;
            case 19 * 60 + 30:
                EveningAnnouncements(date);
                break;
            case 15 * 60 when Calendar.IsFestivalDay(date):
                Bus.Notice(Loc.T("Festival di taman kota sudah buka! Ajak keluarga ke sana (peta M).", "The festival in the park is open! Take the family there (map M)."), "🎪", NoticeKind.Good);
                break;
            case >= 20 * 60 and <= 20 * 60 + 12 when Calendar.IsFestivalDay(date):
                Fireworks(minuteOfDay - (20 * 60));
                break;
            case 7 * 60:
            case 18 * 60 + 45:
                CallFamilyToTable(State.Members.FirstOrDefault(m => m.Id == MemberId.Mother && !m.Away));
                break;
        }

        if (minuteOfDay % 10 == 0)
        {
            AutoDress();
        }

        foreach (FamilyMember member in State.Members)
        {
            if (member.Away && Now >= member.AwayUntil)
            {
                ReturnHome(member);
            }
        }
    }

    /// <summary>The festival is on from 15:00 to 22:00 on festival days.</summary>
    public bool FestivalActive => Calendar.IsFestivalDay(Date) && Hour is >= 15f and < 22f;

    /// <summary>Fireworks over the park; whoever is there gets a memory.</summary>
    private void Fireworks(int step)
    {
        Vector2 c = WorldMap.FestivalCenter;
        for (int i = 0; i < 2; i++)
        {
            // The effect itself launches the shells 8-14 m higher.
            Vector2 p = c + new Vector2(Random.Range(-10f, 10f), Random.Range(-6f, 8f));
            Bus.Publish(new EffectEvent(EffectKind.Fireworks, new Vector3(p.X, Random.Range(-4f, -1f), p.Y), Random.Range(0.6f, 1.1f)));
        }

        Bus.Sound("firework", new Vector3(c.X, 15f, c.Y), 1f);
        List<MemberId> watching = [.. State.Members.Where(m => !m.Away && Vector2.Distance(m.Position, c) < 45f).Select(m => m.Id)];
        foreach (MemberId id in watching)
        {
            FamilyMember m = State.Member(id);
            m.Needs.Add(NeedKind.Fun, 6);
            m.Mood.Add("fireworks", Loc.T("Kembang api!", "Fireworks!"), 12, MoodKind.Excited, Now, 120);
        }

        if (step == 0 && watching.Count >= 2)
        {
            Bus.Publish(new MusicEvent(MusicMood.Celebration));
            CreateMemory(Loc.T("Kembang api di festival", "Fireworks at the festival"),
                Loc.T("Langit malam penuh warna. Semua menengadah sambil berseru \"Wah!\"", "The night sky filled with colour. Everyone looked up and said \"Wow!\""),
                MemoryKind.Trip, EmotionalOutcome.Joyful, watching, WorldMap.Name(PlaceId.Park), 3f, $"fireworks:{Clock.DayIndex}");
        }
    }

    /// <summary>Members who are not in a chosen costume dress for the weather when outside.</summary>
    private void AutoDress()
    {
        WeatherState weather = State.Weather;
        bool sunny = weather.Current == WeatherKind.Sunny && Date.Season == Season.Dry && Hour is >= 9f and < 16f;
        foreach (FamilyMember m in State.Members.Where(m => m.AccessoryAuto && !m.Away))
        {
            bool outside = !IsIndoors(m.Position);
            string? want = !outside ? null
                : weather.IsRaining && State.Inventory.Has("rain-hat") ? "rain-hat"
                : sunny && State.Inventory.Has("straw-hat") ? "straw-hat"
                : null;
            m.Accessory = want;
        }
    }

    private void MorningAnnouncements(GameDate date)
    {
        if (date.Day == 1 && (date.Month == 4 || date.Month == 10))
        {
            Season season = Seasons.Of(date);
            Bus.Notice(Loc.T($"{Seasons.Name(season)} dimulai!", $"The {Seasons.Name(season).ToLowerInvariant()} begins!"), Seasons.Icon(season), NoticeKind.Info);
        }

        foreach (CalendarEvent e in Calendar.EventsOn(date))
        {
            Bus.Notice(Loc.T($"Hari ini: {e.Title}", $"Today: {e.Title}"), e.Icon, NoticeKind.Info);
            switch (e.Kind)
            {
                case CalendarEventKind.ChildrensDay:
                    {
                        string[] presents = ["toy-car", "doll", "storybook", "sketchbook", "ball"];
                        foreach ((MemberId kid, int i) in FamilyNames.Children.Select((k, i) => (k, i)))
                        {
                            FamilyMember child = State.Member(kid);
                            child.Mood.Add("childrens-day", Loc.T("Hari Anak! Dapat kejutan!", "Children's Day surprise!"), 15, MoodKind.Excited, Now, 16 * 60);
                            State.Inventory.Add(presents[(i + date.Year) % presents.Length]);
                        }

                        Bus.Notice(Loc.T("Ayah dan Ibu memberi hadiah untuk anak-anak! Lihat di Barang.", "Mom and Dad gave the children presents! Check your items."), "🎁", NoticeKind.Good);
                        CreateMemory(Loc.T("Hari Anak Nasional", "National Children's Day"), Loc.T("Pagi-pagi ada kado kejutan dari Ayah dan Ibu di meja makan.", "Surprise presents from Mom and Dad waited on the breakfast table."),
                            MemoryKind.Gift, EmotionalOutcome.Joyful, FamilyNames.All, LocationName(State.Member(MemberId.Mother)), 2.5f, $"childrens-day:{date.DayIndex}");
                        break;
                    }

                case CalendarEventKind.MothersDay:
                    State.Member(MemberId.Mother).Mood.Add("mothers-day", Loc.T("Hari Ibu", "Mother's Day"), 10, MoodKind.Happy, Now, 16 * 60);
                    break;
            }

            if (e.Kind == CalendarEventKind.Birthday && e.Member is { } who)
            {
                foreach (FamilyMember m in State.Members.Where(m => m.Id != who))
                {
                    m.Mood.Add("birthday", Loc.T("Ulang tahun keluarga!", "A family birthday!"), 10, MoodKind.Excited, Now, 16 * 60);
                }

                State.Member(who).Mood.Add("my-birthday", Loc.T("Hari ulang tahunku!", "My birthday!"), 18, MoodKind.Excited, Now, 18 * 60);
            }
        }

        if (State.Weather.StormDay == date.DayIndex + 1)
        {
            Bus.Notice(Loc.T("Peringatan: Badai besar diperkirakan datang besok!", "Warning: a severe storm is expected tomorrow!"), "🌀", NoticeKind.Warning);
        }
    }

    private void EveningAnnouncements(GameDate date)
    {
        if (Calendar.EventsOn(date).Any(e => e.Kind == CalendarEventKind.MovieNight) && Scenario is null)
        {
            Bus.Notice(Loc.T("Malam nonton film! Semua berkumpul di sofa.", "Movie night! Everyone gathers on the sofa."), "🎬", NoticeKind.Good);
            foreach (FamilyMember m in State.Members.Where(m => !m.Away && m.Id != State.Controlled && m.Safety == SafetyState.Normal))
            {
                if (FindFurniture(m, ActivityId.WatchTV) is { } sofa)
                {
                    StartTask(m, ActivityId.WatchTV, sofa.Item, sofa.Slot, tag: "movie-night");
                }
            }
        }
    }

    private void SendChildrenToSchool()
    {
        if (Scenario is { Major: true })
        {
            return;
        }

        foreach (MemberId id in FamilyNames.Children)
        {
            FamilyMember child = State.Member(id);
            if (id == State.Controlled || child.Sick || child.Away || child.InDanger)
            {
                continue;
            }

            SendAway(child, ActivityId.School, 13f);
            State.School.Attend(id);
        }

        if (!State.Member(State.Controlled).Away && FamilyNames.IsChild(State.Controlled))
        {
            Bus.Notice(Loc.T("Waktunya sekolah! Pergi ke sekolah lewat peta (M) atau jalan kaki ke timur.", "School time! Use the map (M) or walk east to school."), "🏫", NoticeKind.Info);
        }
    }

    private void SendToWork(MemberId id, float untilHour)
    {
        FamilyMember member = State.Member(id);
        if (id == State.Controlled || member.Away || member.Sick || member.InDanger || Scenario is { Major: true })
        {
            return;
        }

        SendAway(member, ActivityId.Work, untilHour);
        Say(member, Loc.T("Ayah berangkat kerja dulu ya!", "Off to work, see you later!"));
        State.PendingSalary += 350_000;
    }

    private void SendOnErrand(MemberId id)
    {
        FamilyMember member = State.Member(id);
        if (id == State.Controlled || member.Away || Scenario is not null)
        {
            return;
        }

        SendAway(member, ActivityId.Errand, Hour + 1.25f);
        Say(member, Loc.T("Ibu belanja dulu ke pasar!", "Going to the market!"));
    }

    public void SendAway(FamilyMember member, ActivityId activity, float untilHour)
    {
        CancelTask(member);
        member.Away = true;
        member.AwayActivity = activity;
        double dayStart = Math.Floor(Now / GameClock.MinutesPerDay) * GameClock.MinutesPerDay;
        member.AwayUntil = dayStart + (untilHour * 60.0);
        if (member.AwayUntil <= Now)
        {
            member.AwayUntil = Now + 60;
        }
    }

    private void ReturnHome(FamilyMember member)
    {
        member.Away = false;
        member.Position = Rooms.FrontDoor + new Vector2(0.6f, 1.6f);
        member.Yaw = MathF.PI;
        switch (member.AwayActivity)
        {
            case ActivityId.School:
                member.Skills.Practice(Random.Pick(new[] { SkillKind.English, SkillKind.Math, SkillKind.Science, SkillKind.Art }), 0.4f);
                Say(member, Loc.T("Aku pulang! Tadi belajar seru!", "I'm home! School was fun!"));
                break;
            case ActivityId.Work:
                Say(member, Loc.T("Ayah pulang!", "Dad's home!"));
                break;
            case ActivityId.Errand:
                RestockGroceries(member);
                break;
        }
    }

    private void RestockGroceries(FamilyMember shopper)
    {
        long spent = 0;
        foreach ((string item, int want) in new[] { ("rice", 3), ("egg", 6), ("milk", 3), ("flour", 2), ("sugar", 2), ("vegetables", 3), ("snack", 4), ("chicken", 2), ("fruit", 2), ("noodles", 2), ("banana", 1), ("tea", 1), ("peanuts", 1) })
        {
            int need = want - State.Inventory.Count(item);
            if (need <= 0)
            {
                continue;
            }

            long cost = need * Economy.ItemCatalog.Get(item).Price;
            if (State.Wallet.Money - cost < 200_000)
            {
                continue;
            }

            State.Inventory.Add(item, need);
            spent += cost;
            State.Wallet.Pay(cost, Loc.T("Belanja mingguan", "Weekly groceries"), Clock.DayIndex);
        }

        if (spent > 0)
        {
            Bus.Notice(Loc.T($"Ibu pulang belanja ({Loc.Money(spent)}).", $"Mom bought groceries ({Loc.Money(spent)})."), "🛍", NoticeKind.Money);
            Say(shopper, Loc.T("Ibu pulang bawa belanjaan!", "I'm back with groceries!"));
        }
    }

    private void PaySalary(int day)
    {
        if (State.PendingSalary <= 0)
        {
            return;
        }

        State.Wallet.Earn(State.PendingSalary, Loc.T("Gaji Ayah minggu ini", "Dad's weekly pay"), day);
        Bus.Notice(Loc.T($"Gajian! +{Loc.Money(State.PendingSalary)}", $"Pay day! +{Loc.Money(State.PendingSalary)}"), "💰", NoticeKind.Money);
        Bus.Sound("coins");
        State.PendingSalary = 0;
    }

    private void PayBills(int day)
    {
        long bill = 250_000 + (State.House.IndoorRoomCount * 25_000) + (State.Pets.Count * 30_000);
        State.Wallet.Pay(bill, Loc.T("Tagihan listrik, air & internet", "Electricity, water & internet bills"), day);
        Bus.Notice(Loc.T($"Bayar tagihan rumah: {Loc.Money(bill)}", $"Paid the house bills: {Loc.Money(bill)}"), "🧾", NoticeKind.Money);
    }

    private void OnNewDay()
    {
        State.AddStat(Stat.DaysPlayed);
        GameDate date = Date;

        // Leftovers spoil after two days.
        State.Pantry.RemoveAll(p => Clock.DayIndex - p.CookedDay > 2 || p.Servings <= 0);

        // Forecast for tomorrow, and a Great Storm warning in the big adventure chapter.
        State.Weather.Forecast = WeatherSystem.Next(State.Weather.Current, State.Mode, Random, date.Month);
        if (State.Weather.StormDay < 0 && State.Chapter >= 5 && !State.Flag("great-storm-done") && Random.Chance(State.Mode == GameMode.Cozy ? 0.25f : 0.45f))
        {
            State.Weather.StormDay = Clock.DayIndex + 2;
        }

        if (State.Weather.StormDay == Clock.DayIndex + 1)
        {
            State.Weather.Forecast = WeatherKind.SevereStorm;
            StartScenario(ScenarioKind.GreatStorm);
        }

        foreach (FamilyMember member in State.Members)
        {
            // Small chance of catching a cold, more likely when tired or unwashed.
            float risk = 0.01f + (member.Needs[NeedKind.Sleep] < 30 ? 0.03f : 0f) + (member.Needs[NeedKind.Hygiene] < 25 ? 0.03f : 0f);
            if (!member.Sick && Random.Chance(risk * (State.Mode == GameMode.Cozy ? 0.5f : 1f)))
            {
                member.Sick = true;
                member.Needs[NeedKind.Health] = MathF.Min(member.Needs[NeedKind.Health], 55f);
                Bus.Notice(Loc.T($"{member.Name} sedang flu. Buat sup ayam atau ke klinik.", $"{member.Name} caught a cold. Make chicken soup or visit the clinic."), "🤒", NoticeKind.Warning);
            }
        }

        // Pets: the water bowl and food dish.
        foreach (Pet pet in State.Pets)
        {
            if (pet.Hunger < 30 && State.Inventory.Take("pet-food"))
            {
                pet.Hunger = 90;
            }
        }
    }

    // ---------------------------------------------------------------- weather

    private void UpdateWeather(float minutes)
    {
        WeatherState w = State.Weather;
        bool stormDay = w.StormDay == Clock.DayIndex && Hour >= 16f;
        if (stormDay && w.Current != WeatherKind.SevereStorm)
        {
            SetWeather(WeatherKind.SevereStorm);
        }
        else if (!stormDay && Now >= w.NextChange)
        {
            WeatherKind next = w.Current == WeatherKind.SevereStorm && w.StormDay < Clock.DayIndex
                ? WeatherKind.Rain
                : WeatherSystem.Next(w.Current, State.Mode, Random, Date.Month);
            SetWeather(next);
        }

        if (w.IsStormy)
        {
            w.LightningTimer -= minutes;
            if (w.LightningTimer <= 0f)
            {
                w.LightningTimer = Random.Range(2.5f, w.Current == WeatherKind.SevereStorm ? 6f : 12f);
                Lightning();
            }
        }
    }

    public void SetWeather(WeatherKind kind)
    {
        WeatherState w = State.Weather;
        WeatherKind previous = w.Current;
        w.Current = kind;
        w.Intensity = WeatherSystem.IntensityFor(kind, Random);
        w.NextChange = Now + Random.Range(150f, 330f);
        if (previous == kind)
        {
            return;
        }

        Bus.Notice(Loc.T($"Cuaca: {WeatherState.Name(kind)}", $"Weather: {WeatherState.Name(kind)}"), WeatherState.Icon(kind, Clock.IsNight), kind is WeatherKind.Thunderstorm or WeatherKind.SevereStorm ? NoticeKind.Warning : NoticeKind.Info);

        if (w.IsRaining)
        {
            // Rain makes everyone outside run home (or at least under a roof).
            foreach (FamilyMember m in State.Members.Where(m => !m.Away && m.Id != State.Controlled && !State.House.IsIndoors(m.Position) && Rooms.AtHome(m.Position)))
            {
                CancelTask(m);
                Say(m, Loc.T("Hujan! Masuk rumah!", "It's raining! Inside!"));
                StartTask(m, ActivityId.Idle, null, -1, target: new Vector2(-3.4f, 3.5f), run: true);
            }
        }
    }

    private void Lightning()
    {
        Bus.Publish(new ScreenFlashEvent(Settings.ReducedIntensity ? 0.25f : 0.8f));
        Bus.Sound("thunder", null, 1f);
        foreach (FamilyMember m in State.Members.Where(m => !m.Away))
        {
            float fear = m.Personality.Fearfulness * (m.Personality.Fears.Contains("thunder") ? 1.6f : 1f);
            if (fear > 0.35f && Random.Chance(fear))
            {
                m.Mood.Add("thunder", Loc.T("Takut petir!", "Scared of thunder!"), -18, MoodKind.Scared, Now, 25);
                if (Random.Chance(0.35f))
                {
                    Say(m, m.Id == MemberId.YoungerSister ? Loc.T("Kakak, aku takut petir...", "I'm scared of the thunder...") : Loc.T("Aduh, petirnya keras!", "That thunder is loud!"),
                        m.Id == MemberId.YoungerSister ? "ys_scared" : m.Id == MemberId.OlderSister ? "os_scared" : null);
                }
            }
        }

        // Thunderstorms can knock the power out (design section 12).
        float outage = State.Weather.Current == WeatherKind.SevereStorm ? 0.35f : 0.12f;
        if (State.House.PowerOn && Scenario is null && Random.Chance(outage * Director.DangerMultiplier(Rarity.Uncommon)))
        {
            StartScenario(ScenarioKind.PowerOutage);
        }
    }

    // ------------------------------------------------------------------ needs

    private void UpdateNeeds(FamilyMember m, float minutes)
    {
        Needs n = m.Needs;
        bool child = m.IsChild;
        ActivityId activity = m.CurrentActivity;
        bool sleeping = activity is ActivityId.Sleep or ActivityId.Nap && m.Task?.Phase == TaskPhase.Performing;

        n.Add(NeedKind.Hunger, -0.07f * minutes * (child ? 1.1f : 1f));
        n.Add(NeedKind.Hygiene, -0.035f * minutes);
        n.Add(NeedKind.Fun, -(child ? 0.07f : 0.05f) * minutes);
        n.Add(NeedKind.Social, -0.04f * minutes * (0.6f + m.Personality.Sociability));
        if (!sleeping)
        {
            n.Add(NeedKind.Sleep, -0.065f * minutes);
            n.Add(NeedKind.Energy, -0.05f * minutes * (m.Moving ? 1.6f : 1f));
        }

        // Health follows how well the body is looked after.
        bool neglected = n[NeedKind.Hunger] < 12 || n[NeedKind.Sleep] < 10 || n[NeedKind.Hygiene] < 8;
        float healthRate = neglected ? -0.04f : m.Sick ? -0.01f : 0.02f;
        n.Add(NeedKind.Health, healthRate * minutes);
        if (m.Sick && n[NeedKind.Health] > 90)
        {
            m.Sick = false;
            Bus.Notice(Loc.T($"{m.Name} sudah sembuh!", $"{m.Name} feels better!"), "💪", NoticeKind.Good);
        }

        // Stamina recovers slowly when not exerting.
        if (!m.Moving || !m.Running)
        {
            float regen = m.Task?.Phase == TaskPhase.Performing ? m.Task.Def.StaminaPerMinute : 0.25f;
            if (regen > 0)
            {
                m.Stamina.Value += regen * minutes * (n[NeedKind.Energy] > 30 ? 1f : 0.4f);
            }
        }

        m.Mood.Update(n, Now);
        float target = Math.Clamp(50f + (m.Mood.Score * 0.5f), 0f, 100f);
        n[NeedKind.Happiness] += (target - n[NeedKind.Happiness]) * MathF.Min(1f, minutes * 0.02f);
    }

    // ------------------------------------------------------------- utilities

    public void Say(FamilyMember member, string text, string? voiceKey = null)
    {
        member.Say(text, Now);
        Bus.Publish(new SpeechEvent(member.Id, member.Name, text, voiceKey));
    }

    public FamilyMember? NearestMember(Vector2 p, Func<FamilyMember, bool> filter)
    {
        FamilyMember? best = null;
        float bestD = float.MaxValue;
        foreach (FamilyMember m in State.Members)
        {
            if (m.Away || !filter(m))
            {
                continue;
            }

            float d = Vector2.DistanceSquared(m.Position, p);
            if (d < bestD)
            {
                bestD = d;
                best = m;
            }
        }

        return best;
    }

    public RoomId? RoomOf(FamilyMember m) => m.Away ? null : State.House.RoomAt(m.Position);

    public PlaceId? PlaceOf(Vector2 p) => Map.PlaceAt(p);

    public string LocationName(FamilyMember m)
    {
        if (m.Away)
        {
            return m.AwayActivity switch
            {
                ActivityId.School => WorldMap.Name(PlaceId.School),
                ActivityId.Work => Loc.T("Kantor", "Work"),
                _ => Loc.T("Pasar", "Market"),
            };
        }

        if (RoomOf(m) is { } room)
        {
            return Rooms.Name(room);
        }

        return PlaceOf(m.Position) is { } place ? WorldMap.Name(place) : Loc.T("Di jalan", "Out and about");
    }

    // ----------------------------------------------------------- scenarios

    public void StartScenario(ScenarioKind kind)
    {
        if (Scenario is not null)
        {
            return;
        }

        Scenario scenario = Scenarios.Scenario.Create(kind, this);
        if (scenario.Major)
        {
            // "Restart Event" restores the family exactly as it was.
            _snapshot = SaveSystem.Serialize(State);
            _snapshotKind = kind;
        }

        Scenario = scenario;
        foreach (FamilyMember m in State.Members.Where(m => !m.Away && m.Id != State.Controlled && scenario.InterruptOnStart(m)))
        {
            CancelTask(m);
            m.IdleTimer = 0f;
        }

        scenario.Start();
        Bus.Publish(new ScenarioEvent(scenario.Title, "start"));
        if (scenario.Major)
        {
            Bus.Publish(new MusicEvent(MusicMood.Tense));
        }
    }

    private void EndScenario()
    {
        Scenario? scenario = Scenario;
        if (scenario is null)
        {
            return;
        }

        Scenario = null;
        _snapshot = null;
        _snapshotKind = null;
        Director.OnScenarioEnded(scenario);
        if (scenario.Succeeded)
        {
            if (scenario.Major)
            {
                State.AddStat(Stat.EmergenciesHandled);
            }

            Bus.Publish(new ScenarioEvent(scenario.Title, "success"));
        }
        else
        {
            Bus.Publish(new ScenarioEvent(scenario.Title, "ended"));
        }

        Bus.Publish(new MusicEvent(Clock.IsNight ? MusicMood.Night : MusicMood.Cozy));
        foreach (FamilyMember m in State.Members)
        {
            if (m.Safety is SafetyState.Following or SafetyState.Safe)
            {
                m.Safety = SafetyState.Normal;
                m.FollowTarget = null;
            }
        }
    }

    /// <summary>Called by a scenario when someone was left behind.</summary>
    public void FailScenario(string reason)
    {
        if (Scenario is null)
        {
            return;
        }

        ScenarioFailed = true;
        FailureReason = reason;
        Bus.Publish(new ScenarioEvent(Scenario.Title, "failed"));
        Bus.Publish(new MusicEvent(MusicMood.Emotional));
    }

    /// <summary>"Restart Event": back to the moment the scenario began.</summary>
    public void RestartScenario()
    {
        if (_snapshot is null || _snapshotKind is not { } kind)
        {
            ScenarioFailed = false;
            Scenario = null;
            return;
        }

        State = SaveSystem.Deserialize(_snapshot);
        Scenario = null;
        ScenarioFailed = false;
        PendingMiniGame = null;
        foreach (FamilyMember m in State.Members)
        {
            m.Task = null;
            m.Anchor = null;
            m.Pose = AnchorPose.Stand;
        }

        RebuildCollision();
        Bus.Clear();
        StartScenario(kind);
    }

    /// <summary>Ends the current event without restarting: everyone is brought to safety (offered in Cozy mode).</summary>
    public void AbandonScenario()
    {
        if (Scenario is not null)
        {
            Director.OnScenarioEnded(Scenario);
        }

        _snapshot = null;
        _snapshotKind = null;
        State.House.PowerOn = true;
        State.House.Hazards.RemoveAll(h => h.Kind is HazardKind.Fire or HazardKind.Flood);
        foreach (FamilyMember m in State.Members.Where(m => m.InDanger || m.Safety is SafetyState.Following or SafetyState.Safe))
        {
            if (m.InDanger)
            {
                m.Position = Rooms.Get(RoomId.LivingRoom).Area.Center + new Vector2(0.5f * (int)m.Id - 1f, 0.8f);
            }

            m.Safety = SafetyState.Normal;
            m.FollowTarget = null;
        }

        ScenarioFailed = false;
        Scenario = null;
        ScenarioFailed = false;
        RebuildCollision();
    }

    private void TickHazards(float minutes)
    {
        House house = State.House;
        foreach (Hazard hazard in house.Hazards.ToList())
        {
            switch (hazard.Kind)
            {
                case HazardKind.Smoke:
                    hazard.Intensity -= minutes * (house.WindowsClosed ? 0.01f : 0.04f);
                    break;
                case HazardKind.Flour:
                    hazard.Intensity -= minutes * 0.002f;
                    break;
            }

            if (hazard.Intensity <= 0f)
            {
                house.Hazards.Remove(hazard);
            }
        }
    }
}
