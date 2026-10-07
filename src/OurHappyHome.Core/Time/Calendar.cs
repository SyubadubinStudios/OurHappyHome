using OurHappyHome.Core.Family;

namespace OurHappyHome.Core.Time;

public enum CalendarEventKind
{
    SchoolDay,
    FirstSchoolDay,
    HouseInspection,
    Birthday,
    Camping,
    Festival,
    MovieNight,
    PancakeMorning,
    WeekendShopping,
    Picnic,
    Swimming,
    SchoolEvent,
    Holiday,
    Bbq,
    CostumeParty,
    ThemePark,
    BeachTrip,
    SchoolBreak,
    ChildrensDay,
    MothersDay,
    TeachersDay,
}

public enum Season
{
    Rainy,
    Dry,
}

/// <summary>Indonesia's two seasons: rain from October to March, dry from April to September.</summary>
public static class Seasons
{
    public static Season Of(GameDate date) => date.Month is >= 10 or <= 3 ? Season.Rainy : Season.Dry;

    public static string Name(Season season) => season == Season.Rainy ? Loc.T("Musim hujan", "Rainy season") : Loc.T("Musim kemarau", "Dry season");

    public static string Icon(Season season) => season == Season.Rainy ? "🌧" : "🌞";
}

public sealed record CalendarEvent(CalendarEventKind Kind, string Title, string Description, MemberId? Member = null)
{
    public string Icon => Kind switch
    {
        CalendarEventKind.Birthday => "🎂",
        CalendarEventKind.Camping => "⛺",
        CalendarEventKind.Festival => "🎪",
        CalendarEventKind.MovieNight => "🎬",
        CalendarEventKind.PancakeMorning => "🥞",
        CalendarEventKind.WeekendShopping => "🛒",
        CalendarEventKind.Picnic => "🧺",
        CalendarEventKind.Swimming => "🏊",
        CalendarEventKind.FirstSchoolDay or CalendarEventKind.SchoolDay or CalendarEventKind.SchoolEvent => "🏫",
        CalendarEventKind.HouseInspection => "🔍",
        CalendarEventKind.Holiday => "🎉",
        CalendarEventKind.Bbq => "🍢",
        CalendarEventKind.CostumeParty => "🎭",
        CalendarEventKind.ThemePark => "🎢",
        CalendarEventKind.BeachTrip => "🏖",
        CalendarEventKind.SchoolBreak => "🎒",
        CalendarEventKind.ChildrensDay => "🧒",
        CalendarEventKind.MothersDay => "💐",
        CalendarEventKind.TeachersDay => "🍎",
        _ => "📅",
    };
}

/// <summary>
/// Fixed dates (from the design document's January example, birthdays and
/// holidays) plus recurring weekly activities.
/// </summary>
public static class Calendar
{
    public static readonly Dictionary<MemberId, (int Month, int Day)> Birthdays = new()
    {
        [MemberId.Father] = (3, 21),
        [MemberId.Mother] = (5, 9),
        [MemberId.OlderSister] = (2, 14),
        [MemberId.Player] = (4, 2),
        [MemberId.YoungerSister] = (1, 12),
    };

    /// <summary>National holidays: no work and no school.</summary>
    public static bool IsPublicHoliday(GameDate date) => (date.Month, date.Day) is (1, 1) or (5, 1) or (6, 1) or (8, 17) or (12, 25);

    /// <summary>Mid-year and year-end school breaks.</summary>
    public static bool IsSchoolBreak(GameDate date) =>
        (date.Month == 6 && date.Day >= 24) || (date.Month == 7 && date.Day <= 12) || (date.Month == 12 && date.Day >= 20) || (date.Month == 1 && date.Day <= 2);

    /// <summary>The town festival: 24 January, Independence Day and the fourth Saturday of every month.</summary>
    public static bool IsFestivalDay(GameDate date) => EventsOn(date).Any(e => e.Kind == CalendarEventKind.Festival);

    public static IReadOnlyList<CalendarEvent> EventsOn(GameDate date)
    {
        List<CalendarEvent> events = [];

        switch ((date.Month, date.Day))
        {
            case (1, 3):
                events.Add(new(CalendarEventKind.FirstSchoolDay, Loc.T("Hari Pertama Sekolah", "First School Day"),
                    Loc.T("Hari pertama di sekolah baru. Jangan terlambat!", "First day at the new school. Don't be late!")));
                break;
            case (1, 8):
                events.Add(new(CalendarEventKind.HouseInspection, Loc.T("Inspeksi Rumah", "House Inspection"),
                    Loc.T("Periksa semua perabot dan perbaiki yang rusak.", "Check every appliance and fix what is broken.")));
                break;
            case (1, 18):
                events.Add(new(CalendarEventKind.Camping, Loc.T("Kemah Keluarga", "Family Camping"),
                    Loc.T("Kemah di gunung! Siapkan tenda dan senter.", "Camping on the mountain! Pack the tent and flashlights.")));
                break;
            case (1, 24):
                events.Add(new(CalendarEventKind.Festival, Loc.T("Festival Kota", "Town Festival"),
                    Loc.T("Festival di pusat kota: jajanan, permainan dan kembang api.", "Festival downtown: snacks, games and fireworks.")));
                break;
            case (2, 17):
                events.Add(new(CalendarEventKind.SchoolEvent, Loc.T("Pentas Seni Sekolah", "School Art Show"),
                    Loc.T("Kakak memamerkan gambarnya di sekolah.", "Older Sister shows her drawings at school.")));
                break;
            case (3, 1):
                events.Add(new(CalendarEventKind.BeachTrip, Loc.T("Liburan ke Pantai", "Beach Holiday"),
                    Loc.T("Berenang, membuat istana pasir dan berfoto bersama.", "Swim, build sandcastles and take photos together.")));
                break;
            case (8, 17):
                events.Add(new(CalendarEventKind.Holiday, Loc.T("Hari Kemerdekaan", "Independence Day"),
                    Loc.T("Dirgahayu Indonesia! Bendera merah putih berkibar di seluruh kota.", "Happy Independence Day! Red and white flags fly all over town.")));
                events.Add(new(CalendarEventKind.Festival, Loc.T("Lomba 17 Agustus", "Independence Day Games"),
                    Loc.T("Lomba makan kerupuk dan tarik tambang di taman, kembang api malam hari!", "Cracker eating contest and tug of war in the park, fireworks at night!")));
                break;
            case (4, 21):
                events.Add(new(CalendarEventKind.SchoolEvent, Loc.T("Hari Kartini", "Kartini Day"),
                    Loc.T("Anak-anak memakai baju adat ke sekolah.", "The children wear traditional clothes to school.")));
                break;
            case (5, 1):
                events.Add(new(CalendarEventKind.Holiday, Loc.T("Hari Buruh", "Labour Day"),
                    Loc.T("Ayah libur kerja. Waktunya bersama keluarga!", "Dad has the day off. Family time!")));
                break;
            case (6, 1):
                events.Add(new(CalendarEventKind.Holiday, Loc.T("Hari Lahir Pancasila", "Pancasila Day"),
                    Loc.T("Hari libur nasional.", "A national holiday.")));
                break;
            case (6, 24):
            case (12, 20):
                events.Add(new(CalendarEventKind.SchoolBreak, Loc.T("Libur Sekolah Dimulai", "School Holidays Begin"),
                    Loc.T("Tidak ada sekolah selama libur. Saatnya liburan keluarga!", "No school during the break. Time for a family holiday!")));
                break;
            case (7, 23):
                events.Add(new(CalendarEventKind.ChildrensDay, Loc.T("Hari Anak Nasional", "National Children's Day"),
                    Loc.T("Ayah dan Ibu menyiapkan kejutan untuk anak-anak.", "Mom and Dad have a surprise for the children.")));
                break;
            case (10, 28):
                events.Add(new(CalendarEventKind.SchoolEvent, Loc.T("Hari Sumpah Pemuda", "Youth Pledge Day"),
                    Loc.T("Upacara dan lomba pidato di sekolah.", "A ceremony and speech contest at school.")));
                break;
            case (11, 25):
                events.Add(new(CalendarEventKind.TeachersDay, Loc.T("Hari Guru", "Teachers' Day"),
                    Loc.T("Bawakan bunga untuk Bu Guru Rina!", "Bring flowers for Ms. Rina!")));
                break;
            case (12, 22):
                events.Add(new(CalendarEventKind.MothersDay, Loc.T("Hari Ibu", "Mother's Day"),
                    Loc.T("Beri Ibu hadiah atau masakkan sarapan untuknya.", "Give Mom a present or cook her breakfast.")));
                break;
            case (10, 31):
                events.Add(new(CalendarEventKind.CostumeParty, Loc.T("Pesta Kostum", "Costume Party"),
                    Loc.T("Pakai kostum lucu dan berfoto keluarga.", "Wear funny costumes and take a family photo.")));
                break;
            case (12, 25):
            case (1, 1):
                events.Add(new(CalendarEventKind.Holiday, Loc.T("Hari Libur", "Holiday"),
                    Loc.T("Hari libur keluarga. Waktunya bersantai bersama.", "A family holiday. Time to relax together.")));
                break;
        }

        foreach ((MemberId member, (int month, int day)) in Birthdays)
        {
            if (date.Month == month && date.Day == day)
            {
                events.Add(new(CalendarEventKind.Birthday, Loc.T($"Ulang Tahun {FamilyNames.Short(member)}", $"{FamilyNames.Short(member)}'s Birthday"),
                    Loc.T("Siapkan kue, kado dan pesta kejutan!", "Prepare a cake, gifts and a surprise party!"), member));
            }
        }

        if (date.Weekday == Weekday.Saturday && date.Day is >= 22 and <= 28 && !(date.Month == 1 && date.Day == 24))
        {
            events.Add(new(CalendarEventKind.Festival, Loc.T("Festival Kota", "Town Festival"),
                Loc.T("Festival bulanan di taman kota: jajanan, lomba dan kembang api.", "The monthly festival in the park: snacks, games and fireworks.")));
        }

        if (date.Weekday == Weekday.Friday && (date.Day > 24 || date.Day % 14 < 7))
        {
            events.Add(new(CalendarEventKind.MovieNight, Loc.T("Malam Nonton Film", "Family Movie Night"),
                Loc.T("Kumpul di ruang tamu, nonton film dan makan popcorn.", "Gather in the living room for a film and popcorn.")));
        }

        if (date.Weekday == Weekday.Sunday)
        {
            events.Add(new(CalendarEventKind.PancakeMorning, Loc.T("Pagi Pancake", "Pancake Morning"),
                Loc.T("Adik dan Ibu membuat pancake untuk semua.", "Younger Sister and Mother make pancakes for everyone.")));
        }

        if (date.Weekday == Weekday.Saturday)
        {
            events.Add(new(CalendarEventKind.WeekendShopping, Loc.T("Belanja Akhir Pekan", "Weekend Shopping"),
                Loc.T("Belanja bahan makanan di supermarket.", "Buy groceries at the supermarket.")));
            if (date.Day % 14 < 7)
            {
                events.Add(new(CalendarEventKind.Picnic, Loc.T("Piknik di Taman", "Picnic in the Park"),
                    Loc.T("Bawa bekal ke taman kota.", "Take a packed lunch to the community park.")));
            }
            else
            {
                events.Add(new(CalendarEventKind.Swimming, Loc.T("Berenang", "Swimming"),
                    Loc.T("Latihan berenang bersama Ayah.", "Swimming practice with Father.")));
            }
        }

        return events;
    }

    /// <summary>Next birthday of a member at or after <paramref name="fromDay"/>.</summary>
    public static int NextBirthdayDay(MemberId member, int fromDay)
    {
        (int month, int day) = Birthdays[member];
        for (int i = 0; i < 366; i++)
        {
            GameDate date = GameDate.FromDayIndex(fromDay + i);
            if (date.Month == month && date.Day == day)
            {
                return fromDay + i;
            }
        }

        return fromDay;
    }
}
