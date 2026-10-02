using OurHappyHome.Core.Family;

namespace OurHappyHome.Core.School;

public enum Subject
{
    English,
    Math,
    Science,
    Art,
    Sports,
    Computer,
}

public sealed record Question(string Prompt, string[] Choices, int Answer, string Hint = "");

/// <summary>
/// Short lessons played as mini-games at school. Question generators adapt
/// to the child's skill so the lessons grow with the character.
/// </summary>
public static class Lessons
{
    public static readonly Subject[] All = Enum.GetValues<Subject>();

    public static string Name(Subject subject) => subject switch
    {
        Subject.English => Loc.T("Bahasa Inggris", "English"),
        Subject.Math => Loc.T("Matematika", "Mathematics"),
        Subject.Science => Loc.T("IPA", "Science"),
        Subject.Art => Loc.T("Seni", "Art"),
        Subject.Sports => Loc.T("Olahraga", "Sports"),
        _ => Loc.T("Komputer", "Computer"),
    };

    public static string Icon(Subject subject) => subject switch
    {
        Subject.English => "🔤",
        Subject.Math => "➗",
        Subject.Science => "🔬",
        Subject.Art => "🎨",
        Subject.Sports => "⚽",
        _ => "💻",
    };

    public static SkillKind Skill(Subject subject) => subject switch
    {
        Subject.English => SkillKind.English,
        Subject.Math => SkillKind.Math,
        Subject.Science => SkillKind.Science,
        Subject.Art => SkillKind.Art,
        Subject.Sports => SkillKind.Sports,
        _ => SkillKind.Computer,
    };

    public static string Instructions(Subject subject) => subject switch
    {
        Subject.English => Loc.T("Pilih kata bahasa Inggris yang benar!", "Pick the correct English word!"),
        Subject.Math => Loc.T("Hitung secepat mungkin!", "Solve them quickly!"),
        Subject.Science => Loc.T("Jawab pertanyaan sains!", "Answer the science questions!"),
        Subject.Art => Loc.T("Campur warna yang tepat!", "Mix the right colours!"),
        Subject.Sports => Loc.T("Tekan tombol tepat saat bola di zona hijau!", "Hit the button when the ball is in the green zone!"),
        _ => Loc.T("Ketik kata yang muncul!", "Type the word you see!"),
    };

    private static readonly (string Id, string En, string Emoji)[] Vocabulary =
    [
        ("apel", "apple", "🍎"), ("kucing", "cat", "🐱"), ("anjing", "dog", "🐶"), ("rumah", "house", "🏠"),
        ("buku", "book", "📘"), ("matahari", "sun", "☀"), ("hujan", "rain", "🌧"), ("ibu", "mother", "👩"),
        ("ayah", "father", "👨"), ("kakak perempuan", "sister", "👧"), ("sepeda", "bicycle", "🚲"), ("bintang", "star", "⭐"),
        ("bunga", "flower", "🌸"), ("pohon", "tree", "🌳"), ("ikan", "fish", "🐟"), ("burung", "bird", "🐦"),
        ("susu", "milk", "🥛"), ("telur", "egg", "🥚"), ("pintu", "door", "🚪"), ("jendela", "window", "🪟"),
        ("sekolah", "school", "🏫"), ("guru", "teacher", "🧑‍🏫"), ("bahagia", "happy", "😄"), ("keluarga", "family", "👪"),
        ("dapur", "kitchen", "🍳"), ("tempat tidur", "bed", "🛏"), ("bulan", "moon", "🌙"), ("laut", "sea", "🌊"),
    ];

    private static readonly (string Q, string[] Choices, int Answer)[] ScienceBank =
    [
        ("Tumbuhan membuat makanan dengan bantuan...|Plants make food with the help of...", ["Sinar matahari|Sunlight", "Bulan|The moon", "Angin|Wind"], 0),
        ("Air mendidih pada suhu...|Water boils at...", ["100 °C", "50 °C", "0 °C"], 0),
        ("Petir terlihat sebelum guntur karena...|We see lightning before thunder because...", ["Cahaya lebih cepat dari suara|Light is faster than sound", "Guntur malas|Thunder is lazy", "Mata lebih dekat|Eyes are closer"], 0),
        ("Hewan yang bernapas dengan insang adalah...|An animal that breathes with gills is the...", ["Ikan|Fish", "Kucing|Cat", "Burung|Bird"], 0),
        ("Planet tempat kita tinggal adalah...|The planet we live on is...", ["Bumi|Earth", "Mars|Mars", "Venus|Venus"], 0),
        ("Magnet menarik benda dari...|Magnets attract things made of...", ["Besi|Iron", "Kayu|Wood", "Plastik|Plastic"], 0),
        ("Saat listrik padam saat badai, yang aman dipakai adalah...|When the power fails in a storm, it is safest to use...", ["Senter|A flashlight", "Lilin di dekat gorden|Candles near curtains", "Kompor|The stove"], 0),
        ("Es mencair menjadi...|Ice melts into...", ["Air|Water", "Batu|Stone", "Udara|Air"], 0),
        ("Bagian tumbuhan yang menyerap air adalah...|The part of a plant that drinks water is the...", ["Akar|Root", "Daun|Leaf", "Bunga|Flower"], 0),
        ("Jika ada kebakaran kecil, kita harus...|If there is a small fire, we should...", ["Keluar dan memberi tahu orang dewasa|Get out and tell an adult", "Bersembunyi di lemari|Hide in a cupboard", "Mengambil mainan dulu|Grab toys first"], 0),
        ("Matahari terbit dari arah...|The sun rises in the...", ["Timur|East", "Barat|West", "Utara|North"], 0),
        ("Pelangi muncul saat ada matahari dan...|Rainbows appear with sunshine and...", ["Hujan|Rain", "Salju|Snow", "Debu|Dust"], 0),
    ];

    private static readonly (string A, string B, string Result, string[] Wrong)[] ColourMixes =
    [
        ("Merah|Red", "Kuning|Yellow", "Oranye|Orange", ["Hijau|Green", "Ungu|Purple"]),
        ("Biru|Blue", "Kuning|Yellow", "Hijau|Green", ["Oranye|Orange", "Merah muda|Pink"]),
        ("Merah|Red", "Biru|Blue", "Ungu|Purple", ["Cokelat|Brown", "Hijau|Green"]),
        ("Merah|Red", "Putih|White", "Merah muda|Pink", ["Ungu|Purple", "Biru|Blue"]),
        ("Hitam|Black", "Putih|White", "Abu-abu|Grey", ["Kuning|Yellow", "Merah|Red"]),
        ("Biru|Blue", "Putih|White", "Biru muda|Light blue", ["Hijau tua|Dark green", "Oranye|Orange"]),
    ];

    private static readonly string[] TypingWords =
    [
        "family", "home", "happy", "garden", "pancake", "school", "laptop", "keyboard", "mouse", "robot",
        "rainbow", "storm", "picnic", "camping", "puppy", "sister", "brother", "friend", "music", "planet",
    ];

    private static string Pick(string bilingual)
    {
        int bar = bilingual.IndexOf('|');
        return bar < 0 ? bilingual : Loc.T(bilingual[..bar], bilingual[(bar + 1)..]);
    }

    /// <summary>Builds a quiz for the subject; level 0-10 shapes difficulty.</summary>
    public static List<Question> Quiz(Subject subject, float level, GameRandom random, int count = 5)
    {
        List<Question> questions = [];
        for (int i = 0; i < count; i++)
        {
            questions.Add(subject switch
            {
                Subject.Math => MathQuestion(level, random),
                Subject.English => EnglishQuestion(random),
                Subject.Science => ScienceQuestion(random),
                Subject.Art => ArtQuestion(random),
                Subject.Computer => TypingQuestion(random),
                _ => new Question(Loc.T("Siap?", "Ready?"), ["OK"], 0),
            });
        }

        return questions;
    }

    private static Question Shuffle(string prompt, string correct, IEnumerable<string> wrong, GameRandom random, string hint = "")
    {
        List<string> choices = [correct, .. wrong.Distinct().Where(w => w != correct).Take(3)];
        for (int i = choices.Count - 1; i > 0; i--)
        {
            int j = random.Range(0, i + 1);
            (choices[i], choices[j]) = (choices[j], choices[i]);
        }

        return new Question(prompt, [.. choices], choices.IndexOf(correct), hint);
    }

    private static Question MathQuestion(float level, GameRandom random)
    {
        int max = 10 + (int)(level * 6);
        int kind = random.Range(0, level >= 3 ? 4 : 2);
        int a = random.Range(1, max);
        int b = random.Range(1, max);
        (string text, int answer) = kind switch
        {
            0 => ($"{a} + {b}", a + b),
            1 => ($"{Math.Max(a, b)} − {Math.Min(a, b)}", Math.Max(a, b) - Math.Min(a, b)),
            2 => ($"{a % 10 + 2} × {b % 10 + 1}", ((a % 10) + 2) * ((b % 10) + 1)),
            _ => ($"{(a % 9 + 2) * (b % 9 + 1)} ÷ {a % 9 + 2}", (b % 9) + 1),
        };
        int[] offsets = [1, -1, 2, -2, 10, 3];
        IEnumerable<string> wrong = offsets.OrderBy(_ => random.NextFloat()).Take(3).Select(o => Math.Max(0, answer + o).ToString());
        return Shuffle($"{text} = ?", answer.ToString(), wrong, random);
    }

    private static Question EnglishQuestion(GameRandom random)
    {
        (string id, string en, string emoji) = Vocabulary[random.Range(0, Vocabulary.Length)];
        IEnumerable<string> wrong = Vocabulary.Where(v => v.En != en).OrderBy(_ => random.NextFloat()).Take(3).Select(v => v.En);
        string prompt = Loc.T($"{emoji}  Apa bahasa Inggrisnya \"{id}\"?", $"{emoji}  Which word means \"{id}\"?");
        return Shuffle(prompt, en, wrong, random);
    }

    private static Question ScienceQuestion(GameRandom random)
    {
        (string q, string[] choices, int answer) = ScienceBank[random.Range(0, ScienceBank.Length)];
        return Shuffle(Pick(q), Pick(choices[answer]), choices.Where((_, i) => i != answer).Select(Pick), random);
    }

    private static Question ArtQuestion(GameRandom random)
    {
        (string a, string b, string result, string[] wrong) = ColourMixes[random.Range(0, ColourMixes.Length)];
        return Shuffle($"🎨 {Pick(a)} + {Pick(b)} = ?", Pick(result), wrong.Select(Pick), random);
    }

    private static Question TypingQuestion(GameRandom random)
    {
        string word = TypingWords[random.Range(0, TypingWords.Length)];
        return new Question(word, [word], 0, Loc.T("Ketik katanya lalu tekan Enter", "Type the word and press Enter"));
    }

    public static string Grade(float score) => score switch
    {
        >= 90f => "A",
        >= 75f => "B",
        >= 60f => "C",
        >= 45f => "D",
        _ => "E",
    };
}

/// <summary>Grades and attendance of the children.</summary>
public sealed class SchoolRecord
{
    public Dictionary<MemberId, Dictionary<Subject, List<float>>> Scores { get; set; } = [];

    public Dictionary<MemberId, int> DaysAttended { get; set; } = [];

    public int PlayerLessonsToday { get; set; }

    public int LessonDay { get; set; } = -1;

    public void Record(MemberId child, Subject subject, float score)
    {
        if (!Scores.TryGetValue(child, out Dictionary<Subject, List<float>>? bySubject))
        {
            bySubject = [];
            Scores[child] = bySubject;
        }

        if (!bySubject.TryGetValue(subject, out List<float>? list))
        {
            list = [];
            bySubject[subject] = list;
        }

        list.Add(score);
        if (list.Count > 12)
        {
            list.RemoveAt(0);
        }
    }

    public float Average(MemberId child, Subject subject) =>
        Scores.TryGetValue(child, out Dictionary<Subject, List<float>>? s) && s.TryGetValue(subject, out List<float>? list) && list.Count > 0
            ? list.Average()
            : 0f;

    public int Attended(MemberId child) => DaysAttended.TryGetValue(child, out int n) ? n : 0;

    public void Attend(MemberId child) => DaysAttended[child] = Attended(child) + 1;
}
