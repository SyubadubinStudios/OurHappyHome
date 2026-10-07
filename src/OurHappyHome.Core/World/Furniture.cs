using System.Numerics;
using OurHappyHome.Core.Simulation;

namespace OurHappyHome.Core.World;

public enum AnchorPose
{
    Stand,
    Sit,
    Lie,
}

public enum FurnitureCategory
{
    Living,
    Bedroom,
    Kitchen,
    Bathroom,
    Study,
    Hobby,
    Outdoor,
    Safety,
    Decor,
}

/// <summary>
/// Where a character stands, sits or lies when using a piece of furniture,
/// in the furniture's local frame (front faces +Z). Yaw 0 faces +Z, 180 faces
/// the furniture from its front.
/// </summary>
public readonly record struct UseSlot(Vector3 Offset, float YawDegrees, AnchorPose Pose);

public sealed record FurnitureDef(
    string Id,
    string NameId,
    string NameEn,
    FurnitureCategory Category,
    long Price,
    Vector2 Footprint,
    float Height,
    string? Model,
    string Color,
    ActivityId[] Activities,
    UseSlot[] Slots,
    bool NeedsPower = false,
    bool Breakable = false,
    bool Outdoor = false,
    int MinChapter = 1,
    bool EmitsLight = false,
    bool WallMounted = false)
{
    public string Name => Loc.T(NameId, NameEn);
}

/// <summary>A placed piece of furniture.</summary>
public sealed class FurnitureItem
{
    public int Uid { get; set; }
    public string DefId { get; set; } = "";
    public RoomId Room { get; set; }
    public Vector2 Position { get; set; }

    /// <summary>Quarter turns (0-3), counter-clockwise seen from above.</summary>
    public int Rotation { get; set; }

    public float Condition { get; set; } = 100f;
    public bool Broken { get; set; }

    /// <summary>Member currently using each slot (null = free).</summary>
    public MemberSlot[] Occupants { get; set; } = [];

    public FurnitureDef Def => FurnitureCatalog.Get(DefId);

    public float Yaw => Rotation * MathF.PI / 2f;

    public Vector2 Forward => new(MathF.Sin(Yaw), MathF.Cos(Yaw));

    /// <summary>Footprint after rotation.</summary>
    public Rect Bounds
    {
        get
        {
            Vector2 size = Def.Footprint;
            if (Rotation % 2 == 1)
            {
                size = new Vector2(size.Y, size.X);
            }

            return Rect.FromCenter(Position, size);
        }
    }

    public Vector3 SlotWorldPosition(int slot)
    {
        UseSlot s = Def.Slots[slot];
        Vector2 local = new(s.Offset.X, s.Offset.Z);
        Vector2 rotated = Rotate(local, Yaw);
        return new Vector3(Position.X + rotated.X, s.Offset.Y, Position.Y + rotated.Y);
    }

    public float SlotWorldYaw(int slot) => Yaw + (Def.Slots[slot].YawDegrees * MathF.PI / 180f);

    /// <summary>Point on the floor from which the slot is approached (in front of the item).</summary>
    public Vector2 ApproachPoint(int slot)
    {
        UseSlot s = Def.Slots[slot];
        Vector3 p = SlotWorldPosition(slot);
        if (s.Pose == AnchorPose.Stand)
        {
            return new Vector2(p.X, p.Z);
        }

        // Sit / lie: step out of the furniture's footprint in the most natural
        // direction (the way the seat faces, else behind it, else the sides).
        float yaw = SlotWorldYaw(slot);
        Vector2 facing = new(MathF.Sin(yaw), MathF.Cos(yaw));
        Vector2 side = new(facing.Y, -facing.X);
        Vector2 seat = new(p.X, p.Z);
        Vector2[] directions = s.Pose == AnchorPose.Lie
            ? [side * MathF.Sign(s.Offset.X == 0 ? 1 : s.Offset.X), -side * MathF.Sign(s.Offset.X == 0 ? 1 : s.Offset.X), facing, -facing]
            : [facing, -facing, side, -side];
        Rect keepOut = Bounds.Inflate(0.3f);
        Vector2? best = null;
        float bestLength = float.MaxValue;
        foreach (Vector2 dir in directions)
        {
            for (float d = 0.45f; d <= 2.5f; d += 0.1f)
            {
                Vector2 candidate = seat + (dir * d);
                if (!keepOut.Contains(candidate))
                {
                    // Prefer the first direction unless another is much shorter.
                    if (best is null || d < bestLength - 0.6f)
                    {
                        best = candidate;
                        bestLength = d;
                    }

                    break;
                }
            }
        }

        return best ?? seat + (facing * 0.6f);
    }

    /// <summary>The slot this member already holds, else the first free one, else -1.</summary>
    public int FreeSlot(Family.MemberId member)
    {
        EnsureOccupants();
        for (int i = 0; i < Occupants.Length; i++)
        {
            if (Occupants[i].Member == member)
            {
                return i;
            }
        }

        for (int i = 0; i < Occupants.Length; i++)
        {
            if (Occupants[i].Member is null)
            {
                return i;
            }
        }

        return -1;
    }

    public void EnsureOccupants()
    {
        if (Occupants.Length != Def.Slots.Length)
        {
            Occupants = new MemberSlot[Def.Slots.Length];
        }
    }

    public void Release(Family.MemberId member)
    {
        EnsureOccupants();
        for (int i = 0; i < Occupants.Length; i++)
        {
            if (Occupants[i].Member == member)
            {
                Occupants[i] = default;
            }
        }
    }

    public bool Usable => !Broken;

    private static Vector2 Rotate(Vector2 v, float yaw)
    {
        // Rotation about +Y: local +Z maps to (sin, cos).
        float c = MathF.Cos(yaw);
        float s = MathF.Sin(yaw);
        return new Vector2((v.X * c) + (v.Y * s), (-v.X * s) + (v.Y * c));
    }
}

public record struct MemberSlot(Family.MemberId? Member);

public static class FurnitureCatalog
{
    private static readonly UseSlot[] FrontStand = [new(new(0, 0, 0.75f), 180, AnchorPose.Stand)];

    private static UseSlot[] StandAt(float distance) => [new(new(0, 0, distance), 180, AnchorPose.Stand)];

    public static readonly IReadOnlyList<FurnitureDef> All =
    [
        // ---- living room
        new("sofa", "Sofa Kuning", "Yellow Sofa", FurnitureCategory.Living, 900_000, new(2.2f, 0.95f), 0.9f, "sofa", "#E3B23C",
            [ActivityId.WatchTV, ActivityId.Nap, ActivityId.Relax, ActivityId.Chat],
            [new(new(-0.62f, 0.42f, 0.08f), 0, AnchorPose.Sit), new(new(0, 0.42f, 0.08f), 0, AnchorPose.Sit), new(new(0.62f, 0.42f, 0.08f), 0, AnchorPose.Sit)]),
        new("armchair", "Kursi Santai", "Armchair", FurnitureCategory.Living, 450_000, new(0.95f, 0.9f), 0.9f, null, "#C0674B",
            [ActivityId.Read, ActivityId.Relax, ActivityId.Nap],
            [new(new(0, 0.42f, 0.05f), 0, AnchorPose.Sit)]),
        new("tv", "Lemari TV", "TV Cabinet", FurnitureCategory.Living, 1_400_000, new(1.6f, 0.55f), 1.35f, "tv-cabinet", "#8B5E3C",
            [ActivityId.WatchTV], StandAt(1.6f), NeedsPower: true, Breakable: true),
        new("bookshelf", "Rak Buku", "Bookshelf", FurnitureCategory.Study, 700_000, new(1.0f, 0.5f), 1.75f, "bookshelf", "#9C6B3F",
            [ActivityId.Read], FrontStand),
        new("coffee-table", "Meja Kopi", "Coffee Table", FurnitureCategory.Living, 250_000, new(1.1f, 0.6f), 0.42f, null, "#B7835A", [], []),
        new("rug", "Karpet Bulat", "Round Rug", FurnitureCategory.Decor, 180_000, new(2.4f, 1.8f), 0.02f, null, "#D98A6C", [], []),
        new("lamp", "Lampu Berdiri", "Floor Lamp", FurnitureCategory.Decor, 150_000, new(0.4f, 0.4f), 1.6f, null, "#F6E7B0",
            [ActivityId.Repair], FrontStand, NeedsPower: true, Breakable: true, EmitsLight: true),
        new("plant", "Tanaman Hias", "House Plant", FurnitureCategory.Decor, 120_000, new(0.5f, 0.5f), 1.1f, null, "#4F9D4F",
            [ActivityId.WaterPlants], FrontStand),
        new("piano", "Keyboard Musik", "Music Keyboard", FurnitureCategory.Hobby, 1_800_000, new(1.4f, 0.5f), 1.0f, null, "#2C2C34",
            [ActivityId.PlayMusic], [new(new(0, 0, 0.6f), 180, AnchorPose.Stand)], NeedsPower: true, MinChapter: 2),

        // ---- kitchen
        new("kitchen", "Kompor & Konter", "Stove & Counter", FurnitureCategory.Kitchen, 2_000_000, new(2.4f, 0.98f), 1.2f, "kitchen", "#9ED9C3",
            [ActivityId.Cook, ActivityId.MakeDrink, ActivityId.Clean],
            [new(new(-0.55f, 0, 0.85f), 180, AnchorPose.Stand), new(new(0.6f, 0, 0.85f), 180, AnchorPose.Stand)], NeedsPower: true, Breakable: true),
        new("fridge", "Kulkas", "Fridge", FurnitureCategory.Kitchen, 1_600_000, new(1.0f, 0.6f), 1.65f, "fridge", "#F3EBD3",
            [ActivityId.Snack], FrontStand, NeedsPower: true, Breakable: true),
        new("dining", "Meja Makan", "Dining Table", FurnitureCategory.Kitchen, 1_100_000, new(1.5f, 1.5f), 0.75f, "dining-set", "#B98B5E",
            [ActivityId.Eat, ActivityId.Homework, ActivityId.Chat],
            [
                new(new(0f, 0.45f, 0.62f), 180, AnchorPose.Sit), new(new(0f, 0.45f, -0.62f), 0, AnchorPose.Sit),
                new(new(0.62f, 0.45f, 0f), -90, AnchorPose.Sit), new(new(-0.62f, 0.45f, 0f), 90, AnchorPose.Sit),
                new(new(0.95f, 0.45f, 0.6f), -135, AnchorPose.Sit),
            ]),
        new("blender", "Blender Jus", "Juice Blender", FurnitureCategory.Kitchen, 350_000, new(0.5f, 0.45f), 1.0f, null, "#EF8A8A",
            [ActivityId.MakeDrink], FrontStand, NeedsPower: true, Breakable: true),
        new("washer", "Mesin Cuci", "Washing Machine", FurnitureCategory.Bathroom, 1_300_000, new(0.7f, 0.7f), 0.9f, null, "#EDEFF2",
            [ActivityId.Laundry], FrontStand, NeedsPower: true, Breakable: true),

        // ---- bedrooms
        new("double-bed", "Ranjang Ayah Ibu", "Double Bed", FurnitureCategory.Bedroom, 1_500_000, new(1.9f, 1.8f), 1.3f, "double-bed", "#F2F2F2",
            [ActivityId.Sleep, ActivityId.Nap],
            [new(new(-0.42f, 0.62f, 0.05f), 0, AnchorPose.Lie), new(new(0.42f, 0.62f, 0.05f), 0, AnchorPose.Lie)]),
        new("bunk-bed", "Ranjang Loteng", "Loft Bed", FurnitureCategory.Bedroom, 1_200_000, new(1.7f, 2.1f), 1.5f, "bunk-bed", "#C99B66",
            [ActivityId.Sleep, ActivityId.Nap],
            [new(new(0.1f, 0.95f, 0.05f), 0, AnchorPose.Lie)]),
        new("single-bed", "Ranjang Kecil", "Single Bed", FurnitureCategory.Bedroom, 800_000, new(1.0f, 2.0f), 0.6f, null, "#9CC3E6",
            [ActivityId.Sleep, ActivityId.Nap],
            [new(new(0, 0.48f, 0.05f), 0, AnchorPose.Lie)]),
        new("wardrobe", "Lemari Baju", "Wardrobe", FurnitureCategory.Bedroom, 650_000, new(1.2f, 0.6f), 2.0f, null, "#A9764B",
            [ActivityId.GetDressed], FrontStand),
        new("desk", "Meja Belajar", "Study Desk", FurnitureCategory.Study, 750_000, new(1.6f, 1.3f), 1.4f, "desk", "#C49A6C",
            [ActivityId.Homework, ActivityId.Draw, ActivityId.Computer],
            [new(new(0.05f, 0.45f, 0.25f), 180, AnchorPose.Sit)], NeedsPower: true),
        new("toy-box", "Kotak Mainan", "Toy Box", FurnitureCategory.Hobby, 300_000, new(0.9f, 0.55f), 0.55f, null, "#E85D75",
            [ActivityId.Play], [new(new(-0.3f, 0, 0.8f), 180, AnchorPose.Stand), new(new(0.35f, 0, 0.8f), 180, AnchorPose.Stand)]),
        new("easel", "Kanvas Lukis", "Painting Easel", FurnitureCategory.Hobby, 400_000, new(0.8f, 0.7f), 1.6f, null, "#D7B377",
            [ActivityId.Draw], FrontStand),
        new("dog-bed", "Kasur Anjing", "Dog Bed", FurnitureCategory.Decor, 200_000, new(0.9f, 0.7f), 0.2f, null, "#8E5A3C", [], []),
        new("aquarium", "Akuarium", "Aquarium", FurnitureCategory.Decor, 650_000, new(1.0f, 0.45f), 1.15f, null, "#7FC8E8",
            [ActivityId.Relax], StandAt(0.6f), NeedsPower: true),
        new("beanbag", "Kursi Bean Bag", "Beanbag", FurnitureCategory.Living, 300_000, new(0.9f, 0.9f), 0.6f, null, "#F28C38",
            [ActivityId.Read, ActivityId.Relax], [new(new(0, 0.3f, 0.05f), 0, AnchorPose.Sit)]),
        new("painting", "Lukisan Dinding", "Wall Painting", FurnitureCategory.Decor, 220_000, new(0.9f, 0.1f), 1.6f, null, "#F2B880", [], [], WallMounted: true),
        new("arcade", "Mesin Arkade", "Arcade Machine", FurnitureCategory.Hobby, 2_200_000, new(0.8f, 0.7f), 1.8f, null, "#7B4AE2",
            [ActivityId.Play], StandAt(0.7f), NeedsPower: true, Breakable: true, MinChapter: 2),

        // ---- bathroom
        new("toilet", "Kloset", "Toilet", FurnitureCategory.Bathroom, 600_000, new(0.5f, 0.7f), 0.8f, null, "#FFFFFF",
            [ActivityId.Toilet], [new(new(0, 0.42f, 0.12f), 0, AnchorPose.Sit)], Breakable: true),
        new("shower", "Bak Mandi & Shower", "Bathtub & Shower", FurnitureCategory.Bathroom, 1_200_000, new(1.7f, 0.8f), 0.6f, null, "#E6F4F8",
            [ActivityId.Shower], [new(new(0, 0, 0.0f), 0, AnchorPose.Stand)], Breakable: true),
        new("sink", "Wastafel", "Sink", FurnitureCategory.Bathroom, 400_000, new(0.7f, 0.45f), 0.9f, null, "#F4F6F8",
            [ActivityId.WashHands, ActivityId.Repair], FrontStand, Breakable: true),

        // ---- safety
        new("fuse-box", "Panel Listrik", "Fuse Box", FurnitureCategory.Safety, 300_000, new(0.5f, 0.2f), 1.6f, null, "#9AA4AE",
            [ActivityId.FixPower], FrontStand, WallMounted: true),
        new("security", "Kamera & Alarm", "Cameras & Alarm Panel", FurnitureCategory.Safety, 1_500_000, new(0.5f, 0.2f), 1.6f, null, "#3B4B5C",
            [ActivityId.CheckCameras], FrontStand, NeedsPower: true, WallMounted: true, MinChapter: 2),
        new("extinguisher", "Alat Pemadam Api", "Fire Extinguisher", FurnitureCategory.Safety, 400_000, new(0.35f, 0.3f), 0.7f, null, "#D93A2B", [], [], WallMounted: true),
        new("first-aid", "Kotak P3K", "First Aid Kit", FurnitureCategory.Safety, 150_000, new(0.4f, 0.2f), 1.4f, null, "#FFFFFF",
            [ActivityId.FirstAid], FrontStand, WallMounted: true),

        // ---- hobby / big rooms
        new("workbench", "Meja Kerja", "Workbench", FurnitureCategory.Hobby, 900_000, new(1.8f, 0.75f), 0.95f, null, "#8D6E4F",
            [ActivityId.Craft, ActivityId.Repair], [new(new(-0.4f, 0, 0.75f), 180, AnchorPose.Stand), new(new(0.5f, 0, 0.75f), 180, AnchorPose.Stand)]),
        new("computer", "Komputer", "Computer", FurnitureCategory.Study, 3_000_000, new(1.2f, 0.7f), 1.2f, null, "#5E6B78",
            [ActivityId.Computer, ActivityId.Homework], [new(new(0, 0.45f, 0.55f), 180, AnchorPose.Sit)], NeedsPower: true, Breakable: true, MinChapter: 2),
        new("telescope", "Teleskop", "Telescope", FurnitureCategory.Hobby, 1_100_000, new(0.8f, 0.8f), 1.4f, null, "#3C4A7A",
            [ActivityId.Stargaze], FrontStand, MinChapter: 3),
        new("car", "Mobil Keluarga", "Family Car", FurnitureCategory.Outdoor, 0, new(1.9f, 4.0f), 1.5f, "car", "#6EC1E4",
            [ActivityId.Repair], [new(new(1.3f, 0, 0.8f), -90, AnchorPose.Stand)], Breakable: true, Outdoor: true),

        // ---- outdoor
        new("swing-set", "Ayunan & Perosotan", "Swing Set", FurnitureCategory.Outdoor, 1_000_000, new(3.0f, 2.0f), 1.8f, "swing-set", "#E05A47",
            [ActivityId.PlayOutside], [new(new(-0.6f, 0, 1.3f), 180, AnchorPose.Stand), new(new(0.6f, 0, 1.3f), 180, AnchorPose.Stand)], Outdoor: true),
        new("veg-patch", "Petak Sayur", "Vegetable Patch", FurnitureCategory.Outdoor, 350_000, new(2.2f, 1.2f), 0.35f, null, "#6B4F2E",
            [ActivityId.Garden], [new(new(-0.5f, 0, 0.95f), 180, AnchorPose.Stand), new(new(0.5f, 0, 0.95f), 180, AnchorPose.Stand)], Outdoor: true),
        new("flower-bush", "Semak Bunga", "Flower Bush", FurnitureCategory.Outdoor, 150_000, new(1.0f, 1.0f), 0.9f, "flower-bush", "#E57AA0",
            [ActivityId.WaterPlants], FrontStand, Outdoor: true),
        new("lemonade", "Kios Limun", "Lemonade Stand", FurnitureCategory.Outdoor, 300_000, new(1.4f, 0.7f), 1.6f, null, "#F7D547",
            [ActivityId.SellLemonade], [new(new(0, 0, -0.65f), 0, AnchorPose.Stand)], Outdoor: true),
        new("bbq", "Pemanggang Sate", "BBQ Grill", FurnitureCategory.Outdoor, 600_000, new(0.9f, 0.6f), 1.0f, null, "#3A3A3A",
            [ActivityId.Bbq], FrontStand, Outdoor: true, MinChapter: 2),
        new("trampoline", "Trampolin", "Trampoline", FurnitureCategory.Outdoor, 1_200_000, new(3.0f, 3.0f), 0.8f, null, "#3C7DD9",
            [ActivityId.PlayOutside], [new(new(0, 0.8f, 0), 0, AnchorPose.Stand)], Outdoor: true, MinChapter: 2),
        new("picnic", "Meja Piknik", "Picnic Table", FurnitureCategory.Outdoor, 700_000, new(1.8f, 1.6f), 0.75f, null, "#A97C50",
            [ActivityId.Eat, ActivityId.Chat],
            [new(new(-0.45f, 0.45f, 0.75f), 180, AnchorPose.Sit), new(new(0.45f, 0.45f, 0.75f), 180, AnchorPose.Sit),
             new(new(-0.45f, 0.45f, -0.75f), 0, AnchorPose.Sit), new(new(0.45f, 0.45f, -0.75f), 0, AnchorPose.Sit)], Outdoor: true),
        new("hammock", "Ayunan Jaring", "Hammock", FurnitureCategory.Outdoor, 450_000, new(1.0f, 2.6f), 1.4f, null, "#E9C46A",
            [ActivityId.Nap, ActivityId.Relax], [new(new(0, 0.55f, 0.05f), 0, AnchorPose.Lie)], Outdoor: true),
        new("tree", "Pohon", "Tree", FurnitureCategory.Outdoor, 250_000, new(1.2f, 1.2f), 5f, "tree", "#3E8E41", [], [], Outdoor: true),
    ];

    private static readonly Dictionary<string, FurnitureDef> ById = All.ToDictionary(d => d.Id);

    public static FurnitureDef Get(string id) => ById.TryGetValue(id, out FurnitureDef? def) ? def : ById["coffee-table"];

    public static bool Exists(string id) => ById.ContainsKey(id);

    public static string CategoryName(FurnitureCategory category) => category switch
    {
        FurnitureCategory.Living => Loc.T("Ruang Keluarga", "Living"),
        FurnitureCategory.Bedroom => Loc.T("Kamar Tidur", "Bedroom"),
        FurnitureCategory.Kitchen => Loc.T("Dapur", "Kitchen"),
        FurnitureCategory.Bathroom => Loc.T("Kamar Mandi", "Bathroom"),
        FurnitureCategory.Study => Loc.T("Belajar", "Study"),
        FurnitureCategory.Hobby => Loc.T("Hobi", "Hobby"),
        FurnitureCategory.Outdoor => Loc.T("Luar Rumah", "Outdoor"),
        FurnitureCategory.Safety => Loc.T("Keamanan", "Safety"),
        _ => Loc.T("Dekorasi", "Decor"),
    };
}
