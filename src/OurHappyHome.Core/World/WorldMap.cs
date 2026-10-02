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
}

/// <summary>
/// A static thing in the town. The renderer turns it into meshes and the
/// collision world into an obstacle, so what you see is what you bump into.
/// </summary>
public sealed record TownFeature(FeatureKind Kind, Rect Area, float Height, string Color, float Yaw = 0f, string Label = "")
{
    public bool Solid => Kind is FeatureKind.House or FeatureKind.School or FeatureKind.Shop or FeatureKind.Tree or FeatureKind.PineTree
        or FeatureKind.Fence or FeatureKind.Pond or FeatureKind.Ocean or FeatureKind.Rock or FeatureKind.Tent or FeatureKind.FerrisWheel
        or FeatureKind.Carousel or FeatureKind.Mountain or FeatureKind.ParkedCar or FeatureKind.LampPost or FeatureKind.Bush
        or FeatureKind.Campfire or FeatureKind.Playground;

    /// <summary>Trees and posts only block near their trunk.</summary>
    public Rect CollisionArea => Kind switch
    {
        FeatureKind.Tree or FeatureKind.PineTree => Rect.FromCenter(Area.Center, new Vector2(0.7f, 0.7f)),
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
        Places.Add(new(PlaceId.Beach, new Rect(-70f, 228f, 170f, 300f), new Vector2(40f, 278f), 0f));
        Places.Add(new(PlaceId.Forest, new Rect(-270f, -90f, -110f, 90f), new Vector2(-104f, 20f), -MathF.PI / 2f));
        Places.Add(new(PlaceId.Camping, new Rect(-80f, -280f, 30f, -190f), new Vector2(-25f, -185f), MathF.PI));

        // ---- roads: main street in front of the house, then spurs to each district.
        Road(-260f, 17f, 230f, 23f);           // main street (z = 20)
        Road(-28f, -200f, -22f, 17f);          // north road to the mountain
        Road(57f, 23f, 63f, 200f);             // south road downtown and to the beach
        Road(20f, 128f, 146f, 134f);           // downtown boulevard
        Road(63f, 196f, 69f, 226f);
        Road(-28f, -206f, 20f, -200f);         // camp loop
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
        Add(FeatureKind.Sand, new Rect(-80f, 226f, 180f, 305f), 0.02f, "#F2DDA4");
        Add(FeatureKind.Ocean, new Rect(-300f, 300f, 300f, 340f), 0.0f, "#2A9DD8");
        Add(FeatureKind.Pier, new Rect(80f, 280f, 84f, 320f), 0.6f, "#A57A52");
        for (int i = 0; i < 8; i++)
        {
            Add(FeatureKind.Umbrella, Rect.FromCenter(new Vector2(-40f + (i * 22f), 262f + ((i % 2) * 8f)), new Vector2(2.6f, 2.6f)), 2.4f,
                i % 2 == 0 ? "#E63946" : "#2A9D8F");
        }

        for (int i = 0; i < 10; i++)
        {
            Add(FeatureKind.Tree, Rect.FromCenter(new Vector2(-70f + (i * 25f), 232f), new Vector2(3f, 3f)), 7f, "#3FA34D", 0f, "palm");
        }

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
            if (Vector2.Distance(p, new Vector2(-24f, -232f)) < 18f || MathF.Abs(p.X + 25f) < 4f)
            {
                continue;
            }

            Add(FeatureKind.PineTree, Rect.FromCenter(p, new Vector2(4f, 4f)), random.Range(7f, 12f), "#2F6B3A");
        }
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
