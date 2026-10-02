using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OurHappyHome.UI;

/// <summary>A rounded bar whose value can be updated every frame without rebuilding the HUD.</summary>
public sealed class Meter : Grid
{
    private readonly Border _fill;
    private readonly double _width;
    private float _value = -1f;
    private string? _color;

    public Meter(double width, double height)
    {
        _width = width;
        Width = width;
        Height = height;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(new Border { Background = Ui.B("#EFE3D3"), CornerRadius = new CornerRadius(height / 2) });
        _fill = new Border { CornerRadius = new CornerRadius(height / 2), HorizontalAlignment = HorizontalAlignment.Left, Width = height };
        Children.Add(_fill);
    }

    public void Set(float value, string? color = null)
    {
        value = Math.Clamp(value, 0f, 100f);
        string c = color ?? Ui.Level(value);
        if (MathF.Abs(value - _value) < 0.4f && c == _color)
        {
            return;
        }

        _value = value;
        _fill.Width = Math.Max(Height, _width * value / 100f);
        if (c != _color)
        {
            _color = c;
            _fill.Background = Ui.B(c);
        }
    }
}
