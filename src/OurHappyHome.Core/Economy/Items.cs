namespace OurHappyHome.Core.Economy;

public enum ItemCategory
{
    Ingredient,
    Safety,
    Gift,
    Hobby,
    Pet,
    Garden,
    Costume,
}

public sealed record ItemDef(string Id, string NameId, string NameEn, string Icon, ItemCategory Category, long Price, string Shop, string[] Tags)
{
    public string Name => Loc.T(NameId, NameEn);
}

/// <summary>Everything the family can buy at the supermarket or the mall.</summary>
public static class ItemCatalog
{
    public const string Supermarket = "supermarket";
    public const string Mall = "mall";

    public static readonly IReadOnlyList<ItemDef> All =
    [
        new("rice", "Beras", "Rice", "🍚", ItemCategory.Ingredient, 15_000, Supermarket, []),
        new("egg", "Telur", "Eggs", "🥚", ItemCategory.Ingredient, 5_000, Supermarket, []),
        new("chicken", "Ayam", "Chicken", "🍗", ItemCategory.Ingredient, 35_000, Supermarket, []),
        new("flour", "Tepung", "Flour", "🌾", ItemCategory.Ingredient, 12_000, Supermarket, []),
        new("milk", "Susu", "Milk", "🥛", ItemCategory.Ingredient, 18_000, Supermarket, []),
        new("sugar", "Gula", "Sugar", "🍬", ItemCategory.Ingredient, 14_000, Supermarket, []),
        new("butter", "Mentega", "Butter", "🧈", ItemCategory.Ingredient, 20_000, Supermarket, []),
        new("chocolate", "Cokelat", "Chocolate", "🍫", ItemCategory.Ingredient, 22_000, Supermarket, ["sweet"]),
        new("fruit", "Buah & Lemon", "Fruit & Lemons", "🍋", ItemCategory.Ingredient, 16_000, Supermarket, []),
        new("vegetables", "Sayuran", "Vegetables", "🥕", ItemCategory.Ingredient, 10_000, Supermarket, []),
        new("snack", "Roti & Camilan", "Bread & Snacks", "🥐", ItemCategory.Ingredient, 9_000, Supermarket, []),
        new("noodles", "Mi", "Noodles", "🍜", ItemCategory.Ingredient, 8_000, Supermarket, []),
        new("banana", "Pisang", "Bananas", "🍌", ItemCategory.Ingredient, 12_000, Supermarket, []),
        new("peanuts", "Kacang Tanah", "Peanuts", "🥜", ItemCategory.Ingredient, 12_000, Supermarket, []),
        new("tea", "Teh", "Tea", "🍵", ItemCategory.Ingredient, 8_000, Supermarket, []),
        new("flashlight", "Senter", "Flashlight", "🔦", ItemCategory.Safety, 45_000, Supermarket, []),
        new("batteries", "Baterai", "Batteries", "🔋", ItemCategory.Safety, 20_000, Supermarket, []),
        new("candle", "Lilin", "Candles", "🕯", ItemCategory.Safety, 8_000, Supermarket, []),
        new("bulb", "Bola Lampu", "Light Bulb", "💡", ItemCategory.Safety, 25_000, Supermarket, []),
        new("bandage", "Perban & Plester", "Bandages", "🩹", ItemCategory.Safety, 15_000, Supermarket, []),
        new("sandbag", "Karung Pasir", "Sandbags", "🧱", ItemCategory.Safety, 30_000, Supermarket, []),
        new("water", "Air Galon", "Bottled Water", "💧", ItemCategory.Safety, 20_000, Supermarket, []),
        new("umbrella", "Payung", "Umbrella", "☂", ItemCategory.Safety, 40_000, Supermarket, []),
        new("pet-food", "Makanan Hewan", "Pet Food", "🦴", ItemCategory.Pet, 30_000, Supermarket, []),
        new("seeds", "Bibit Tanaman", "Seeds", "🌱", ItemCategory.Garden, 10_000, Supermarket, []),
        new("flowers", "Buket Bunga", "Flower Bouquet", "💐", ItemCategory.Gift, 60_000, Mall, ["flowers", "gardening"]),
        new("toy-car", "Mobil Mainan", "Toy Car", "🚙", ItemCategory.Gift, 75_000, Mall, ["toys", "cars"]),
        new("doll", "Boneka", "Doll", "🧸", ItemCategory.Gift, 80_000, Mall, ["toys", "cute"]),
        new("storybook", "Buku Cerita Inggris", "English Storybook", "📘", ItemCategory.Gift, 55_000, Mall, ["books", "english"]),
        new("sketchbook", "Buku Gambar & Krayon", "Sketchbook & Crayons", "🖍", ItemCategory.Gift, 50_000, Mall, ["drawing", "art"]),
        new("hair-clip", "Jepit Rambut Kerang", "Shell Hair Clips", "🐚", ItemCategory.Gift, 35_000, Mall, ["fashion", "cute"]),
        new("cap", "Topi Baru", "New Cap", "🧢", ItemCategory.Gift, 70_000, Mall, ["fashion", "outdoor"]),
        new("toolset", "Set Perkakas", "Tool Set", "🧰", ItemCategory.Gift, 150_000, Mall, ["repair", "building"]),
        new("apron", "Celemek Lucu", "Cute Apron", "👩‍🍳", ItemCategory.Gift, 65_000, Mall, ["cooking", "household"]),
        new("ball", "Bola Sepak", "Football", "⚽", ItemCategory.Hobby, 90_000, Mall, ["sports", "outdoor"]),
        new("camera", "Kamera Instan", "Instant Camera", "📷", ItemCategory.Hobby, 450_000, Mall, ["photography"]),
        new("bicycle", "Sepeda", "Bicycle", "🚲", ItemCategory.Hobby, 900_000, Mall, ["cycling", "outdoor"]),
        new("tent", "Tenda Kemah", "Camping Tent", "⛺", ItemCategory.Hobby, 350_000, Mall, ["outdoor"]),
        new("rain-hat", "Topi Hujan", "Rain Hat", "☔", ItemCategory.Costume, 40_000, Mall, ["outdoor"]),
        new("party-hat", "Topi Pesta", "Party Hat", "🥳", ItemCategory.Costume, 25_000, Mall, ["party", "cute"]),
        new("straw-hat", "Topi Jerami", "Straw Hat", "👒", ItemCategory.Costume, 60_000, Mall, ["outdoor", "gardening"]),
        new("beanie", "Kupluk Rajut", "Knitted Beanie", "🧶", ItemCategory.Costume, 55_000, Mall, ["fashion", "cute"]),
        new("crown", "Mahkota Kertas", "Paper Crown", "👑", ItemCategory.Costume, 30_000, Mall, ["cute", "party"]),
        new("cat-ears", "Bando Telinga Kucing", "Cat-Ear Headband", "🐱", ItemCategory.Costume, 35_000, Mall, ["cute", "fashion"]),
        new("guitar", "Gitar Kecil", "Ukulele", "🎸", ItemCategory.Gift, 250_000, Mall, ["music"]),
    ];

    private static readonly Dictionary<string, ItemDef> ById = All.ToDictionary(i => i.Id);

    public static ItemDef Get(string id) => ById[id];

    public static bool Exists(string id) => ById.ContainsKey(id);
}

/// <summary>Items the family owns.</summary>
public sealed class Inventory
{
    public Dictionary<string, int> Counts { get; set; } = [];

    public int Count(string id) => Counts.TryGetValue(id, out int n) ? n : 0;

    public bool Has(string id, int amount = 1) => Count(id) >= amount;

    public void Add(string id, int amount = 1) => Counts[id] = Count(id) + amount;

    public bool Take(string id, int amount = 1)
    {
        if (!Has(id, amount))
        {
            return false;
        }

        Counts[id] = Count(id) - amount;
        return true;
    }

    public static Inventory CreateStarter()
    {
        Inventory inventory = new();
        foreach ((string id, int n) in new[]
        {
            ("rice", 4), ("egg", 8), ("flour", 2), ("milk", 3), ("sugar", 2), ("vegetables", 3),
            ("snack", 4), ("chocolate", 1), ("fruit", 2), ("flashlight", 1), ("bulb", 1), ("water", 2), ("pet-food", 2),
            ("rain-hat", 1),
        })
        {
            inventory.Add(id, n);
        }

        return inventory;
    }
}

public sealed record Transaction(int Day, string Text, long Amount);

/// <summary>Family money with a short ledger for the UI.</summary>
public sealed class Wallet
{
    public long Money { get; set; } = 1_500_000;

    public long KidsEarnings { get; set; }

    public List<Transaction> Ledger { get; set; } = [];

    public bool CanAfford(long amount) => Money >= amount;

    public bool Spend(long amount, string text, int day)
    {
        if (!CanAfford(amount))
        {
            return false;
        }

        Money -= amount;
        Log(day, text, -amount);
        return true;
    }

    public void Earn(long amount, string text, int day, bool byKids = false)
    {
        Money += amount;
        if (byKids)
        {
            KidsEarnings += amount;
        }

        Log(day, text, amount);
    }

    /// <summary>Bills are paid even if it makes the balance negative.</summary>
    public void Pay(long amount, string text, int day)
    {
        Money -= amount;
        Log(day, text, -amount);
    }

    private void Log(int day, string text, long amount)
    {
        Ledger.Add(new Transaction(day, text, amount));
        if (Ledger.Count > 60)
        {
            Ledger.RemoveAt(0);
        }
    }
}
