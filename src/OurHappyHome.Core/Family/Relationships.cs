namespace OurHappyHome.Core.Family;

public enum RelationshipStatus
{
    Close,
    Good,
    Okay,
    Distant,
    Upset,
}

/// <summary>
/// Directed relationship values between the five members (0-100). A -> B
/// can differ from B -> A, as in the design document's example.
/// </summary>
public sealed class Relationships
{
    private const int Count = 5;
    private float[] _values = new float[Count * Count];

    /// <summary>Remaining minutes of an argument between two members (symmetric).</summary>
    private float[] _argument = new float[Count * Count];

    public Relationships()
    {
        foreach (MemberId a in FamilyNames.All)
        {
            foreach (MemberId b in FamilyNames.All)
            {
                if (a != b)
                {
                    this[a, b] = 70f;
                }
            }
        }

        // Starting values echo the design document.
        this[MemberId.YoungerSister, MemberId.OlderSister] = 80f;
        this[MemberId.Player, MemberId.Father] = 92f;
        this[MemberId.Player, MemberId.YoungerSister] = 61f;
        this[MemberId.Player, MemberId.Mother] = 90f;
        this[MemberId.Mother, MemberId.Father] = 88f;
        this[MemberId.Father, MemberId.Mother] = 90f;
        this[MemberId.OlderSister, MemberId.Player] = 58f;
        this[MemberId.YoungerSister, MemberId.Mother] = 90f;
    }

    public float this[MemberId from, MemberId to]
    {
        get => from == to ? 100f : _values[((int)from * Count) + (int)to];
        set
        {
            if (from != to)
            {
                _values[((int)from * Count) + (int)to] = Math.Clamp(value, 0f, 100f);
            }
        }
    }

    public float[] Values
    {
        get => (float[])_values.Clone();
        set => _values = value.Length == Count * Count ? (float[])value.Clone() : _values;
    }

    public float[] Arguments
    {
        get => (float[])_argument.Clone();
        set => _argument = value.Length == Count * Count ? (float[])value.Clone() : _argument;
    }

    /// <summary>Changes both directions; the receiver feels it a little more.</summary>
    public void Change(MemberId a, MemberId b, float amount)
    {
        this[a, b] += amount;
        this[b, a] += amount * 1.1f;
    }

    public void StartArgument(MemberId a, MemberId b, float minutes)
    {
        _argument[((int)a * Count) + (int)b] = minutes;
        _argument[((int)b * Count) + (int)a] = minutes;
        Change(a, b, -6f);
    }

    public bool Arguing(MemberId a, MemberId b) => _argument[((int)a * Count) + (int)b] > 0f;

    public void Reconcile(MemberId a, MemberId b)
    {
        _argument[((int)a * Count) + (int)b] = 0f;
        _argument[((int)b * Count) + (int)a] = 0f;
        Change(a, b, 4f);
    }

    /// <summary>Arguments cool down over time.</summary>
    public IEnumerable<(MemberId A, MemberId B)> Tick(float minutes)
    {
        for (int i = 0; i < _argument.Length; i++)
        {
            if (_argument[i] > 0f)
            {
                _argument[i] -= minutes;
                if (_argument[i] <= 0f)
                {
                    _argument[i] = 0f;
                    int a = i / Count;
                    int b = i % Count;
                    if (a < b)
                    {
                        yield return ((MemberId)a, (MemberId)b);
                    }
                }
            }
        }
    }

    public float FamilyAverage()
    {
        float total = 0f;
        int n = 0;
        foreach (MemberId a in FamilyNames.All)
        {
            foreach (MemberId b in FamilyNames.All)
            {
                if (a != b)
                {
                    total += this[a, b];
                    n++;
                }
            }
        }

        return total / n;
    }

    public RelationshipStatus Status(MemberId a, MemberId b)
    {
        if (Arguing(a, b))
        {
            return RelationshipStatus.Upset;
        }

        float v = this[a, b];
        return v >= 85f ? RelationshipStatus.Close
            : v >= 65f ? RelationshipStatus.Good
            : v >= 45f ? RelationshipStatus.Okay
            : RelationshipStatus.Distant;
    }

    public static string StatusName(RelationshipStatus status) => status switch
    {
        RelationshipStatus.Close => Loc.T("Sangat dekat", "Very close"),
        RelationshipStatus.Good => Loc.T("Akrab", "Good"),
        RelationshipStatus.Okay => Loc.T("Biasa", "Okay"),
        RelationshipStatus.Distant => Loc.T("Renggang", "Distant"),
        _ => Loc.T("Sedang marahan", "Upset"),
    };
}
