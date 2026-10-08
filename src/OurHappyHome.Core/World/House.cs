using System.Numerics;
using System.Text.Json.Serialization;

namespace OurHappyHome.Core.World;

/// <summary>A straight wall run between two (or one) rooms.</summary>
public sealed record WallSegment(Vector2 Start, Vector2 End, bool Horizontal, RoomId? NegativeSide, RoomId? PositiveSide)
{
    public const float Thickness = 0.16f;
    public const float Height = 2.7f;

    public bool Exterior => NegativeSide is null || PositiveSide is null;

    public float Length => Vector2.Distance(Start, End);

    public Vector2 Center => (Start + End) / 2f;

    /// <summary>Window opening along the wall (centre offset from Start, width); 0 width = none.</summary>
    public float WindowCenter { get; init; }

    public float WindowWidth { get; init; }

    public Rect Bounds => Horizontal
        ? new Rect(MathF.Min(Start.X, End.X), Start.Y - (Thickness / 2), MathF.Max(Start.X, End.X), Start.Y + (Thickness / 2))
        : new Rect(Start.X - (Thickness / 2), MathF.Min(Start.Y, End.Y), Start.X + (Thickness / 2), MathF.Max(Start.Y, End.Y));
}

public sealed record DoorOpening(Vector2 Center, bool Horizontal, float Width, bool Exterior, RoomId A, RoomId? B)
{
    public string Key => $"{Center.X:0.00},{Center.Y:0.00}";
}

public enum HazardKind
{
    Puddle,
    Fire,
    Smoke,
    BrokenGlass,
    Debris,
    Flood,
    Flour,
}

public sealed class Hazard
{
    public int Id { get; set; }
    public HazardKind Kind { get; set; }
    public Vector2 Position { get; set; }
    public RoomId Room { get; set; }

    /// <summary>0-1; fire spreads and puddles grow while it rises.</summary>
    public float Intensity { get; set; } = 0.3f;
}

/// <summary>
/// The family home: which rooms exist, the furniture in them, paint and
/// floors, and its safety state (power, locks, lights, alarm, hazards).
/// </summary>
public sealed class House
{
    private const float Step = 0.25f;

    public HashSet<RoomId> BuiltRooms { get; set; } = [.. Rooms.StartingRooms];

    public List<FurnitureItem> Furniture { get; set; } = [];

    public Dictionary<RoomId, string> WallPaint { get; set; } = [];

    public Dictionary<RoomId, FloorStyle> FloorOverrides { get; set; } = [];

    public bool PowerOn { get; set; } = true;
    public bool DoorsLocked { get; set; }
    public bool WindowsClosed { get; set; }
    public bool ExteriorLightsOn { get; set; }
    public bool AlarmActive { get; set; }
    public bool OutdoorItemsSecured { get; set; }

    /// <summary>Rooms whose ceiling light is on (the AI and player switch them).</summary>
    public HashSet<RoomId> LightsOn { get; set; } = [];

    public List<Hazard> Hazards { get; set; } = [];

    /// <summary>Windows broken by a storm (wall segment keys).</summary>
    public HashSet<string> BrokenWindows { get; set; } = [];

    public int NextUid { get; set; } = 1;
    public int NextHazardId { get; set; } = 1;

    /// <summary>Bumped on every structural change so the renderer knows when to rebuild.</summary>
    [JsonIgnore]
    public int Revision { get; private set; }

    [JsonIgnore]
    public int FurnitureRevision { get; private set; }

    private List<WallSegment>? _walls;
    private List<DoorOpening>? _doors;

    public void Touch()
    {
        Revision++;
        _walls = null;
        _doors = null;
    }

    public void TouchFurniture() => FurnitureRevision++;

    public bool Has(RoomId room) => BuiltRooms.Contains(room);

    public int IndoorRoomCount => BuiltRooms.Count(r => Rooms.Get(r).Indoor);

    public string Title => Rooms.HouseTitle(IndoorRoomCount);

    public string Paint(RoomId room) => WallPaint.TryGetValue(room, out string? c) ? c : Rooms.Get(room).WallColor;

    public FloorStyle Floor(RoomId room) => FloorOverrides.TryGetValue(room, out FloorStyle f) ? f : Rooms.Get(room).Floor;

    public bool CanBuild(RoomId room, int chapter, out string reason)
    {
        RoomDef def = Rooms.Get(room);
        if (Has(room))
        {
            reason = Loc.T("Sudah dibangun", "Already built");
            return false;
        }

        if (chapter < def.MinChapter)
        {
            reason = Loc.T($"Terbuka di Bab {def.MinChapter}", $"Unlocks in Chapter {def.MinChapter}");
            return false;
        }

        if (def.Requires is { } required && !Has(required))
        {
            reason = Loc.T($"Butuh {Rooms.Name(required)}", $"Needs {Rooms.Name(required)}");
            return false;
        }

        if (room == RoomId.UpperHall && Furniture.Any(f => f.Room == RoomId.Hall && f.Def.Height > 0.05f && !f.Def.WallMounted && f.Bounds.Overlaps(Floors.Stairs.Inflate(0.4f))))
        {
            reason = Loc.T("Kosongkan sisi timur lorong untuk tangga", "Clear the east side of the hallway for the stairs");
            return false;
        }

        reason = "";
        return true;
    }

    public void Build(RoomId room)
    {
        if (BuiltRooms.Add(room))
        {
            foreach ((string id, Vector2 local, int rotation) in StarterFurniture(room))
            {
                Place(id, room, local, rotation);
            }

            // The upper floor comes with its balcony.
            if (room == RoomId.UpperHall)
            {
                Build(RoomId.Balcony);
            }

            Touch();
            TouchFurniture();
        }
    }

    /// <summary>True when the stairs exist (the upper floor has been built).</summary>
    public bool HasUpperFloor => Has(RoomId.UpperHall);

    // ------------------------------------------------------------------ rooms

    /// <summary>The room (indoor first, then outdoor area) containing a point, or null outside the lot.</summary>
    public RoomId? RoomAt(Vector2 p)
    {
        foreach (RoomId id in BuiltRooms)
        {
            RoomDef def = Rooms.Get(id);
            if (def.Indoor && def.Area.Contains(p))
            {
                return id;
            }
        }

        foreach (RoomId id in new[] { RoomId.Pool, RoomId.TreeHouse, RoomId.Garden, RoomId.Balcony })
        {
            if (Has(id) && Rooms.Get(id).Area.Contains(p))
            {
                return id;
            }
        }

        if (Rooms.Get(RoomId.FrontYard).Area.Contains(p))
        {
            return RoomId.FrontYard;
        }

        if (Rooms.Lot.Contains(p))
        {
            return RoomId.Backyard;
        }

        return null;
    }

    public bool IsIndoors(Vector2 p) => RoomAt(p) is { } r && Rooms.Get(r).Indoor;

    /// <summary>Footprint of all built indoor rooms (for the roof).</summary>
    public IEnumerable<RoomDef> IndoorRooms => BuiltRooms.Select(Rooms.Get).Where(r => r.Indoor);

    // -------------------------------------------------------------- furniture

    public FurnitureItem Place(string defId, RoomId room, Vector2 position, int rotation)
    {
        FurnitureItem item = new() { Uid = NextUid++, DefId = defId, Room = room, Position = position, Rotation = ((rotation % 4) + 4) % 4 };
        item.EnsureOccupants();
        Furniture.Add(item);
        TouchFurniture();
        return item;
    }

    public void Remove(FurnitureItem item)
    {
        Furniture.Remove(item);
        TouchFurniture();
    }

    public FurnitureItem? Find(int uid) => Furniture.Find(f => f.Uid == uid);

    public IEnumerable<FurnitureItem> InRoom(RoomId room) => Furniture.Where(f => f.Room == room);

    public IEnumerable<FurnitureItem> WithActivity(Simulation.ActivityId activity) =>
        Furniture.Where(f => f.Def.Activities.Contains(activity));

    /// <summary>Checks placement: inside the room, not overlapping walls or other furniture.</summary>
    public bool CanPlace(FurnitureDef def, RoomId room, Vector2 position, int rotation, FurnitureItem? ignore = null)
    {
        Vector2 size = rotation % 2 == 1 ? new Vector2(def.Footprint.Y, def.Footprint.X) : def.Footprint;
        Rect bounds = Rect.FromCenter(position, size);
        RoomDef roomDef = Rooms.Get(room);
        Rect area = roomDef.Area;
        if (roomDef.Indoor)
        {
            area = area.Inflate(-WallSegment.Thickness / 2);
        }

        if (bounds.X0 < area.X0 || bounds.X1 > area.X1 || bounds.Z0 < area.Z0 || bounds.Z1 > area.Z1)
        {
            return false;
        }

        if (def.Height <= 0.05f)
        {
            return true; // rugs go under things
        }

        foreach (FurnitureItem other in Furniture)
        {
            if (other == ignore || other.Def.Height <= 0.05f)
            {
                continue;
            }

            if (other.Bounds.Overlaps(bounds))
            {
                return false;
            }
        }

        // Keep doorways clear.
        foreach (DoorOpening door in Doors)
        {
            Rect clear = Rect.FromCenter(door.Center, new Vector2(door.Width + 0.3f, door.Width + 0.3f));
            if (clear.Overlaps(bounds))
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ walls

    public IReadOnlyList<WallSegment> Walls
    {
        get
        {
            if (_walls is null)
            {
                BuildWalls();
            }

            return _walls!;
        }
    }

    public IReadOnlyList<DoorOpening> Doors
    {
        get
        {
            if (_doors is null)
            {
                BuildWalls();
            }

            return _doors!;
        }
    }

    private static int Q(float v) => (int)MathF.Round(v / Step);

    private void BuildWalls()
    {
        // Unit wall pieces on a 0.25 m lattice, keyed by (horizontal, line, cell).
        Dictionary<(bool H, int Line, int Cell), (RoomId? Neg, RoomId? Pos)> pieces = [];

        void AddEdge(bool horizontal, float line, float from, float to, RoomId room, bool roomOnPositiveSide)
        {
            for (int cell = Q(from); cell < Q(to); cell++)
            {
                (bool, int, int) key = (horizontal, Q(line), cell);
                pieces.TryGetValue(key, out (RoomId? Neg, RoomId? Pos) sides);
                if (roomOnPositiveSide)
                {
                    sides.Pos = room;
                }
                else
                {
                    sides.Neg = room;
                }

                pieces[key] = sides;
            }
        }

        foreach (RoomDef room in IndoorRooms)
        {
            Rect a = room.Area;
            AddEdge(true, a.Z0, a.X0, a.X1, room.Id, true);
            AddEdge(true, a.Z1, a.X0, a.X1, room.Id, false);
            AddEdge(false, a.X0, a.Z0, a.Z1, room.Id, true);
            AddEdge(false, a.X1, a.Z0, a.Z1, room.Id, false);
        }

        // Cut doorways where both sides exist (or the door leads outside).
        List<DoorOpening> doors = [];
        HashSet<string> seen = [];
        foreach (RoomDef room in IndoorRooms)
        {
            foreach (DoorDef door in room.Doors)
            {
                if (door.Other is { } other && !Has(other))
                {
                    continue;
                }

                Vector2 center = door.Horizontal ? new Vector2(door.Center, door.Line) : new Vector2(door.Line, door.Center);
                DoorOpening opening = new(center, door.Horizontal, door.Width, door.Exterior, room.Id, door.Other);
                if (!seen.Add(opening.Key))
                {
                    continue;
                }

                doors.Add(opening);
                for (int cell = Q(door.Center - (door.Width / 2)); cell < Q(door.Center + (door.Width / 2)); cell++)
                {
                    pieces.Remove((door.Horizontal, Q(door.Line), cell));
                }
            }
        }

        // Merge consecutive pieces with the same neighbours into segments.
        List<WallSegment> walls = [];
        foreach (IGrouping<(bool H, int Line), KeyValuePair<(bool H, int Line, int Cell), (RoomId? Neg, RoomId? Pos)>> run in
                 pieces.GroupBy(p => (p.Key.H, p.Key.Line)))
        {
            List<KeyValuePair<(bool H, int Line, int Cell), (RoomId? Neg, RoomId? Pos)>> ordered = [.. run.OrderBy(p => p.Key.Cell)];
            int start = 0;
            for (int i = 1; i <= ordered.Count; i++)
            {
                bool breakRun = i == ordered.Count
                    || ordered[i].Key.Cell != ordered[i - 1].Key.Cell + 1
                    || ordered[i].Value != ordered[start].Value;
                if (!breakRun)
                {
                    continue;
                }

                float line = run.Key.Line * Step;
                float from = ordered[start].Key.Cell * Step;
                float to = (ordered[i - 1].Key.Cell + 1) * Step;
                (RoomId? neg, RoomId? pos) = ordered[start].Value;
                Vector2 a = run.Key.H ? new Vector2(from, line) : new Vector2(line, from);
                Vector2 b = run.Key.H ? new Vector2(to, line) : new Vector2(line, to);
                float length = to - from;
                bool exterior = neg is null || pos is null;
                bool garage = neg == RoomId.Garage || pos == RoomId.Garage;
                walls.Add(new WallSegment(a, b, run.Key.H, neg, pos)
                {
                    WindowCenter = length / 2f,
                    WindowWidth = exterior && !garage && length >= 2.4f ? MathF.Min(1.4f, length - 1.0f) : 0f,
                });
                start = i;
            }
        }

        _walls = walls;
        _doors = doors;
    }

    public static string WallKey(WallSegment wall) => $"{wall.Start.X:0.00},{wall.Start.Y:0.00}-{wall.End.X:0.00},{wall.End.Y:0.00}";

    // ---------------------------------------------------------------- hazards

    public Hazard AddHazard(HazardKind kind, Vector2 position, float intensity = 0.3f)
    {
        Hazard hazard = new() { Id = NextHazardId++, Kind = kind, Position = position, Room = RoomAt(position) ?? RoomId.Backyard, Intensity = intensity };
        Hazards.Add(hazard);
        return hazard;
    }

    public bool AnyHazard(HazardKind kind) => Hazards.Exists(h => h.Kind == kind);

    // ------------------------------------------------------- starting layout

    /// <summary>Furniture placed when a room is built (positions in world space).</summary>
    public static IEnumerable<(string Id, Vector2 Position, int Rotation)> StarterFurniture(RoomId room) => room switch
    {
        RoomId.LivingRoom =>
        [
            ("rug", new(-3.4f, 3.0f), 0),
            ("sofa", new(-3.4f, 1.0f), 0),
            ("coffee-table", new(-3.4f, 2.4f), 0),
            ("tv", new(-3.4f, 4.6f), 2),
            ("bookshelf", new(-5.65f, 1.6f), 1),
            ("lamp", new(-5.5f, 0.5f), 0),
            ("plant", new(-5.5f, 5.5f), 0),
            ("first-aid", new(-0.3f, 0.8f), 3),
        ],
        RoomId.Kitchen =>
        [
            ("kitchen", new(2.9f, 5.45f), 2),
            ("fridge", new(5.35f, 5.6f), 2),
            ("dining", new(3.0f, 2.2f), 0),
            ("extinguisher", new(0.4f, 5.6f), 2),
        ],
        RoomId.Hall =>
        [
            ("fuse-box", new(-1.05f, -5.0f), 1),
        ],
        RoomId.UpperHall =>
        [
            ("plant", new(-0.8f, -705.5f), 0),
        ],
        RoomId.Attic =>
        [
            ("rug", new(-3.6f, -703f), 0),
            ("toy-box", new(-5.2f, -705.4f), 0),
            ("beanbag", new(-2.3f, -704.8f), 0),
            ("bookshelf", new(-5.65f, -702.4f), 1),
        ],
        RoomId.Studio =>
        [
            ("desk", new(3.6f, -705.1f), 0),
            ("easel", new(5.2f, -702.2f), 3),
            ("computer", new(2.4f, -701f), 2),
            ("plant", new(5.5f, -700.6f), 0),
        ],
        RoomId.Balcony =>
        [
            ("telescope", new(-4.8f, -695.2f), 2),
            ("plant", new(-0.6f, -694.6f), 0),
            ("plant", new(-5.5f, -699.3f), 0),
            ("beanbag", new(-2.5f, -696f), 2),
        ],
        RoomId.ParentsBedroom =>
        [
            ("double-bed", new(-3.6f, -4.95f), 0),
            ("wardrobe", new(-5.6f, -2.0f), 1),
            ("plant", new(-1.8f, -5.5f), 0),
        ],
        RoomId.KidsBedroom =>
        [
            ("bunk-bed", new(4.95f, -4.9f), 0),
            ("single-bed", new(2.0f, -4.9f), 0),
            ("single-bed", new(3.4f, -4.9f), 0),
            ("desk", new(3.6f, -0.75f), 2),
            ("toy-box", new(5.4f, -1.2f), 3),
        ],
        RoomId.Bathroom =>
        [
            ("shower", new(7.5f, 5.45f), 2),
            ("toilet", new(8.55f, 1.2f), 3),
            ("sink", new(8.6f, 3.0f), 3),
            ("washer", new(6.5f, 0.45f), 0),
        ],
        RoomId.GirlsBedroom =>
        [
            ("single-bed", new(7.0f, -4.9f), 0),
            ("single-bed", new(9.6f, -4.9f), 0),
            ("easel", new(9.8f, -1.0f), 3),
            ("wardrobe", new(8.3f, -5.6f), 0),
        ],
        RoomId.Playroom =>
        [
            ("rug", new(-8.5f, 3.0f), 0),
            ("toy-box", new(-10.4f, 4.6f), 1),
            ("piano", new(-8.5f, 5.6f), 2),
            ("armchair", new(-10.3f, 0.8f), 1),
        ],
        RoomId.Library =>
        [
            ("bookshelf", new(-10.7f, -2.0f), 1),
            ("bookshelf", new(-10.7f, -4.0f), 1),
            ("bookshelf", new(-8.5f, -5.7f), 0),
            ("armchair", new(-7.2f, -3.0f), 3),
            ("computer", new(-8.5f, -0.5f), 2),
        ],
        RoomId.Garage =>
        [
            ("car", new(11.5f, 3.0f), 0),
        ],
        RoomId.Workshop =>
        [
            ("workbench", new(12.25f, -5.55f), 0),
            ("security", new(13.8f, -2.5f), 3),
        ],
        RoomId.SecretRoom =>
        [
            ("armchair", new(-8.5f, -8.3f), 0),
            ("lamp", new(-9.6f, -8.6f), 0),
        ],
        RoomId.FrontYard =>
        [
            ("tree", new(-7.5f, 13.5f), 0),
            ("tree", new(16.5f, 12.5f), 0),
            ("flower-bush", new(-6.5f, 7.0f), 0),
            ("flower-bush", new(0.5f, 7.0f), 0),
        ],
        RoomId.Backyard =>
        [
            ("tree", new(14.5f, -13.5f), 0),
            ("tree", new(-6f, -14.5f), 0),
            ("picnic", new(-3.0f, -10.0f), 0),
        ],
        RoomId.Garden =>
        [
            ("veg-patch", new(-12.0f, 9.6f), 0),
            ("veg-patch", new(-12.0f, 12.4f), 0),
            ("flower-bush", new(-14.2f, 13.4f), 0),
        ],
        _ => [],
    };

    public static House CreateStarter()
    {
        House house = new() { BuiltRooms = [] };
        foreach (RoomId room in Rooms.StartingRooms)
        {
            house.Build(room);
        }

        return house;
    }
}
