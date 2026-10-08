using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Scenarios;

/// <summary>
/// Rolls random events every game hour. Frequencies come from the rarity
/// tiers in design section 13, scaled by the chosen mode: Cozy Mode makes
/// dangerous events rare, Adventure Mode makes them more frequent, and the
/// reduced-intensity accessibility option tones them down further.
/// </summary>
public sealed class EventDirector(GameSession session)
{
    private double _nextRoll = 8 * 60;
    private double _cooldownUntil;

    public ScenarioKind? LastScenario { get; private set; }

    /// <summary>Multiplier for dangerous events of a rarity, from mode and settings.</summary>
    public float DangerMultiplier(Rarity rarity)
    {
        float mode = (session.State.Mode, rarity) switch
        {
            (GameMode.Cozy, Rarity.Common) => 1f,
            (GameMode.Cozy, Rarity.Uncommon) => 0.35f,
            (GameMode.Cozy, Rarity.Rare) => 0.1f,
            (GameMode.Adventure, Rarity.Common) => 1.1f,
            (GameMode.Adventure, Rarity.Uncommon) => 1.7f,
            (GameMode.Adventure, Rarity.Rare) => 2.6f,
            _ => 1f,
        };
        return mode * (session.Settings.ReducedIntensity ? 0.6f : 1f);
    }

    public void Tick(float minutes)
    {
        GameSession s = session;
        if (s.Now < _nextRoll)
        {
            return;
        }

        _nextRoll = s.Now + 60;
        if (s.Scenario is not null || s.Now < _cooldownUntil || s.Clock.DayIndex < 1 && s.Hour < 12)
        {
            return;
        }

        // Only at home: trips have their own encounters.
        if (!Rooms.AtHome(s.Controlled.Position))
        {
            return;
        }

        float hour = s.Hour;
        bool night = hour >= 22f || hour < 5f;
        bool evening = hour is >= 17.5f and < 22f;
        bool day = hour is >= 7f and < 17.5f;
        WeatherState weather = s.State.Weather;

        List<(ScenarioKind Kind, float Chance)> table =
        [
            (ScenarioKind.CatVisitor, day || evening ? 0.025f : 0f),
            (ScenarioKind.LightBulb, !night ? 0.02f : 0.005f),
            (ScenarioKind.FaucetLeak, 0.018f),
            (ScenarioKind.ApplianceBroken, 0.015f),
            (ScenarioKind.MonkeyThief, day ? 0.009f : 0f),
            (ScenarioKind.PowerOutage, weather.IsStormy ? 0.05f : 0.003f),
            (ScenarioKind.LocalFlood, weather.Current is WeatherKind.Thunderstorm or WeatherKind.Rain && weather.Intensity > 0.6f ? 0.025f : 0f),
            (ScenarioKind.DangerousAnimal, day && weather.OutdoorFriendly ? 0.006f : 0f),
            (ScenarioKind.SuspiciousStranger, evening ? 0.004f : 0f),
            (ScenarioKind.Burglary, night ? 0.0025f : 0f),
            (ScenarioKind.SmallFire, (day || evening) && s.State.Members.Any(m => m.Task?.Activity == ActivityId.Cook) ? 0.004f : 0f),
        ];

        foreach ((ScenarioKind kind, float chance) in table)
        {
            Rarity rarity = Scenario.RarityOf(kind);
            bool dangerous = kind is not (ScenarioKind.CatVisitor or ScenarioKind.LightBulb or ScenarioKind.FaucetLeak or ScenarioKind.ApplianceBroken or ScenarioKind.MonkeyThief);
            float p = chance * (dangerous ? DangerMultiplier(rarity) : 1f);
            if (kind == LastScenario)
            {
                p *= 0.3f;
            }

            // Early chapters stay gentle while the family settles in.
            if (s.State.Chapter <= 1 && rarity == Rarity.Rare)
            {
                p *= 0.2f;
            }

            if (s.Random.Chance(p))
            {
                s.StartScenario(kind);
                return;
            }
        }
    }

    public void OnScenarioEnded(Scenario scenario)
    {
        LastScenario = scenario.Kind;
        _cooldownUntil = session.Now + (scenario.Major ? 360 : 150);
    }
}
