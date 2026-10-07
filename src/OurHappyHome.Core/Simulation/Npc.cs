using System.Numerics;
using OurHappyHome.Core.Time;

namespace OurHappyHome.Core.Simulation;

public enum NpcDays
{
    Any,

    /// <summary>Weekdays outside holidays and school breaks.</summary>
    SchoolDays,

    /// <summary>Weekends, holidays and school breaks.</summary>
    DaysOff,
}

/// <summary>
/// One entry of a neighbour's day: where they are between two hours. The first
/// matching stop wins; <see cref="MinFriendship"/> unlocks visits to the family.
/// </summary>
public sealed record NpcStop(float From, float To, Vector2 Spot, float Radius, NpcDays Days = NpcDays.Any, float MinFriendship = 0f, string Tag = "")
{
    public bool Matches(float hour, GameDate date, float friendship) =>
        hour >= From && hour < To && friendship >= MinFriendship
        && Days switch { NpcDays.SchoolDays => date.IsSchoolDay, NpcDays.DaysOff => !date.IsSchoolDay, _ => true };
}

/// <summary>A neighbour or townsperson the family can meet.</summary>
public sealed class Npc
{
    public string Id { get; init; } = "";
    public string NameId { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string RoleId { get; init; } = "";
    public string RoleEn { get; init; } = "";
    public string Model { get; init; } = "";
    public string Color { get; init; } = "#888888";
    public float Height { get; init; } = 1.6f;
    public Vector2 Home { get; init; }
    public float WanderRadius { get; init; } = 3f;
    public string[] LinesId { get; init; } = [];
    public string[] LinesEn { get; init; } = [];

    /// <summary>Their day, in priority order. Nobody is around when no stop matches.</summary>
    public List<NpcStop> Schedule { get; init; } = [];

    /// <summary>Where they are now (null: at home indoors, not visible).</summary>
    public NpcStop? Stop { get; private set; }

    public Vector2 Position { get; set; }
    public float Yaw { get; set; }
    public bool Moving { get; private set; }

    /// <summary>Seconds left in a conversation; the NPC stands still and plays its Talk clip.</summary>
    public float TalkTime { get; private set; }

    public bool Talking => TalkTime > 0f;
    private Vector2 _target;
    private float _timer;

    public string Name => Loc.T(NameId, NameEn);

    public string Role => Loc.T(RoleId, RoleEn);

    public string Line(int index) => Loc.T(LinesId[index % LinesId.Length], LinesEn[index % LinesEn.Length]);

    public bool Present => Stop is not null;

    /// <summary>Stops, turns to the listener and talks for a few seconds.</summary>
    public void TalkTo(Vector2 listener, float seconds = 4f)
    {
        Vector2 to = listener - Position;
        if (to.LengthSquared() > 1e-4f)
        {
            Yaw = MathF.Atan2(to.X, to.Y);
        }

        TalkTime = seconds;
        Moving = false;
    }

    /// <summary>Picks the current stop; far moves happen off-screen.</summary>
    public void FollowSchedule(float hour, GameDate date, float friendship)
    {
        NpcStop? stop = Schedule.FirstOrDefault(s => s.Matches(hour, date, friendship));
        if (stop == Stop)
        {
            return;
        }

        Stop = stop;
        if (stop is not null && Vector2.Distance(Position, stop.Spot) > 20f)
        {
            Position = stop.Spot;
            _target = stop.Spot;
        }

        _timer = 0f;
    }

    public void Update(float dt, GameRandom random)
    {
        if (Stop is null)
        {
            Moving = false;
            return;
        }

        if (TalkTime > 0f)
        {
            TalkTime -= dt;
            Moving = false;
            return;
        }

        _timer -= dt;
        if (_timer <= 0f)
        {
            _timer = random.Range(3f, 9f);
            float r = Stop.Radius;
            _target = Stop.Spot + new Vector2(random.Range(-r, r), random.Range(-r, r));
        }

        Vector2 to = _target - Position;
        float d = to.Length();
        if (d > 0.2f)
        {
            Position += to / d * MathF.Min(d, 1.0f * dt);
            Yaw = MathF.Atan2(to.X, to.Y);
            Moving = true;
        }
        else
        {
            Moving = false;
        }
    }

    /// <summary>An aisle of the supermarket interior.</summary>
    private static readonly Vector2 Supermarket = new(276f, -281.8f);

    public static List<Npc> CreateNeighbours()
    {
        List<Npc> npcs =
        [
            new()
            {
                Id = "grandma", NameId = "Nenek Sari", NameEn = "Grandma Sari", RoleId = "Tetangga sebelah", RoleEn = "Next-door neighbour",
                Model = "grandma", Color = "#B07AA1", Height = 1.5f, Home = new Vector2(30f, 10.5f), WanderRadius = 2.5f,
                Schedule =
                [
                    new(9f, 11f, Supermarket, 1.5f, NpcDays.DaysOff, Tag: "shopping"),
                    new(16f, 18f, new Vector2(-41f, 34.6f), 1.2f, NpcDays.SchoolDays, Tag: "park"),
                    new(6f, 20.5f, new Vector2(30f, 10.5f), 2.5f),
                ],
                LinesId = ["Halo cucu! Nenek baru bikin kue, mau?", "Rajin belajar ya, Nak.", "Kalau hujan besar, bawa masuk barang-barang di halaman ya."],
                LinesEn = ["Hello dear! I just baked cookies, want some?", "Study hard, little one.", "When big rain comes, bring the yard things inside."],
            },
            new()
            {
                Id = "budi", NameId = "Pak Budi", NameEn = "Mr. Budi", RoleId = "Tetangga, tukang kayu", RoleEn = "Neighbour, carpenter",
                Model = "neighbour", Color = "#5F8FBF", Height = 1.72f, Home = new Vector2(-55f, 10f), WanderRadius = 3f,
                Schedule =
                [
                    new(13f, 16f, new Vector2(-22f, 60f), 2.5f, NpcDays.DaysOff, Tag: "park"),
                    new(7f, 19f, new Vector2(-55f, 10f), 3f),
                ],
                LinesId = ["Pagar saya perlu dicat, mau bantu?", "Kalau ada yang rusak, panggil ayahmu ya!", "Badai kemarin besar sekali!"],
                LinesEn = ["My fence needs painting, want to help?", "If something breaks, call your dad!", "That storm was huge!"],
            },
            new()
            {
                Id = "dimas", NameId = "Dimas", NameEn = "Dimas", RoleId = "Teman sekelas", RoleEn = "Classmate",
                Model = "friend", Color = "#E07A5F", Height = 1.36f, Home = new Vector2(-28f, 42f), WanderRadius = 5f,
                Schedule =
                [
                    new(9f, 11.5f, new Vector2(3.5f, 12.4f), 1.2f, NpcDays.DaysOff, MinFriendship: 30f, Tag: "visit"),
                    new(7.25f, 13f, new Vector2(248f, -279.6f), 0.3f, NpcDays.SchoolDays, Tag: "class"),
                    new(14f, 18.5f, new Vector2(-28f, 42f), 5f, Tag: "park"),
                ],
                LinesId = ["Ayo main bola di taman!", "Kamu sudah kerjakan PR matematika?", "Besok ada festival, ikut ya!"],
                LinesEn = ["Let's play football in the park!", "Did you finish the maths homework?", "There's a festival tomorrow, come along!"],
            },
            new()
            {
                Id = "teacher", NameId = "Bu Guru Rina", NameEn = "Ms. Rina", RoleId = "Guru kelas", RoleEn = "Class teacher",
                Model = "teacher", Color = "#81B29A", Height = 1.62f, Home = new Vector2(249.2f, -283.5f), WanderRadius = 0.8f,
                Schedule =
                [
                    new(6.5f, 15f, new Vector2(249.2f, -283.5f), 0.8f, NpcDays.SchoolDays, Tag: "class"),
                    new(10f, 12f, Supermarket + new Vector2(2.5f, 0.3f), 1.2f, NpcDays.DaysOff, Tag: "shopping"),
                ],
                LinesId = ["Selamat pagi! Siap belajar hari ini?", "Bahasa Inggris adikmu bagus sekali.", "Jangan lupa membaca setiap hari."],
                LinesEn = ["Good morning! Ready to learn today?", "Your sister's English is excellent.", "Remember to read every day."],
            },
            new()
            {
                Id = "doctor", NameId = "dr. Sinta", NameEn = "Dr. Sinta", RoleId = "Dokter klinik", RoleEn = "Clinic doctor",
                Model = "doctor", Color = "#9BD1E5", Height = 1.62f, Home = new Vector2(246.5f, -259.6f), WanderRadius = 0.8f,
                Schedule = [new(7f, 21f, new Vector2(246.5f, -259.6f), 0.8f, Tag: "clinic")],
                LinesId = ["Halo! Ada yang sakit hari ini?", "Jangan lupa cuci tangan dan minum air putih ya.", "Istirahat yang cukup supaya cepat sembuh!"],
                LinesEn = ["Hello! Is anyone unwell today?", "Remember to wash your hands and drink water.", "Get plenty of rest so you get well soon!"],
            },
        ];

        foreach (Npc npc in npcs)
        {
            npc.Position = npc.Home;
        }

        return npcs;
    }
}
