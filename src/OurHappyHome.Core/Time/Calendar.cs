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
                    Loc.T("Lomba makan kerupuk dan tarik tambang!", "Cracker eating contest and tug of war!")));
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
