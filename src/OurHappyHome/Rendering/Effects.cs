using System.Numerics;
using Avalonia.Media;
using OurHappyHome.Core.Simulation;
using ThreeNet;
using Color = Avalonia.Media.Color;

namespace OurHappyHome.Rendering;

/// <summary>
/// Billboard particle system: pooled camera-facing quads for hearts,
/// sparkles, confetti, fireworks, smoke, steam, flames, splashes and more,
/// plus a rain curtain that follows the camera.
/// </summary>
public sealed class Effects
{
    private const int PoolSize = 420;
    private const int RainDrops = 260;

    private sealed class Particle
    {
        public Node Node = null!;
        public Material? Material;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
        public float Size0;
        public float Size1;
        public float Gravity;
        public float Drag;
        public float Spin;
        public float Angle;
        public bool Active;
        public bool Upright;
    }

    public sealed class Emitter
    {
        public EffectKind Kind;
        public Vector3 Position;
        public float Rate;
        public float Scale = 1f;
        public bool Alive = true;
        internal float Accumulator;
    }

    private readonly Scene _scene;
    private readonly Meshes _meshes;
    private readonly Textures _textures;
    private readonly Random _random = new(5);
    private readonly Particle[] _pool = new Particle[PoolSize];
    private readonly List<Emitter> _emitters = [];
    private readonly Node[] _rain = new Node[RainDrops];
    private readonly Vector3[] _rainPos = new Vector3[RainDrops];
    private readonly bool[] _rainShown = new bool[RainDrops];
    private readonly Node _root;
    private readonly Material[] _confetti;
    private readonly Material[] _fireworks;
    private readonly Material _smoke;
    private readonly Material _steam;
    private readonly Material _flame;
    private readonly Material _splash;
    private readonly Material _dust;
    private readonly Material _flour;
    private readonly Material _rainMaterial;
    private int _next;

    public Effects(Scene scene, Meshes meshes, Textures textures)
    {
        _scene = scene;
        _meshes = meshes;
        _textures = textures;
        _root = scene.CreateNode(null, "effects");

        _smoke = textures.Blob("smoke", Color.FromArgb(110, 95, 95, 100));
        _steam = textures.Blob("steam", Color.FromArgb(120, 255, 255, 255));
        _flame = textures.Sprite("flame", dc =>
        {
            RadialGradientBrush b = new()
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(255, 255, 250, 200), 0),
                    new GradientStop(Color.FromArgb(230, 255, 170, 40), 0.35),
                    new GradientStop(Color.FromArgb(120, 240, 70, 20), 0.7),
                    new GradientStop(Color.FromArgb(0, 200, 40, 10), 1),
                },
            };
            dc.DrawEllipse(b, null, new Avalonia.Rect(0, 0, 128, 128));
        }, emissive: 3f, additive: true);
        _splash = textures.Blob("splash", Color.FromArgb(200, 190, 230, 255));
        _dust = textures.Blob("dust", Color.FromArgb(160, 170, 140, 100));
        _flour = textures.Blob("flour", Color.FromArgb(220, 255, 255, 250));
        _rainMaterial = textures.Sprite("rain", dc =>
        {
            LinearGradientBrush b = new()
            {
                StartPoint = new Avalonia.RelativePoint(0.5, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(0.5, 1, Avalonia.RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, 200, 220, 255), 0), new GradientStop(Color.FromArgb(170, 220, 235, 255), 1) },
            };
            dc.FillRectangle(b, new Avalonia.Rect(56, 0, 16, 128));
        }, emissive: 0.6f);

        string[] confettiColors = ["#FF6B6B", "#FFD93D", "#6BCB77", "#4D96FF", "#C77DFF"];
        _confetti = [.. confettiColors.Select(c => textures.Sprite("confetti" + c, dc => dc.FillRectangle(new SolidColorBrush(Color.Parse(c)), new Avalonia.Rect(24, 40, 80, 48)), emissive: 1.2f))];
        _fireworks = [.. confettiColors.Select(c => textures.Blob("fw" + c, Color.Parse(c), 0.7f))];

        for (int i = 0; i < PoolSize; i++)
        {
            Node node = scene.CreateNode(_root, "particle");
            node.Visible = false;
            _pool[i] = new Particle { Node = node };
        }

        for (int i = 0; i < RainDrops; i++)
        {
            Node drop = scene.AddMesh(meshes.UnitPlane, _rainMaterial, _root, "rain");
            drop.CastShadow = false;
            drop.ReceiveShadow = false;
            drop.Visible = false;
            _rain[i] = drop;
            _rainPos[i] = new Vector3(Rand(-14, 14), Rand(0, 14), Rand(-14, 14));
        }
    }

    /// <summary>0 = no rain, 1 = downpour.</summary>
    public float RainIntensity { get; set; }

    public float Wind { get; set; }

    private float Rand(float min, float max) => min + ((float)_random.NextDouble() * (max - min));

    public Emitter AddEmitter(EffectKind kind, Vector3 position, float rate, float scale = 1f)
    {
        Emitter e = new() { Kind = kind, Position = position, Rate = rate, Scale = scale };
        _emitters.Add(e);
        return e;
    }

    public void RemoveEmitter(Emitter emitter)
    {
        emitter.Alive = false;
        _emitters.Remove(emitter);
    }

    /// <summary>A one-shot burst for a simulation effect event.</summary>
    public void Burst(EffectKind kind, Vector3 at, float scale = 1f)
    {
        int count = kind switch
        {
            EffectKind.Confetti => 60,
            EffectKind.Fireworks => 4,
            EffectKind.Sparkles or EffectKind.Stars => 14,
            EffectKind.Hearts => 7,
            EffectKind.Smoke => 10,
            EffectKind.Splash => 16,
            EffectKind.Flour => 18,
            EffectKind.Coins => 9,
            EffectKind.Leaves => 10,
            EffectKind.Dust => 10,
            _ => 5,
        };
        count = (int)(count * MathF.Max(0.5f, scale));
        if (kind == EffectKind.Fireworks)
        {
            for (int i = 0; i < count; i++)
            {
                Firework(at + new Vector3(Rand(-6, 6), Rand(8, 14), Rand(-6, 6)));
            }

            return;
        }

        for (int i = 0; i < count; i++)
        {
            Spawn(kind, at, scale);
        }
    }

    private void Firework(Vector3 center)
    {
        Material material = _fireworks[_random.Next(_fireworks.Length)];
        for (int i = 0; i < 26; i++)
        {
            Vector3 dir = Vector3.Normalize(new Vector3(Rand(-1, 1), Rand(-1, 1), Rand(-1, 1)));
            Emit(material, center, dir * Rand(5f, 7f), 1.6f, 0.45f, 0.1f, -2f, 0.8f);
        }
    }

    private void Spawn(EffectKind kind, Vector3 at, float scale)
    {
        Vector3 jitter = new(Rand(-0.25f, 0.25f), Rand(-0.1f, 0.2f), Rand(-0.25f, 0.25f));
        switch (kind)
        {
            case EffectKind.Hearts:
                Emit(_textures.Emoji("❤"), at + jitter, new(Rand(-0.3f, 0.3f), Rand(0.6f, 1.1f), Rand(-0.3f, 0.3f)), 1.6f, 0.32f * scale, 0.15f, 0f, 0.5f);
                break;
            case EffectKind.Sparkles:
                Emit(_textures.Emoji("✨"), at + jitter, new(Rand(-1.2f, 1.2f), Rand(0.5f, 1.8f), Rand(-1.2f, 1.2f)), 1.1f, 0.3f * scale, 0.05f, -1f, 1.5f);
                break;
            case EffectKind.Stars:
                Emit(_textures.Emoji("⭐"), at + jitter, new(Rand(-1.5f, 1.5f), Rand(1f, 2.4f), Rand(-1.5f, 1.5f)), 1.2f, 0.28f * scale, 0.05f, -3f, 1.2f);
                break;
            case EffectKind.Confetti:
                Emit(_confetti[_random.Next(_confetti.Length)], at + jitter, new(Rand(-3f, 3f), Rand(3f, 6.5f), Rand(-3f, 3f)), 3f, 0.16f * scale, 0.16f * scale, -6f, 1.2f, Rand(-8f, 8f));
                break;
            case EffectKind.Smoke:
                Emit(_smoke, at + jitter, new(Rand(-0.2f, 0.2f), Rand(0.5f, 1.0f), Rand(-0.2f, 0.2f)), 2.6f, 0.35f * scale, 1.1f * scale, 0.1f, 0.4f);
                break;
            case EffectKind.Steam:
                Emit(_steam, at + jitter, new(Rand(-0.1f, 0.1f), Rand(0.4f, 0.7f), Rand(-0.1f, 0.1f)), 1.6f, 0.2f * scale, 0.8f * scale, 0f, 0.3f);
                break;
            case EffectKind.Flour:
                Emit(_flour, at + jitter, new(Rand(-1.5f, 1.5f), Rand(0.5f, 2f), Rand(-1.5f, 1.5f)), 2f, 0.3f * scale, 0.9f * scale, -2f, 1.5f);
                break;
            case EffectKind.Splash:
                Emit(_splash, at + jitter, new(Rand(-1.5f, 1.5f), Rand(2f, 4f), Rand(-1.5f, 1.5f)), 1f, 0.15f * scale, 0.05f, -9f, 0.3f);
                break;
            case EffectKind.Dust:
                Emit(_dust, at + jitter, new(Rand(-1f, 1f), Rand(0.3f, 1f), Rand(-1f, 1f)), 1.4f, 0.3f * scale, 1.1f * scale, 0f, 1.2f);
                break;
            case EffectKind.Leaves:
                Emit(_textures.Emoji("🍃"), at + jitter + new Vector3(0, 1.5f, 0), new(Rand(-1f, 1f) + Wind, Rand(-0.6f, 0.2f), Rand(-1f, 1f)), 3f, 0.25f * scale, 0.2f, -0.4f, 0.2f, Rand(-3, 3));
                break;
            case EffectKind.Zzz:
                Emit(_textures.Emoji("💤"), at + jitter + new Vector3(0, 0.6f, 0), new(Rand(-0.1f, 0.1f), 0.35f, Rand(-0.1f, 0.1f)), 2.4f, 0.2f, 0.45f, 0f, 0.1f);
                break;
            case EffectKind.Notes:
                Emit(_textures.Emoji(_random.Next(2) == 0 ? "🎵" : "🎶"), at + jitter, new(Rand(-0.4f, 0.4f), Rand(0.5f, 0.9f), Rand(-0.4f, 0.4f)), 1.8f, 0.28f, 0.3f, 0f, 0.3f, Rand(-1, 1));
                break;
            case EffectKind.Coins:
                Emit(_textures.Emoji("🪙"), at + jitter, new(Rand(-1.2f, 1.2f), Rand(2f, 3.5f), Rand(-1.2f, 1.2f)), 1.4f, 0.26f, 0.22f, -6f, 0.4f, Rand(-6, 6));
                break;
            default:
                Emit(_flame, at + jitter, new(Rand(-0.2f, 0.2f), Rand(1f, 2f), Rand(-0.2f, 0.2f)), 0.8f, 0.5f * scale, 0.1f, 0f, 0.5f);
                break;
        }
    }

    private void Emit(Material material, Vector3 position, Vector3 velocity, float life, float size0, float size1, float gravity, float drag, float spin = 0f)
    {
        Particle p = _pool[_next];
        _next = (_next + 1) % PoolSize;
        if (p.Material != material)
        {
            if (p.Material is not null)
            {
                p.Node.DetachMesh();
            }

            p.Node.AttachMesh(_meshes.UnitPlane, material);
            p.Node.CastShadow = false;
            p.Node.ReceiveShadow = false;
            p.Material = material;
        }

        p.Position = position;
        p.Velocity = velocity;
        p.Life = 0f;
        p.MaxLife = life * Rand(0.8f, 1.2f);
        p.Size0 = size0;
        p.Size1 = size1;
        p.Gravity = gravity;
        p.Drag = drag;
        p.Spin = spin;
        p.Angle = Rand(0, MathF.Tau);
        p.Active = true;
        p.Node.Visible = true;
    }

    public void Update(float dt, Node camera)
    {
        Quaternion facing = camera.Rotation;
        Vector3 cameraPos = camera.Position;

        foreach (Emitter e in _emitters.ToList())
        {
            e.Accumulator += e.Rate * dt;
            while (e.Accumulator >= 1f)
            {
                e.Accumulator -= 1f;
                if (e.Kind == EffectKind.Sparkles)
                {
                    Spawn(EffectKind.Sparkles, e.Position, e.Scale);
                }
                else if (e.Kind == EffectKind.Smoke)
                {
                    Spawn(EffectKind.Smoke, e.Position, e.Scale);
                }
                else if (e.Kind == EffectKind.Steam)
                {
                    Spawn(EffectKind.Steam, e.Position, e.Scale);
                }
                else
                {
                    // Fire: flames plus a little smoke.
                    Emit(_flame, e.Position + new Vector3(Rand(-0.3f, 0.3f) * e.Scale, 0, Rand(-0.3f, 0.3f) * e.Scale), new(Rand(-0.15f, 0.15f), Rand(1f, 1.8f), Rand(-0.15f, 0.15f)), 0.7f, 0.6f * e.Scale, 0.1f, 0.5f, 0.6f);
                    if (_random.NextDouble() < 0.25)
                    {
                        Spawn(EffectKind.Smoke, e.Position + new Vector3(0, 1.2f * e.Scale, 0), e.Scale);
                    }
                }
            }
        }

        foreach (Particle p in _pool)
        {
            if (!p.Active)
            {
                continue;
            }

            p.Life += dt;
            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
                p.Node.Visible = false;
                continue;
            }

            p.Velocity += new Vector3(Wind * 0.3f, p.Gravity, 0f) * dt;
            p.Velocity *= 1f - MathF.Min(1f, p.Drag * dt);
            p.Position += p.Velocity * dt;
            p.Angle += p.Spin * dt;
            float t = p.Life / p.MaxLife;
            float size = float.Lerp(p.Size0, p.Size1, t) * (t < 0.1f ? t / 0.1f : 1f);
            p.Node.SetTransform(p.Position, facing * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, p.Angle), new Vector3(size, size, size));
        }

        UpdateRain(dt, cameraPos, camera);
    }

    private void UpdateRain(float dt, Vector3 cameraPos, Node camera)
    {
        int visible = (int)(RainDrops * Math.Clamp(RainIntensity, 0f, 1f));
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, camera.Rotation);
        Vector3 focus = cameraPos + (new Vector3(forward.X, 0f, forward.Z) * 7f);
        float yaw = MathF.Atan2(cameraPos.X - focus.X, cameraPos.Z - focus.Z);
        Quaternion upright = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -Wind * 0.08f);
        for (int i = 0; i < RainDrops; i++)
        {
            Node drop = _rain[i];
            if (i >= visible)
            {
                if (_rainShown[i])
                {
                    drop.Visible = false;
                    _rainShown[i] = false;
                }

                continue;
            }

            Vector3 p = _rainPos[i];
            p.Y -= 16f * dt;
            p.X += Wind * 2f * dt;
            if (p.Y < 0f)
            {
                p = new Vector3(Rand(-14, 14), 14f + Rand(0, 2), Rand(-14, 14));
            }

            _rainPos[i] = p;
            if (!_rainShown[i])
            {
                drop.Visible = true;
                _rainShown[i] = true;
            }
            drop.SetTransform(focus + new Vector3(p.X, p.Y - 2f, p.Z), upright, new Vector3(0.05f, 0.6f, 1f));
        }
    }
}
