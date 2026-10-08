using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;
using ThreeNet;
using AColor = Avalonia.Media.Color;

namespace OurHappyHome.Rendering;

/// <summary>
/// Everything outside the family lot, built once from the shared
/// <see cref="WorldMap"/>: roads, neighbours' houses, school, downtown shops,
/// park, beach, forest, campsite, theme park, mountains and drifting clouds.
/// </summary>
public sealed class TownView
{
    private const int MaxStreetLights = 10;

    private readonly Scene _scene;
    private readonly Meshes _m;
    private readonly Textures _t;
    private readonly ModelLibrary _models;
    private readonly Effects _effects;
    private readonly Node _root;
    private readonly List<(Vector3 Position, Node Glow)> _lamps = [];
    private readonly Node[] _lampLights = new Node[MaxStreetLights];
    private readonly List<Node> _spinners = [];
    private readonly List<Node> _clouds = [];
    private readonly List<(Node Node, Vector2 Center)> _campfires = [];
    private readonly List<Node> _windowsAtNight = [];
    private Node? _campLight;
    private readonly Node _festival;
    private readonly List<(Node Node, float Phase)> _foam = [];
    private readonly List<(Node Node, Vector2 Center, float Radius, float Speed, float Height, float Phase)> _gulls = [];
    private float _fireflyTimer;
    private readonly List<(Node Node, float Offset)> _angkots = [];
    private int _butterflyTick;
    private readonly List<Node> _lanterns = [];
    private bool _festivalShown = true;
    private float _time;
    private bool _night;

    public TownView(Scene scene, Meshes meshes, Textures textures, ModelLibrary models, Effects effects, WorldMap map)
    {
        _scene = scene;
        _m = meshes;
        _t = textures;
        _models = models;
        _effects = effects;
        _root = scene.CreateNode(null, "town");
        _festival = scene.CreateNode(_root, "festival");

        // Large enough that its edge is never within the camera's 700 m view distance.
        Rect b = WorldMap.Bounds;
        Material wideGround = _scene.CreateMaterial(_t.Grass.Options with { UvScale = new Vector2(220f, 220f) });
        _m.Ground(_root, new Vector3(b.Center.X, -0.02f, b.Center.Y), b.Size + new Vector2(2200f, 2200f), wideGround, "world-ground");

        foreach (TownFeature feature in map.Features)
        {
            Build(feature);
        }

        BuildSeagulls();
        for (int i = 0; i < 2; i++)
        {
            Node angkot = _scene.CreateNode(_root, "angkot");
            Node body = angkot.CreateChild("angkot-body");
            if (_models.Height("angkot", body, 2.1f) is null)
            {
                _m.Block(body, 0f, 0f, 0.3f, new Vector3(1.7f, 1.6f, 4.2f), _t.Solid("#2E86C1", 0.4f));
            }

            angkot.SetShadowsRecursive(true, true);
            _angkots.Add((angkot, i * 0.5f));
        }

        for (int i = 0; i < MaxStreetLights; i++)
        {
            _lampLights[i] = _scene.AddLight(Light.Point(new Vector3(1f, 0.85f, 0.6f), 9f, 14f) with { Enabled = false }, _root, "street-light");
        }

        Random random = new(11);
        Material cloud = _t.Solid("#FFFFFF", 1f, emissive: 0.25f);
        for (int i = 0; i < 18; i++)
        {
            Node c = _scene.CreateNode(_root, "cloud");
            c.Position = new Vector3(random.Next(-280, 280), 55f + random.Next(0, 25), random.Next(-280, 300));
            for (int k = 0; k < 4; k++)
            {
                _m.Sphere(c, new Vector3((k - 1.5f) * 7f, random.Next(0, 4), random.Next(-3, 3)), new Vector3(14f, 7f, 10f), cloud, true).CastShadow = false;
            }

            _clouds.Add(c);
        }
    }

    // ------------------------------------------------------------- builders

    private void Build(TownFeature f)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        Material color = _t.Solid(f.Color, 0.7f);
        switch (f.Kind)
        {
            case FeatureKind.Road:
                {
                    Material asphalt = _scene.CreateMaterial(_t.Asphalt.Options with { UvScale = a.Size / 6f });
                    _m.Ground(_root, new Vector3(c.X, 0.015f, c.Y), a.Size, asphalt, "road");
                    bool alongX = a.Width > a.Depth;
                    float length = alongX ? a.Width : a.Depth;
                    Material paint = _t.Solid("#F2F0E6", 0.6f);
                    for (float s = 2f; s < length - 2f; s += 7f)
                    {
                        Vector3 p = alongX ? new Vector3(a.X0 + s, 0.025f, c.Y) : new Vector3(c.X, 0.025f, a.Z0 + s);
                        _m.Ground(_root, p, alongX ? new Vector2(3f, 0.18f) : new Vector2(0.18f, 3f), paint, "lane");
                    }

                    break;
                }

            case FeatureKind.Sidewalk:
                _m.Ground(_root, new Vector3(c.X, f.Height * 0.5f, c.Y), a.Size, _scene.CreateMaterial(_t.Paving.Options with { UvScale = a.Size / 2.5f }), "sidewalk");
                break;
            case FeatureKind.Field:
                _m.Ground(_root, new Vector3(c.X, 0.005f + (f.Height * 0.1f), c.Y), a.Size, _scene.CreateMaterial(_t.Lawn.Options with { UvScale = a.Size / 6f }), "field");
                break;
            case FeatureKind.Sand:
                _m.Ground(_root, new Vector3(c.X, 0.01f, c.Y), a.Size, _scene.CreateMaterial(_t.Sand.Options with { UvScale = a.Size / 8f, BaseColor = new Vector4(0.93f, 0.83f, 0.64f, 1f) }), "sand");
                break;
            case FeatureKind.Ocean:
            case FeatureKind.Pond:
                if (f.Kind == FeatureKind.Pond)
                {
                    Node edge = _scene.AddMesh(_m.UnitDisc, _t.Solid("#B9A27E", 0.9f), _root, "pond-edge");
                    edge.Position = new Vector3(c.X, 0.02f, c.Y);
                    edge.Scale = new Vector3(a.Width + 1.2f, 0.05f, a.Depth + 1.2f);
                    Node water = _scene.AddMesh(_m.UnitDisc, _t.Water, _root, "pond");
                    water.Position = new Vector3(c.X, 0.07f, c.Y);
                    water.Scale = new Vector3(a.Width, 0.02f, a.Depth);
                    water.CastShadow = false;
                }
                else
                {
                    // Deep, matt blue so the sea still reads as water at the low beach camera angle.
                    Material sea = _scene.CreateMaterial(_t.Water.Options with
                    {
                        BaseColor = new Vector4(0.05f, 0.36f, 0.55f, 1f),
                        Roughness = 0.45f,
                        Reflectance = 0.06f,
                        UvScale = new Vector2(120f, 60f),
                    });
                    _m.Ground(_root, new Vector3(c.X, 0.05f, a.Z0 + 760f), new Vector2(a.Width + 2400f, 1520f), sea, "ocean");
                }

                break;
            case FeatureKind.House:
                NeighbourHouse(f, color);
                break;
            case FeatureKind.School:
                Building(f, color, AColor.Parse("#2E6FBF"), floors: 2, awning: false);
                break;
            case FeatureKind.Shop:
                Building(f, color, AColor.Parse("#FFFFFF"), floors: f.Height > 9 ? 2 : 1, awning: true);
                break;
            case FeatureKind.Tree:
                {
                    Node tree = _scene.CreateNode(_root, "tree");
                    tree.Position = new Vector3(c.X, 0f, c.Y);
                    if (f.Label == "palm" && _models.Height("palm-tree", tree, f.Height, f.Yaw) is not null)
                    {
                        tree.SetShadowsRecursive(true, true);
                    }
                    else if (f.Label == "palm")
                    {
                        _m.Cylinder(tree, new Vector3(0f, f.Height / 2f, 0f), 0.18f, f.Height, _t.Solid("#9C7A54", 0.9f), true);
                        for (int i = 0; i < 6; i++)
                        {
                            Node leaf = _m.Box(tree, new Vector3(0f, f.Height, 0f), new Vector3(3.2f, 0.08f, 0.7f), _t.Solid("#3FA34D", 0.8f));
                            leaf.EulerAngles = new Vector3(0f, i * MathF.PI / 3f, -0.35f);
                        }
                    }
                    else
                    {
                        float s = a.Width / 3.5f;
                        tree.Scale = new Vector3(s);
                        FurnitureFactory.Tree(tree, _m, _t, (int)(c.X * 7 + c.Y * 3), _models);
                    }

                    break;
                }

            case FeatureKind.PineTree:
                {
                    Node tree = _scene.CreateNode(_root, "pine");
                    tree.Position = new Vector3(c.X, 0f, c.Y);
                    if (_models.Height("pine-tree", tree, f.Height, c.X * 0.37f) is not null)
                    {
                        tree.SetShadowsRecursive(true, true);
                        break;
                    }

                    float r = a.Width / 2f;
                    _m.Cylinder(tree, new Vector3(0f, 0.8f, 0f), 0.22f, 1.6f, _t.Solid("#6E4B2A", 0.9f), true);
                    _m.Cone(tree, new Vector3(0f, 1.2f, 0f), r, f.Height * 0.55f, color);
                    _m.Cone(tree, new Vector3(0f, 1.2f + (f.Height * 0.32f), 0f), r * 0.72f, f.Height * 0.5f, color);
                    break;
                }

            case FeatureKind.Bush:
                _m.Sphere(_root, new Vector3(c.X, f.Height * 0.45f, c.Y), new Vector3(a.Width, f.Height, a.Depth), color, true);
                break;
            case FeatureKind.Flowerbed:
                {
                    _m.Block(_root, c.X, c.Y, 0f, new Vector3(a.Width, 0.15f, a.Depth), _t.Dirt);
                    Random r = new((int)(c.X * 13 + c.Y));
                    for (int i = 0; i < 10; i++)
                    {
                        _m.Sphere(_root, new Vector3(c.X + (((float)r.NextDouble() - 0.5f) * a.Width * 0.85f), 0.25f, c.Y + (((float)r.NextDouble() - 0.5f) * a.Depth * 0.8f)), new Vector3(0.28f), color, true);
                    }

                    break;
                }

            case FeatureKind.Fence:
                {
                    bool alongX = a.Width > a.Depth;
                    float length = alongX ? a.Width : a.Depth;
                    Material picket = _t.Solid("#FFFFFF", 0.6f);
                    _m.Box(_root, new Vector3(c.X, 0.7f, c.Y), alongX ? new Vector3(length, 0.07f, 0.05f) : new Vector3(0.05f, 0.07f, length), picket);
                    _m.Box(_root, new Vector3(c.X, 0.35f, c.Y), alongX ? new Vector3(length, 0.07f, 0.05f) : new Vector3(0.05f, 0.07f, length), picket);
                    for (float s = 0f; s <= length; s += 1.5f)
                    {
                        Vector3 p = alongX ? new Vector3(a.X0 + s, 0.5f, c.Y) : new Vector3(c.X, 0.5f, a.Z0 + s);
                        _m.Box(_root, p, new Vector3(0.1f, 1.0f, 0.1f), picket);
                    }

                    break;
                }

            case FeatureKind.Bench:
                _m.Block(_root, c.X, c.Y, 0.4f, new Vector3(a.Width, 0.08f, a.Depth), _t.Solid("#A97C50", 0.8f));
                _m.Block(_root, c.X, c.Y - (a.Depth / 2f), 0.45f, new Vector3(a.Width, 0.45f, 0.06f), _t.Solid("#A97C50", 0.8f));
                _m.Block(_root, c.X - (a.Width / 2f) + 0.1f, c.Y, 0f, new Vector3(0.08f, 0.4f, a.Depth), _t.Solid("#3C3F46", 0.4f, 0.6f));
                _m.Block(_root, c.X + (a.Width / 2f) - 0.1f, c.Y, 0f, new Vector3(0.08f, 0.4f, a.Depth), _t.Solid("#3C3F46", 0.4f, 0.6f));
                break;
            case FeatureKind.LampPost:
                {
                    _m.Cylinder(_root, new Vector3(c.X, f.Height / 2f, c.Y), 0.07f, f.Height, _t.Solid("#3C3F46", 0.4f, 0.6f), true);
                    Node glow = _m.Sphere(_root, new Vector3(c.X, f.Height + 0.1f, c.Y), new Vector3(0.45f), _t.LampGlow, true);
                    glow.CastShadow = false;
                    _lamps.Add((new Vector3(c.X, f.Height - 0.2f, c.Y), glow));
                    break;
                }

            case FeatureKind.Playground:
                {
                    Material red = _t.Solid("#E5533D", 0.5f);
                    Material blue = _t.Solid(f.Color, 0.5f);
                    _m.Block(_root, c.X - (a.Width / 2f), c.Y, 0f, new Vector3(0.12f, 2.2f, 0.12f), red);
                    _m.Block(_root, c.X + (a.Width / 2f), c.Y, 0f, new Vector3(0.12f, 2.2f, 0.12f), red);
                    _m.Block(_root, c.X, c.Y, 2.2f, new Vector3(a.Width, 0.12f, 0.12f), blue);
                    _m.Block(_root, c.X + 1.5f, c.Y + 1.5f, 1.2f, new Vector3(1.4f, 0.1f, 1.4f), blue);
                    Node slide = _m.Box(_root, new Vector3(c.X + 1.5f, 0.65f, c.Y + 2.9f), new Vector3(0.8f, 0.06f, 2.6f), _t.Solid("#F7D547", 0.4f));
                    slide.EulerAngles = new Vector3(-0.5f, 0f, 0f);
                    _m.Block(_root, c.X - 1f, c.Y, 0.4f, new Vector3(0.6f, 0.06f, 0.3f), red);
                    break;
                }

            case FeatureKind.Rock:
                _m.Sphere(_root, new Vector3(c.X, f.Height * 0.3f, c.Y), new Vector3(a.Width, f.Height, a.Depth), color, true);
                break;
            case FeatureKind.Prop:
                {
                    Node prop = _scene.CreateNode(_root, f.Label);
                    prop.Position = new Vector3(c.X, 0f, c.Y);
                    if (_models.Height(f.Label, prop, f.Height, f.Yaw) is null && !ProceduralProp(prop, f))
                    {
                        _m.Block(prop, 0f, 0f, 0f, new Vector3(a.Width * 0.8f, f.Height, a.Depth * 0.8f), color);
                    }

                    prop.SetShadowsRecursive(true, true);
                    break;
                }

            case FeatureKind.Shore:
                Shore(f);
                break;
            case FeatureKind.Decal:
                {
                    Material decal = f.Label == "trail" ? _scene.CreateMaterial(_t.Dirt.Options with { UvScale = a.Size / 2f }) : color;
                    Node flat = _m.Ground(_root, new Vector3(c.X, f.Label == "trail" ? 0.045f : 0.05f, c.Y), a.Size, decal, f.Label);
                    flat.Rotation = Quaternion.CreateFromYawPitchRoll(f.Yaw, -MathF.PI / 2f, 0f);
                    flat.CastShadow = false;
                    if (f.Label == "towel")
                    {
                        Node stripe = _m.Ground(_root, new Vector3(c.X, 0.055f, c.Y), new Vector2(a.Width, a.Depth * 0.18f), _t.Solid("#FFFFFF", 0.9f), "towel-stripe");
                        stripe.Rotation = Quaternion.CreateFromYawPitchRoll(f.Yaw, -MathF.PI / 2f, 0f);
                        stripe.CastShadow = false;
                    }

                    break;
                }
            case FeatureKind.Tent:
                {
                    Node dome = _scene.CreateNode(_root, "tent");
                    dome.Position = new Vector3(c.X, 0f, c.Y);
                    if (_models.Height(f.Color == "#F4A259" ? "dome-tent" : "dome-tent-blue", dome, f.Height + 0.2f, c.X * 0.2f) is not null)
                    {
                        dome.SetShadowsRecursive(true, true);
                        break;
                    }

                    Node tent = _m.Cone(_root, new Vector3(c.X, 0f, c.Y), a.Width / 2f, f.Height, color, pyramid: true);
                    tent.EulerAngles = new Vector3(0f, MathF.PI / 4f, 0f);
                    _m.Box(_root, new Vector3(c.X, 0.5f, c.Y + (a.Width * 0.36f)), new Vector3(0.6f, 1f, 0.02f), _t.Solid("#3D2B1F", 0.9f));
                    break;
                }

            case FeatureKind.Campfire:
                {
                    Material log = _t.Solid("#6E4B2A", 0.9f);
                    for (int i = 0; i < 4; i++)
                    {
                        Node l = _m.Box(_root, new Vector3(c.X, 0.12f, c.Y), new Vector3(1.1f, 0.14f, 0.14f), log);
                        l.EulerAngles = new Vector3(0f, i * MathF.PI / 4f, 0f);
                    }

                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * MathF.Tau / 8f;
                        _m.Sphere(_root, new Vector3(c.X + (MathF.Cos(ang) * 0.75f), 0.1f, c.Y + (MathF.Sin(ang) * 0.75f)), new Vector3(0.3f, 0.2f, 0.3f), _t.Solid("#8A8F98", 0.9f), true);
                    }

                    _effects.AddEmitter(EffectKind.Fireworks, new Vector3(c.X, 0.3f, c.Y), 14f, 0.7f);
                    _campfires.Add((_root, c));
                    break;
                }

            case FeatureKind.FerrisWheel:
                {
                    Material frame = _t.Solid("#FFFFFF", 0.4f, 0.4f);
                    _m.Box(_root, new Vector3(c.X - 1.2f, f.Height / 2f, c.Y), new Vector3(0.3f, f.Height, 0.3f), frame).EulerAngles = new Vector3(0f, 0f, 0.15f);
                    _m.Box(_root, new Vector3(c.X + 1.2f, f.Height / 2f, c.Y), new Vector3(0.3f, f.Height, 0.3f), frame).EulerAngles = new Vector3(0f, 0f, -0.15f);
                    Node wheel = _scene.CreateNode(_root, "ferris-wheel");
                    wheel.Position = new Vector3(c.X, f.Height - 1f, c.Y);
                    wheel.EulerAngles = new Vector3(0f, MathF.PI / 2f, 0f);
                    Node spin = wheel.CreateChild("spin");
                    float radius = f.Height * 0.45f;
                    Node ring = _scene.AddMesh(_m.Torus, color, spin, "ring");
                    ring.Scale = new Vector3(radius, radius, 2f);
                    ring.EulerAngles = new Vector3(MathF.PI / 2f, 0f, 0f);
                    for (int i = 0; i < 12; i++)
                    {
                        float ang = i * MathF.Tau / 12f;
                        Node spoke = _m.Box(spin, Vector3.Zero, new Vector3(0.12f, radius * 2f, 0.12f), frame);
                        spoke.EulerAngles = new Vector3(0f, 0f, ang);
                        _m.Box(spin, new Vector3(MathF.Cos(ang) * radius, MathF.Sin(ang) * radius, 0f), new Vector3(1.2f, 1.1f, 1.2f), _t.Solid(i % 2 == 0 ? "#FFD166" : "#06D6A0", 0.5f));
                    }

                    _spinners.Add(spin);
                    break;
                }

            case FeatureKind.Carousel:
                {
                    Node carousel = _scene.CreateNode(_root, "carousel");
                    carousel.Position = new Vector3(c.X, 0f, c.Y);
                    Node spin = carousel.CreateChild("spin");
                    spin.Name = "carousel-spin";
                    _m.Cylinder(spin, new Vector3(0f, 0.2f, 0f), a.Width / 2f, 0.4f, _t.Solid("#EF476F", 0.5f));
                    _m.Cylinder(spin, new Vector3(0f, 2.2f, 0f), 0.25f, 4f, _t.Solid("#FFD166", 0.3f, 0.6f));
                    _m.Cone(spin, new Vector3(0f, 4.2f, 0f), a.Width / 2f + 0.3f, 1.8f, color);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * MathF.Tau / 8f;
                        float r = a.Width * 0.36f;
                        _m.Cylinder(spin, new Vector3(MathF.Cos(ang) * r, 2.2f, MathF.Sin(ang) * r), 0.05f, 3.6f, _t.Solid("#F2F2F2", 0.3f, 0.6f), true);
                        _m.Box(spin, new Vector3(MathF.Cos(ang) * r, 1.3f + (0.2f * (i % 2)), MathF.Sin(ang) * r), new Vector3(0.35f, 0.5f, 0.9f), _t.Solid(i % 2 == 0 ? "#FFFFFF" : "#8E7CE0", 0.5f)).EulerAngles = new Vector3(0f, -ang, 0f);
                    }

                    _spinners.Add(spin);
                    break;
                }

            case FeatureKind.Mountain:
                {
                    Random r = new((int)c.X);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = c.X + (((float)r.NextDouble() - 0.5f) * a.Width);
                        float h = f.Height * (0.6f + ((float)r.NextDouble() * 0.5f));
                        float rad = h * 0.9f;
                        _m.Cone(_root, new Vector3(x, -1f, c.Y + (((float)r.NextDouble() - 0.5f) * a.Depth)), rad, h, color).CastShadow = false;
                        _m.Cone(_root, new Vector3(x, h * 0.72f - 1f, c.Y), rad * 0.29f, h * 0.29f, _t.Solid("#F4F6F8", 0.8f)).CastShadow = false;
                    }

                    break;
                }

            case FeatureKind.ParkedCar:
                {
                    Node car = _scene.CreateNode(_root, "parked-car");
                    car.Position = new Vector3(c.X, 0f, c.Y);
                    car.EulerAngles = new Vector3(0f, MathF.PI / 2f, 0f);
                    if (_models.Fit("car", car, new Vector2(1.9f, 4f)) is null)
                    {
                        _m.Block(car, 0, 0, 0.3f, new Vector3(1.8f, 0.9f, 3.8f), color);
                    }

                    break;
                }

            case FeatureKind.Pier:
                {
                    Material plank = _t.Solid("#A57A52", 0.85f);
                    _m.Block(_root, c.X, c.Y, f.Height - 0.1f, new Vector3(a.Width, 0.15f, a.Depth), plank);
                    for (float z = a.Z0; z <= a.Z1; z += 3f)
                    {
                        _m.Block(_root, a.X0, z, -0.5f, new Vector3(0.2f, f.Height + 0.5f, 0.2f), plank);
                        _m.Block(_root, a.X1, z, -0.5f, new Vector3(0.2f, f.Height + 0.5f, 0.2f), plank);
                    }

                    break;
                }

            case FeatureKind.Umbrella:
                _m.Cylinder(_root, new Vector3(c.X, f.Height / 2f, c.Y), 0.04f, f.Height, _t.Solid("#FFFFFF", 0.4f), true);
                _m.Cone(_root, new Vector3(c.X, f.Height - 0.5f, c.Y), a.Width / 2f, 0.7f, color);
                _m.Block(_root, c.X + 1.2f, c.Y, 0f, new Vector3(0.7f, 0.1f, 1.7f), _t.Solid("#FFFFFF", 0.8f));
                break;
            case FeatureKind.Sign:
                {
                    Node sign = _scene.CreateNode(_root, "sign");
                    sign.Position = new Vector3(c.X, 0f, c.Y);
                    sign.EulerAngles = new Vector3(0f, f.Yaw, 0f);
                    float w = MathF.Max(a.Width, a.Depth);
                    _m.Block(sign, -w / 2f + 0.1f, 0f, 0f, new Vector3(0.12f, f.Height, 0.12f), _t.Solid("#3C3F46"));
                    _m.Block(sign, w / 2f - 0.1f, 0f, 0f, new Vector3(0.12f, f.Height, 0.12f), _t.Solid("#3C3F46"));
                    Material board = _t.Sign(f.Label, AColor.Parse(f.Color), AColor.Parse("#FFFFFF"));
                    _m.Box(sign, new Vector3(0f, f.Height, 0f), new Vector3(w, w / 4f, 0.08f), board);
                    break;
                }

            default:
                BuildInterior(f, color);
                break;
        }
    }

    /// <summary>Pieces of the enterable school, supermarket and clinic rooms.</summary>
    private void BuildInterior(TownFeature f, Material color)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        switch (f.Kind)
        {
            case FeatureKind.InteriorFloor:
                {
                    Material floor = f.Label switch { "tile" => _t.Tile, "checker" => _t.Checker, _ => _t.Wood };
                    _m.Ground(_root, new Vector3(c.X, 0.03f, c.Y), a.Size, _scene.CreateMaterial(floor.Options with { UvScale = a.Size / 2f }), "interior-floor");
                    break;
                }

            case FeatureKind.InteriorWall:
                _m.Block(_root, c.X, c.Y, 0f, new Vector3(a.Width, f.Height, a.Depth), color);
                _m.Block(_root, c.X, c.Y, f.Height, new Vector3(a.Width + 0.04f, 0.06f, a.Depth + 0.04f), _t.Solid("#FFFFFF", 0.5f));
                break;
            case FeatureKind.Door:
                {
                    // An open door frame, so it never hides whoever stands just inside.
                    _m.Block(_root, a.X0 + 0.06f, c.Y, 0f, new Vector3(0.12f, f.Height, a.Depth + 0.1f), color);
                    _m.Block(_root, a.X1 - 0.06f, c.Y, 0f, new Vector3(0.12f, f.Height, a.Depth + 0.1f), color);
                    _m.Block(_root, c.X, c.Y, f.Height - 0.12f, new Vector3(a.Width, 0.12f, a.Depth + 0.1f), color);
                    Node sign = _m.Box(_root, new Vector3(c.X, f.Height + 0.25f, c.Y), new Vector3(1.2f, 0.3f, 0.04f), _t.Sign(f.Label, AColor.Parse("#2E9E5B"), AColor.Parse("#FFFFFF")));
                    sign.CastShadow = false;
                    sign.EulerAngles = new Vector3(0f, MathF.PI, 0f);
                    _m.Ground(_root, new Vector3(c.X, 0.045f, c.Y - 0.8f), new Vector2(1.6f, 1f), _t.Solid("#B23A3A", 0.95f), "doormat");
                    break;
                }

            case FeatureKind.Shelf:
                {
                    Material wood = _t.Solid(f.Label == "fridge" ? "#E8F1F2" : "#B5835A", f.Label == "fridge" ? 0.3f : 0.8f);
                    _m.Block(_root, c.X, c.Y, 0f, new Vector3(a.Width, f.Height, a.Depth), wood);
                    bool alongX = a.Width >= a.Depth;
                    float length = alongX ? a.Width : a.Depth;
                    string[] goods = f.Label == "books" ? ["#C0392B", "#2E86C1", "#F1C40F", "#27AE60"] : ["#E4572E", "#F3A712", "#FFFFFF", "#669BBC", "#A8C686", f.Color];
                    Random r = new((int)(c.X * 31 + c.Y));
                    for (int level = 0; level < 3; level++)
                    {
                        float y = 0.25f + (level * (f.Height - 0.2f) / 3f);
                        for (float s = 0.25f; s < length - 0.2f; s += 0.32f)
                        {
                            float along = -length / 2f + s;
                            Vector3 size = new(0.22f, 0.18f + ((float)r.NextDouble() * 0.14f), 0.22f);
                            foreach (float side in new[] { -1f, 1f })
                            {
                                float off = side * ((alongX ? a.Depth : a.Width) / 2f + 0.06f);
                                Vector3 p = alongX ? new Vector3(c.X + along, y, c.Y + off) : new Vector3(c.X + off, y, c.Y + along);
                                _m.Block(_root, p.X, p.Z, p.Y, size, _t.Solid(goods[r.Next(goods.Length)], 0.6f)).CastShadow = false;
                            }
                        }
                    }

                    break;
                }

            case FeatureKind.Counter:
                _m.Block(_root, c.X, c.Y, 0f, new Vector3(a.Width, f.Height, a.Depth), color);
                _m.Block(_root, c.X, c.Y, f.Height, new Vector3(a.Width + 0.1f, 0.06f, a.Depth + 0.1f), _t.Solid("#F5F5F5", 0.3f));
                _m.Block(_root, c.X - (a.Width * 0.25f), c.Y, f.Height + 0.06f, new Vector3(0.45f, 0.3f, 0.35f), _t.Solid("#3C3F46", 0.4f));
                if (f.Label.Length > 0)
                {
                    Node label = _m.Box(_root, new Vector3(c.X, f.Height + 0.9f, c.Y), new Vector3(1.4f, 0.35f, 0.04f), _t.Sign(f.Label, AColor.Parse("#2E6FBF"), AColor.Parse("#FFFFFF")));
                    label.CastShadow = false;
                }

                break;
            case FeatureKind.Desk:
                {
                    bool big = f.Label == "teacher";
                    _m.Block(_root, c.X, c.Y, f.Height - 0.05f, new Vector3(a.Width, 0.05f, a.Depth), color);
                    foreach ((float x, float z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                    {
                        _m.Block(_root, c.X + (x * ((a.Width / 2f) - 0.05f)), c.Y + (z * ((a.Depth / 2f) - 0.05f)), 0f, new Vector3(0.05f, f.Height - 0.05f, 0.05f), _t.Solid("#5A4632"));
                    }

                    // Chair behind the desk (the pupil side faces the board, the teacher faces the class).
                    float chairZ = big ? -(a.Depth / 2f) - 0.35f : (a.Depth / 2f) + 0.3f;
                    _m.Block(_root, c.X, c.Y + chairZ, 0.4f, new Vector3(0.4f, 0.05f, 0.4f), _t.Solid("#3D8FE5", 0.6f));
                    _m.Block(_root, c.X, c.Y + chairZ + (big ? -0.2f : 0.2f), 0.4f, new Vector3(0.4f, 0.45f, 0.05f), _t.Solid("#3D8FE5", 0.6f));
                    _m.Block(_root, c.X, c.Y + chairZ, 0f, new Vector3(0.05f, 0.4f, 0.05f), _t.Solid("#5A4632"));
                    _m.Block(_root, c.X - 0.1f, c.Y, f.Height, new Vector3(0.3f, 0.03f, 0.22f), _t.Solid(big ? "#F1C40F" : "#FFFFFF", 0.8f));
                    break;
                }

            case FeatureKind.Board:
                {
                    float w = a.Width;
                    _m.Block(_root, c.X - (w / 2f) + 0.1f, c.Y, 0f, new Vector3(0.1f, f.Height, 0.1f), _t.Solid("#5A4632"));
                    _m.Block(_root, c.X + (w / 2f) - 0.1f, c.Y, 0f, new Vector3(0.1f, f.Height, 0.1f), _t.Solid("#5A4632"));
                    _m.Box(_root, new Vector3(c.X, f.Height - 0.6f, c.Y), new Vector3(w, 1.2f, 0.08f), _t.Sign(f.Label, AColor.Parse(f.Color), AColor.Parse("#FFFFFF")));
                    break;
                }

            case FeatureKind.FestivalStall:
                FestivalStall(f, color);
                break;
            case FeatureKind.Inn:
                NeighbourHouse(f, color);
                {
                    Node sign = _m.Box(_root, new Vector3(c.X, f.Height + 1.9f, a.Z0 - 0.4f), new Vector3(4.5f, 0.9f, 0.08f), _t.Sign(f.Label, AColor.Parse("#2A9D8F"), AColor.Parse("#FFFFFF")));
                    sign.EulerAngles = new Vector3(0f, MathF.PI, 0f);
                    sign.CastShadow = false;
                }

                break;
            case FeatureKind.FestivalStage:
                FestivalGrounds(f);
                break;
            case FeatureKind.ClinicBed:
                _m.Block(_root, c.X, c.Y, 0f, new Vector3(a.Width, f.Height - 0.15f, a.Depth), _t.Solid("#B8C4CC", 0.4f, 0.5f));
                _m.Block(_root, c.X, c.Y, f.Height - 0.15f, new Vector3(a.Width - 0.05f, 0.15f, a.Depth - 0.05f), color);
                _m.Block(_root, c.X, c.Y - (a.Depth / 2f) + 0.25f, f.Height, new Vector3(a.Width * 0.7f, 0.1f, 0.35f), _t.Solid("#DDEBF7", 0.8f));
                _m.Block(_root, c.X, c.Y + 0.25f, f.Height - 0.02f, new Vector3(a.Width - 0.02f, 0.04f, a.Depth * 0.55f), _t.Solid("#9BD1E5", 0.9f));
                break;
            case FeatureKind.Plant:
                _m.Cylinder(_root, new Vector3(c.X, 0.2f, c.Y), a.Width * 0.35f, 0.4f, _t.Solid("#C2703D", 0.8f), true);
                _m.Sphere(_root, new Vector3(c.X, 0.4f + ((f.Height - 0.4f) * 0.5f), c.Y), new Vector3(a.Width, f.Height - 0.4f, a.Depth), color, true);
                break;
        }
    }

    /// <summary>Small beach props built from primitives: sandcastles, balls, surfboards and a volleyball net.</summary>
    private bool ProceduralProp(Node prop, TownFeature f)
    {
        float h = f.Height;
        prop.EulerAngles = new Vector3(0f, f.Yaw, 0f);
        switch (f.Label)
        {
            case "sandcastle":
                {
                    Material sand = _t.Solid("#E3C98F", 0.95f);
                    _m.Block(prop, 0f, 0f, 0f, new Vector3(h * 1.4f, h * 0.35f, h * 1.4f), sand);
                    foreach ((float x, float z) in new[] { (-0.45f, -0.45f), (0.45f, -0.45f), (-0.45f, 0.45f), (0.45f, 0.45f) })
                    {
                        _m.Cylinder(prop, new Vector3(x * h, h * 0.55f, z * h), h * 0.18f, h * 0.45f, sand, true);
                        _m.Cone(prop, new Vector3(x * h, h * 0.78f, z * h), h * 0.2f, h * 0.22f, sand, pyramid: true);
                    }

                    _m.Cylinder(prop, new Vector3(0f, h * 0.6f, 0f), h * 0.28f, h * 0.55f, sand, true);
                    _m.Cone(prop, new Vector3(0f, h * 0.88f, 0f), h * 0.3f, h * 0.25f, sand);
                    _m.Box(prop, new Vector3(0.08f, h * 1.2f, 0f), new Vector3(0.18f, 0.12f, 0.01f), _t.Solid("#E63946", 0.6f));
                    return true;
                }

            case "beach-ball":
                {
                    string[] colors = ["#E63946", "#FFFFFF", "#4A90D9", "#FFD23F"];
                    for (int i = 0; i < 4; i++)
                    {
                        Node slice = _m.Sphere(prop, new Vector3(0f, h * 0.5f, 0f), new Vector3(h * (1f - (i * 0.02f)), h, h * (0.25f + (i * 0.25f))), _t.Solid(colors[i], 0.4f), true);
                        slice.EulerAngles = new Vector3(0f, i * 0.8f, 0f);
                    }

                    return true;
                }

            case "surfboard":
                {
                    Node board = _m.Sphere(prop, new Vector3(0f, h * 0.48f, 0f), new Vector3(0.55f, h, 0.1f), _t.Solid(f.Area.Center.X % 2 < 1 ? "#FF7B54" : "#2A9D8F", 0.35f), true);
                    board.EulerAngles = new Vector3(0.08f, 0f, 0f);
                    _m.Box(prop, new Vector3(0f, h * 0.5f, 0.05f), new Vector3(0.06f, h * 0.8f, 0.01f), _t.Solid("#FFFFFF", 0.4f));
                    return true;
                }

            case "volleyball":
                {
                    float w = f.Area.Width;
                    Material pole = _t.Solid("#DDDDDD", 0.3f, 0.5f);
                    _m.Cylinder(prop, new Vector3(-w / 2f, h / 2f, 0f), 0.05f, h, pole, true);
                    _m.Cylinder(prop, new Vector3(w / 2f, h / 2f, 0f), 0.05f, h, pole, true);
                    Node net = _m.Box(prop, new Vector3(0f, h - 0.45f, 0f), new Vector3(w, 0.8f, 0.02f), _t.Solid("#F5F5F5", 0.9f));
                    net.CastShadow = false;
                    _m.Box(prop, new Vector3(0f, h - 0.04f, 0f), new Vector3(w, 0.07f, 0.04f), _t.Solid("#2E6FBF", 0.6f));
                    return true;
                }

            default:
                return false;
        }
    }

    /// <summary>Wet sand, a turquoise shallow band and foam lines that wash in and out.</summary>
    private void Shore(TownFeature f)
    {
        Rect a = f.Area;
        float shoreline = 300f;
        _m.Ground(_root, new Vector3(a.Center.X, 0.025f, (a.Z0 + shoreline) / 2f), new Vector2(a.Width, shoreline - a.Z0), _t.Solid("#D9BF86", 0.95f), "wet-sand");
        Material shallow = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.3f, 0.78f, 0.82f, 0.85f), 0f, 0.2f) with
        {
            AlphaMode = AlphaMode.Blend,
            DepthWrite = false,
            NormalMap = _t.Water.Options.NormalMap,
            NormalScale = 0.4f,
            UvScale = new Vector2(80f, 2f),
        });
        Node band = _m.Ground(_root, new Vector3(a.Center.X, 0.07f, (shoreline + a.Z1) / 2f + 1f), new Vector2(a.Width, a.Z1 - shoreline + 2f), shallow, "shallow-water");
        band.CastShadow = false;
        Material foam = _t.Solid("#FFFFFF", 0.6f, emissive: 0.15f);
        for (int i = 0; i < 3; i++)
        {
            Node line = _m.Ground(_root, new Vector3(a.Center.X, 0.08f + (i * 0.002f), shoreline + i), new Vector2(a.Width, 0.35f - (i * 0.08f)), foam, "foam");
            line.CastShadow = false;
            _foam.Add((line, i * 2.1f));
        }
    }

    /// <summary>
    /// Where an angkot is along its loop (0-1): eastbound along z = 18.8, then
    /// westbound along z = 21.2, easing to a stop for a few seconds at each halte.
    /// </summary>
    private static (Vector3 Position, float Yaw) AngkotPose(float t)
    {
        const float west = -250f;
        const float east = 228f;
        bool eastbound = t < 0.5f;
        float u = eastbound ? t * 2f : (t - 0.5f) * 2f;
        float x = eastbound ? float.Lerp(west, east, u) : float.Lerp(east, west, u);

        // Linger near a bus stop on this side of the road.
        foreach ((float hx, float hz, float _) in WorldMap.HalteSpots)
        {
            bool sameSide = eastbound ? hz < 20f : hz > 20f;
            float d = x - hx;
            if (sameSide && MathF.Abs(d) < 14f)
            {
                x = hx + (MathF.Sign(d) * MathF.Pow(MathF.Abs(d) / 14f, 2.2f) * 14f);
            }
        }

        return (new Vector3(x, 0f, eastbound ? 18.8f : 21.2f), eastbound ? MathF.PI / 2f : -MathF.PI / 2f);
    }

    /// <summary>Seagulls circling over the beach and the pier.</summary>
    private void BuildSeagulls()
    {
        Random r = new(17);
        for (int i = 0; i < 7; i++)
        {
            Node gull = _scene.CreateNode(_root, "gull");
            if (_models.Height("seagull", gull, 0.7f) is null)
            {
                _m.Sphere(gull, Vector3.Zero, new Vector3(0.9f, 0.15f, 0.35f), _t.Solid("#FFFFFF"), true);
            }

            gull.SetShadowsRecursive(false, false);
            Vector2 center = new(10f + (float)(r.NextDouble() * 120f), 285f + (float)(r.NextDouble() * 25f));
            _gulls.Add((gull, center, 8f + (float)(r.NextDouble() * 18f), 0.25f + (float)(r.NextDouble() * 0.25f), 8f + (float)(r.NextDouble() * 7f), (float)(r.NextDouble() * MathF.Tau)));
        }
    }

    private void FestivalStall(TownFeature f, Material awning)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        Node stall = _scene.CreateNode(_festival, "stall");
        stall.Position = new Vector3(c.X, 0f, c.Y);
        Material wood = _t.Solid("#A0703F", 0.8f);
        _m.Block(stall, 0f, 0f, 0f, new Vector3(a.Width, 0.95f, a.Depth * 0.6f), _t.Solid("#F5EBDD", 0.7f));
        foreach ((float x, float z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            _m.Block(stall, x * ((a.Width / 2f) - 0.08f), z * ((a.Depth / 2f) - 0.08f), 0f, new Vector3(0.1f, f.Height, 0.1f), wood);
        }

        // Striped awning.
        for (int i = 0; i < 6; i++)
        {
            Material stripe = i % 2 == 0 ? awning : _t.Solid("#FFFFFF", 0.7f);
            Node strip = _m.Box(stall, new Vector3((-a.Width / 2f) + ((i + 0.5f) * a.Width / 6f), f.Height + 0.1f, 0f), new Vector3(a.Width / 6f, 0.06f, a.Depth + 0.4f), stripe);
            strip.EulerAngles = new Vector3(0.12f, 0f, 0f);
        }

        string sign = f.Label switch
        {
            "kerupuk" => Loc.T("Lomba Kerupuk", "Cracker Contest"),
            "tug" => Loc.T("Tarik Tambang", "Tug of War"),
            "food" => Loc.T("Kerak Telor", "Street Food"),
            _ => Loc.T("Mainan & Hadiah", "Toys & Prizes"),
        };
        Node board = _m.Box(stall, new Vector3(0f, f.Height + 0.5f, (a.Depth / 2f) + 0.2f), new Vector3(a.Width * 0.8f, 0.45f, 0.05f), _t.Sign(sign, AColor.Parse(f.Color), AColor.Parse("#FFFFFF")));
        board.CastShadow = false;
        string goods = f.Label switch { "kerupuk" => "#F2D7A0", "food" => "#E9C46A", "toys" => "#FF5DA2", _ => "#C49A6C" };
        for (int i = 0; i < 5; i++)
        {
            _m.Sphere(stall, new Vector3(-1f + (i * 0.5f), 1.05f, 0.1f), new Vector3(0.25f, 0.15f, 0.25f), _t.Solid(goods, 0.6f), true);
        }
    }

    /// <summary>Contest ground with rope and flags, bunting over the lawn and paper lanterns.</summary>
    private void FestivalGrounds(TownFeature f)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        _m.Ground(_festival, new Vector3(c.X, 0.04f, c.Y), a.Size, _t.Solid("#D9C9A3", 0.95f), "festival-ground");
        Node rope = _m.Cylinder(_festival, new Vector3(c.X, 0.12f, c.Y), 0.04f, a.Width - 1f, _t.Solid("#C8A165", 0.9f), true);
        rope.EulerAngles = new Vector3(0f, 0f, MathF.PI / 2f);
        Node mid = _m.Box(_festival, new Vector3(c.X, 0.14f, c.Y), new Vector3(0.06f, 0.1f, 0.4f), _t.Solid("#E63946", 0.5f));
        mid.CastShadow = false;

        // Red and white flags on poles around the ground.
        for (int i = 0; i < 6; i++)
        {
            float x = c.X - 10f + (i * 4f);
            float z = c.Y - 5f;
            _m.Cylinder(_festival, new Vector3(x, 1.6f, z), 0.04f, 3.2f, _t.Solid("#DDDDDD", 0.3f, 0.6f), true);
            _m.Box(_festival, new Vector3(x + 0.4f, 2.95f, z), new Vector3(0.75f, 0.25f, 0.03f), _t.Solid("#E63946", 0.6f)).CastShadow = false;
            _m.Box(_festival, new Vector3(x + 0.4f, 2.7f, z), new Vector3(0.75f, 0.25f, 0.03f), _t.Solid("#FFFFFF", 0.6f)).CastShadow = false;
        }

        // Bunting: little triangles on strings between poles, over the stalls.
        string[] colors = ["#E63946", "#FFFFFF", "#F4A261", "#2A9D8F", "#FFD23F"];
        for (int line = 0; line < 2; line++)
        {
            float z = 43.2f + (line * 3.2f);
            for (int i = 0; i < 28; i++)
            {
                float x = -54f + (i * 0.75f);
                float sag = 0.45f * MathF.Sin(MathF.PI * (i % 14) / 14f);
                Node flag = _m.Cone(_festival, new Vector3(x, 3.6f - sag, z), 0.16f, 0.32f, _t.Solid(colors[i % colors.Length], 0.6f), pyramid: true);
                flag.EulerAngles = new Vector3(MathF.PI, 0f, 0f);
                flag.CastShadow = false;
            }
        }

        // Paper lanterns that glow at night.
        for (int i = 0; i < 10; i++)
        {
            Node lantern = _m.Sphere(_festival, new Vector3(-53f + (i * 2.2f), 3.2f, 41.8f), new Vector3(0.35f, 0.45f, 0.35f), _t.Solid(i % 2 == 0 ? "#FF7B54" : "#FFD23F", 0.5f, emissive: 0.2f), true);
            lantern.CastShadow = false;
            _lanterns.Add(lantern);
        }
    }

    /// <summary>Shows the festival decorations only on festival days.</summary>
    public void ShowFestival(bool shown)
    {
        if (shown != _festivalShown)
        {
            _festivalShown = shown;
            _festival.Visible = shown;
        }
    }

    private void NeighbourHouse(TownFeature f, Material wall)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        float h = f.Height;
        Node house = _scene.CreateNode(_root, "neighbour");
        house.Position = new Vector3(c.X, 0f, c.Y);
        house.EulerAngles = new Vector3(0f, f.Yaw, 0f);
        _m.Block(house, 0f, 0f, 0f, new Vector3(a.Width, h, a.Depth), wall);
        Material roof = _t.Solid(new[] { "#B84A39", "#4A6FA5", "#5C8D5A", "#8E5B3E" }[(int)MathF.Abs(c.X) % 4], 0.7f);
        _m.Roof(house, Vector2.Zero, new Vector2(a.Width, a.Depth), h, a.Depth * 0.32f, 0.5f, roof, wall);
        Material door = _t.Solid("#6E4B2A", 0.6f);
        _m.Block(house, -a.Width * 0.2f, a.Depth / 2f + 0.03f, 0f, new Vector3(1f, 2.1f, 0.06f), door);
        for (int i = 0; i < 2; i++)
        {
            Node w = _m.Block(house, (i == 0 ? 0.15f : 0.35f) * a.Width, a.Depth / 2f + 0.03f, 1f, new Vector3(1.2f, 1.1f, 0.05f), _t.WindowGlow);
            w.CastShadow = false;
            _windowsAtNight.Add(w);
        }

        _m.Block(house, -a.Width * 0.2f, a.Depth / 2f + 1.6f, 0f, new Vector3(1.4f, 0.1f, 2.6f), _t.Paving);
    }

    private void Building(TownFeature f, Material wall, AColor signColor, int floors, bool awning)
    {
        Rect a = f.Area;
        Vector2 c = a.Center;
        float h = f.Height;
        Node b = _scene.CreateNode(_root, "building");
        b.Position = new Vector3(c.X, 0f, c.Y);
        _m.Block(b, 0f, 0f, 0f, new Vector3(a.Width, h, a.Depth), wall);
        _m.Block(b, 0f, 0f, h, new Vector3(a.Width + 0.3f, 0.3f, a.Depth + 0.3f), _t.Solid("#EDE7DB", 0.7f));

        // Windows on the front (+Z) face.
        float front = a.Depth / 2f + 0.03f;
        int columns = Math.Max(2, (int)(a.Width / 3.2f));
        for (int floor = 0; floor < floors; floor++)
        {
            for (int i = 0; i < columns; i++)
            {
                float x = -a.Width / 2f + ((i + 0.5f) * a.Width / columns);
                if (floor == 0 && i == columns / 2)
                {
                    _m.Block(b, x, front, 0f, new Vector3(2.2f, 2.6f, 0.06f), _t.Glass);
                    continue;
                }

                Node w = _m.Block(b, x, front, 1f + (floor * 3.5f), new Vector3(1.6f, 1.6f, 0.05f), _t.WindowGlow);
                w.CastShadow = false;
                _windowsAtNight.Add(w);
            }
        }

        if (awning)
        {
            Node aw = _m.Box(b, new Vector3(0f, 3f, front + 0.8f), new Vector3(a.Width * 0.8f, 0.12f, 1.8f), _t.Solid(f.Color, 0.6f));
            aw.EulerAngles = new Vector3(0.25f, 0f, 0f);
        }

        Material sign = _t.Sign(f.Label, signColor == AColor.Parse("#FFFFFF") ? AColor.Parse("#2B2D42") : signColor, AColor.Parse("#FFFFFF"));
        _m.Box(b, new Vector3(0f, h - 1.2f, front + 0.05f), new Vector3(MathF.Min(a.Width * 0.7f, 14f), MathF.Min(a.Width * 0.7f, 14f) / 4f, 0.1f), sign);
    }

    // --------------------------------------------------------------- update

    public void Update(float dt, Vector3 cameraPosition, bool night, float windStrength)
    {
        _time += dt;
        foreach (Node spinner in _spinners)
        {
            Vector3 e = spinner.EulerAngles;
            spinner.EulerAngles = spinner.Name == "carousel-spin" ? new Vector3(0f, e.Y + (dt * 0.6f), 0f) : new Vector3(0f, 0f, e.Z + (dt * 0.15f));
        }

        foreach (Node cloud in _clouds)
        {
            Vector3 p = cloud.Position;
            p.X += dt * (1.5f + (windStrength * 6f));
            if (p.X > 320f)
            {
                p.X = -320f;
            }

            cloud.Position = p;
        }

        _t.Water.Update(o => o with { UvOffset = new Vector2(_time * 0.01f, _time * 0.006f) });

        // Angkots drive the main street east and back west, pausing at the bus stops.
        foreach ((Node angkot, float offset) in _angkots)
        {
            (Vector3 pos, float yaw) = AngkotPose(((_time / 110f) + offset) % 1f);
            angkot.SetTransform(pos, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw), Vector3.One);
        }

        // Waves wash up the beach and back.
        foreach ((Node line, float phase) in _foam)
        {
            Vector3 p = line.Position;
            line.Position = new Vector3(p.X, p.Y, 300.6f + (2.2f * MathF.Sin((_time * 0.7f) + phase)));
        }

        foreach ((Node gull, Vector2 center, float radius, float speed, float height, float phase) in _gulls)
        {
            float a = phase + (_time * speed);
            Vector3 pos = new(center.X + (MathF.Cos(a) * radius), height + MathF.Sin(_time * 1.3f + phase), center.Y + (MathF.Sin(a) * radius));
            gull.Position = pos;
            gull.EulerAngles = new Vector3(0f, -a, 0.35f * MathF.Sin(_time * 2f + phase));
        }

        // Butterflies over the campsite meadow and the park by day.
        if (!night)
        {
            Vector2 cam = new(cameraPosition.X, cameraPosition.Z);
            foreach (Vector2 meadow in new[] { new Vector2(-24f, -232f), new Vector2(-36f, 48f) })
            {
                if (Vector2.Distance(cam, meadow) < 70f && ((int)(_time * 10f) % 9) == 0 && _butterflyTick != (int)(_time * 10f))
                {
                    _butterflyTick = (int)(_time * 10f);
                    float ang = _time * 7.1f;
                    float r = 3f + ((_time * 5.3f) % 18f);
                    _effects.Butterfly(new Vector3(meadow.X + (MathF.Cos(ang) * r), 0.4f + ((_time * 1.7f) % 1.2f), meadow.Y + (MathF.Sin(ang) * r)));
                }
            }
        }

        // Fireflies drift around the campsite after dark.
        Vector2 camp = new(-24f, -232f);
        if (night && Vector2.Distance(new Vector2(cameraPosition.X, cameraPosition.Z), camp) < 90f)
        {
            _fireflyTimer -= dt;
            if (_fireflyTimer <= 0f)
            {
                _fireflyTimer = 0.12f;
                float ang = _time * 13.7f;
                float r = 4f + ((_time * 7.3f) % 22f);
                _effects.Firefly(new Vector3(camp.X + (MathF.Cos(ang) * r), 0.5f + ((_time * 3.1f) % 2f), camp.Y + (MathF.Sin(ang) * r)));
            }
        }
        _t.PoolWater.Update(o => o with { UvOffset = new Vector2(_time * 0.03f, _time * 0.02f) });

        // Only the street lamps nearest the camera get real lights.
        if (night != _night || ((int)(_time * 2) % 2 == 0))
        {
            _night = night;
            Vector2 cam = new(cameraPosition.X, cameraPosition.Z);
            List<(Vector3 Position, Node Glow)> nearest = night
                ? [.. _lamps.OrderBy(l => Vector2.DistanceSquared(new Vector2(l.Position.X, l.Position.Z), cam)).Take(MaxStreetLights)]
                : [];
            for (int i = 0; i < MaxStreetLights; i++)
            {
                Node light = _lampLights[i];
                if (i < nearest.Count)
                {
                    light.Position = nearest[i].Position;
                    if (light.Light is { Enabled: false } l)
                    {
                        light.Light = l with { Enabled = true };
                    }
                }
                else if (light.Light is { Enabled: true } l)
                {
                    light.Light = l with { Enabled = false };
                }
            }
        }

        // Campfire light near the camera.
        if (_campfires.Count > 0)
        {
            Vector2 fire = _campfires[0].Center;
            float distance = Vector2.Distance(fire, new Vector2(cameraPosition.X, cameraPosition.Z));
            if (distance < 60f)
            {
                _campLight ??= _scene.AddLight(Light.Point(new Vector3(1f, 0.6f, 0.25f), 12f, 12f), _root, "campfire-light");
                _campLight.Position = new Vector3(fire.X, 1.2f, fire.Y);
                _campLight.Light = Light.Point(new Vector3(1f, 0.6f, 0.25f), 10f + (3f * MathF.Sin(_time * 17f)), 12f);
            }
            else if (_campLight?.Light is { Enabled: true } l)
            {
                _campLight.Light = l with { Enabled = false };
            }
        }
    }
}
