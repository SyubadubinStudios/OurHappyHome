namespace OurHappyHome.Core.Progression;

/// <summary>Counters the chapter goals and achievements read.</summary>
public static class Stat
{
    public const string FamilyMeals = "family-meals";
    public const string PlayerCooked = "player-cooked";
    public const string SchoolDays = "school-days";
    public const string FurnitureBought = "furniture-bought";
    public const string KidsEarned = "kids-earned";
    public const string VisitedPark = "visited-park";
    public const string VisitedSupermarket = "visited-supermarket";
    public const string GroceriesBought = "groceries-bought";
    public const string PetsAdopted = "pets-adopted";
    public const string NeighboursMet = "neighbours-met";
    public const string RoomsBuilt = "rooms-built";
    public const string EmergenciesHandled = "emergencies-handled";
    public const string Memories = "memories";
    public const string PerfectDishes = "perfect-dishes";
    public const string Repairs = "repairs";
    public const string Rescues = "rescues";
    public const string CampingTrips = "camping-trips";
    public const string BeachTrips = "beach-trips";
    public const string ThemeParkTrips = "theme-park-trips";
    public const string GreatStormSurvived = "great-storm";
    public const string Gifts = "gifts";
    public const string Photos = "photos";
    public const string LessonsPassed = "lessons-passed";
    public const string Hugs = "hugs";
    public const string PancakesMade = "pancakes";
    public const string Overnights = "overnights";
    public const string FestivalGames = "festival-games";
    public const string BestFriends = "best-friends";
    public const string UpperFloor = "upper-floor";
    public const string BooksRead = "books";
    public const string DaysPlayed = "days";
}

public sealed record Goal(string Id, string TextId, string TextEn, string Icon, Func<GameState, float> Progress, float Target)
{
    public string Text => Loc.T(TextId, TextEn);

    public bool Done(GameState state) => Progress(state) >= Target;
}

public sealed record Chapter(int Number, string TitleId, string TitleEn, string StoryId, string StoryEn, Goal[] Goals, long Reward)
{
    public string Title => Loc.T(TitleId, TitleEn);

    public string Story => Loc.T(StoryId, StoryEn);
}

/// <summary>
/// The story progression from the design document: Our Little Home, New
/// Neighborhood, Growing Together, Our Dream Home and Big Adventures, then an
/// endless family sandbox.
/// </summary>
public static class Chapters
{
    public const int Sandbox = 6;

    private static float S(GameState s, string stat) => s.Stat(stat);

    public static readonly IReadOnlyList<Chapter> All =
    [
        new(1, "Rumah Kecil Kita", "Our Little Home",
            "Keluarga kita baru pindah ke rumah kecil dengan perabot sederhana dan uang terbatas. Mari mulai hidup baru bersama!",
            "The family has just moved into a small home with basic furniture and little money. Let's start our new life together!",
            [
                new("c1-breakfast", "Sarapan bersama keluarga", "Eat breakfast together", "🍳", s => S(s, Stat.FamilyMeals), 1),
                new("c1-cook", "Masak satu hidangan sendiri", "Cook a dish yourself", "🥘", s => S(s, Stat.PlayerCooked), 1),
                new("c1-school", "Pergi ke sekolah 2 hari", "Go to school for 2 days", "🏫", s => S(s, Stat.SchoolDays), 2),
                new("c1-furniture", "Beli satu perabot baru", "Buy a new piece of furniture", "🛋", s => S(s, Stat.FurnitureBought), 1),
                new("c1-earn", "Hasilkan Rp 50.000 sendiri", "Earn Rp 50,000 yourself", "🍋", s => s.Wallet.KidsEarnings, 50_000),
            ], 500_000),
        new(2, "Lingkungan Baru", "New Neighborhood",
            "Saatnya berkenalan dengan tetangga dan menjelajahi kota.",
            "Time to meet the neighbours and explore the town.",
            [
                new("c2-park", "Kunjungi taman kota", "Visit the community park", "🌳", s => S(s, Stat.VisitedPark), 1),
                new("c2-shop", "Belanja bahan makanan", "Buy groceries", "🛒", s => S(s, Stat.GroceriesBought), 1),
                new("c2-pet", "Adopsi hewan peliharaan", "Adopt a pet", "🐶", s => S(s, Stat.PetsAdopted), 1),
                new("c2-neighbours", "Berkenalan dengan 2 tetangga", "Meet 2 neighbours", "👋", s => S(s, Stat.NeighboursMet), 2),
                new("c2-room", "Bangun kamar untuk Kakak & Adik", "Build the sisters' bedroom", "🎀", s => s.House.Has(World.RoomId.GirlsBedroom) ? 1 : 0, 1),
            ], 800_000),
        new(3, "Tumbuh Bersama", "Growing Together",
            "Setiap anggota keluarga mengembangkan bakat, persahabatan dan hubungan yang lebih erat.",
            "Everyone grows their talents, friendships and bonds.",
            [
                new("c3-skill", "Capai level 3 pada satu keahlian", "Reach level 3 in any skill", "⭐", s => s.Player.Skills.Levels.Values.DefaultIfEmpty(0).Max(), 3),
                new("c3-bond", "Rata-rata hubungan keluarga 75%", "Family bond average of 75%", "💞", s => s.Relationships.FamilyAverage(), 75),
                new("c3-emergency", "Atasi satu keadaan darurat", "Handle an emergency", "🛡", s => S(s, Stat.EmergenciesHandled), 1),
                new("c3-memories", "Kumpulkan 10 kenangan", "Collect 10 memories", "📸", s => s.Memories.Count, 10),
                new("c3-perfect", "Masak hidangan Sempurna", "Cook a Perfect dish", "🌟", s => S(s, Stat.PerfectDishes), 1),
            ], 1_500_000),
        new(4, "Rumah Impian Kita", "Our Dream Home",
            "Keluarga kini mampu merenovasi dan membangun rumah impian.",
            "The family can now afford big renovations and build the dream home.",
            [
                new("c4-rooms", "Miliki 9 ruangan di dalam rumah", "Have 9 indoor rooms", "🏠", s => s.House.IndoorRoomCount, 9),
                new("c4-decor", "Tata 45 perabot", "Place 45 pieces of furniture", "🪴", s => s.House.Furniture.Count, 45),
                new("c4-repair", "Perbaiki 5 barang rusak", "Repair 5 broken things", "🔧", s => S(s, Stat.Repairs), 5),
                new("c4-gift", "Beri 3 hadiah", "Give 3 gifts", "🎁", s => S(s, Stat.Gifts), 3),
            ], 2_500_000),
        new(5, "Petualangan Besar", "Big Adventures",
            "Kemah, liburan, cuaca ekstrem dan cerita keluarga yang lebih besar menanti.",
            "Camping, holidays, extreme weather and bigger family stories await.",
            [
                new("c5-camp", "Berkemah di gunung", "Go camping on the mountain", "⛺", s => S(s, Stat.CampingTrips), 1),
                new("c5-beach", "Liburan ke pantai", "Take a beach holiday", "🏖", s => S(s, Stat.BeachTrips), 1),
                new("c5-park", "Ke taman bermain", "Visit the theme park", "🎡", s => S(s, Stat.ThemeParkTrips), 1),
                new("c5-storm", "Lewati Badai Besar bersama", "Survive the Great Storm together", "🌀", s => S(s, Stat.GreatStormSurvived), 1),
            ], 5_000_000),
    ];

    public static Chapter? Get(int number) => number >= 1 && number <= All.Count ? All[number - 1] : null;

    public static string SandboxTitle => Loc.T("Kotak Pasir Keluarga", "Family Sandbox");
}

public sealed record Achievement(string Id, string TitleId, string TitleEn, string DescId, string DescEn, string Icon, string Stat, float Target)
{
    public string Title => Loc.T(TitleId, TitleEn);

    public string Description => Loc.T(DescId, DescEn);
}

public static class Achievements
{
    public static readonly IReadOnlyList<Achievement> All =
    [
        new("pancake-master", "Raja Pancake", "Pancake Master", "Buat 5 kali pancake", "Make pancakes 5 times", "🥞", Stat.PancakesMade, 5),
        new("nobody-left", "Tak Ada yang Tertinggal", "Nobody Left Behind", "Selamatkan anggota keluarga 3 kali", "Rescue family members 3 times", "🦸", Stat.Rescues, 3),
        new("handyman", "Tukang Andal", "Handy Helper", "Perbaiki 10 barang", "Repair 10 things", "🧰", Stat.Repairs, 10),
        new("bookworm", "Kutu Buku", "Bookworm", "Baca 15 kali", "Read 15 times", "📚", Stat.BooksRead, 15),
        new("photographer", "Fotografer Keluarga", "Family Photographer", "Simpan 20 foto di album", "Save 20 photos in the album", "📷", Stat.Photos, 20),
        new("star-student", "Murid Teladan", "Star Student", "Lulus 20 pelajaran", "Pass 20 lessons", "🏅", Stat.LessonsPassed, 20),
        new("hugger", "Pelukan Hangat", "Warm Hugs", "Peluk keluarga 25 kali", "Hug the family 25 times", "🤗", Stat.Hugs, 25),
        new("chef", "Koki Cilik", "Little Chef", "Masak 3 hidangan Sempurna", "Cook 3 Perfect dishes", "👨‍🍳", Stat.PerfectDishes, 3),
        new("builder", "Arsitek Rumah", "Home Architect", "Bangun 5 ruangan baru", "Build 5 new rooms", "🏗", Stat.RoomsBuilt, 5),
        new("explorer", "Penjelajah", "Explorer", "Pergi kemah, ke pantai dan taman bermain", "Go camping, to the beach and the theme park", "🧭", Stat.CampingTrips, 1),
        new("hundred-days", "100 Hari Bahagia", "100 Happy Days", "Bermain selama 100 hari", "Play for 100 days", "📅", Stat.DaysPlayed, 100),
        new("festival-star", "Bintang Festival", "Festival Star", "Ikut 4 lomba festival", "Join 4 festival contests", "🎪", Stat.FestivalGames, 4),
        new("sleepover", "Liburan Menginap", "A Night Away", "Menginap di tenda atau penginapan pantai", "Stay overnight in a tent or at the beach inn", "🌙", Stat.Overnights, 1),
        new("two-storey", "Rumah Bertingkat", "Two-Storey Home", "Bangun lantai atas dengan tangga dan balkon", "Build the upper floor with stairs and a balcony", "🪜", Stat.UpperFloor, 1),
        new("good-neighbour", "Tetangga Baik", "Good Neighbour", "Bersahabat dengan seorang tetangga", "Become best friends with a neighbour", "🤝", Stat.BestFriends, 1),
    ];
}
