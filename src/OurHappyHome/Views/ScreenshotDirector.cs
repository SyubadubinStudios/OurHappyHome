using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
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

        game.OpenPanel("album");
        await Wait(1.5);
        await Shot("family-album");
        game.ClosePanel();

        game.OpenPanel("pause");
        await Wait(1);
        await Shot("pause-menu");
        game.ClosePanel();

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
