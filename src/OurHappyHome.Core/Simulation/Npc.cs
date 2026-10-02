using System.Numerics;

namespace OurHappyHome.Core.Simulation;

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

    /// <summary>Present only during these hours (e.g. the teacher at school).</summary>
    public float FromHour { get; init; }

    public float ToHour { get; init; } = 24f;

    public Vector2 Position { get; set; }
    public float Yaw { get; set; }
    public bool Moving { get; private set; }
    private Vector2 _target;
    private float _timer;

    public string Name => Loc.T(NameId, NameEn);

    public string Role => Loc.T(RoleId, RoleEn);

    public string Line(int index) => Loc.T(LinesId[index % LinesId.Length], LinesEn[index % LinesEn.Length]);

    public bool Present(float hour) => hour >= FromHour && hour < ToHour;

    public void Update(float dt, GameRandom random)
    {
        _timer -= dt;
        if (_timer <= 0f)
        {
            _timer = random.Range(3f, 9f);
            _target = Home + new Vector2(random.Range(-WanderRadius, WanderRadius), random.Range(-WanderRadius, WanderRadius));
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

    public static List<Npc> CreateNeighbours()
    {
        List<Npc> npcs =
        [
            new()
            {
                Id = "grandma", NameId = "Nenek Sari", NameEn = "Grandma Sari", RoleId = "Tetangga sebelah", RoleEn = "Next-door neighbour",
                Model = "grandma", Color = "#B07AA1", Height = 1.5f, Home = new Vector2(30f, 10.5f), WanderRadius = 2.5f,
                LinesId = ["Halo cucu! Nenek baru bikin kue, mau?", "Rajin belajar ya, Nak.", "Kalau hujan besar, bawa masuk barang-barang di halaman ya."],
                LinesEn = ["Hello dear! I just baked cookies, want some?", "Study hard, little one.", "When big rain comes, bring the yard things inside."],
            },
            new()
            {
                Id = "budi", NameId = "Pak Budi", NameEn = "Mr. Budi", RoleId = "Tetangga, tukang kayu", RoleEn = "Neighbour, carpenter",
                Model = "neighbour", Color = "#5F8FBF", Height = 1.72f, Home = new Vector2(-55f, 10f), WanderRadius = 3f,
                LinesId = ["Pagar saya perlu dicat, mau bantu?", "Kalau ada yang rusak, panggil ayahmu ya!", "Badai kemarin besar sekali!"],
                LinesEn = ["My fence needs painting, want to help?", "If something breaks, call your dad!", "That storm was huge!"],
            },
            new()
            {
                Id = "dimas", NameId = "Dimas", NameEn = "Dimas", RoleId = "Teman sekelas", RoleEn = "Classmate",
                Model = "friend", Color = "#E07A5F", Height = 1.36f, Home = new Vector2(-28f, 42f), WanderRadius = 5f,
                LinesId = ["Ayo main bola di taman!", "Kamu sudah kerjakan PR matematika?", "Besok ada festival, ikut ya!"],
                LinesEn = ["Let's play football in the park!", "Did you finish the maths homework?", "There's a festival tomorrow, come along!"],
                FromHour = 14f, ToHour = 18.5f,
            },
            new()
            {
                Id = "teacher", NameId = "Bu Guru Rina", NameEn = "Ms. Rina", RoleId = "Guru kelas", RoleEn = "Class teacher",
                Model = "teacher", Color = "#81B29A", Height = 1.62f, Home = new Vector2(124f, 6f), WanderRadius = 2f,
                LinesId = ["Selamat pagi! Siap belajar hari ini?", "Bahasa Inggris adikmu bagus sekali.", "Jangan lupa membaca setiap hari."],
                LinesEn = ["Good morning! Ready to learn today?", "Your sister's English is excellent.", "Remember to read every day."],
                FromHour = 6.5f, ToHour = 15f,
            },
        ];

        foreach (Npc npc in npcs)
        {
            npc.Position = npc.Home;
        }

        return npcs;
    }
}
