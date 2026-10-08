using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OurHappyHome.Core;
using OurHappyHome.Core.World;
using OurHappyHome.UI;
using Point = Avalonia.Point;

namespace OurHappyHome.Views;

public sealed partial class GameScreen
{
    private enum BuildTool
    {
        Rooms,
        Furniture,
        Move,
        Paint,
        Floor,
    }

    private bool _buildMode;
    private Border? _buildPanel;
    private BuildTool _tool = BuildTool.Rooms;
    private FurnitureCategory _category = FurnitureCategory.Living;
    private FurnitureDef? _placing;
    private FurnitureItem? _moving;
    private int _rotation;
    private Vector2 _cursor;
    private string _paint = "#F2E3C6";
    private FloorStyle _floor = FloorStyle.Wood;

    private static readonly string[] PaintColors =
    [
        "#F2E3C6", "#FFF4E0", "#F8DDE8", "#E7DDF0", "#D9E8F7", "#DDEFE6", "#FFF0C2", "#FFD8B5", "#CFE8C9", "#BFD7EA", "#F4C7C3", "#E9DCC9",
        "#FFE3E3", "#E0F7FA", "#FCE4B6", "#D7CCE8", "#B5D99C", "#F7C59F", "#A8DADC", "#FFCAD4", "#C9ADA7", "#E2ECE9", "#FDFFB6", "#9BC1BC",
    ];

    private void EnterBuildMode()
    {
        if (_renderer is null)
        {
            return;
        }

        _panels.Children.Clear();
        _buildMode = true;
        _renderer.BuildMode = true;
        _renderer.Rig.BuildCenter = new Vector3(1.5f, 0f, 0f);
        _renderer.Rig.Zoom = 1f;
        RebuildBuildPanel();
        Audio.Play("whoosh");
    }

    private void ExitBuildMode()
    {
        _buildMode = false;
        _placing = null;
        _moving = null;
        if (_renderer is not null)
        {
            _renderer.BuildMode = false;
            _renderer.House.BuildUpper = false;
            _renderer.Rig.Zoom = 1f;
        }

        if (_buildPanel is not null)
        {
            _root.Children.Remove(_buildPanel);
            _buildPanel = null;
        }

        _view.Focus();
    }

    private void RebuildBuildPanel()
    {
        if (_buildPanel is not null)
        {
            _root.Children.Remove(_buildPanel);
        }

        GameState state = Session.State;
        StackPanel body = new() { Spacing = 10 };
        body.Children.Add(Ui.Row((Ui.Stack(8, Orientation.Horizontal, Ui.Emoji("🔨", 22), Ui.Title(Loc.T("Mode Bangun", "Build Mode"), 19)), GridLength.Star),
            (Ui.Button("✕", ExitBuildMode, "#F3E2CF", "#5A4636", 13), GridLength.Auto)));
        body.Children.Add(Ui.Text($"💰 {Loc.Money(state.Wallet.Money)} · {state.House.Title} · {state.House.IndoorRoomCount} {Loc.T("ruangan", "rooms")}", 13, Ui.B("#2E7D32"), FontWeight.SemiBold));

        WrapPanel tools = new();
        foreach ((BuildTool tool, string icon, string label) in new[]
        {
            (BuildTool.Rooms, "🏠", Loc.T("Ruangan", "Rooms")),
            (BuildTool.Furniture, "🛋", Loc.T("Perabot", "Furniture")),
            (BuildTool.Move, "✋", Loc.T("Pindah/Jual", "Move/Sell")),
            (BuildTool.Paint, "🎨", Loc.T("Cat", "Paint")),
            (BuildTool.Floor, "🟫", Loc.T("Lantai", "Floors")),
        })
        {
            Button b = TabButton(label, icon, _tool == tool, () =>
            {
                _tool = tool;
                _placing = null;
                _moving = null;
                _renderer?.SetGhost(null, default, 0, true);
                RebuildBuildPanel();
            });
            b.Margin = new Thickness(0, 0, 6, 6);
            tools.Children.Add(b);
        }

        body.Children.Add(tools);

        if (state.House.HasUpperFloor && _renderer is not null)
        {
            // Which floor the cursor works on.
            WrapPanel floors = new();
            foreach ((bool upper, string icon, string label) in new[] { (false, "⬇", Loc.T("Lantai bawah", "Ground floor")), (true, "⬆", Loc.T("Lantai atas", "Upper floor")) })
            {
                Button b = TabButton(label, icon, _renderer.House.BuildUpper == upper, () =>
                {
                    _renderer.House.BuildUpper = upper;
                    Vector3 c = _renderer.Rig.BuildCenter;
                    _renderer.Rig.BuildCenter = new Vector3(c.X, upper ? Floors.Height : 0f, c.Z);
                    _placing = null;
                    _moving = null;
                    _renderer.SetGhost(null, default, 0, true);
                    RebuildBuildPanel();
                });
                b.Margin = new Thickness(0, 0, 6, 6);
                floors.Children.Add(b);
            }

            body.Children.Add(floors);
        }

        switch (_tool)
        {
            case BuildTool.Rooms:
                foreach (RoomDef room in Rooms.All.Where(r => r.Cost > 0 || state.House.Has(r.Id)))
                {
                    if (state.House.Has(room.Id) && room.Cost == 0)
                    {
                        continue;
                    }

                    bool built = state.House.Has(room.Id);
                    bool can = state.House.CanBuild(room.Id, state.Chapter, out string reason);
                    StackPanel info = Ui.Stack(2, Orientation.Vertical,
                        Ui.Text($"{Rooms.Icon(room.Id)} {room.Name}", 14, Ui.Ink, FontWeight.Bold),
                        Ui.Text(room.Description, 11.5, Ui.Muted, wrap: true),
                        Ui.Text(built ? Loc.T("✔ Sudah dibangun", "✔ Built") : can ? Loc.Money(room.Cost) : $"🔒 {reason} · {Loc.Money(room.Cost)}", 12, built ? Ui.B("#2E7D32") : Ui.Ink, FontWeight.SemiBold));
                    Grid row = Ui.Row((info, GridLength.Star), (built ? new Panel() : Ui.Button(Loc.T("Bangun", "Build"), () =>
                    {
                        if (Session.BuildRoom(room.Id))
                        {
                            RebuildBuildPanel();
                        }
                    }, size: 12.5, enabled: can && state.Wallet.CanAfford(room.Cost)), GridLength.Auto));
                    row.ColumnSpacing = 8;
                    body.Children.Add(Ui.Card(row, 10, built ? "#EAF7E6" : "#FFFDF7", 12));
                }

                break;
            case BuildTool.Furniture:
                WrapPanel cats = new();
                foreach (FurnitureCategory category in Enum.GetValues<FurnitureCategory>())
                {
                    Button c = Ui.Button(FurnitureCatalog.CategoryName(category), () =>
                    {
                        _category = category;
                        RebuildBuildPanel();
                    }, _category == category ? "#5A4636" : "#FFF1DE", _category == category ? "#FFFFFF" : "#5A4636", 11.5);
                    c.Margin = new Thickness(0, 0, 4, 4);
                    c.Padding = new Thickness(8, 4);
                    cats.Children.Add(c);
                }

                body.Children.Add(cats);
                foreach (FurnitureDef def in FurnitureCatalog.All.Where(d => d.Category == _category && d.Price > 0))
                {
                    bool unlocked = state.Chapter >= def.MinChapter;
                    bool selected = _placing?.Id == def.Id;
                    StackPanel info = Ui.Stack(2, Orientation.Vertical,
                        Ui.Text(def.Name, 13.5, Ui.Ink, FontWeight.Bold),
                        Ui.Text(unlocked ? $"{Loc.Money(def.Price)} · {string.Join(" ", def.Activities.Take(3).Select(a => Core.Simulation.ActivityCatalog.Get(a).Icon))}" : Loc.T($"🔒 Bab {def.MinChapter}", $"🔒 Chapter {def.MinChapter}"), 12, Ui.Muted));
                    Grid row = Ui.Row((info, GridLength.Star), (Ui.Button(selected ? Loc.T("Dipilih", "Selected") : Loc.T("Pilih", "Pick"), () =>
                    {
                        _placing = def;
                        _rotation = 0;
                        RebuildBuildPanel();
                    }, selected ? "#4CAF50" : "#F28C38", size: 12, enabled: unlocked && state.Wallet.CanAfford(def.Price)), GridLength.Auto));
                    body.Children.Add(Ui.Card(row, 8, selected ? "#FFF1DE" : "#FFFDF7", 12));
                }

                body.Children.Add(Ui.Text(Loc.T("Klik untuk menaruh · R memutar · klik kanan batal", "Click to place · R rotates · right-click cancels"), 12, Ui.Muted, wrap: true));
                break;
            case BuildTool.Move:
                body.Children.Add(Ui.Text(_moving is null
                    ? Loc.T("Klik perabot untuk memindahkannya. Klik lagi untuk menaruh. R memutar.", "Click furniture to pick it up. Click again to put it down. R rotates.")
                    : Loc.T($"Memindahkan: {_moving.Def.Name}", $"Moving: {_moving.Def.Name}"), 13.5, Ui.Ink, wrap: true));
                if (_moving is { } moving)
                {
                    body.Children.Add(Ui.Button(Loc.T($"Jual ({Loc.Money(moving.Def.Price / 2)})", $"Sell ({Loc.Money(moving.Def.Price / 2)})"), () =>
                    {
                        Session.SellFurniture(moving);
                        _moving = null;
                        _renderer?.SetGhost(null, default, 0, true);
                        RebuildBuildPanel();
                    }, "#E5484D", icon: "💲", size: 13));
                }

                break;
            case BuildTool.Paint:
                body.Children.Add(Ui.Text(Loc.T("Pilih warna, lalu klik ruangan (Rp 50.000).", "Pick a colour, then click a room (Rp 50,000)."), 13, Ui.Muted, wrap: true));
                WrapPanel swatches = new();
                foreach (string color in PaintColors)
                {
                    Border swatch = new()
                    {
                        Width = 40,
                        Height = 40,
                        Margin = new Thickness(0, 0, 6, 6),
                        CornerRadius = new CornerRadius(20),
                        Background = Ui.B(color),
                        BorderBrush = Ui.B(_paint == color ? "#3B2F2A" : "#D8C8B8"),
                        BorderThickness = new Thickness(_paint == color ? 3 : 1),
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    };
                    swatch.PointerPressed += (_, _) =>
                    {
                        _paint = color;
                        RebuildBuildPanel();
                    };
                    swatches.Children.Add(swatch);
                }

                body.Children.Add(swatches);
                break;
            case BuildTool.Floor:
                body.Children.Add(Ui.Text(Loc.T("Pilih lantai, lalu klik ruangan (Rp 100.000).", "Pick a floor, then click a room (Rp 100,000)."), 13, Ui.Muted, wrap: true));
                foreach ((FloorStyle style, string name) in new[]
                {
                    (FloorStyle.Wood, Loc.T("Kayu", "Wood")), (FloorStyle.Tile, Loc.T("Keramik", "Tile")), (FloorStyle.Carpet, Loc.T("Karpet", "Carpet")),
                    (FloorStyle.Checker, Loc.T("Kotak-kotak", "Checker")), (FloorStyle.Concrete, Loc.T("Semen", "Concrete")),
                })
                {
                    body.Children.Add(TabButton(name, "🟫", _floor == style, () =>
                    {
                        _floor = style;
                        RebuildBuildPanel();
                    }));
                }

                break;
        }

        body.Children.Add(Ui.Text(Loc.T("WASD menggeser kamera · roda zoom · Esc/B keluar", "WASD pans · wheel zooms · Esc/B exits"), 11.5, Ui.Muted, wrap: true));
        ScrollViewer scroll = new() { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _buildPanel = Ui.Card(scroll, 14, "#FFF8EC", 20);
        _buildPanel.Width = 330;
        _buildPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _buildPanel.Margin = new Thickness(14, 14, 0, 14);
        _root.Children.Add(_buildPanel);
    }

    private void UpdateBuildMode(float dt)
    {
        if (!_buildMode || _renderer is null)
        {
            return;
        }

        float forward = Axis(Avalonia.Input.Key.W, Avalonia.Input.Key.S) + Axis(Avalonia.Input.Key.Up, Avalonia.Input.Key.Down);
        float right = Axis(Avalonia.Input.Key.D, Avalonia.Input.Key.A) + Axis(Avalonia.Input.Key.Right, Avalonia.Input.Key.Left);
        Vector2 pan = (_renderer.Rig.Forward * forward) + (_renderer.Rig.Right * right);
        Vector3 c = _renderer.Rig.BuildCenter + (new Vector3(pan.X, 0f, pan.Y) * 14f * dt);
        _renderer.Rig.BuildCenter = new Vector3(Math.Clamp(c.X, -20f, 22f), _renderer.Rig.BuildCenter.Y, Math.Clamp(c.Z, -20f, 20f));
        if (Axis(Avalonia.Input.Key.Q, Avalonia.Input.Key.Z) != 0f)
        {
            _renderer.Rig.Yaw += Axis(Avalonia.Input.Key.Q, Avalonia.Input.Key.Z) * dt * 1.5f;
        }
    }

    private void BuildPointerMoved(Point p)
    {
        if (GroundAt(p) is not { } ground || _renderer is null)
        {
            return;
        }

        _cursor = new Vector2(MathF.Round(ground.X * 4f) / 4f, MathF.Round(ground.Y * 4f) / 4f);
        FurnitureDef? def = _placing ?? _moving?.Def;
        if (def is null)
        {
            return;
        }

        bool valid = PlacementRoom(def, _cursor) is { } room && Session.State.House.CanPlace(def, room, _cursor, _rotation, _moving);
        _renderer.SetGhost(def, _cursor, _rotation, valid);
    }

    private RoomId? PlacementRoom(FurnitureDef def, Vector2 at)
    {
        House house = Session.State.House;
        RoomId? room = house.RoomAt(at);
        if (room is not { } r)
        {
            return null;
        }

        bool indoor = Rooms.Get(r).Indoor;
        return def.Outdoor == !indoor ? r : null;
    }

    private void BuildClick(Point p)
    {
        if (GroundAt(p) is not { } ground)
        {
            return;
        }

        Vector2 at = new(MathF.Round(ground.X * 4f) / 4f, MathF.Round(ground.Y * 4f) / 4f);
        House house = Session.State.House;
        switch (_tool)
        {
            case BuildTool.Furniture when _placing is { } def:
                if (PlacementRoom(def, at) is { } room && Session.BuyFurniture(def.Id, room, at, _rotation))
                {
                    RebuildBuildPanel();
                }
                else
                {
                    Audio.Play("wrong", gain: 0.5f);
                }

                break;
            case BuildTool.Move:
                if (_moving is null)
                {
                    _moving = house.Furniture.FirstOrDefault(f => f.Bounds.Inflate(0.15f).Contains(ground) && f.DefId != "car");
                    if (_moving is not null)
                    {
                        _rotation = _moving.Rotation;
                        Audio.Play("pop");
                        RebuildBuildPanel();
                    }
                }
                else if (PlacementRoom(_moving.Def, at) is { } target && Session.MoveFurniture(_moving, target, at, _rotation))
                {
                    _moving = null;
                    _renderer?.SetGhost(null, default, 0, true);
                    RebuildBuildPanel();
                }
                else
                {
                    Audio.Play("wrong", gain: 0.5f);
                }

                break;
            case BuildTool.Paint:
                if (house.RoomAt(ground) is { } paintRoom && Rooms.Get(paintRoom).Indoor && Session.PaintRoom(paintRoom, _paint))
                {
                    RebuildBuildPanel();
                }

                break;
            case BuildTool.Floor:
                if (house.RoomAt(ground) is { } floorRoom && Rooms.Get(floorRoom).Indoor && Session.SetFloor(floorRoom, _floor))
                {
                    RebuildBuildPanel();
                }

                break;
        }
    }

    private void BuildCancel()
    {
        _placing = null;
        _moving = null;
        _renderer?.SetGhost(null, default, 0, true);
        RebuildBuildPanel();
    }

    private void BuildRotate()
    {
        _rotation = (_rotation + 1) % 4;
        Audio.Play("click");
    }
}
