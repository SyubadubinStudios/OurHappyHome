namespace OurHappyHome.Core.Family;

public enum MoodKind
{
    Happy,
    Content,
    Excited,
    Proud,
    Sad,
    Annoyed,
    Scared,
    Tired,
    Sick,
    Bored,
}

/// <summary>A temporary emotional effect, e.g. "Ate a perfect pancake" +15 for 4 hours.</summary>
public sealed class Moodlet
{
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public float Value { get; set; }
    public MoodKind Kind { get; set; } = MoodKind.Happy;
    public double ExpiresAt { get; set; }
}

/// <summary>
/// Mood is derived from needs plus active moodlets. The dominant moodlet
/// colours the mood (a scary thunderstorm makes a character Scared even when
/// fed and rested).
/// </summary>
public sealed class Mood
{
    public List<Moodlet> Moodlets { get; set; } = [];

    public MoodKind Current { get; set; } = MoodKind.Content;

    /// <summary>-100..100 overall feeling.</summary>
    public float Score { get; set; } = 30f;

    public void Add(string key, string text, float value, MoodKind kind, double now, double durationMinutes)
    {
        Moodlet? existing = Moodlets.Find(m => m.Key == key);
        if (existing is not null)
        {
            existing.ExpiresAt = Math.Max(existing.ExpiresAt, now + durationMinutes);
            existing.Value = value;
            existing.Text = text;
            existing.Kind = kind;
            return;
        }

        Moodlets.Add(new Moodlet { Key = key, Text = text, Value = value, Kind = kind, ExpiresAt = now + durationMinutes });
    }

    public void Remove(string key) => Moodlets.RemoveAll(m => m.Key == key);

    public bool Has(string key) => Moodlets.Exists(m => m.Key == key);

    public void Update(Needs needs, double now)
    {
        Moodlets.RemoveAll(m => m.ExpiresAt <= now);

        float fromNeeds = (needs.Wellbeing() - 55f) * 1.1f;
        float fromMoodlets = 0f;
        Moodlet? strongest = null;
        foreach (Moodlet m in Moodlets)
        {
            fromMoodlets += m.Value;
            if (strongest is null || MathF.Abs(m.Value) > MathF.Abs(strongest.Value))
            {
                strongest = m;
            }
        }

        Score = Math.Clamp(fromNeeds + fromMoodlets, -100f, 100f);

        if (strongest is not null && MathF.Abs(strongest.Value) >= 12f)
        {
            Current = strongest.Kind;
        }
        else if (needs[NeedKind.Health] < 35f)
        {
            Current = MoodKind.Sick;
        }
        else if (needs[NeedKind.Sleep] < 25f || needs[NeedKind.Energy] < 20f)
        {
            Current = MoodKind.Tired;
        }
        else if (needs[NeedKind.Fun] < 25f)
        {
            Current = MoodKind.Bored;
        }
        else if (Score > 45f)
        {
            Current = MoodKind.Happy;
        }
        else if (Score > 5f)
        {
            Current = MoodKind.Content;
        }
        else if (Score > -30f)
        {
            Current = MoodKind.Annoyed;
        }
        else
        {
            Current = MoodKind.Sad;
        }
    }

    public static string Emoji(MoodKind mood) => mood switch
    {
        MoodKind.Happy => "😄",
        MoodKind.Content => "🙂",
        MoodKind.Excited => "🤩",
        MoodKind.Proud => "😎",
        MoodKind.Sad => "😢",
        MoodKind.Annoyed => "😠",
        MoodKind.Scared => "😨",
        MoodKind.Tired => "🥱",
        MoodKind.Sick => "🤒",
        _ => "😐",
    };

    public static string Name(MoodKind mood) => mood switch
    {
        MoodKind.Happy => Loc.T("Bahagia", "Happy"),
        MoodKind.Content => Loc.T("Tenang", "Content"),
        MoodKind.Excited => Loc.T("Bersemangat", "Excited"),
        MoodKind.Proud => Loc.T("Bangga", "Proud"),
        MoodKind.Sad => Loc.T("Sedih", "Sad"),
        MoodKind.Annoyed => Loc.T("Kesal", "Annoyed"),
        MoodKind.Scared => Loc.T("Takut", "Scared"),
        MoodKind.Tired => Loc.T("Lelah", "Tired"),
        MoodKind.Sick => Loc.T("Kurang sehat", "Unwell"),
        _ => Loc.T("Bosan", "Bored"),
    };
}
