using System.Numerics;
using System.Text.Json.Serialization;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Economy;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.School;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

/// <summary>
/// Everything that is saved: the family, the house, money, memories, weather,
/// pets and progression. Runtime systems live in <see cref="GameSession"/>.
/// </summary>
public sealed class GameState
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string SaveName { get; set; } = "";

    public DateTime SavedAt { get; set; }

    public ulong RandomState { get; set; } = 0xC0FFEE1234UL;

    public GameMode Mode { get; set; } = GameMode.Normal;

    public string PlayerName { get; set; } = "Raka";

    public GameClock Clock { get; set; } = new();

    public List<FamilyMember> Members { get; set; } = [];

    public Relationships Relationships { get; set; } = new();

    public House House { get; set; } = new();

    public Inventory Inventory { get; set; } = new();

    public Wallet Wallet { get; set; } = new();

    public List<PreparedFood> Pantry { get; set; } = [];

    public List<FamilyMemory> Memories { get; set; } = [];

    public int NextMemoryId { get; set; } = 1;

    public WeatherState Weather { get; set; } = new();

    public List<Pet> Pets { get; set; } = [];

    public int NextPetId { get; set; } = 1;

    public int Chapter { get; set; } = 1;

    public HashSet<string> Achievements { get; set; } = [];

    public Dictionary<string, float> Stats { get; set; } = [];

    public SchoolRecord School { get; set; } = new();

    public MemberId Controlled { get; set; } = MemberId.Player;

    public HashSet<string> Flags { get; set; } = [];

    public HashSet<string> NeighboursMet { get; set; } = [];

    /// <summary>How well the family knows each neighbour (0-100).</summary>
    public Dictionary<string, float> Friendships { get; set; } = [];

    public float Friendship(string npc) => Friendships.GetValueOrDefault(npc);

    /// <summary>Last game minute whose scheduled moments were processed.</summary>
    public double ScheduleCursor { get; set; }

    /// <summary>Accumulated pay of the current week, paid on Friday.</summary>
    public long PendingSalary { get; set; }

    /// <summary>Members who travel with the player on a family trip.</summary>
    public HashSet<MemberId> Party { get; set; } = [];

    [JsonIgnore]
    public GameRandom Random { get; private set; } = new(0xC0FFEE1234UL);

    public FamilyMember Member(MemberId id) => Members[(int)id];

    [JsonIgnore]
    public FamilyMember Player => Member(MemberId.Player);

    [JsonIgnore]
    public FamilyMember ControlledMember => Member(Controlled);

    public float Stat(string key) => Stats.TryGetValue(key, out float v) ? v : 0f;

    public void AddStat(string key, float amount = 1f) => Stats[key] = Stat(key) + amount;

    public bool Flag(string key) => Flags.Contains(key);

    /// <summary>Restores the random generator after loading.</summary>
    public void AfterLoad()
    {
        Random = new GameRandom(RandomState);
        FamilyNames.PlayerName = PlayerName;
        foreach (FurnitureItem item in House.Furniture)
        {
            item.Occupants = [];
            item.EnsureOccupants();
        }

        House.Touch();
        House.TouchFurniture();
    }

    public void BeforeSave()
    {
        RandomState = Random.State;
        SavedAt = DateTime.Now;
    }

    public int MealServings => Pantry.Where(p => !p.Recipe.IsDrink).Sum(p => p.Servings);

    public static GameState CreateNew(GameMode mode, string playerName, ulong seed)
    {
        FamilyNames.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Raka" : playerName.Trim();
        GameState state = new()
        {
            Mode = mode,
            PlayerName = FamilyNames.PlayerName,
            RandomState = seed,
            House = House.CreateStarter(),
            Inventory = Inventory.CreateStarter(),
            Wallet = new Wallet { Money = mode == GameMode.Cozy ? 2_500_000 : 1_500_000 },
        };
        state.Random = new GameRandom(seed);

        // Morning of moving day: everyone starts in their bedrooms.
        Vector2[] starts =
        [
            new(-3.0f, -2.2f),  // Father, parents' room
            new(-4.2f, -2.0f),  // Mother
            new(4.5f, -2.2f),   // Older sister, kids' room
            new(2.6f, -2.0f),   // Player
            new(3.6f, -2.8f),   // Younger sister
        ];
        foreach (MemberId id in FamilyNames.All)
        {
            FamilyMember member = FamilyMember.Create(id);
            member.Position = starts[(int)id];
            member.Yaw = MathF.PI;
            state.Members.Add(member);
        }

        state.Clock.TotalMinutes = 6 * 60;
        state.ScheduleCursor = state.Clock.TotalMinutes;
        state.Weather.Current = WeatherKind.Sunny;
        state.Weather.Forecast = WeatherKind.Cloudy;
        state.Pantry.Add(new PreparedFood { RecipeId = "fried-rice", Quality = CookQuality.Normal, Servings = 2, CookedDay = 0 });
        return state;
    }
}
