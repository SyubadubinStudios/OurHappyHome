using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;

namespace OurHappyHome.UI;

/// <summary>
/// Small UI kit for the game's cozy look: palette (with colour-assist
/// variants), scalable text, cards, buttons, progress bars and portraits.
/// Everything is built in code so panels can be generated from game data.
/// </summary>
public static class Ui
{
    private static readonly Dictionary<string, Bitmap?> Images = [];

    public static GameSettings Settings { get; set; } = new();

    public static IBrush Cream => B(Settings.ColorAssist == ColorAssist.HighContrast ? "#FFFFFF" : "#FFF8EC");
    public static IBrush Paper => B(Settings.ColorAssist == ColorAssist.HighContrast ? "#FFFFFF" : "#FFFDF7");
    public static IBrush Ink => B(Settings.ColorAssist == ColorAssist.HighContrast ? "#000000" : "#3B2F2A");
    public static IBrush Muted => B(Settings.ColorAssist == ColorAssist.HighContrast ? "#222222" : "#8A7A70");
    public static IBrush Accent => B("#F28C38");
    public static IBrush Line => B(Settings.ColorAssist == ColorAssist.HighContrast ? "#000000" : "#EADBC8");
    public static IBrush Dim => B("#99201610");

    public static string GoodColor => Settings.ColorAssist switch
    {
        ColorAssist.Deuteranopia or ColorAssist.Protanopia => "#2F80ED",
        ColorAssist.Tritanopia => "#1B9E77",
        _ => "#4CAF50",
    };

    public static string WarnColor => Settings.ColorAssist switch
    {
        ColorAssist.Tritanopia => "#E7298A",
        _ => "#F2A93B",
    };

    public static string BadColor => Settings.ColorAssist switch
    {
        ColorAssist.Deuteranopia or ColorAssist.Protanopia => "#E8710A",
        _ => "#E5484D",
    };

    public static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static double Fs(double size) => size * Settings.TextScale * (Settings.ReadingAssist ? 1.12 : 1.0);

    /// <summary>Colour for a 0-100 value: good, warning or bad.</summary>
    public static string Level(float value) => value >= 55 ? GoodColor : value >= 25 ? WarnColor : BadColor;

    public static TextBlock Text(string text, double size = 14, IBrush? color = null, FontWeight weight = FontWeight.Normal, bool wrap = false) => new()
    {
        Text = text,
        FontSize = Fs(size),
        Foreground = color ?? Ink,
        FontWeight = weight,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Title(string text, double size = 22) => Text(text, size, Ink, FontWeight.Bold);

    public static TextBlock Emoji(string glyph, double size = 18) => new()
    {
        Text = glyph,
        FontSize = Fs(size),
        FontFamily = new FontFamily("Segoe UI Emoji"),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    public static Border Card(Control child, double padding = 14, string? background = null, double radius = 18) => new()
    {
        Background = background is null ? Paper : B(background),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        BorderBrush = Line,
        BorderThickness = new Thickness(Settings.ColorAssist == ColorAssist.HighContrast ? 2 : 1),
        BoxShadow = BoxShadows.Parse("0 6 18 0 #33000000"),
        Child = child,
    };

    public static Button Button(string label, Action onClick, string background = "#F28C38", string foreground = "#FFFFFF", double size = 15, string? icon = null, bool enabled = true, string? tip = null)
    {
        StackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon is not null)
        {
            content.Children.Add(Emoji(icon, size + 2));
        }

        content.Children.Add(Text(label, size, B(foreground), FontWeight.SemiBold));
        Button button = new()
        {
            Content = content,
            Background = B(background),
            Foreground = B(foreground),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 9),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = enabled,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        if (tip is not null)
        {
            ToolTip.SetTip(button, tip);
        }

        button.Click += (_, _) =>
        {
            Sounds?.Invoke("click");
            onClick();
        };
        return button;
    }

    public static Button Ghost(string label, Action onClick, string? icon = null, double size = 14, bool enabled = true) =>
        Button(label, onClick, "#FFF1DE", "#5A4636", size, icon, enabled);

    /// <summary>Hook for UI click sounds.</summary>
    public static Action<string>? Sounds { get; set; }

    /// <summary>Rounded progress bar with optional icon and label.</summary>
    public static Control Bar(float value, string? color = null, double width = 120, double height = 10, string? icon = null, string? label = null)
    {
        float v = Math.Clamp(value, 0f, 100f);
        Grid track = new() { Width = width, Height = height, VerticalAlignment = VerticalAlignment.Center };
        track.Children.Add(new Border { Background = B("#EFE3D3"), CornerRadius = new CornerRadius(height / 2) });
        track.Children.Add(new Border
        {
            Background = B(color ?? Level(v)),
            CornerRadius = new CornerRadius(height / 2),
            Width = Math.Max(height, width * v / 100f),
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        if (icon is null && label is null)
        {
            return track;
        }

        StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (icon is not null)
        {
            row.Children.Add(Emoji(icon, 13));
        }

        if (label is not null)
        {
            row.Children.Add(Text(label, 11.5, Muted));
        }

        row.Children.Add(track);
        return row;
    }

    public static Bitmap? Image(string relative)
    {
        if (Images.TryGetValue(relative, out Bitmap? cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://OurHappyHome/Assets/{relative}")));
        }
        catch (Exception)
        {
            bitmap = null;
        }

        Images[relative] = bitmap;
        return bitmap;
    }

    public static Control Portrait(MemberId id, double size = 56, bool danger = false)
    {
        Grid grid = new() { Width = size, Height = size };
        grid.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(size / 2),
            Background = B(FamilyNames.Accent(id)),
            BorderBrush = danger ? B(BadColor) : B("#FFFFFF"),
            BorderThickness = new Thickness(danger ? 4 : 3),
        });
        if (Image($"Art/portrait-{FamilyNames.ModelName(id)}.png") is { } bitmap)
        {
            grid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(size / 2),
                ClipToBounds = true,
                Margin = new Thickness(3),
                Child = new Avalonia.Controls.Image { Source = bitmap, Stretch = Stretch.UniformToFill },
            });
        }
        else
        {
            grid.Children.Add(Text(FamilyNames.Short(id)[..1], size * 0.4, B("#FFFFFF"), FontWeight.Bold));
        }

        return grid;
    }

    public static Control Pill(string text, string background, string foreground = "#FFFFFF", double size = 12) => new Border
    {
        Background = B(background),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 3),
        Child = Text(text, size, B(foreground), FontWeight.SemiBold),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Grid Row(params (Control Control, GridLength Width)[] cells)
    {
        Grid grid = new();
        int i = 0;
        foreach ((Control control, GridLength width) in cells)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(width));
            Grid.SetColumn(control, i++);
            grid.Children.Add(control);
        }

        return grid;
    }

    public static StackPanel Stack(double spacing = 8, Orientation orientation = Orientation.Vertical, params Control[] children)
    {
        StackPanel panel = new() { Spacing = spacing, Orientation = orientation };
        foreach (Control c in children)
        {
            panel.Children.Add(c);
        }

        return panel;
    }

    /// <summary>A modal window frame with title, close button and scrollable body.</summary>
    public static Border Modal(string icon, string title, Control body, Action onClose, double width = 760, double height = 560)
    {
        Grid header = Row(
            (Stack(10, Orientation.Horizontal, Emoji(icon, 26), Title(title, 24)), GridLength.Star),
            (Button("✕", onClose, "#F3E2CF", "#5A4636", 14), GridLength.Auto));
        Grid layout = new() { RowDefinitions = new RowDefinitions("Auto,*") };
        layout.Children.Add(header);
        ScrollViewer scroll = new() { Content = body, Margin = new Thickness(0, 12, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Border card = Card(layout, 20, "#FFF8EC", 24);
        card.Width = width;
        card.MaxHeight = height;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        return card;
    }
}
