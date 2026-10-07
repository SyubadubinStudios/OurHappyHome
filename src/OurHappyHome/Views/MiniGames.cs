using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OurHappyHome.Audio;
using OurHappyHome.Core;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.School;
using OurHappyHome.Core.Simulation;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

public interface IMiniGame
{
    bool KeyDown(KeyEventArgs e);

    void KeyUp(KeyEventArgs e);
}

public sealed partial class GameScreen
{
    private void OpenMiniGame(MiniGameRequest request)
    {
        Control game;
        switch (request.Kind)
        {
            case "cook":
                {
                    Recipe recipe = Recipes.Get(request.Argument);
                    CookingGame cooking = new(recipe, Session.Controlled.Skills[SkillKind.Cooking], Audio, score => FinishMiniGame(() => Session.CompleteMiniGame(score)));
                    game = cooking;
                    _miniGame = cooking;
                    break;
                }

            case "school":
                {
                    SchoolGame school = new(Session.Controlled, Session.Date.Weekday, Audio, Session.Random, scores => FinishMiniGame(() => Session.CompleteSchool(scores)));
                    game = school;
                    _miniGame = school;
                    break;
                }

            case "kerupuk":
                {
                    KerupukGame kerupuk = new(Session.Controlled.Name, Audio, score => FinishMiniGame(() => Session.CompleteMiniGame(score)));
                    game = kerupuk;
                    _miniGame = kerupuk;
                    break;
                }

            case "tug":
                {
                    int team = int.TryParse(request.Argument, out int n) ? n : 1;
                    TugOfWarGame tug = new(team, Audio, score => FinishMiniGame(() => Session.CompleteMiniGame(score)));
                    game = tug;
                    _miniGame = tug;
                    break;
                }

            case "lemonade":
                {
                    LemonadeGame lemonade = new(Audio, score => FinishMiniGame(() => Session.CompleteMiniGame(score)));
                    game = lemonade;
                    _miniGame = lemonade;
                    break;
                }

            default:
                {
                    ArcadeGame arcade = new(Audio, score => FinishMiniGame(() => Session.CompleteMiniGame(score)));
                    game = arcade;
                    _miniGame = arcade;
                    break;
                }
        }

        ShowModal(game);
    }

    /// <summary>Screenshot helpers.</summary>
    public void CloseMiniGameForCapture()
    {
        _miniGame = null;
        _panels.Children.Clear();
    }

    public void StartSchoolForCapture() => (_miniGame as SchoolGame)?.Begin();

    public void ExitBuildForCapture() => ExitBuildMode();

    private void FinishMiniGame(Action report)
    {
        _miniGame = null;
        _panels.Children.Clear();
        report();
        _view.Focus();
    }
}

/// <summary>Shared frame for mini-games: a big card, a timer and Space/click input.</summary>
public abstract class MiniGameBase : UserControl, IMiniGame
{
    private readonly DispatcherTimer _timer;
    private readonly DateTime _start = DateTime.Now;
    private double _last;

    protected MiniGameBase(AudioManager audio)
    {
        Audio = audio;
        Body = new StackPanel { Spacing = 14 };
        Border card = Ui.Card(Body, 26, "#FFF8EC", 26);
        card.Width = 640;
        card.MinHeight = 380;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        Content = card;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            double now = (DateTime.Now - _start).TotalSeconds;
            float dt = (float)Math.Min(0.05, now - _last);
            _last = now;
            Tick(dt);
        });
        _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    protected AudioManager Audio { get; }

    protected StackPanel Body { get; }

    protected bool SpaceDown { get; private set; }

    protected abstract void Tick(float dt);

    protected virtual void Press()
    {
    }

    protected virtual void Release()
    {
    }

    protected void Stop() => _timer.Stop();

    public virtual bool KeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter or Key.E)
        {
            if (!SpaceDown)
            {
                SpaceDown = true;
                Press();
            }

            return true;
        }

        return e.Key != Key.Escape;
    }

    public virtual void KeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter or Key.E && SpaceDown)
        {
            SpaceDown = false;
            Release();
        }
    }

    protected Button BigButton(string text, string icon)
    {
        Button b = Ui.Button(text, () => { }, icon: icon, size: 18);
        b.HorizontalAlignment = HorizontalAlignment.Center;
        b.Padding = new Thickness(30, 14);
        b.AddHandler(PointerPressedEvent, (_, _) =>
        {
            SpaceDown = true;
            Press();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        b.AddHandler(PointerReleasedEvent, (_, _) =>
        {
            if (SpaceDown)
            {
                SpaceDown = false;
                Release();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        return b;
    }

    /// <summary>A horizontal gauge with a target zone and a marker.</summary>
    protected sealed class Gauge : Canvas
    {
        private readonly Border _zone;
        private readonly Border _marker;
        private readonly Border _fill;
        private const double W = 520;

        public Gauge()
        {
            Width = W;
            Height = 34;
            HorizontalAlignment = HorizontalAlignment.Center;
            Children.Add(new Border { Width = W, Height = 34, CornerRadius = new CornerRadius(17), Background = Ui.B("#EFE3D3") });
            _fill = new Border { Width = 0, Height = 34, CornerRadius = new CornerRadius(17), Background = Ui.B("#FFC98B") };
            Children.Add(_fill);
            _zone = new Border { Height = 34, Background = Ui.B("#8854C26A"), CornerRadius = new CornerRadius(6) };
            Children.Add(_zone);
            _marker = new Border { Width = 8, Height = 44, CornerRadius = new CornerRadius(4), Background = Ui.B("#3B2F2A") };
            Children.Add(_marker);
            SetTop(_marker, -5);
        }

        public void Zone(float from, float to)
        {
            _zone.Width = Math.Max(4, (to - from) * W);
            SetLeft(_zone, from * W);
            _zone.IsVisible = to > from;
        }

        public void Marker(float at, bool visible = true)
        {
            SetLeft(_marker, (Math.Clamp(at, 0f, 1f) * W) - 4);
            _marker.IsVisible = visible;
        }

        public void Fill(float amount) => _fill.Width = Math.Clamp(amount, 0f, 1f) * W;
    }
}

/// <summary>Cooking: each recipe step is a timing, mashing or hold-and-release challenge.</summary>
public sealed class CookingGame : MiniGameBase
{
    private readonly Recipe _recipe;
    private readonly Action<float> _done;
    private readonly float _skill;
    private readonly List<float> _scores = [];
    private readonly TextBlock _title;
    private readonly TextBlock _stepText;
    private readonly TextBlock _hint;
    private readonly TextBlock _feedback;
    private readonly Gauge _gauge = new();
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
    private int _step = -1;
    private float _t;
    private float _value;
    private float _zoneFrom;
    private float _zoneTo;
    private int _presses;
    private bool _waiting;
    private float _wait;

    public CookingGame(Recipe recipe, float skill, AudioManager audio, Action<float> done) : base(audio)
    {
        _recipe = recipe;
        _skill = skill;
        _done = done;
        _title = Ui.Title($"{recipe.Icon} {recipe.Name}", 26);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _stepText = Ui.Title("", 20);
        _stepText.HorizontalAlignment = HorizontalAlignment.Center;
        _hint = Ui.Text("", 14, Ui.Muted, wrap: true);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _feedback = Ui.Text("", 18, Ui.Ink, FontWeight.Bold);
        _feedback.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(_title);
        Body.Children.Add(_dots);
        Body.Children.Add(_stepText);
        Body.Children.Add(_hint);
        Body.Children.Add(_gauge);
        Body.Children.Add(_feedback);
        Body.Children.Add(BigButton(Loc.T("Tekan / tahan (Spasi)", "Press / hold (Space)"), "👆"));
        NextStep();
    }

    private CookStep Step => _recipe.Steps[_step];

    private void NextStep()
    {
        _step++;
        _dots.Children.Clear();
        for (int i = 0; i < _recipe.Steps.Length; i++)
        {
            _dots.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Ui.B(i < _step ? "#4CAF50" : i == _step ? "#F28C38" : "#EFE3D3") });
        }

        if (_step >= _recipe.Steps.Length)
        {
            float score = _scores.Count == 0 ? 0.5f : _scores.Average();
            Stop();
            _done(score);
            return;
        }

        _t = 0f;
        _value = 0f;
        _presses = 0;
        // Wider zones for skilled cooks and easy steps.
        float width = Math.Clamp(0.22f - (Step.Difficulty * 0.12f) + (_skill * 0.012f), 0.08f, 0.3f);
        _zoneFrom = 0.25f + ((float)Random.Shared.NextDouble() * (0.6f - width));
        _zoneTo = _zoneFrom + width;
        _stepText.Text = $"{Step.Icon} {Step.Text}";
        _hint.Text = Step.Kind switch
        {
            StepKind.Timing => Loc.T("Tekan saat penanda masuk zona hijau!", "Press when the marker is in the green zone!"),
            StepKind.Mash => Loc.T("Tekan berkali-kali dengan cepat untuk mengisi bar!", "Tap quickly to fill the bar!"),
            _ => Loc.T("Tahan, lalu lepas saat bar di zona hijau!", "Hold, then release in the green zone!"),
        };
        _gauge.Zone(Step.Kind == StepKind.Mash ? 0.9f : _zoneFrom, Step.Kind == StepKind.Mash ? 1f : _zoneTo);
        _gauge.Fill(0f);
        _waiting = false;
    }

    protected override void Tick(float dt)
    {
        if (_step >= _recipe.Steps.Length)
        {
            return;
        }

        if (_waiting)
        {
            _wait -= dt;
            if (_wait <= 0f)
            {
                NextStep();
            }

            return;
        }

        _t += dt;
        switch (Step.Kind)
        {
            case StepKind.Timing:
                _value = (MathF.Sin(_t * (2.2f + (Step.Difficulty * 2f))) * 0.5f) + 0.5f;
                _gauge.Marker(_value);
                if (_t > 8f)
                {
                    Score(0.1f);
                }

                break;
            case StepKind.Mash:
                _value = MathF.Max(0f, _value - (dt * 0.12f));
                _gauge.Fill(_value);
                _gauge.Marker(_value, false);
                if (_value >= 0.9f)
                {
                    Score(Math.Clamp(1.4f - (_t / 4f), 0.4f, 1f));
                }
                else if (_t > 4.5f)
                {
                    Score(_value * 0.8f);
                }

                break;
            case StepKind.Hold:
                if (SpaceDown)
                {
                    _value = MathF.Min(1f, _value + (dt * (0.35f + (Step.Difficulty * 0.3f))));
                }

                _gauge.Fill(_value);
                _gauge.Marker(_value);
                if (_value >= 1f)
                {
                    Score(0.05f, Loc.T("Terlalu lama! Gosong 💥", "Too long! Burnt 💥"));
                }

                break;
        }
    }

    protected override void Press()
    {
        if (_waiting || _step >= _recipe.Steps.Length)
        {
            return;
        }

        switch (Step.Kind)
        {
            case StepKind.Timing:
                float centre = (_zoneFrom + _zoneTo) / 2f;
                float distance = MathF.Abs(_value - centre) / ((_zoneTo - _zoneFrom) / 2f);
                Score(distance <= 1f ? 1f - (distance * 0.35f) : MathF.Max(0.1f, 0.6f - ((distance - 1f) * 0.3f)));
                break;
            case StepKind.Mash:
                _presses++;
                _value += 0.075f + (_skill * 0.004f);
                Audio.Play("pop", gain: 0.3f, pitch: 0.8f + (_value * 0.6f));
                break;
        }
    }

    protected override void Release()
    {
        if (_waiting || _step >= _recipe.Steps.Length || Step.Kind != StepKind.Hold || _value <= 0.02f)
        {
            return;
        }

        bool inZone = _value >= _zoneFrom && _value <= _zoneTo;
        float centre = (_zoneFrom + _zoneTo) / 2f;
        Score(inZone ? 1f - (MathF.Abs(_value - centre) * 2f) : MathF.Max(0.1f, 0.6f - (MathF.Abs(_value - centre) * 2f)));
    }

    private void Score(float score, string? message = null)
    {
        _scores.Add(Math.Clamp(score, 0f, 1f));
        _feedback.Text = message ?? (score >= 0.85f ? Loc.T("Sempurna! ⭐", "Perfect! ⭐") : score >= 0.6f ? Loc.T("Bagus! 👍", "Nice! 👍") : score >= 0.35f ? Loc.T("Lumayan 🙂", "Okay 🙂") : Loc.T("Ups! 😅", "Oops! 😅"));
        _feedback.Foreground = Ui.B(score >= 0.6f ? Ui.GoodColor : score >= 0.35f ? Ui.WarnColor : Ui.BadColor);
        Audio.Play(score >= 0.6f ? "correct" : "wrong", gain: 0.5f);
        _waiting = true;
        _wait = 0.9f;
    }
}

/// <summary>A school morning: three lessons chosen by weekday, each a short mini-game.</summary>
public sealed class SchoolGame : MiniGameBase
{
    private readonly FamilyMember _child;
    private readonly Action<IReadOnlyDictionary<Subject, float>> _done;
    private readonly GameRandom _random;
    private readonly List<Subject> _subjects;
    private readonly Dictionary<Subject, float> _scores = [];
    private int _lesson = -1;
    private List<Question> _questions = [];
    private int _question;
    private int _correct;
    private float _questionTime;
    private float _sportsT;
    private int _sportsHits;
    private int _sportsTries;
    private readonly Gauge _gauge = new();
    private bool _between;

    public SchoolGame(FamilyMember child, Core.Time.Weekday weekday, AudioManager audio, GameRandom random, Action<IReadOnlyDictionary<Subject, float>> done) : base(audio)
    {
        _child = child;
        _done = done;
        _random = random;
        Subject[][] timetable =
        [
            [Subject.English, Subject.Math, Subject.Art],
            [Subject.Science, Subject.Sports, Subject.English],
            [Subject.Math, Subject.Computer, Subject.Science],
            [Subject.English, Subject.Art, Subject.Sports],
            [Subject.Math, Subject.Science, Subject.Computer],
        ];
        _subjects = [.. timetable[Math.Min(4, (int)weekday)]];
        ShowIntro();
    }

    private Subject Current => _subjects[_lesson];

    public void Begin() => NextLesson();

    private void ShowIntro()
    {
        Body.Children.Clear();
        TextBlock title = Ui.Title(Loc.T("🏫 Selamat pagi, kelas!", "🏫 Good morning, class!"), 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(title);
        Body.Children.Add(Ui.Text(Loc.T($"Bu Guru Rina: \"Halo {_child.Name}! Hari ini kita belajar:\"", $"Ms. Rina: \"Hello {_child.Name}! Today we'll learn:\""), 15, Ui.Ink, wrap: true));
        foreach (Subject s in _subjects)
        {
            Body.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(Lessons.Icon(s), 22), Ui.Text(Lessons.Name(s), 17, Ui.Ink, FontWeight.SemiBold)));
        }

        Button start = Ui.Button(Loc.T("Mulai pelajaran", "Start lessons"), NextLesson, icon: "✏", size: 17);
        start.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(start);
    }

    private void NextLesson()
    {
        _lesson++;
        if (_lesson >= _subjects.Count)
        {
            ShowReport();
            return;
        }

        _between = false;
        _question = 0;
        _correct = 0;
        _sportsHits = 0;
        _sportsTries = 0;
        _questions = Lessons.Quiz(Current, _child.Skills[Lessons.Skill(Current)], _random, Current == Subject.Computer ? 4 : 5);
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        Body.Children.Clear();
        TextBlock header = Ui.Title($"{Lessons.Icon(Current)} {Lessons.Name(Current)}", 22);
        header.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(header);
        Body.Children.Add(Ui.Text(Lessons.Instructions(Current), 14, Ui.Muted));
        _questionTime = 0f;

        if (Current == Subject.Sports)
        {
            Body.Children.Add(Ui.Text(Loc.T($"Tendangan {_sportsTries + 1} dari 5 ⚽", $"Kick {_sportsTries + 1} of 5 ⚽"), 18, Ui.Ink, FontWeight.Bold));
            _gauge.Zone(0.42f, 0.58f);
            Body.Children.Add(_gauge);
            Body.Children.Add(BigButton(Loc.T("Tendang! (Spasi)", "Kick! (Space)"), "⚽"));
            _sportsT = 0f;
            return;
        }

        Question q = _questions[_question];
        Body.Children.Add(Ui.Text(Loc.T($"Soal {_question + 1} dari {_questions.Count}", $"Question {_question + 1} of {_questions.Count}"), 13, Ui.Muted));
        TextBlock prompt = Ui.Title(q.Prompt, Current == Subject.Computer ? 34 : 22);
        prompt.TextWrapping = TextWrapping.Wrap;
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(prompt);

        if (Current == Subject.Computer)
        {
            TextBox box = new() { FontSize = Ui.Fs(22), Width = 320, HorizontalAlignment = HorizontalAlignment.Center, PlaceholderText = q.Hint };
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Answer(string.Equals(box.Text?.Trim(), q.Choices[0], StringComparison.OrdinalIgnoreCase));
                    e.Handled = true;
                }
            };
            Body.Children.Add(box);
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Background);
            return;
        }

        WrapPanel choices = new() { HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < q.Choices.Length; i++)
        {
            int index = i;
            Button b = Ui.Button(q.Choices[i], () => Answer(index == q.Answer), "#FFE3C2", "#3B2F2A", 17);
            b.Margin = new Thickness(6);
            b.MinWidth = 160;
            choices.Children.Add(b);
        }

        Body.Children.Add(choices);
    }

    private void Answer(bool right)
    {
        if (_between)
        {
            return;
        }

        if (right)
        {
            _correct++;
        }

        Audio.Play(right ? "correct" : "wrong", gain: 0.6f);
        _question++;
        if (_question >= _questions.Count)
        {
            FinishLesson((float)_correct / _questions.Count);
        }
        else
        {
            ShowQuestion();
        }
    }

    private void FinishLesson(float fraction)
    {
        float score = MathF.Round(fraction * 100f);
        _scores[Current] = score;
        _between = true;
        Body.Children.Clear();
        TextBlock grade = Ui.Title($"{Lessons.Name(Current)}: {Lessons.Grade(score)} ({score:0})", 26);
        grade.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(Ui.Emoji(score >= 75 ? "🌟" : score >= 50 ? "🙂" : "📚", 48));
        Body.Children.Add(grade);
        Body.Children.Add(Ui.Text(score >= 75 ? Loc.T("Hebat! Bu Guru bangga.", "Great! Your teacher is proud.") : Loc.T("Terus berlatih, pasti bisa!", "Keep practising, you'll get there!"), 15, Ui.Muted));
        Button next = Ui.Button(_lesson + 1 < _subjects.Count ? Loc.T("Pelajaran berikutnya", "Next lesson") : Loc.T("Lihat rapor hari ini", "See today's report"), NextLesson, icon: "➡", size: 16);
        next.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(next);
    }

    private void ShowReport()
    {
        Body.Children.Clear();
        TextBlock title = Ui.Title(Loc.T("📝 Rapor hari ini", "📝 Today's report"), 24);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(title);
        foreach ((Subject s, float score) in _scores)
        {
            Body.Children.Add(Ui.Row((Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(Lessons.Icon(s), 18), Ui.Text(Lessons.Name(s), 16)), new GridLength(240)), (Ui.Bar(score, width: 200, height: 12), GridLength.Auto), (Ui.Text($"  {Lessons.Grade(score)}", 18, Ui.Ink, FontWeight.Bold), GridLength.Star)));
        }

        Button done = Ui.Button(Loc.T("Pulang sekolah", "Head home"), () =>
        {
            Stop();
            _done(_scores);
        }, icon: "🎒", size: 17);
        done.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(done);
    }

    protected override void Tick(float dt)
    {
        if (_lesson < 0 || _lesson >= _subjects.Count || _between)
        {
            return;
        }

        if (Current == Subject.Sports)
        {
            _sportsT += dt;
            _gauge.Marker((MathF.Sin(_sportsT * (3f + (_sportsTries * 0.5f))) * 0.5f) + 0.5f);
        }
    }

    protected override void Press()
    {
        if (_lesson < 0 || _lesson >= _subjects.Count || _between || Current != Subject.Sports)
        {
            return;
        }

        float position = (MathF.Sin(_sportsT * (3f + (_sportsTries * 0.5f))) * 0.5f) + 0.5f;
        bool goal = position is >= 0.42f and <= 0.58f;
        _sportsTries++;
        if (goal)
        {
            _sportsHits++;
        }

        Audio.Play(goal ? "cheer" : "wrong", gain: 0.6f);
        if (_sportsTries >= 5)
        {
            FinishLesson(_sportsHits / 5f);
        }
        else
        {
            ShowQuestion();
        }
    }

    public override bool KeyDown(KeyEventArgs e)
    {
        if (e.Source is TextBox)
        {
            return false;
        }

        return base.KeyDown(e);
    }
}

/// <summary>Lemonade stand: serve each customer the drink they ask for.</summary>
public sealed class LemonadeGame : MiniGameBase
{
    private readonly Action<float> _done;
    private readonly TextBlock _customer;
    private readonly TextBlock _order;
    private readonly TextBlock _cup;
    private readonly TextBlock _status;
    private readonly Random _random = new();
    private int _wantLemon;
    private int _wantIce;
    private int _lemon;
    private int _ice;
    private int _served;
    private int _happy;
    private float _timeLeft = 40f;
    private const int Customers = 6;

    public LemonadeGame(AudioManager audio, Action<float> done) : base(audio)
    {
        _done = done;
        TextBlock title = Ui.Title(Loc.T("🍋 Kios Es Limun", "🍋 Lemonade Stand"), 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _customer = Ui.Emoji("🙂", 64);
        _order = Ui.Title("", 22);
        _order.HorizontalAlignment = HorizontalAlignment.Center;
        _cup = Ui.Text("", 20);
        _cup.HorizontalAlignment = HorizontalAlignment.Center;
        _status = Ui.Text("", 14, Ui.Muted);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(title);
        Body.Children.Add(_customer);
        Body.Children.Add(_order);
        Body.Children.Add(_cup);
        StackPanel buttons = Ui.Stack(10, Orientation.Horizontal,
            Ui.Button(Loc.T("+ Lemon", "+ Lemon"), () => { _lemon++; Refresh(); }, "#FFE66D", "#3B2F2A", 16, "🍋"),
            Ui.Button(Loc.T("+ Es", "+ Ice"), () => { _ice++; Refresh(); }, "#BDE7FF", "#3B2F2A", 16, "🧊"),
            Ui.Button(Loc.T("Ulang", "Reset"), () => { _lemon = 0; _ice = 0; Refresh(); }, "#FFF1DE", "#5A4636", 14, "↺"),
            Ui.Button(Loc.T("Sajikan!", "Serve!"), Serve, icon: "🥤", size: 16));
        buttons.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(buttons);
        Body.Children.Add(_status);
        NewCustomer();
    }

    private void NewCustomer()
    {
        string[] faces = ["🙂", "😃", "👵", "👦", "👧", "🧑", "👨", "👩"];
        _customer.Text = faces[_random.Next(faces.Length)];
        _wantLemon = _random.Next(1, 4);
        _wantIce = _random.Next(0, 3);
        _lemon = 0;
        _ice = 0;
        _order.Text = Loc.T($"\"Satu gelas ya: {_wantLemon}× lemon, {_wantIce}× es!\"", $"\"One cup please: {_wantLemon}× lemon, {_wantIce}× ice!\"");
        Refresh();
    }

    private void Refresh()
    {
        _cup.Text = Loc.T("Gelasmu: ", "Your cup: ") + string.Concat(Enumerable.Repeat("🍋", _lemon)) + string.Concat(Enumerable.Repeat("🧊", _ice)) + (_lemon + _ice == 0 ? "🥛" : "");
        _status.Text = Loc.T($"Pelanggan {_served + 1}/{Customers} · Senang {_happy} · Waktu {_timeLeft:0} dtk", $"Customer {_served + 1}/{Customers} · Happy {_happy} · Time {_timeLeft:0}s");
    }

    private void Serve()
    {
        bool right = _lemon == _wantLemon && _ice == _wantIce;
        if (right)
        {
            _happy++;
            Audio.Play("coins", gain: 0.7f);
        }
        else
        {
            Audio.Play("wrong", gain: 0.5f);
        }

        _served++;
        if (_served >= Customers)
        {
            Finish();
        }
        else
        {
            NewCustomer();
        }
    }

    private void Finish()
    {
        Stop();
        _done((float)_happy / Customers);
    }

    protected override void Tick(float dt)
    {
        _timeLeft -= dt;
        if (_timeLeft <= 0f)
        {
            Finish();
            return;
        }

        if ((int)(_timeLeft * 10) % 5 == 0)
        {
            Refresh();
        }
    }
}

/// <summary>Arcade: catch as many falling stars as you can in 20 seconds.</summary>
public sealed class ArcadeGame : MiniGameBase
{
    private readonly Action<float> _done;
    private readonly Canvas _field = new() { Width = 560, Height = 300, Background = Ui.B("#1B1F3B"), ClipToBounds = true };
    private readonly List<(TextBlock Star, double X, double Y, double Speed)> _stars = [];
    private readonly TextBlock _score;
    private readonly Random _random = new();
    private float _time = 20f;
    private float _spawn;
    private int _caught;

    public ArcadeGame(AudioManager audio, Action<float> done) : base(audio)
    {
        _done = done;
        TextBlock title = Ui.Title(Loc.T("🕹 Tangkap Bintang!", "🕹 Catch the Stars!"), 24);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _score = Ui.Text("", 15, Ui.Ink, FontWeight.Bold);
        _score.HorizontalAlignment = HorizontalAlignment.Center;
        Border frame = new() { CornerRadius = new CornerRadius(14), ClipToBounds = true, Child = _field, HorizontalAlignment = HorizontalAlignment.Center };
        Body.Children.Add(title);
        Body.Children.Add(Ui.Text(Loc.T("Klik bintang yang jatuh sebelum menyentuh tanah!", "Click the falling stars before they land!"), 14, Ui.Muted));
        Body.Children.Add(frame);
        Body.Children.Add(_score);
    }

    protected override void Tick(float dt)
    {
        _time -= dt;
        if (_time <= 0f)
        {
            Stop();
            _done(Math.Clamp(_caught / 18f, 0f, 1f));
            return;
        }

        _spawn -= dt;
        if (_spawn <= 0f)
        {
            _spawn = 0.45f;
            TextBlock star = Ui.Emoji(_random.Next(5) == 0 ? "🌟" : "⭐", 30);
            star.Cursor = new Cursor(StandardCursorType.Hand);
            double x = _random.Next(10, 510);
            star.PointerPressed += (_, _) =>
            {
                _caught++;
                Audio.Play("coins", gain: 0.4f, pitch: 1.2f);
                _field.Children.Remove(star);
                _stars.RemoveAll(s => s.Star == star);
            };
            _field.Children.Add(star);
            _stars.Add((star, x, -30, 90 + _random.Next(0, 80)));
        }

        for (int i = _stars.Count - 1; i >= 0; i--)
        {
            (TextBlock star, double x, double y, double speed) = _stars[i];
            y += speed * dt;
            if (y > 300)
            {
                _field.Children.Remove(star);
                _stars.RemoveAt(i);
                continue;
            }

            _stars[i] = (star, x, y, speed);
            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
        }

        _score.Text = Loc.T($"Bintang: {_caught} · Waktu: {_time:0} dtk", $"Stars: {_caught} · Time: {_time:0}s");
    }
}

/// <summary>Cracker eating contest: hands behind your back, mash Space to munch faster than the others.</summary>
public sealed class KerupukGame : MiniGameBase
{
    private readonly Action<float> _done;
    private readonly string[] _names;
    private readonly float[] _progress = new float[4];
    private readonly float[] _speed = new float[4];
    private readonly TextBlock[] _crackers = new TextBlock[4];
    private readonly Border[] _bars = new Border[4];
    private readonly TextBlock _status;
    private readonly Random _random = new();
    private float _time = 20f;
    private float _countdown = 3f;
    private bool _finished;

    public KerupukGame(string player, AudioManager audio, Action<float> done) : base(audio)
    {
        _done = done;
        _names = [player, "Dimas", "Putri", "Bayu"];
        for (int i = 1; i < 4; i++)
        {
            _speed[i] = 0.045f + ((float)_random.NextDouble() * 0.03f);
        }

        TextBlock title = Ui.Title(Loc.T("🍘 Lomba Makan Kerupuk", "🍘 Cracker Eating Contest"), 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(title);
        Body.Children.Add(Ui.Text(Loc.T("Tangan di belakang! Tekan Spasi (atau tombol) secepatnya untuk menggigit kerupuk.", "Hands behind your back! Press Space (or the button) as fast as you can to munch."), 14, Ui.Muted, wrap: true));
        for (int i = 0; i < 4; i++)
        {
            _crackers[i] = Ui.Emoji("🍘", 34);
            _bars[i] = new Border { Height = 14, Width = 0, CornerRadius = new CornerRadius(7), Background = Ui.B(i == 0 ? "#F28C38" : "#B9A89A"), HorizontalAlignment = HorizontalAlignment.Left };
            Border track = new() { Width = 360, Height = 14, CornerRadius = new CornerRadius(7), Background = Ui.B("#EFE3D3"), Child = _bars[i] };
            Grid row = Ui.Row((Ui.Text(_names[i], 15, Ui.Ink, i == 0 ? FontWeight.Bold : FontWeight.Normal), new GridLength(110)), (_crackers[i], new GridLength(60)), (track, GridLength.Auto));
            row.VerticalAlignment = VerticalAlignment.Center;
            Body.Children.Add(row);
        }

        _status = Ui.Title("", 18);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(_status);
        Body.Children.Add(BigButton(Loc.T("Gigit!", "Munch!"), "😋"));
    }

    protected override void Press()
    {
        if (_countdown > 0f || _finished)
        {
            return;
        }

        _progress[0] = MathF.Min(1f, _progress[0] + 0.03f);
        Audio.Play("pop", gain: 0.35f, pitch: 0.9f + ((float)_random.NextDouble() * 0.3f));
    }

    protected override void Tick(float dt)
    {
        if (_finished)
        {
            return;
        }

        if (_countdown > 0f)
        {
            _countdown -= dt;
            _status.Text = _countdown > 0f ? $"{MathF.Ceiling(_countdown):0}..." : Loc.T("Mulai!", "Go!");
            return;
        }

        _time -= dt;
        for (int i = 1; i < 4; i++)
        {
            // The others munch in little bursts.
            _progress[i] = MathF.Min(1f, _progress[i] + (_speed[i] * dt * (0.5f + (float)_random.NextDouble())));
        }

        for (int i = 0; i < 4; i++)
        {
            _bars[i].Width = 360 * _progress[i];
            _crackers[i].RenderTransform = new ScaleTransform(1 - (_progress[i] * 0.85), 1 - (_progress[i] * 0.85));
        }

        _status.Text = Loc.T($"Waktu: {_time:0} dtk", $"Time: {_time:0}s");
        if (_progress.Any(p => p >= 1f) || _time <= 0f)
        {
            _finished = true;
            Stop();
            int rank = 1 + _progress.Skip(1).Count(p => p > _progress[0]);
            _status.Text = rank == 1 ? Loc.T("🏆 Juara 1!", "🏆 First place!") : Loc.T($"Juara {rank}", $"Place {rank}");
            Audio.Play(rank == 1 ? "fanfare" : "correct", gain: 0.6f);
            DispatcherTimer.RunOnce(() => _done(rank switch { 1 => 1f, 2 => 0.6f, 3 => 0.3f, _ => 0.1f }), TimeSpan.FromSeconds(1.6));
        }
    }
}

/// <summary>Tug of war: mash to pull the ribbon to your side; every family member on the team adds strength.</summary>
public sealed class TugOfWarGame : MiniGameBase
{
    private readonly Action<float> _done;
    private readonly int _team;
    private readonly Gauge _gauge = new();
    private readonly TextBlock _status;
    private readonly Random _random = new();
    private float _rope = 0.5f;
    private float _time = 25f;
    private float _countdown = 3f;
    private float _burst;
    private bool _finished;

    public TugOfWarGame(int team, AudioManager audio, Action<float> done) : base(audio)
    {
        _done = done;
        _team = Math.Clamp(team, 1, 5);
        TextBlock title = Ui.Title(Loc.T("🪢 Tarik Tambang", "🪢 Tug of War"), 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(title);
        Body.Children.Add(Ui.Text(Loc.T($"Timmu: {_team} orang. Tekan Spasi berulang-ulang untuk menarik pita ke kiri!", $"Your team: {_team}. Mash Space to pull the ribbon to the left!"), 14, Ui.Muted, wrap: true));
        Grid teams = Ui.Row((Ui.Text(string.Concat(Enumerable.Repeat("💪", _team)) + Loc.T(" Keluarga", " Family"), 16, Ui.Ink, FontWeight.Bold), GridLength.Star),
            (Ui.Text(Loc.T("Tim Tetangga ", "Neighbours ") + "💪💪💪", 16, Ui.Ink, FontWeight.Bold), GridLength.Auto));
        Body.Children.Add(teams);
        _gauge.Zone(0f, 0.2f);
        Body.Children.Add(_gauge);
        _status = Ui.Title("", 18);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Body.Children.Add(_status);
        Body.Children.Add(BigButton(Loc.T("Tarik!", "Pull!"), "🪢"));
    }

    protected override void Press()
    {
        if (_countdown > 0f || _finished)
        {
            return;
        }

        _rope -= 0.012f + (0.004f * _team);
        Audio.Play("rope", gain: 0.25f);
    }

    protected override void Tick(float dt)
    {
        if (_finished)
        {
            return;
        }

        if (_countdown > 0f)
        {
            _countdown -= dt;
            _status.Text = _countdown > 0f ? $"{MathF.Ceiling(_countdown):0}..." : Loc.T("Tarik!", "Pull!");
            _gauge.Marker(_rope);
            return;
        }

        _time -= dt;
        _burst -= dt;
        float pull = 0.075f + (_burst > 0f ? 0.09f : 0f);
        if (_burst < -1.5f && _random.NextDouble() < dt)
        {
            _burst = 0.8f; // the neighbours heave together
        }

        _rope = Math.Clamp(_rope + (pull * dt), 0f, 1f);
        _gauge.Marker(_rope);
        _gauge.Fill(1f - _rope);
        _status.Text = Loc.T($"Waktu: {_time:0} dtk", $"Time: {_time:0}s");
        if (_rope <= 0.2f || _rope >= 0.8f || _time <= 0f)
        {
            _finished = true;
            Stop();
            bool won = _rope < 0.5f;
            _status.Text = won ? Loc.T("🏆 Tim keluarga menang!", "🏆 The family team wins!") : Loc.T("Tim tetangga menang. Seru sekali!", "The neighbours win. What fun!");
            Audio.Play(won ? "fanfare" : "correct", gain: 0.6f);
            float score = won ? 0.6f + (0.4f * (0.5f - _rope) / 0.3f) : 0.4f * (1f - _rope);
            DispatcherTimer.RunOnce(() => _done(Math.Clamp(score, 0f, 1f)), TimeSpan.FromSeconds(1.6));
        }
    }
}
