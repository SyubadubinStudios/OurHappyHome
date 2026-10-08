using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;

namespace OurHappyHome.Views;

/// <summary>
/// <c>--screenshots &lt;folder&gt;</c>: plays through the menus and a scripted
/// slice of the game, saving PNGs of the real window for the documentation,
/// then quits. It drives the same code paths a player uses.
/// </summary>
public sealed class ScreenshotDirector(MainWindow window, string folder)
{
    private int _index;

    public async Task RunAsync()
    {
        Directory.CreateDirectory(folder);
        SaveSystem.Folder = Path.Combine(folder, "_saves");
        await Wait(3);
        await Shot("main-menu");

        window.ShowAbout();
        await Wait(4);
        await Shot("about-credits");

        window.ShowMenu();
        await Wait(1);
        GameSession session = GameSession.NewGame(GameMode.Normal, "Raka", window.Settings, 2024);
        session.State.SaveName = "Raka · Normal";
        window.StartGame(session);
        await WaitForGame();
        GameScreen game = window.Game!;
        game.ClosePanel();
        if (Environment.GetEnvironmentVariable("OHH_SHOTS_ONLY") == "house")
        {
            // Dev shortcut: only the two-storey house shots.
            await HouseShots(game);
            Dispatcher.UIThread.Post(() => window.Close());
            return;
        }

        if (Environment.GetEnvironmentVariable("OHH_SHOTS_ONLY") == "scenes")
        {
            // Dev shortcut: only the outdoor trip scenes, from several angles.
            await ScenesAsync(game);
            Dispatcher.UIThread.Post(() => window.Close());
            return;
        }

        game.Renderer!.Rig.Yaw = 0.35f;
        await Simulate(game, 20f * 60f / 60f, realSeconds: 6);
        await Shot("morning-kids-room");

        // Breakfast together at the dining table.
        await JumpTo(game, 6.9f);
        session.Controlled.Position = new Vector2(1.2f, 3.2f);
        await Simulate(game, 0, realSeconds: 8);
        await Shot("breakfast-together");

        game.OpenPanel("family");
        await Wait(1.2);
        await Shot("family-panel");
        game.ClosePanel();

        game.OpenPanel("journal");
        await Wait(1.2);
        await Shot("journal-chapters");
        game.ClosePanel();

        // Cooking mini-game.
        session.Bus.Publish(new MiniGameRequest("cook", "pancakes"));
        await Wait(1.5);
        await Shot("cooking-minigame");
        game.CloseMiniGameForCapture();

        // School lesson.
        session.Bus.Publish(new MiniGameRequest("school", ""));
        await Wait(1);
        game.StartSchoolForCapture();
        await Wait(1);
        await Shot("school-lesson");
        game.CloseMiniGameForCapture();

        // Build mode with a new room.
        session.State.Wallet.Money = 20_000_000;
        session.BuildRoom(RoomId.GirlsBedroom);
        session.BuildRoom(RoomId.Garden);
        game.OpenPanel("build");
        await Wait(3);
        await Shot("build-mode");
        game.ExitBuildForCapture();

        // Outside the house in the afternoon.
        await JumpTo(game, 15.5f);
        session.Controlled.Position = new Vector2(-1.2f, 13f);
        session.Controlled.Yaw = MathF.PI;
        game.Renderer!.Rig.Yaw = 0.2f;
        await Simulate(game, 0, realSeconds: 5);
        await Shot("house-exterior");

        // Evening rain and a power outage: the family gathers.
        await JumpTo(game, 19.6f);
        session.Controlled.Position = new Vector2(-2.5f, 3.2f);
        session.SetWeather(WeatherKind.Thunderstorm);
        await Simulate(game, 0, realSeconds: 3);
        await Shot("night-thunderstorm");
        await EndScenario(session);
        session.StartScenario(ScenarioKind.PowerOutage);
        session.FlashlightOn = true;
        await Simulate(game, 0, realSeconds: 6);
        await Shot("power-outage");
        await EndScenario(session);

        // A kitchen fire with a rescue countdown.
        session.SetWeather(WeatherKind.Cloudy);
        await JumpTo(game, 17.2f);
        session.Controlled.Position = new Vector2(2.4f, 2.0f);
        await EndScenario(session);
        session.StartScenario(ScenarioKind.SmallFire);
        await Simulate(game, 0, realSeconds: 4);
        await Shot("emergency-fire");
        await EndScenario(session);

        // Family trips.
        await JumpTo(game, 10.5f);
        session.SetWeather(WeatherKind.Sunny);
        session.Travel(PlaceId.Park, true);
        game.Renderer!.Rig.Yaw = session.Map.Get(PlaceId.Park).EntranceYaw + MathF.PI;
        game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
        await Simulate(game, 0, realSeconds: 5);
        await Shot("trip-park");

        session.Travel(PlaceId.Beach, true);
        game.Renderer!.Rig.Yaw = session.Map.Get(PlaceId.Beach).EntranceYaw + MathF.PI;
        game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
        await JumpTo(game, 17.1f);
        session.SetWeather(WeatherKind.Sunny);
        await Simulate(game, 0, realSeconds: 5);
        await Shot("trip-beach-sunset");

        session.Travel(PlaceId.Camping, true);
        game.Renderer!.Rig.Yaw = session.Map.Get(PlaceId.Camping).EntranceYaw + MathF.PI;
        await JumpTo(game, 20.8f);
        session.Controlled.Position = new Vector2(-24f, -221f);
        game.Renderer!.Rig.Snap(new Vector3(-24f, 1f, -221f));
        await Simulate(game, 0, realSeconds: 5);
        await Shot("trip-camping-night");

        session.Travel(PlaceId.School, false);
        game.Renderer!.Rig.Yaw = session.Map.Get(PlaceId.School).EntranceYaw + MathF.PI;
        await JumpTo(game, 9f);
        await Simulate(game, 0, realSeconds: 4);
        await Shot("town-school");

        game.OpenPanel("map");
        await Wait(1.5);
        await Shot("town-map");
        game.ClosePanel();

        // Decorate a few album photos with frames and stickers.
        string[] frames = ["gold", "pastel", "wood", "film"];
        foreach ((FamilyMemory memory, int i) in session.State.Memories.AsEnumerable().Reverse().Take(4).Select((m, i) => (m, i)))
        {
            memory.Frame = frames[i];
            memory.Stickers = [.. new[] { "⭐", "💖", "🌈" }.Take(1 + (i % 3))];
        }

        game.OpenPanel("album");
        await Wait(1.5);
        await Shot("family-album");
        game.ClosePanel();

        game.OpenPanel("pause");
        await Wait(1);
        await Shot("pause-menu");
        game.ClosePanel();

        // v1.1: rigged neighbours, pets and scenario visitors.
        session.Travel(PlaceId.Home, true);
        await JumpTo(game, 16f);
        session.SetWeather(WeatherKind.Sunny);
        session.AdoptPet(PetKind.Dog, "Coco");
        session.AdoptPet(PetKind.Cat, "Mochi");
        Npc grandma = session.Npcs.First(n => n.Id == "grandma");
        session.Controlled.Position = grandma.Home + new Vector2(-1.6f, 2.2f);
        grandma.Position = grandma.Home;
        grandma.TalkTo(session.Controlled.Position, 8f);
        session.Controlled.Yaw = MathF.Atan2(grandma.Position.X - session.Controlled.Position.X, grandma.Position.Y - session.Controlled.Position.Y);
        foreach ((Pet pet, int i) in session.State.Pets.Select((p, i) => (p, i)))
        {
            pet.Position = session.Controlled.Position + new Vector2(0.9f + (i * 0.7f), 0.6f);
        }

        await EndScenario(session);
        game.Renderer!.Rig.Yaw = 0.5f;
        game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
        await Simulate(game, 0, realSeconds: 3);
        await Shot("neighbours-pets");

        await VisitorShot(game, ScenarioKind.MonkeyThief, "monkey-thief");
        await VisitorShot(game, ScenarioKind.SuspiciousStranger, "stranger-visitor");

        // Enterable buildings.
        session.SetWeather(WeatherKind.Sunny);
        await InteriorShot(game, PlaceId.School, 9.5f, "interior-classroom");
        await InteriorShot(game, PlaceId.Supermarket, 10.5f, "interior-supermarket");
        await InteriorShot(game, PlaceId.Clinic, 11.5f, "interior-clinic");

        // Costumes from the dress-up box.
        session.Travel(PlaceId.Home, true);
        await JumpTo(game, 16.5f);
        session.SetWeather(WeatherKind.Sunny);
        string[] hats = ["straw-hat", "crown", "cat-ears", "party-hat", "beanie"];
        foreach (string hat in hats)
        {
            session.State.Inventory.Add(hat, 1);
        }

        foreach ((FamilyMember m, int i) in session.State.Members.Select((m, i) => (m, i)))
        {
            session.SetAccessory(m.Id, hats[i]);
        }

        // Line up in the front yard facing the camera.
        await Simulate(game, 0, realSeconds: 1);
        session.Paused = true;
        foreach ((FamilyMember m, int i) in session.State.Members.Select((m, i) => (m, i)))
        {
            m.Task = null;
            m.Anchor = null;
            m.Pose = AnchorPose.Stand;
            m.Moving = false;
            m.Position = new Vector2(-3.4f + (i * 1.1f), 11.2f);
            m.Yaw = 0f;
        }

        game.Renderer!.Rig.Yaw = 0f;
        game.Renderer!.Rig.Zoom = 0.55f;
        game.Renderer!.Rig.Snap(new Vector3(-1.2f, 1f, 11.2f));
        await Wait(0.6);
        await Shot("family-costumes");
        game.Renderer!.Rig.Zoom = 1f;
        session.Paused = false;
        game.OpenPanel("wardrobe");
        await Wait(1.2);
        await Shot("wardrobe");
        game.ClosePanel();
        foreach (FamilyMember m in session.State.Members)
        {
            session.SetAccessory(m.Id, null); // back to dressing for the weather
        }

        // v1.2: a good friend visits on Sunday morning.
        session.Travel(PlaceId.Home, false);
        session.State.Friendships["dimas"] = 35f;
        await SkipTo(game, 1, 23, 9.9f);
        session.SetWeather(WeatherKind.Sunny);
        await Simulate(game, 0, realSeconds: 3);
        Npc dimas = session.Npcs.First(n => n.Id == "dimas");
        session.Controlled.Position = dimas.Position + new Vector2(-1.4f, 1.2f);
        session.Controlled.Yaw = MathF.Atan2(1.4f, -1.2f);
        game.Renderer!.Rig.Yaw = 0.6f;
        game.Renderer!.Rig.Zoom = 0.7f;
        game.Renderer!.Rig.Snap(new Vector3(dimas.Position.X, 1f, dimas.Position.Y));
        await Wait(1.2);
        await Shot("dimas-visit");
        game.Renderer!.Rig.Zoom = 1f;

        // Rain: everyone outside puts on a rain hat by themselves.
        session.SetWeather(WeatherKind.Rain);
        await SkipTo(game, 1, 23, 10.985f);
        session.Paused = true;
        foreach ((FamilyMember m, int i) in session.State.Members.Select((m, i) => (m, i)))
        {
            m.Task = null;
            m.Anchor = null;
            m.Pose = AnchorPose.Stand;
            m.Moving = false;
            m.Position = new Vector2(-3.4f + (i * 1.1f), 11.2f);
            m.Yaw = 0f;
        }

        session.Paused = false;
        await Wait(1.5); // the 11:00 check dresses them
        session.Paused = true;
        game.Renderer!.Rig.Yaw = 0f;
        game.Renderer!.Rig.Zoom = 0.55f;
        game.Renderer!.Rig.Snap(new Vector3(-1.2f, 1f, 11.2f));
        await Wait(0.8);
        await Shot("rainy-day-hats");
        game.Renderer!.Rig.Zoom = 1f;
        session.Paused = false;

        // The monthly town festival in the park.
        session.SetWeather(WeatherKind.Sunny);
        await SkipTo(game, 1, 24, 16.4f);
        session.Travel(PlaceId.Park, true);
        session.Controlled.Position = new Vector2(-44f, 47.6f);
        session.Controlled.Yaw = MathF.PI;
        game.Renderer!.Rig.Yaw = 0f;
        game.Renderer!.Rig.Snap(new Vector3(-44f, 1f, 47f));
        await Simulate(game, 0, realSeconds: 3);
        await Shot("festival-park");

        await FestivalGame(game, "kerupuk", "kerupuk-minigame");
        await FestivalGame(game, "tug", "tug-of-war");

        await SkipTo(game, 1, 24, 20.02f);
        session.Controlled.Position = new Vector2(-31f, 54f);
        session.Controlled.Yaw = MathF.PI;
        game.Renderer!.Rig.Yaw = 0f;
        game.Renderer!.Rig.PitchOffset = -0.7f;
        game.Renderer!.Rig.Zoom = 1.2f;
        game.Renderer!.Rig.Snap(new Vector3(-31f, 1f, 54f));
        await Wait(2.4);
        await Shot("festival-fireworks");
        game.Renderer!.Rig.PitchOffset = 0f;
        game.Renderer!.Rig.Zoom = 1f;

        // A night in the tent at the campsite.
        session.State.Inventory.Add("tent");
        await SkipTo(game, 1, 29, 19f);
        session.Travel(PlaceId.Camping, true);
        session.StayOvernight(PlaceId.Camping);
        session.Controlled.Position = new Vector2(-24f, -221f);
        game.Renderer!.Rig.Yaw = MathF.PI;
        game.Renderer!.Rig.Snap(new Vector3(-24f, 1f, -221f));
        await Simulate(game, 0, realSeconds: 4);
        await Shot("camping-morning");

        // v1.3: lively beach, the scouts' camp and the new sky.
        await SkipTo(game, 2, 5, 11f);
        session.SetWeather(WeatherKind.Sunny);
        session.Travel(PlaceId.Beach, true);
        session.SetWeather(WeatherKind.Sunny);
        game.Renderer!.Rig.Yaw = MathF.PI + 0.25f;
        game.Renderer!.Rig.Zoom = 1.25f;
        game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
        await Simulate(game, 0, realSeconds: 4);
        await Shot("beach-life");
        game.Renderer!.Rig.Zoom = 1f;

        await SkipTo(game, 2, 5, 15.5f);
        session.Travel(PlaceId.Camping, true);
        session.SetWeather(WeatherKind.Sunny);
        session.Controlled.Position = new Vector2(-6f, -236f);
        session.Controlled.Yaw = MathF.Atan2(15f, -9f);
        game.Renderer!.Rig.Yaw = session.Controlled.Yaw + MathF.PI;
        game.Renderer!.Rig.Zoom = 0.75f;
        game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
        await Simulate(game, 0, realSeconds: 4);
        await Shot("camp-scouts");
        game.Renderer!.Rig.Zoom = 1f;

        await SkipTo(game, 2, 5, 17.55f);
        session.SetWeather(WeatherKind.Sunny);
        session.Controlled.Position = new Vector2(-46f, -244f);
        session.Controlled.Yaw = MathF.Atan2(-12f, -12f);
        game.Renderer!.Rig.Yaw = session.Controlled.Yaw + MathF.PI;
        game.Renderer!.Rig.PitchOffset = -0.2f;
        game.Renderer!.Rig.Snap(new Vector3(-46f, 1f, -244f));
        await Simulate(game, 0, realSeconds: 3);
        await Shot("golden-hour-sky");
        game.Renderer!.Rig.PitchOffset = 0f;

        // v1.4: the two-storey house, the stairs, upstairs rooms, bicycle and bus stop.
        session.Travel(PlaceId.Home, false);
        await HouseShots(game);

        Dispatcher.UIThread.Post(() => window.Close());
    }

    private static Task Wait(double seconds) => Task.Delay(TimeSpan.FromSeconds(seconds));

    private async Task WaitForGame()
    {
        for (int i = 0; i < 200 && window.Game?.Renderer is null; i++)
        {
            await Wait(0.1);
        }

        await Wait(3);
    }

    /// <summary>Lets the game run at high speed for some game minutes, then normally for a while.</summary>
    private static async Task Simulate(GameScreen game, float gameMinutes, double realSeconds)
    {
        Refill(game.Session);
        if (gameMinutes > 0)
        {
            game.Session.SetSpeed(4f);
            await Wait(gameMinutes / 4f / 60f * 60f);
            game.Session.SetSpeed(1f);
        }

        await Wait(realSeconds);
    }

    private static async Task JumpTo(GameScreen game, float hour)
    {
        GameSession s = game.Session;
        double day = Math.Floor(s.Now / 1440.0) * 1440.0;
        double target = day + (hour * 60.0);
        if (target <= s.Now)
        {
            target += 1440.0;
        }

        // Fast-forward in small steps so the family keeps living normally.
        game.ClosePanel();
        int guard = 0;
        while (s.Now < target && guard++ < 20000)
        {
            s.Paused = false;
            if (s.ScenarioFailed)
            {
                s.AbandonScenario();
            }

            s.Tick(0.1f);
            if (s.PendingMiniGame is not null)
            {
                s.CompleteMiniGame(0.8f);
            }

            s.State.Clock.Speed = 40f;
            if ((int)(s.Now * 10) % 200 == 0)
            {
                await Task.Yield();
            }
        }

        s.State.Clock.Speed = 1f;
        Refill(s);
        await Wait(0.5);
    }

    /// <summary>The scripted player has no time to eat: keep the HUD looking like a cared-for family.</summary>
    private static void Refill(GameSession session)
    {
        foreach (FamilyMember m in session.State.Members)
        {
            foreach (NeedKind need in Needs.All)
            {
                m.Needs[need] = MathF.Max(m.Needs[need], 72f + ((int)need * 3));
            }

            m.Sick = false;
        }
    }

    /// <summary>The two-storey house: outside, on the stairs, upstairs and in build mode.</summary>
    private async Task HouseShots(GameScreen game)
    {
        GameSession session = game.Session;
        House house = session.State.House;
        foreach (RoomId room in new[] { RoomId.UpperHall, RoomId.Attic, RoomId.Studio })
        {
            house.Build(room);
        }

        await SkipTo(game, 1, 8, 10f);
        session.SetWeather(WeatherKind.Sunny);

        // Outside, from the front garden and from the back.
        session.Controlled.Position = new Vector2(-2f, 12.5f);
        session.Controlled.Yaw = MathF.PI;
        game.Renderer!.Rig.Yaw = 0.35f;
        game.Renderer!.Rig.Zoom = 1.6f;
        game.Renderer!.Rig.Snap(new Vector3(-2f, 1f, 12.5f));
        await Simulate(game, 0, realSeconds: 3);
        await Shot("two-storey-front");
        game.Renderer!.Rig.Yaw = MathF.PI - 0.5f;
        session.Controlled.Position = new Vector2(2f, -12f);
        game.Renderer!.Rig.Snap(new Vector3(2f, 1f, -12f));
        await Wait(1.5);
        await Shot("two-storey-back");
        game.Renderer!.Rig.Zoom = 1f;

        // Climbing the stairs.
        session.Controlled.Position = Floors.StairBottom;
        game.Renderer!.Rig.Yaw = MathF.PI + 0.6f;
        game.Renderer!.Rig.Snap(new Vector3(Floors.StairBottom.X, 1f, Floors.StairBottom.Y));
        await Wait(1);
        session.StartClimb(session.Controlled);
        await Wait(0.9);
        await Shot("stairs-climb");
        await Wait(1.5);

        // Upstairs: the studio, the attic and the balcony.
        foreach ((RoomId room, string name, float yaw) in new[] { (RoomId.Studio, "upstairs-studio", 0.4f), (RoomId.Attic, "upstairs-attic", -0.4f), (RoomId.Balcony, "upstairs-balcony", 0.5f) })
        {
            Vector2 c = Rooms.Get(room).Area.Center;
            session.Controlled.Position = c + new Vector2(0f, 1.2f);
            game.Renderer!.Rig.Yaw = yaw;
            game.Renderer!.Rig.Snap(Floors.ToRender(c, 1f));
            await Simulate(game, 0, realSeconds: 2.5);
            await Shot(name);
        }

        // Build mode on the upper floor.
        session.Controlled.Position = Rooms.Get(RoomId.Studio).Area.Center;
        game.OpenPanel("build");
        await Wait(1);
        game.Renderer!.House.BuildUpper = true;
        game.Renderer!.Rig.BuildCenter = new Vector3(0f, Floors.Height, -2f);
        await Wait(1.5);
        await Shot("build-upper-floor");
        game.ExitBuildForCapture();

        // Riding the bicycle past the bus stop on the main street.
        session.State.Inventory.Add("bicycle");
        session.Controlled.Position = new Vector2(-2f, 19.8f);
        session.Controlled.Yaw = MathF.PI / 2f;
        game.Renderer!.Rig.Yaw = -MathF.PI / 2f + 0.5f;
        game.Renderer!.Rig.Snap(new Vector3(-2f, 1f, 19.8f));
        session.StartTask(session.Controlled, ActivityId.Idle, null, -1, target: new Vector2(40f, 19.8f), fromPlayer: true, minutes: 1);
        await Wait(1.0);
        game.Renderer!.Rig.Snap(Floors.ToRender(session.Controlled.Position, 1f));
        await Wait(0.4);
        await Shot("bicycle-and-halte");
    }

    /// <summary>The beach and the campsite by day and night, looking four ways.</summary>
    private async Task ScenesAsync(GameScreen game)
    {
        GameSession session = game.Session;
        foreach ((PlaceId place, int day, float hour) in new[] { (PlaceId.Beach, 8, 10.5f), (PlaceId.Beach, 8, 17.6f), (PlaceId.Camping, 8, 10.5f), (PlaceId.Camping, 8, 21f) })
        {
            await SkipTo(game, 1, day, hour);
            session.SetWeather(WeatherKind.Sunny);
            session.Travel(place, true);
            await Simulate(game, 0, realSeconds: 3);
            for (int i = 0; i < 4; i++)
            {
                game.Renderer!.Rig.Yaw = session.Controlled.Yaw + MathF.PI + (i * MathF.PI / 2f);
                game.Renderer!.Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
                await Wait(1.2);
                await Shot($"scene-{place.ToString().ToLowerInvariant()}-{(int)hour:00}-{i}");
            }

            // Looking out towards the horizon to check the sky.
            game.Renderer!.Rig.PitchOffset = -0.22f;
            game.Renderer!.Rig.Yaw = session.Controlled.Yaw + MathF.PI;
            await Wait(1.2);
            await Shot($"scene-{place.ToString().ToLowerInvariant()}-{(int)hour:00}-sky");
            game.Renderer!.Rig.PitchOffset = 0f;
        }
    }

    /// <summary>Jumps the calendar straight to a date and hour (no simulation in between).</summary>
    private static async Task SkipTo(GameScreen game, int month, int day, float hour)
    {
        GameSession s = game.Session;
        game.ClosePanel();
        await EndScenario(s);
        s.State.Clock.TotalMinutes = (GameDate.ToDayIndex(1, month, day) * 1440.0) + (hour * 60.0);
        s.State.ScheduleCursor = s.State.Clock.TotalMinutes - 0.5;
        s.State.Clock.Speed = 1f;
        Refill(s);
        await Wait(0.6);
    }

    /// <summary>Opens a festival contest at its stall and captures the mini-game mid-play.</summary>
    private async Task FestivalGame(GameScreen game, string stall, string name)
    {
        GameSession session = game.Session;
        TownFeature feature = session.Map.Features.First(f => f.Kind == FeatureKind.FestivalStall && f.Label == stall);
        session.Controlled.Position = feature.Area.Center + new Vector2(0f, 1.7f);
        await Wait(0.3);
        if (session.GetInteractions().SelectMany(t => t.Options).FirstOrDefault(o => o.Id == stall) is { Enabled: true } option)
        {
            option.Execute();
            await Wait(5.5);
            await Shot(name);
            game.CloseMiniGameForCapture();
            session.CompleteMiniGame(0.8f);
        }
    }

    /// <summary>Takes the family inside a town building at the given hour.</summary>
    private async Task InteriorShot(GameScreen game, PlaceId place, float hour, string name)
    {
        GameSession session = game.Session;
        await EndScenario(session);
        session.Travel(place, true);
        await JumpTo(game, hour);
        await EndScenario(session);
        session.EnterInterior(place);
        await Simulate(game, 0, realSeconds: 3);
        await Shot(name);
        session.ExitInterior();
    }

    /// <summary>Starts a scenario and frames its first visitor.</summary>
    private async Task VisitorShot(GameScreen game, ScenarioKind kind, string name)
    {
        GameSession session = game.Session;
        await EndScenario(session);
        session.StartScenario(kind);
        await Simulate(game, 0, realSeconds: 3);
        if (session.Scenario?.Actors.FirstOrDefault(a => a.Visible) is { } actor)
        {
            // Close enough to frame, far enough not to scare the visitor away.
            session.Controlled.Position = actor.Position + new Vector2(2.2f, 3.2f);
            game.Renderer!.Rig.Snap(new Vector3(actor.Position.X, 1f, actor.Position.Y));
        }

        game.Renderer!.Rig.Zoom = 0.6f;
        await Wait(0.8);
        await Shot(name);
        game.Renderer!.Rig.Zoom = 1f;
        await EndScenario(session);
    }

    private static async Task EndScenario(GameSession session)
    {
        session.AbandonScenario();
        session.FlashlightOn = false;
        await Wait(0.5);
    }

    private async Task Shot(string name)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Size size = window.ClientSize;
            using RenderTargetBitmap bitmap = new(new PixelSize((int)size.Width, (int)size.Height), new Avalonia.Vector(96, 96));
            bitmap.Render(window);
            _index++;
            bitmap.Save(Path.Combine(folder, $"{_index:00}-{name}.png"));
        });
    }
}
