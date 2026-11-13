using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The detail panel for a placed building: its name as the title, a turning
/// picture of it, its status rows (power, state, fields in range, what it is working
/// on, its recipe, and its progress), and its actions (turn off or on, destroy with a
/// confirming second click, and close). The rows refresh every frame; the panel closes
/// itself when its building is gone.
/// </summary>
public partial class BuildingDetail : Dialog
{
    /// <summary>How long the Destroy button waits for its confirming click.</summary>
    public const double ConfirmSeconds = 3.0;

    private Simulation _sim = null!;
    private TurntableView _turntable = null!;
    private GridContainer _rows = null!;
    private ProgressBar _progress = null!;
    private Label _progressLabel = null!;
    private Button _toggle = null!;
    private Button _destroy = null!;
    private double _confirmLeft;
    private readonly Dictionary<string, Label> _values = new();
    private readonly Dictionary<string, Label> _names = new();

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "buildingDetail";

    /// <summary>The building shown, or null.</summary>
    public Building? Building { get; private set; }

    /// <summary>The status rows as shown (label to value), for the snapshot.</summary>
    public IReadOnlyDictionary<string, string> Rows => _values
        .Where(entry => entry.Value.Visible)
        .ToDictionary(entry => entry.Key, entry => entry.Value.Text);

    /// <summary>The Destroy button's text (it reads "Confirm destroy" while waiting).</summary>
    public string DestroyText => _destroy.Text;

    public float TurnDegrees => _turntable.TurnDegrees;

    public void Initialize(Simulation sim)
    {
        _sim = sim;
    }

    protected override void Build()
    {
        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        Body.AddChild(center);
        _turntable = new TurntableView();
        center.AddChild(_turntable);

        _rows = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _rows.AddThemeConstantOverride("h_separation", UiTheme.Padding);
        _rows.AddThemeConstantOverride("v_separation", 2);
        Body.AddChild(_rows);
        foreach (string row in new[] { "Power", "State", "In range", "Working on", "Recipe" })
        {
            Label name = UiTheme.MakeLabel(row, UiTheme.SmallSize, UiTheme.TextDim);
            Label value = UiTheme.MakeLabel("", UiTheme.SmallSize);
            value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _rows.AddChild(name);
            _rows.AddChild(value);
            _names[row] = name;
            _values[row] = value;
        }

        _progressLabel = UiTheme.MakeLabel("Progress", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_progressLabel);
        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 8f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _progress.AddThemeStyleboxOverride("background", UiTheme.Box(UiTheme.Cell, 3));
        _progress.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTheme.Accent, 3));
        Body.AddChild(_progress);

        Body.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, UiTheme.Gap),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _toggle = new Button
        {
            Name = "Toggle"
        };
        _toggle.Pressed += OnToggle;
        Body.AddChild(_toggle);
        Host.Register(Id + "/toggle", _toggle);

        _destroy = new Button
        {
            Name = "Destroy"
        };
        StyleBoxFlat danger = UiTheme.Box(UiTheme.Danger, UiTheme.CellRadius, padding: UiTheme.Gap);
        StyleBoxFlat dangerHover = (StyleBoxFlat)danger.Duplicate();
        dangerHover.BgColor = UiTheme.Danger.Lightened(0.15f);
        dangerHover.BorderColor = UiTheme.Accent;
        dangerHover.SetBorderWidthAll(UiTheme.OutlineWidth);
        _destroy.AddThemeStyleboxOverride("normal", danger);
        _destroy.AddThemeStyleboxOverride("hover", dangerHover);
        _destroy.AddThemeStyleboxOverride("pressed", danger);
        _destroy.Pressed += OnDestroy;
        Body.AddChild(_destroy);
        Host.Register(Id + "/destroy", _destroy);
    }

    /// <summary>Shows a building (opening the panel if needed).</summary>
    public void ShowBuilding(Building building)
    {
        Building = building;
        _confirmLeft = 0;
        TitleLabel.Text = building.Def.Name;
        _turntable.Show(building.Type);
        Host.Open(this);
        Refresh();
    }

    public override void OnClosed()
    {
        Building = null;
        _confirmLeft = 0;
        base.OnClosed();
    }

    public override void Refresh()
    {
        if (Building is not Building building || !_sim.Buildings.Contains(building))
        {
            Host.Close(this);
            return;
        }

        BuildingDef def = building.Def;
        BuildingStatus status = _sim.BuildingStatusOf(building);
        Set("Power", building.Enabled ? "On" : "Off");
        Set("State", status.State == BuildingState.Working ? "Working" : "Idle: " + status.IdleReason);

        float progress = 0f;
        if (def.IsExtractor)
        {
            List<ResourceField> fields = _sim.FieldsInRange(building.Position, def.WorkRadius, def.Works);
            string inRange = fields.Count == 0
                ? "none"
                : string.Join(", ", def.Works
                    .Select(type => (type, inType: fields.Where(f => f.Type == type).ToList()))
                    .Where(entry => entry.inType.Count > 0)
                    // The field count, then what those fields hold in all.
                    .Select(entry => $"{ResourceDefs.For(entry.type).Name} {entry.inType.Count}"
                        + $" ({Mathf.CeilToInt(entry.inType.Sum(f => f.Amount))})"));
            Set("In range", inRange);

            ResourceField? field = building.CurrentField is { Depleted: false } current ? current : null;
            // Only a building that works more than one type says which it is on.
            Set("Working on", def.Works.Count > 1
                ? field is null ? "nothing" : $"{ResourceDefs.For(field.Type).Name} ({Mathf.CeilToInt(field.Amount)} left)"
                : null);
            if (field is not null && ResourceDefs.For(field.Type).GatherSeconds is float seconds)
            {
                progress = building.Progress / (seconds / def.Speed);
            }
            Set("Recipe", null);
        }
        else
        {
            Set("In range", null);
            Set("Working on", null);
            Recipe? recipe = building.Recipe;
            Set("Recipe", recipe is null ? null : RecipeText(recipe));
            if (recipe is not null && building.Crafting)
            {
                progress = building.Progress / recipe.CycleTime;
            }
        }
        _progress.Value = Mathf.Clamp(progress, 0f, 1f);

        _toggle.Text = building.Enabled ? "Turn off" : "Turn on";
        _destroy.Text = _confirmLeft > 0 ? "Confirm destroy" : "Destroy (refund " + RefundText(building) + ")";
    }

    public override void _Process(double delta)
    {
        if (_confirmLeft > 0)
        {
            _confirmLeft -= delta;
        }
    }

    private void Set(string row, string? value)
    {
        bool shown = value is not null;
        _names[row].Visible = shown;
        _values[row].Visible = shown;
        _values[row].Text = value ?? "";
    }

    private static string RecipeText(Recipe recipe)
    {
        static string Amounts(IEnumerable<ResourceAmount> amounts) =>
            string.Join(" + ", amounts.Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
        return $"{Amounts(recipe.Inputs)} to {Amounts(recipe.Outputs)}, {recipe.CycleTime:0.#} s";
    }

    private string RefundText(Building building)
    {
        return string.Join(", ", _sim.DestroyRefund(building)
            .Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
    }

    private void OnToggle()
    {
        if (Building is Building building)
        {
            _sim.SetBuildingEnabled(building.Id, !building.Enabled);
        }
    }

    /// <summary>The first click arms the button for a few seconds; a second click in that time destroys.</summary>
    private void OnDestroy()
    {
        if (Building is not Building building)
        {
            return;
        }
        if (_confirmLeft <= 0)
        {
            _confirmLeft = ConfirmSeconds;
            return;
        }
        _sim.TryDestroyBuilding(building.Id);
        Host.Close(this);
    }
}
