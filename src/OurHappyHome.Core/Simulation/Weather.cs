namespace OurHappyHome.Core.Simulation;

public enum WeatherKind
{
    Sunny,
    Cloudy,
    Rain,
    Thunderstorm,
    Fog,
    Windy,
    SevereStorm,
}

/// <summary>
/// Weather that changes every few hours, with tomorrow's forecast. Adventure
/// mode makes storms more likely; Cozy mode keeps the skies friendlier.
/// </summary>
public sealed class WeatherState
{
    public WeatherKind Current { get; set; } = WeatherKind.Sunny;

    /// <summary>0-1 visual intensity (rain density, wind strength, fog density).</summary>
    public float Intensity { get; set; } = 0.5f;

    public double NextChange { get; set; } = 9 * 60;

    public WeatherKind Forecast { get; set; } = WeatherKind.Sunny;

    /// <summary>Day index on which the Great Storm will hit (-1 = none scheduled).</summary>
    public int StormDay { get; set; } = -1;

    /// <summary>Minutes until the next lightning strike during thunderstorms.</summary>
    public float LightningTimer { get; set; } = 3f;

    public float Temperature => Current switch
    {
        WeatherKind.Sunny => 31f,
        WeatherKind.Cloudy => 28f,
        WeatherKind.Rain => 25f,
        WeatherKind.Thunderstorm => 24f,
        WeatherKind.Fog => 22f,
        WeatherKind.Windy => 27f,
        _ => 23f,
    };

    public bool IsRaining => Current is WeatherKind.Rain or WeatherKind.Thunderstorm or WeatherKind.SevereStorm;

    public bool IsStormy => Current is WeatherKind.Thunderstorm or WeatherKind.SevereStorm;

    public bool OutdoorFriendly => Current is WeatherKind.Sunny or WeatherKind.Cloudy;

    public static string Name(WeatherKind kind) => kind switch
    {
        WeatherKind.Sunny => Loc.T("Cerah", "Sunny"),
        WeatherKind.Cloudy => Loc.T("Berawan", "Cloudy"),
        WeatherKind.Rain => Loc.T("Hujan", "Rain"),
        WeatherKind.Thunderstorm => Loc.T("Badai petir", "Thunderstorm"),
        WeatherKind.Fog => Loc.T("Berkabut", "Fog"),
        WeatherKind.Windy => Loc.T("Angin kencang", "Strong wind"),
        _ => Loc.T("Badai besar", "Severe storm"),
    };

    public static string Icon(WeatherKind kind, bool night = false) => kind switch
    {
        WeatherKind.Sunny => night ? "🌙" : "☀",
        WeatherKind.Cloudy => "⛅",
        WeatherKind.Rain => "🌧",
        WeatherKind.Thunderstorm => "⛈",
        WeatherKind.Fog => "🌫",
        WeatherKind.Windy => "🌬",
        _ => "🌀",
    };
}

public static class WeatherSystem
{
    /// <summary>Picks the next weather from the current one (a small Markov chain).</summary>
    public static WeatherKind Next(WeatherKind current, GameMode mode, GameRandom random, int month)
    {
        bool rainySeason = month is >= 10 or <= 3;
        float storm = mode == GameMode.Adventure ? 1.8f : mode == GameMode.Cozy ? 0.35f : 1f;
        float rain = rainySeason ? 1.4f : 0.7f;
        float[] weights = current switch
        {
            WeatherKind.Sunny => [5f, 3f, 1f * rain, 0.3f * rain * storm, 0.4f, 0.6f, 0f],
            WeatherKind.Cloudy => [3f, 2.5f, 2f * rain, 0.7f * rain * storm, 0.6f, 0.8f, 0f],
            WeatherKind.Rain => [1.5f, 2.5f, 2f, 1.0f * storm, 0.6f, 0.4f, 0f],
            WeatherKind.Thunderstorm => [0.6f, 2f, 2.5f, 1f * storm, 0.3f, 0.6f, 0f],
            WeatherKind.Fog => [3f, 2f, 0.6f, 0.1f, 1f, 0.2f, 0f],
            WeatherKind.Windy => [2.5f, 2.5f, 1f, 0.6f * storm, 0.1f, 1f, 0f],
            _ => [0.5f, 2f, 3f, 1f, 0.2f, 1f, 0f],
        };
        return (WeatherKind)random.Weighted(weights);
    }

    public static float IntensityFor(WeatherKind kind, GameRandom random) => kind switch
    {
        WeatherKind.Rain => random.Range(0.35f, 0.8f),
        WeatherKind.Thunderstorm => random.Range(0.6f, 0.95f),
        WeatherKind.SevereStorm => 1f,
        WeatherKind.Fog => random.Range(0.4f, 0.9f),
        WeatherKind.Windy => random.Range(0.5f, 1f),
        _ => random.Range(0.2f, 0.6f),
    };
}
