using System.Numerics;

namespace OurHappyHome.Core.World;

public enum RoomId
{
    LivingRoom,
    Kitchen,
    Hall,
    ParentsBedroom,
    KidsBedroom,
    Bathroom,
    GirlsBedroom,
    Playroom,
    Library,
    Garage,
    Workshop,
    SecretRoom,
    FrontYard,
    Backyard,
    Garden,
    Pool,
    TreeHouse,
}

public enum FloorStyle
{
    Wood,
    Tile,
    Carpet,
    Concrete,
    Grass,
    Checker,
}

/// <summary>
/// A doorway on a room boundary. <see cref="Horizontal"/> doors sit on a line
/// of constant Z (spanning X), vertical ones on constant X.
/// </summary>
public sealed record DoorDef(bool Horizontal, float Line, float Center, float Width, RoomId? Other)
{
    /// <summary>Other == null means the door leads outside.</summary>
    public bool Exterior => Other is null;
}

public sealed record RoomDef(
    RoomId Id,
    Rect Area,
    bool Indoor,
    long Cost,
    int MinChapter,
    FloorStyle Floor,
    string WallColor,
    DoorDef[] Doors,
    RoomId? Requires = null)
{
    public string Name => Rooms.Name(Id);

    public string Description => Rooms.Description(Id);
}

/// <summary>
/// The fixed lot plan. Rooms expand outwards from the small starting home
/// into slots that already line up, so buying a room never needs a redesign:
/// <code>
///   back row  (z -6..0): Library | Parents | Hall | Kids | Girls | Workshop
///   front row (z  0..6): Playroom | Living  | Kitchen | Bath | Garage
/// </code>
/// </summary>
public static class Rooms
{
    public static readonly Rect Lot = new(-16f, -16f, 18f, 15f);

    public static readonly Vector2 FrontDoor = new(-1.2f, 6f);

    public static readonly IReadOnlyList<RoomDef> All =
    [
        new(RoomId.LivingRoom, new(-6f, 0f, 0f, 6f), true, 0, 1, FloorStyle.Wood, "#F2E3C6",
        [
            new(true, 6f, -1.2f, 1.1f, null),
            new(false, 0f, 3f, 2.0f, RoomId.Kitchen),
            new(true, 0f, -0.6f, 0.9f, RoomId.Hall),
            new(false, -6f, 3f, 1.0f, RoomId.Playroom),
        ]),
        new(RoomId.Kitchen, new(0f, 0f, 6f, 6f), true, 0, 1, FloorStyle.Checker, "#DDEFE6",
        [
            new(false, 0f, 3f, 2.0f, RoomId.LivingRoom),
            new(false, 6f, 3f, 0.9f, RoomId.Bathroom),
        ]),
        new(RoomId.Hall, new(-1.25f, -6f, 1.25f, 0f), true, 0, 1, FloorStyle.Wood, "#EFE6D8",
        [
            new(true, 0f, -0.6f, 0.9f, RoomId.LivingRoom),
            new(true, -6f, 0f, 1.0f, null),
            new(false, -1.25f, -3f, 0.9f, RoomId.ParentsBedroom),
            new(false, 1.25f, -3f, 0.9f, RoomId.KidsBedroom),
        ]),
        new(RoomId.ParentsBedroom, new(-6f, -6f, -1.25f, 0f), true, 0, 1, FloorStyle.Carpet, "#E7DDF0",
        [
            new(false, -1.25f, -3f, 0.9f, RoomId.Hall),
        ]),
        new(RoomId.KidsBedroom, new(1.25f, -6f, 6f, 0f), true, 0, 1, FloorStyle.Wood, "#D9E8F7",
        [
            new(false, 1.25f, -3f, 0.9f, RoomId.Hall),
            new(false, 6f, -3f, 0.9f, RoomId.GirlsBedroom),
        ]),
        new(RoomId.Bathroom, new(6f, 0f, 9f, 6f), true, 0, 1, FloorStyle.Tile, "#D7F0F4",
        [
            new(false, 6f, 3f, 0.9f, RoomId.Kitchen),
        ]),
        new(RoomId.GirlsBedroom, new(6f, -6f, 10.5f, 0f), true, 2_500_000, 1, FloorStyle.Carpet, "#F8DDE8",
        [
            new(false, 6f, -3f, 0.9f, RoomId.KidsBedroom),
        ]),
        new(RoomId.Playroom, new(-11f, 0f, -6f, 6f), true, 3_000_000, 2, FloorStyle.Carpet, "#FFF0C2",
        [
            new(false, -6f, 3f, 1.0f, RoomId.LivingRoom),
            new(true, 0f, -8.5f, 0.9f, RoomId.Library),
        ]),
        new(RoomId.Library, new(-11f, -6f, -6f, 0f), true, 3_500_000, 3, FloorStyle.Wood, "#E9DCC9",
        [
            new(true, 0f, -8.5f, 0.9f, RoomId.Playroom),
            new(true, -6f, -8.5f, 0.9f, RoomId.SecretRoom),
        ], RoomId.Playroom),
        new(RoomId.Garage, new(9f, 0f, 14f, 6f), true, 4_000_000, 2, FloorStyle.Concrete, "#DADDE2",
        [
            new(true, 6f, 11.5f, 3.0f, null),
            new(true, 0f, 12.25f, 0.9f, RoomId.Workshop),
        ]),
        new(RoomId.Workshop, new(10.5f, -6f, 14f, 0f), true, 2_800_000, 3, FloorStyle.Concrete, "#E8E1D3",
        [
            new(true, -6f, 12.25f, 1.0f, null),
            new(true, 0f, 12.25f, 0.9f, RoomId.Garage),
        ]),
        new(RoomId.SecretRoom, new(-10f, -9f, -7f, -6f), true, 2_000_000, 4, FloorStyle.Checker, "#CFC2E8",
        [
            new(true, -6f, -8.5f, 0.9f, RoomId.Library),
        ], RoomId.Library),
        new(RoomId.FrontYard, new(-16f, 6f, 18f, 15f), false, 0, 1, FloorStyle.Grass, "#7FB069", []),
        new(RoomId.Backyard, new(-16f, -16f, 18f, -6f), false, 0, 1, FloorStyle.Grass, "#7FB069", []),
        new(RoomId.Garden, new(-15f, 8f, -9f, 14f), false, 800_000, 1, FloorStyle.Grass, "#6FA35A", []),
        new(RoomId.Pool, new(3f, -14.5f, 10f, -9.5f), false, 6_000_000, 4, FloorStyle.Tile, "#5EC4E8", []),
        new(RoomId.TreeHouse, new(-15f, -15.5f, -10.5f, -10.5f), false, 3_000_000, 4, FloorStyle.Grass, "#8C6B4A", []),
    ];

    public static readonly RoomId[] StartingRooms =
    [
        RoomId.LivingRoom, RoomId.Kitchen, RoomId.Hall, RoomId.ParentsBedroom, RoomId.KidsBedroom,
        RoomId.Bathroom, RoomId.FrontYard, RoomId.Backyard,
    ];

    public static RoomDef Get(RoomId id) => All[(int)id];

    public static string Name(RoomId id) => id switch
    {
        RoomId.LivingRoom => Loc.T("Ruang Keluarga", "Living Room"),
        RoomId.Kitchen => Loc.T("Dapur & Ruang Makan", "Kitchen & Dining"),
        RoomId.Hall => Loc.T("Lorong", "Hallway"),
        RoomId.ParentsBedroom => Loc.T("Kamar Ayah & Ibu", "Parents' Bedroom"),
        RoomId.KidsBedroom => Loc.T("Kamar Anak", "Kids' Bedroom"),
        RoomId.Bathroom => Loc.T("Kamar Mandi", "Bathroom"),
        RoomId.GirlsBedroom => Loc.T("Kamar Kakak & Adik", "Sisters' Bedroom"),
        RoomId.Playroom => Loc.T("Ruang Bermain", "Playroom"),
        RoomId.Library => Loc.T("Perpustakaan", "Library"),
        RoomId.Garage => Loc.T("Garasi", "Garage"),
        RoomId.Workshop => Loc.T("Bengkel Ayah", "Workshop"),
        RoomId.SecretRoom => Loc.T("Ruang Rahasia", "Secret Room"),
        RoomId.FrontYard => Loc.T("Halaman Depan", "Front Yard"),
        RoomId.Backyard => Loc.T("Halaman Belakang", "Backyard"),
        RoomId.Garden => Loc.T("Kebun", "Garden"),
        RoomId.Pool => Loc.T("Kolam Renang", "Swimming Pool"),
        _ => Loc.T("Rumah Pohon", "Tree House"),
    };

    public static string Description(RoomId id) => id switch
    {
        RoomId.GirlsBedroom => Loc.T("Kamar sendiri untuk Kak Nara dan Dinda. Kakak bisa menghias dindingnya.", "A room of their own for Nara and Dinda."),
        RoomId.Playroom => Loc.T("Mainan, matras dan tempat main saat hujan.", "Toys, mats and a place to play on rainy days."),
        RoomId.Library => Loc.T("Rak buku besar. Dinda suka membaca buku bahasa Inggris di sini.", "Big bookshelves. Dinda loves reading English books here."),
        RoomId.Garage => Loc.T("Tempat mobil keluarga. Membuka perjalanan dengan mobil.", "Home of the family car. Unlocks car trips."),
        RoomId.Workshop => Loc.T("Meja kerja Ayah untuk memperbaiki dan membuat kerajinan.", "Father's workbench for repairs and crafts."),
        RoomId.SecretRoom => Loc.T("Ruang rahasia di balik rak buku! Tempat aman saat darurat.", "A secret room behind the bookshelf! A safe room in emergencies."),
        RoomId.Garden => Loc.T("Bunga dan kebun sayur. Hasil panen bisa dijual.", "Flowers and a vegetable patch. Sell the harvest."),
        RoomId.Pool => Loc.T("Kolam renang keluarga untuk berlatih berenang.", "A family pool to practise swimming."),
        RoomId.TreeHouse => Loc.T("Rumah pohon untuk petualangan dan markas rahasia anak-anak.", "A tree house for adventures and the kids' secret base."),
        _ => Loc.T("Bagian dari rumah keluarga.", "Part of the family home."),
    };

    public static string Icon(RoomId id) => id switch
    {
        RoomId.LivingRoom => "🛋",
        RoomId.Kitchen => "🍳",
        RoomId.Hall => "🚪",
        RoomId.ParentsBedroom => "🛏",
        RoomId.KidsBedroom => "🧸",
        RoomId.Bathroom => "🛁",
        RoomId.GirlsBedroom => "🎀",
        RoomId.Playroom => "🧩",
        RoomId.Library => "📚",
        RoomId.Garage => "🚗",
        RoomId.Workshop => "🔧",
        RoomId.SecretRoom => "🗝",
        RoomId.FrontYard => "🏡",
        RoomId.Backyard => "🌳",
        RoomId.Garden => "🌻",
        RoomId.Pool => "🏊",
        _ => "🌲",
    };

    public static string HouseTitle(int indoorRooms) => indoorRooms switch
    {
        <= 6 => Loc.T("Rumah Kecil", "Small Home"),
        <= 8 => Loc.T("Rumah Keluarga", "Family Home"),
        <= 10 => Loc.T("Rumah Besar", "Large Home"),
        _ => Loc.T("Rumah Impian", "Dream Home"),
    };
}
