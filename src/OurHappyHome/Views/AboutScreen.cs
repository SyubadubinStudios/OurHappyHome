using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OurHappyHome.Core;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

/// <summary>About the game, with the required credit and scrolling credits.</summary>
public sealed class AboutScreen : UserControl, IKeyHandler
{
    private readonly MainWindow _window;
    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly StackPanel _credits = new() { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer _timer;
    private double _offset;
    private bool _paused;

    public AboutScreen(MainWindow window)
    {
        _window = window;
        Grid root = new();
        root.Children.Add(new Image { Source = Ui.Image("Art/home-exterior.jpg"), Stretch = Stretch.UniformToFill, Opacity = 0.9 });
        root.Children.Add(new Border { Background = Ui.B("#CC1E1410") });

        Grid layout = new() { ColumnDefinitions = new ColumnDefinitions("460,*"), Margin = new Thickness(60, 40) };

        StackPanel about = new() { Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        about.Children.Add(Ui.Emoji("🏡", 56));
        about.Children.Add(new TextBlock { Text = "Our Happy Home", FontSize = Ui.Fs(44), FontWeight = FontWeight.Black, Foreground = Brushes.White });
        about.Children.Add(new TextBlock { Text = Loc.T("Setiap hari adalah cerita. Setiap keluarga adalah petualangan.", "Every day is a story. Every family is an adventure."), FontSize = Ui.Fs(15), FontStyle = FontStyle.Italic, Foreground = Ui.B("#FFE8C7"), TextWrapping = TextWrapping.Wrap });
        Border credit = Ui.Card(Ui.Stack(6, Orientation.Vertical,
            Ui.Text(Loc.T("Dibuat oleh", "Created by"), 13, Ui.Muted),
            Ui.Title("Ariana Mischa Fadhila", 24),
            Ui.Text(Loc.T("dari Syubadubin Studios", "of Syubadubin Studios"), 16, Ui.B("#F28C38"), FontWeight.SemiBold),
            new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 6) },
            Ui.Text("Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios", 13, Ui.Ink, wrap: true)), 18, "#FFF8EC");
        about.Children.Add(credit);
        about.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Game simulasi kehidupan keluarga 3D: bangun rumah, jalani hari bersama Ayah, Ibu, Kak Nara dan Dinda, jelajahi kota, dan pastikan tidak ada yang tertinggal.",
                "A 3D family life simulation: build the home, live each day with Dad, Mom, Nara and Dinda, explore the town, and make sure nobody gets left behind."),
            Foreground = Ui.B("#F6EDE3"),
            FontSize = Ui.Fs(14),
            TextWrapping = TextWrapping.Wrap,
        });
        about.Children.Add(new TextBlock { Text = Loc.T("Versi 1.1 · .NET 10 · Avalonia · ThreeNet", "Version 1.1 · .NET 10 · Avalonia · ThreeNet"), Foreground = Ui.B("#CDBBA8"), FontSize = Ui.Fs(12) });
        about.Children.Add(Ui.Stack(10, Orientation.Horizontal,
            Ui.Button(Loc.T("Kembali", "Back"), Back, icon: "◀"),
            Ui.Ghost(Loc.T("Jeda / lanjut kredit", "Pause / resume credits"), () => _paused = !_paused, "⏯")));
        layout.Children.Add(about);

        Border creditsFrame = new()
        {
            Margin = new Thickness(40, 0, 0, 0),
            CornerRadius = new CornerRadius(24),
            Background = Ui.B("#66000000"),
            Child = _canvas,
        };
        Grid.SetColumn(creditsFrame, 1);
        layout.Children.Add(creditsFrame);
        root.Children.Add(layout);
        Content = root;

        BuildCredits();
        _canvas.Children.Add(_credits);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Scroll());
        _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        window.Audio.SetMood(Core.Simulation.MusicMood.Emotional);
    }

    private void BuildCredits()
    {
        void Heading(string text)
        {
            _credits.Children.Add(new TextBlock { Text = text, FontSize = Ui.Fs(15), FontWeight = FontWeight.Bold, Foreground = Ui.B("#F28C38"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 26, 0, 4), LetterSpacing = 2 });
        }

        void Line(string text, double size = 18, string color = "#FFFFFF", FontWeight weight = FontWeight.SemiBold)
        {
            _credits.Children.Add(new TextBlock { Text = text, FontSize = Ui.Fs(size), FontWeight = weight, Foreground = Ui.B(color), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 });
        }

        _credits.Children.Add(Ui.Emoji("🏡", 64));
        Line("OUR HAPPY HOME", 34, "#FFFFFF", FontWeight.Black);
        Line(Loc.T("Setiap hari adalah cerita.", "Every day is a story."), 16, "#FFE8C7", FontWeight.Normal);

        Heading(Loc.T("DIBUAT OLEH", "CREATED BY"));
        Line("Ariana Mischa Fadhila", 26);
        Line(Loc.T("dari Syubadubin Studios", "of Syubadubin Studios"), 18, "#FFD9A8");

        Heading(Loc.T("SUTRADARA KREATIF & DESAIN GAME", "CREATIVE DIRECTION & GAME DESIGN"));
        Line("Ariana Mischa Fadhila");
        Heading(Loc.T("STUDIO", "STUDIO"));
        Line("Syubadubin Studios");

        Heading(Loc.T("KELUARGA KITA", "OUR FAMILY"));
        Line(Loc.T("Ayah · Ibu · Kak Nara · Raka · Dinda", "Dad · Mom · Nara · Raka · Dinda"));
        Line(Loc.T("dan Brownie si anjing kecil", "and Brownie the puppy"), 15, "#FFE8C7", FontWeight.Normal);

        Heading(Loc.T("TETANGGA & WARGA KOTA", "NEIGHBOURS & TOWNSFOLK"));
        Line(Loc.T("Nenek Sari · Pak Budi · Dimas · Bu Guru Rina · dr. Sinta", "Grandma Sari · Mr. Budi · Dimas · Ms. Rina · Dr. Sinta"));
        Line(Loc.T("serta polisi, pemadam kebakaran, tim SAR, Coco & Mochi", "plus the police, firefighters, rescuers, Coco & Mochi"), 15, "#FFE8C7", FontWeight.Normal);

        Heading(Loc.T("PEMROGRAMAN & SIMULASI", "PROGRAMMING & SIMULATION"));
        Line(Loc.T("Simulasi keluarga hidup, AI rutinitas harian, cuaca, ekonomi, kenangan", "Living family simulation, daily-routine AI, weather, economy, memories"), 15, "#F6EDE3", FontWeight.Normal);
        Line("Syubadubin Studios");

        Heading(Loc.T("SENI, KONSEP & MODEL 3D", "ART, CONCEPTS & 3D MODELS"));
        Line(Loc.T("Concept art & ilustrasi: Nano Banana 2 / Qwen Image via Rodin MCP", "Concept art & illustrations: Nano Banana 2 / Qwen Image via Rodin MCP"), 15, "#F6EDE3", FontWeight.Normal);
        Line(Loc.T("Model 3D keluarga, hewan & perabot: Rodin (Hyper3D)", "Family, pet & furniture 3D models: Rodin (Hyper3D)"), 15, "#F6EDE3", FontWeight.Normal);
        Line(Loc.T("Rigging & 12 animasi karakter: Blender 5.2 (Blender MCP)", "Rigging & 12 character animations: Blender 5.2 (Blender MCP)"), 15, "#F6EDE3", FontWeight.Normal);
        Line(Loc.T("Rig hewan berkaki empat (jalan, duduk, tidur, menggonggong): Blender MCP", "Four-legged pet rigs (walk, sit, sleep, bark): Blender MCP"), 15, "#F6EDE3", FontWeight.Normal);

        Heading(Loc.T("SUARA & MUSIK", "SOUND & MUSIC"));
        Line(Loc.T("Suara karakter: ElevenLabs v3 via Rodin MCP", "Character voices: ElevenLabs v3 via Rodin MCP"), 15, "#F6EDE3", FontWeight.Normal);
        Line(Loc.T("Musik & efek suara prosedural: Syubadubin Synth", "Procedural music & sound effects: Syubadubin Synth"), 15, "#F6EDE3", FontWeight.Normal);

        Heading(Loc.T("TEKNOLOGI", "TECHNOLOGY"));
        Line(".NET 10 · Avalonia UI 12");
        Line(Loc.T("ThreeNet: 3D native untuk .NET (Gravicode Studios, dipimpin Kang Fadhil)", "ThreeNet: native 3D for .NET (Gravicode Studios, led by Kang Fadhil)"), 15, "#F6EDE3", FontWeight.Normal);
        Line("wgpu · Rust · Inter font", 15, "#F6EDE3", FontWeight.Normal);

        Heading(Loc.T("PENGUJI KELUARGA", "FAMILY PLAYTESTERS"));
        Line(Loc.T("Semua keluarga yang suka bermain bersama", "Every family who loves to play together"), 16, "#F6EDE3", FontWeight.Normal);

        Heading(Loc.T("TERIMA KASIH KHUSUS", "SPECIAL THANKS"));
        Line(Loc.T("Ayah & Ibu di seluruh dunia,", "Moms and dads everywhere,"), 16, "#F6EDE3", FontWeight.Normal);
        Line(Loc.T("kakak & adik yang selalu saling menjaga.", "and siblings who always look out for each other."), 16, "#F6EDE3", FontWeight.Normal);

        Heading(Loc.T("JANJI KAMI", "OUR PROMISE"));
        Line(Loc.T("Tanpa kekerasan. Tanpa adegan menakutkan.", "No violence. No scary scenes."), 16, "#FFFFFF", FontWeight.Normal);
        Line(Loc.T("Hanya keluarga, tolong-menolong, dan kenangan.", "Just family, helping each other, and memories."), 16, "#FFFFFF", FontWeight.Normal);
        _credits.Children.Add(new Border { Height = 30 });
        Line(Loc.T("Tidak ada yang boleh tertinggal. 💛", "Nobody gets left behind. 💛"), 22, "#FFD9A8", FontWeight.Bold);
        Line("— Syubadubin Studios —", 16, "#FFFFFF", FontWeight.Normal);
        _credits.Children.Add(new Border { Height = 60 });
    }

    private void Scroll()
    {
        if (_paused || _canvas.Bounds.Height <= 0)
        {
            return;
        }

        _offset += 0.9;
        double height = _credits.Bounds.Height;
        double frame = _canvas.Bounds.Height;
        if (_offset > height + frame)
        {
            _offset = 0;
        }

        _credits.Width = _canvas.Bounds.Width;
        Canvas.SetTop(_credits, frame - _offset);
    }

    private void Back() => _window.Transition(_window.ShowMenu);

    public bool KeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Back();
            return true;
        }

        if (e.Key == Key.Space)
        {
            _paused = !_paused;
            return true;
        }

        return false;
    }

    public void KeyUp(KeyEventArgs e)
    {
    }
}
