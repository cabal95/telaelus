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

    /// <summary>A level whose tier is open but whose research is not done: grayscale and dim, its research on hover.</summary>
    Locked
}

/// <summary>
/// The build menu (B): a modal with a tier picker ("Tier 1" to "Tier 4") over a grid of the chosen
/// tier's buildings by name; Road and Bridge are tier 1 cells among them. A tier not yet unlocked is a
/// disabled cell whose tooltip names the research that unlocks it. The menu reopens on the last tier
/// viewed (the highest unlocked the first time). Hovering a cell shows its description and cost;
/// clicking it closes the menu and selects the building (or line tool) for placement, affordable or not.
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
    private SegmentedPicker _tierPicker = null!;
    private int _tier;
    private readonly Dictionary<LineTool, InfraCell> _infra = new();
    // Each building level's cell, by its id: the level's name without spaces ("DrillRig").
    private readonly Dictionary<string, Cell> _cells = new();
    private string _signature = "";

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "buildMenu";

    /// <summary>The chosen tier's cells in display order: type (a building's family, or a line tool), level, state, and label (for the snapshot).</summary>
    public IReadOnlyList<(string Type, int Level, BuildCellState State, string Label)> Cells => _order
        .Select(key => Enum.TryParse(key, out LineTool tool)
            ? (key, 1, _infra[tool].State, _infra[tool].Label.Text)
            : (_cells[key].Type.ToString(), _cells[key].Level, _cells[key].State, _cells[key].Label.Text))
        .ToList();

    /// <summary>A level's cell id: its name without spaces ("DrillRig").</summary>
    public static string CellId(BuildingType type, int level) => BuildingDefs.For(type, level).Name.Replace(" ", "");

    /// <summary>The tier shown, 1 to 4 (for the snapshot).</summary>
    public int Tier => _tier;

    /// <summary>The tier picker's cells (label, enabled, and never checked here), for the snapshot.</summary>
    public IReadOnlyList<(string Label, bool Enabled, bool Checked)> TierCells => Enumerable.Range(0, ResourceDefs.MaxTier)
        .Select(i => (TierLabel(i + 1), _tierPicker.IsEnabled(i), false))
        .ToList();

    // The shown cells' keys: a LineTool's or BuildingType's name.
    private List<string> _order = new();

    /// <summary>"Tier 2".</summary>
    public static string TierLabel(int tier) => "Tier " + tier;

    /// <summary>"Unlocked by Steel Works research": the tooltip on a locked tier's cell.</summary>
    public static string UnlockedBy(int tier)
    {
        ResearchProject project = ResearchDefs.All.First(p => ResearchDefs.For(p).Unlocks == tier);
        return $"Unlocked by {ResearchDefs.For(project).Name} research";
    }



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
        _tierPicker = new SegmentedPicker(Host, Id + "/tier")
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _tierPicker.Chosen += index =>
        {
            _tier = index + 1;
            Refresh();
        };
        Body.AddChild(_tierPicker);

        _grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _grid.AddThemeConstantOverride("h_separation", UiTheme.Gap);
        _grid.AddThemeConstantOverride("v_separation", UiTheme.Gap);
        Body.AddChild(_grid);

        foreach (LineTool tool in Enum.GetValues<LineTool>())
        {
            var cell = new InfraCell(this, tool);
            _infra[tool] = cell;
            _grid.AddChild(cell);
            Host.Register(Id + "/" + tool, cell);
        }
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels().Where(key => BuildingDefs.For(key.Type).Buildable))
        {
            var cell = new Cell(this, type, level);
            string id = CellId(type, level);
            _cells[id] = cell;
            _grid.AddChild(cell);
            Host.Register(Id + "/" + id, cell);
        }
    }

    public override void OnOpened()
    {
        base.OnOpened();
        if (_tier == 0)
        {
            _tier = _sim.UnlockedTier;
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

        _tierPicker.SetOptions(Enumerable.Range(1, ResourceDefs.MaxTier).Select(TierLabel).ToList(), inner);
        for (int tier = 1; tier <= ResourceDefs.MaxTier; tier++)
        {
            bool unlocked = tier <= _sim.UnlockedTier;
            _tierPicker.SetEnabled(tier - 1, unlocked, unlocked ? null : UnlockedBy(tier));
        }
        _tierPicker.Selected = _tier - 1;

        // A level whose research is not done still shows in its (open) tier, locked.
        var states = new Dictionary<string, BuildCellState>();
        foreach ((string id, Cell cell) in _cells.Where(p => BuildingDefs.For(p.Value.Type, p.Value.Level).Tier == _tier))
        {
            states[id] = !_sim.IsUnlocked(cell.Type, cell.Level) ? BuildCellState.Locked
                : _sim.CanAfford(cell.Type, cell.Level) ? BuildCellState.Affordable
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

        // Road and Bridge are tier 1 cells, sorted in with the buildings by name; locked levels follow.
        var shown = states.Keys.Select(id => (Key: id, Name: BuildingDefs.For(_cells[id].Type, _cells[id].Level).Name,
            Locked: states[id] == BuildCellState.Locked, Cell: (Control)_cells[id]));
        if (_tier == 1)
        {
            shown = shown.Concat(_infra.Select(p => (Key: p.Key.ToString(), Name: p.Value.Label.Text, Locked: false, Cell: (Control)p.Value)));
        }
        var ordered = shown.OrderBy(entry => entry.Locked).ThenBy(entry => entry.Name, StringComparer.Ordinal).ToList();
        _order = ordered.Select(entry => entry.Key).ToList();
        foreach (Control cell in _cells.Values.Concat<Control>(_infra.Values))
        {
            cell.Visible = false;
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Cell.Visible = true;
            _grid.MoveChild(ordered[i].Cell, i);
            if (ordered[i].Cell is Cell building)
            {
                building.Apply(states[ordered[i].Key], _pictures);
            }
        }
    }

    private string Signature(Dictionary<string, BuildCellState> states)
    {
        var text = new StringBuilder().Append(_tier).Append('|');
        foreach ((string id, BuildCellState state) in states)
        {
            text.Append(id).Append(':').Append(state).Append(';');
        }
        return text.ToString();
    }

    private void Pick(BuildingType type, int level)
    {
        if (!_sim.IsUnlocked(type, level))
        {
            return;
        }
        Host.Close(this);
        _builder.Select(type, level);
    }

    /// <summary>The tooltip lines for a building's cost at a level: one per material, short ones marked.</summary>
    public static List<TipLine> CostLines(Simulation sim, BuildingType type, int level = 1)
    {
        return CostLines(sim, BuildingDefs.For(type, level).Cost);
    }

    /// <summary>
    /// Why a level cannot be built yet, as tooltip lines: "Needs Steel Works research" while its tier is
    /// locked, "Needs Powered Mining research" while its project is not complete, and for each of that
    /// project's own unfinished prerequisites "Deep Mining needs Powered Mining". Empty once unlocked.
    /// </summary>
    public static List<TipLine> LockLines(Simulation sim, BuildingType type, int level)
    {
        var lines = new List<TipLine>();
        int tier = BuildingDefs.For(type, level).Tier;
        if (tier > sim.UnlockedTier)
        {
            ResearchProject opens = ResearchDefs.All.First(p => ResearchDefs.For(p).Unlocks == tier);
            lines.Add(new TipLine($"Needs {ResearchDefs.For(opens).Name} research", UiTheme.Warning));
        }
        if (level > 1 && ResearchDefs.UnlockingLevel(type, level) is ResearchProject project && !sim.IsResearched(project))
        {
            ResearchDef def = ResearchDefs.For(project);
            lines.Add(new TipLine($"Needs {def.Name} research", UiTheme.Warning));
            foreach (ResearchProject before in def.Prerequisites.Where(p => !sim.IsResearched(p)))
            {
                lines.Add(new TipLine($"{def.Name} needs {ResearchDefs.For(before).Name}", UiTheme.TextDim));
            }
        }
        return lines;
    }

    /// <summary>The tooltip lines for a list of costs: one per material, short ones marked.</summary>
    public static List<TipLine> CostLines(Simulation sim, IReadOnlyList<ResourceAmount> costs)
    {
        var lines = new List<TipLine>();
        foreach (ResourceAmount cost in costs)
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

    /// <summary>One building level's cell: its picture over its label, outlined on hover.</summary>
    private sealed partial class Cell : PanelContainer
    {
        private readonly BuildMenu _menu;
        private readonly TextureRect _picture;
        private readonly StyleBoxFlat _normal = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: 4);
        private readonly StyleBoxFlat _hover;

        public Label Label { get; }

        public BuildCellState State { get; private set; } = BuildCellState.Affordable;

        public BuildingType Type { get; }

        public int Level { get; }

        public Cell(BuildMenu menu, BuildingType type, int level)
        {
            _menu = menu;
            Type = type;
            Level = level;
            Name = CellId(type, level);
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
            Label.Text = BuildingDefs.For(Type, Level).Name;
            Label.AddThemeColorOverride("font_color", state == BuildCellState.Locked ? UiTheme.TextDim : UiTheme.Text);
            _picture.Texture = pictures.Color(Type, Level);
            _picture.Material = state == BuildCellState.Affordable ? null : BuildingPictures.GrayscaleMaterial;
            _picture.Modulate = state == BuildCellState.Locked ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
        }

        private void OnEnter()
        {
            AddThemeStyleboxOverride("panel", _hover);
            var lines = new List<TipLine>(TipLine.Wrapped(BuildingDefs.DescriptionOf(Type, Level), UiTheme.TextDim));
            lines.AddRange(State == BuildCellState.Locked ? LockLines(_menu._sim, Type, Level) : CostLines(_menu._sim, Type, Level));
            _menu.Host.ShowHover(this, lines);
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
                _menu.Pick(Type, Level);
            }
        }
    }
}
