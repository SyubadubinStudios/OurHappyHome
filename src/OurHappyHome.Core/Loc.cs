namespace OurHappyHome.Core;

public enum Language
{
    Indonesian,
    English,
}

/// <summary>
/// Tiny two-language text helper. Every player facing string is written
/// inline as <c>Loc.T("Indonesia", "English")</c>, so a translation can never
/// drift away from the code that shows it.
/// </summary>
public static class Loc
{
    public static Language Language { get; set; } = Language.Indonesian;

    public static string T(string indonesian, string english) =>
        Language == Language.Indonesian ? indonesian : english;

    /// <summary>Formats money as Indonesian Rupiah, e.g. <c>Rp 1.250.000</c>.</summary>
    public static string Money(long amount)
    {
        string digits = Math.Abs(amount).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        return (amount < 0 ? "-Rp " : "Rp ") + digits;
    }

    public static string Percent(float value) => $"{MathF.Round(value):0}%";
}
