using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OurHappyHome.Core;
using OurHappyHome.Core.Simulation;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

/// <summary>Title screen: key art, logo, main menu, new game, load and settings.</summary>
public sealed class MenuScreen : UserControl, IKeyHandler
{
    private readonly MainWindow _window;
    private readonly Grid _layers = new();
    private readonly Image _art;
    private readonly Canvas _petals = new() { IsHitTestVisible = false };
    private readonly Panel _overlay = new();
    private readonly DispatcherTimer _timer;
    private readonly List<(TextBlock Glyph, double X, double Y, double Speed, double Phase)> _floating = [];
    private double _time;

    public MenuScreen(MainWindow window)
    {
        _window = window;
        _art = new Image { Source = Ui.Image("Art/key-art.jpg"), Stretch = Stretch.UniformToFill, RenderTransformOrigin = RelativePoint.Center };
        _layers.Children.Add(_art);
        _layers.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#E6301E14"), 0), new GradientStop(Color.Parse("#66301E14"), 0.45), new GradientStop(Color.Parse("#00000000"), 0.75) },
            },
        });
        _layers.Children.Add(_petals);
        _layers.Children.Add(BuildMenu());
        _layers.Children.Add(_overlay);
        Content = _layers;

        Random r = new(4);
        string[] glyphs = ["🌸", "💛", "🍃", "✨", "🌼", "💗"];
        for (int i = 0; i < 16; i++)
        {
            TextBlock glyph = Ui.Emoji(glyphs[i % glyphs.Length], 14 + r.Next(0, 16));
            glyph.Opacity = 0.75;
            _petals.Children.Add(glyph);
            _floating.Add((glyph, r.NextDouble(), r.NextDouble(), 0.02 + (r.NextDouble() * 0.04), r.NextDouble() * 6));
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => Animate());
        _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Animate()
    {
        _time += 0.033;
        double zoom = 1.04 + (0.04 * Math.Sin(_time * 0.12));
        _art.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(zoom, zoom), new TranslateTransform(Math.Sin(_time * 0.07) * 18, Math.Cos(_time * 0.05) * 8) },
        };

        double w = Bounds.Width;
        double h = Bounds.Height;
        for (int i = 0; i < _floating.Count; i++)
        {
            (TextBlock glyph, double x, double y, double speed, double phase) = _floating[i];
            y -= speed * 0.033;
            if (y < -0.05)
            {
                y = 1.05;
            }

            _floating[i] = (glyph, x, y, speed, phase);
            Canvas.SetLeft(glyph, (x * w) + (Math.Sin(_time + phase) * 24));
            Canvas.SetTop(glyph, y * h);
        }
    }

    private Control BuildMenu()
    {
        StackPanel menu = new() { Spacing = 12, Width = 380, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(70, 0, 0, 0) };
        TextBlock logo = new()
        {
            Text = "Our Happy Home",
            FontSize = Ui.Fs(58),
            FontWeight = FontWeight.Black,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Effect = new DropShadowEffect { BlurRadius = 18, Color = Color.Parse("#AA000000"), OffsetX = 0, OffsetY = 4 },
        };
        menu.Children.Add(Ui.Emoji("🏡", 64));
        menu.Children.Add(logo);
        menu.Children.Add(new TextBlock
        {
            Text = Loc.T("Setiap hari adalah cerita. Setiap keluarga adalah petualangan.", "Every day is a story. Every family is an adventure."),
            FontSize = Ui.Fs(16),
            Foreground = Ui.B("#FFE8C7"),
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18),
        });

        IReadOnlyList<SaveInfo> saves = SaveSystem.List();
        if (saves.Count > 0)
        {
            SaveInfo latest = saves[0];
            menu.Children.Add(Wide(Ui.Button(Loc.T($"Lanjutkan · Hari {latest.Day}", $"Continue · Day {latest.Day}"), () => Load(latest.Slot), "#F28C38", icon: "▶", size: 17)));
        }

        menu.Children.Add(Wide(Ui.Button(Loc.T("Permainan Baru", "New Game"), ShowNewGame, saves.Count > 0 ? "#FFB347" : "#F28C38", icon: "✨", size: 17)));
        menu.Children.Add(Wide(Ui.Button(Loc.T("Muat Permainan", "Load Game"), ShowLoad, "#FFF1DE", "#5A4636", icon: "📂", size: 16, enabled: saves.Count > 0)));
        menu.Children.Add(Wide(Ui.Button(Loc.T("Pengaturan", "Settings"), ShowSettings, "#FFF1DE", "#5A4636", icon: "⚙", size: 16)));
        menu.Children.Add(Wide(Ui.Button(Loc.T("Tentang & Kredit", "About & Credits"), _window.ShowAbout, "#FFF1DE", "#5A4636", icon: "ℹ", size: 16)));
        menu.Children.Add(Wide(Ui.Button(Loc.T("Keluar", "Quit"), () => _window.Close(), "#FFF1DE", "#5A4636", icon: "🚪", size: 16)));

        Grid root = new();
        root.Children.Add(menu);
        StackPanel corner = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(20) };
        corner.Children.Add(Ui.Button("ID", () => SetLanguage(Language.Indonesian), Loc.Language == Language.Indonesian ? "#F28C38" : "#FFF1DE", Loc.Language == Language.Indonesian ? "#FFFFFF" : "#5A4636", 13));
        corner.Children.Add(Ui.Button("EN", () => SetLanguage(Language.English), Loc.Language == Language.English ? "#F28C38" : "#FFF1DE", Loc.Language == Language.English ? "#FFFFFF" : "#5A4636", 13));
        root.Children.Add(corner);
        root.Children.Add(new TextBlock
        {
            Text = "Dibuat oleh Ariana Mischa Fadhila · Syubadubin Studios · v1.0",
            Foreground = Ui.B("#FFE8C7"),
            FontSize = Ui.Fs(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(70, 0, 0, 22),
        });
        return root;
    }

    private static Control Wide(Button b)
    {
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.Padding = new Thickness(18, 12);
        return b;
    }

    private void SetLanguage(Language language)
    {
        _window.Settings.Language = language;
        _window.ApplySettings();
    }

    private void Close() => _overlay.Children.Clear();

    private void ShowOverlay(Control modal)
    {
        _overlay.Children.Clear();
        _overlay.Children.Add(new Border { Background = Ui.Dim });
        _overlay.Children.Add(modal);
    }

    // ------------------------------------------------------------ new game

    private void ShowNewGame()
    {
        GameMode mode = GameMode.Normal;
        TextBox name = new() { Text = "Raka", FontSize = Ui.Fs(16), Width = 240, MaxLength = 16, PlaceholderText = Loc.T("Nama anak laki-laki", "The boy's name") };
        CheckBox tutorials = new() { IsChecked = _window.Settings.Tutorials, Content = Ui.Text(Loc.T("Tampilkan tips & tutorial", "Show tips & tutorials"), 14) };
        StackPanel modes = new() { Orientation = Orientation.Horizontal, Spacing = 12 };

        void Refresh()
        {
            modes.Children.Clear();
            foreach ((GameMode m, string icon, string titleId, string titleEn, string descId, string descEn) in new[]
            {
                (GameMode.Cozy, "🌸", "Mode Santai", "Cozy Mode", "Fokus membangun rumah dan kehidupan keluarga. Kejadian berbahaya sangat jarang dan waktu penyelamatan lebih panjang.", "Focus on home building and family life. Dangerous events are very rare and rescues get more time."),
                (GameMode.Normal, "🏡", "Mode Normal", "Normal Mode", "Keseimbangan antara kehidupan sehari-hari dan petualangan tak terduga.", "A balance of everyday life and unexpected adventures."),
                (GameMode.Adventure, "⛈", "Mode Petualangan", "Adventure Mode", "Lebih banyak badai, kejadian darurat dan tantangan. Tetap ramah keluarga.", "More storms, emergencies and challenges. Still family friendly."),
            })
            {
                bool selected = m == mode;
                StackPanel inner = Ui.Stack(6, Orientation.Vertical, Ui.Emoji(icon, 34), Ui.Title(Loc.T(titleId, titleEn), 17), Ui.Text(Loc.T(descId, descEn), 12.5, Ui.Muted, wrap: true));
                Border card = Ui.Card(inner, 14, selected ? "#FFE3C2" : "#FFFDF7");
                card.Width = 220;
                card.BorderBrush = selected ? Ui.Accent : Ui.Line;
                card.BorderThickness = new Thickness(selected ? 3 : 1);
                card.Cursor = new Cursor(StandardCursorType.Hand);
                card.PointerPressed += (_, _) =>
                {
                    mode = m;
                    Refresh();
                };
                modes.Children.Add(card);
            }
        }

        Refresh();
        StackPanel body = new() { Spacing = 16 };
        body.Children.Add(Ui.Text(Loc.T("Pilih gaya bermain:", "Choose how you want to play:"), 15, weight: FontWeight.SemiBold));
        body.Children.Add(modes);
        body.Children.Add(Ui.Stack(10, Orientation.Horizontal, Ui.Portrait(Core.Family.MemberId.Player, 52), Ui.Stack(4, Orientation.Vertical, Ui.Text(Loc.T("Nama si anak laki-laki (10 tahun):", "Name of the 10-year-old boy:"), 14), name)));
        body.Children.Add(tutorials);
        body.Children.Add(Ui.Button(Loc.T("Mulai Cerita Keluarga Kita!", "Start Our Family Story!"), () =>
        {
            _window.Settings.Tutorials = tutorials.IsChecked == true;
            _window.Settings.Save();
            GameSession session = GameSession.NewGame(mode, name.Text ?? "Raka", _window.Settings);
            session.State.SaveName = $"{session.State.PlayerName} · {Loc.T(mode switch { GameMode.Cozy => "Santai", GameMode.Adventure => "Petualangan", _ => "Normal" }, mode.ToString())}";
            _window.StartGame(session);
        }, icon: "🏡", size: 17));
        ShowOverlay(Ui.Modal("✨", Loc.T("Permainan Baru", "New Game"), body, Close, 760, 620));
    }

    // ---------------------------------------------------------------- load

    private void ShowLoad()
    {
        StackPanel list = new() { Spacing = 10 };
        foreach (SaveInfo save in SaveSystem.List())
        {
            Grid row = Ui.Row(
                (Ui.Stack(4, Orientation.Vertical,
                    Ui.Title(string.IsNullOrWhiteSpace(save.Name) ? save.Slot : save.Name, 16),
                    Ui.Text(Loc.T($"Hari {save.Day} · {save.House} · Bab {save.Chapter} · {Loc.Money(save.Money)}", $"Day {save.Day} · {save.House} · Chapter {save.Chapter} · {Loc.Money(save.Money)}"), 13, Ui.Muted),
                    Ui.Text(save.SavedAt.ToString("dd MMM yyyy HH:mm"), 12, Ui.Muted)), GridLength.Star),
                (Ui.Button(Loc.T("Muat", "Load"), () => Load(save.Slot), icon: "▶"), GridLength.Auto),
                (Ui.Ghost("🗑", () =>
                {
                    SaveSystem.Delete(save.Slot);
                    ShowLoad();
                }), GridLength.Auto));
            row.ColumnSpacing = 10;
            list.Children.Add(Ui.Card(row, 12));
        }

        if (list.Children.Count == 0)
        {
            list.Children.Add(Ui.Text(Loc.T("Belum ada simpanan.", "No saves yet."), 15, Ui.Muted));
        }

        ShowOverlay(Ui.Modal("📂", Loc.T("Muat Permainan", "Load Game"), list, Close, 640, 560));
    }

    private void Load(string slot)
    {
        try
        {
            GameState state = SaveSystem.Load(slot);
            _window.StartGame(new GameSession(state, _window.Settings));
        }
        catch (Exception ex)
        {
            ShowOverlay(Ui.Modal("⚠", Loc.T("Gagal memuat", "Could not load"), Ui.Text(ex.Message, 14, wrap: true), Close, 520, 300));
        }
    }

    // ------------------------------------------------------------ settings

    private void ShowSettings() => ShowOverlay(SettingsPanel.Build(_window, Close));

    public bool KeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _overlay.Children.Count > 0)
        {
            Close();
            return true;
        }

        return false;
    }

    public void KeyUp(KeyEventArgs e)
    {
    }
}
