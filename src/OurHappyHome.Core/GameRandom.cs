namespace OurHappyHome.Core;

/// <summary>
/// Deterministic xorshift random generator. The state is a single number, so
/// saves and scenario restarts can restore it exactly.
/// </summary>
public sealed class GameRandom
{
    private ulong _state;

    public GameRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public ulong State
    {
        get => _state;
        set => _state = value == 0 ? 0x9E3779B97F4A7C15UL : value;
    }

    public ulong NextULong()
    {
        ulong x = _state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _state = x;
        return x;
    }

    /// <summary>Uniform value in [0, 1).</summary>
    public float NextFloat() => (NextULong() >> 40) / (float)(1UL << 24);

    public float Range(float min, float max) => min + ((max - min) * NextFloat());

    /// <summary>Uniform integer in [min, max).</summary>
    public int Range(int min, int max) => max <= min ? min : min + (int)(NextULong() % (ulong)(max - min));

    public bool Chance(float probability) => NextFloat() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

    /// <summary>Picks an index with probability proportional to its weight.</summary>
    public int Weighted(IReadOnlyList<float> weights)
    {
        float total = 0f;
        foreach (float w in weights)
        {
            total += MathF.Max(0f, w);
        }

        if (total <= 0f)
        {
            return Range(0, weights.Count);
        }

        float roll = NextFloat() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= MathF.Max(0f, weights[i]);
            if (roll <= 0f)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
