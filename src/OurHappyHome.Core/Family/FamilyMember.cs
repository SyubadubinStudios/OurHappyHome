using System.Numerics;
using System.Text.Json.Serialization;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Family;

/// <summary>Danger states from the rescue system (design section 16).</summary>
public enum SafetyState
{
    Normal,
    NeedsHelp,
    Trapped,
    Down,
    Following,
    Safe,
}

public enum TaskPhase
{
    Walking,
    Performing,
    Done,
}

/// <summary>What a character is doing right now: walking to something, then using it.</summary>
public sealed class MemberTask
{
    public ActivityId Activity { get; set; }
    public int? FurnitureUid { get; set; }
    public int Slot { get; set; } = -1;
    public Vector2 Target { get; set; }
    public float? TargetYaw { get; set; }
    public TaskPhase Phase { get; set; } = TaskPhase.Walking;
    public List<Vector2>? Path { get; set; }
    public int PathIndex { get; set; }
    public float Remaining { get; set; }
    public float Total { get; set; }
    public MemberId? Partner { get; set; }
    public string? RecipeId { get; set; }
    public bool Run { get; set; }
    public bool FromPlayer { get; set; }
    public bool Interruptible { get; set; } = true;

    /// <summary>Scenario specific outcome, e.g. "extinguish:12" or "repair:5".</summary>
    public string Tag { get; set; } = "";

    /// <summary>Mini-game result for cooking started by the player (0-1).</summary>
    public float? MiniGameScore { get; set; }

    public float Progress => Total <= 0 ? 0 : 1f - (Remaining / Total);

    /// <summary>Real seconds spent walking; a walk that takes far too long gives up.</summary>
    public float WalkTime { get; set; }

    public float WalkBudget { get; set; }

    public ActivityDef Def => ActivityCatalog.Get(Activity);
}

/// <summary>
/// One of the five independently simulated family members: needs, stamina,
/// mood, skills, personality, memories, position and current task.
/// </summary>
public sealed class FamilyMember
{
    public MemberId Id { get; set; }
    public Needs Needs { get; set; } = new();
    public Stamina Stamina { get; set; } = new();
    public Mood Mood { get; set; } = new();
    public Skills Skills { get; set; } = new();
    public Personality Personality { get; set; } = new();

    public Vector2 Position { get; set; }
    public float Yaw { get; set; }

    /// <summary>Away from the visible world (school, work, errands).</summary>
    public bool Away { get; set; }

    public ActivityId AwayActivity { get; set; }

    public double AwayUntil { get; set; }

    public SafetyState Safety { get; set; } = SafetyState.Normal;

    /// <summary>Real seconds left before a rescue fails (when NeedsHelp / Trapped / Down).</summary>
    public float RescueTimer { get; set; }

    public bool Sick { get; set; }

    /// <summary>On the stairs between floors (the renderer follows the steps).</summary>
    public StairClimb? Climb { get; set; }

    /// <summary>Costume item worn on the head (party hat, straw hat...), or null.</summary>
    public string? Accessory { get; set; }

    /// <summary>
    /// True until the player picks a costume: the member then dresses for the
    /// weather on their own (rain hat in the rain, straw hat in the dry-season sun).
    /// </summary>
    public bool AccessoryAuto { get; set; } = true;

    public List<int> MemoryIds { get; set; } = [];

    /// <summary>Who this member is following on a trip or rescue.</summary>
    public MemberId? FollowTarget { get; set; }

    [JsonIgnore]
    public MemberTask? Task { get; set; }

    [JsonIgnore]
    public string? Bubble { get; set; }

    [JsonIgnore]
    public double BubbleUntil { get; set; }

    [JsonIgnore]
    public bool Moving { get; set; }

    [JsonIgnore]
    public bool Running { get; set; }

    /// <summary>Height above the floor while sitting or lying on furniture.</summary>
    [JsonIgnore]
    public Vector3? Anchor { get; set; }

    [JsonIgnore]
    public AnchorPose Pose { get; set; } = AnchorPose.Stand;

    [JsonIgnore]
    public float IdleTimer { get; set; }

    [JsonIgnore]
    public float StuckTimer { get; set; }

    /// <summary>Waypoints used while following someone around walls.</summary>
    [JsonIgnore]
    public List<Vector2>? FollowPath { get; set; }

    [JsonIgnore]
    public float RepathTimer { get; set; }

    public string Name => FamilyNames.Short(Id);

    public bool IsChild => FamilyNames.IsChild(Id);

    public bool InDanger => Safety is SafetyState.NeedsHelp or SafetyState.Trapped or SafetyState.Down;

    public ActivityId CurrentActivity => Away ? AwayActivity : Task?.Activity ?? ActivityId.Idle;

    public string StatusText
    {
        get
        {
            if (Safety == SafetyState.NeedsHelp)
            {
                return Loc.T("Butuh bantuan!", "Needs help!");
            }

            if (Safety == SafetyState.Trapped)
            {
                return Loc.T("Terjebak!", "Trapped!");
            }

            if (Safety == SafetyState.Down)
            {
                return Loc.T("Lemas, butuh pertolongan", "Down, needs help");
            }

            if (Safety == SafetyState.Following)
            {
                return Loc.T("Ikut kamu", "Following you");
            }

            if (Away)
            {
                return ActivityCatalog.Get(AwayActivity).Name;
            }

            if (Task is { } task)
            {
                return task.Phase == TaskPhase.Walking
                    ? Loc.T($"Menuju: {task.Def.Name}", $"Going to: {task.Def.Name}")
                    : task.Def.Name;
            }

            return Loc.T("Santai", "Relaxing");
        }
    }

    /// <summary>The clip the renderer should play.</summary>
    public string Animation
    {
        get
        {
            if (Safety is SafetyState.NeedsHelp or SafetyState.Trapped)
            {
                return "Scared";
            }

            if (Safety == SafetyState.Down)
            {
                return "Sit";
            }

            if (Moving)
            {
                return Running ? "Run" : "Walk";
            }

            if (Task is { Phase: TaskPhase.Performing } task)
            {
                if (Pose == AnchorPose.Sit && task.Def.Animation is "Read" or "Talk" or "Idle")
                {
                    return "Sit";
                }

                return Pose switch
                {
                    AnchorPose.Lie => "Sleep",
                    AnchorPose.Sit => "Sit",
                    _ => task.Def.Animation,
                };
            }

            if (Mood.Current == MoodKind.Scared)
            {
                return "Scared";
            }

            return "Idle";
        }
    }

    public void Say(string text, double now, double minutes = 2.5)
    {
        Bubble = text;
        BubbleUntil = now + minutes;
    }

    public static FamilyMember Create(MemberId id)
    {
        FamilyMember m = new() { Id = id };
        Personality p = m.Personality;
        Skills s = m.Skills;
        switch (id)
        {
            case MemberId.Father:
                p.Traits.AddRange(["Pelindung", "Suka bercanda", "Pekerja keras"]);
                p.Likes.AddRange(["repair", "building", "outdoor", "cars", "sports"]);
                p.Dislikes.Add("mess");
                p.Fears.Add("family-in-danger");
                p.Hobbies.AddRange([SkillKind.Repair, SkillKind.Building, SkillKind.Outdoor]);
                p.Goals.AddRange([Loc.T("Membangun rumah impian", "Build the dream home"), Loc.T("Mengajak keluarga berkemah", "Take the family camping")]);
                p.Fearfulness = 0.1f;
                p.Sociability = 0.55f;
                p.WakeHour = 6.1f;
                p.BedHour = 22.5f;
                s[SkillKind.Repair] = 4.2f;
                s[SkillKind.Building] = 3.8f;
                s[SkillKind.Driving] = 5f;
                s[SkillKind.Outdoor] = 3.5f;
                s[SkillKind.Protection] = 4f;
                s[SkillKind.Cooking] = 1.2f;
                s[SkillKind.FirstAid] = 2f;
                s[SkillKind.Swimming] = 4f;
                break;
            case MemberId.Mother:
                p.Traits.AddRange(["Penyayang", "Teratur", "Kreatif"]);
                p.Likes.AddRange(["cooking", "gardening", "flowers", "household", "music"]);
                p.Dislikes.Add("arguments");
                p.Fears.Add("fire");
                p.Hobbies.AddRange([SkillKind.Cooking, SkillKind.Gardening, SkillKind.Music]);
                p.Goals.AddRange([Loc.T("Membuka usaha kue rumahan", "Start a home bakery"), Loc.T("Kebun bunga yang indah", "A beautiful flower garden")]);
                p.Fearfulness = 0.25f;
                p.Sociability = 0.7f;
                p.WakeHour = 5.9f;
                p.BedHour = 22f;
                s[SkillKind.Cooking] = 4.5f;
                s[SkillKind.Shopping] = 3.5f;
                s[SkillKind.Gardening] = 3f;
                s[SkillKind.Household] = 4f;
                s[SkillKind.Care] = 4.5f;
                s[SkillKind.FirstAid] = 3f;
                s[SkillKind.Driving] = 2.5f;
                break;
            case MemberId.OlderSister:
                p.Traits.AddRange(["Ceria", "Artistik", "Suka tidur"]);
                p.Likes.AddRange(["drawing", "art", "music", "fashion", "gardening", "photography", "cute", "flowers"]);
                p.Dislikes.Add("homework");
                p.Fears.AddRange(["dark", "thunder"]);
                p.Hobbies.AddRange([SkillKind.Drawing, SkillKind.Music, SkillKind.Fashion, SkillKind.Photography]);
                p.Goals.AddRange([Loc.T("Pameran gambar di sekolah", "Show drawings at school"), Loc.T("Punya kamar sendiri", "A room of her own")]);
                p.Fearfulness = 0.45f;
                p.Sociability = 0.6f;
                p.WakeHour = 6.75f;
                p.BedHour = 21.25f;
                s[SkillKind.Drawing] = 3f;
                s[SkillKind.Music] = 1.5f;
                s[SkillKind.Fashion] = 2f;
                s[SkillKind.Photography] = 1f;
                s[SkillKind.English] = 2.5f;
                s[SkillKind.Art] = 3f;
                s[SkillKind.Math] = 2f;
                break;
            case MemberId.Player:
                p.Traits.AddRange(["Pemberani", "Serba bisa", "Sayang Ibu"]);
                p.Likes.AddRange(["sports", "cars", "toys", "outdoor", "cycling", "repair"]);
                p.Dislikes.Add("vegetables");
                p.Fears.Add("losing-family");
                p.Hobbies.AddRange([SkillKind.Cycling, SkillKind.Sports, SkillKind.Crafting]);
                p.Goals.AddRange([Loc.T("Menjaga keluarga", "Keep the family safe"), Loc.T("Rumah yang bahagia", "A happy home")]);
                p.Fearfulness = 0.2f;
                p.Sociability = 0.6f;
                p.WakeHour = 6.3f;
                p.BedHour = 21f;
                s[SkillKind.Cooking] = 0.8f;
                s[SkillKind.Cycling] = 2f;
                s[SkillKind.Sports] = 2f;
                s[SkillKind.Repair] = 0.8f;
                s[SkillKind.Math] = 2f;
                s[SkillKind.English] = 1.5f;
                s[SkillKind.Science] = 1.5f;
                break;
            default:
                p.Traits.AddRange(["Ceria", "Cerdas", "Ingin tahu", "Suka menolong"]);
                p.Likes.AddRange(["books", "english", "cooking", "cute", "toys", "animals"]);
                p.Dislikes.Add("loud-noises");
                p.Fears.AddRange(["thunder", "dark"]);
                p.Hobbies.AddRange([SkillKind.Reading, SkillKind.English, SkillKind.Cooking]);
                p.Goals.AddRange([Loc.T("Membaca 100 buku bahasa Inggris", "Read 100 English books"), Loc.T("Memasak sarapan untuk keluarga", "Cook breakfast for the family")]);
                p.Fearfulness = 0.6f;
                p.Sociability = 0.8f;
                p.WakeHour = 6.05f;
                p.BedHour = 20.5f;
                s[SkillKind.Reading] = 3f;
                s[SkillKind.English] = 3.5f;
                s[SkillKind.Cooking] = 0.6f;
                s[SkillKind.Math] = 2.5f;
                s[SkillKind.Science] = 2f;
                break;
        }

        return m;
    }
}
