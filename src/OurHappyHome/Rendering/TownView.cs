using System.Numerics;
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

        Material ground = _scene.CreateMaterial(_t.Grass.Options with { UvScale = new Vector2(70f, 70f) });
        Rect b = WorldMap.Bounds;
        _m.Ground(_root, new Vector3(b.Center.X, -0.02f, b.Center.Y), b.Size + new Vector2(400f, 400f), ground, "world-ground");

        foreach (TownFeature feature in map.Features)
        {
            Build(feature);
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
                _m.Ground(_root, new Vector3(c.X, 0.01f, c.Y), a.Size, _scene.CreateMaterial(_t.Sand.Options with { UvScale = a.Size / 8f }), "sand");
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
                    _m.Ground(_root, new Vector3(c.X, 0.05f, c.Y + 200f), new Vector2(a.Width + 400f, a.Depth + 400f), _t.Water, "ocean");
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
                    if (f.Label == "palm")
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
            case FeatureKind.Tent:
                {
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
