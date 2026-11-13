using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>How a building's cell in the build menu looks.</summary>
public enum BuildCellState
{
    /// <summary>Discovered, and the stockpile holds its cost: full color.</summary>
    Affordable,

    /// <summary>Discovered, but short of its cost right now: grayscale.</summary>
    Unaffordable,

    /// <summary>Not unlocked yet: an off-black silhouette labeled "Undiscovered".</summary>
    Undiscovered
}

/// <summary>
/// The build menu (B): a modal overlay with a grid of every building. Discovered
/// buildings come first, then undiscovered ones, each group by name. Hovering a
/// discovered cell shows its cost; clicking it closes the menu and selects the
/// building for placement, affordable or not.
/// </summary>
public partial class BuildMenu : Overlay
{
    public const float CellSize = 112f;
    private const float PictureHeight = 80f;

    private Simulation _sim = null!;
    private BuildController _builder = null!;
    private BuildingPictures _pictures = null!;
    private GridContainer _grid = null!;
    private readonly Dictionary<BuildingType, Cell> _cells = new();
    private string _signature = "";

    public override OverlayKind Kind => OverlayKind.Modal;

    public override string Id => "buildMenu";

    /// <summary>The cells in display order, with their state and label (for the snapshot).</summary>
    public IReadOnlyList<(BuildingType Type, BuildCellState State, string Label)> Cells => _order
        .Select(type => (type, _cells[type].State, _cells[type].Label.Text))
        .ToList();

    private List<BuildingType> _order = new();

    public void Initialize(Simulation sim, BuildController builder, BuildingPictures pictures)
    {
        _sim = sim;
        _builder = builder;
        _pictures = pictures;
        _pictures.PicturesReady += () => _signature = "";
    }

    protected override void Build()
    {
        _grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _grid.AddThemeConstantOverride("h_separation", UiTheme.Gap);
        _grid.AddThemeConstantOverride("v_separation", UiTheme.Gap);
        Body.AddChild(_grid);

        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            var cell = new Cell(this, type);
            _cells[type] = cell;
            _grid.AddChild(cell);
            Host.Register(Id + "/" + type, cell);
        }
    }

    public override void OnKey(Key key)
    {
        if (key == Key.B)
        {
            Host.Close(this);
        }
    }

    public override void Refresh()
    {
        // Columns: as many cells as fit the panel's inner width.
        float inner = ModalWidth(Host.UiSize) - 2 * UiTheme.Padding;
        _grid.Columns = Math.Max(1, (int)((inner + UiTheme.Gap) / (CellSize + UiTheme.Gap)));

        var states = new Dictionary<BuildingType, BuildCellState>();
        foreach (BuildingType type in _cells.Keys)
        {
            states[type] = !_sim.IsUnlocked(type) ? BuildCellState.Undiscovered
                : _sim.CanAfford(type) ? BuildCellState.Affordable
                : BuildCellState.Unaffordable;
        }

        string signature = Signature(states);
        if (signature == _signature)
        {
            return;
        }
        _signature = signature;

        _order = states.Keys
            .OrderBy(type => states[type] == BuildCellState.Undiscovered ? 1 : 0)
            .ThenBy(type => BuildingDefs.For(type).Name, StringComparer.Ordinal)
            .ToList();
        for (int i = 0; i < _order.Count; i++)
        {
            Cell cell = _cells[_order[i]];
            _grid.MoveChild(cell, i);
            cell.Apply(states[_order[i]], _pictures);
        }
    }

    private static string Signature(Dictionary<BuildingType, BuildCellState> states)
    {
        var text = new StringBuilder();
        foreach ((BuildingType type, BuildCellState state) in states)
        {
            text.Append(type).Append(':').Append(state).Append(';');
        }
        return text.ToString();
    }

    private void Pick(BuildingType type)
    {
        if (!_sim.IsUnlocked(type))
        {
            return;
        }
        Host.Close(this);
        _builder.Select(type);
    }

    /// <summary>The tooltip lines for a building's cost: one per material, short ones marked.</summary>
    public static List<TipLine> CostLines(Simulation sim, BuildingType type)
    {
        var lines = new List<TipLine>();
        foreach (ResourceAmount cost in BuildingDefs.For(type).Cost)
        {
            sim.Stockpile.TryGetValue(cost.Type, out float have);
            int held = Mathf.FloorToInt(have);
            string name = ResourceDefs.For(cost.Type).Name;
            lines.Add(held < cost.Amount
                ? new TipLine($"{(int)cost.Amount} {name} (have {held})", UiTheme.Short)
                : new TipLine($"{(int)cost.Amount} {name}"));
        }
        return lines;
    }

    /// <summary>One building's cell: its picture over its label, outlined on hover.</summary>
    private sealed partial class Cell : PanelContainer
    {
        private readonly BuildMenu _menu;
        private readonly BuildingType _type;
        private readonly TextureRect _picture;
        private readonly StyleBoxFlat _normal = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: 4);
        private readonly StyleBoxFlat _hover;

        public Label Label { get; }

        public BuildCellState State { get; private set; } = BuildCellState.Undiscovered;

        public Cell(BuildMenu menu, BuildingType type)
        {
            _menu = menu;
            _type = type;
            Name = type.ToString();
            CustomMinimumSize = new Vector2(CellSize, CellSize);
            MouseFilter = MouseFilterEnum.Stop;
            _hover = (StyleBoxFlat)_normal.Duplicate();
            _hover.BgColor = UiTheme.CellHover;
            _hover.BorderColor = UiTheme.Accent;
            _hover.SetBorderWidthAll(UiTheme.OutlineWidth);
            AddThemeStyleboxOverride("panel", _normal);

            var column = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            column.AddThemeConstantOverride("separation", 2);
            AddChild(column);
            _picture = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0f, PictureHeight),
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            column.AddChild(_picture);
            Label = UiTheme.MakeLabel("", UiTheme.SmallSize);
            Label.HorizontalAlignment = HorizontalAlignment.Center;
            Label.MouseFilter = MouseFilterEnum.Ignore;
            column.AddChild(Label);

            MouseEntered += OnEnter;
            MouseExited += OnExit;
            GuiInput += OnInput;
        }

        public void Apply(BuildCellState state, BuildingPictures pictures)
        {
            State = state;
            bool discovered = state != BuildCellState.Undiscovered;
            Label.Text = discovered ? BuildingDefs.For(_type).Name : "Undiscovered";
            Label.AddThemeColorOverride("font_color", discovered ? UiTheme.Text : UiTheme.TextDim);
            _picture.Texture = discovered ? pictures.Color(_type) : pictures.Silhouette(_type);
            _picture.Material = state == BuildCellState.Unaffordable ? BuildingPictures.GrayscaleMaterial : null;
        }

        private void OnEnter()
        {
            AddThemeStyleboxOverride("panel", _hover);
            // An undiscovered building keeps its cost a mystery.
            if (State != BuildCellState.Undiscovered)
            {
                _menu.Host.ShowHover(this, CostLines(_menu._sim, _type));
            }
        }

        private void OnExit()
        {
            AddThemeStyleboxOverride("panel", _normal);
            _menu.Host.HideHover(this);
        }

        private void OnInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                AcceptEvent();
                _menu.Pick(_type);
            }
        }
    }
}
