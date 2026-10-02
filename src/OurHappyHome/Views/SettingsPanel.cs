using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OurHappyHome.Core;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

/// <summary>Settings including every accessibility option from design section 27.</summary>
public static class SettingsPanel
{
    public static Control Build(MainWindow window, Action onClose)
    {
        GameSettings s = window.Settings;
        StackPanel body = new() { Spacing = 14 };

        void Section(string icon, string title)
        {
            body.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(icon, 18), Ui.Title(title, 17)));
        }

        Control Choice<T>(string label, T current, (T Value, string Text)[] options, Action<T> set)
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach ((T value, string text) in options)
            {
                bool selected = EqualityComparer<T>.Default.Equals(value, current);
                row.Children.Add(Ui.Button(text, () =>
                {
                    set(value);
                    Rebuild();
                }, selected ? "#F28C38" : "#FFF1DE", selected ? "#FFFFFF" : "#5A4636", 13));
            }

            Grid grid = Ui.Row((Ui.Text(label, 14), new GridLength(220)), (row, GridLength.Star));
            return grid;
        }

        Control Toggle(string label, string hint, bool value, Action<bool> set)
        {
            CheckBox box = new() { IsChecked = value, Content = Ui.Stack(2, Orientation.Vertical, Ui.Text(label, 14, weight: FontWeight.SemiBold), Ui.Text(hint, 12, Ui.Muted, wrap: true)) };
            box.IsCheckedChanged += (_, _) =>
            {
                set(box.IsChecked == true);
                s.Save();
            };
            return box;
        }

        Control Volume(string label, string icon, float value, Action<float> set)
        {
            Slider slider = new() { Minimum = 0, Maximum = 1, Value = value, Width = 260, VerticalAlignment = VerticalAlignment.Center };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty)
                {
                    set((float)slider.Value);
                }
            };
            return Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(icon, 15), Ui.Text(label, 14)), new GridLength(220)), (slider, GridLength.Star));
        }

        void Rebuild()
        {
            s.Save();
            body.Children.Clear();
            Fill();
        }

        void Fill()
        {
            Section("🌐", Loc.T("Bahasa", "Language"));
            body.Children.Add(Choice(Loc.T("Bahasa permainan", "Game language"), s.Language,
                [(Language.Indonesian, "Bahasa Indonesia"), (Language.English, "English")], v => s.Language = v));

            Section("🔊", Loc.T("Suara", "Sound"));
            body.Children.Add(Volume(Loc.T("Volume utama", "Master volume"), "🔊", s.MasterVolume, v => s.MasterVolume = v));
            body.Children.Add(Volume(Loc.T("Musik", "Music"), "🎵", s.MusicVolume, v => s.MusicVolume = v));
            body.Children.Add(Volume(Loc.T("Efek suara", "Sound effects"), "🔔", s.SfxVolume, v => s.SfxVolume = v));
            body.Children.Add(Volume(Loc.T("Suara karakter", "Voices"), "🗣", s.VoiceVolume, v => s.VoiceVolume = v));

            Section("♿", Loc.T("Aksesibilitas", "Accessibility"));
            body.Children.Add(Choice(Loc.T("Ukuran teks", "Text size"), s.TextScale,
                [(0.9f, Loc.T("Kecil", "Small")), (1.0f, Loc.T("Normal", "Normal")), (1.2f, Loc.T("Besar", "Large")), (1.4f, Loc.T("Sangat besar", "Extra large"))], v => s.TextScale = v));
            body.Children.Add(Toggle(Loc.T("Teks percakapan (subtitle)", "Subtitles"), Loc.T("Tampilkan semua ucapan sebagai teks.", "Show every spoken line as text."), s.Subtitles, v => s.Subtitles = v));
            body.Children.Add(Toggle(Loc.T("Bantuan membaca", "Reading assistance"), Loc.T("Teks lebih besar, ikon di setiap petunjuk, dan suara membacakan tujuan bila tersedia.", "Bigger text, icons on every hint, and objectives read aloud where voices exist."), s.ReadingAssist, v => s.ReadingAssist = v));
            body.Children.Add(Choice(Loc.T("Bantuan warna", "Colour assistance"), s.ColorAssist,
                [(ColorAssist.None, Loc.T("Normal", "Off")), (ColorAssist.Deuteranopia, "Deutan"), (ColorAssist.Protanopia, "Protan"), (ColorAssist.Tritanopia, "Tritan"), (ColorAssist.HighContrast, Loc.T("Kontras tinggi", "High contrast"))], v => s.ColorAssist = v));
            body.Children.Add(Toggle(Loc.T("Kurangi intensitas darurat", "Reduced emergency intensity"), Loc.T("Tanpa kilatan & guncangan layar, musik lebih tenang, waktu penyelamatan lebih lama, kejadian berbahaya lebih jarang.", "No flashes or screen shake, calmer music, longer rescue timers, fewer dangerous events."), s.ReducedIntensity, v => s.ReducedIntensity = v));
            body.Children.Add(Toggle(Loc.T("Kontrol sederhana", "Simplified controls"), Loc.T("Klik di tanah untuk berjalan, klik objek untuk berinteraksi. Tidak perlu keyboard.", "Click the ground to walk, click things to interact. No keyboard needed."), s.SimplifiedControls, v => s.SimplifiedControls = v));
            body.Children.Add(Toggle(Loc.T("Tutorial & tips", "Tutorials & tips"), Loc.T("Tampilkan petunjuk untuk pemain baru.", "Show hints for new players."), s.Tutorials, v => s.Tutorials = v));
            body.Children.Add(Choice(Loc.T("Kesulitan penyelamatan", "Rescue difficulty"), s.RescueTimeScale,
                [(1.6f, Loc.T("Mudah", "Easy")), (1.0f, Loc.T("Normal", "Normal")), (0.75f, Loc.T("Sulit", "Hard"))], v => s.RescueTimeScale = v));

            Section("🖥", Loc.T("Grafis", "Graphics"));
            body.Children.Add(Choice(Loc.T("Kualitas grafis", "Graphics quality"), s.Quality,
                [(GraphicsQuality.Low, Loc.T("Cepat", "Fast")), (GraphicsQuality.Medium, Loc.T("Sedang", "Medium")), (GraphicsQuality.High, Loc.T("Tinggi", "High"))], v => s.Quality = v));
            body.Children.Add(Toggle(Loc.T("Layar penuh (F11)", "Fullscreen (F11)"), "", s.Fullscreen, v =>
            {
                s.Fullscreen = v;
                window.WindowState = v ? WindowState.FullScreen : WindowState.Normal;
            }));
            body.Children.Add(Toggle(Loc.T("Tampilkan FPS", "Show FPS"), "", s.ShowFps, v => s.ShowFps = v));
            body.Children.Add(Ui.Button(Loc.T("Simpan & tutup", "Save & close"), () =>
            {
                window.ApplySettings();
                onClose();
            }, icon: "✔"));
        }

        Fill();
        return Ui.Modal("⚙", Loc.T("Pengaturan", "Settings"), body, () =>
        {
            window.ApplySettings();
            onClose();
        }, 780, 660);
    }
}
