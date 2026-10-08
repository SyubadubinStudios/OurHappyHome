using System.Numerics;

namespace OurHappyHome.Core.World;

public enum PlaceId
{
    Home,
    Neighborhood,
    Park,
    School,
    Supermarket,
    Mall,
    Clinic,
    Restaurant,
    Arcade,
    ThemePark,
    Beach,
    Forest,
    Camping,
}

public enum FeatureKind
{
    Road,
    Sidewalk,
    House,
    School,
    Shop,
    Tree,
    PineTree,
    Bush,
    Fence,
    Pond,
    Ocean,
    Sand,
    Bench,
    LampPost,
    Playground,
    Rock,
    Tent,
    Campfire,
    FerrisWheel,
    Carousel,
    Mountain,
    Flowerbed,
    ParkedCar,
    Field,
    Pier,
    Umbrella,
    Sign,

    // Interiors (school classroom, supermarket, clinic).
    InteriorFloor,
    InteriorWall,
    Door,
    Shelf,
    Counter,
    Desk,
    Board,
    ClinicBed,
    Plant,

    // Festival grounds in the park (only shown and usable on festival days).
    FestivalStall,
    FestivalStage,

    /// <summary>The beach inn where the family can stay the night.</summary>
    Inn,

    /// <summary>A model placed as scenery: <see cref="TownFeature.Label"/> is the model name, Height its height.</summary>
    Prop,

    /// <summary>Wet sand, shallow water and moving foam along the beach.</summary>
    Shore,

    /// <summary>Flat ground decoration you can walk over: beach towels, dirt trails.</summary>
    Decal,
}

/// <summary>
/// A static thing in the town. The renderer turns it into meshes and the
/// collision world into an obstacle, so what you see is what you bump into.
/// </summary>
public sealed record TownFeature(FeatureKind Kind, Rect Area, float Height, string Color, float Yaw = 0f, string Label = "")
{
    /// <summary>How far into the sea people can paddle before it is too deep.</summary>
    public const float WadeDepthMetres = 6f;

    public bool Solid => Kind is FeatureKind.House or FeatureKind.School or FeatureKind.Shop or FeatureKind.Tree or FeatureKind.PineTree
        or FeatureKind.Fence or FeatureKind.Pond or FeatureKind.Ocean or FeatureKind.Rock or FeatureKind.Tent or FeatureKind.FerrisWheel
        or FeatureKind.Carousel or FeatureKind.Mountain or FeatureKind.ParkedCar or FeatureKind.LampPost or FeatureKind.Bush
        or FeatureKind.Campfire or FeatureKind.Playground or FeatureKind.InteriorWall or FeatureKind.Door or FeatureKind.Shelf
        or FeatureKind.Counter or FeatureKind.Desk or FeatureKind.Board or FeatureKind.ClinicBed or FeatureKind.Plant or FeatureKind.Inn or FeatureKind.Prop;

    /// <summary>Trees and posts only block near their trunk.</summary>
    public Rect CollisionArea => Kind switch
    {
        FeatureKind.Tree or FeatureKind.PineTree => Rect.FromCenter(Area.Center, new Vector2(0.7f, 0.7f)),
        // The first metres of the sea are shallow enough to paddle in.
        FeatureKind.Ocean => new Rect(Area.X0, Area.Z0 + WadeDepthMetres, Area.X1, Area.Z1),
        FeatureKind.Prop => Rect.FromCenter(Area.Center, Area.Size * 0.8f),
        FeatureKind.LampPost => Rect.FromCenter(Area.Center, new Vector2(0.3f, 0.3f)),
        FeatureKind.Campfire => Rect.FromCenter(Area.Center, new Vector2(1.0f, 1.0f)),
        _ => Area,
    };
}

public sealed record Place(PlaceId Id, Rect Area, Vector2 Entrance, float EntranceYaw)
{
    public string Name => WorldMap.Name(Id);

    public string Icon => WorldMap.Icon(Id);

    public string Description => WorldMap.Description(Id);

    /// <summary>Point on the map image (0-1) for the world map screen.</summary>
    public Vector2 MapPoint => new((Area.Center.X + 300f) / 600f, (Area.Center.Y + 300f) / 640f);
}

/// <summary>
/// The inside of a town building. Interiors are diorama rooms built in an
/// empty corner of the map: entering teleports the party in, the exit mat
/// teleports them back to the building's entrance.
/// </summary>
public sealed record Interior(PlaceId Place, Rect Area, Vector2 Spawn, float SpawnYaw, Vector2 Exit, IReadOnlyList<Vector2> Services)
{
    public string Name => WorldMap.Name(Place);
}

/// <summary>
/// The town around the family home, laid out like the design document's map:
/// <code>
///                  Mountain / Camping (north, -Z)
///                         |
///  Forest (west) --- Neighborhood --- School (east)
///                         |
///                     Downtown (south)
///                    /        \
///                Clinic       Mall
///                   |
///                 Beach
/// </code>
/// </summary>
public sealed class WorldMap
{
    public static readonly Rect Bounds = new(-300f, -300f, 300f, 340f);

    public List<TownFeature> Features { get; } = [];

    public List<Place> Places { get; } = [];

    public List<Interior> Interiors { get; } = [];

    public Interior? InteriorFor(PlaceId id) => Interiors.FirstOrDefault(i => i.Place == id);

    public Interior? InteriorAt(Vector2 p) => Interiors.FirstOrDefault(i => i.Area.Contains(p));

    public static WorldMap Generate(int seed = 7)
    {
        WorldMap map = new();
        GameRandom random = new((ulong)seed);
        map.Build(random);
        return map;
    }

    public Place Get(PlaceId id) => Places.First(p => p.Id == id);

    public PlaceId? PlaceAt(Vector2 p)
    {
        if (InteriorAt(p) is { } inside)
        {
            return inside.Place;
        }

        if (Floors.IsUpper(p))
        {
            return PlaceId.Home;
        }

        // Small places first so a shop inside downtown wins over the district.
        foreach (Place place in Places.OrderBy(pl => pl.Area.Width * pl.Area.Depth))
        {
            if (place.Area.Contains(p))
            {
                return place.Id;
            }
        }

        return null;
    }

    private void Add(FeatureKind kind, Rect area, float height, string color, float yaw = 0f, string label = "") =>
        Features.Add(new TownFeature(kind, area, height, color, yaw, label));

    private void Road(float x0, float z0, float x1, float z1)
    {
        Add(FeatureKind.Road, new Rect(x0, z0, x1, z1), 0.02f, "#4A4E57");
    }

    private void Build(GameRandom random)
    {
        // ---- places
        Places.Add(new(PlaceId.Home, Rooms.Lot, new Vector2(-1.2f, 9f), MathF.PI));
        Places.Add(new(PlaceId.Neighborhood, new Rect(-90f, -30f, 80f, 70f), new Vector2(-1.2f, 20f), MathF.PI));
        Places.Add(new(PlaceId.Park, new Rect(-58f, 27f, -14f, 72f), new Vector2(-36f, 28f), 0f));
        Places.Add(new(PlaceId.School, new Rect(92f, -48f, 156f, 14f), new Vector2(124f, 18f), MathF.PI));
        Places.Add(new(PlaceId.Supermarket, new Rect(26f, 96f, 56f, 122f), new Vector2(41f, 125f), MathF.PI));
        Places.Add(new(PlaceId.Mall, new Rect(76f, 96f, 116f, 124f), new Vector2(96f, 127f), MathF.PI));
        Places.Add(new(PlaceId.Clinic, new Rect(26f, 140f, 54f, 166f), new Vector2(40f, 137f), 0f));
        Places.Add(new(PlaceId.Restaurant, new Rect(76f, 140f, 100f, 162f), new Vector2(88f, 137f), 0f));
        Places.Add(new(PlaceId.Arcade, new Rect(108f, 140f, 128f, 160f), new Vector2(118f, 137f), 0f));
        Places.Add(new(PlaceId.ThemePark, new Rect(150f, 90f, 236f, 176f), new Vector2(146f, 132f), MathF.PI / 2f));
        Places.Add(new(PlaceId.Beach, new Rect(-70f, 228f, 170f, 300f), new Vector2(38f, 285f), 0f));
        Places.Add(new(PlaceId.Forest, new Rect(-270f, -90f, -110f, 90f), new Vector2(-104f, 20f), -MathF.PI / 2f));
        Places.Add(new(PlaceId.Camping, new Rect(-80f, -280f, 30f, -190f), new Vector2(-24f, -212f), MathF.PI));

        // ---- roads: main street in front of the house, then spurs to each district.
        Road(-260f, 17f, 230f, 23f);           // main street (z = 20)
        Road(-28f, -192f, -22f, 17f);          // north road to the mountain (ends at the camp ground)
        Road(57f, 23f, 63f, 200f);             // south road downtown and to the beach
        Road(20f, 128f, 146f, 134f);           // downtown boulevard
        Road(63f, 196f, 69f, 226f);
        Add(FeatureKind.Sidewalk, new Rect(-260f, 15.2f, 230f, 17f), 0.06f, "#C9C3B8");
        Add(FeatureKind.Sidewalk, new Rect(-260f, 23f, 230f, 24.8f), 0.06f, "#C9C3B8");

        // Driveway and front path of the family home.
        Add(FeatureKind.Sidewalk, new Rect(9.5f, 6f, 13.5f, 15.2f), 0.04f, "#B9B2A6");
        Add(FeatureKind.Sidewalk, new Rect(-1.8f, 6f, -0.6f, 15.2f), 0.04f, "#D8CBB3");

        // Lot fence with gaps for the path and the driveway.
        Rect lot = Rooms.Lot;
        Fence(lot.X0, lot.Z1, -2.2f, lot.Z1, true);
        Fence(-0.2f, lot.Z1, 9.3f, lot.Z1, true);
        Fence(13.7f, lot.Z1, lot.X1, lot.Z1, true);
        Fence(lot.X0, lot.Z0, lot.X1, lot.Z0, true);
        Fence(lot.X0, lot.Z0, lot.X0, lot.Z1, false);
        Fence(lot.X1, lot.Z0, lot.X1, lot.Z1, false);

        // ---- neighbours along the main street
        float[] northLots = [-80f, -55f, 32f, 58f];
        foreach (float x in northLots)
        {
            NeighbourHouse(random, new Vector2(x, -4f), 0f);
        }

        float[] southLots = [8f, 34f, -76f];
        foreach (float x in southLots)
        {
            NeighbourHouse(random, new Vector2(x, 38f), MathF.PI);
        }

        // Street trees and lamps.
        for (float x = -250f; x <= 220f; x += 18f)
        {
            if (MathF.Abs(x - (-25f)) < 6 || MathF.Abs(x - 60f) < 6)
            {
                continue;
            }

            Add(FeatureKind.LampPost, Rect.FromCenter(new Vector2(x, 25.6f), new Vector2(0.4f, 0.4f)), 4.2f, "#3C3F46");
            if (x < -20f || x > 20f)
            {
                Add(FeatureKind.Tree, Rect.FromCenter(new Vector2(x + 9f, 14.0f), new Vector2(3.5f, 3.5f)), 5.5f, "#4E9A4B");
            }
        }

        // ---- community park
        Add(FeatureKind.Field, new Rect(-58f, 27f, -14f, 72f), 0.03f, "#86C06C");
        Add(FeatureKind.Pond, Rect.FromCenter(new Vector2(-44f, 56f), new Vector2(14f, 9f)), 0.05f, "#4DA6D9");
        Add(FeatureKind.Playground, Rect.FromCenter(new Vector2(-26f, 40f), new Vector2(6f, 4f)), 2.4f, "#E5533D");
        for (int i = 0; i < 6; i++)
        {
            Add(FeatureKind.Bench, Rect.FromCenter(new Vector2(-50f + (i * 6f), 33f), new Vector2(1.6f, 0.5f)), 0.5f, "#9C6B3F");
        }

        for (int i = 0; i < 18; i++)
        {
            Vector2 p = new(random.Range(-56f, -16f), random.Range(46f, 70f));
            if (Vector2.Distance(p, new Vector2(-44f, 56f)) > 10f)
            {
                Add(FeatureKind.Tree, Rect.FromCenter(p, new Vector2(4f, 4f)), random.Range(4.5f, 7f), "#5DA34E");
            }
        }

        for (int i = 0; i < 8; i++)
        {
            Add(FeatureKind.Flowerbed, Rect.FromCenter(new Vector2(random.Range(-55f, -18f), random.Range(30f, 44f)), new Vector2(2.5f, 1.5f)), 0.3f, i % 2 == 0 ? "#F28BB0" : "#F7D046");
        }

        // ---- school
        Road(120f, 14f, 126f, 17f);
        Add(FeatureKind.Field, new Rect(92f, -48f, 156f, 14f), 0.03f, "#9BC86E");
        Add(FeatureKind.School, new Rect(100f, -40f, 150f, -18f), 9f, "#F0C987", 0f, Loc.T("SD Pelangi", "Rainbow Elementary"));
        Add(FeatureKind.Sign, Rect.FromCenter(new Vector2(118f, 12f), new Vector2(3f, 0.3f)), 2.2f, "#2E6FBF", 0f, Loc.T("SD Pelangi", "Rainbow Elementary"));
        Add(FeatureKind.Sidewalk, new Rect(120f, -18f, 128f, 14f), 0.04f, "#D8CBB3");
        Add(FeatureKind.Field, new Rect(132f, -12f, 154f, 10f), 0.05f, "#4FA35A");     // football field
        Add(FeatureKind.Playground, Rect.FromCenter(new Vector2(104f, 0f), new Vector2(6f, 4f)), 2.4f, "#3D8FE5");
        for (int i = 0; i < 6; i++)
        {
            Add(FeatureKind.Tree, Rect.FromCenter(new Vector2(95f + (i * 11f), 10f), new Vector2(3.5f, 3.5f)), 5f, "#4E9A4B");
        }

        // ---- downtown
        Add(FeatureKind.Sidewalk, new Rect(20f, 92f, 146f, 170f), 0.04f, "#CFC8BC");
        Add(FeatureKind.Shop, new Rect(28f, 100f, 54f, 120f), 7f, "#7CC7A1", 0f, Loc.T("Supermarket Segar", "Fresh Supermarket"));
        Add(FeatureKind.Shop, new Rect(78f, 98f, 114f, 122f), 11f, "#A58BE0", 0f, Loc.T("Mal Ceria", "Happy Mall"));
        Add(FeatureKind.Shop, new Rect(28f, 142f, 52f, 164f), 7f, "#F5F5F5", 0f, Loc.T("Klinik Sehat", "Health Clinic"));
        Add(FeatureKind.Shop, new Rect(78f, 142f, 98f, 160f), 6f, "#F29E4C", 0f, Loc.T("Warung Bakso", "Meatball Restaurant"));
        Add(FeatureKind.Shop, new Rect(110f, 142f, 126f, 158f), 6f, "#E35D9F", 0f, Loc.T("Arkade Bintang", "Star Arcade"));
        for (int i = 0; i < 6; i++)
        {
            Add(FeatureKind.ParkedCar, Rect.FromCenter(new Vector2(30f + (i * 18f), 126f), new Vector2(4f, 1.9f)), 1.5f,
                random.Pick(new[] { "#E4572E", "#29335C", "#F3A712", "#A8C686", "#669BBC" }));
        }

        // ---- theme park
        Add(FeatureKind.Field, new Rect(150f, 90f, 236f, 176f), 0.03f, "#A3D977");
        Add(FeatureKind.FerrisWheel, Rect.FromCenter(new Vector2(205f, 120f), new Vector2(4f, 20f)), 22f, "#FF6B6B");
        Add(FeatureKind.Carousel, Rect.FromCenter(new Vector2(175f, 150f), new Vector2(10f, 10f)), 5f, "#FFD166");
        Add(FeatureKind.Sign, Rect.FromCenter(new Vector2(152f, 132f), new Vector2(0.3f, 6f)), 4f, "#EF476F", MathF.PI / 2f, Loc.T("Taman Bermain", "Theme Park"));

        // ---- beach
        Add(FeatureKind.Sand, new Rect(-80f, 226f, 180f, 300.5f), 0.02f, "#F2DDA4");
        Add(FeatureKind.Ocean, new Rect(-300f, 300f, 300f, 340f), 0.0f, "#2A9DD8");
        Add(FeatureKind.Shore, new Rect(-300f, 293f, 300f, 300f + TownFeature.WadeDepthMetres), 0.0f, "#E3C98F");
        Add(FeatureKind.Pier, new Rect(80f, 280f, 84f, 320f), 0.6f, "#A57A52");
        BuildBeach(new GameRandom(41));

        Add(FeatureKind.Inn, Rect.FromCenter(new Vector2(18f, 247f), new Vector2(11f, 7f)), 3.4f, "#F4E3C3", MathF.PI, Loc.T("Penginapan Pantai", "Beach Inn"));

        // ---- forest
        Road(-110f, 18f, -28f, 22f);
        for (int i = 0; i < 240; i++)
        {
            Vector2 p = new(random.Range(-268f, -112f), random.Range(-88f, 88f));
            if (MathF.Abs(p.Y - 20f) < 4f && p.X > -200f)
            {
                continue; // keep the trail open
            }

            bool pine = random.Chance(0.55f);
            float size = random.Range(3f, 5.5f);
            Add(pine ? FeatureKind.PineTree : FeatureKind.Tree, Rect.FromCenter(p, new Vector2(size, size)), random.Range(6f, 11f), pine ? "#2F6B3A" : "#4C8C3F");
        }

        for (int i = 0; i < 25; i++)
        {
            Add(FeatureKind.Rock, Rect.FromCenter(new Vector2(random.Range(-260f, -115f), random.Range(-85f, 85f)), new Vector2(random.Range(1f, 2.5f), random.Range(1f, 2.5f))), random.Range(0.6f, 1.6f), "#8A8F98");
        }

        // ---- camping ground and the mountain behind it
        Add(FeatureKind.Field, new Rect(-80f, -280f, 30f, -190f), 0.03f, "#7FB36A");
        Add(FeatureKind.Mountain, Rect.FromCenter(new Vector2(-40f, -330f), new Vector2(260f, 60f)), 90f, "#6E8B74");
        Add(FeatureKind.Mountain, Rect.FromCenter(new Vector2(120f, -320f), new Vector2(180f, 50f)), 70f, "#7F9C84");
        Add(FeatureKind.Mountain, Rect.FromCenter(new Vector2(-220f, -300f), new Vector2(150f, 60f)), 80f, "#728F79");
        Add(FeatureKind.Tent, Rect.FromCenter(new Vector2(-30f, -235f), new Vector2(3.2f, 3.2f)), 2f, "#F4A259");
        Add(FeatureKind.Tent, Rect.FromCenter(new Vector2(-18f, -240f), new Vector2(3.2f, 3.2f)), 2f, "#5B8E7D");
        Add(FeatureKind.Campfire, Rect.FromCenter(new Vector2(-24f, -226f), new Vector2(1.6f, 1.6f)), 0.5f, "#C94C2C");
        for (int i = 0; i < 40; i++)
        {
            Vector2 p = new(random.Range(-78f, 28f), random.Range(-278f, -192f));
            if (Vector2.Distance(p, new Vector2(-24f, -232f)) < 18f || MathF.Abs(p.X + 25f) < 4f || CampClearing(p))
            {
                continue;
            }

            Add(FeatureKind.PineTree, Rect.FromCenter(p, new Vector2(4f, 4f)), random.Range(7f, 12f), "#2F6B3A");
        }

        BuildCamp(new GameRandom(31));

        // Angkot stops (halte): by the family home, downtown and at the park.
        foreach ((float x, float z, float yaw) in HalteSpots)
        {
            Prop("halte", new Vector2(x, z), new Vector2(3f, 1.2f), 2.6f, yaw);
        }

        BuildFestival();
        BuildInteriors();
    }

    /// <summary>Bus stop positions (x, z, yaw); angkots pause in front of them.</summary>
    public static readonly (float X, float Z, float Yaw)[] HalteSpots = [(7.5f, 16.1f, 0f), (-44f, 25.6f, MathF.PI), (24f, 126.4f, 0f)];

    // ------------------------------------------------------- beach & camp

    private void Prop(string model, Vector2 at, Vector2 footprint, float height, float yaw = 0f) =>
        Add(FeatureKind.Prop, Rect.FromCenter(at, footprint), height, "#888888", yaw, model);

    /// <summary>Palms, warungs, a lifeguard tower, deck chairs, boats and rocks along the sea.</summary>
    private void BuildBeach(GameRandom random)
    {
        Vector2[] warungs = [new(-20f, 252f), new(62f, 252f), new(126f, 252f)];
        foreach (Vector2 w in warungs)
        {
            Prop("beach-warung", w, new Vector2(5f, 4f), 3.8f);
        }

        // Two loose rows of palms behind the sand, keeping clear of buildings.
        foreach ((float z, float step) in new[] { (231f, 11f), (241f, 17f) })
        {
            for (float x = -76f; x < 176f; x += step)
            {
                Vector2 p = new(x + random.Range(-3f, 3f), z + random.Range(-2.5f, 2.5f));
                if (Vector2.Distance(p, new Vector2(18f, 247f)) < 9f || warungs.Any(w => Vector2.Distance(p, w) < 6f))
                {
                    continue;
                }

                Add(FeatureKind.Tree, Rect.FromCenter(p, new Vector2(3f, 3f)), random.Range(6.5f, 9f), "#3FA34D", random.Range(0f, MathF.Tau), "palm");
            }
        }

        Prop("lifeguard-tower", new Vector2(45f, 289f), new Vector2(2.4f, 2.4f), 5f);

        // Family beach life close to where the family arrives.
        string[] towels = ["#E63946", "#2A9D8F", "#F4A261", "#9B5DE5", "#FFD23F", "#4A90D9"];
        for (int i = 0; i < 10; i++)
        {
            Vector2 p = new(-40f + (i * 20f) + random.Range(-4f, 4f), 276f + random.Range(0f, 14f));
            if (Vector2.Distance(p, new Vector2(38f, 285f)) > 3f && Vector2.Distance(p, new Vector2(45f, 289f)) > 3.5f && MathF.Abs(p.X - 82f) > 3f)
            {
                Add(FeatureKind.Decal, Rect.FromCenter(p, new Vector2(1f, 1.9f)), 0.03f, towels[i % towels.Length], random.Range(-0.5f, 0.5f), "towel");
            }
        }

        Prop("sandcastle", new Vector2(30f, 292f), new Vector2(1.6f, 1.6f), 0.9f);
        Prop("sandcastle", new Vector2(96f, 291f), new Vector2(1.4f, 1.4f), 0.7f, 0.6f);
        Prop("beach-ball", new Vector2(33f, 289.5f), new Vector2(0.5f, 0.5f), 0.45f);
        Prop("beach-ball", new Vector2(70f, 284f), new Vector2(0.5f, 0.5f), 0.45f);
        Prop("volleyball", new Vector2(92f, 274f), new Vector2(8f, 0.6f), 2.4f);
        foreach ((float x, float yaw) in new[] { (55f, 0.1f), (57.2f, -0.12f), (59.3f, 0.05f) })
        {
            Prop("surfboard", new Vector2(x, 256.5f), new Vector2(0.7f, 0.3f), 2.2f, yaw);
        }
        for (int i = 0; i < 12; i++)
        {
            Vector2 p = new(-58f + (i * 19f) + random.Range(-3f, 3f), 268f + random.Range(0f, 18f));
            if (MathF.Abs(p.X - 82f) < 4f || Vector2.Distance(p, new Vector2(45f, 289f)) < 5f)
            {
                continue;
            }

            Prop("deck-chair", p, new Vector2(2f, 2f), 2.6f, random.Range(-0.4f, 0.4f));
        }

        // Fishing boats pulled up on the sand, and two out at sea.
        Prop("boat", new Vector2(-36f, 295f), new Vector2(2f, 5f), 1.6f, 0.35f);
        Prop("boat", new Vector2(108f, 294.5f), new Vector2(2f, 5f), 1.6f, -0.25f);
        Prop("boat", new Vector2(152f, 295.5f), new Vector2(2f, 5f), 1.6f, 0.1f);
        Prop("boat", new Vector2(20f, 318f), new Vector2(2f, 5f), 1.6f, 1.2f);
        Prop("boat", new Vector2(135f, 326f), new Vector2(2f, 5f), 1.6f, -0.9f);

        foreach ((float x, float z, float s) in new[] { (-77f, 288f, 2.4f), (-70f, 299f, 1.8f), (175f, 285f, 2.6f), (168f, 298f, 1.6f), (-5f, 236f, 1.4f) })
        {
            Prop("rocks", new Vector2(x, z), new Vector2(s * 1.6f, s * 1.3f), s * 0.7f, x);
        }
    }

    /// <summary>The camp lake, the scouts' camp and the flag pole stay free of trees.</summary>
    private static bool CampClearing(Vector2 p) =>
        Rect.FromCenter(CampLake, new Vector2(30f, 22f)).Contains(p)
        || Vector2.Distance(p, ScoutCamp) < 13f
        || Vector2.Distance(p, new Vector2(-16f, -207f)) < 4f
        || Rect.FromCenter(new Vector2(-24f, -205f), new Vector2(18f, 26f)).Contains(p); // arrival point and camera

    public static readonly Vector2 CampLake = new(-58f, -256f);

    public static readonly Vector2 ScoutCamp = new(9f, -245f);

    private void Trail(Vector2 from, Vector2 to, float width = 1.6f)
    {
        Vector2 d = to - from;
        Add(FeatureKind.Decal, Rect.FromCenter((from + to) / 2f, new Vector2(width, d.Length())), 0.02f, "#8B6B45", MathF.Atan2(d.X, d.Y), "trail");
    }

    /// <summary>A lake, the scouts' tents and fire, log benches, rocks, flowers and a thick ring of pines.</summary>
    private void BuildCamp(GameRandom random)
    {
        // Dirt trails from the road end to the campfire, the lake and the scouts.
        Trail(new Vector2(-25f, -192f), new Vector2(-24f, -222f), 2.2f);
        Trail(new Vector2(-27f, -229f), new Vector2(-48f, -250f));
        Trail(new Vector2(-21f, -228f), ScoutCamp + new Vector2(-4f, 4f));
        Add(FeatureKind.Pond, Rect.FromCenter(CampLake, new Vector2(24f, 15f)), 0.05f, "#4DA6D9");
        Prop("log-bench", new Vector2(-46f, -246f), new Vector2(1.9f, 0.5f), 0.7f, 0.3f);

        // Benches around the family campfire.
        Prop("log-bench", new Vector2(-24f, -223.2f), new Vector2(1.9f, 0.5f), 0.7f);
        Prop("log-bench", new Vector2(-26.9f, -226.6f), new Vector2(0.5f, 1.9f), 0.7f, MathF.PI / 2f);
        Prop("log-bench", new Vector2(-21.1f, -226.6f), new Vector2(0.5f, 1.9f), 0.7f, MathF.PI / 2f);

        // The scouts' camp (Pramuka) with their own fire.
        Add(FeatureKind.Campfire, Rect.FromCenter(ScoutCamp + new Vector2(0f, 4f), new Vector2(1.6f, 1.6f)), 0.5f, "#C94C2C");
        Prop("dome-tent-green", ScoutCamp + new Vector2(-5f, -3f), new Vector2(2.6f, 3.2f), 2.2f, 0.4f);
        Prop("dome-tent-blue", ScoutCamp + new Vector2(1f, -6f), new Vector2(2.6f, 3.2f), 2.2f, 0.05f);
        Prop("dome-tent-green", ScoutCamp + new Vector2(6.5f, -3f), new Vector2(2.6f, 3.2f), 2.2f, -0.4f);
        Prop("log-bench", ScoutCamp + new Vector2(0f, 6.8f), new Vector2(1.9f, 0.5f), 0.7f);
        Prop("flag-pole", new Vector2(-16f, -207f), new Vector2(0.8f, 0.8f), 5.2f, 0.6f);

        foreach ((float x, float z, float s) in new[] { (-72f, -244f, 2f), (-44f, -266f, 1.6f), (-66f, -268f, 1.4f), (22f, -268f, 2.2f), (-6f, -198f, 1.2f), (24f, -215f, 1.8f) })
        {
            Prop("rocks", new Vector2(x, z), new Vector2(s * 1.6f, s * 1.3f), s * 0.7f, z);
        }

        for (int i = 0; i < 10; i++)
        {
            Vector2 p = new(random.Range(-70f, 20f), random.Range(-275f, -200f));
            if (Vector2.Distance(p, new Vector2(-24f, -230f)) > 10f && !CampClearing(p) && MathF.Abs(p.X + 25f) > 4f)
            {
                Add(FeatureKind.Flowerbed, Rect.FromCenter(p, new Vector2(1.6f, 1f)), 0.25f, i % 2 == 0 ? "#FFFFFF" : "#F7D046");
            }
        }

        // A dense forest ring around the camp ground (clear of the road and entrance).
        Rect field = new(-76f, -276f, 26f, -194f);
        for (int i = 0; i < 160; i++)
        {
            Vector2 p = new(random.Range(-115f, 65f), random.Range(-305f, -168f));
            if (field.Contains(p) || CampClearing(p) || (MathF.Abs(p.X + 25f) < 5f && p.Y > -215f) || (p.Y > -210f && p.Y < -196f && p.X > -30f && p.X < 24f))
            {
                continue;
            }

            Add(FeatureKind.PineTree, Rect.FromCenter(p, new Vector2(4.5f, 4.5f)), random.Range(8f, 14f), "#2F6B3A");
        }
    }

    /// <summary>Where the festival fireworks go up.</summary>
    public static readonly Vector2 FestivalCenter = new(-31f, 46f);

    private void BuildFestival()
    {
        // Stalls along the lawn in front of the pond; the stage is the contest ground.
        (string Label, string Color, float X)[] stalls =
        [
            ("kerupuk", "#E63946", -52f),
            ("tug", "#2A9D8F", -46.5f),
            ("food", "#F4A261", -41f),
            ("toys", "#9B5DE5", -35.5f),
        ];
        foreach ((string label, string color, float x) in stalls)
        {
            Add(FeatureKind.FestivalStall, Rect.FromCenter(new Vector2(x, 44.5f), new Vector2(3.2f, 1.6f)), 2.6f, color, 0f, label);
        }

        Add(FeatureKind.FestivalStage, Rect.FromCenter(FestivalCenter, new Vector2(8f, 5f)), 0.25f, "#B5835A", 0f, "stage");
    }

    // ------------------------------------------------------------ interiors

    /// <summary>Floor, four low walls (cutaway height) and a door on the south side.</summary>
    private Rect Room(Vector2 center, Vector2 size, string floor, string wall)
    {
        Rect a = Rect.FromCenter(center, size);
        const float t = 0.2f;
        const float h = 1.5f;
        Add(FeatureKind.InteriorFloor, a, 0.02f, "#C9A26B", 0f, floor);
        Add(FeatureKind.InteriorWall, new Rect(a.X0 - t, a.Z0 - t, a.X1 + t, a.Z0), h, wall);
        Add(FeatureKind.InteriorWall, new Rect(a.X0 - t, a.Z1, a.X1 + t, a.Z1 + t), h, wall);
        Add(FeatureKind.InteriorWall, new Rect(a.X0 - t, a.Z0, a.X0, a.Z1), h, wall);
        Add(FeatureKind.InteriorWall, new Rect(a.X1, a.Z0, a.X1 + t, a.Z1), h, wall);
        Add(FeatureKind.Door, Rect.FromCenter(new Vector2(center.X, a.Z1 + 0.1f), new Vector2(1.4f, 0.12f)), 2.2f, "#8E5B3E", 0f, Loc.T("Keluar", "Exit"));
        return a;
    }

    private void BuildInteriors()
    {
        // School classroom: blackboard, teacher's desk and twelve pupil desks.
        Vector2 sc = new(252f, -280f);
        Room(sc, new Vector2(18f, 12f), "wood", "#F3E3C3");
        Add(FeatureKind.Board, Rect.FromCenter(sc + new Vector2(0f, -5.6f), new Vector2(6f, 0.15f)), 2.6f, "#2F5D3A", 0f, "A B C   1 + 2 = 3");
        Add(FeatureKind.Desk, Rect.FromCenter(sc + new Vector2(0f, -3.6f), new Vector2(2f, 0.9f)), 0.8f, "#8E5B3E", 0f, "teacher");
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                Add(FeatureKind.Desk, Rect.FromCenter(sc + new Vector2(-6f + (col * 4f), -0.8f + (row * 2.3f)), new Vector2(1.2f, 0.7f)), 0.7f, "#C9A26B");
            }
        }

        Add(FeatureKind.Plant, Rect.FromCenter(sc + new Vector2(-8.2f, -5.2f), new Vector2(0.8f, 0.8f)), 1.3f, "#4E9A4B");
        Add(FeatureKind.Plant, Rect.FromCenter(sc + new Vector2(8.2f, -5.2f), new Vector2(0.8f, 0.8f)), 1.3f, "#4E9A4B");
        Add(FeatureKind.Shelf, Rect.FromCenter(sc + new Vector2(8.3f, 0f), new Vector2(0.7f, 3f)), 1.4f, "#B5835A", 0f, "books");
        Interiors.Add(new Interior(PlaceId.School, Rect.FromCenter(sc, new Vector2(18f, 12f)), sc + new Vector2(0f, 3.8f), MathF.PI,
            sc + new Vector2(0f, 5.5f), [sc + new Vector2(0f, -2.7f)]));

        // Supermarket: four aisles of shelves and a cashier by the door.
        Vector2 sm = new(280f, -280f);
        Room(sm, new Vector2(20f, 12f), "tile", "#DDF1E4");
        string[] shelfColors = ["#E4572E", "#F3A712", "#29335C", "#669BBC"];
        for (int i = 0; i < 4; i++)
        {
            Vector2 c = sm + new Vector2(i % 2 == 0 ? -4.5f : 4.5f, i < 2 ? -3.2f : -0.4f);
            Add(FeatureKind.Shelf, Rect.FromCenter(c, new Vector2(7f, 0.9f)), 1.5f, shelfColors[i], 0f, "groceries");
        }

        Add(FeatureKind.Shelf, Rect.FromCenter(sm + new Vector2(0f, -5.5f), new Vector2(12f, 0.7f)), 1.7f, "#A8C686", 0f, "fridge");
        Add(FeatureKind.Counter, Rect.FromCenter(sm + new Vector2(-6.5f, 3.4f), new Vector2(3f, 1f)), 1.0f, "#7CC7A1", 0f, Loc.T("Kasir", "Cashier"));
        Add(FeatureKind.Plant, Rect.FromCenter(sm + new Vector2(9.2f, 5.2f), new Vector2(0.8f, 0.8f)), 1.3f, "#4E9A4B");
        Interiors.Add(new Interior(PlaceId.Supermarket, Rect.FromCenter(sm, new Vector2(20f, 12f)), sm + new Vector2(0f, 3.8f), MathF.PI,
            sm + new Vector2(0f, 5.5f), [sm + new Vector2(-6.5f, 2.4f), sm + new Vector2(-4.5f, -1.8f), sm + new Vector2(4.5f, -1.8f), sm + new Vector2(0f, -4.6f)]));

        // Clinic: reception, the doctor's desk, two beds and a waiting bench.
        Vector2 cl = new(252f, -258f);
        Room(cl, new Vector2(14f, 10f), "checker", "#F4F8FB");
        Add(FeatureKind.Counter, Rect.FromCenter(cl + new Vector2(-4.2f, 2.2f), new Vector2(3f, 0.9f)), 1.0f, "#9BD1E5", 0f, Loc.T("Pendaftaran", "Reception"));
        Add(FeatureKind.Desk, Rect.FromCenter(cl + new Vector2(-3f, -3.2f), new Vector2(1.8f, 0.9f)), 0.8f, "#FFFFFF", 0f, "teacher");
        Add(FeatureKind.ClinicBed, Rect.FromCenter(cl + new Vector2(2.6f, -3.4f), new Vector2(1.1f, 2.1f)), 0.7f, "#FFFFFF");
        Add(FeatureKind.ClinicBed, Rect.FromCenter(cl + new Vector2(5.2f, -3.4f), new Vector2(1.1f, 2.1f)), 0.7f, "#FFFFFF");
        Add(FeatureKind.Bench, Rect.FromCenter(cl + new Vector2(4f, 3.6f), new Vector2(3f, 0.5f)), 0.5f, "#7FA7C9");
        Add(FeatureKind.Plant, Rect.FromCenter(cl + new Vector2(-6.3f, -4.2f), new Vector2(0.8f, 0.8f)), 1.3f, "#4E9A4B");
        Interiors.Add(new Interior(PlaceId.Clinic, Rect.FromCenter(cl, new Vector2(14f, 10f)), cl + new Vector2(0f, 2.8f), MathF.PI,
            cl + new Vector2(0f, 4.5f), [cl + new Vector2(-3f, -2.3f), cl + new Vector2(-4.2f, 1.3f)]));
    }

    private void Fence(float x0, float z0, float x1, float z1, bool horizontal)
    {
        Rect area = horizontal
            ? new Rect(MathF.Min(x0, x1), z0 - 0.06f, MathF.Max(x0, x1), z0 + 0.06f)
            : new Rect(x0 - 0.06f, MathF.Min(z0, z1), x0 + 0.06f, MathF.Max(z0, z1));
        Add(FeatureKind.Fence, area, 1.0f, "#FFFFFF");
    }

    private void NeighbourHouse(GameRandom random, Vector2 center, float yaw)
    {
        string[] walls = ["#F6D8AE", "#BEE3DB", "#FAE1DD", "#D5E5A3", "#C9DAEA", "#F1E3C8"];
        Vector2 size = new(random.Range(10f, 13f), random.Range(8f, 10f));
        Add(FeatureKind.House, Rect.FromCenter(center, size), random.Range(3.0f, 3.4f), random.Pick(walls), yaw);
        float front = yaw == 0f ? 1f : -1f;
        Add(FeatureKind.Tree, Rect.FromCenter(center + new Vector2(size.X / 2f + 3f, front * 4f), new Vector2(3.5f, 3.5f)), 5.5f, "#4E9A4B");
        Add(FeatureKind.Bush, Rect.FromCenter(center + new Vector2(-size.X / 2f + 1f, front * (size.Y / 2f + 1f)), new Vector2(2f, 1f)), 0.9f, "#5DA34E");
    }

    public static string Name(PlaceId id) => id switch
    {
        PlaceId.Home => Loc.T("Rumah", "Home"),
        PlaceId.Neighborhood => Loc.T("Lingkungan Rumah", "Neighborhood"),
        PlaceId.Park => Loc.T("Taman Kota", "Community Park"),
        PlaceId.School => Loc.T("Sekolah", "School"),
        PlaceId.Supermarket => Loc.T("Supermarket", "Supermarket"),
        PlaceId.Mall => Loc.T("Mal", "Mall"),
        PlaceId.Clinic => Loc.T("Klinik", "Clinic"),
        PlaceId.Restaurant => Loc.T("Restoran", "Restaurant"),
        PlaceId.Arcade => Loc.T("Arkade", "Arcade"),
        PlaceId.ThemePark => Loc.T("Taman Bermain", "Theme Park"),
        PlaceId.Beach => Loc.T("Pantai", "Beach"),
        PlaceId.Forest => Loc.T("Hutan", "Forest"),
        _ => Loc.T("Bumi Perkemahan", "Camping Ground"),
    };

    public static string Icon(PlaceId id) => id switch
    {
        PlaceId.Home => "🏡",
        PlaceId.Neighborhood => "🏘",
        PlaceId.Park => "🌳",
        PlaceId.School => "🏫",
        PlaceId.Supermarket => "🛒",
        PlaceId.Mall => "🛍",
        PlaceId.Clinic => "🏥",
        PlaceId.Restaurant => "🍜",
        PlaceId.Arcade => "🕹",
        PlaceId.ThemePark => "🎡",
        PlaceId.Beach => "🏖",
        PlaceId.Forest => "🌲",
        _ => "⛺",
    };

    public static string Description(PlaceId id) => id switch
    {
        PlaceId.Home => Loc.T("Rumah keluarga kita.", "Our family home."),
        PlaceId.Neighborhood => Loc.T("Tetangga yang ramah dan jalan untuk bersepeda.", "Friendly neighbours and streets for cycling."),
        PlaceId.Park => Loc.T("Piknik, kolam ikan, taman bermain.", "Picnics, a pond and a playground."),
        PlaceId.School => Loc.T("Belajar Bahasa Inggris, Matematika, IPA, Seni, Olahraga dan Komputer.", "English, Maths, Science, Art, Sports and Computers."),
        PlaceId.Supermarket => Loc.T("Bahan makanan, senter dan baterai.", "Groceries, flashlights and batteries."),
        PlaceId.Mall => Loc.T("Perabot, baju, mainan dan hadiah.", "Furniture, clothes, toys and gifts."),
        PlaceId.Clinic => Loc.T("Periksa kesehatan bila ada yang sakit.", "See the doctor when someone is unwell."),
        PlaceId.Restaurant => Loc.T("Makan bakso bersama keluarga.", "Eat meatball soup together."),
        PlaceId.Arcade => Loc.T("Permainan seru, hadiah tiket.", "Fun games and ticket prizes."),
        PlaceId.ThemePark => Loc.T("Bianglala dan komidi putar!", "Ferris wheel and carousel!"),
        PlaceId.Beach => Loc.T("Berenang, istana pasir dan matahari terbenam.", "Swimming, sandcastles and sunsets."),
        PlaceId.Forest => Loc.T("Jelajahi jalan setapak, kumpulkan bunga dan jamur. Hati-hati hewan liar!", "Explore trails, gather flowers and mushrooms. Watch for wild animals!"),
        _ => Loc.T("Tenda, api unggun dan bintang-bintang.", "Tents, campfire and stars."),
    };
}
