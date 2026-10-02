using OurHappyHome.Core.Economy;

namespace OurHappyHome.Core.Cooking;

public enum CookQuality
{
    Failed,
    Poor,
    Normal,
    Delicious,
    Perfect,
}

public enum StepKind
{
    /// <summary>Stop a moving marker inside the green zone.</summary>
    Timing,

    /// <summary>Press quickly to fill the bar before time runs out.</summary>
    Mash,

    /// <summary>Hold and release when the gauge is in the zone.</summary>
    Hold,
}

public sealed record CookStep(string TextId, string TextEn, string Icon, StepKind Kind, float Difficulty)
{
    public string Text => Loc.T(TextId, TextEn);
}

public sealed record Recipe(
    string Id,
    string NameId,
    string NameEn,
    string Icon,
    (string Item, int Count)[] Ingredients,
    CookStep[] Steps,
    float Minutes,
    float MinSkill,
    int Servings,
    float Nutrition,
    long SellPrice,
    bool IsDrink = false,
    bool HealsSick = false,
    bool Celebration = false,
    bool GrillOnly = false)
{
    public string Name => Loc.T(NameId, NameEn);

    public bool CanMake(Inventory inventory) => Ingredients.All(i => inventory.Has(i.Item, i.Count));

    public string IngredientText => Ingredients.Length == 0
        ? Loc.T("Tanpa bahan", "No ingredients")
        : string.Join(", ", Ingredients.Select(i => $"{ItemCatalog.Get(i.Item).Icon} {i.Count}× {ItemCatalog.Get(i.Item).Name}"));
}

/// <summary>A cooked dish waiting to be eaten.</summary>
public sealed class PreparedFood
{
    public string RecipeId { get; set; } = "";
    public CookQuality Quality { get; set; }
    public int Servings { get; set; }
    public int CookedDay { get; set; }
    public List<Family.MemberId> Cooks { get; set; } = [];

    public Recipe Recipe => Recipes.Get(RecipeId);
}

public static class Recipes
{
    private static CookStep S(string id, string en, string icon, StepKind kind, float difficulty = 0.5f) => new(id, en, icon, kind, difficulty);

    public static readonly IReadOnlyList<Recipe> All =
    [
        new("fried-rice", "Nasi Goreng", "Fried Rice", "🍛", [("rice", 1), ("egg", 1), ("vegetables", 1)],
            [S("Potong sayuran", "Chop the vegetables", "🔪", StepKind.Mash, 0.4f), S("Tumis bumbu", "Fry the spices", "🔥", StepKind.Timing, 0.5f), S("Aduk nasi", "Stir the rice", "🥄", StepKind.Mash, 0.5f), S("Angkat tepat waktu", "Plate it at the right time", "🍽", StepKind.Timing, 0.55f)],
            35, 1, 5, 70, 15_000),
        new("eggs", "Telur Dadar", "Omelette", "🍳", [("egg", 2)],
            [S("Pecahkan telur", "Crack the eggs", "🥚", StepKind.Timing, 0.35f), S("Kocok telur", "Whisk", "🥄", StepKind.Mash, 0.3f), S("Balik telur", "Flip it", "🍳", StepKind.Timing, 0.5f)],
            15, 0, 3, 45, 8_000),
        new("chicken", "Ayam Goreng", "Fried Chicken", "🍗", [("chicken", 1), ("flour", 1)],
            [S("Lumuri tepung", "Coat with flour", "🌾", StepKind.Mash, 0.5f), S("Goreng sampai keemasan", "Fry until golden", "🔥", StepKind.Hold, 0.6f), S("Tiriskan", "Drain the oil", "🍗", StepKind.Timing, 0.6f)],
            40, 2, 5, 80, 20_000),
        new("pancakes", "Pancake", "Pancakes", "🥞", [("flour", 1), ("egg", 1), ("milk", 1), ("sugar", 1)],
            [S("Campur adonan", "Mix the batter", "🥣", StepKind.Mash, 0.45f), S("Tuang adonan", "Pour the batter", "🫗", StepKind.Hold, 0.5f), S("Balik pancake", "Flip the pancake", "🥞", StepKind.Timing, 0.65f), S("Tambah sirup", "Add syrup", "🍯", StepKind.Timing, 0.4f)],
            30, 1, 5, 60, 12_000),
        new("cupcakes", "Kue Mangkuk", "Cupcakes", "🧁", [("flour", 1), ("egg", 1), ("sugar", 1), ("butter", 1), ("chocolate", 1)],
            [S("Kocok mentega", "Cream the butter", "🧈", StepKind.Mash, 0.55f), S("Isi cetakan", "Fill the cups", "🧁", StepKind.Hold, 0.55f), S("Panggang", "Bake", "🔥", StepKind.Hold, 0.7f), S("Hias krim", "Decorate", "✨", StepKind.Timing, 0.65f)],
            50, 3, 8, 35, 10_000),
        new("pudding", "Puding Cokelat", "Chocolate Pudding", "🍮", [("milk", 1), ("sugar", 1), ("chocolate", 1)],
            [S("Didihkan susu", "Heat the milk", "🥛", StepKind.Hold, 0.5f), S("Aduk cokelat", "Stir in chocolate", "🍫", StepKind.Mash, 0.5f), S("Tuang ke cetakan", "Pour into moulds", "🍮", StepKind.Timing, 0.5f)],
            30, 2, 6, 30, 9_000),
        new("warm-drink", "Cokelat Hangat", "Hot Chocolate", "☕", [("milk", 1), ("chocolate", 1)],
            [S("Panaskan susu", "Warm the milk", "🥛", StepKind.Hold, 0.35f), S("Aduk", "Stir", "🥄", StepKind.Mash, 0.3f)],
            8, 0, 5, 15, 6_000, IsDrink: true),
        new("milk", "Segelas Susu", "Glass of Milk", "🥛", [("milk", 1)],
            [S("Tuang susu", "Pour the milk", "🫗", StepKind.Hold, 0.25f)],
            3, 0, 4, 12, 4_000, IsDrink: true),
        new("water", "Air Putih", "Water", "💧", [],
            [S("Tuang air", "Pour water", "💧", StepKind.Hold, 0.15f)],
            2, 0, 5, 4, 0, IsDrink: true),
        new("lemonade", "Es Limun", "Lemonade", "🍋", [("fruit", 1), ("sugar", 1)],
            [S("Peras lemon", "Squeeze lemons", "🍋", StepKind.Mash, 0.4f), S("Aduk dengan gula", "Stir with sugar", "🥄", StepKind.Timing, 0.4f)],
            10, 0, 8, 10, 5_000, IsDrink: true),
        new("soup", "Sup Ayam", "Chicken Soup", "🍲", [("chicken", 1), ("vegetables", 1)],
            [S("Potong ayam & sayur", "Chop chicken & vegetables", "🔪", StepKind.Mash, 0.5f), S("Rebus perlahan", "Simmer gently", "🍲", StepKind.Hold, 0.55f), S("Bumbui", "Season", "🧂", StepKind.Timing, 0.5f)],
            45, 2, 5, 65, 15_000, HealsSick: true),
        new("satay", "Sate Ayam", "Chicken Satay", "🍢", [("chicken", 2)],
            [S("Tusuk sate", "Skewer the chicken", "🍢", StepKind.Mash, 0.5f), S("Kipas arang", "Fan the charcoal", "🔥", StepKind.Mash, 0.6f), S("Balik sate", "Turn the skewers", "🔄", StepKind.Timing, 0.65f), S("Siram bumbu kacang", "Add peanut sauce", "🥜", StepKind.Timing, 0.5f)],
            40, 3, 6, 75, 18_000, GrillOnly: true),
        new("salad", "Salad Buah", "Fruit Salad", "🥗", [("fruit", 1), ("vegetables", 1)],
            [S("Potong buah", "Cut the fruit", "🔪", StepKind.Mash, 0.35f), S("Campur", "Toss", "🥗", StepKind.Timing, 0.35f)],
            12, 0, 4, 35, 8_000),
        new("birthday-cake", "Kue Ulang Tahun", "Birthday Cake", "🎂", [("flour", 2), ("egg", 2), ("sugar", 2), ("butter", 1), ("chocolate", 1)],
            [S("Kocok adonan", "Beat the batter", "🥣", StepKind.Mash, 0.6f), S("Panggang", "Bake", "🔥", StepKind.Hold, 0.7f), S("Lapisi krim", "Frost the cake", "🍰", StepKind.Timing, 0.7f), S("Pasang lilin", "Add the candles", "🕯", StepKind.Timing, 0.6f)],
            70, 4, 10, 40, 0, Celebration: true),
    ];

    private static readonly Dictionary<string, Recipe> ById = All.ToDictionary(r => r.Id);

    public static Recipe Get(string id) => ById[id];

    public static bool Exists(string id) => ById.ContainsKey(id);

    public static string QualityName(CookQuality quality) => quality switch
    {
        CookQuality.Failed => Loc.T("Gagal", "Failed"),
        CookQuality.Poor => Loc.T("Kurang", "Poor"),
        CookQuality.Normal => Loc.T("Biasa", "Normal"),
        CookQuality.Delicious => Loc.T("Lezat", "Delicious"),
        _ => Loc.T("Sempurna", "Perfect"),
    };

    public static string QualityStars(CookQuality quality) => quality switch
    {
        CookQuality.Failed => "💥",
        CookQuality.Poor => "★☆☆☆",
        CookQuality.Normal => "★★☆☆",
        CookQuality.Delicious => "★★★☆",
        _ => "★★★★",
    };

    public static float NutritionMultiplier(CookQuality quality) => quality switch
    {
        CookQuality.Failed => 0.3f,
        CookQuality.Poor => 0.7f,
        CookQuality.Normal => 1f,
        CookQuality.Delicious => 1.15f,
        _ => 1.3f,
    };

    /// <summary>
    /// Quality from skill (0-10), how well the mini-game went (0-1, or null for
    /// an AI cook) and whether someone helped. Low skill can still get lucky.
    /// </summary>
    public static CookQuality Evaluate(Recipe recipe, float skill, float? miniGameScore, bool helped, GameRandom random)
    {
        float skillPart = Math.Clamp((skill - recipe.MinSkill + 2f) / 8f, 0f, 1f);
        float score = miniGameScore is { } mini
            ? (mini * 0.65f) + (skillPart * 0.35f)
            : (skillPart * 0.75f) + random.Range(-0.2f, 0.32f);
        if (helped)
        {
            score += 0.08f;
        }

        if (skill < recipe.MinSkill)
        {
            score -= 0.15f * (recipe.MinSkill - skill);
        }

        return score switch
        {
            < 0.15f => CookQuality.Failed,
            < 0.36f => CookQuality.Poor,
            < 0.64f => CookQuality.Normal,
            < 0.87f => CookQuality.Delicious,
            _ => CookQuality.Perfect,
        };
    }
}
