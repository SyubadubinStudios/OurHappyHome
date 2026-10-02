using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OurHappyHome.Audio;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;
using OurHappyHome.Rendering;
using OurHappyHome.UI;
using ThreeNet;
using ThreeNet.Avalonia;
using Key = Avalonia.Input.Key;
using Point = Avalonia.Point;
using Vector = Avalonia.Vector;
using MouseButton = Avalonia.Input.MouseButton;

namespace OurHappyHome.Views;

/// <summary>
/// The in-game screen: the ThreeNet 3D view, the HUD and every panel. It runs
/// the frame loop (input → simulation → events → renderer → audio → HUD).
/// </summary>
public sealed partial class GameScreen : UserControl, IKeyHandler
{
    private readonly MainWindow _window;
    private readonly ThreeNetView _view = new() { Focusable = true };
    private readonly Grid _root = new();
    private readonly Grid _hud = new();
    private readonly Panel _panels = new();
    private readonly Border _flash = new() { Background = Brushes.White, Opacity = 0, IsHitTestVisible = false };
    private readonly Border _loading;
    private readonly HashSet<Key> _keys = [];
    private GameRenderer? _renderer;
    private Point? _rightDragFrom;
    private Point? _leftDown;
    private bool _dragged;
    private float _hudTimer;
    private float _promptTimer;
    private float _autosaveCheck;
    private float _fpsTimer;
    private int _frames;
    private float _moodHold;
    private MusicMood? _heldMood;
    private readonly List<(int MemoryId, int FramesToWait)> _pendingPhotos = [];

    public GameScreen(MainWindow window, GameSession session)
    {
        _window = window;
        Session = session;
        Audio = window.Audio;

        _view.RendererOptions = QualityOptions(window.Settings.Quality);
        _view.RenderScale = window.Settings.Quality switch { GraphicsQuality.Low => 0.6, GraphicsQuality.Medium => 0.75, _ => 0.9 };
        _view.MaxFramesPerSecond = 60;
        _view.IsRendering = false;
        _view.EnableInteraction = false;
        _view.Frame += OnFrame;
        _view.RenderFailed += (_, message) => Notify(Loc.T($"Grafis gagal: {message}", $"Graphics failed: {message}"), "⚠", NoticeKind.Danger);
        _view.PointerPressed += OnPointerPressed;
        _view.PointerMoved += OnPointerMoved;
        _view.PointerReleased += OnPointerReleased;
        _view.PointerWheelChanged += (_, e) =>
        {
            if (_renderer is { } r)
            {
                r.Rig.Zoom = Math.Clamp(r.Rig.Zoom * (e.Delta.Y > 0 ? 0.9f : 1.1f), 0.35f, 2.6f);
            }
        };

        _root.Children.Add(_view);
        _root.Children.Add(_flash);
        _root.Children.Add(_hud);
        _root.Children.Add(_panels);
        _loading = BuildLoading();
        _root.Children.Add(_loading);
        Content = _root;

        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(Initialize, DispatcherPriority.Background);
    }

    public GameSession Session { get; private set; }

    public AudioManager Audio { get; }

    public GameRenderer? Renderer => _renderer;

    public ThreeNetView View => _view;

    private static RendererOptions QualityOptions(GraphicsQuality quality) => RendererOptions.Default with
    {
        BgraOutput = true,
        VSync = false,
        MsaaSamples = quality == GraphicsQuality.Low ? 1 : quality == GraphicsQuality.Medium ? 2 : 4,
        ToneMapping = ToneMapping.Aces,
        Exposure = 1.05f,
        Bloom = quality != GraphicsQuality.Low,
        BloomIntensity = 0.3f,
        BloomThreshold = 1.1f,
        Shadows = true,
        ShadowMapSize = quality == GraphicsQuality.High ? 2048 : 1024,
        ShadowCascades = quality == GraphicsQuality.High ? 3 : 2,
        ShadowDistance = 45f,
        ShadowSoftness = 1,
        Ssao = quality == GraphicsQuality.High,
        SsaoRadius = 0.45f,
        SsaoIntensity = 1.3f,
        SsaoDirectStrength = 0.25f,
    };

    private Border BuildLoading()
    {
        StackPanel panel = Ui.Stack(14, Avalonia.Layout.Orientation.Vertical,
            Ui.Emoji("🏡", 64),
            Ui.Title(Loc.T("Menyiapkan rumah keluarga…", "Getting the family home ready…"), 24),
            Ui.Text(Loc.T("Membangun kota, memanggil keluarga, menggubah musik", "Building the town, calling the family, composing music"), 14, Ui.Muted),
            new ProgressBar { IsIndeterminate = true, Width = 280 });
        panel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        panel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        foreach (Control c in panel.Children)
        {
            c.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        }

        return new Border
        {
            Background = new ImageBrush(Ui.Image("Art/home-interior.jpg")) { Stretch = Stretch.UniformToFill, Opacity = 0.35 },
            Child = new Border { Background = Ui.B("#E6FFF8EC"), Child = panel },
        };
    }

    private void Initialize()
    {
        ThreeNetRuntime.Initialize();
        _renderer = new GameRenderer(Session) { ReducedIntensity = _window.Settings.ReducedIntensity };
        _view.Scene = _renderer.Scene;
        _view.Camera = _renderer.CameraNode;
        _view.IsRendering = true;
        BuildHud();
        _root.Children.Remove(_loading);
        _view.Focus();
        Audio.SetMood(Session.Clock.IsNight ? MusicMood.Night : MusicMood.Cozy);
        Notify(Loc.T($"Selamat datang di rumah, {Session.State.PlayerName}!", $"Welcome home, {Session.State.PlayerName}!"), "🏡", NoticeKind.Good);
        if (Session.State.Chapter <= 5 && Session.Clock.DayIndex == 0 && Session.Hour < 6.5f)
        {
            ShowChapterCard(Session.State.Chapter, false);
        }
    }

    public void Shutdown()
    {
        _view.IsRendering = false;
        _view.Scene = null;
        _renderer?.Dispose();
        _renderer = null;
    }

    public void ReleaseKeys() => _keys.Clear();

    public void ApplySettings()
    {
        if (_renderer is null)
        {
            return;
        }

        _renderer.ReducedIntensity = _window.Settings.ReducedIntensity;
        _view.RendererOptions = QualityOptions(_window.Settings.Quality);
        _view.RenderScale = _window.Settings.Quality switch { GraphicsQuality.Low => 0.6, GraphicsQuality.Medium => 0.75, _ => 0.9 };
        BuildHud();
    }

    // ----------------------------------------------------------------- frame

    private void OnFrame(object? sender, FrameEventArgs e)
    {
        if (_renderer is null)
        {
            return;
        }

        float dt = MathF.Min(e.DeltaSeconds, 0.05f);
        bool modal = PanelOpen && !_buildMode;
        Session.Paused = modal || _pausedByPlayer || _buildMode;
        ReadMovement();
        Session.Tick(dt);
        if (_renderer.Session != Session)
        {
            _renderer.Session = Session;
        }

        DrainEvents();
        UpdateBuildMode(dt);
        _renderer.Update(dt);
        _flash.Opacity = _renderer.Flash * 0.75;
        UpdateAudio(dt);
        CapturePhotos();

        _hudTimer -= dt;
        if (_hudTimer <= 0f)
        {
            _hudTimer = 0.12f;
            UpdateHud();
        }

        _promptTimer -= dt;
        if (_promptTimer <= 0f)
        {
            _promptTimer = 0.18f;
            UpdatePrompt();
        }

        UpdateToasts(dt);

        _autosaveCheck -= dt;
        if (_autosaveCheck <= 0f)
        {
            _autosaveCheck = 1f;
            if (Session.WantsAutosave())
            {
                Save(autosave: true);
            }
        }

        _frames++;
        _fpsTimer += dt;
        if (_fpsTimer >= 1f)
        {
            _fps = _frames / _fpsTimer;
            _frames = 0;
            _fpsTimer = 0f;
        }
    }

    private float _fps;

    private void ReadMovement()
    {
        if (_renderer is null || PanelOpen || _buildMode)
        {
            Session.PlayerMove = Vector2.Zero;
            return;
        }

        float forward = Axis(Key.W, Key.S) + Axis(Key.Up, Key.Down);
        float right = Axis(Key.D, Key.A) + Axis(Key.Right, Key.Left);
        Vector2 move = (_renderer.Rig.Forward * forward) + (_renderer.Rig.Right * right);
        Session.PlayerMove = move.LengthSquared() > 1f ? Vector2.Normalize(move) : move;
        Session.PlayerRun = _keys.Contains(Key.LeftShift) || _keys.Contains(Key.RightShift);

        float rotate = Axis(Key.Z, Key.C);
        if (rotate != 0f)
        {
            _renderer.Rig.Yaw += rotate * 1.6f * 0.016f;
        }
    }

    private float Axis(Key positive, Key negative) => (_keys.Contains(positive) ? 1f : 0f) - (_keys.Contains(negative) ? 1f : 0f);

    // ---------------------------------------------------------------- events

    private void DrainEvents()
    {
        int guard = 0;
        while (Session.Bus.TryDequeue(out GameEvent e) && guard++ < 200)
        {
            _renderer?.HandleEvent(e);
            switch (e)
            {
                case NoticeEvent notice:
                    Notify(notice.Text, notice.Icon, notice.Kind);
                    break;
                case SpeechEvent speech:
                    ShowSubtitle(speech.SpeakerName, speech.Text, speech.Speaker);
                    Audio.Speak(speech.Speaker, speech.VoiceKey, speech.Text.Length);
                    break;
                case SoundEvent sound:
                    Audio.Play(sound.Sound, sound.Position, sound.Gain);
                    break;
                case MusicEvent music:
                    if (music.Mood is MusicMood.Celebration or MusicMood.Emotional)
                    {
                        _heldMood = music.Mood;
                        _moodHold = 24f;
                    }

                    break;
                case MemoryEvent memory when memory.TakePhoto:
                    _pendingPhotos.Add((memory.Memory.Id, 2));
                    ShowPolaroid(memory.Memory);
                    break;
                case SkillUpEvent:
                    Audio.Play("success", gain: 0.5f);
                    break;
                case ScenarioEvent scenario:
                    OnScenario(scenario);
                    break;
                case ChapterEvent chapter:
                    ShowChapterCard(chapter.Chapter, chapter.Completed);
                    break;
                case MiniGameRequest request:
                    OpenMiniGame(request);
                    break;
                case OpenPanelEvent panel:
                    OpenPanel(panel.Panel, panel.Argument);
                    break;
            }
        }
    }

    private void OnScenario(ScenarioEvent e)
    {
        switch (e.Phase)
        {
            case "start":
                Audio.Play("alert", gain: 0.6f);
                break;
            case "success":
                Audio.Play("success");
                Notify(Loc.T($"Berhasil: {e.Title}! Semua aman.", $"Resolved: {e.Title}! Everyone is safe."), "💚", NoticeKind.Good);
                _heldMood = MusicMood.Emotional;
                _moodHold = 20f;
                break;
            case "failed":
                Audio.Play("fail");
                ShowFailure();
                break;
        }
    }

    // ----------------------------------------------------------------- audio

    private void UpdateAudio(float dt)
    {
        if (_renderer is null)
        {
            return;
        }

        WeatherState weather = Session.State.Weather;
        FamilyMember player = Session.Controlled;
        bool indoors = Session.IsIndoors(player.Position);
        bool night = Session.Clock.IsNight;
        PlaceId? place = Session.Map.PlaceAt(player.Position);
        float outside = indoors ? 0.35f : 1f;

        Audio.SetAmbience("rain", weather.IsRaining ? weather.Intensity * outside : 0f);
        Audio.SetAmbience("wind", weather.Current switch { WeatherKind.Windy => 0.7f, WeatherKind.SevereStorm => 0.9f, WeatherKind.Thunderstorm => 0.35f, _ => 0.05f } * outside);
        Audio.SetAmbience("birds", !night && !weather.IsRaining ? 0.45f * outside : 0f);
        Audio.SetAmbience("crickets", night && !weather.IsRaining ? 0.5f * outside : 0f);
        bool fireNear = Session.State.House.AnyHazard(HazardKind.Fire) || place == PlaceId.Camping;
        Audio.SetAmbience("fire", fireNear ? 0.6f : 0f);
        Audio.SetAmbience("ocean", place == PlaceId.Beach || player.Position.Y > 215f ? 0.8f : 0f);
        Audio.SetAmbience("town", place is PlaceId.Supermarket or PlaceId.Mall or PlaceId.Restaurant or PlaceId.Arcade or PlaceId.School or PlaceId.ThemePark ? 0.35f : 0f);

        MusicMood mood;
        if (Session.ScenarioFailed)
        {
            mood = MusicMood.Emotional;
        }
        else if (Session.Scenario is { Dangerous: true })
        {
            mood = MusicMood.Tense;
        }
        else if (_moodHold > 0f && _heldMood is { } held)
        {
            _moodHold -= dt;
            mood = held;
        }
        else if (night)
        {
            mood = MusicMood.Night;
        }
        else if (!Rooms.Lot.Contains(player.Position))
        {
            mood = MusicMood.Explore;
        }
        else
        {
            mood = MusicMood.Cozy;
        }

        Audio.SetMood(mood);
        Audio.Update(dt, _renderer.Scene, _renderer.CameraNode);
    }

    // ---------------------------------------------------------------- photos

    private void CapturePhotos()
    {
        for (int i = _pendingPhotos.Count - 1; i >= 0; i--)
        {
            (int id, int wait) = _pendingPhotos[i];
            if (wait > 0)
            {
                _pendingPhotos[i] = (id, wait - 1);
                continue;
            }

            _pendingPhotos.RemoveAt(i);
            try
            {
                using WriteableBitmap? frame = _view.CaptureFrame();
                if (frame is null)
                {
                    continue;
                }

                string folder = Path.Combine(SaveSystem.PhotoFolder, Sanitize(Session.State.SaveName));
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"memory-{id}.png");
                frame.Save(file);
                Session.AttachPhoto(id, file);
            }
            catch (Exception)
            {
                // Photos are a bonus; never interrupt play for them.
            }
        }
    }

    private static string Sanitize(string name)
    {
        string clean = new([.. (string.IsNullOrWhiteSpace(name) ? "family" : name).Select(c => char.IsLetterOrDigit(c) ? c : '-')]);
        return clean.Trim('-').ToLowerInvariant();
    }

    public void Save(bool autosave = false)
    {
        try
        {
            string slot = (autosave ? "autosave-" : "") + Sanitize(Session.State.SaveName);
            SaveSystem.Save(Session.State, slot);
            Notify(autosave ? Loc.T("Tersimpan otomatis.", "Autosaved.") : Loc.T("Permainan tersimpan!", "Game saved!"), "💾", NoticeKind.Good);
        }
        catch (Exception ex)
        {
            Notify(Loc.T($"Gagal menyimpan: {ex.Message}", $"Save failed: {ex.Message}"), "⚠", NoticeKind.Danger);
        }
    }

    // ----------------------------------------------------------------- mouse

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _view.Focus();
        PointerPointProperties props = e.GetCurrentPoint(_view).Properties;
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            _rightDragFrom = e.GetPosition(_view);
        }
        else if (props.IsLeftButtonPressed)
        {
            _leftDown = e.GetPosition(_view);
            _dragged = false;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        Point p = e.GetPosition(_view);
        if (_rightDragFrom is { } from && _renderer is not null)
        {
            Vector delta = p - from;
            _rightDragFrom = p;
            _renderer.Rig.Yaw -= (float)delta.X * 0.006f;
            _renderer.Rig.PitchOffset = Math.Clamp(_renderer.Rig.PitchOffset + ((float)delta.Y * 0.004f), -0.4f, 0.5f);
        }

        if (_leftDown is { } down && (Math.Abs(p.X - down.X) + Math.Abs(p.Y - down.Y)) > 6)
        {
            _dragged = true;
        }

        if (_buildMode)
        {
            BuildPointerMoved(p);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Point p = e.GetPosition(_view);
        if (e.InitialPressMouseButton is MouseButton.Right or MouseButton.Middle)
        {
            if (_buildMode)
            {
                BuildCancel();
            }

            _rightDragFrom = null;
            return;
        }

        if (_leftDown is null || _dragged || _renderer is null)
        {
            _leftDown = null;
            return;
        }

        _leftDown = null;
        if (_buildMode)
        {
            BuildClick(p);
            return;
        }

        // Click to walk (and to interact when clicking near something).
        if (GroundAt(p) is { } ground && !PanelOpen)
        {
            FamilyMember me = Session.Controlled;
            if (Vector2.Distance(ground, me.Position) < 60f)
            {
                Session.StartTask(me, ActivityId.Idle, null, -1, target: ground, run: _keys.Contains(Key.LeftShift), fromPlayer: true, minutes: 1);
                _renderer.Effects.Burst(EffectKind.Sparkles, new Vector3(ground.X, 0.2f, ground.Y), 0.4f);
            }
        }
    }

    public Vector2? GroundAt(Point p)
    {
        if (_renderer is null || _view.Bounds.Width <= 0)
        {
            return null;
        }

        float ndcX = (float)((p.X / _view.Bounds.Width * 2) - 1);
        float ndcY = (float)(1 - (p.Y / _view.Bounds.Height * 2));
        float aspect = (float)(_view.Bounds.Width / Math.Max(1, _view.Bounds.Height));
        return _renderer.GroundAt(ndcX, ndcY, aspect);
    }

    // -------------------------------------------------------------- keyboard

    public bool KeyDown(KeyEventArgs e)
    {
        if (_miniGame is { } game && game.KeyDown(e))
        {
            return true;
        }

        if (e.Key == Key.Escape)
        {
            if (_buildMode)
            {
                ExitBuildMode();
            }
            else if (PanelOpen)
            {
                ClosePanel();
            }
            else
            {
                ShowPause();
            }

            return true;
        }

        if (_buildMode)
        {
            _keys.Add(e.Key);
            switch (e.Key)
            {
                case Key.R:
                    BuildRotate();
                    break;
                case Key.B:
                    ExitBuildMode();
                    break;
            }

            return true;
        }

        if (PanelOpen)
        {
            return e.Source is TextBox ? false : e.Key is not (Key.Tab);
        }

        if (e.Source is TextBox)
        {
            return false;
        }

        _keys.Add(e.Key);
        switch (e.Key)
        {
            case Key.E or Key.Enter:
                ExecuteOption(0);
                return true;
            case >= Key.D1 and <= Key.D9:
                ExecuteOption(e.Key - Key.D1);
                return true;
            case Key.Tab:
                Session.SwitchControl();
                return true;
            case Key.F:
                Session.FlashlightOn = !Session.FlashlightOn;
                return true;
            case Key.P:
                Session.TakeFamilyPhoto();
                return true;
            case Key.J:
                OpenPanel("journal");
                return true;
            case Key.K:
                OpenPanel("family");
                return true;
            case Key.M:
                OpenPanel("map");
                return true;
            case Key.B:
                if (_buildMode)
                {
                    ExitBuildMode();
                }
                else
                {
                    OpenPanel("build");
                }

                return true;
            case Key.L:
                OpenPanel("album");
                return true;
            case Key.I:
                OpenPanel("inventory");
                return true;
            case Key.H or Key.F1:
                OpenPanel("help");
                return true;
            case Key.OemComma:
                Session.SetSpeed(Session.Clock.Speed / 2f);
                return true;
            case Key.OemPeriod:
                Session.SetSpeed(Session.Clock.Speed * 2f);
                return true;
            case Key.Space:
                _pausedByPlayer = !_pausedByPlayer;
                return true;
        }

        return e.Key is Key.Up or Key.Down or Key.Left or Key.Right;
    }

    public void KeyUp(KeyEventArgs e)
    {
        _keys.Remove(e.Key);
        _miniGame?.KeyUp(e);
    }

    private bool _pausedByPlayer;
}
