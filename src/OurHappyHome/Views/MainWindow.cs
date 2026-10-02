using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using OurHappyHome.Audio;
using OurHappyHome.Core;
using OurHappyHome.Core.Simulation;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

/// <summary>Screens that want keyboard input.</summary>
public interface IKeyHandler
{
    bool KeyDown(KeyEventArgs e);

    void KeyUp(KeyEventArgs e);
}

/// <summary>
/// The single application window. It owns settings and audio, and swaps
/// between the title menu, the game, and the About / credits screen.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly Grid _root = new();
    private readonly ContentControl _screen = new();
    private readonly Border _fade = new() { Background = Brushes.Black, Opacity = 0, IsHitTestVisible = false };
    private readonly DispatcherTimer _menuAudio;
    private GameScreen? _game;

    public MainWindow()
    {
        Settings = GameSettings.Load();
        Loc.Language = Settings.Language;
        Ui.Settings = Settings;
        Audio = new AudioManager(Settings);
        Ui.Sounds = name => Audio.Play(name, gain: 0.5f);

        Title = "Our Happy Home";
        Width = 1440;
        Height = 860;
        MinWidth = 1100;
        MinHeight = 700;
        Background = Ui.B("#2B211C");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://OurHappyHome/Assets/icon.ico")));
        }
        catch (Exception)
        {
            // The icon is cosmetic.
        }

        if (Settings.Fullscreen)
        {
            WindowState = WindowState.FullScreen;
        }

        _root.Children.Add(_screen);
        _root.Children.Add(_fade);
        Content = _root;

        _menuAudio = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) =>
        {
            if (_game is null)
            {
                Audio.Update(0.033f, null, null);
            }
        });
        _menuAudio.Start();

        AddHandler(KeyDownEvent, OnKeyDownTunnel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Deactivated += (_, _) => (_screen.Content as GameScreen)?.ReleaseKeys();
        Closing += (_, _) =>
        {
            _game?.Shutdown();
            Settings.Save();
            Audio.Dispose();
        };

        Opened += (_, _) =>
        {
            ShowMenu();
            if (Program.ScreenshotFolder is { } folder)
            {
                _ = new ScreenshotDirector(this, folder).RunAsync();
            }
        };
    }

    public GameSettings Settings { get; }

    public AudioManager Audio { get; }

    public GameScreen? Game => _game;

    public Control? CurrentScreen => _screen.Content as Control;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            Settings.Fullscreen = WindowState == WindowState.FullScreen;
            e.Handled = true;
            return;
        }

        if (_screen.Content is IKeyHandler handler && handler.KeyDown(e))
        {
            e.Handled = true;
        }
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        if (_screen.Content is IKeyHandler handler)
        {
            handler.KeyUp(e);
        }
    }

    public async void Transition(Action swap)
    {
        _fade.IsHitTestVisible = true;
        for (int i = 0; i <= 8; i++)
        {
            _fade.Opacity = i / 8.0;
            await Task.Delay(16);
        }

        swap();
        await Task.Delay(30);
        for (int i = 8; i >= 0; i--)
        {
            _fade.Opacity = i / 8.0;
            await Task.Delay(20);
        }

        _fade.IsHitTestVisible = false;
    }

    public void ShowMenu()
    {
        _game?.Shutdown();
        _game = null;
        _screen.Content = new MenuScreen(this);
        Audio.SetMood(MusicMood.Cozy);
        Audio.SetAmbience("birds", 0.35f);
        Audio.SetAmbience("rain", 0f);
        Audio.SetAmbience("wind", 0f);
        Audio.SetAmbience("crickets", 0f);
        Audio.SetAmbience("fire", 0f);
        Audio.SetAmbience("ocean", 0f);
        Audio.SetAmbience("town", 0f);
    }

    public void ShowAbout() => Transition(() => _screen.Content = new AboutScreen(this));

    public void StartGame(GameSession session) => Transition(() =>
    {
        _game?.Shutdown();
        _game = new GameScreen(this, session);
        _screen.Content = _game;
        Audio.SetAmbience("birds", 0f);
    });

    /// <summary>Applies changed settings everywhere (language, text size, colours).</summary>
    public void ApplySettings()
    {
        Loc.Language = Settings.Language;
        Ui.Settings = Settings;
        Settings.Save();
        if (_screen.Content is MenuScreen)
        {
            _screen.Content = new MenuScreen(this);
        }

        _game?.ApplySettings();
    }
}
