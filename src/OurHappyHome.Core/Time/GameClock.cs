namespace OurHappyHome.Core.Time;

public enum Weekday
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
}

/// <summary>A calendar date in the game world (no leap years).</summary>
public readonly record struct GameDate(int Year, int Month, int Day, Weekday Weekday, int DayIndex)
{
    private static readonly int[] MonthLengths = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    /// <summary>Day index 0 is Monday 3 January, year 1: the first school day.</summary>
    public static GameDate FromDayIndex(int dayIndex)
    {
        int dayOfYearStart = 2; // Jan 3 is the third day (0 based: 2).
        int absolute = dayIndex + dayOfYearStart;
        int year = 1 + (absolute / 365);
        int dayOfYear = absolute % 365;
        int month = 0;
        while (dayOfYear >= MonthLengths[month])
        {
            dayOfYear -= MonthLengths[month];
            month++;
        }

        Weekday weekday = (Weekday)(((dayIndex % 7) + 7) % 7);
        return new GameDate(year, month + 1, dayOfYear + 1, weekday, dayIndex);
    }

    public static int DaysInMonth(int month) => MonthLengths[(month - 1) % 12];

    /// <summary>Day index of a date in the given year (may be negative before the start).</summary>
    public static int ToDayIndex(int year, int month, int day)
    {
        int dayOfYear = day - 1;
        for (int m = 1; m < month; m++)
        {
            dayOfYear += MonthLengths[m - 1];
        }

        return ((year - 1) * 365) + dayOfYear - 2;
    }

    public bool IsWeekend => Weekday is Weekday.Saturday or Weekday.Sunday;

    /// <summary>Father works on weekdays that are not public holidays.</summary>
    public bool IsWorkDay => !IsWeekend && !Calendar.IsPublicHoliday(this);

    /// <summary>School runs on work days outside the school breaks.</summary>
    public bool IsSchoolDay => IsWorkDay && !Calendar.IsSchoolBreak(this);

    public Season Season => Seasons.Of(this);

    public string MonthName => MonthNames(Month);

    public static string MonthNames(int month) => month switch
    {
        1 => Loc.T("Januari", "January"),
        2 => Loc.T("Februari", "February"),
        3 => Loc.T("Maret", "March"),
        4 => Loc.T("April", "April"),
        5 => Loc.T("Mei", "May"),
        6 => Loc.T("Juni", "June"),
        7 => Loc.T("Juli", "July"),
        8 => Loc.T("Agustus", "August"),
        9 => Loc.T("September", "September"),
        10 => Loc.T("Oktober", "October"),
        11 => Loc.T("November", "November"),
        _ => Loc.T("Desember", "December"),
    };

    public string WeekdayName => Weekday switch
    {
        Weekday.Monday => Loc.T("Senin", "Monday"),
        Weekday.Tuesday => Loc.T("Selasa", "Tuesday"),
        Weekday.Wednesday => Loc.T("Rabu", "Wednesday"),
        Weekday.Thursday => Loc.T("Kamis", "Thursday"),
        Weekday.Friday => Loc.T("Jumat", "Friday"),
        Weekday.Saturday => Loc.T("Sabtu", "Saturday"),
        _ => Loc.T("Minggu", "Sunday"),
    };

    public override string ToString() => $"{WeekdayName}, {Day} {MonthName}";
}

/// <summary>
/// Game time in minutes since day 0, 00:00. One real second is one game
/// minute at normal speed, so a day lasts 24 real minutes.
/// </summary>
public sealed class GameClock
{
    public const double MinutesPerDay = 24 * 60;

    public double TotalMinutes { get; set; } = 6 * 60; // the game starts at 06:00

    /// <summary>Simulation speed multiplier chosen by the player (1, 2, 4) or set while sleeping.</summary>
    public float Speed { get; set; } = 1f;

    public int DayIndex => (int)(TotalMinutes / MinutesPerDay);

    public double MinuteOfDay => TotalMinutes % MinutesPerDay;

    public float Hour => (float)(MinuteOfDay / 60.0);

    public GameDate Date => GameDate.FromDayIndex(DayIndex);

    public bool IsNight => Hour < 5.75f || Hour >= 18.5f;

    public string TimeText
    {
        get
        {
            int minutes = (int)MinuteOfDay;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }

    /// <summary>Advances the clock; returns the number of day boundaries crossed.</summary>
    public int Advance(double minutes)
    {
        int before = DayIndex;
        TotalMinutes += minutes;
        return DayIndex - before;
    }

    /// <summary>True when the clock passed <paramref name="hour"/> during the last advance of <paramref name="minutes"/>.</summary>
    public bool Crossed(float hour, double minutes)
    {
        double now = MinuteOfDay;
        double previous = now - minutes;
        double target = hour * 60.0;
        if (previous >= 0)
        {
            return previous < target && now >= target;
        }

        // The advance wrapped past midnight.
        return now >= target || previous + MinutesPerDay < target;
    }

    public static string Format(double minuteOfDay)
    {
        int minutes = (int)minuteOfDay;
        return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
    }
}
