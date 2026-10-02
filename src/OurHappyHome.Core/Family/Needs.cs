namespace OurHappyHome.Core.Family;

public enum NeedKind
{
    Health,
    Hunger,
    Energy,
    Happiness,
    Hygiene,
    Sleep,
    Fun,
    Social,
}

/// <summary>
/// The eight simulated needs of a character, each 0 (empty, urgent) to 100
/// (fully satisfied). Hunger is stored as fullness, so 100 means not hungry.
/// </summary>
public sealed class Needs
{
    public static readonly NeedKind[] All = Enum.GetValues<NeedKind>();

    private readonly float[] _values = [100f, 85f, 90f, 80f, 90f, 95f, 70f, 75f];

    public float this[NeedKind kind]
    {
        get => _values[(int)kind];
        set => _values[(int)kind] = Math.Clamp(value, 0f, 100f);
    }

    public float[] Values
    {
        get => (float[])_values.Clone();
        set
        {
            for (int i = 0; i < _values.Length && i < value.Length; i++)
            {
                _values[i] = Math.Clamp(value[i], 0f, 100f);
            }
        }
    }

    public void Add(NeedKind kind, float delta) => this[kind] += delta;

    /// <summary>The need that most needs attention, and how urgent it is (0-1).</summary>
    public (NeedKind Kind, float Urgency) MostUrgent()
    {
        NeedKind worst = NeedKind.Fun;
        float urgency = -1f;
        foreach (NeedKind kind in All)
        {
            if (kind == NeedKind.Happiness)
            {
                continue;
            }

            float u = Urgency(kind);
            if (u > urgency)
            {
                urgency = u;
                worst = kind;
            }
        }

        return (worst, urgency);
    }

    /// <summary>Non-linear urgency: needs only start to shout below ~50%.</summary>
    public float Urgency(NeedKind kind)
    {
        float v = this[kind] / 100f;
        return MathF.Pow(1f - v, 2.2f);
    }

    /// <summary>Overall wellbeing used for happiness drift.</summary>
    public float Wellbeing()
    {
        float total = 0f;
        foreach (NeedKind kind in All)
        {
            if (kind != NeedKind.Happiness)
            {
                total += this[kind];
            }
        }

        return total / (All.Length - 1);
    }

    public static string Name(NeedKind kind) => kind switch
    {
        NeedKind.Health => Loc.T("Kesehatan", "Health"),
        NeedKind.Hunger => Loc.T("Kenyang", "Hunger"),
        NeedKind.Energy => Loc.T("Energi", "Energy"),
        NeedKind.Happiness => Loc.T("Bahagia", "Happiness"),
        NeedKind.Hygiene => Loc.T("Kebersihan", "Hygiene"),
        NeedKind.Sleep => Loc.T("Tidur", "Sleep"),
        NeedKind.Fun => Loc.T("Senang-senang", "Fun"),
        _ => Loc.T("Keluarga", "Family"),
    };

    public static string Icon(NeedKind kind) => kind switch
    {
        NeedKind.Health => "❤",
        NeedKind.Hunger => "🍚",
        NeedKind.Energy => "⚡",
        NeedKind.Happiness => "😊",
        NeedKind.Hygiene => "🛁",
        NeedKind.Sleep => "💤",
        NeedKind.Fun => "🎈",
        _ => "👪",
    };
}

public enum StaminaState
{
    Ready,
    Tired,
    Exhausted,
    MustRecover,
}

/// <summary>
/// Physical stamina, spent by running, rescues and demanding tasks. The
/// thresholds come from the design document: 100% ready, 60% tired, 20%
/// exhausted, 0% must recover.
/// </summary>
public sealed class Stamina
{
    private float _value = 100f;
    private bool _recovering;

    public float Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0f, 100f);
            if (_value <= 0f)
            {
                _recovering = true;
            }
            else if (_recovering && _value >= 35f)
            {
                _recovering = false;
            }
        }
    }

    public bool Recovering
    {
        get => _recovering;
        set => _recovering = value;
    }

    public StaminaState State => _recovering || _value <= 0f ? StaminaState.MustRecover
        : _value <= 20f ? StaminaState.Exhausted
        : _value <= 60f ? StaminaState.Tired
        : StaminaState.Ready;

    /// <summary>Demanding actions (running, carrying, rescues) are blocked when exhausted.</summary>
    public bool CanDoDemanding => State is StaminaState.Ready or StaminaState.Tired;

    public bool CanRun => State is StaminaState.Ready or StaminaState.Tired;

    /// <summary>Spends stamina if possible; returns false when the action must be refused.</summary>
    public bool TrySpend(float amount)
    {
        if (!CanDoDemanding)
        {
            return false;
        }

        Value -= amount;
        return true;
    }

    public static string Label(StaminaState state) => state switch
    {
        StaminaState.Ready => Loc.T("Siap", "Ready"),
        StaminaState.Tired => Loc.T("Lelah", "Tired"),
        StaminaState.Exhausted => Loc.T("Kelelahan", "Exhausted"),
        _ => Loc.T("Harus istirahat", "Must recover"),
    };
}
