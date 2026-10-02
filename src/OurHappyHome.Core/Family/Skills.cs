namespace OurHappyHome.Core.Family;

public enum SkillKind
{
    Cooking,
    Repair,
    Building,
    Driving,
    Outdoor,
    Protection,
    Shopping,
    Gardening,
    Household,
    Care,
    Drawing,
    Music,
    Fashion,
    Photography,
    Animals,
    Cycling,
    Swimming,
    Reading,
    Sports,
    Crafting,
    FirstAid,
    English,
    Math,
    Science,
    Art,
    Computer,
}

/// <summary>Skill levels 0-10; each level needs a little more practice than the last.</summary>
public sealed class Skills
{
    public const float MaxLevel = 10f;

    public Dictionary<SkillKind, float> Levels { get; set; } = [];

    public float this[SkillKind kind]
    {
        get => Levels.TryGetValue(kind, out float v) ? v : 0f;
        set => Levels[kind] = Math.Clamp(value, 0f, MaxLevel);
    }

    public int Level(SkillKind kind) => (int)this[kind];

    /// <summary>Adds practice; returns true when a whole level was gained.</summary>
    public bool Practice(SkillKind kind, float amount)
    {
        float before = this[kind];
        float slowdown = 1f + (before * 0.25f);
        this[kind] = before + (amount / slowdown);
        return (int)this[kind] > (int)before;
    }

    public static string Name(SkillKind kind) => kind switch
    {
        SkillKind.Cooking => Loc.T("Memasak", "Cooking"),
        SkillKind.Repair => Loc.T("Memperbaiki", "Repair"),
        SkillKind.Building => Loc.T("Membangun", "Building"),
        SkillKind.Driving => Loc.T("Menyetir", "Driving"),
        SkillKind.Outdoor => Loc.T("Alam Bebas", "Outdoor"),
        SkillKind.Protection => Loc.T("Melindungi", "Protection"),
        SkillKind.Shopping => Loc.T("Belanja", "Shopping"),
        SkillKind.Gardening => Loc.T("Berkebun", "Gardening"),
        SkillKind.Household => Loc.T("Rumah Tangga", "Household"),
        SkillKind.Care => Loc.T("Merawat", "Family Care"),
        SkillKind.Drawing => Loc.T("Menggambar", "Drawing"),
        SkillKind.Music => Loc.T("Musik", "Music"),
        SkillKind.Fashion => Loc.T("Fesyen", "Fashion"),
        SkillKind.Photography => Loc.T("Fotografi", "Photography"),
        SkillKind.Animals => Loc.T("Merawat Hewan", "Animal Care"),
        SkillKind.Cycling => Loc.T("Bersepeda", "Cycling"),
        SkillKind.Swimming => Loc.T("Berenang", "Swimming"),
        SkillKind.Reading => Loc.T("Membaca", "Reading"),
        SkillKind.Sports => Loc.T("Olahraga", "Sports"),
        SkillKind.Crafting => Loc.T("Kerajinan", "Crafting"),
        SkillKind.FirstAid => Loc.T("P3K", "First Aid"),
        SkillKind.English => Loc.T("Bahasa Inggris", "English"),
        SkillKind.Math => Loc.T("Matematika", "Mathematics"),
        SkillKind.Science => Loc.T("IPA", "Science"),
        SkillKind.Art => Loc.T("Seni", "Art"),
        _ => Loc.T("Komputer", "Computer"),
    };

    public static string Icon(SkillKind kind) => kind switch
    {
        SkillKind.Cooking => "🍳",
        SkillKind.Repair => "🔧",
        SkillKind.Building => "🔨",
        SkillKind.Driving => "🚗",
        SkillKind.Outdoor => "🏕",
        SkillKind.Protection => "🛡",
        SkillKind.Shopping => "🛒",
        SkillKind.Gardening => "🌱",
        SkillKind.Household => "🧹",
        SkillKind.Care => "🤗",
        SkillKind.Drawing => "🖍",
        SkillKind.Music => "🎵",
        SkillKind.Fashion => "👗",
        SkillKind.Photography => "📷",
        SkillKind.Animals => "🐾",
        SkillKind.Cycling => "🚲",
        SkillKind.Swimming => "🏊",
        SkillKind.Reading => "📚",
        SkillKind.Sports => "⚽",
        SkillKind.Crafting => "✂",
        SkillKind.FirstAid => "🩹",
        SkillKind.English => "🔤",
        SkillKind.Math => "➗",
        SkillKind.Science => "🔬",
        SkillKind.Art => "🎨",
        _ => "💻",
    };
}

/// <summary>Who someone is: traits, likes, fears, hobbies and long-term goals.</summary>
public sealed class Personality
{
    public List<string> Traits { get; set; } = [];
    public List<string> Likes { get; set; } = [];
    public List<string> Dislikes { get; set; } = [];
    public List<string> Fears { get; set; } = [];
    public List<SkillKind> Hobbies { get; set; } = [];
    public List<string> Goals { get; set; } = [];

    /// <summary>How easily scared (0-1): thunder, darkness, strangers.</summary>
    public float Fearfulness { get; set; } = 0.3f;

    /// <summary>How much they seek company (0-1).</summary>
    public float Sociability { get; set; } = 0.5f;

    /// <summary>Morning person (wakes early) vs sleepyhead.</summary>
    public float WakeHour { get; set; } = 6.5f;

    public float BedHour { get; set; } = 21.5f;
}
