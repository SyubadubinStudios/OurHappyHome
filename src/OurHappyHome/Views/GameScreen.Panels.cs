using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OurHappyHome.Core;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Economy;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.School;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;
using OurHappyHome.UI;
using Calendar = OurHappyHome.Core.Time.Calendar;

namespace OurHappyHome.Views;

public sealed partial class GameScreen
{
    private IMiniGame? _miniGame;

    public bool PanelOpen => _panels.Children.Count > 0;

    private void ShowModal(Control modal, bool dim = true)
    {
        _panels.Children.Clear();
        if (dim)
        {
            Border shade = new() { Background = Ui.Dim };
            _panels.Children.Add(shade);
        }

        _panels.Children.Add(modal);
        _keys.Clear();
    }

    public void ClosePanel()
    {
        if (_miniGame is not null)
        {
            return; // mini-games close themselves when finished
        }

        _panels.Children.Clear();
        _view.Focus();
    }

    public void OpenPanel(string panel, string argument = "")
    {
        Audio.Play("page", gain: 0.5f);
        switch (panel)
        {
            case "journal":
                ShowJournal();
                MarkTip("journal");
                break;
            case "family":
                ShowFamily(Session.State.Controlled);
                MarkTip("family");
                break;
            case "album":
                ShowAlbum();
                break;
            case "map":
                ShowMap();
                MarkTip("map");
                break;
            case "build":
                EnterBuildMode();
                MarkTip("build");
                break;
            case "inventory":
                ShowInventory();
                break;
            case "help":
                ShowHelp();
                break;
            case "recipes":
                ShowRecipes();
                break;
            case "shop":
                ShowShop(argument);
                break;
            case "gift":
                ShowGift(Enum.Parse<MemberId>(argument));
                break;
            case "adopt":
                ShowAdopt();
                break;
            case "pause":
                ShowPause();
                break;
        }
    }

    // ------------------------------------------------------------- pause

    public void ShowPause()
    {
        StackPanel body = new() { Spacing = 10, Width = 320, HorizontalAlignment = HorizontalAlignment.Center };
        void Add(string icon, string text, Action action) => body.Children.Add(Wide(Ui.Button(text, action, "#FFF1DE", "#5A4636", 16, icon)));
        body.Children.Add(Wide(Ui.Button(Loc.T("Lanjutkan", "Resume"), ClosePanel, icon: "▶", size: 17)));
        Add("💾", Loc.T("Simpan permainan", "Save game"), () =>
        {
            Save();
            ClosePanel();
        });
        Add("📒", Loc.T("Jurnal & kalender", "Journal & calendar"), () => OpenPanel("journal"));
        Add("👪", Loc.T("Keluarga", "Family"), () => OpenPanel("family"));
        Add("⚙", Loc.T("Pengaturan", "Settings"), () => ShowModal(SettingsPanel.Build(_window, ShowPause)));
        Add("❓", Loc.T("Bantuan & kontrol", "Help & controls"), ShowHelp);
        Add("🏠", Loc.T("Simpan & kembali ke menu", "Save & return to menu"), () =>
        {
            Save();
            _window.Transition(_window.ShowMenu);
        });
        ShowModal(Ui.Modal("⏸", Loc.T("Jeda", "Paused"), body, ClosePanel, 420, 620));
    }

    private static Control Wide(Button b)
    {
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        return b;
    }

    // ----------------------------------------------------- failure / chapter

    private void ShowFailure()
    {
        StackPanel body = new() { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
        body.Children.Add(Ui.Emoji("💔", 56));
        body.Children.Add(new TextBlock { Text = Loc.T("KELUARGA GAGAL", "FAMILY FAILED"), FontSize = Ui.Fs(34), FontWeight = FontWeight.Black, Foreground = Ui.B(Ui.BadColor), HorizontalAlignment = HorizontalAlignment.Center });
        body.Children.Add(new TextBlock { Text = Loc.T("Tidak ada yang boleh tertinggal.", "Nobody Gets Left Behind."), FontSize = Ui.Fs(20), FontStyle = FontStyle.Italic, Foreground = Ui.Ink, HorizontalAlignment = HorizontalAlignment.Center });
        body.Children.Add(Ui.Text(Session.FailureReason, 14, Ui.Muted, wrap: true));
        body.Children.Add(Ui.Text(Loc.T("Tenang, kita bisa coba lagi bersama. Ingat: cari anggota keluarga yang butuh bantuan, tekan E untuk menolong, lalu bawa ke tempat aman.", "Don't worry, we can try again together. Remember: find the family member who needs help, press E to help, then lead them to safety."), 13.5, Ui.Ink, wrap: true));
        StackPanel buttons = Ui.Stack(10, Orientation.Horizontal,
            Ui.Button(Loc.T("Ulangi Kejadian", "Restart Event"), () =>
            {
                _panels.Children.Clear();
                Session.RestartScenario();
                _renderer!.Session = Session;
                _view.Focus();
            }, icon: "🔄", size: 17));
        if (Session.State.Mode == GameMode.Cozy)
        {
            buttons.Children.Add(Ui.Ghost(Loc.T("Lewati kejadian ini", "Skip this event"), () =>
            {
                _panels.Children.Clear();
                Session.AbandonScenario();
                _view.Focus();
            }, "⏭"));
        }

        buttons.HorizontalAlignment = HorizontalAlignment.Center;
        body.Children.Add(buttons);
        Border card = Ui.Card(body, 28, "#FFF8EC", 26);
        card.Width = 560;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        _panels.Children.Clear();
        _panels.Children.Add(new Border { Background = Ui.B("#CC1E1410") });
        _panels.Children.Add(card);
    }

    private void ShowChapterCard(int number, bool completed)
    {
        Chapter? chapter = Chapters.Get(number);
        string title = chapter?.Title ?? Chapters.SandboxTitle;
        StackPanel body = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        body.Children.Add(Ui.Emoji(completed ? "🏆" : "📖", 54));
        body.Children.Add(new TextBlock
        {
            Text = completed ? Loc.T($"Bab {number} selesai!", $"Chapter {number} complete!") : number >= Chapters.Sandbox ? Loc.T("Babak akhir", "End game") : Loc.T($"Bab {number}", $"Chapter {number}"),
            FontSize = Ui.Fs(18), Foreground = Ui.Accent, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center,
        });
        body.Children.Add(new TextBlock { Text = title, FontSize = Ui.Fs(32), FontWeight = FontWeight.Black, Foreground = Ui.Ink, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (completed && chapter is not null)
        {
            body.Children.Add(Ui.Text(Loc.T($"Hadiah keluarga: {Loc.Money(chapter.Reward)}", $"Family reward: {Loc.Money(chapter.Reward)}"), 16, Ui.B("#2E7D32"), FontWeight.Bold));
        }
        else if (chapter is not null)
        {
            body.Children.Add(Ui.Text(chapter.Story, 15, Ui.Ink, wrap: true));
            foreach (Goal goal in chapter.Goals)
            {
                body.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(goal.Icon, 15), Ui.Text(goal.Text, 14)));
            }
        }
        else
        {
            body.Children.Add(Ui.Text(Loc.T("Semua bab selesai! Kini rumah dan keluarga ini milikmu untuk terus ditulis ceritanya.", "Every chapter is complete! The home and family are yours to keep writing stories with."), 15, Ui.Ink, wrap: true));
        }

        body.Children.Add(Ui.Button(Loc.T("Ayo!", "Let's go!"), ClosePanel, icon: "✨", size: 17));
        Border card = Ui.Card(body, 28, "#FFF8EC", 26);
        card.Width = 560;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        ShowModal(card);
        Audio.Play(completed ? "fanfare" : "achievement", gain: 0.7f);
    }

    // ----------------------------------------------------------- journal

    private void ShowJournal(int tab = 0)
    {
        GameState state = Session.State;
        StackPanel body = new() { Spacing = 12 };
        StackPanel tabs = Ui.Stack(6, Orientation.Horizontal,
            TabButton(Loc.T("Bab & Tujuan", "Chapters"), "🎯", tab == 0, () => ShowJournal(0)),
            TabButton(Loc.T("Kalender", "Calendar"), "📅", tab == 1, () => ShowJournal(1)),
            TabButton(Loc.T("Pencapaian", "Achievements"), "🏆", tab == 2, () => ShowJournal(2)),
            TabButton(Loc.T("Keuangan", "Money"), "💰", tab == 3, () => ShowJournal(3)),
            TabButton(Loc.T("Rapor", "Report card"), "📝", tab == 4, () => ShowJournal(4)));
        body.Children.Add(tabs);

        switch (tab)
        {
            case 0:
                foreach (Chapter chapter in Chapters.All)
                {
                    bool current = chapter.Number == state.Chapter;
                    bool done = chapter.Number < state.Chapter;
                    StackPanel c = new() { Spacing = 6 };
                    c.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(done ? "✅" : current ? "📖" : "🔒", 18), Ui.Title(Loc.T($"Bab {chapter.Number}: {chapter.Title}", $"Chapter {chapter.Number}: {chapter.Title}"), 16)));
                    if (current || done)
                    {
                        c.Children.Add(Ui.Text(chapter.Story, 13, Ui.Muted, wrap: true));
                        foreach (Goal goal in chapter.Goals)
                        {
                            float progress = Math.Min(goal.Progress(state), goal.Target);
                            c.Children.Add(Ui.Row(
                                (Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(goal.Done(state) || done ? "✅" : goal.Icon, 14), Ui.Text(goal.Text, 13.5)), GridLength.Star),
                                (Ui.Bar(done ? 100 : progress / goal.Target * 100f, width: 140), GridLength.Auto),
                                (Ui.Text(goal.Target >= 1000 ? Loc.Money((long)progress) : $" {progress:0}/{goal.Target:0}", 12, Ui.Muted), new GridLength(110))));
                        }
                    }

                    Border card = Ui.Card(c, 12, current ? "#FFF1DE" : "#FFFDF7");
                    body.Children.Add(card);
                }

                break;
            case 1:
                body.Children.Add(CalendarView());
                break;
            case 2:
                WrapPanel grid = new();
                foreach (Achievement a in Achievements.All)
                {
                    bool got = state.Achievements.Contains(a.Id);
                    Border card = Ui.Card(Ui.Stack(4, Orientation.Vertical, Ui.Emoji(got ? a.Icon : "🔒", 30), Ui.Text(a.Title, 13.5, Ui.Ink, FontWeight.Bold, wrap: true), Ui.Text(a.Description, 12, Ui.Muted, wrap: true),
                        Ui.Bar(Math.Min(100f, state.Stat(a.Stat) / a.Target * 100f), width: 150, height: 7)), 10, got ? "#FFF4D6" : "#F6F1EA");
                    card.Width = 180;
                    card.Margin = new Thickness(0, 0, 10, 10);
                    card.Opacity = got ? 1 : 0.75;
                    grid.Children.Add(card);
                }

                body.Children.Add(grid);
                break;
            case 3:
                body.Children.Add(Ui.Title(Loc.T($"Uang keluarga: {Loc.Money(state.Wallet.Money)}", $"Family money: {Loc.Money(state.Wallet.Money)}"), 18));
                body.Children.Add(Ui.Text(Loc.T($"Hasil usaha anak-anak: {Loc.Money(state.Wallet.KidsEarnings)} · Gaji Ayah minggu ini: {Loc.Money(state.PendingSalary)} (dibayar Jumat)", $"Kids' earnings: {Loc.Money(state.Wallet.KidsEarnings)} · Dad's pay this week: {Loc.Money(state.PendingSalary)} (paid Friday)"), 13, Ui.Muted, wrap: true));
                foreach (Transaction t in state.Wallet.Ledger.AsEnumerable().Reverse().Take(30))
                {
                    body.Children.Add(Ui.Row((Ui.Text($"{Loc.T("Hari", "Day")} {t.Day + 1}", 12, Ui.Muted), new GridLength(70)), (Ui.Text(t.Text, 13), GridLength.Star),
                        (Ui.Text((t.Amount >= 0 ? "+" : "") + Loc.Money(t.Amount), 13, Ui.B(t.Amount >= 0 ? "#2E7D32" : "#C0392B"), FontWeight.SemiBold), GridLength.Auto)));
                }

                break;
            case 4:
                foreach (MemberId child in FamilyNames.Children)
                {
                    StackPanel c = new() { Spacing = 4 };
                    c.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Portrait(child, 36), Ui.Title(FamilyNames.Short(child), 16), Ui.Text(Loc.T($"Hadir {state.School.Attended(child)} hari", $"Attended {state.School.Attended(child)} days"), 12.5, Ui.Muted)));
                    foreach (Subject subject in Lessons.All)
                    {
                        float avg = state.School.Average(child, subject);
                        float skill = state.Member(child).Skills[Lessons.Skill(subject)];
                        c.Children.Add(Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(Lessons.Icon(subject), 13), Ui.Text(Lessons.Name(subject), 13)), new GridLength(200)),
                            (Ui.Bar(avg > 0 ? avg : skill * 10f, width: 160), GridLength.Auto),
                            (Ui.Text(avg > 0 ? $"  {Lessons.Grade(avg)} ({avg:0})" : $"  Lv {skill:0.0}", 12.5, Ui.Muted), GridLength.Star)));
                    }

                    body.Children.Add(Ui.Card(c, 12));
                }

                break;
        }

        ShowModal(Ui.Modal("📒", Loc.T("Jurnal Keluarga", "Family Journal"), body, ClosePanel, 860, 680));
    }

    private static Button TabButton(string text, string icon, bool selected, Action click) =>
        Ui.Button(text, click, selected ? "#F28C38" : "#FFF1DE", selected ? "#FFFFFF" : "#5A4636", 13, icon);

    private Control CalendarView()
    {
        GameDate today = Session.Date;
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(Ui.Title($"{today.MonthName} · {Loc.T("Tahun", "Year")} {today.Year}", 18));
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
        string[] names = [Loc.T("Sen", "Mon"), Loc.T("Sel", "Tue"), Loc.T("Rab", "Wed"), Loc.T("Kam", "Thu"), Loc.T("Jum", "Fri"), Loc.T("Sab", "Sat"), Loc.T("Min", "Sun")];
        int rows = 7;
        for (int r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int i = 0; i < 7; i++)
        {
            TextBlock h = Ui.Text(names[i], 12, Ui.Muted, FontWeight.Bold);
            h.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(h, i);
            grid.Children.Add(h);
        }

        int first = GameDate.ToDayIndex(today.Year, today.Month, 1);
        GameDate firstDate = GameDate.FromDayIndex(first);
        int column = (int)firstDate.Weekday;
        int row = 1;
        for (int day = 1; day <= GameDate.DaysInMonth(today.Month); day++)
        {
            GameDate date = GameDate.FromDayIndex(first + day - 1);
            IReadOnlyList<CalendarEvent> events = Calendar.EventsOn(date);
            bool isToday = date.DayIndex == today.DayIndex;
            StackPanel cell = Ui.Stack(1, Orientation.Vertical, Ui.Text(day.ToString(), 13, isToday ? Brushes.White : Ui.Ink, FontWeight.Bold), Ui.Text(string.Concat(events.Take(3).Select(e => e.Icon)), 13));
            Border b = new()
            {
                Child = cell,
                Background = isToday ? Ui.Accent : events.Any(e => e.Kind is CalendarEventKind.Birthday or CalendarEventKind.Camping or CalendarEventKind.Festival) ? Ui.B("#FFF1DE") : Ui.Paper,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2),
                Padding = new Thickness(6, 4),
                MinHeight = 46,
            };
            if (events.Count > 0)
            {
                ToolTip.SetTip(b, string.Join("\n", events.Select(e => $"{e.Icon} {e.Title}: {e.Description}")));
            }

            Grid.SetColumn(b, column);
            Grid.SetRow(b, row);
            grid.Children.Add(b);
            column++;
            if (column == 7)
            {
                column = 0;
                row++;
            }
        }

        panel.Children.Add(grid);
        panel.Children.Add(Ui.Title(Loc.T("Akan datang", "Coming up"), 15));
        for (int i = 0; i < 21; i++)
        {
            GameDate date = GameDate.FromDayIndex(today.DayIndex + i);
            foreach (CalendarEvent e in Calendar.EventsOn(date).Where(e => e.Kind is not (CalendarEventKind.PancakeMorning or CalendarEventKind.WeekendShopping)))
            {
                panel.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Emoji(e.Icon, 15), Ui.Text($"{date}", 12.5, Ui.Muted), Ui.Text($"{e.Title} — {e.Description}", 13, wrap: true)));
            }
        }

        return panel;
    }

    // ------------------------------------------------------------- family

    private void ShowFamily(MemberId id)
    {
        GameState state = Session.State;
        FamilyMember m = state.Member(id);
        StackPanel body = new() { Spacing = 12 };
        StackPanel picker = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (MemberId other in FamilyNames.All)
        {
            Control p = Ui.Portrait(other, other == id ? 54 : 44, state.Member(other).InDanger);
            p.Cursor = new Cursor(StandardCursorType.Hand);
            p.PointerPressed += (_, _) => ShowFamily(other);
            ToolTip.SetTip(p, FamilyNames.Short(other));
            picker.Children.Add(p);
        }

        body.Children.Add(picker);

        Grid columns = new() { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        StackPanel left = new() { Spacing = 8 };
        left.Children.Add(Ui.Title($"{m.Name} · {FamilyNames.Role(id)}", 18));
        left.Children.Add(Ui.Text($"{Mood.Emoji(m.Mood.Current)} {Mood.Name(m.Mood.Current)} · {m.StatusText} · {Session.LocationName(m)}", 13.5));
        left.Children.Add(Ui.Text(Loc.T("Sifat: ", "Traits: ") + string.Join(", ", m.Personality.Traits), 13, Ui.Muted, wrap: true));
        left.Children.Add(Ui.Text(Loc.T("Suka: ", "Likes: ") + string.Join(", ", m.Personality.Likes), 13, Ui.Muted, wrap: true));
        left.Children.Add(Ui.Text(Loc.T("Takut: ", "Fears: ") + string.Join(", ", m.Personality.Fears), 13, Ui.Muted, wrap: true));
        left.Children.Add(Ui.Text(Loc.T("Cita-cita: ", "Goals: ") + string.Join(" · ", m.Personality.Goals), 13, Ui.Muted, wrap: true));
        left.Children.Add(Ui.Title(Loc.T("Kebutuhan", "Needs"), 15));
        foreach (NeedKind need in Needs.All)
        {
            left.Children.Add(Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(Needs.Icon(need), 13), Ui.Text(Needs.Name(need), 13)), new GridLength(150)), (Ui.Bar(m.Needs[need], width: 170), GridLength.Auto), (Ui.Text($" {m.Needs[need]:0}%", 12, Ui.Muted), GridLength.Star)));
        }

        left.Children.Add(Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Emoji("⚡", 13), Ui.Text(Loc.T("Stamina", "Stamina"), 13)), new GridLength(150)), (Ui.Bar(m.Stamina.Value, width: 170), GridLength.Auto), (Ui.Text($" {Stamina.Label(m.Stamina.State)}", 12, Ui.Muted), GridLength.Star)));
        left.Children.Add(Ui.Title(Loc.T("Perasaan saat ini", "Current feelings"), 15));
        foreach (Moodlet moodlet in m.Mood.Moodlets.Where(x => x.Text.Length > 0).Take(6))
        {
            left.Children.Add(Ui.Text($"{Mood.Emoji(moodlet.Kind)} {moodlet.Text} ({(moodlet.Value >= 0 ? "+" : "")}{moodlet.Value:0})", 12.5, Ui.Muted));
        }

        StackPanel right = new() { Spacing = 8 };
        right.Children.Add(Ui.Title(Loc.T("Hubungan", "Relationships"), 15));
        foreach (MemberId other in FamilyNames.All.Where(o => o != id))
        {
            float value = state.Relationships[id, other];
            right.Children.Add(Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Portrait(other, 26), Ui.Text(FamilyNames.Short(other), 13)), new GridLength(130)),
                (Ui.Bar(value, "#E57AA0", 150), GridLength.Auto),
                (Ui.Text($" {value:0}% · {Relationships.StatusName(state.Relationships.Status(id, other))}", 12, Ui.Muted), GridLength.Star)));
        }

        right.Children.Add(Ui.Title(Loc.T("Keahlian", "Skills"), 15));
        foreach ((SkillKind skill, float level) in m.Skills.Levels.Where(kv => kv.Value >= 0.1f).OrderByDescending(kv => kv.Value).Take(12))
        {
            right.Children.Add(Ui.Row((Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(Skills.Icon(skill), 13), Ui.Text(Skills.Name(skill), 13)), new GridLength(150)), (Ui.Bar(level * 10f, "#5B8DEF", 140), GridLength.Auto), (Ui.Text($" Lv {level:0.0}", 12, Ui.Muted), GridLength.Star)));
        }

        right.Children.Add(Ui.Title(Loc.T("Kenangan", "Memories"), 15));
        foreach (FamilyMemory memory in state.Memories.Where(mem => mem.Involves(id)).Reverse().Take(6))
        {
            right.Children.Add(Ui.Text($"{FamilyMemory.OutcomeIcon(memory.Outcome)} {memory.Title} · {Loc.T("Hari", "Day")} {memory.DayIndex + 1}", 12.5, Ui.Muted, wrap: true));
        }

        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(right);
        body.Children.Add(columns);
        if (id != state.Controlled && !m.Away && !m.InDanger)
        {
            body.Children.Add(Ui.Button(Loc.T($"Bermain sebagai {m.Name}", $"Play as {m.Name}"), () =>
            {
                Session.SwitchControl(id);
                ClosePanel();
            }, icon: "🎮"));
        }

        ShowModal(Ui.Modal("👪", Loc.T("Keluarga Kita", "Our Family"), body, ClosePanel, 920, 700));
    }

    // -------------------------------------------------------------- album

    private void ShowAlbum()
    {
        GameState state = Session.State;
        WrapPanel grid = new();
        foreach (FamilyMemory memory in state.Memories.AsEnumerable().Reverse())
        {
            StackPanel card = new() { Spacing = 4 };
            Bitmap? photo = null;
            if (memory.PhotoFile is { } file && File.Exists(file))
            {
                try
                {
                    using FileStream stream = File.OpenRead(file);
                    photo = Bitmap.DecodeToWidth(stream, 260);
                }
                catch (Exception)
                {
                    photo = null;
                }
            }

            card.Children.Add(new Border
            {
                Height = 150,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Background = Ui.B("#EFE3D3"),
                Child = photo is not null
                    ? new Image { Source = photo, Stretch = Stretch.UniformToFill }
                    : Ui.Emoji(FamilyMemory.OutcomeIcon(memory.Outcome), 42),
            });
            card.Children.Add(Ui.Text(memory.Title, 13, Ui.Ink, FontWeight.Bold, wrap: true));
            card.Children.Add(Ui.Text($"{GameDate.FromDayIndex(memory.DayIndex)} · {memory.Location}", 11, Ui.Muted, wrap: true));
            card.Children.Add(Ui.Text(memory.Description, 11.5, Ui.Ink, wrap: true));
            card.Children.Add(Ui.Stack(2, Orientation.Horizontal, [.. memory.Participants.Select(p => Ui.Portrait(p, 22))]));
            Border polaroid = new()
            {
                Background = Brushes.White,
                Padding = new Thickness(8, 8, 8, 10),
                Margin = new Thickness(0, 0, 14, 14),
                Width = 220,
                BoxShadow = BoxShadows.Parse("0 4 10 0 #33000000"),
                RenderTransform = new RotateTransform((memory.Id % 5) - 2),
                Child = card,
            };
            grid.Children.Add(polaroid);
        }

        Control body = grid.Children.Count > 0 ? grid : Ui.Text(Loc.T("Belum ada kenangan. Masak bersama, bermain, berpetualang, atau tekan P untuk berfoto!", "No memories yet. Cook together, play, go on trips, or press P to take a photo!"), 15, Ui.Muted, wrap: true);
        ShowModal(Ui.Modal("📸", Loc.T($"Album Keluarga · {state.Memories.Count} kenangan", $"Family Album · {state.Memories.Count} memories"), body, ClosePanel, 960, 720));
    }

    // ---------------------------------------------------------------- map

    private void ShowMap()
    {
        bool bring = true;
        Grid map = new() { Width = 820, Height = 560 };
        map.Children.Add(new Border { CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = new Image { Source = Ui.Image("Art/world-map.jpg"), Stretch = Stretch.UniformToFill, Opacity = 0.85 } });
        Canvas pins = new();
        map.Children.Add(pins);
        CheckBox family = new() { IsChecked = true, Content = Ui.Text(Loc.T("Ajak keluarga (perjalanan keluarga)", "Bring the family (family trip)"), 14, weight: FontWeight.SemiBold) };
        family.IsCheckedChanged += (_, _) => bring = family.IsChecked == true;

        foreach (Place place in Session.Map.Places.Where(p => p.Id != PlaceId.Neighborhood))
        {
            // Map layout follows the design: north up, school east, forest west, beach south.
            (double x, double y) = place.Id switch
            {
                PlaceId.Home => (0.48, 0.42),
                PlaceId.Park => (0.38, 0.53),
                PlaceId.School => (0.78, 0.40),
                PlaceId.Forest => (0.14, 0.42),
                PlaceId.Camping => (0.47, 0.12),
                PlaceId.Supermarket => (0.52, 0.64),
                PlaceId.Mall => (0.66, 0.66),
                PlaceId.Clinic => (0.44, 0.74),
                PlaceId.Restaurant => (0.58, 0.76),
                PlaceId.Arcade => (0.70, 0.78),
                PlaceId.ThemePark => (0.86, 0.68),
                _ => (0.5, 0.92),
            };
            Border pin = new()
            {
                Background = Ui.Paper,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(8, 4),
                BorderBrush = Ui.Accent,
                BorderThickness = new Thickness(2),
                BoxShadow = BoxShadows.Parse("0 3 8 0 #44000000"),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = Ui.Stack(4, Orientation.Horizontal, Ui.Emoji(place.Icon, 16), Ui.Text(place.Name, 12.5, Ui.Ink, FontWeight.SemiBold)),
            };
            ToolTip.SetTip(pin, place.Description);
            pin.PointerPressed += (_, _) =>
            {
                Audio.Play("whoosh");
                Session.Travel(place.Id, bring && place.Id != PlaceId.Home);
                _renderer?.Rig.Snap(new System.Numerics.Vector3(place.Entrance.X, 1f, place.Entrance.Y));
                if (_renderer is not null)
                {
                    _renderer.Rig.Yaw = place.EntranceYaw + MathF.PI;
                }
                ClosePanel();
                _window.Transition(() => { });
            };
            Canvas.SetLeft(pin, (x * 820) - 50);
            Canvas.SetTop(pin, (y * 560) - 14);
            pins.Children.Add(pin);
        }

        StackPanel body = Ui.Stack(10, Orientation.Vertical, map, family,
            Ui.Text(Loc.T("Tip: kamu juga bisa berjalan atau bersepeda ke mana saja. Perjalanan keluarga membuat kenangan!", "Tip: you can also walk or cycle anywhere. Family trips create memories!"), 12.5, Ui.Muted, wrap: true));
        ShowModal(Ui.Modal("🗺", Loc.T("Peta Kota", "Town Map"), body, ClosePanel, 880, 720));
    }

    // ------------------------------------------------------------- shop

    private void ShowShop(string shop)
    {
        GameState state = Session.State;
        StackPanel body = new() { Spacing = 10 };
        body.Children.Add(Ui.Text(Loc.T($"Uang keluarga: {Loc.Money(state.Wallet.Money)}", $"Family money: {Loc.Money(state.Wallet.Money)}"), 16, Ui.B("#2E7D32"), FontWeight.Bold));
        foreach (IGrouping<ItemCategory, ItemDef> group in ItemCatalog.All.Where(i => i.Shop == shop).GroupBy(i => i.Category))
        {
            body.Children.Add(Ui.Title(group.Key switch
            {
                ItemCategory.Ingredient => Loc.T("Bahan makanan", "Groceries"),
                ItemCategory.Safety => Loc.T("Keamanan & darurat", "Safety & emergencies"),
                ItemCategory.Gift => Loc.T("Hadiah", "Gifts"),
                ItemCategory.Hobby => Loc.T("Hobi", "Hobbies"),
                ItemCategory.Pet => Loc.T("Hewan peliharaan", "Pets"),
                _ => Loc.T("Kebun", "Garden"),
            }, 15));
            WrapPanel items = new();
            foreach (ItemDef item in group)
            {
                Border card = Ui.Card(Ui.Stack(4, Orientation.Vertical,
                    Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(item.Icon, 22), Ui.Text(item.Name, 13, Ui.Ink, FontWeight.SemiBold)),
                    Ui.Text($"{Loc.Money(item.Price)} · {Loc.T("punya", "own")} {state.Inventory.Count(item.Id)}", 12, Ui.Muted),
                    Ui.Stack(4, Orientation.Horizontal,
                        Ui.Button("+1", () =>
                        {
                            Session.Buy(item.Id);
                            ShowShop(shop);
                        }, size: 12, enabled: state.Wallet.CanAfford(item.Price)),
                        Ui.Ghost("+5", () =>
                        {
                            Session.Buy(item.Id, 5);
                            ShowShop(shop);
                        }, size: 12, enabled: state.Wallet.CanAfford(item.Price * 5)))), 10);
                card.Width = 200;
                card.Margin = new Thickness(0, 0, 8, 8);
                items.Children.Add(card);
            }

            body.Children.Add(items);
        }

        ShowModal(Ui.Modal(shop == ItemCatalog.Mall ? "🛍" : "🛒", shop == ItemCatalog.Mall ? Loc.T("Mal Ceria", "Happy Mall") : Loc.T("Supermarket Segar", "Fresh Supermarket"), body, ClosePanel, 900, 700));
    }

    // ----------------------------------------------------------- recipes

    private void ShowRecipes()
    {
        GameState state = Session.State;
        FamilyMember me = Session.Controlled;
        StackPanel body = new() { Spacing = 8 };
        body.Children.Add(Ui.Text(Loc.T($"Keahlian memasak {me.Name}: level {me.Skills[SkillKind.Cooking]:0.0}. Mainkan mini-game untuk hasil terbaik!", $"{me.Name}'s cooking skill: level {me.Skills[SkillKind.Cooking]:0.0}. Play the mini-game for the best result!"), 13.5, Ui.Muted, wrap: true));
        foreach (Recipe recipe in Recipes.All.Where(r => !r.GrillOnly || state.House.Furniture.Any(f => f.DefId == "bbq")))
        {
            bool can = recipe.CanMake(state.Inventory);
            Grid row = Ui.Row(
                (Ui.Emoji(recipe.Icon, 26), GridLength.Auto),
                (Ui.Stack(2, Orientation.Vertical, Ui.Text(recipe.Name, 14.5, Ui.Ink, FontWeight.Bold),
                    Ui.Text(recipe.IngredientText, 12, Ui.Muted, wrap: true),
                    Ui.Text(Loc.T($"Porsi {recipe.Servings} · Min. keahlian {recipe.MinSkill:0} · {recipe.Steps.Length} langkah", $"{recipe.Servings} servings · Min. skill {recipe.MinSkill:0} · {recipe.Steps.Length} steps"), 11.5, Ui.Muted)), GridLength.Star),
                (Ui.Button(Loc.T("Masak", "Cook"), () =>
                {
                    ClosePanel();
                    Session.StartPlayerCooking(recipe.Id);
                }, enabled: can, icon: "🍳", size: 13), GridLength.Auto));
            row.ColumnSpacing = 10;
            Border card = Ui.Card(row, 10, can ? "#FFFDF7" : "#F5EFE8");
            card.Opacity = can ? 1 : 0.7;
            body.Children.Add(card);
        }

        ShowModal(Ui.Modal("📖", Loc.T("Buku Resep Keluarga", "Family Recipe Book"), body, ClosePanel, 760, 680));
    }

    // -------------------------------------------------------------- gifts

    private void ShowGift(MemberId to)
    {
        GameState state = Session.State;
        FamilyMember other = state.Member(to);
        StackPanel body = new() { Spacing = 8 };
        body.Children.Add(Ui.Text(Loc.T($"{other.Name} suka: {string.Join(", ", other.Personality.Likes)}", $"{other.Name} likes: {string.Join(", ", other.Personality.Likes)}"), 13, Ui.Muted, wrap: true));
        foreach (ItemDef item in ItemCatalog.All.Where(i => i.Category is ItemCategory.Gift or ItemCategory.Hobby && state.Inventory.Has(i.Id)))
        {
            bool loved = item.Tags.Any(t => other.Personality.Likes.Contains(t));
            body.Children.Add(Ui.Card(Ui.Row(
                (Ui.Emoji(item.Icon, 24), GridLength.Auto),
                (Ui.Text($"{item.Name} ×{state.Inventory.Count(item.Id)}{(loved ? Loc.T("  💗 pasti suka!", "  💗 will love it!") : "")}", 14), GridLength.Star),
                (Ui.Button(Loc.T("Berikan", "Give"), () =>
                {
                    Session.GiveGift(to, item.Id);
                    ClosePanel();
                }, icon: "🎁", size: 13), GridLength.Auto)), 10));
        }

        if (body.Children.Count == 1)
        {
            body.Children.Add(Ui.Text(Loc.T("Belum punya hadiah. Beli di Mal!", "No gifts yet. Buy some at the Mall!"), 14, Ui.Muted));
        }

        ShowModal(Ui.Modal("🎁", Loc.T($"Hadiah untuk {other.Name}", $"A gift for {other.Name}"), body, ClosePanel, 600, 560));
    }

    // -------------------------------------------------------------- adopt

    private void ShowAdopt()
    {
        StackPanel body = new() { Spacing = 10 };
        TextBox name = new() { Text = "Brownie", Width = 220, PlaceholderText = Loc.T("Nama hewan", "Pet name") };
        body.Children.Add(Ui.Stack(8, Orientation.Horizontal, Ui.Text(Loc.T("Nama:", "Name:"), 14), name));
        foreach ((PetKind kind, string icon, string desc) in new[]
        {
            (PetKind.Dog, "🐶", Loc.T("Setia, suka bermain lempar bola, menggonggong jika ada yang aneh.", "Loyal, loves fetch, barks when something is wrong.")),
            (PetKind.Cat, "🐱", Loc.T("Lucu dan penasaran, suka tidur di sofa.", "Cute and curious, loves napping on the sofa.")),
            (PetKind.Rabbit, "🐰", Loc.T("Lembut dan pemalu, suka sayuran dari kebun.", "Soft and shy, loves garden vegetables.")),
            (PetKind.Hamster, "🐹", Loc.T("Kecil dan lincah.", "Tiny and quick.")),
            (PetKind.Fish, "🐠", Loc.T("Tenang dan menenangkan.", "Calm and calming.")),
        })
        {
            long cost = Pet.AdoptionCost(kind);
            body.Children.Add(Ui.Card(Ui.Row(
                (Ui.Emoji(icon, 30), GridLength.Auto),
                (Ui.Stack(2, Orientation.Vertical, Ui.Text($"{new Pet { Kind = kind }.KindName} · {Loc.Money(cost)}", 14.5, Ui.Ink, FontWeight.Bold), Ui.Text(desc, 12.5, Ui.Muted, wrap: true)), GridLength.Star),
                (Ui.Button(Loc.T("Adopsi", "Adopt"), () =>
                {
                    if (Session.AdoptPet(kind, name.Text ?? "Brownie"))
                    {
                        ClosePanel();
                    }
                }, icon: "🏠", size: 13, enabled: Session.State.Wallet.CanAfford(cost)), GridLength.Auto)), 10));
        }

        ShowModal(Ui.Modal("🐾", Loc.T("Adopsi Hewan Peliharaan", "Adopt a Pet"), body, ClosePanel, 640, 640));
    }

    // ---------------------------------------------------------- inventory

    private void ShowInventory()
    {
        GameState state = Session.State;
        StackPanel body = new() { Spacing = 10 };
        WrapPanel items = new();
        foreach ((string id, int count) in state.Inventory.Counts.Where(kv => kv.Value > 0 && ItemCatalog.Exists(kv.Key)))
        {
            ItemDef item = ItemCatalog.Get(id);
            Border card = Ui.Card(Ui.Stack(6, Orientation.Horizontal, Ui.Emoji(item.Icon, 20), Ui.Text($"{item.Name} ×{count}", 13)), 8);
            card.Margin = new Thickness(0, 0, 8, 8);
            items.Children.Add(card);
        }

        body.Children.Add(Ui.Title(Loc.T("Barang keluarga", "Family items"), 15));
        body.Children.Add(items);
        body.Children.Add(Ui.Title(Loc.T("Makanan siap santap", "Ready to eat"), 15));
        foreach (PreparedFood food in state.Pantry)
        {
            body.Children.Add(Ui.Text($"{food.Recipe.Icon} {food.Recipe.Name} · {Recipes.QualityName(food.Quality)} {Recipes.QualityStars(food.Quality)} · {Loc.T("porsi", "servings")} {food.Servings}", 13.5));
        }

        if (state.Pantry.Count == 0)
        {
            body.Children.Add(Ui.Text(Loc.T("Belum ada makanan. Masak di dapur!", "No food yet. Cook in the kitchen!"), 13, Ui.Muted));
        }

        body.Children.Add(Ui.Ghost(Loc.T(Session.FlashlightOn ? "Matikan senter (F)" : "Nyalakan senter (F)", Session.FlashlightOn ? "Switch off flashlight (F)" : "Switch on flashlight (F)"), () =>
        {
            Session.FlashlightOn = !Session.FlashlightOn;
            ShowInventory();
        }, "🔦", enabled: state.Inventory.Has("flashlight") || state.Inventory.Has("candle")));
        ShowModal(Ui.Modal("🎒", Loc.T("Barang & Dapur", "Items & Pantry"), body, ClosePanel, 760, 620));
    }

    // --------------------------------------------------------------- help

    private void ShowHelp()
    {
        StackPanel body = new() { Spacing = 6 };
        (string Keys, string Id, string En)[] controls =
        [
            ("W A S D / ←↑→↓", "Berjalan (relatif ke kamera)", "Walk (camera relative)"),
            ("Shift", "Berlari (memakai stamina)", "Run (uses stamina)"),
            (Loc.T("Klik kiri tanah", "Left-click ground"), "Berjalan ke titik itu", "Walk to that spot"),
            (Loc.T("Seret klik kanan · Z / C · roda", "Right-drag · Z / C · wheel"), "Putar kamera · zoom", "Rotate camera · zoom"),
            ("E / Enter · 1-9", "Interaksi dengan benda, keluarga, hewan", "Interact with objects, family, pets"),
            ("Tab", "Ganti anggota keluarga yang dikendalikan", "Switch the controlled family member"),
            ("F", "Senter", "Flashlight"),
            ("P", "Ambil foto keluarga (masuk album)", "Take a family photo (goes to the album)"),
            ("Spasi · , .", "Jeda · ubah kecepatan waktu", "Pause · change game speed"),
            ("J · K · M · B · L · I", "Jurnal · Keluarga · Peta · Bangun · Album · Barang", "Journal · Family · Map · Build · Album · Items"),
            ("R", "Putar perabot (mode bangun)", "Rotate furniture (build mode)"),
            ("Esc · F11", "Menu jeda / tutup panel · Layar penuh", "Pause menu / close panel · Fullscreen"),
        ];
        foreach ((string keys, string id, string en) in controls)
        {
            body.Children.Add(Ui.Row((Ui.Pill(keys, "#5A4636", size: 12.5), new GridLength(270)), (Ui.Text(Loc.T(id, en), 14), GridLength.Star)));
        }

        body.Children.Add(Ui.Title(Loc.T("Cara bermain", "How to play"), 16));
        foreach ((string id, string en) in new[]
        {
            ("Jaga kebutuhan setiap anggota keluarga: makan, tidur, mandi, bermain dan berkumpul.", "Look after everyone's needs: food, sleep, hygiene, fun and family time."),
            ("Keluarga hidup sendiri: Ibu memasak, Ayah bekerja dan memperbaiki, Kakak menggambar, Dinda membaca dan membantu Ibu.", "The family lives on its own: Mom cooks, Dad works and repairs, Nara draws, Dinda reads and helps Mom."),
            ("Pergi ke sekolah di hari kerja untuk mini-game pelajaran. Nilai bagus menaikkan keahlian.", "Go to school on weekdays for lesson mini-games. Good grades raise skills."),
            ("Hasilkan uang dengan jualan limun, gambar, kerajinan dan membantu tetangga. Ayah gajian setiap Jumat.", "Earn money with lemonade, drawings, crafts and helping neighbours. Dad is paid every Friday."),
            ("Kejadian darurat: ikuti daftar tugas di atas layar. Jika ada yang butuh bantuan, temukan dia, tekan E, lalu bawa ke tempat aman sebelum waktu habis.", "Emergencies: follow the checklist at the top. If someone needs help, find them, press E, and lead them to safety before time runs out."),
            ("Tidak ada yang boleh tertinggal. Jika gagal, ulangi kejadiannya.", "Nobody gets left behind. If it goes wrong, restart the event."),
        })
        {
            body.Children.Add(Ui.Text("• " + Loc.T(id, en), 13.5, Ui.Ink, wrap: true));
        }

        ShowModal(Ui.Modal("❓", Loc.T("Bantuan & Kontrol", "Help & Controls"), body, ClosePanel, 820, 660));
    }
}
