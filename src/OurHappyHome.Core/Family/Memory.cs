namespace OurHappyHome.Core.Family;

public enum MemoryKind
{
    Cooking,
    Meal,
    Gift,
    Trip,
    Argument,
    Reconcile,
    Achievement,
    Celebration,
    Emergency,
    Rescue,
    Pet,
    School,
    Building,
    Play,
    Birthday,
    Weather,
    Photo,
    Chapter,
}

public enum EmotionalOutcome
{
    Joyful,
    Heartwarming,
    Funny,
    Proud,
    Scary,
    Sad,
    Relieved,
}

/// <summary>
/// A meaningful moment: who was there, where, what happened, how it felt and
/// how it changed their relationships. Memories with a photo fill the album.
/// </summary>
public sealed class FamilyMemory
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public MemoryKind Kind { get; set; }
    public EmotionalOutcome Outcome { get; set; }
    public int DayIndex { get; set; }
    public double Minute { get; set; }
    public string Location { get; set; } = "";
    public List<MemberId> Participants { get; set; } = [];
    public float RelationshipChange { get; set; }
    public string? PhotoFile { get; set; }

    /// <summary>Album decoration: frame style and up to three stickers (emoji).</summary>
    public string Frame { get; set; } = "classic";

    public List<string> Stickers { get; set; } = [];

    /// <summary>Optional key used by the AI to recall it (e.g. "recipe:Pancakes").</summary>
    public string Tag { get; set; } = "";

    public float Importance { get; set; } = 1f;

    public bool Involves(MemberId member) => Participants.Contains(member);

    public static string OutcomeIcon(EmotionalOutcome outcome) => outcome switch
    {
        EmotionalOutcome.Joyful => "😄",
        EmotionalOutcome.Heartwarming => "💗",
        EmotionalOutcome.Funny => "😂",
        EmotionalOutcome.Proud => "🏆",
        EmotionalOutcome.Scary => "😱",
        EmotionalOutcome.Sad => "😢",
        _ => "😌",
    };

    public static string OutcomeName(EmotionalOutcome outcome) => outcome switch
    {
        EmotionalOutcome.Joyful => Loc.T("Gembira", "Joyful"),
        EmotionalOutcome.Heartwarming => Loc.T("Mengharukan", "Heartwarming"),
        EmotionalOutcome.Funny => Loc.T("Lucu", "Funny"),
        EmotionalOutcome.Proud => Loc.T("Membanggakan", "Proud"),
        EmotionalOutcome.Scary => Loc.T("Menegangkan", "Scary"),
        EmotionalOutcome.Sad => Loc.T("Sedih", "Sad"),
        _ => Loc.T("Lega", "Relieved"),
    };
}
