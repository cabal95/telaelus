using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

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
/// The build menu (B): a modal with a grid of every building. Discovered
/// buildings come first, then undiscovered ones, each group by name. Hovering a
/// discovered cell shows its cost; clicking it closes the menu and selects the
/// building for placement, affordable or not.
/// </summary>
public partial class BuildMenu : Dialog
{
    public const float CellSize = 112f;
    private const float PictureHeight = 80f;

    private Simulation _sim = null!;
    private BuildController _builder = null!;
    private BuildingPictures _pictures = null!;
    private ResourceIcons _icons = null!;
    private GridContainer _grid = null!;
    private GridContainer _infraGrid = null!;
    private readonly Dictionary<LineTool, InfraCell> _infra = new();
    private readonly Dictionary<BuildingType, Cell> _cells = new();
    private string _signature = "";

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "buildMenu";

    /// <summary>The cells in display order, the Infrastructure tools then the buildings, with their state and label (for the snapshot).</summary>
    public IReadOnlyList<(string Type, BuildCellState State, string Label)> Cells => _infra
        .Select(p => (p.Key.ToString(), p.Value.State, p.Value.Label.Text))
        .Concat(_order.Select(type => (type.ToString(), _cells[type].State, _cells[type].Label.Text)))
        .ToList();

    private List<BuildingType> _order = new();

    public void Initialize(Simulation sim, BuildController builder, BuildingPictures pictures, ResourceIcons icons)
    {
        _sim = sim;
        _builder = builder;
        _pictures = pictures;
        _icons = icons;
        _pictures.PicturesReady += () => _signature = "";
    }

    protected override void Build()
    {
        // Roads and bridges first, under their own heading, so they are found without scrolling past
        // every building; then the buildings.
        Body.AddChild(Heading("Infrastructure"));
        _infraGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _infraGrid.AddThemeConstantOverride("h_separation", UiTheme.Gap);
        Body.AddChild(_infraGrid);
        foreach (LineTool tool in Enum.GetValues<LineTool>())
        {
            var cell = new InfraCell(this, tool);
            _infra[tool] = cell;
            _infraGrid.AddChild(cell);
            Host.Register(Id + "/" + tool, cell);
        }

        Body.AddChild(Heading("Buildings"));
        _grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _grid.AddThemeConstantOverride("h_separation", UiTheme.Gap);
        _grid.AddThemeConstantOverride("v_separation", UiTheme.Gap);
        Body.AddChild(_grid);

        foreach (BuildingType type in Enum.GetValues<BuildingType>().Where(type => BuildingDefs.For(type).Buildable))
        {
            var cell = new Cell(this, type);
            _cells[type] = cell;
            _grid.AddChild(cell);
            Host.Register(Id + "/" + type, cell);
        }
    }

    private static Label Heading(string text)
    {
        Label heading = UiTheme.MakeLabel(text, UiTheme.SmallSize, UiTheme.TextDim);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        return heading;
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

        foreach (InfraCell cell in _infra.Values)
        {
            cell.Refresh();
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

    private void PickLine(LineTool tool)
    {
        Host.Close(this);
        _builder.SelectLine(tool);
    }

    /// <summary>The cost of one tile of a line tool on plain ground (a road on Grass; a bridge).</summary>
    public static IReadOnlyList<ResourceAmount> TileCost(LineTool tool)
    {
        return tool == LineTool.Road
            ? new[] { new ResourceAmount(ResourceType.Stone, 4f) }
            : Simulation.BridgeTileCost;
    }

    /// <summary>A road or bridge cell: its picture over its label, in color when a tile is affordable, gray when not.</summary>
    private sealed partial class InfraCell : PanelContainer
    {
        private readonly BuildMenu _menu;
        private readonly LineTool _tool;
        private readonly TextureRect _picture;
        private readonly StyleBoxFlat _normal = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: 4);
        private readonly StyleBoxFlat _hover;

        public Label Label { get; }

        public BuildCellState State { get; private set; } = BuildCellState.Affordable;

        public InfraCell(BuildMenu menu, LineTool tool)
        {
            _menu = menu;
            _tool = tool;
            Name = tool.ToString();
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
            Label = UiTheme.MakeLabel(tool.ToString(), UiTheme.SmallSize);
            Label.HorizontalAlignment = HorizontalAlignment.Center;
            Label.MouseFilter = MouseFilterEnum.Ignore;
            column.AddChild(Label);
            MouseEntered += () =>
            {
                AddThemeStyleboxOverride("panel", _hover);
                _menu.Host.ShowHover(this, TipLines());
            };
            MouseExited += () =>
            {
                AddThemeStyleboxOverride("panel", _normal);
                _menu.Host.HideHover(this);
            };
            GuiInput += @event =>
            {
                if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    AcceptEvent();
                    _menu.PickLine(_tool);
                }
            };
        }

        public void Refresh()
        {
            _picture.Texture ??= _menu._icons.Icon(_tool == LineTool.Road ? "road" : "bridge");
            bool affordable = TileCost(_tool).All(c => HudOverlay.Whole(_menu._sim, c.Type) >= c.Amount);
            State = affordable ? BuildCellState.Affordable : BuildCellState.Unaffordable;
            _picture.Material = affordable ? null : BuildingPictures.GrayscaleMaterial;
        }

        private List<TipLine> TipLines()
        {
            return _tool == LineTool.Road
                ? new List<TipLine>
                {
                    new("Road: 2 cells wide, faster for trucks and you"),
                    new("1 Stone a cell, 2 on Sand, Snow, and Mud", UiTheme.TextDim)
                }
                : new List<TipLine>
                {
                    new("Bridge: a straight span over water"),
                    new("3 Wood and 1 Stone a cell, up to 12 cells", UiTheme.TextDim)
                };
        }
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
            // An undiscovered building keeps what it does and its cost a mystery.
            if (State != BuildCellState.Undiscovered)
            {
                var lines = new List<TipLine>(TipLine.Wrapped(BuildingDefs.DescriptionOf(_type), UiTheme.TextDim));
                lines.AddRange(CostLines(_menu._sim, _type));
                _menu.Host.ShowHover(this, lines);
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
