using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

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
    private IReadOnlyList<ResourceType> _fuels = System.Array.Empty<ResourceType>();
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
    private Button _sellTruck = null!;
    private ResourceIcons _icons = null!;
    // The Shipyard's Ship launch section: its heading, a row per bill entry, the button, why it is off, and the ships launched.
    private VBoxContainer _launchBox = null!;
    private readonly List<(ResourceType Type, float Need, TextureRect Icon, Label Amount)> _billRows = new();
    private Button _launch = null!;
    private Label _launchNote = null!;
    private Label _shipsLaunched = null!;
    private Button _openResearch = null!;

    /// <summary>Raised when the lab's button asks for the research tree.</summary>
    public event System.Action? ResearchRequested;
    private Label _recipeLabel = null!;
    private OptionButton _recipe = null!;
    private RichTextLabel _recipeNeeds = null!;
    private string _recipeSignature = "";
    // A queueing processor's order controls: the recipe the picker has chosen to queue, the quantity
    // stepper and Craft, and a row per order with a button that removes it.
    private int _selected;
    private int _quantity;
    private HBoxContainer _orderRow = null!;
    private Button _less = null!;
    private Label _quantityLabel = null!;
    private Button _more = null!;
    private Button _craft = null!;
    private Label _queueLabel = null!;
    private VBoxContainer _queue = null!;
    private string _queueSignature = "";
    private int _queueRows;
    private Label _inputLabel = null!;
    private OptionButton _input = null!;
    private Label _inputNote = null!;
    private string _inputSignature = "";
    private int _inputIndex = -1;
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
    private bool _destroyHovered;

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

    /// <summary>The Destroy button's tooltip ("Refund: 13 Wood, 8 Stone").</summary>
    public string DestroyTip { get; private set; } = "";

    /// <summary>The Buy truck button's text and whether it can be pressed, or null when hidden (not a Depot).</summary>
    /// <summary>The quantity to queue as shown ("5"), and whether Craft can be pressed; null when hidden.</summary>
    public (string Quantity, bool CanCraft)? Order => _orderRow.Visible ? (_quantityLabel.Text, !_craft.Disabled) : null;

    /// <summary>The queue's rows as shown ("Iron x 5 (crafting)"), or null when hidden; empty when the queue is.</summary>
    public IReadOnlyList<string>? QueueRows => _queue.Visible
        ? _queue.GetChildren().Select(row => row is Label label ? label.Text : row.GetChild<Label>(0).Text).ToList()
        : null;

    /// <summary>The Recipe picker's choice as shown ("Copper"), or null when hidden.</summary>
    public string? RecipeChoiceText => _recipe.Visible && _recipe.Selected >= 0 ? _recipe.GetItemText(_recipe.Selected) : null;

    /// <summary>
    /// The line under the Recipe picker ("2 Iron Ore + 1 Coal to 1 Iron, 16 s") and the inputs it
    /// shows short (storage holds less than a cycle's worth), or null while the picker is hidden.
    /// </summary>
    public (string Text, IReadOnlyList<string> Short)? RecipeNeeds => _recipeNeeds.Visible ? (_recipeNeeds.GetParsedText(), _recipeShort) : null;

    private IReadOnlyList<string> _recipeShort = new List<string>();

    /// <summary>The input picker's choice and note as shown ("Auto", "Auto: uses Coal (Coal 59, Charcoal 1)"), or null when hidden.</summary>
    public (string Choice, string Note)? InputChoiceText => _input.Visible && _input.Selected >= 0 ? (_input.GetItemText(_input.Selected), _inputNote.Text) : null;

    /// <summary>The Output picker's choice as shown ("Iron Ore, every 5 s"), or null when hidden.</summary>
    public string? OutputText => _output.Visible && _output.Selected >= 0 ? _output.GetItemText(_output.Selected) : null;

    public (string Text, bool Enabled)? BuyTruck => _buyTruck.Visible ? (_buyTruck.Text, !_buyTruck.Disabled) : null;

    /// <summary>
    /// The Shipyard's Ship launch section as shown: each bill entry's "have / need", the button's text
    /// and whether it can be pressed, why it cannot (or null), and the ships launched (or null before
    /// the first); null for any other building. For the snapshot.
    /// </summary>
    public (IReadOnlyList<string> Bill, string Button, bool Enabled, string? Note, string? Ships)? Launch => _launchBox.Visible
        ? (_billRows.Select(r => r.Amount.Text).ToList(), _launch.Text, !_launch.Disabled,
            _launchNote.Visible ? _launchNote.Text : null, _shipsLaunched.Visible ? _shipsLaunched.Text : null)
        : null;

    /// <summary>The Sell truck button as shown (a Depot's panel only), for the snapshot.</summary>
    public (string Text, bool Enabled)? SellTruck => _sellTruck.Visible ? (_sellTruck.Text, !_sellTruck.Disabled) : null;

    /// <summary>What a storage building adds, as shown ("Wood", "+200"), for the snapshot.</summary>
    public IEnumerable<(string Resource, string Amount)> Adds => _adds.GetChildren().OfType<Label>()
        .Chunk(2)
        .Select(pair => (pair[0].Text, pair[1].Text));

    public float TurnDegrees => _turntable.TurnDegrees;

    public void Initialize(Simulation sim, ResourceIcons icons)
    {
        _sim = sim;
        _icons = icons;
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

        _rows = new StatusRows("Network", "Switch", "Power", "Water", "Sand", "Charge", "Research", "State", "Site", "Plot", "Eats", "In range", "Working on", "Recipe", "Buffer", "Truck", "Burning", "Links", "Reach",
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
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        foreach (ResourceType type in SiloChoices)
        {
            _silo.AddItem(ResourceDefs.For(type).Name);
        }
        _silo.ItemSelected += OnSiloSelected;
        Body.AddChild(_silo);
        Host.Register(Id + "/siloResource", _silo);

        _recipeLabel = UiTheme.MakeLabel("Recipe", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_recipeLabel);
        _recipe = new OptionButton
        {
            Name = "Recipe",
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        _recipe.ItemSelected += OnRecipeSelected;
        Body.AddChild(_recipe);
        Host.Register(Id + "/recipe", _recipe);
        _recipeNeeds = new RichTextLabel
        {
            Name = "RecipeNeeds",
            BbcodeEnabled = true,
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _recipeNeeds.AddThemeFontSizeOverride("normal_font_size", UiTheme.SmallSize);
        _recipeNeeds.AddThemeColorOverride("default_color", UiTheme.TextDim);
        Body.AddChild(_recipeNeeds);

        _inputLabel = UiTheme.MakeLabel("Input", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_inputLabel);
        _input = new OptionButton
        {
            Name = "InputChoice",
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        _input.ItemSelected += OnInputSelected;
        Body.AddChild(_input);
        Host.Register(Id + "/input", _input);
        _inputNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_inputNote);

        _orderRow = new HBoxContainer
        {
            Name = "Order",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _orderRow.AddThemeConstantOverride("separation", UiTheme.Gap / 2);
        _less = OrderButton("Less", "-", "One cycle fewer (Shift: ten)");
        _less.Pressed += () => StepQuantity(-1);
        _orderRow.AddChild(_less);
        _quantityLabel = UiTheme.MakeLabel("", UiTheme.BodySize);
        _quantityLabel.CustomMinimumSize = new Vector2(36f, 0f);
        _quantityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _quantityLabel.VerticalAlignment = VerticalAlignment.Center;
        _orderRow.AddChild(_quantityLabel);
        _more = OrderButton("More", "+", "One cycle more (Shift: ten)");
        _more.Pressed += () => StepQuantity(1);
        _orderRow.AddChild(_more);
        _craft = OrderButton("Craft", "Craft", null);
        _craft.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _craft.Pressed += OnCraft;
        _orderRow.AddChild(_craft);
        Body.AddChild(_orderRow);
        _queueLabel = UiTheme.MakeLabel("Queue", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_queueLabel);
        _queue = new VBoxContainer
        {
            Name = "Queue",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _queue.AddThemeConstantOverride("separation", 2);
        Body.AddChild(_queue);

        _fuelLabel = UiTheme.MakeLabel("Fuel", UiTheme.SmallSize, UiTheme.TextDim);
        Body.AddChild(_fuelLabel);
        _fuel = new OptionButton
        {
            Name = "Fuel",
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
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
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
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
        _openResearch = new Button
        {
            Name = "OpenResearch",
            Text = "Research tree [T]",
            FocusMode = FocusModeEnum.None
        };
        _openResearch.Pressed += () => ResearchRequested?.Invoke();
        Body.AddChild(_openResearch);
        Host.Register(Id + "/openResearch", _openResearch);
        _buyNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.Short);
        Body.AddChild(_buyNote);
        _sellTruck = new Button
        {
            Name = "SellTruck",
            FocusMode = FocusModeEnum.None
        };
        _sellTruck.Pressed += OnSellTruck;
        Body.AddChild(_sellTruck);
        Host.Register(Id + "/sellTruck", _sellTruck);

        _launchBox = new VBoxContainer
        {
            Name = "Launch",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _launchBox.AddThemeConstantOverride("separation", UiTheme.Gap / 2);
        _launchBox.AddChild(UiTheme.MakeLabel("Ship launch", UiTheme.SmallSize, UiTheme.TextDim));
        // The button first, then the bill, so Launch ship shows (disabled until it can launch) without scrolling.
        _launch = new Button
        {
            Name = "LaunchShip",
            Text = "Launch ship",
            FocusMode = FocusModeEnum.None
        };
        _launch.Pressed += OnLaunch;
        _launchBox.AddChild(_launch);
        Host.Register(Id + "/launch", _launch);
        _launchNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _launchBox.AddChild(_launchNote);
        _shipsLaunched = UiTheme.MakeLabel("", UiTheme.BodySize);
        _launchBox.AddChild(_shipsLaunched);
        foreach (ResourceAmount cost in Simulation.LaunchCost)
        {
            var line = new HBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            line.AddThemeConstantOverride("separation", 6);
            TextureRect icon = HudOverlay.MakeIcon(20f);
            icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            line.AddChild(icon);
            Label name = UiTheme.MakeLabel(ResourceDefs.For(cost.Type).Name, UiTheme.BodySize);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            line.AddChild(name);
            Label amount = UiTheme.MakeLabel("", UiTheme.BodySize);
            line.AddChild(amount);
            _launchBox.AddChild(line);
            _billRows.Add((cost.Type, cost.Amount, icon, amount));
        }
        Body.AddChild(_launchBox);
        // Right under the progress bar, above the recipe picker, so Launch ship shows without scrolling.
        Body.MoveChild(_launchBox, _progress.GetIndex() + 1);

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
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
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
        _destroy.MouseEntered += () => _destroyHovered = true;
        _destroy.MouseExited += () =>
        {
            _destroyHovered = false;
            Host.HideHover(_destroy);
        };
        Body.AddChild(_destroy);
        Host.Register(Id + "/destroy", _destroy);
    }

    private Button OrderButton(string name, string text, string? tip)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            TooltipText = tip ?? "",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(28f, 0f)
        };
        Host.Register(Id + "/" + char.ToLowerInvariant(name[0]) + name[1..], button);
        return button;
    }

    /// <summary>Shows a building (opening the panel if needed).</summary>
    public void ShowBuilding(Building building)
    {
        if (building != Building)
        {
            _selected = building.RecipeIndex;
            _quantity = 0;
        }
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
        _rows.Set("Charge", ChargeText(building));
        _rows.Set("Research", def.Researches ? ResearchText() : null);
        _openResearch.Visible = def.Researches;
        RefreshFuel(building);
        bool works = !storage && !def.IsHub;
        _rows.Set("State", works ? status.State == BuildingState.Working ? "Working" : "Idle: " + status.IdleReason : null);
        RefreshStorage(building);
        _rows.Set("Network", building.Online ? "Online" : "Offline");
        _rows.Set("Buffer", BufferText(building));
        _rows.Set("Truck", TruckText.CollectorRow(_sim, building));
        RefreshTrucks(building);
        RefreshLaunch(building);
        RefreshRenewables(building);
        RefreshRecipePickers(building);
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
            // A queueing processor names its recipe only while it has an order.
            bool idleQueue = building.Queues && building.Queue.Count == 0 && !building.Crafting;
            _rows.Set("Recipe", recipe is null || idleQueue ? null : RecipeText(recipe)
                + (building.PendingRecipe is int next ? $" (next: {building.Recipes[next].Name})" : ""));
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
        bool hasProgress = works && (def.IsCollector || building.Recipe is not null) && !(building.Queues && building.Queue.Count == 0 && !building.Crafting);
        _progressLabel.Visible = hasProgress;
        _progress.Visible = hasProgress;

        _toggle.Visible = !storage;
        _destroy.Visible = !def.Permanent;
        _toggle.Text = building.Enabled ? "Turn off" : "Turn on";
        _destroy.Text = _confirmLeft > 0 ? "Confirm destroy" : "Destroy";
        DestroyTip = "Refund: " + RefundText(building);
        if (_destroyHovered)
        {
            Host.ShowHover(_destroy, TipLine.Wrapped(DestroyTip).ToList());
        }
    }

    public override void _Process(double delta)
    {
        if (_confirmLeft > 0)
        {
            _confirmLeft -= delta;
        }
    }

    /// <summary>
    /// The Recipe picker (a building with more than one recipe; a change waits for the running cycle)
    /// and the input picker for the recipe's input with alternatives (Auto, then each by name), with
    /// a dim note naming what it uses and why.
    /// </summary>
    private void RefreshRecipePickers(Building building)
    {
        bool many = building.Recipes.Count > 1;
        _recipeLabel.Visible = many;
        _recipe.Visible = many;
        if (many)
        {
            string signature = string.Join("|", building.Recipes.Select(r => r.Name));
            if (signature != _recipeSignature)
            {
                _recipeSignature = signature;
                _recipe.Clear();
                foreach (Recipe option in building.Recipes)
                {
                    _recipe.AddItem(option.Name);
                }
            }
            _recipe.Selected = building.Queues ? _selected : building.PendingRecipe ?? building.RecipeIndex;
        }
        _recipeNeeds.Visible = many;
        if (many && PickedRecipe(building) is Recipe picked)
        {
            (string needs, List<string> shortOf) = NeedsText(picked);
            if (_recipeNeeds.Text != needs)
            {
                _recipeNeeds.Text = needs;
            }
            _recipeShort = shortOf;
        }
        RefreshOrders(building);

        _inputIndex = -1;
        if (PickedRecipe(building) is Recipe recipe)
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                if (recipe.OptionsFor(i).Count > 1)
                {
                    _inputIndex = i;
                    break;
                }
            }
        }
        bool alternative = _inputIndex >= 0;
        _inputLabel.Visible = alternative;
        _input.Visible = alternative;
        _inputNote.Visible = alternative;
        if (!alternative)
        {
            _inputSignature = "";
            return;
        }
        Recipe current = PickedRecipe(building)!;
        IReadOnlyList<ResourceType> options = current.OptionsFor(_inputIndex);
        string inputSignature = current.Name + "|" + _inputIndex;
        if (inputSignature != _inputSignature)
        {
            _inputSignature = inputSignature;
            _input.Clear();
            _input.AddItem("Auto");
            foreach (ResourceType option in options)
            {
                _input.AddItem(ResourceDefs.For(option).Name);
            }
        }
        _inputLabel.Text = current.InputName(_inputIndex);
        ResourceType uses = _sim.ChosenInput(building, current, _inputIndex);
        if (building.InputChoices.TryGetValue((PickedIndex(building), _inputIndex), out ResourceType chosen))
        {
            _input.Selected = 1 + options.ToList().IndexOf(chosen);
            _inputNote.Text = $"Uses only {ResourceDefs.For(chosen).Name}";
        }
        else
        {
            _input.Selected = 0;
            _inputNote.Text = $"Auto: uses {ResourceDefs.For(uses).Name} ("
                + string.Join(", ", options.Select(o => $"{ResourceDefs.For(o).Name} {Mathf.FloorToInt(_sim.AtHand(building, o))}")) + ")";
        }
    }

    /// <summary>The recipe the pickers show: the one chosen to queue, or a running processor's own.</summary>
    private int PickedIndex(Building building)
    {
        return building.Queues ? _selected : building.PendingRecipe ?? building.RecipeIndex;
    }

    private Recipe? PickedRecipe(Building building)
    {
        return building.Recipes.Count > 0 ? building.Recipes[PickedIndex(building)] : null;
    }

    private void OnRecipeSelected(long index)
    {
        if (Building is not Building building)
        {
            return;
        }
        if (building.Queues)
        {
            _selected = (int)index;
            _quantity = 0;
        }
        else
        {
            _sim.SetRecipe(building.Id, (int)index);
        }
    }

    private void OnInputSelected(long index)
    {
        if (Building is not Building building || PickedRecipe(building) is not Recipe recipe || _inputIndex < 0)
        {
            return;
        }
        IReadOnlyList<ResourceType> options = recipe.OptionsFor(_inputIndex);
        _sim.SetInputChoice(building.Id, PickedIndex(building), _inputIndex, index <= 0 ? null : options[(int)index - 1]);
    }

    /// <summary>Items a cycle of the recipe chosen to queue makes (the quantity's step).</summary>
    private int PerCycle(Building building)
    {
        return Simulation.WholeCycles(building.Recipes[_selected], 1);
    }

    /// <summary>
    /// A queueing processor's quantity and Craft (disabled when the queue is full and the last order
    /// is another recipe), then its orders, the first marked while it crafts.
    /// </summary>
    private void RefreshOrders(Building building)
    {
        bool queues = building.Queues;
        _orderRow.Visible = queues;
        _queueLabel.Visible = queues;
        _queue.Visible = queues;
        if (!queues)
        {
            _queueSignature = "";
            return;
        }
        int step = PerCycle(building);
        _quantity = Mathf.Clamp(_quantity <= 0 ? step : _quantity, step, MaxCycles * step);
        _quantityLabel.Text = _quantity.ToString();
        _less.Disabled = _quantity <= step;
        _more.Disabled = _quantity >= MaxCycles * step;
        List<CraftOrder> orders = building.Queue;
        _craft.Disabled = orders.Count >= Simulation.MaxQueue && orders[^1].RecipeIndex != _selected;

        string signature = $"{building.Id}{(building.Crafting ? "*" : "")}:" + string.Join("|", orders.Select(o => $"{o.RecipeIndex}x{o.Remaining}"));
        if (signature == _queueSignature)
        {
            return;
        }
        _queueSignature = signature;
        for (int i = 0; i < _queueRows; i++)
        {
            Host.Unregister($"{Id}/queue/{i}");
        }
        foreach (Node child in _queue.GetChildren())
        {
            _queue.RemoveChild(child);
            child.QueueFree();
        }
        _queueRows = orders.Count;
        if (orders.Count == 0)
        {
            _queue.AddChild(UiTheme.MakeLabel("Queue empty", UiTheme.SmallSize, UiTheme.TextDim));
            return;
        }
        for (int i = 0; i < orders.Count; i++)
        {
            var row = new HBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            string name = building.Recipes[orders[i].RecipeIndex].Name;
            Label label = UiTheme.MakeLabel($"{name} x {orders[i].Remaining}" + (i == 0 && building.Crafting ? " (crafting)" : ""), UiTheme.BodySize);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
            int index = i;
            var remove = new Button
            {
                Name = "Remove",
                Text = "X",
                TooltipText = "Remove this order",
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(28f, 0f)
            };
            remove.Pressed += () => OnRemoveOrder(index);
            row.AddChild(remove);
            Host.Register($"{Id}/queue/{i}", remove);
            _queue.AddChild(row);
        }
    }

    /// <summary>The most cycles one press of Craft queues.</summary>
    private const int MaxCycles = 99;

    private void StepQuantity(int direction)
    {
        if (Building is not Building building)
        {
            return;
        }
        int step = PerCycle(building) * (Input.IsKeyPressed(Key.Shift) ? 10 : 1);
        _quantity = Mathf.Clamp(_quantity + direction * step, PerCycle(building), MaxCycles * PerCycle(building));
        Refresh();
    }

    private void OnCraft()
    {
        if (Building is Building building && _sim.QueueCraft(building.Id, _selected, _quantity))
        {
            Refresh();
        }
    }

    private void OnRemoveOrder(int index)
    {
        if (Building is Building building && _sim.CancelCraft(building.Id, index))
        {
            Refresh();
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

    /// <summary>
    /// A Depot's Trucks row, a row per truck with its state, the Buy truck button with what it is
    /// short of, and the Sell truck button with what selling gives back.
    /// </summary>
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
        _sellTruck.Visible = slots > 0;
        _buyNote.Visible = false;
        if (slots <= 0)
        {
            return;
        }
        Truck? toSell = _sim.TruckToSell(building);
        var back = toSell is null ? new List<ResourceAmount>() : Simulation.RefundOf(toSell).ToList();
        _sellTruck.Text = toSell is null ? "Sell truck"
            : back.Count == 0 ? "Sell truck (nothing back)"
            : "Sell truck (" + string.Join(", ", back.Select(c => $"{(int)c.Amount} {ResourceDefs.For(c.Type).Name}")) + " back)";
        _sellTruck.Disabled = toSell is null;
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

    /// <summary>
    /// The Shipyard's Ship launch section: the bill (have / need, short amounts in the short color),
    /// the Launch ship button, enabled only while this Shipyard is switched on and online and the
    /// whole bill is in storage, with why not under it, and the ships launched once there is one.
    /// </summary>
    private void RefreshLaunch(Building building)
    {
        _launchBox.Visible = building.Type == BuildingType.Shipyard;
        if (!_launchBox.Visible)
        {
            return;
        }
        foreach ((ResourceType type, float need, TextureRect icon, Label amount) in _billRows)
        {
            icon.Texture ??= _icons.For(type);
            int have = HudOverlay.Whole(_sim, type);
            amount.Text = $"{have} / {(int)need}";
            amount.AddThemeColorOverride("font_color", have < need ? UiTheme.Short : UiTheme.Text);
        }
        string? why = !building.Enabled ? "This Shipyard is switched off"
            : !building.Online ? "This Shipyard is offline"
            : !_sim.LaunchBillMet ? "Needs the whole bill in storage"
            : null;
        _launch.Disabled = why is not null;
        _launchNote.Visible = why is not null;
        _launchNote.Text = why ?? "";
        _shipsLaunched.Visible = _sim.ShipsLaunched > 0;
        _shipsLaunched.Text = "Ships launched: " + _sim.ShipsLaunched;
    }

    private void OnLaunch()
    {
        if (Building is Building building && building.Enabled && building.Online)
        {
            _sim.TryLaunchShip();
        }
    }

    private void OnSellTruck()
    {
        if (Building is Building building)
        {
            _sim.TrySellTruck(building.Id);
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
    /// The buffer row: the output buffer of an extractor ("Iron Ore 12, Coal 3 / 20") or a processor
    /// ("Iron 4 / 10"), or null for a building without one.
    /// </summary>
    private string? BufferText(Building building)
    {
        BuildingDef def = building.Def;
        if (def.OutputBuffer <= 0f)
        {
            return null;
        }
        string output = building.OutputCount <= 0f
            ? "empty"
            : string.Join(", ", building.Output.Select(e => $"{ResourceDefs.For(e.Key).Name} {Mathf.FloorToInt(e.Value)}"));
        return $"{output} / {def.OutputBuffer:0}";
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
            float speed = _sim.WorkSpeed(building) * (def.Solar ? _sim.Daylight : 1f);
            return speed < 1f ? $"Supplies {supply * speed:0.#} of {supply:0}" : $"Supplies {supply:0}";
        }
        return null;
    }

    /// <summary>A Research Lab's Research row: the running project, how far along, and the research speed.</summary>
    private string ResearchText()
    {
        if (_sim.ActiveResearch is not ResearchProject project)
        {
            return "No research running";
        }
        ResearchDef def = ResearchDefs.For(project);
        float percent = 100f * _sim.ResearchProgress(project) / def.Work;
        return $"{def.Name}, {Mathf.FloorToInt(percent)}%, speed {_sim.ResearchSpeed:0.##}x";
    }

    /// <summary>A Battery Bank's Charge row: what it holds and whether it is charging, discharging, full, or holding.</summary>
    private string? ChargeText(Building building)
    {
        BuildingDef def = building.Def;
        if (def.StoreCapacity <= 0f)
        {
            return null;
        }
        float flow = _sim.Capacity(CapacityType.Power).StoreFlow;
        bool live = building.Enabled && building.Online;
        string doing = building.Charge >= def.StoreCapacity - 0.5f ? "full"
            : live && flow > 0f ? "charging"
            : live && flow < 0f && building.Charge > 0f ? "discharging"
            : "holding";
        return $"{Mathf.FloorToInt(building.Charge)} / {def.StoreCapacity:0}, {doing}";
    }

    /// <summary>The Burning row for a fuel burner: what burns and for how long.</summary>
    private static string? BurningText(Building building)
    {
        if (!building.Def.BurnsFuel)
        {
            return null;
        }
        return building.Burning is ResourceType fuel && building.BurnLeft > 0f
            ? $"{ResourceDefs.For(fuel).Name}, {Mathf.CeilToInt(building.BurnLeft)} s left"
            : "nothing";
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
        // The picker offers the fuels this building takes (a Fission Reactor's Fuel Rods, a fire's
        // Coal, Charcoal, and Wood), rebuilt when the panel shows a building that takes others.
        if (!ReferenceEquals(_fuels, building.Def.Fuels))
        {
            _fuels = building.Def.Fuels;
            _fuel.Clear();
            _fuel.AddItem("Best");
            foreach (ResourceType fuel in _fuels)
            {
                _fuel.AddItem(ResourceDefs.For(fuel).Name);
            }
        }
        _fuel.Selected = building.FuelChoice is ResourceType chosen ? 1 + _fuels.ToList().IndexOf(chosen) : 0;
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
            _sim.SetFuelChoice(building.Id, index == 0 ? null : _fuels[(int)index - 1]);
        }
    }

    private void OnSiloSelected(long index)
    {
        if (Building is Building building)
        {
            _sim.SetSiloResource(building.Id, SiloChoices[index]);
        }
    }

    /// <summary>
    /// A recipe as the Recipe row reads it, in BBCode with each input storage holds less than a
    /// cycle's worth of in the danger color (an input with alternatives only when every one is short),
    /// and the names of those inputs.
    /// </summary>
    private (string Text, List<string> Short) NeedsText(Recipe recipe)
    {
        var shortOf = new List<string>();
        var inputs = new List<string>();
        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            float amount = recipe.Inputs[i].Amount;
            string text = $"{(int)amount} {recipe.InputName(i)}";
            bool isShort = recipe.OptionsFor(i).All(type => _sim.Stockpile.GetValueOrDefault(type) < amount);
            if (isShort)
            {
                shortOf.Add(recipe.InputName(i));
                text = $"[color=#{UiTheme.Danger.ToHtml(false)}]{text}[/color]";
            }
            inputs.Add(text);
        }
        string outputs = string.Join(" + ", recipe.Outputs.Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
        string makes = recipe.Inputs.Count == 0 ? outputs : $"{string.Join(" + ", inputs)} to {outputs}";
        return ($"{makes}, {recipe.CycleTime:0.#} s", shortOf);
    }

    private static string RecipeText(Recipe recipe)
    {
        static string Amounts(IEnumerable<ResourceAmount> amounts) =>
            string.Join(" + ", amounts.Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
        // An input with alternatives reads as all of them ("1 Coal or Charcoal").
        string inputs = string.Join(" + ", recipe.Inputs.Select((a, i) => $"{(int)a.Amount} {recipe.InputName(i)}"));
        string makes = recipe.Inputs.Count == 0 ? Amounts(recipe.Outputs) : $"{inputs} to {Amounts(recipe.Outputs)}";
        return $"{makes}, {recipe.CycleTime:0.#} s";
    }

    private string RefundText(Building building)
    {
        string refund = string.Join(", ", _sim.DestroyRefund(building)
            .Select(a => $"{(int)a.Amount} {ResourceDefs.For(a.Type).Name}"));
        return refund.Length > 0 ? refund : "nothing";
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
