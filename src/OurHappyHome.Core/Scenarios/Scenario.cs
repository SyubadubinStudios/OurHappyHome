using System.Numerics;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core.Scenarios;

public enum ScenarioKind
{
    CatVisitor,
    LightBulb,
    FaucetLeak,
    ApplianceBroken,
    MonkeyThief,
    PowerOutage,
    LocalFlood,
    DangerousAnimal,
    WildAnimal,
    SuspiciousStranger,
    Burglary,
    SmallFire,
    GreatStorm,
}

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
}

public enum ActorKind
{
    Cat,
    Monkey,
    Snake,
    Boar,
    Stranger,
    Police,
    Firefighter,
    Rescuer,
}

/// <summary>A creature or visitor that only exists during a scenario.</summary>
public sealed class ScenarioActor
{
    public int Id { get; init; }
    public ActorKind Kind { get; init; }
    public Vector2 Position { get; set; }
    public Vector2 Target { get; set; }
    public float Speed { get; set; } = 1.2f;
    public float Yaw { get; set; }
    public bool Moving { get; set; }
    public bool Visible { get; set; } = true;
    public string Label { get; set; } = "";
}

public sealed class Objective
{
    public string Id { get; init; } = "";
    public string Text { get; set; } = "";
    public string Icon { get; init; } = "•";
    public bool Done { get; set; }
    public bool Optional { get; init; }
    public Vector2? Target { get; set; }
}

/// <summary>
/// A random or scripted event with objectives (design section 13). Major
/// scenarios can put family members in Down / Trapped / Needs Help states and
/// fail with "Nobody Gets Left Behind"; minor ones simply resolve.
/// </summary>
public abstract class Scenario
{
    private int _nextActor = 1;

    protected Scenario(GameSession session, ScenarioKind kind)
    {
        S = session;
        Kind = kind;
        StartedAt = session.Now;
    }

    protected GameSession S { get; }

    protected GameState State => S.State;

    protected House House => S.State.House;

    public ScenarioKind Kind { get; }

    public abstract string Title { get; }

    public abstract string Icon { get; }

    public abstract Rarity Rarity { get; }

    /// <summary>Major scenarios use rescue timers, can fail, and freeze the game speed at 1×.</summary>
    public virtual bool Major => false;

    /// <summary>Counts as dangerous for Cozy / Adventure scaling.</summary>
    public virtual bool Dangerous => Major;

    public double StartedAt { get; }

    public List<Objective> Objectives { get; } = [];

    public List<ScenarioActor> Actors { get; } = [];

    public bool Finished { get; private set; }

    public bool Succeeded { get; private set; }

    /// <summary>Rooms kept dark (broken bulb) for the renderer.</summary>
    public HashSet<RoomId> DarkRooms { get; } = [];

    public static Scenario Create(ScenarioKind kind, GameSession session) => kind switch
    {
        ScenarioKind.CatVisitor => new CatVisitorScenario(session),
        ScenarioKind.LightBulb => new LightBulbScenario(session),
        ScenarioKind.FaucetLeak => new FaucetLeakScenario(session),
        ScenarioKind.ApplianceBroken => new ApplianceScenario(session),
        ScenarioKind.MonkeyThief => new MonkeyScenario(session),
        ScenarioKind.PowerOutage => new PowerOutageScenario(session),
        ScenarioKind.LocalFlood => new FloodScenario(session),
        ScenarioKind.DangerousAnimal => new SnakeScenario(session),
        ScenarioKind.WildAnimal => new WildAnimalScenario(session),
        ScenarioKind.SuspiciousStranger => new StrangerScenario(session),
        ScenarioKind.Burglary => new BurglaryScenario(session),
        ScenarioKind.SmallFire => new FireScenario(session),
        _ => new GreatStormScenario(session),
    };

    public static Rarity RarityOf(ScenarioKind kind) => kind switch
    {
        ScenarioKind.CatVisitor or ScenarioKind.LightBulb or ScenarioKind.FaucetLeak or ScenarioKind.ApplianceBroken => Rarity.Common,
        ScenarioKind.MonkeyThief or ScenarioKind.PowerOutage or ScenarioKind.LocalFlood or ScenarioKind.DangerousAnimal or ScenarioKind.WildAnimal => Rarity.Uncommon,
        _ => Rarity.Rare,
    };

    public abstract void Start();

    public virtual void Tick(float minutes, float realDt)
    {
        MoveActors(realDt);
    }

    /// <summary>Scenario specific things to do near the player.</summary>
    public virtual IEnumerable<InteractionTarget> Targets(FamilyMember me) => [];

    /// <summary>Lets the scenario steer an autonomous member; return true if it gave them a task.</summary>
    public virtual bool Direct(FamilyMember m) => false;

    /// <summary>Whether this member drops what they are doing when the scenario starts.</summary>
    public virtual bool InterruptOnStart(FamilyMember m) => Major;

    // Hooks from the simulation.
    public virtual void OnPowerFixed(FamilyMember by) => House.PowerOn = true;

    public virtual void OnCleaned(FamilyMember by, int removed) { }

    public virtual void OnCamerasChecked(FamilyMember by) { }

    public virtual void OnComforted(FamilyMember by, FamilyMember other) { }

    public virtual void OnTaskCompleted(FamilyMember m, ActivityId activity, MemberTask task) { }

    public virtual void OnCooked(FamilyMember m, Recipe recipe, CookQuality quality) { }

    public virtual void OnRepaired(FamilyMember m, FurnitureItem item) { }

    public virtual void OnWarmDrinks(FamilyMember m) { }

    public virtual void OnRescued(FamilyMember by, FamilyMember other) { }

    public virtual void OnSafetyChanged() { }

    public virtual void OnFireOut(FamilyMember by) { }

    // ------------------------------------------------------------- helpers

    public string Banner
    {
        get
        {
            FamilyMember? danger = State.Members.Where(m => m.InDanger).OrderBy(m => m.RescueTimer).FirstOrDefault();
            if (danger is null)
            {
                return "";
            }

            string state = danger.Safety switch
            {
                SafetyState.Trapped => Loc.T("TERJEBAK", "IS TRAPPED"),
                SafetyState.Down => Loc.T("LEMAS", "IS DOWN"),
                _ => Loc.T("BUTUH BANTUAN", "NEEDS HELP"),
            };
            return $"{danger.Name.ToUpperInvariant()} {state}";
        }
    }

    public float? Countdown => State.Members.Where(m => m.InDanger).Select(m => (float?)m.RescueTimer).Min();

    protected Objective Add(string id, string textId, string textEn, string icon, bool optional = false, Vector2? target = null)
    {
        Objective o = new() { Id = id, Text = Loc.T(textId, textEn), Icon = icon, Optional = optional, Target = target };
        Objectives.Add(o);
        return o;
    }

    protected bool IsDone(string id) => Objectives.FirstOrDefault(o => o.Id == id)?.Done ?? false;

    protected void Done(string id)
    {
        if (Objectives.FirstOrDefault(o => o.Id == id) is { Done: false } o)
        {
            o.Done = true;
            S.Bus.Notice(Loc.T($"✓ {o.Text}", $"✓ {o.Text}"), "✅", NoticeKind.Good);
            S.Bus.Sound("objective");
        }
    }

    protected bool RequiredDone => Objectives.Where(o => !o.Optional).All(o => o.Done);

    protected void Complete(bool success)
    {
        if (Finished)
        {
            return;
        }

        Finished = true;
        Succeeded = success;
        foreach (FamilyMember m in State.Members.Where(m => m.InDanger))
        {
            m.Safety = SafetyState.Normal;
        }
    }

    protected ScenarioActor Spawn(ActorKind kind, Vector2 position, string label = "")
    {
        ScenarioActor actor = new() { Id = _nextActor++, Kind = kind, Position = position, Target = position, Label = label };
        Actors.Add(actor);
        return actor;
    }

    protected void MoveActors(float realDt)
    {
        foreach (ScenarioActor a in Actors)
        {
            Vector2 to = a.Target - a.Position;
            float d = to.Length();
            if (d > 0.15f)
            {
                a.Position += to / d * MathF.Min(d, a.Speed * realDt);
                a.Yaw = MathF.Atan2(to.X, to.Y);
                a.Moving = true;
            }
            else
            {
                a.Moving = false;
            }
        }
    }

    protected float RescueSeconds(float baseSeconds)
    {
        float mode = State.Mode switch
        {
            GameMode.Cozy => 1.7f,
            GameMode.Adventure => 0.8f,
            _ => 1f,
        };
        return baseSeconds * mode * S.Settings.RescueTimeScale * (S.Settings.ReducedIntensity ? 1.3f : 1f);
    }

    protected void Danger(FamilyMember m, SafetyState state, float baseSeconds, Vector2? at = null)
    {
        S.CancelTask(m);
        m.Away = false;
        if (at is { } p)
        {
            m.Position = p;
        }

        m.Safety = state;
        m.RescueTimer = RescueSeconds(baseSeconds);
        m.Mood.Add("scared", Loc.T("Ketakutan!", "Frightened!"), -20, MoodKind.Scared, S.Now, 60);
        string voice = m.Id switch
        {
            MemberId.YoungerSister => "ys_help",
            MemberId.OlderSister => "os_help",
            MemberId.Mother => "mom_help",
            MemberId.Father => "dad_help",
            _ => "boy_help",
        };
        S.Say(m, Loc.T("Tolong! Aku di sini!", "Help! I'm over here!"), voice);
        S.Bus.Sound("alert");
    }

    /// <summary>Counts rescue timers down and marks followers who reached a safe zone.</summary>
    protected void UpdateRescues(float realDt, Func<Vector2, bool> isSafe)
    {
        foreach (FamilyMember m in State.Members)
        {
            if (m.InDanger)
            {
                m.RescueTimer -= realDt;
                if (m.RescueTimer <= 0f)
                {
                    S.FailScenario(Loc.T($"{m.Name} tidak sempat diselamatkan.", $"{m.Name} could not be reached in time."));
                    return;
                }
            }
            else if (m.Safety == SafetyState.Following && m.FollowTarget is not null && isSafe(m.Position))
            {
                m.Safety = SafetyState.Safe;
                m.FollowTarget = null;
                State.AddStat(Progression.Stat.Rescues);
                S.Bus.Notice(Loc.T($"{m.Name} sudah aman!", $"{m.Name} is safe!"), "💚", NoticeKind.Good);
                S.Bus.Effect(EffectKind.Hearts, m.Position, 1.8f);
                S.Say(m, Loc.T("Terima kasih sudah menyelamatkanku!", "Thank you for saving me!"));
            }
        }
    }

    protected bool AnyoneInDanger => State.Members.Any(m => m.InDanger || (m.Safety == SafetyState.Following && m.FollowTarget is not null));

    protected InteractionTarget Here(string key, string title, string icon, List<InteractionOption> options)
    {
        Vector2 p = S.Controlled.Position;
        return new InteractionTarget(key, title, icon, new Vector3(p.X, 2.2f, p.Y), 0.05f, options);
    }

    protected InteractionTarget At(string key, string title, string icon, Vector2 position, float height, List<InteractionOption> options) =>
        new(key, title, icon, new Vector3(position.X, height, position.Y), Vector2.Distance(position, S.Controlled.Position), options);

    protected IEnumerable<FamilyMember> Present => State.Members.Where(m => !m.Away);

    protected bool InRoom(FamilyMember m, params RoomId[] rooms) => !m.Away && House.RoomAt(m.Position) is { } r && rooms.Contains(r);

    protected static Rect RoomArea(RoomId room) => Rooms.Get(room).Area;

    protected void Memory(string titleId, string titleEn, string descId, string descEn, EmotionalOutcome outcome, float importance = 3f) =>
        S.CreateMemory(Loc.T(titleId, titleEn), Loc.T(descId, descEn), MemoryKind.Emergency, outcome,
            [.. Present.Select(m => m.Id)], S.LocationName(S.Controlled), importance, $"scenario:{Kind}");

    /// <summary>Sends an autonomous member somewhere, running.</summary>
    protected void RunTo(FamilyMember m, Vector2 target, ActivityId activity = ActivityId.Hide, float minutes = 30f)
    {
        S.StartTask(m, activity, null, -1, target: target, run: true, minutes: minutes);
    }
}
