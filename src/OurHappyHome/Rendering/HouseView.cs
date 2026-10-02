using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;
using ThreeNet;

namespace OurHappyHome.Rendering;

/// <summary>
/// The family home in 3D, rebuilt from <see cref="House"/> whenever rooms are
/// added or repainted: floors, two-sided walls with windows, door frames and
/// a swinging front door, gable roofs, room lights, furniture and hazards.
/// Walls on the camera's side drop down (Sims-style cutaway) so the family
/// stays visible indoors.
/// </summary>
public sealed class HouseView
{
    private const float WallHeight = WallSegment.Height;

    private sealed class WallVisual
    {
        public Node Pivot = null!;
        public WallSegment Segment = null!;
        public Node? Glass;
        public RoomId? GlowRoom;
        public bool Lit;
        public float Scale = 1f;
    }

    private sealed class DoorVisual
    {
        public Node Hinge = null!;
        public Vector2 Center;
        public float Angle;
        public float BaseYaw;
        public bool Garage;
    }

    private sealed class FurnitureVisual
    {
        public Node Node = null!;
        public Vector2 Position;
        public int Rotation;
        public bool Broken;
        public Node? BrokenIcon;
        public Node? Light;
    }

    private sealed class HazardVisual
    {
        public Node Node = null!;
        public Effects.Emitter? Emitter;
        public Node? Light;
        public HazardKind Kind;
    }

    private readonly Scene _scene;
    private readonly Meshes _m;
    private readonly Textures _t;
    private readonly ModelLibrary _models;
    private readonly Effects _effects;
    private readonly Node _root;
    private Node _structure;
    private readonly Node _furnitureRoot;
    private readonly Node _hazardRoot;
    private readonly List<WallVisual> _walls = [];
    private readonly List<DoorVisual> _doors = [];
    private readonly List<Node> _roofs = [];
    private readonly Dictionary<RoomId, Node> _roomLights = [];
    private readonly Dictionary<int, FurnitureVisual> _furniture = [];
    private readonly Dictionary<int, HazardVisual> _hazards = [];
    private int _revision = -1;
    private int _furnitureRevision = -1;
    private int _brokenWindowCount = -1;
    private float _time;

    public HouseView(Scene scene, Meshes meshes, Textures textures, ModelLibrary models, Effects effects)
    {
        _scene = scene;
        _m = meshes;
        _t = textures;
        _models = models;
        _effects = effects;
        _root = scene.CreateNode(null, "house");
        _structure = scene.CreateNode(_root, "structure");
        _furnitureRoot = scene.CreateNode(_root, "furniture");
        _hazardRoot = scene.CreateNode(_root, "hazards");

        // The lot lawn sits just above the town ground.
        Rect lot = Rooms.Lot;
        _m.Ground(_root, new Vector3(lot.Center.X, 0.01f, lot.Center.Y), lot.Size, LawnFor(lot.Size), "lot-lawn");
    }

    private Material LawnFor(Vector2 size) => _scene.CreateMaterial(_t.Lawn.Options with { UvScale = size / 4f });

    /// <summary>Roofs and cutaway walls are hidden while the player is inside or building.</summary>
    public bool Cutaway { get; set; }

    public bool BuildMode { get; set; }

    // --------------------------------------------------------------- sync

    public void Sync(GameSession session, Vector3 cameraPosition, float dt)
    {
        House house = session.State.House;
        _time += dt;
        if (house.Revision != _revision || house.BrokenWindows.Count != _brokenWindowCount)
        {
            RebuildStructure(house);
            _revision = house.Revision;
            _brokenWindowCount = house.BrokenWindows.Count;
        }

        if (house.FurnitureRevision != _furnitureRevision)
        {
            SyncFurniture(house);
            _furnitureRevision = house.FurnitureRevision;
        }

        SyncBroken(house);
        SyncHazards(house, dt);
        UpdateCutaway(session, cameraPosition, dt);
        UpdateDoors(session, dt);
        UpdateLights(session);
    }

    // ---------------------------------------------------------- structure

    private void RebuildStructure(House house)
    {
        _structure.Remove();
        _structure = _scene.CreateNode(_root, "structure");
        _walls.Clear();
        _doors.Clear();
        _roofs.Clear();
        _roomLights.Clear();

        Material foundation = _t.Solid("#D9D2C5", 0.9f);
        foreach (RoomDef room in house.IndoorRooms)
        {
            Rect a = room.Area;
            _m.Block(_structure, a.Center.X, a.Center.Y, -0.15f, new Vector3(a.Width + 0.3f, 0.17f, a.Depth + 0.3f), foundation, "foundation").CastShadow = false;
            Material floor = _scene.CreateMaterial(_t.Floor(house.Floor(room.Id)).Options with { UvScale = a.Size / (house.Floor(room.Id) == FloorStyle.Checker ? 1.2f : 1.6f) });
            _m.Ground(_structure, new Vector3(a.Center.X, 0.025f, a.Center.Y), a.Size, floor, $"floor-{room.Id}");

            Node light = _scene.AddLight(Light.Point(new Vector3(1f, 0.82f, 0.62f), 7f, MathF.Max(a.Width, a.Depth) + 2.5f), _structure, $"light-{room.Id}");
            light.Position = new Vector3(a.Center.X, 2.45f, a.Center.Y);
            _roomLights[room.Id] = light;
        }

        foreach (WallSegment wall in house.Walls)
        {
            _walls.Add(BuildWall(house, wall));
        }

        foreach (DoorOpening door in house.Doors)
        {
            BuildDoor(door);
        }

        // One gable roof per room: a little village of roofs that grows with the house.
        foreach (RoomDef room in house.IndoorRooms)
        {
            Rect a = room.Area;
            float rise = MathF.Min(a.Width, a.Depth) * 0.36f;
            Node roof = _m.Roof(_structure, a.Center, a.Size, WallHeight, rise, 0.35f, _t.RoofTile, _t.Siding);
            _roofs.Add(roof);
        }

        BuildOutdoorRooms(house);
    }

    private WallVisual BuildWall(House house, WallSegment wall)
    {
        Node pivot = _scene.CreateNode(_structure, "wall");
        pivot.Position = new Vector3(wall.Center.X, 0f, wall.Center.Y);
        pivot.EulerAngles = new Vector3(0f, wall.Horizontal ? 0f : MathF.PI / 2f, 0f);
        float length = wall.Length;
        float half = WallSegment.Thickness / 2f;
        WallVisual visual = new() { Pivot = pivot, Segment = wall };

        foreach ((RoomId? room, float side) in new[] { (wall.NegativeSide, -1f), (wall.PositiveSide, 1f) })
        {
            Material material = room is { } r ? _t.Paint(house.Paint(r)) : _t.Siding;
            float z = side * half / 2f;
            if (wall.WindowWidth <= 0f)
            {
                _m.Box(pivot, new Vector3(0f, WallHeight / 2f, z), new Vector3(length, WallHeight, half), material);
                continue;
            }

            float w = wall.WindowWidth;
            float sideLength = (length - w) / 2f;
            _m.Box(pivot, new Vector3(0f, 0.45f, z), new Vector3(length, 0.9f, half), material);
            _m.Box(pivot, new Vector3(0f, 2.4f, z), new Vector3(length, 0.6f, half), material);
            _m.Box(pivot, new Vector3(-(w / 2f) - (sideLength / 2f), 1.5f, z), new Vector3(sideLength, 1.2f, half), material);
            _m.Box(pivot, new Vector3((w / 2f) + (sideLength / 2f), 1.5f, z), new Vector3(sideLength, 1.2f, half), material);
        }

        if (wall.WindowWidth > 0f)
        {
            Material frame = _t.Solid("#FFFFFF", 0.4f);
            float w = wall.WindowWidth;
            float depth = WallSegment.Thickness + 0.04f;
            _m.Box(pivot, new Vector3(0f, 0.88f, 0f), new Vector3(w + 0.16f, 0.06f, depth + 0.06f), frame);
            _m.Box(pivot, new Vector3(0f, 2.12f, 0f), new Vector3(w + 0.1f, 0.05f, depth), frame);
            _m.Box(pivot, new Vector3(-w / 2f, 1.5f, 0f), new Vector3(0.05f, 1.2f, depth), frame);
            _m.Box(pivot, new Vector3(w / 2f, 1.5f, 0f), new Vector3(0.05f, 1.2f, depth), frame);
            _m.Box(pivot, new Vector3(0f, 1.5f, 0f), new Vector3(0.04f, 1.2f, depth * 0.5f), frame);
            if (!house.BrokenWindows.Contains(House.WallKey(wall)))
            {
                visual.Glass = _m.Box(pivot, new Vector3(0f, 1.5f, 0f), new Vector3(w, 1.2f, 0.03f), _t.Glass, "glass");
                visual.Glass.CastShadow = false;
                visual.GlowRoom = wall.NegativeSide ?? wall.PositiveSide;
            }
        }

        return visual;
    }

    private void BuildDoor(DoorOpening door)
    {
        Node pivot = _scene.CreateNode(_structure, "door");
        pivot.Position = new Vector3(door.Center.X, 0f, door.Center.Y);
        pivot.EulerAngles = new Vector3(0f, door.Horizontal ? 0f : MathF.PI / 2f, 0f);
        Material frame = _t.Solid("#FFFFFF", 0.5f);
        float depth = WallSegment.Thickness + 0.03f;
        _m.Box(pivot, new Vector3(0f, 2.4f, 0f), new Vector3(door.Width + 0.02f, 0.6f, WallSegment.Thickness), _t.Siding);
        _m.Box(pivot, new Vector3(0f, 2.12f, 0f), new Vector3(door.Width + 0.12f, 0.06f, depth), frame);
        _m.Box(pivot, new Vector3(-door.Width / 2f, 1.05f, 0f), new Vector3(0.06f, 2.1f, depth), frame);
        _m.Box(pivot, new Vector3(door.Width / 2f, 1.05f, 0f), new Vector3(0.06f, 2.1f, depth), frame);
        _walls.Add(new WallVisual { Pivot = pivot, Segment = new WallSegment(door.Center, door.Center, door.Horizontal, door.A, door.B) });

        if (!door.Exterior)
        {
            return;
        }

        bool garage = door.Width > 2f;
        Node hinge = _scene.CreateNode(_structure, "door-hinge");
        Vector2 along = door.Horizontal ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
        Vector2 hingePos = garage ? door.Center : door.Center - (along * (door.Width / 2f));
        hinge.Position = new Vector3(hingePos.X, 0f, hingePos.Y);
        hinge.EulerAngles = new Vector3(0f, door.Horizontal ? 0f : -MathF.PI / 2f, 0f);
        if (garage)
        {
            _m.Box(hinge, new Vector3(0f, 1.05f, 0f), new Vector3(door.Width, 2.1f, 0.06f), _t.Solid("#E8E4DA", 0.5f));
            for (int i = 1; i < 6; i++)
            {
                _m.Box(hinge, new Vector3(0f, i * 0.35f, 0.035f), new Vector3(door.Width, 0.03f, 0.01f), _t.Solid("#CFCAC0"));
            }
        }
        else
        {
            Material leaf = _t.Solid("#9C4A2F", 0.55f);
            _m.Box(hinge, new Vector3(door.Width / 2f, 1.03f, 0f), new Vector3(door.Width - 0.04f, 2.06f, 0.06f), leaf);
            _m.Sphere(hinge, new Vector3(door.Width - 0.15f, 1.0f, 0.06f), new Vector3(0.07f), _t.Solid("#E8C35A", 0.3f, 0.8f));
            _m.Box(hinge, new Vector3(door.Width / 2f, 1.6f, 0.035f), new Vector3(0.4f, 0.4f, 0.01f), _t.WindowGlow);
        }

        _doors.Add(new DoorVisual { Hinge = hinge, Center = door.Center, Garage = garage, BaseYaw = door.Horizontal ? 0f : -MathF.PI / 2f });
    }

    private void BuildOutdoorRooms(House house)
    {
        if (house.Has(RoomId.Pool))
        {
            Rect a = Rooms.Get(RoomId.Pool).Area;
            Material tile = _t.Solid("#EAF6F8", 0.4f);
            _m.Block(_structure, a.Center.X, a.Z0 + 0.3f, 0f, new Vector3(a.Width, 0.18f, 0.6f), tile);
            _m.Block(_structure, a.Center.X, a.Z1 - 0.3f, 0f, new Vector3(a.Width, 0.18f, 0.6f), tile);
            _m.Block(_structure, a.X0 + 0.3f, a.Center.Y, 0f, new Vector3(0.6f, 0.18f, a.Depth), tile);
            _m.Block(_structure, a.X1 - 0.3f, a.Center.Y, 0f, new Vector3(0.6f, 0.18f, a.Depth), tile);
            _m.Ground(_structure, new Vector3(a.Center.X, 0.03f, a.Center.Y), a.Size - new Vector2(1.2f, 1.2f), _t.Solid("#7FD3EA", 0.2f));
            _m.Ground(_structure, new Vector3(a.Center.X, 0.12f, a.Center.Y), a.Size - new Vector2(1.2f, 1.2f), _t.PoolWater, "pool-water");
        }

        if (house.Has(RoomId.TreeHouse))
        {
            Rect a = Rooms.Get(RoomId.TreeHouse).Area;
            Vector2 c = a.Center;
            Node tree = _scene.CreateNode(_structure, "tree-house-tree");
            tree.Position = new Vector3(c.X, 0f, c.Y);
            tree.Scale = new Vector3(1.6f);
            FurnitureFactory.Tree(tree, _m, _t, 1, _models);
            Material plank = _t.Solid("#A0703F", 0.8f);
            _m.Block(_structure, c.X, c.Y, 2.6f, new Vector3(3.2f, 0.15f, 3.2f), plank);
            _m.Block(_structure, c.X, c.Y, 2.75f, new Vector3(2.4f, 1.6f, 2.2f), _t.Solid("#D9A86C", 0.8f));
            _m.Roof(_structure, c, new Vector2(2.6f, 2.4f), 4.35f, 0.9f, 0.2f, _t.Solid("#3E7CB1", 0.6f), _t.Solid("#D9A86C"));
            for (int i = 0; i < 8; i++)
            {
                _m.Box(_structure, new Vector3(c.X + 1.7f, 0.3f + (i * 0.32f), c.Y + 1.2f), new Vector3(0.5f, 0.05f, 0.08f), plank);
            }
        }

        if (house.Has(RoomId.Garden))
        {
            Rect a = Rooms.Get(RoomId.Garden).Area;
            Material picket = _t.Solid("#FFFFFF", 0.6f);
            for (float x = a.X0; x <= a.X1; x += 0.5f)
            {
                _m.Block(_structure, x, a.Z0, 0f, new Vector3(0.06f, 0.5f, 0.06f), picket);
            }

            _m.Block(_structure, a.Center.X, a.Z0, 0.35f, new Vector3(a.Width, 0.05f, 0.04f), picket);
        }

        if (!house.Has(RoomId.Garage))
        {
            Node car = _scene.CreateNode(_structure, "driveway-car");
            car.Position = new Vector3(GameSession.DrivewayCar.X, 0f, GameSession.DrivewayCar.Y);
            if (_models.Fit("car", car, new Vector2(1.9f, 4.0f)) is null)
            {
                _m.Block(car, 0, 0, 0.3f, new Vector3(1.8f, 0.9f, 3.6f), _t.Solid("#6EC1E4", 0.3f, 0.3f));
            }
        }

        // Front porch step and mailbox.
        Vector2 door = Rooms.FrontDoor;
        _m.Block(_structure, door.X, door.Y + 0.6f, 0f, new Vector3(2.2f, 0.12f, 1.0f), _t.Solid("#D8CBB3", 0.8f));
        _m.Block(_structure, door.X + 1.6f, 14.2f, 0f, new Vector3(0.08f, 1.0f, 0.08f), _t.Solid("#6B4F2E"));
        _m.Block(_structure, door.X + 1.6f, 14.2f, 1.0f, new Vector3(0.3f, 0.25f, 0.45f), _t.Solid("#D93A2B", 0.5f));
    }

    // ---------------------------------------------------------- furniture

    private void SyncFurniture(House house)
    {
        HashSet<int> alive = [];
        foreach (FurnitureItem item in house.Furniture)
        {
            alive.Add(item.Uid);
            if (_furniture.TryGetValue(item.Uid, out FurnitureVisual? visual))
            {
                if (visual.Position != item.Position || visual.Rotation != item.Rotation)
                {
                    visual.Node.Position = new Vector3(item.Position.X, 0f, item.Position.Y);
                    visual.Node.EulerAngles = new Vector3(0f, item.Yaw, 0f);
                    visual.Position = item.Position;
                    visual.Rotation = item.Rotation;
                }

                continue;
            }

            Node node = _scene.CreateNode(_furnitureRoot, item.DefId);
            node.Position = new Vector3(item.Position.X, 0f, item.Position.Y);
            node.EulerAngles = new Vector3(0f, item.Yaw, 0f);
            FurnitureFactory.Build(item.Def, node, _m, _t, _models, item.Uid);
            FurnitureVisual created = new() { Node = node, Position = item.Position, Rotation = item.Rotation };
            if (item.Def.EmitsLight)
            {
                created.Light = _scene.AddLight(Light.Point(new Vector3(1f, 0.8f, 0.55f), 3.5f, 4f), node, "lamp-light");
                created.Light.Position = new Vector3(0f, 1.4f, 0f);
            }

            _furniture[item.Uid] = created;
        }

        foreach (int uid in _furniture.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _furniture[uid].Node.Remove();
            _furniture.Remove(uid);
        }
    }

    private void SyncBroken(House house)
    {
        foreach (FurnitureItem item in house.Furniture)
        {
            if (!_furniture.TryGetValue(item.Uid, out FurnitureVisual? visual) || visual.Broken == item.Broken)
            {
                continue;
            }

            visual.Broken = item.Broken;
            if (item.Broken)
            {
                visual.BrokenIcon = _m.Quad(null, new Vector3(item.Position.X, item.Def.Height + 0.5f, item.Position.Y), new Vector2(0.45f, 0.45f), _t.Emoji("🛠"), "broken-icon");
            }
            else
            {
                visual.BrokenIcon?.Remove();
                visual.BrokenIcon = null;
            }
        }
    }

    /// <summary>Broken-item icons face the camera.</summary>
    public void FaceIcons(Quaternion cameraRotation)
    {
        foreach (FurnitureVisual v in _furniture.Values)
        {
            if (v.BrokenIcon is { } icon)
            {
                Vector3 p = icon.Position;
                icon.SetTransform(new Vector3(p.X, p.Y, p.Z), cameraRotation, new Vector3(0.45f + (0.05f * MathF.Sin(_time * 4f))));
            }
        }
    }

    // ------------------------------------------------------------ hazards

    private void SyncHazards(House house, float dt)
    {
        HashSet<int> alive = [];
        foreach (Hazard hazard in house.Hazards)
        {
            alive.Add(hazard.Id);
            if (!_hazards.TryGetValue(hazard.Id, out HazardVisual? visual))
            {
                visual = CreateHazard(hazard);
                _hazards[hazard.Id] = visual;
            }

            float i = Math.Clamp(hazard.Intensity, 0.05f, 1f);
            switch (hazard.Kind)
            {
                case HazardKind.Puddle:
                    visual.Node.Scale = new Vector3(0.6f + (1.8f * i), 0.01f, 0.5f + (1.4f * i));
                    break;
                case HazardKind.Flood:
                    visual.Node.Scale = new Vector3(2f + (5f * i), 0.02f, 1.5f + (3f * i));
                    break;
                case HazardKind.Flour:
                    visual.Node.Scale = new Vector3(1.2f * i + 0.4f, 0.01f, 1f * i + 0.4f);
                    break;
                case HazardKind.Fire:
                    if (visual.Emitter is { } fire)
                    {
                        fire.Scale = 0.5f + (i * 1.1f);
                        fire.Rate = 14f + (i * 26f);
                    }

                    if (visual.Light is { } light)
                    {
                        float flicker = 0.75f + (0.25f * MathF.Sin(_time * 23f) * MathF.Sin(_time * 7.3f));
                        light.Light = Light.Point(new Vector3(1f, 0.55f, 0.2f), 18f * i * flicker, 7f);
                    }

                    break;
                case HazardKind.Smoke:
                    if (visual.Emitter is { } smoke)
                    {
                        smoke.Rate = 2f + (i * 6f);
                        smoke.Scale = 0.6f + i;
                    }

                    break;
            }
        }

        foreach (int id in _hazards.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            HazardVisual v = _hazards[id];
            v.Node.Remove();
            if (v.Emitter is { } e)
            {
                _effects.RemoveEmitter(e);
            }

            _hazards.Remove(id);
        }
    }

    private HazardVisual CreateHazard(Hazard hazard)
    {
        Vector3 p = new(hazard.Position.X, 0f, hazard.Position.Y);
        Node node = _scene.CreateNode(_hazardRoot, $"hazard-{hazard.Kind}");
        node.Position = p;
        HazardVisual visual = new() { Node = node, Kind = hazard.Kind };
        switch (hazard.Kind)
        {
            case HazardKind.Puddle or HazardKind.Flood:
                {
                    Node disc = _scene.AddMesh(_m.UnitDisc, _t.PoolWater, _hazardRoot, "water");
                    disc.Position = p + new Vector3(0f, 0.04f, 0f);
                    disc.CastShadow = false;
                    node.Remove();
                    visual.Node = disc;
                    break;
                }

            case HazardKind.Flour:
                {
                    Node disc = _scene.AddMesh(_m.UnitDisc, _t.Solid("#FBFAF5", 0.95f), _hazardRoot, "flour");
                    disc.Position = p + new Vector3(0f, 0.035f, 0f);
                    disc.CastShadow = false;
                    node.Remove();
                    visual.Node = disc;
                    break;
                }

            case HazardKind.Fire:
                visual.Emitter = _effects.AddEmitter(EffectKind.Fireworks, p + new Vector3(0f, 0.9f, 0f), 20f);
                visual.Light = _scene.AddLight(Light.Point(new Vector3(1f, 0.55f, 0.2f), 10f, 7f), node, "fire-light");
                visual.Light.Position = new Vector3(0f, 1.4f, 0f);
                break;
            case HazardKind.Smoke:
                visual.Emitter = _effects.AddEmitter(EffectKind.Smoke, p + new Vector3(0f, 1.4f, 0f), 4f);
                break;
            case HazardKind.BrokenGlass:
                {
                    Random r = new(hazard.Id);
                    for (int i = 0; i < 9; i++)
                    {
                        Node shard = _m.Box(node, new Vector3(((float)r.NextDouble() - 0.5f) * 1.2f, 0.03f, ((float)r.NextDouble() - 0.5f) * 1.0f), new Vector3(0.12f, 0.02f, 0.07f), _t.Glass);
                        shard.EulerAngles = new Vector3(0f, (float)r.NextDouble() * 3f, 0f);
                    }

                    break;
                }

            case HazardKind.Debris:
                {
                    Random r = new(hazard.Id);
                    Material bark = _t.Solid("#6E4B2A", 0.9f);
                    for (int i = 0; i < 4; i++)
                    {
                        Node branch = _m.Box(node, new Vector3(((float)r.NextDouble() - 0.5f) * 1.6f, 0.08f, ((float)r.NextDouble() - 0.5f) * 1.2f), new Vector3(1.2f, 0.1f, 0.12f), bark);
                        branch.EulerAngles = new Vector3(0f, (float)r.NextDouble() * 3f, 0.05f);
                    }

                    _m.Sphere(node, new Vector3(0.3f, 0.25f, 0.2f), new Vector3(0.9f, 0.5f, 0.7f), _t.Solid("#4E9A4B", 0.9f), true);
                    break;
                }
        }

        return visual;
    }

    // ----------------------------------------------------- per-frame looks

    private void UpdateCutaway(GameSession session, Vector3 cameraPosition, float dt)
    {
        FamilyMember player = session.Controlled;
        Vector2 p = player.Position;
        bool inside = session.IsIndoors(p) || (Rooms.Lot.Contains(p) && BuildMode);
        Cutaway = inside;
        foreach (Node roof in _roofs)
        {
            roof.Visible = !inside && !BuildMode;
        }

        Vector2 toCamera = new(cameraPosition.X - p.X, cameraPosition.Z - p.Y);
        if (toCamera.LengthSquared() > 1e-4f)
        {
            toCamera = Vector2.Normalize(toCamera);
        }

        foreach (WallVisual wall in _walls)
        {
            float target = 1f;
            if (BuildMode)
            {
                target = 0.12f;
            }
            else if (inside)
            {
                Vector2 c = wall.Segment.Center;
                float along = Vector2.Dot(c - p, toCamera);
                // Walls between the camera and the player (and close to them) drop down.
                if (along > -0.4f && Vector2.Distance(c, p) < 14f)
                {
                    target = 0.12f;
                }
            }
            else if (Rooms.Lot.Contains(p))
            {
                // Outside but close: drop exterior walls hiding the player behind them.
                Vector2 c = wall.Segment.Center;
                float along = Vector2.Dot(c - p, toCamera);
                float side = MathF.Abs((toCamera.X * (c.Y - p.Y)) - (toCamera.Y * (c.X - p.X)));
                if (along > 0.3f && side < 3f && Vector2.Distance(c, p) < 9f)
                {
                    target = 0.35f;
                }
            }

            if (MathF.Abs(wall.Scale - target) > 0.001f)
            {
                wall.Scale = float.Lerp(wall.Scale, target, 1f - MathF.Exp(-10f * dt));
                if (MathF.Abs(wall.Scale - target) < 0.005f)
                {
                    wall.Scale = target;
                }

                wall.Pivot.Scale = new Vector3(1f, wall.Scale, 1f);
            }
        }
    }

    private void UpdateDoors(GameSession session, float dt)
    {
        foreach (DoorVisual door in _doors)
        {
            bool near = session.State.Members.Any(m => !m.Away && Vector2.Distance(m.Position, door.Center) < 1.6f)
                || session.State.Pets.Any(p => Vector2.Distance(p.Position, door.Center) < 1.2f);
            if (session.State.House.DoorsLocked && !session.State.Members.Any(m => !m.Away && Vector2.Distance(m.Position, door.Center) < 1.0f))
            {
                near = false;
            }

            float target = near ? 1f : 0f;
            door.Angle = float.Lerp(door.Angle, target, 1f - MathF.Exp(-8f * dt));
            if (door.Garage)
            {
                door.Hinge.Scale = new Vector3(1f, 1f - (door.Angle * 0.85f), 1f);
                Vector3 pos = door.Hinge.Position;
                door.Hinge.Position = new Vector3(pos.X, door.Angle * 1.8f, pos.Z);
            }
            else
            {
                door.Hinge.EulerAngles = new Vector3(0f, door.BaseYaw + (door.Angle * 1.75f), 0f);
            }
        }
    }

    private bool _lightsWereOn;

    private void UpdateLights(GameSession session)
    {
        bool anyLit = false;
        foreach ((RoomId room, Node light) in _roomLights)
        {
            bool lit = session.RoomLit(room);
            anyLit |= lit;
            if (light.Light is { } l && l.Enabled != lit)
            {
                light.Light = l with { Enabled = lit };
            }
        }

        foreach (WallVisual wall in _walls)
        {
            if (wall.Glass is null || wall.GlowRoom is not { } room)
            {
                continue;
            }

            bool lit = session.RoomLit(room);
            if (lit != wall.Lit)
            {
                wall.Lit = lit;
                wall.Glass.DetachMesh();
                wall.Glass.AttachMesh(_m.UnitBox, lit ? _t.WindowGlow : _t.Glass);
            }
        }

        foreach (FurnitureVisual f in _furniture.Values)
        {
            if (f.Light is { Light: { } l } node)
            {
                bool on = session.State.House.PowerOn && (session.Hour < 6.5f || session.Hour > 17.5f);
                if (l.Enabled != on)
                {
                    node.Light = l with { Enabled = on };
                }
            }
        }

        if (anyLit != _lightsWereOn)
        {
            _lightsWereOn = anyLit;
            _t.WindowGlow.Update(o => o with { EmissiveIntensity = anyLit ? 2.2f : 0f });
            _t.LampGlow.Update(o => o with { EmissiveIntensity = anyLit ? 4f : 0.3f });
        }
    }
}
