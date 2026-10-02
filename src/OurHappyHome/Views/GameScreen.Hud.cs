using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.UI;

namespace OurHappyHome.Views;

public sealed partial class GameScreen
{
    private sealed class MemberRow
    {
        public MemberId Id;
        public Border Card = null!;
        public TextBlock Mood = null!;
        public TextBlock Status = null!;
        public Meter Lowest = null!;
        public TextBlock LowestIcon = null!;
    }

    // Character card
    private Panel _portraitHost = null!;
    private TextBlock _name = null!;
    private TextBlock _role = null!;
    private TextBlock _moodText = null!;
    private Meter _stamina = null!;
    private TextBlock _staminaText = null!;
    private readonly Dictionary<NeedKind, Meter> _needs = [];
    private MemberId? _shownMember;

    // Clock
    private TextBlock _date = null!;
    private TextBlock _clock = null!;
    private TextBlock _weather = null!;
    private TextBlock _money = null!;
    private StackPanel _speeds = null!;

    // Objective, scenario, family
    private TextBlock _objectiveIcon = null!;
    private TextBlock _objective = null!;
    private TextBlock _chapter = null!;
    private Border _banner = null!;
    private StackPanel _bannerBody = null!;
    private readonly List<MemberRow> _rows = [];
    private StackPanel _petRows = null!;

    // Prompt, notices, subtitles
    private Border _prompt = null!;
    private StackPanel _promptBody = null!;
    private List<InteractionTarget> _targets = [];
    private StackPanel _toasts = null!;
    private readonly List<(Control Control, float Life)> _toastLife = [];
    private Border _subtitle = null!;
    private TextBlock _subtitleText = null!;
    private float _subtitleLife;
    private TextBlock _fpsText = null!;
    private TextBlock _pauseBadge = null!;
    private Border _tip = null!;
    private TextBlock _tipText = null!;
    private string? _tipId;

    private void BuildHud()
    {
        _hud.Children.Clear();
        _rows.Clear();
        _needs.Clear();
        _shownMember = null;

        // ---- top-left: the controlled character.
        _portraitHost = new Panel();
        _name = Ui.Title("", 19);
        _role = Ui.Text("", 12, Ui.Muted);
        _moodText = Ui.Text("", 13, Ui.Ink, FontWeight.SemiBold);
        _stamina = new Meter(150, 12);
        _staminaText = Ui.Text("", 12, Ui.Muted);
        Grid needs = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,12,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        int index = 0;
        foreach (NeedKind need in Needs.All)
        {
            int row = index % 4;
            int col = index < 4 ? 0 : 3;
            TextBlock icon = Ui.Emoji(Needs.Icon(need), 13);
            ToolTip.SetTip(icon, Needs.Name(need));
            icon.Margin = new Thickness(0, 2, 6, 2);
            Meter meter = new(78, 9);
            ToolTip.SetTip(meter, Needs.Name(need));
            Grid.SetRow(icon, row);
            Grid.SetColumn(icon, col);
            Grid.SetRow(meter, row);
            Grid.SetColumn(meter, col + 1);
            needs.Children.Add(icon);
            needs.Children.Add(meter);
            _needs[need] = meter;
            index++;
        }

        StackPanel info = Ui.Stack(2, Orientation.Vertical, _name, _role, _moodText);
        Grid top = Ui.Row((_portraitHost, GridLength.Auto), (info, GridLength.Star));
        top.ColumnSpacing = 12;
        StackPanel staminaRow = Ui.Stack(8, Orientation.Horizontal, Ui.Emoji("⚡", 14), _stamina, _staminaText);
        staminaRow.Margin = new Thickness(0, 8, 0, 0);
        Border character = Ui.Card(Ui.Stack(0, Orientation.Vertical, top, staminaRow, needs), 12);
        character.Width = 300;
        character.HorizontalAlignment = HorizontalAlignment.Left;
        character.VerticalAlignment = VerticalAlignment.Top;
        character.Margin = new Thickness(14);
        character.Cursor = new Cursor(StandardCursorType.Hand);
        character.PointerPressed += (_, _) => OpenPanel("family");
        _hud.Children.Add(character);

        // ---- top-centre: objective, chapter and scenario banner.
        _objectiveIcon = Ui.Emoji("🏡", 18);
        _objective = Ui.Text("", 15, Ui.Ink, FontWeight.SemiBold);
        _objective.MaxWidth = 460;
        _chapter = Ui.Text("", 11.5, Ui.Muted);
        Border objective = Ui.Card(Ui.Stack(2, Orientation.Vertical, Ui.Stack(8, Orientation.Horizontal, _objectiveIcon, _objective), _chapter), 10);
        objective.HorizontalAlignment = HorizontalAlignment.Center;
        objective.VerticalAlignment = VerticalAlignment.Top;
        objective.Margin = new Thickness(0, 14, 0, 0);
        _bannerBody = new StackPanel { Spacing = 4 };
        _banner = Ui.Card(_bannerBody, 12, "#FFF0E6");
        _banner.BorderBrush = Ui.B(Ui.BadColor);
        _banner.BorderThickness = new Thickness(2);
        _banner.IsVisible = false;
        _banner.Width = 420;
        StackPanel centre = Ui.Stack(10, Orientation.Vertical, objective, _banner);
        centre.HorizontalAlignment = HorizontalAlignment.Center;
        centre.VerticalAlignment = VerticalAlignment.Top;
        centre.Margin = new Thickness(0, 14, 0, 0);
        objective.Margin = default;
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _hud.Children.Add(centre);

        // ---- top-right: clock, weather, money, speed, menu.
        _date = Ui.Text("", 12.5, Ui.Muted, FontWeight.SemiBold);
        _clock = Ui.Text("", 30, Ui.Ink, FontWeight.Black);
        _weather = Ui.Text("", 13, Ui.Ink);
        _money = Ui.Text("", 15, Ui.B("#2E7D32"), FontWeight.Bold);
        _speeds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _pauseBadge = Ui.Text("", 12, Ui.B(Ui.BadColor), FontWeight.Bold);
        Border clock = Ui.Card(Ui.Stack(2, Orientation.Vertical, _date, _clock, _weather, _money, _speeds, _pauseBadge), 12);
        clock.Width = 220;
        StackPanel menu = Ui.Stack(6, Orientation.Horizontal,
            RoundButton("📒", "journal", Loc.T("Jurnal (J)", "Journal (J)")),
            RoundButton("👪", "family", Loc.T("Keluarga (K)", "Family (K)")),
            RoundButton("🗺", "map", Loc.T("Peta (M)", "Map (M)")),
            RoundButton("🔨", "build", Loc.T("Bangun (B)", "Build (B)")),
            RoundButton("📸", "album", Loc.T("Album (L)", "Album (L)")),
            RoundButton("🎒", "inventory", Loc.T("Barang (I)", "Items (I)")),
            RoundButton("⏸", "pause", Loc.T("Menu (Esc)", "Menu (Esc)")));
        StackPanel right = Ui.Stack(10, Orientation.Vertical, clock, menu);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        right.VerticalAlignment = VerticalAlignment.Top;
        right.Margin = new Thickness(14);
        _hud.Children.Add(right);

        // ---- right: family status panel.
        StackPanel family = new() { Spacing = 6 };
        family.Children.Add(Ui.Text(Loc.T("KELUARGA", "FAMILY"), 11, Ui.Muted, FontWeight.Bold));
        foreach (MemberId id in FamilyNames.All)
        {
            MemberRow row = new() { Id = id, Mood = Ui.Emoji("🙂", 16), Status = Ui.Text("", 11.5, Ui.Muted), Lowest = new Meter(54, 7), LowestIcon = Ui.Emoji("🍚", 11) };
            row.Status.MaxWidth = 150;
            StackPanel nameLine = Ui.Stack(6, Orientation.Horizontal, Ui.Text(FamilyNames.Short(id), 13, Ui.Ink, FontWeight.SemiBold), row.LowestIcon, row.Lowest);
            StackPanel text = Ui.Stack(0, Orientation.Vertical, nameLine, row.Status);
            Grid g = Ui.Row((Ui.Portrait(id, 34), GridLength.Auto), (text, GridLength.Star), (row.Mood, GridLength.Auto));
            g.ColumnSpacing = 8;
            row.Card = new Border { Child = g, Padding = new Thickness(5, 3), CornerRadius = new CornerRadius(12), Background = Ui.B("#00FFFFFF"), Cursor = new Cursor(StandardCursorType.Hand) };
            row.Card.PointerPressed += (_, _) => Session.SwitchControl(id);
            ToolTip.SetTip(row.Card, Loc.T("Klik untuk mengendalikan", "Click to play as this member"));
            family.Children.Add(row.Card);
            _rows.Add(row);
        }

        _petRows = new StackPanel { Spacing = 4 };
        family.Children.Add(_petRows);
        Border familyCard = Ui.Card(family, 10);
        familyCard.Width = 250;
        familyCard.HorizontalAlignment = HorizontalAlignment.Right;
        familyCard.VerticalAlignment = VerticalAlignment.Top;
        familyCard.Margin = new Thickness(0, 250, 14, 0);
        _hud.Children.Add(familyCard);

        // ---- bottom: interaction prompt, subtitles, notices.
        _promptBody = new StackPanel { Spacing = 6 };
        _prompt = Ui.Card(_promptBody, 12);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.VerticalAlignment = VerticalAlignment.Bottom;
        _prompt.Margin = new Thickness(0, 0, 0, 18);
        _prompt.MaxWidth = 560;
        _prompt.IsVisible = false;
        _hud.Children.Add(_prompt);

        _subtitleText = new TextBlock { Foreground = Brushes.White, FontSize = Ui.Fs(17), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 720 };
        _subtitle = new Border
        {
            Background = Ui.B("#B3000000"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 8),
            Child = _subtitleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 200),
            IsVisible = false,
            IsHitTestVisible = false,
        };
        _hud.Children.Add(_subtitle);

        _toasts = new StackPanel { Spacing = 6, Width = 330, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14, 0, 0, 18), IsHitTestVisible = false };
        _toastLife.Clear();
        _hud.Children.Add(_toasts);

        _fpsText = new TextBlock { Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 2, 0, 0), IsHitTestVisible = false };
        _hud.Children.Add(_fpsText);

        _tipText = Ui.Text("", 13.5, Ui.Ink, wrap: true);
        _tip = Ui.Card(Ui.Stack(8, Orientation.Vertical,
            Ui.Stack(8, Orientation.Horizontal, Ui.Emoji("💡", 18), Ui.Text(Loc.T("Tips", "Tip"), 14, Ui.B("#F28C38"), FontWeight.Bold)),
            _tipText,
            Ui.Ghost(Loc.T("Mengerti", "Got it"), () => DismissTip())), 12, "#FFFBE6");
        _tip.Width = 280;
        _tip.HorizontalAlignment = HorizontalAlignment.Right;
        _tip.VerticalAlignment = VerticalAlignment.Bottom;
        _tip.Margin = new Thickness(0, 0, 14, 18);
        _tip.IsVisible = false;
        _hud.Children.Add(_tip);

        UpdateHud();
    }

    private Button RoundButton(string icon, string panel, string tip)
    {
        Button b = new()
        {
            Content = Ui.Emoji(icon, 17),
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = Ui.Paper,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) =>
        {
            Audio.Click();
            if (panel == "pause")
            {
                ShowPause();
            }
            else
            {
                OpenPanel(panel);
            }
        };
        return b;
    }

    private void UpdateHud()
    {
        if (_name is null)
        {
            return;
        }

        GameState state = Session.State;
        FamilyMember me = Session.Controlled;
        if (_shownMember != me.Id)
        {
            _shownMember = me.Id;
            _portraitHost.Children.Clear();
            _portraitHost.Children.Add(Ui.Portrait(me.Id, 62));
            _name.Text = me.Name;
            _role.Text = FamilyNames.Role(me.Id);
        }

        _moodText.Text = $"{Mood.Emoji(me.Mood.Current)} {Mood.Name(me.Mood.Current)} · {me.StatusText}";
        StaminaState staminaState = me.Stamina.State;
        _stamina.Set(me.Stamina.Value, staminaState switch
        {
            StaminaState.Ready => Ui.GoodColor,
            StaminaState.Tired => Ui.WarnColor,
            _ => Ui.BadColor,
        });
        _staminaText.Text = $"{Stamina.Label(staminaState)} {me.Stamina.Value:0}%";
        foreach ((NeedKind need, Meter meter) in _needs)
        {
            meter.Set(me.Needs[need]);
        }

        _date.Text = $"{Session.Date} · {Loc.T("Hari", "Day")} {Session.Clock.DayIndex + 1}";
        _clock.Text = Session.Clock.TimeText;
        WeatherState w = state.Weather;
        _weather.Text = $"{WeatherState.Icon(w.Current, Session.Clock.IsNight)} {WeatherState.Name(w.Current)} · {w.Temperature:0}°C · {Loc.T("Besok", "Tmrw")} {WeatherState.Icon(w.Forecast)}";
        _money.Text = $"💰 {Loc.Money(state.Wallet.Money)}";
        RebuildSpeeds();
        _pauseBadge.Text = _pausedByPlayer ? Loc.T("⏸ DIJEDA (Spasi)", "⏸ PAUSED (Space)") : Session.AllAsleep ? Loc.T("💤 Semua tidur… waktu dipercepat", "💤 Everyone's asleep… fast forward") : "";

        (string icon, string text) = Session.CurrentObjective();
        _objectiveIcon.Text = icon;
        _objective.Text = text;
        if (Chapters.Get(state.Chapter) is { } chapter)
        {
            int done = chapter.Goals.Count(g => g.Done(state));
            _chapter.Text = Loc.T($"Bab {chapter.Number}: {chapter.Title} · {done}/{chapter.Goals.Length} tujuan · {state.House.Title}", $"Chapter {chapter.Number}: {chapter.Title} · {done}/{chapter.Goals.Length} goals · {state.House.Title}");
        }
        else
        {
            _chapter.Text = $"{Chapters.SandboxTitle} · {state.House.Title}";
        }

        UpdateBanner();

        foreach (MemberRow row in _rows)
        {
            FamilyMember m = state.Member(row.Id);
            row.Mood.Text = m.InDanger ? "🆘" : Mood.Emoji(m.Mood.Current);
            row.Status.Text = m.InDanger ? m.StatusText : $"{m.StatusText} · {Session.LocationName(m)}";
            (NeedKind lowest, _) = m.Needs.MostUrgent();
            row.LowestIcon.Text = Needs.Icon(lowest);
            row.Lowest.Set(m.Needs[lowest]);
            bool controlled = m.Id == state.Controlled;
            bool blink = m.InDanger && ((int)(Session.Now * 2) % 2 == 0 || DateTime.Now.Millisecond < 500);
            row.Card.Background = m.InDanger ? Ui.B(blink ? "#FFD9D9" : "#FFEFEF") : controlled ? Ui.B("#FFEBD1") : Ui.B("#00FFFFFF");
            row.Card.Opacity = m.Away ? 0.6 : 1;
        }

        _petRows.Children.Clear();
        foreach (Pet pet in state.Pets)
        {
            _petRows.Children.Add(Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(pet.Icon, 16), Ui.Text(pet.Name, 12.5, weight: FontWeight.SemiBold),
                Ui.Bar(pet.Hunger, width: 30, height: 6, icon: "🦴"), Ui.Bar(pet.Happiness, width: 30, height: 6, icon: "❤"), Ui.Bar(pet.Trust, width: 30, height: 6, icon: "🤝")));
        }

        _fpsText.Text = _window.Settings.ShowFps ? $"{_fps:0} fps · {_view.Stats.DrawCalls} draws · {_view.Stats.Triangles / 1000}k tris" : "";
        UpdateTips();
    }

    private float _lastSpeed = -1f;
    private bool _lastPaused;

    private void RebuildSpeeds()
    {
        if (Math.Abs(_lastSpeed - Session.Clock.Speed) < 0.01f && _lastPaused == _pausedByPlayer)
        {
            return;
        }

        _lastSpeed = Session.Clock.Speed;
        _lastPaused = _pausedByPlayer;
        _speeds.Children.Clear();
        _speeds.Children.Add(SpeedButton("⏸", _pausedByPlayer, () => _pausedByPlayer = !_pausedByPlayer));
        foreach ((float speed, string label) in new[] { (1f, "▶"), (2f, "▶▶"), (4f, "▶▶▶") })
        {
            _speeds.Children.Add(SpeedButton(label, !_pausedByPlayer && Math.Abs(Session.Clock.Speed - speed) < 0.01f, () =>
            {
                _pausedByPlayer = false;
                Session.SetSpeed(speed);
            }));
        }
    }

    private static Button SpeedButton(string label, bool active, Action click)
    {
        Button b = new()
        {
            Content = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = active ? Brushes.White : Ui.Ink },
            Background = active ? Ui.Accent : Ui.B("#FFF1DE"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3),
            MinWidth = 34,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        b.Click += (_, _) => click();
        return b;
    }

    private void UpdateBanner()
    {
        Scenario? scenario = Session.Scenario;
        _banner.IsVisible = scenario is not null;
        if (scenario is null)
        {
            return;
        }

        _bannerBody.Children.Clear();
        _bannerBody.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(scenario.Icon, 20), Ui.Title(scenario.Title, 16)));
        if (scenario is GreatStormScenario storm)
        {
            _bannerBody.Children.Add(Ui.Text(storm.PhaseName, 12.5, Ui.B("#C0392B"), FontWeight.SemiBold));
        }

        if (scenario.Banner is { Length: > 0 } banner && scenario.Countdown is { } seconds)
        {
            int s = (int)MathF.Max(0f, seconds);
            _bannerBody.Children.Add(Ui.Text(banner, 17, Ui.B(Ui.BadColor), FontWeight.Black));
            _bannerBody.Children.Add(Ui.Text(Loc.T($"Aman dalam: {s / 60:00}:{s % 60:00}", $"Safe in: {s / 60:00}:{s % 60:00}"), 22, Ui.B(Ui.BadColor), FontWeight.Black));
            _bannerBody.Children.Add(Ui.Text(Loc.T("Temukan dan bawa ke tempat aman.", "Find them and bring them to a safe place."), 12.5, Ui.Muted));
        }

        foreach (Objective o in scenario.Objectives)
        {
            _bannerBody.Children.Add(Ui.Stack(6, Orientation.Horizontal,
                Ui.Emoji(o.Done ? "✅" : o.Icon, 13),
                Ui.Text(o.Text + (o.Optional ? Loc.T(" (opsional)", " (optional)") : ""), 12.5, o.Done ? Ui.Muted : Ui.Ink, o.Done ? FontWeight.Normal : FontWeight.SemiBold, wrap: true)));
        }
    }

    // ---------------------------------------------------------------- prompt

    private void UpdatePrompt()
    {
        if (_prompt is null)
        {
            return;
        }

        if (PanelOpen || _buildMode || Session.ScenarioFailed)
        {
            _prompt.IsVisible = false;
            return;
        }

        _targets = Session.GetInteractions();
        if (_targets.Count == 0)
        {
            _prompt.IsVisible = false;
            return;
        }

        InteractionTarget target = _targets[0];
        _promptBody.Children.Clear();
        _promptBody.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(target.Icon, 20), Ui.Title(target.Title, 16),
            _targets.Count > 1 ? Ui.Text(Loc.T($"+{_targets.Count - 1} lainnya di dekat sini", $"+{_targets.Count - 1} more nearby"), 11.5, Ui.Muted) : new Panel()));
        WrapPanel options = new() { Orientation = Orientation.Horizontal };
        int key = 1;
        foreach (InteractionOption option in target.Options.Take(9))
        {
            int index = key - 1;
            string hint = key == 1 ? "E" : key.ToString();
            Button b = Ui.Button($"[{hint}] {option.Label}", () => ExecuteOption(index), option.Enabled ? (key == 1 ? "#F28C38" : "#FFE3C2") : "#EEE6DC", option.Enabled ? (key == 1 ? "#FFFFFF" : "#4A3A30") : "#A89A8C", 12.5, option.Icon, option.Enabled);
            b.Margin = new Thickness(0, 0, 6, 6);
            if (!option.Enabled && option.Reason is not null)
            {
                ToolTip.SetTip(b, option.Reason);
                ToolTip.SetShowOnDisabled(b, true);
            }

            options.Children.Add(b);
            key++;
        }

        _promptBody.Children.Add(options);
        InteractionOption? firstDisabled = target.Options.FirstOrDefault(o => !o.Enabled && o.Reason is not null);
        if (firstDisabled is not null && target.Options.All(o => !o.Enabled))
        {
            _promptBody.Children.Add(Ui.Text("ℹ " + firstDisabled.Reason, 12, Ui.Muted, wrap: true));
        }

        _prompt.IsVisible = true;
    }

    private void ExecuteOption(int index)
    {
        if (_targets.Count == 0 || PanelOpen)
        {
            return;
        }

        InteractionTarget target = _targets[0];
        List<InteractionOption> enabled = target.Options;
        if (index < 0 || index >= enabled.Count)
        {
            return;
        }

        InteractionOption option = enabled[index];
        if (!option.Enabled)
        {
            Audio.Play("wrong", gain: 0.5f);
            if (option.Reason is not null)
            {
                Notify(option.Reason, "ℹ", NoticeKind.Info);
            }

            return;
        }

        option.Execute();
        _promptTimer = 0f;
        MarkTip("interact");
    }

    // --------------------------------------------------------- notices etc.

    public void Notify(string text, string icon, NoticeKind kind)
    {
        if (_toasts is null)
        {
            return;
        }

        string background = kind switch
        {
            NoticeKind.Good => "#EAF7E6",
            NoticeKind.Warning => "#FFF4D6",
            NoticeKind.Danger => "#FFE1E1",
            NoticeKind.Money => "#E6F4EA",
            NoticeKind.Skill => "#EAF0FF",
            NoticeKind.Memory => "#FDEBFF",
            _ => "#FFFDF7",
        };
        Border toast = Ui.Card(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(icon, 16), Ui.Text(text, 12.5, Ui.Ink, wrap: true)), 9, background, 12);
        ((StackPanel)toast.Child!).Children[1].Width = 270;
        _toasts.Children.Add(toast);
        _toastLife.Add((toast, 6f));
        while (_toasts.Children.Count > 6)
        {
            _toasts.Children.RemoveAt(0);
            _toastLife.RemoveAt(0);
        }

        if (kind == NoticeKind.Danger)
        {
            Audio.Play("alert", gain: 0.4f);
        }
    }

    private void UpdateToasts(float dt)
    {
        for (int i = _toastLife.Count - 1; i >= 0; i--)
        {
            (Control control, float life) = _toastLife[i];
            life -= dt;
            control.Opacity = Math.Clamp(life, 0f, 1f);
            if (life <= 0f)
            {
                _toasts.Children.Remove(control);
                _toastLife.RemoveAt(i);
            }
            else
            {
                _toastLife[i] = (control, life);
            }
        }

        if (_subtitle is not null && _subtitleLife > 0f)
        {
            _subtitleLife -= dt;
            _subtitle.IsVisible = _subtitleLife > 0f;
        }
    }

    private void ShowSubtitle(string speaker, string text, MemberId? member)
    {
        if (_subtitle is null || !_window.Settings.Subtitles)
        {
            return;
        }

        _subtitleText.Inlines?.Clear();
        _subtitleText.Text = null;
        _subtitleText.Inlines ??= [];
        _subtitleText.Inlines.Add(new Avalonia.Controls.Documents.Run($"{speaker}: ") { Foreground = member is { } id ? Ui.B(FamilyNames.Accent(id)) : Ui.B("#FFD9A8"), FontWeight = FontWeight.Bold });
        _subtitleText.Inlines.Add(new Avalonia.Controls.Documents.Run(text));
        _subtitle.IsVisible = true;
        _subtitleLife = 3.5f + (text.Length * 0.04f);
    }

    private void ShowPolaroid(FamilyMemory memory)
    {
        Audio.Play("camera", gain: 0.6f);
        Notify(Loc.T($"📸 Kenangan tersimpan di album: {memory.Title}", $"📸 Saved to the album: {memory.Title}"), FamilyMemory.OutcomeIcon(memory.Outcome), NoticeKind.Memory);
    }

    // ----------------------------------------------------------------- tips

    private static readonly (string Id, string TextId, string TextEn)[] Tips =
    [
        ("move", "Gunakan W A S D (atau klik tanah) untuk berjalan. Tahan Shift untuk berlari. Seret klik kanan untuk memutar kamera.", "Use W A S D (or click the ground) to walk. Hold Shift to run. Right-drag to turn the camera."),
        ("interact", "Dekati benda atau anggota keluarga, lalu tekan E (atau klik tombol) untuk berinteraksi.", "Walk up to things or family members and press E (or click a button) to interact."),
        ("needs", "Kartu kiri atas menunjukkan kebutuhanmu. Jika ada yang merah, cari makan, tidur, mandi atau bermain!", "The top-left card shows your needs. When one turns red, eat, sleep, wash or play!"),
        ("family", "Panel kanan menunjukkan keluarga. Mereka hidup sendiri, tapi kamu bisa klik untuk bermain sebagai mereka (Tab).", "The right panel shows the family. They live on their own, but you can click to play as them (Tab)."),
        ("map", "Tekan M untuk peta kota: sekolah, supermarket, taman, pantai, hutan dan perkemahan.", "Press M for the town map: school, supermarket, park, beach, forest and campsite."),
        ("build", "Tekan B untuk mode bangun: tambah ruangan, beli perabot, cat dinding.", "Press B for build mode: add rooms, buy furniture, paint walls."),
        ("journal", "Tekan J untuk jurnal: tujuan bab, kalender keluarga dan pencapaian.", "Press J for the journal: chapter goals, the family calendar and achievements."),
    ];

    private readonly HashSet<string> _doneTips = [];
    private float _tipDelay = 4f;

    private void UpdateTips()
    {
        if (!_window.Settings.Tutorials || _tip is null)
        {
            if (_tip is not null)
            {
                _tip.IsVisible = false;
            }

            return;
        }

        if (Session.State.Flag("tips-done"))
        {
            _tip.IsVisible = false;
            return;
        }

        if (_tipId is null)
        {
            _tipDelay -= 0.12f;
            if (_tipDelay > 0f)
            {
                return;
            }

            (string Id, string TextId, string TextEn)? next = Tips.FirstOrDefault(t => !_doneTips.Contains(t.Id) && !Session.State.Flag("tip:" + t.Id));
            if (next is not { Id: not null } tip)
            {
                Session.State.Flags.Add("tips-done");
                return;
            }

            _tipId = tip.Id;
            _tipText.Text = Loc.T(tip.TextId, tip.TextEn);
            _tip.IsVisible = true;
        }

        if (_tipId == "move" && Session.Controlled.Moving)
        {
            MarkTip("move");
        }
    }

    private void MarkTip(string id)
    {
        if (_tipId == id)
        {
            DismissTip();
        }
        else
        {
            _doneTips.Add(id);
            Session.State.Flags.Add("tip:" + id);
        }
    }

    private void DismissTip()
    {
        if (_tipId is not null)
        {
            _doneTips.Add(_tipId);
            Session.State.Flags.Add("tip:" + _tipId);
        }

        _tipId = null;
        _tipDelay = 6f;
        if (_tip is not null)
        {
            _tip.IsVisible = false;
        }
    }
}
