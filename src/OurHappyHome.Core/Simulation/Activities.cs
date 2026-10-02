using OurHappyHome.Core.Family;

namespace OurHappyHome.Core.Simulation;

public enum ActivityId
{
    Idle,
    Wander,
    Sleep,
    Nap,
    Eat,
    Snack,
    Cook,
    MakeDrink,
    Shower,
    Toilet,
    WashHands,
    WatchTV,
    Read,
    Homework,
    Draw,
    PlayMusic,
    Play,
    PlayOutside,
    Swim,
    Garden,
    WaterPlants,
    Repair,
    FixPower,
    Craft,
    Clean,
    Laundry,
    Chat,
    PlayWithPet,
    Computer,
    Stargaze,
    SellLemonade,
    Bbq,
    Exercise,
    GetDressed,
    CheckCameras,
    Relax,
    FirstAid,
    School,
    Work,
    Errand,
    Comfort,
    Hide,
}

public enum AgeGroup
{
    Any,
    Adult,
    Child,
}

/// <summary>
/// What an activity does per game minute and how it looks. Rates are applied
/// while the activity is being performed (not while walking to it).
/// </summary>
public sealed record ActivityDef(
    ActivityId Id,
    string NameId,
    string NameEn,
    string Icon,
    string Animation,
    float Minutes,
    (NeedKind Need, float PerMinute)[] Rates,
    SkillKind? Skill = null,
    float SkillPerMinute = 0f,
    float StaminaPerMinute = 0f,
    AgeGroup Ages = AgeGroup.Any,
    bool NeedsFurniture = true,
    bool Social = false,
    bool Away = false,
    bool Demanding = false)
{
    public string Name => Loc.T(NameId, NameEn);

    public float Rate(NeedKind need)
    {
        foreach ((NeedKind n, float r) in Rates)
        {
            if (n == need)
            {
                return r;
            }
        }

        return 0f;
    }

    public bool AllowedFor(MemberId member) => Ages switch
    {
        AgeGroup.Adult => FamilyNames.IsAdult(member),
        AgeGroup.Child => FamilyNames.IsChild(member),
        _ => true,
    };
}

public static class ActivityCatalog
{
    private static readonly ActivityDef[] Defs =
    [
        new(ActivityId.Idle, "Santai", "Idle", "🙂", "Idle", 8, [(NeedKind.Energy, 0.05f)], NeedsFurniture: false, StaminaPerMinute: 0.6f),
        new(ActivityId.Wander, "Jalan-jalan", "Wander", "🚶", "Walk", 6, [(NeedKind.Fun, 0.1f)], NeedsFurniture: false),
        new(ActivityId.Sleep, "Tidur", "Sleep", "💤", "Sleep", 480, [(NeedKind.Sleep, 0.24f), (NeedKind.Energy, 0.15f), (NeedKind.Health, 0.01f)], StaminaPerMinute: 0.5f),
        new(ActivityId.Nap, "Tidur siang", "Nap", "😴", "Sleep", 45, [(NeedKind.Sleep, 0.3f), (NeedKind.Energy, 0.2f)], StaminaPerMinute: 0.8f),
        new(ActivityId.Eat, "Makan", "Eat", "🍽", "Sit", 25, [(NeedKind.Hunger, 2.6f), (NeedKind.Social, 0.4f), (NeedKind.Energy, 0.4f)], StaminaPerMinute: 0.5f),
        new(ActivityId.Snack, "Camilan", "Snack", "🍎", "Idle", 6, [(NeedKind.Hunger, 3.2f)]),
        new(ActivityId.Cook, "Memasak", "Cook", "🍳", "Cook", 40, [(NeedKind.Fun, 0.2f)], SkillKind.Cooking, 0.03f, -0.15f),
        new(ActivityId.MakeDrink, "Membuat minuman", "Make a drink", "🥤", "Cook", 8, [(NeedKind.Fun, 0.3f)], SkillKind.Cooking, 0.02f),
        new(ActivityId.Shower, "Mandi", "Shower", "🚿", "Idle", 15, [(NeedKind.Hygiene, 5f), (NeedKind.Energy, 0.2f)]),
        new(ActivityId.Toilet, "Ke toilet", "Toilet", "🚽", "Sit", 5, [(NeedKind.Hygiene, 1.5f)]),
        new(ActivityId.WashHands, "Cuci tangan", "Wash hands", "🧼", "Idle", 3, [(NeedKind.Hygiene, 3f)]),
        new(ActivityId.WatchTV, "Nonton TV", "Watch TV", "📺", "Sit", 60, [(NeedKind.Fun, 1.2f), (NeedKind.Energy, 0.05f), (NeedKind.Social, 0.2f)], StaminaPerMinute: 0.6f),
        new(ActivityId.Read, "Membaca", "Read", "📖", "Read", 40, [(NeedKind.Fun, 0.7f)], SkillKind.Reading, 0.03f),
        new(ActivityId.Homework, "Belajar / PR", "Homework", "✏", "Sit", 45, [(NeedKind.Fun, -0.15f)], SkillKind.Math, 0.04f, Ages: AgeGroup.Child),
        new(ActivityId.Draw, "Menggambar", "Draw", "🖍", "Read", 50, [(NeedKind.Fun, 1.0f)], SkillKind.Drawing, 0.04f),
        new(ActivityId.PlayMusic, "Main musik", "Play music", "🎹", "Work", 40, [(NeedKind.Fun, 1.1f)], SkillKind.Music, 0.04f),
        new(ActivityId.Play, "Bermain", "Play", "🧸", "Cheer", 40, [(NeedKind.Fun, 1.6f), (NeedKind.Social, 0.4f)], SkillKind.Crafting, 0.01f, -0.1f, AgeGroup.Child),
        new(ActivityId.PlayOutside, "Main di luar", "Play outside", "🛝", "Cheer", 40, [(NeedKind.Fun, 1.8f), (NeedKind.Hygiene, -0.3f)], SkillKind.Sports, 0.03f, -0.35f, Demanding: true),
        new(ActivityId.Swim, "Berenang", "Swim", "🏊", "Run", 40, [(NeedKind.Fun, 1.5f), (NeedKind.Hygiene, 0.3f)], SkillKind.Swimming, 0.05f, -0.5f, NeedsFurniture: false, Demanding: true),
        new(ActivityId.Garden, "Berkebun", "Garden", "🌱", "Work", 40, [(NeedKind.Fun, 0.5f), (NeedKind.Hygiene, -0.4f)], SkillKind.Gardening, 0.04f, -0.2f),
        new(ActivityId.WaterPlants, "Siram tanaman", "Water plants", "🚿", "Work", 8, [(NeedKind.Fun, 0.3f)], SkillKind.Gardening, 0.03f),
        new(ActivityId.Repair, "Memperbaiki", "Repair", "🔧", "Work", 30, [(NeedKind.Hygiene, -0.3f)], SkillKind.Repair, 0.05f, -0.4f, Demanding: true),
        new(ActivityId.FixPower, "Periksa listrik", "Check the fuse box", "⚡", "Work", 10, [], SkillKind.Repair, 0.04f, -0.2f),
        new(ActivityId.Craft, "Membuat kerajinan", "Crafting", "✂", "Work", 45, [(NeedKind.Fun, 0.8f)], SkillKind.Crafting, 0.04f, -0.1f),
        new(ActivityId.Clean, "Bersih-bersih", "Clean up", "🧹", "Work", 25, [(NeedKind.Hygiene, -0.2f)], SkillKind.Household, 0.04f, -0.3f),
        new(ActivityId.Laundry, "Mencuci baju", "Laundry", "🧺", "Work", 20, [], SkillKind.Household, 0.03f, -0.15f),
        new(ActivityId.Chat, "Ngobrol", "Chat", "💬", "Talk", 20, [(NeedKind.Social, 2.2f), (NeedKind.Fun, 0.5f)], NeedsFurniture: false, Social: true),
        new(ActivityId.PlayWithPet, "Main dengan hewan", "Play with pet", "🐾", "Cheer", 20, [(NeedKind.Fun, 1.2f), (NeedKind.Social, 0.5f)], SkillKind.Animals, 0.05f, -0.2f, NeedsFurniture: false),
        new(ActivityId.Computer, "Main komputer", "Use the computer", "💻", "Sit", 45, [(NeedKind.Fun, 1.1f)], SkillKind.Computer, 0.04f),
        new(ActivityId.Stargaze, "Lihat bintang", "Stargaze", "🔭", "Idle", 30, [(NeedKind.Fun, 1.0f)], SkillKind.Science, 0.04f),
        new(ActivityId.SellLemonade, "Jualan limun", "Sell lemonade", "🍋", "Talk", 60, [(NeedKind.Social, 0.8f), (NeedKind.Fun, 0.4f)], SkillKind.Shopping, 0.03f, Ages: AgeGroup.Child),
        new(ActivityId.Bbq, "Bakar sate", "Barbecue", "🍢", "Cook", 40, [(NeedKind.Fun, 0.6f), (NeedKind.Social, 0.4f)], SkillKind.Cooking, 0.03f),
        new(ActivityId.Exercise, "Olahraga", "Exercise", "🤸", "Cheer", 25, [(NeedKind.Health, 0.15f), (NeedKind.Hygiene, -0.5f), (NeedKind.Fun, 0.4f)], SkillKind.Sports, 0.05f, -0.6f, NeedsFurniture: false, Demanding: true),
        new(ActivityId.GetDressed, "Ganti baju", "Get dressed", "👕", "Idle", 6, [(NeedKind.Hygiene, 1.0f), (NeedKind.Fun, 0.3f)], SkillKind.Fashion, 0.05f),
        new(ActivityId.CheckCameras, "Cek kamera", "Check cameras", "📹", "Idle", 4, [], SkillKind.Protection, 0.05f),
        new(ActivityId.Relax, "Duduk santai", "Relax", "🛋", "Sit", 30, [(NeedKind.Energy, 0.35f), (NeedKind.Fun, 0.2f)], StaminaPerMinute: 1.0f),
        new(ActivityId.FirstAid, "Pertolongan pertama", "First aid", "🩹", "Work", 8, [(NeedKind.Health, 2f)], SkillKind.FirstAid, 0.05f),
        new(ActivityId.School, "Sekolah", "School", "🏫", "Idle", 360, [(NeedKind.Social, 0.12f), (NeedKind.Fun, 0.02f), (NeedKind.Hunger, 0.03f)], SkillKind.English, 0.012f, Ages: AgeGroup.Child, NeedsFurniture: false, Away: true),
        new(ActivityId.Work, "Bekerja", "Work", "💼", "Idle", 480, [(NeedKind.Fun, -0.03f), (NeedKind.Hunger, 0.03f)], SkillKind.Building, 0.01f, Ages: AgeGroup.Adult, NeedsFurniture: false, Away: true),
        new(ActivityId.Errand, "Belanja ke pasar", "Run errands", "🛍", "Idle", 75, [(NeedKind.Social, 0.2f)], SkillKind.Shopping, 0.02f, Ages: AgeGroup.Adult, NeedsFurniture: false, Away: true),
        new(ActivityId.Comfort, "Menenangkan", "Comfort", "🤗", "Talk", 10, [(NeedKind.Social, 1.5f)], SkillKind.Care, 0.05f, NeedsFurniture: false, Social: true),
        new(ActivityId.Hide, "Berlindung", "Take shelter", "🛡", "Scared", 30, [], NeedsFurniture: false),
    ];

    private static readonly Dictionary<ActivityId, ActivityDef> ById = Defs.ToDictionary(d => d.Id);

    public static IReadOnlyList<ActivityDef> All => Defs;

    public static ActivityDef Get(ActivityId id) => ById[id];
}
