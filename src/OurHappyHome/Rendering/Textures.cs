using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OurHappyHome.Core.World;
using ThreeNet;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Vector = Avalonia.Vector;

namespace OurHappyHome.Rendering;

/// <summary>Deterministic value noise for the texture generators.</summary>
internal static class Noise
{
    private static float Hash(int x, int y, int seed)
    {
        int h = (x * 374761393) + (y * 668265263) + (seed * 362437);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
    }

    public static float Value(float x, float y, int seed = 0)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - (2f * fx));
        fy = fy * fy * (3f - (2f * fy));
        float a = Hash(x0, y0, seed);
        float b = Hash(x0 + 1, y0, seed);
        float c = Hash(x0, y0 + 1, seed);
        float d = Hash(x0 + 1, y0 + 1, seed);
        return float.Lerp(float.Lerp(a, b, fx), float.Lerp(c, d, fx), fy);
    }

    /// <summary>Tileable fbm: the lattice wraps every <paramref name="period"/> cells.</summary>
    public static float Fbm(float x, float y, int octaves = 4, int seed = 0)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float frequency = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(x * frequency, y * frequency, seed + i) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return sum / total;
    }
}

/// <summary>
/// Every material in the game, generated at start-up: floors, walls, roof
/// tiles, grass, roads, water, plus flat colours and sprite textures for
/// particles, name tags and speech bubbles (drawn with Avalonia).
/// </summary>
public sealed class Textures
{
    private const int Size = 256;
    private readonly Scene _scene;
    private readonly Dictionary<string, Material> _colors = [];
    private readonly Dictionary<string, Material> _paints = [];
    private readonly Dictionary<string, Material> _sprites = [];
    private readonly Dictionary<string, (Texture Texture, Vector2 Size)> _labels = [];
    private Texture? _stuccoGrey;

    public Textures(Scene scene)
    {
        _scene = scene;
        Grass = Surface((u, v) =>
        {
            float n = Noise.Fbm(u * 64f, v * 64f, 4, 3);
            float patch = Noise.Fbm(u * 6f, v * 6f, 3, 9);
            Vector3 c = Vector3.Lerp(new Vector3(0.20f, 0.42f, 0.12f), new Vector3(0.38f, 0.62f, 0.2f), (n * 0.6f) + (patch * 0.4f));
            return (c, n);
        }, 0.95f, new Vector2(40f, 40f));

        Lawn = Surface((u, v) =>
        {
            float stripe = (((int)(u * 8f)) % 2 == 0) ? 1f : 0.9f;
            float n = Noise.Fbm(u * 70f, v * 70f, 4, 5);
            Vector3 c = Vector3.Lerp(new Vector3(0.24f, 0.5f, 0.14f), new Vector3(0.36f, 0.62f, 0.2f), n) * stripe;
            return (c, n);
        }, 0.92f, new Vector2(6f, 6f));

        Wood = Surface((u, v) =>
        {
            int plank = (int)(v * 6f);
            float shift = (plank * 0.37f) % 1f;
            float along = ((u * 2f) + shift) % 1f;
            bool gap = ((v * 6f) % 1f) < 0.035f || along < 0.012f;
            float grain = Noise.Fbm((u * 90f) + (plank * 13f), v * 14f, 4, 37);
            Vector3 wood = Vector3.Lerp(new Vector3(0.42f, 0.25f, 0.12f), new Vector3(0.66f, 0.43f, 0.22f), grain);
            return (gap ? wood * 0.5f : wood, gap ? 0f : grain * 0.6f);
        }, 0.45f, new Vector2(1.5f, 1.5f));

        Tile = Surface((u, v) =>
        {
            bool grout = ((u * 5f) % 1f) < 0.04f || ((v * 5f) % 1f) < 0.04f;
            float n = Noise.Fbm(u * 40f, v * 40f, 2, 41);
            Vector3 c = grout ? new Vector3(0.62f, 0.64f, 0.66f) : new Vector3(0.84f, 0.92f, 0.95f) * (0.95f + (n * 0.05f));
            return (c, grout ? 0f : 0.8f);
        }, 0.25f, new Vector2(1.2f, 1.2f));

        Checker = Surface((u, v) =>
        {
            bool dark = (((int)(u * 8f)) + ((int)(v * 8f))) % 2 == 0;
            float n = Noise.Fbm(u * 50f, v * 50f, 2, 43);
            Vector3 c = dark ? new Vector3(0.35f, 0.62f, 0.55f) : new Vector3(0.95f, 0.94f, 0.88f);
            return (c * (0.95f + (n * 0.05f)), 0.5f);
        }, 0.3f, new Vector2(1f, 1f));

        Carpet = Surface((u, v) =>
        {
            float n = Noise.Fbm(u * 160f, v * 160f, 3, 47);
            Vector3 c = new Vector3(0.82f, 0.74f, 0.66f) * (0.88f + (n * 0.16f));
            return (c, n);
        }, 0.98f, new Vector2(2f, 2f));

        Concrete = Surface((u, v) =>
        {
            float n = Noise.Fbm(u * 50f, v * 50f, 4, 53);
            float g = 0.55f + (n * 0.12f);
            return (new Vector3(g, g, g * 1.02f), n);
        }, 0.9f, new Vector2(2f, 2f));

        Asphalt = Surface((u, v) =>
        {
            float grain = Noise.Fbm(u * 120f, v * 120f, 4, 3);
            float g = 0.11f + (grain * 0.06f);
            return (new Vector3(g, g * 1.02f, g * 1.08f), grain);
        }, 0.9f, new Vector2(30f, 30f));

        Paving = Surface((u, v) =>
        {
            float jx = MathF.Abs(((u * 4f) % 1f) - 0.5f);
            float jy = MathF.Abs(((v * 4f) % 1f) - 0.5f);
            float joint = MathF.Min(jx, jy) < 0.45f ? 1f : 0.5f;
            float speck = Noise.Fbm(u * 90f, v * 90f, 3, 11);
            return (new Vector3(0.78f, 0.74f, 0.68f) * (0.8f + (speck * 0.2f)) * joint, joint * 0.8f);
        }, 0.85f, new Vector2(4f, 4f));

        Sand = Surface((u, v) =>
        {
            float n = Noise.Fbm(u * 100f, v * 100f, 4, 61);
            float ripple = (MathF.Sin((u * 60f) + (Noise.Fbm(u * 4f, v * 4f, 2, 62) * 8f)) * 0.5f) + 0.5f;
            return (new Vector3(0.93f, 0.82f, 0.58f) * (0.9f + (n * 0.1f)), (ripple * 0.4f) + (n * 0.6f));
        }, 0.95f, new Vector2(40f, 40f));

        RoofTile = Surface((u, v) =>
        {
            float ripple = (MathF.Sin(u * MathF.Tau * 10f) * 0.5f) + 0.5f;
            float course = ((v * 8f) % 1f) < 0.12f ? 0.6f : 1f;
            float wear = Noise.Fbm(u * 40f, v * 40f, 3, 23);
            Vector3 c = Vector3.Lerp(new Vector3(0.55f, 0.16f, 0.08f), new Vector3(0.75f, 0.28f, 0.14f), ripple) * course;
            return (Vector3.Lerp(c, new Vector3(0.45f, 0.3f, 0.25f), wear * 0.2f), (ripple * 0.7f) + (course * 0.3f));
        }, 0.8f, new Vector2(2f, 2f));

        Siding = Surface((u, v) =>
        {
            float board = (v * 10f) % 1f;
            float shade = board < 0.12f ? 0.78f : 1f;
            float n = Noise.Fbm(u * 80f, v * 6f, 3, 71);
            return (new Vector3(0.98f, 0.9f, 0.7f) * shade * (0.95f + (n * 0.05f)), board < 0.12f ? 0f : 0.7f);
        }, 0.75f, new Vector2(1.2f, 1.2f));

        Brick = Surface((u, v) =>
        {
            int row = (int)(v * 16f);
            float offset = (row % 2 == 0) ? 0f : 0.5f;
            float bx = ((u * 8f) + offset) % 1f;
            float by = (v * 16f) % 1f;
            bool mortar = bx < 0.05f || by < 0.09f;
            float wear = Noise.Fbm(u * 60f, v * 60f, 3, 17);
            Vector3 clay = Vector3.Lerp(new Vector3(0.6f, 0.26f, 0.18f), new Vector3(0.75f, 0.38f, 0.26f), wear);
            return (mortar ? new Vector3(0.8f, 0.78f, 0.74f) : clay, mortar ? 0.2f : 0.8f);
        }, 0.88f, new Vector2(2f, 2f));

        Dirt = Surface((u, v) =>
        {
            float n = Noise.Fbm(u * 60f, v * 60f, 4, 81);
            return (new Vector3(0.36f, 0.24f, 0.14f) * (0.8f + (n * 0.3f)), n);
        }, 0.95f, new Vector2(2f, 2f));

        Water = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.04f, 0.33f, 0.58f, 1f), 0.0f, 0.3f) with
        {
            NormalMap = WaveNormal(),
            NormalScale = 0.6f,
            UvScale = new Vector2(30f, 30f),
            Reflectance = 0.25f,
        });
        PoolWater = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.2f, 0.7f, 0.9f, 0.8f), 0f, 0.05f) with
        {
            AlphaMode = AlphaMode.Blend,
            NormalMap = WaveNormal(),
            NormalScale = 0.5f,
            UvScale = new Vector2(3f, 3f),
            DepthWrite = false,
        });
        Glass = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.62f, 0.8f, 0.95f, 0.35f), 0f, 0.05f) with { AlphaMode = AlphaMode.Blend, DepthWrite = false });
        WindowGlow = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(1f, 0.86f, 0.55f, 0.85f), 0f, 0.2f) with
        {
            AlphaMode = AlphaMode.Blend,
            Emissive = new Vector3(1f, 0.75f, 0.42f),
            EmissiveIntensity = 0f,
            DepthWrite = false,
        });
        LampGlow = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(1f, 0.95f, 0.8f, 1f), 0f, 0.3f) with
        {
            Emissive = new Vector3(1f, 0.85f, 0.55f),
            EmissiveIntensity = 0.3f,
        });
    }

    public Scene Scene => _scene;

    public Material Grass { get; }
    public Material Lawn { get; }
    public Material Wood { get; }
    public Material Tile { get; }
    public Material Checker { get; }
    public Material Carpet { get; }
    public Material Concrete { get; }
    public Material Asphalt { get; }
    public Material Paving { get; }
    public Material Sand { get; }
    public Material RoofTile { get; }
    public Material Siding { get; }
    public Material Brick { get; }
    public Material Dirt { get; }
    public Material Water { get; }
    public Material PoolWater { get; }
    public Material Glass { get; }
    public Material WindowGlow { get; }
    public Material LampGlow { get; }

    public Material Floor(FloorStyle style) => style switch
    {
        FloorStyle.Wood => Wood,
        FloorStyle.Tile => Tile,
        FloorStyle.Carpet => Carpet,
        FloorStyle.Concrete => Concrete,
        FloorStyle.Grass => Lawn,
        _ => Checker,
    };

    /// <summary>Flat PBR colour, cached by hex value and finish.</summary>
    public Material Solid(string hex, float roughness = 0.7f, float metallic = 0f, float emissive = 0f)
    {
        string key = $"{hex}:{roughness}:{metallic}:{emissive}";
        if (!_colors.TryGetValue(key, out Material? material))
        {
            Vector4 c = Hex(hex);
            MaterialOptions options = MaterialOptions.Pbr(c, metallic, roughness);
            if (emissive > 0f)
            {
                options = options with { Emissive = new Vector3(c.X, c.Y, c.Z), EmissiveIntensity = emissive };
            }

            material = _scene.CreateMaterial(options);
            _colors[key] = material;
        }

        return material;
    }

    /// <summary>Interior wall paint: a tinted plaster texture.</summary>
    public Material Paint(string hex)
    {
        if (!_paints.TryGetValue(hex, out Material? material))
        {
            _stuccoGrey ??= Gray((u, v) => 0.86f + (Noise.Fbm(u * 120f, v * 120f, 4, 29) * 0.14f));
            material = _scene.CreateMaterial(MaterialOptions.Pbr(Hex(hex), 0f, 0.85f) with { BaseColorMap = _stuccoGrey, UvScale = new Vector2(2f, 2f) });
            _paints[hex] = material;
        }

        return material;
    }

    public static Vector4 Hex(string hex)
    {
        string h = hex.TrimStart('#');
        uint value = uint.Parse(h.Length == 6 ? h : h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return MathHelpers.FromHex(value);
    }

    // ------------------------------------------------------------ generators

    private Material Surface(Func<float, float, (Vector3 Color, float Height)> sample, float roughness, Vector2 uvScale)
    {
        byte[] color = new byte[Size * Size * 4];
        float[] height = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                (Vector3 rgb, float h) = sample(x / (float)Size, y / (float)Size);
                int i = ((y * Size) + x) * 4;
                color[i] = Encode(rgb.X);
                color[i + 1] = Encode(rgb.Y);
                color[i + 2] = Encode(rgb.Z);
                color[i + 3] = 255;
                height[(y * Size) + x] = h;
            }
        }

        Texture colorMap = _scene.CreateTexture(Size, Size, color);
        colorMap.SetSampler(WrapMode.Repeat, WrapMode.Repeat, linearFilter: true, mipmaps: true, anisotropy: 8);
        return _scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, roughness) with
        {
            BaseColorMap = colorMap,
            NormalMap = NormalFromHeight(height, 2f),
            NormalScale = 0.8f,
            UvScale = uvScale,
        });
    }

    private Texture Gray(Func<float, float, float> sample)
    {
        byte[] pixels = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                byte g = Encode(sample(x / (float)Size, y / (float)Size));
                int i = ((y * Size) + x) * 4;
                pixels[i] = g;
                pixels[i + 1] = g;
                pixels[i + 2] = g;
                pixels[i + 3] = 255;
            }
        }

        Texture t = _scene.CreateTexture(Size, Size, pixels);
        t.SetSampler(WrapMode.Repeat, WrapMode.Repeat, true, true, 8);
        return t;
    }

    private Texture WaveNormal()
    {
        float[] height = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                float v = y / (float)Size;
                height[(y * Size) + x] = (MathF.Sin((u + (Noise.Fbm(u * 4, v * 4, 2, 5) * 0.3f)) * MathF.Tau * 4f) * 0.5f) + (Noise.Fbm(u * 16f, v * 16f, 3, 7) * 0.5f);
            }
        }

        return NormalFromHeight(height, 3f);
    }

    private Texture NormalFromHeight(float[] height, float strength)
    {
        byte[] pixels = new byte[Size * Size * 4];
        float At(int x, int y) => height[(((y + Size) % Size) * Size) + ((x + Size) % Size)];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = At(x + 1, y) - At(x - 1, y);
                float dy = At(x, y + 1) - At(x, y - 1);
                Vector3 n = Vector3.Normalize(new Vector3(-dx * strength, -dy * strength, 1f));
                int i = ((y * Size) + x) * 4;
                pixels[i] = (byte)((n.X * 0.5f + 0.5f) * 255f);
                pixels[i + 1] = (byte)((n.Y * 0.5f + 0.5f) * 255f);
                pixels[i + 2] = (byte)((n.Z * 0.5f + 0.5f) * 255f);
                pixels[i + 3] = 255;
            }
        }

        Texture t = _scene.CreateTexture(Size, Size, pixels, TextureFormat.Rgba8Unorm);
        t.SetSampler(WrapMode.Repeat, WrapMode.Repeat, true, true, 8);
        return t;
    }

    private static byte Encode(float linear)
    {
        float c = Math.Clamp(linear, 0f, 1f);
        float srgb = c <= 0.0031308f ? c * 12.92f : (1.055f * MathF.Pow(c, 1f / 2.4f)) - 0.055f;
        return (byte)MathF.Round(srgb * 255f);
    }

    // ------------------------------------------------------- avalonia sprites

    /// <summary>Renders with Avalonia and uploads as a texture (UI thread only).</summary>
    public Texture Draw(int width, int height, Action<DrawingContext> draw)
    {
        using RenderTargetBitmap bitmap = new(new PixelSize(width, height), new Vector(96, 96));
        using (DrawingContext context = bitmap.CreateDrawingContext())
        {
            draw(context);
        }

        using MemoryStream png = new();
        bitmap.Save(png);
        Texture texture = _scene.LoadTexture(png.ToArray(), srgb: true);
        texture.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, linearFilter: true, mipmaps: true, anisotropy: 4);
        return texture;
    }

    /// <summary>An unlit, alpha blended sprite material (particles, icons), cached by key.</summary>
    public Material Sprite(string key, Action<DrawingContext> draw, int size = 128, float emissive = 1f, bool additive = false)
    {
        if (_sprites.TryGetValue(key, out Material? material))
        {
            return material;
        }

        Texture texture = Draw(size, size, draw);
        material = _scene.CreateMaterial(MaterialOptions.Basic(Vector4.One) with
        {
            BaseColorMap = texture,
            AlphaMode = AlphaMode.Blend,
            CullMode = CullMode.None,
            DepthWrite = false,
            Emissive = Vector3.One,
            EmissiveIntensity = emissive,
            EmissiveMap = additive ? texture : null,
            RenderOrder = 10,
        });
        _sprites[key] = material;
        return material;
    }

    /// <summary>A sprite showing an emoji glyph.</summary>
    public Material Emoji(string glyph, int size = 128) => Sprite("emoji:" + glyph, dc =>
    {
        FormattedText text = new(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Emoji"), size * 0.72, Brushes.White);
        dc.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
    }, size);

    /// <summary>A soft radial blob (smoke, glow, rain splash).</summary>
    public Material Blob(string key, Color color, float softness = 1f) => Sprite("blob:" + key, dc =>
    {
        RadialGradientBrush brush = new()
        {
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(Color.FromArgb((byte)(color.A * 0.6f), color.R, color.G, color.B), 0.45 * softness),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
            },
        };
        dc.DrawEllipse(brush, null, new Rect(0, 0, 128, 128));
    });

    /// <summary>Text texture sized to the text (name tags, speech bubbles, signs).</summary>
    public (Texture Texture, Vector2 Size) Label(string text, Color background, Color foreground, double fontSize = 28, double maxWidth = 420, bool bubble = false)
    {
        string key = $"{text}|{background}|{foreground}|{fontSize}|{bubble}";
        if (_labels.TryGetValue(key, out (Texture, Vector2) cached))
        {
            return cached;
        }

        Typeface typeface = new("Inter", FontStyle.Normal, FontWeight.SemiBold);
        FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, new SolidColorBrush(foreground));
        bool wraps = formatted.Width > maxWidth;
        if (wraps)
        {
            formatted.MaxTextWidth = maxWidth;
        }
        double pad = fontSize * 0.55;
        int w = (int)Math.Ceiling(Math.Min(maxWidth, formatted.Width) + (pad * 2));
        int tail = bubble ? (int)(fontSize * 0.6) : 0;
        int h = (int)Math.Ceiling(formatted.Height + (pad * 1.3)) + tail;
        Texture texture = Draw(w, h, dc =>
        {
            Rect box = new(1, 1, w - 2, h - 2 - tail);
            dc.DrawRectangle(new SolidColorBrush(background), new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 2), box, (float)(fontSize * 0.5), (float)(fontSize * 0.5));
            if (bubble)
            {
                StreamGeometry g = new();
                using (StreamGeometryContext ctx = g.Open())
                {
                    ctx.BeginFigure(new Point((w / 2.0) - (tail * 0.7), h - tail - 3), true);
                    ctx.LineTo(new Point(w / 2.0, h - 1));
                    ctx.LineTo(new Point((w / 2.0) + (tail * 0.7), h - tail - 3));
                    ctx.EndFigure(true);
                }

                dc.DrawGeometry(new SolidColorBrush(background), null, g);
            }

            dc.DrawText(formatted, new Point(pad, pad * 0.65));
        });

        (Texture, Vector2) result = (texture, new Vector2(w, h));
        _labels[key] = result;
        if (_labels.Count > 300)
        {
            // Speech bubbles change all the time: forget old ones.
            foreach (string old in _labels.Keys.Take(100).ToList())
            {
                _labels[old].Texture.Destroy();
                _labels.Remove(old);
            }
        }

        return result;
    }

    public Material LabelMaterial((Texture Texture, Vector2 Size) label, bool unlit = true) =>
        _scene.CreateMaterial(MaterialOptions.Basic(Vector4.One) with
        {
            BaseColorMap = label.Texture,
            AlphaMode = AlphaMode.Blend,
            CullMode = CullMode.None,
            DepthWrite = false,
            Emissive = Vector3.One,
            EmissiveIntensity = unlit ? 0.9f : 0f,
            EmissiveMap = label.Texture,
            RenderOrder = 20,
        });

    /// <summary>Signboard with big text (shops, school).</summary>
    public Material Sign(string text, Color background, Color foreground, int width = 512, int height = 128)
    {
        Texture texture = Draw(width, height, dc =>
        {
            dc.FillRectangle(new SolidColorBrush(background), new Rect(0, 0, width, height));
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(foreground), height * 0.04), new Rect(height * 0.06, height * 0.06, width - (height * 0.12), height - (height * 0.12)), (float)(height * 0.08));
            FormattedText title = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Bold), height * 0.48, new SolidColorBrush(foreground));
            if (title.Width > width * 0.9)
            {
                title.SetFontSize(height * 0.48 * width * 0.9 / title.Width);
            }

            dc.DrawText(title, new Point((width - title.Width) / 2, (height - title.Height) / 2));
        });
        return _scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.6f) with
        {
            BaseColorMap = texture,
            Emissive = Vector3.One,
            EmissiveMap = texture,
            EmissiveIntensity = 0.15f,
        });
    }
}
