using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The detail panel for a placed building: its name as the title, a turning
/// picture of it, its status rows (power, state, fields in range, what it is working
/// on, its recipe, and its progress), for a storage building what it adds to storage
/// (a Silo also picks its resource), and its actions (turn off or on, destroy with a
/// confirming second click, and close; a storage building has no power to switch, and a
/// permanent building, the HQ, cannot be destroyed). The
/// rows refresh every frame; the panel closes itself when its building is gone.
/// </summary>
public partial class BuildingDetail : Dialog
{
    /// <summary>How long the Destroy button waits for its confirming click.</summary>
    public const double ConfirmSeconds = 3.0;

    private Simulation _sim = null!;
    private TurntableView _turntable = null!;
    private StatusRows _rows = null!;
    private ProgressBar _progress = null!;
    private Label _progressLabel = null!;
    private Button _toggle = null!;
    private Button _destroy = null!;
    private Label _addsLabel = null!;
    private GridContainer _adds = null!;
    private Label _siloLabel = null!;
    private OptionButton _silo = null!;
    private Label _fuelLabel = null!;
    private OptionButton _fuel = null!;
    private Label _fuelNote = null!;
    private Button _buyTruck = null!;
    private Label _outputLabel = null!;
    private OptionButton _output = null!;
    private string _outputSignature = "";
    private List<ResourceType> _outputChoices = new();
    private Label _buyNote = null!;
    private string _addsSignature = "";

    /// <summary>The Bulk resources a Silo can hold, in table order (the picker's items).</summary>
    private static readonly ResourceType[] SiloChoices = System.Enum.GetValues<ResourceType>()
        .Where(type => ResourceDefs.For(type).Storage.Class == StorageClass.Bulk)
        .ToArray();
    private double _confirmLeft;

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "buildingDetail";

    /// <summary>The building shown, or null.</summary>
    public Building? Building { get; private set; }

    /// <summary>The status rows as shown (label to value), for the snapshot.</summary>
    public IReadOnlyDictionary<string, string> Rows => _rows.Shown;

    /// <summary>The fuel picker's choice as shown ("Best", "Coal"), or null when hidden.</summary>
    public string? FuelText => _fuel.Visible ? _fuel.GetItemText(_fuel.Selected) : null;

    /// <summary>The Destroy button's text (it reads "Confirm destroy" while waiting), or null when hidden.</summary>
    public string? DestroyText => _destroy.Visible ? _destroy.Text : null;

    /// <summary>The Buy truck button's text and whether it can be pressed, or null when hidden (not a Depot).</summary>
    /// <summary>The Output picker's choice as shown ("Iron Ore, every 5 s"), or null when hidden.</summary>
    public string? OutputText => _output.Visible && _output.Selected >= 0 ? _output.GetItemText(_output.Selected) : null;

    public (string Text, bool Enabled)? BuyTruck => _buyTruck.Visible ? (_buyTruck.Text, !_buyTruck.Disabled) : null;

    /// <summary>What a storage building adds, as shown ("Wood", "+200"), for the snapshot.</summary>
    public IEnumerable<(string Resource, string Amount)> Adds => _adds.GetChildren().OfType<Label>()
        .Chunk(2)
        .Select(pair => (pair[0].Text, pair[1].Text));

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

        _rows = new StatusRows("Network", "Switch", "Power", "Water", "Sand", "State", "Site", "Plot", "Eats", "In range", "Working on", "Recipe", "Buffer", "Truck", "Burning", "Links", "Reach",
            "Trucks", "Truck 1", "Truck 2", "Truck 3", "Truck 4");
        Body.AddChild(_rows);

        _progressLabel = UiTheme.MakeLabel("Progress", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_progressLabel);
        _progress = StatusRows.MakeProgressBar();
        Body.AddChild(_progress);

        _siloLabel = UiTheme.MakeLabel("Holds", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_siloLabel);
        _silo = new OptionButton
        {
            Name = "SiloResource",
            FocusMode = FocusModeEnum.None
        };
        foreach (ResourceType type in SiloChoices)
        {
            _silo.AddItem(ResourceDefs.For(type).Name);
        }
        _silo.ItemSelected += OnSiloSelected;
        Body.AddChild(_silo);
        Host.Register(Id + "/siloResource", _silo);

        _fuelLabel = UiTheme.MakeLabel("Fuel", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_fuelLabel);
        _fuel = new OptionButton
        {
            Name = "Fuel",
            FocusMode = FocusModeEnum.None
        };
        _fuel.AddItem("Best");
        foreach (ResourceType fuel in ResourceDefs.Fuels)
        {
            _fuel.AddItem(ResourceDefs.For(fuel).Name);
        }
        _fuel.ItemSelected += OnFuelSelected;
        Body.AddChild(_fuel);
        Host.Register(Id + "/fuel", _fuel);
        _fuelNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_fuelNote);

        _outputLabel = UiTheme.MakeLabel("Output", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_outputLabel);
        _output = new OptionButton
        {
            Name = "Output",
            FocusMode = FocusModeEnum.None
        };
        _output.ItemSelected += OnOutputSelected;
        Body.AddChild(_output);
        Host.Register(Id + "/output", _output);

        _buyTruck = new Button
        {
            Name = "BuyTruck",
            FocusMode = FocusModeEnum.None
        };
        _buyTruck.Pressed += OnBuyTruck;
        Body.AddChild(_buyTruck);
        Host.Register(Id + "/buyTruck", _buyTruck);
        _buyNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.Short);
        Body.AddChild(_buyNote);

        _addsLabel = UiTheme.MakeLabel("Adds to storage", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_addsLabel);
        _adds = new GridContainer
        {
            Columns = 2,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _adds.AddThemeConstantOverride("h_separation", UiTheme.Padding);
        _adds.AddThemeConstantOverride("v_separation", 2);
        Body.AddChild(_adds);

        Body.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, UiTheme.Gap),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _toggle = new Button
        {
            Name = "Toggle",
            FocusMode = FocusModeEnum.None
        };
        _toggle.Pressed += OnToggle;
        Body.AddChild(_toggle);
        Host.Register(Id + "/toggle", _toggle);

        _destroy = new Button
        {
            Name = "Destroy",
            FocusMode = FocusModeEnum.None
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
        _addsSignature = "";
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
        bool storage = def.Storage != StorageRole.None;
        _rows.Set("Switch", storage ? null : building.Enabled ? "On" : "Off");
        foreach (CapacityType capacity in Simulation.CapacityTypes)
        {
            _rows.Set(capacity.ToString(), CapacityRowText(building, capacity));
        }
        _rows.Set("Burning", BurningText(building));
        RefreshFuel(building);
        bool works = !storage && !def.IsHub;
        _rows.Set("State", works ? status.State == BuildingState.Working ? "Working" : "Idle: " + status.IdleReason : null);
        RefreshStorage(building);
        _rows.Set("Network", building.Online ? "Online" : "Offline");
        _rows.Set("Buffer", BufferText(building));
        _rows.Set("Truck", TruckText.CollectorRow(_sim, building));
        RefreshTrucks(building);
        RefreshRenewables(building);
        _rows.Set("Links", def.IsHub ? _sim.LinkedHubs(building).Count.ToString() : null);
        _rows.Set("Reach", def.IsHub ? $"covers {def.CoverageRadius:0}, links {def.LinkRadius:0}" : null);

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
            _rows.Set("In range", inRange);

            ResourceField? field = building.CurrentField is { Depleted: false } current ? current : null;
            // Only a building that works more than one type says which it is on.
            _rows.Set("Working on", def.Works.Count > 1
                ? field is null ? "nothing" : $"{ResourceDefs.For(field.Type).Name} ({Mathf.CeilToInt(field.Amount)} left)"
                : null);
            if (field is not null && ResourceDefs.For(field.Type).GatherSeconds is float seconds)
            {
                progress = building.Progress / (seconds / def.Speed);
            }
            _rows.Set("Recipe", null);
        }
        else
        {
            _rows.Set("In range", null);
            _rows.Set("Working on", null);
            Recipe? recipe = building.Recipe;
            _rows.Set("Recipe", recipe is null ? null : RecipeText(recipe));
            if (recipe is not null && building.Crafting)
            {
                progress = building.Progress / recipe.CycleTime;
            }
        }
        if (def.ProduceSeconds > 0f)
        {
            progress = building.Progress / _sim.UnitSeconds(building);
        }
        _progress.Value = Mathf.Clamp(progress, 0f, 1f);
        bool hasProgress = works && (def.IsCollector || building.Recipe is not null);
        _progressLabel.Visible = hasProgress;
        _progress.Visible = hasProgress;

        _toggle.Visible = !storage;
        _destroy.Visible = !def.Permanent;
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

    /// <summary>
    /// The renewables' rows and picker: a Forester's Plot ("Growing: 2:10 left", "Grown: 112 / 150
    /// Wood", "Replanting in 18 s"), a deep building's Site, what an eating provider Eats, and the
    /// Output picker of a collector with a choice (the Quarry, a building on a motherlode).
    /// </summary>
    private void RefreshRenewables(Building building)
    {
        BuildingDef def = building.Def;
        _rows.Set("Plot", def.Plot is ({ } plot) ? PlotText(building, plot.Amount) : null);
        _rows.Set("Site", building.SiteMotherlode is { Motherlode: MotherlodeType kind }
            ? MotherlodeDefs.For(kind).Name + " motherlode"
            : building.SiteDeposit is ResourceType deposit ? "Worked-out " + ResourceDefs.For(deposit).Name : null);
        if (def.Eats is (ResourceType food, float seconds))
        {
            _sim.Stockpile.TryGetValue(food, out float stock);
            _rows.Set("Eats", $"1 {ResourceDefs.For(food).Name} every {seconds:0} s; {Mathf.FloorToInt(stock)} in storage");
        }
        else
        {
            _rows.Set("Eats", null);
        }

        _outputChoices = def.ProduceSeconds > 0f ? _sim.OutputChoices(building).ToList() : new List<ResourceType>();
        bool show = _outputChoices.Count > 0;
        _outputLabel.Visible = show;
        _output.Visible = show;
        if (!show)
        {
            _outputSignature = "";
            return;
        }
        var labels = _outputChoices.Select(type => $"{ResourceDefs.For(type).Name}, every {RateSeconds(building, type)} s").ToList();
        string signature = string.Join("|", labels);
        if (signature != _outputSignature)
        {
            _outputSignature = signature;
            _output.Clear();
            foreach (string label in labels)
            {
                _output.AddItem(label);
            }
        }
        _output.Selected = _sim.OutputOf(building) is ResourceType current ? _outputChoices.IndexOf(current) : 0;
        _output.Disabled = _outputChoices.Count < 2;
    }

    /// <summary>Seconds a unit of one output takes at a collector, as shown ("5", "7.5", "26.7").</summary>
    private string RateSeconds(Building building, ResourceType type)
    {
        float seconds = building.SiteMotherlode is { Motherlode: MotherlodeType kind }
            ? MotherlodeDefs.For(kind).UnitSeconds(type)
            : building.Def.ProduceSeconds;
        return seconds.ToString("0.##");
    }

    private static string PlotText(Building forester, float amount)
    {
        if (forester.Plot is not ResourceField field)
        {
            return $"Replanting in {Mathf.CeilToInt(forester.PlotWait)} s";
        }
        if (field.Growing)
        {
            int left = Mathf.CeilToInt(field.GrowLeft);
            return $"Growing: {left / 60}:{left % 60:00} left";
        }
        return $"Grown: {Mathf.CeilToInt(field.Amount)} / {amount:0} Wood";
    }

    private void OnOutputSelected(long index)
    {
        if (Building is Building building && index >= 0 && index < _outputChoices.Count)
        {
            _sim.SetOutputChoice(building.Id, _outputChoices[(int)index]);
        }
    }

    /// <summary>A Depot's Trucks row, a row per truck with its state, and the Buy truck button with what it is short of.</summary>
    private void RefreshTrucks(Building building)
    {
        int slots = building.Def.TruckSlots;
        var trucks = slots > 0 ? _sim.TrucksOf(building) : new List<Truck>();
        _rows.Set("Trucks", slots > 0 ? $"{trucks.Count} / {slots}" : null);
        for (int i = 0; i < 4; i++)
        {
            _rows.Set($"Truck {i + 1}", i < trucks.Count ? TruckText.State(trucks[i]) : null);
        }
        _buyTruck.Visible = slots > 0;
        _buyNote.Visible = false;
        if (slots <= 0)
        {
            return;
        }
        _buyTruck.Text = "Buy truck (" + string.Join(", ", TruckDefs.Cost.Select(c => $"{(int)c.Amount} {ResourceDefs.For(c.Type).Name}")) + ")";
        _buyTruck.Disabled = !_sim.CanBuyTruck(building);
        string? note = trucks.Count >= slots
            ? "Every bay is full"
            : string.Join(", ", TruckDefs.Cost
                .Where(c => (_sim.Stockpile.TryGetValue(c.Type, out float have) ? have : 0f) < c.Amount)
                .Select(c => $"{(int)c.Amount} {ResourceDefs.For(c.Type).Name} (have {Mathf.FloorToInt(_sim.Stockpile.TryGetValue(c.Type, out float have) ? have : 0f)})"));
        _buyNote.Visible = !string.IsNullOrEmpty(note);
        _buyNote.Text = note ?? "";
        _buyNote.AddThemeColorOverride("font_color", trucks.Count >= slots ? UiTheme.TextDim : UiTheme.Short);
    }

    private void OnBuyTruck()
    {
        if (Building is Building building)
        {
            _sim.TryBuyTruck(building.Id);
        }
    }

    /// <summary>The Silo picker, and the rows of what a storage building adds (rebuilt only when they change).</summary>
    private void RefreshStorage(Building building)
    {
        bool silo = building.Def.Storage == StorageRole.Silo;
        _siloLabel.Visible = silo;
        _silo.Visible = silo;
        if (silo)
        {
            _silo.Selected = System.Array.IndexOf(SiloChoices, building.SiloResource);
        }

        var lines = Simulation.StorageAddedBy(building)
            .Select(a => (ResourceDefs.For(a.Type).Name, $"+{a.Amount:0}"))
            .ToList();
        if (building.Def.CargoPool > 0f)
        {
            lines.Add(("Cargo pool", $"+{building.Def.CargoPool:0}"));
        }
        string signature = string.Join("|", lines) + building.Online;
        _addsLabel.Visible = lines.Count > 0;
        _addsLabel.Text = building.Online ? "Adds to storage" : "Offline: adds nothing until connected";
        if (signature == _addsSignature)
        {
            return;
        }
        _addsSignature = signature;
        foreach (Node child in _adds.GetChildren())
        {
            _adds.RemoveChild(child);
            child.QueueFree();
        }
        foreach ((string name, string amount) in lines)
        {
            _adds.AddChild(UiTheme.MakeLabel(name, UiTheme.SmallSize));
            _adds.AddChild(UiTheme.MakeLabel(amount, UiTheme.SmallSize));
        }
    }

    /// <summary>
    /// The buffer row: an extractor's output ("Iron Ore 12, Coal 3 / 20"), a processor's output and
    /// inputs ("Iron 4 / 10; in: Iron Ore 3 / 5"), or null for a building without one.
    /// </summary>
    private static string? BufferText(Building building)
    {
        BuildingDef def = building.Def;
        if (def.OutputBuffer <= 0f)
        {
            return null;
        }
        string output = building.OutputCount <= 0f
            ? "empty"
            : string.Join(", ", building.Output.Select(e => $"{ResourceDefs.For(e.Key).Name} {Mathf.FloorToInt(e.Value)}"));
        string text = $"{output} / {def.OutputBuffer:0}";
        if (def.InputCycles > 0 && building.Recipe is Recipe recipe)
        {
            text += "; in: " + string.Join(", ", recipe.Inputs.Select(input =>
            {
                building.Input.TryGetValue(input.Type, out float held);
                return $"{ResourceDefs.For(input.Type).Name} {Mathf.FloorToInt(held)} / {input.Amount * def.InputCycles:0}";
            }));
        }
        return text;
    }

    /// <summary>
    /// A capacity row (Power, Water, Sand): what a consumer draws and how fast that type runs, or
    /// what a provider supplies (less when its own draws slow it), or null when it neither.
    /// </summary>
    private string? CapacityRowText(Building building, CapacityType capacity)
    {
        BuildingDef def = building.Def;
        float draw = def.Draw(capacity);
        if (draw > 0f)
        {
            if (!building.Online)
            {
                return $"No {capacity}: offline";
            }
            CapacityReading reading = _sim.Capacity(capacity);
            return reading.State switch
            {
                CapacityState.Blackout => $"Draws {draw:0}: blackout",
                CapacityState.Brownout => $"Draws {draw:0} (running at {reading.Speed * 100f:0}%)",
                _ => $"Draws {draw:0}"
            };
        }
        float supply = def.Supply(capacity);
        if (supply > 0f)
        {
            if (!building.Online)
            {
                return "Offline: supplies nothing";
            }
            float speed = _sim.WorkSpeed(building);
            return speed < 1f ? $"Supplies {supply * speed:0.#} of {supply:0}" : $"Supplies {supply:0}";
        }
        return null;
    }

    /// <summary>The Burning row for a building with a fuel slot: what burns and for how long, and what waits in the slot.</summary>
    private static string? BurningText(Building building)
    {
        if (!building.Def.BurnsFuel)
        {
            return null;
        }
        float slot = building.FuelSlot.Values.Sum();
        string burning = building.Burning is ResourceType fuel && building.BurnLeft > 0f
            ? $"{ResourceDefs.For(fuel).Name}, {Mathf.CeilToInt(building.BurnLeft)} s left"
            : "nothing";
        return $"{burning}; {Mathf.FloorToInt(slot)} in slot";
    }

    /// <summary>The fuel picker and its note naming what Best burns now.</summary>
    private void RefreshFuel(Building building)
    {
        bool burns = building.Def.BurnsFuel;
        _fuelLabel.Visible = burns;
        _fuel.Visible = burns;
        _fuelNote.Visible = burns;
        if (!burns)
        {
            return;
        }
        _fuel.Selected = building.FuelChoice is ResourceType chosen ? 1 + ResourceDefs.Fuels.ToList().IndexOf(chosen) : 0;
        _fuelNote.Text = building.FuelChoice is null
            ? _sim.BestFuelAt(building) is ResourceType best
                ? $"Best: burns {ResourceDefs.For(best).Name} ({ResourceDefs.For(best).BurnSeconds:0} s per unit)"
                : "Best: no fuel at hand"
            : $"Burns only {ResourceDefs.For(building.FuelChoice.Value).Name}";
    }

    private void OnFuelSelected(long index)
    {
        if (Building is Building building)
        {
            _sim.SetFuelChoice(building.Id, index == 0 ? null : ResourceDefs.Fuels[(int)index - 1]);
        }
    }

    private void OnSiloSelected(long index)
    {
        if (Building is Building building)
        {
            _sim.SetSiloResource(building.Id, SiloChoices[index]);
        }
    }

    private static string RecipeText(Recipe recipe)
    {
        static string Amounts(IEnumerable<ResourceAmount> amounts) =>
            string.Join(" + ", amounts.Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
        string makes = recipe.Inputs.Count == 0 ? Amounts(recipe.Outputs) : $"{Amounts(recipe.Inputs)} to {Amounts(recipe.Outputs)}";
        return $"{makes}, {recipe.CycleTime:0.#} s";
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
