using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The detail panel for a placed building (presentation.md): its name as the title; a turning
/// picture of it with the status strip as a column beside it (on/off, network, capacities, charge,
/// the fuel burning, a collector's truck); the state line; the text rows that need words; the
/// progress bar; then its controls in sections (Launch, Production, Output, Fuel, Storage, Trucks,
/// Research), each only when it has something; and Destroy at the bottom (asking once; a permanent
/// building, the HQ, cannot be destroyed). It refreshes every frame and closes itself when its
/// building is gone.
/// </summary>
public partial class BuildingDetail : Dialog
{
    /// <summary>How long the Destroy button waits for its confirming click.</summary>
    public const double ConfirmSeconds = 3.0;

    /// <summary>Room kept beside the body for its scroll bar when choosing a picker's form.</summary>
    private const float ScrollBarRoom = 12f;

    /// <summary>
    /// Idle reasons that are the player's choice or the normal course of things, shown dim; any
    /// other reason is a problem, shown in the warning color.
    /// </summary>
    private static readonly string[] CalmIdle = { "Switched off", "Queue empty", "Holding:", "No research running", "Night:" };

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private TurntableView _turntable = null!;
    private StatusStrip _strip = null!;
    private IconToggle _toggle = null!;
    private Label _state = null!;
    private StatusRows _rows = null!;
    private ProgressBar _progress = null!;
    private readonly List<VBoxContainer> _sections = new();
    private float _room;

    // Launch (the Shipyard): the button, why it is off, the ships launched, and a row per bill entry.
    private VBoxContainer _launchBox = null!;
    private readonly List<(ResourceType Type, float Need, TextureRect Icon, Label Amount)> _billRows = new();
    private Button _launch = null!;
    private Label _launchNote = null!;
    private Label _shipsLaunched = null!;

    // Production: the Recipe dropdown and its needs line, Mode, the input picker and its note, then a
    // queueing processor's order row and queue, or in Hold its Hold at row and storage line.
    private Label _recipeLabel = null!;
    private OptionButton _recipe = null!;
    private RichTextLabel _recipeNeeds = null!;
    private string _recipeSignature = "";
    private IReadOnlyList<string> _recipeShort = new List<string>();
    private SegmentedPicker _mode = null!;
    private SegmentedPicker _input = null!;
    private Label _inputNote = null!;
    private int _inputIndex = -1;
    // The recipe the picker has chosen to queue, and the quantity to queue.
    private int _selected;
    private int _quantity;
    private HBoxContainer _orderRow = null!;
    private StepButton _less = null!;
    private Label _quantityLabel = null!;
    private StepButton _more = null!;
    private Button _craft = null!;
    private Label _queueLabel = null!;
    private VBoxContainer _queue = null!;
    private string _queueSignature = "";
    private int _queueRows;
    private HBoxContainer _holdRow = null!;
    private StepButton _holdLess = null!;
    private Label _holdLabel = null!;
    private StepButton _holdMore = null!;
    private RichTextLabel _holdLine = null!;

    // Output: a collector's choice of what it makes.
    private OptionButton _output = null!;
    private string _outputSignature = "";
    private List<ResourceType> _outputChoices = new();

    // Fuel: what a burner burns, and the note naming what Best burns now.
    private SegmentedPicker _fuel = null!;
    private IReadOnlyList<ResourceType> _fuels = System.Array.Empty<ResourceType>();
    private Label _fuelNote = null!;

    // Storage: a Silo's resource, and what a storage building adds.
    private Label _siloLabel = null!;
    private OptionButton _silo = null!;
    private Label _addsLabel = null!;
    private GridContainer _adds = null!;
    private string _addsSignature = "";

    // Trucks (a Depot): a bay per truck slot, Buy and Sell truck, and the buy note.
    private StatusStrip _bays = null!;
    private HBoxContainer _truckButtons = null!;
    private Button _buyTruck = null!;
    private Button _sellTruck = null!;
    private Label _buyNote = null!;
    private string _buyTip = "";
    private string _sellTip = "";

    private Button _openResearch = null!;
    private Button _destroy = null!;
    private Button _upgrade = null!;
    private bool _upgradeHovered;
    private double _confirmLeft;
    private bool _destroyHovered;

    /// <summary>The Bulk resources a Silo can hold, in table order (the picker's items).</summary>
    private static readonly ResourceType[] SiloChoices = System.Enum.GetValues<ResourceType>()
        .Where(type => ResourceDefs.For(type).Storage.Class == StorageClass.Bulk)
        .ToArray();

    /// <summary>Raised when the lab's button asks for the research tree.</summary>
    public event System.Action? ResearchRequested;

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "buildingDetail";

    /// <summary>The building shown, or null.</summary>
    public Building? Building { get; private set; }

    /// <summary>The status rows as shown (label to value), for the snapshot.</summary>
    public IReadOnlyDictionary<string, string> Rows => _rows.Shown;

    /// <summary>The status strip as shown, the on/off toggle first while it shows, for the snapshot.</summary>
    public IReadOnlyList<StatusEntry> Strip => _toggle.Visible
        ? _strip.Shown.Prepend(new StatusEntry("switch", null, null, _toggle.On ? Tone.Good : Tone.Dim, _toggle.Tip)).ToList()
        : _strip.Shown;

    /// <summary>The state line as shown and its tone, or null when hidden, for the snapshot.</summary>
    public (string Text, Tone Tone)? State => _state.Visible ? (_state.Text, _stateTone) : null;

    private Tone _stateTone;

    /// <summary>A Depot's bays as shown (each truck's state, or "empty", and its tooltip), or null for any other building.</summary>
    public IReadOnlyList<(string State, string Tip)>? Bays => _bays.Visible
        ? _bays.Shown.Select(bay => (bay.Id, bay.Tip)).ToList()
        : null;

    /// <summary>The fuel picker's choice as shown ("Best", "Coal"), or null when hidden.</summary>
    public string? FuelText => _fuel.Visible ? _fuel.SelectedText : null;

    /// <summary>The Destroy button's text (it reads "Confirm destroy" while waiting), or null when hidden.</summary>
    /// <summary>The panel's title: the building's level name (for the snapshot).</summary>
    public string Title => TitleLabel.Text;

    public string? DestroyText => _destroy.Visible ? _destroy.Text : null;

    /// <summary>The Upgrade button as shown (its label, whether it can be pressed, its tooltip lines), or null when hidden.</summary>
    public (string Text, bool Enabled, IReadOnlyList<string> Tip)? UpgradeShown => _upgrade.Visible
        ? (_upgrade.Text, !_upgrade.Disabled, UpgradeTip.Select(line => line.Text).ToList())
        : null;

    private List<TipLine> UpgradeTip { get; set; } = new();

    /// <summary>The Destroy button's tooltip ("Refund: 13 Wood, 8 Stone").</summary>
    public string DestroyTip { get; private set; } = "";

    /// <summary>The quantity to queue as shown ("5"), and whether Craft can be pressed; null when hidden.</summary>
    public (string Quantity, bool CanCraft)? Order => _orderRow.Visible ? (_quantityLabel.Text, !_craft.Disabled) : null;

    /// <summary>The queue's rows as shown ("Iron x 5 (crafting)"), or null when hidden; empty when the queue is.</summary>
    public IReadOnlyList<string>? QueueRows => _queue.Visible
        ? _queue.GetChildren().Select(row => row is Label label ? label.Text : row.GetChild<Label>(0).Text).ToList()
        : null;

    /// <summary>The Mode picker's choice as shown ("Queue", "Hold"), or null when hidden.</summary>
    public string? ModeText => _mode.Visible ? _mode.SelectedText : null;

    /// <summary>The hold target as shown ("50") and the storage line under it, or null when hidden.</summary>
    public (string Target, string Line)? HoldShown => _holdRow.Visible ? (_holdLabel.Text, _holdLine.GetParsedText()) : null;

    /// <summary>The Recipe picker's choice as shown ("Copper"), or null when hidden.</summary>
    public string? RecipeChoiceText => _recipe.Visible && _recipe.Selected >= 0 ? _recipe.GetItemText(_recipe.Selected) : null;

    /// <summary>
    /// The line under the Recipe picker ("2 Iron Ore + 1 Coal to 1 Iron, 16 s") and the inputs it
    /// shows short (storage holds less than a cycle's worth), or null while the picker is hidden.
    /// </summary>
    public (string Text, IReadOnlyList<string> Short)? RecipeNeeds => _recipeNeeds.Visible ? (_recipeNeeds.GetParsedText(), _recipeShort) : null;

    /// <summary>The input picker's choice and note as shown ("Auto", "Auto: uses Coal (Coal 59, Charcoal 1)"), or null when hidden.</summary>
    public (string Choice, string Note)? InputChoiceText => _input.Visible && _input.SelectedText is string choice ? (choice, _inputNote.Text) : null;

    /// <summary>The Output picker's choice as shown ("Iron Ore, every 5 s"), or null when hidden.</summary>
    public string? OutputText => _output.Visible && _output.Selected >= 0 ? _output.GetItemText(_output.Selected) : null;

    /// <summary>The Buy truck button's text, whether it can be pressed, and its tooltip, or null when hidden (not a Depot).</summary>
    public (string Text, bool Enabled, string Tip)? BuyTruck => _buyTruck.Visible ? (_buyTruck.Text, !_buyTruck.Disabled, _buyTip) : null;

    /// <summary>The Sell truck button as shown, with its tooltip (a Depot's panel only), for the snapshot.</summary>
    public (string Text, bool Enabled, string Tip)? SellTruck => _sellTruck.Visible ? (_sellTruck.Text, !_sellTruck.Disabled, _sellTip) : null;

    /// <summary>
    /// The Shipyard's launch section as shown: each bill entry's "have / need", the button's text
    /// and whether it can be pressed, why it cannot (or null), and the ships launched (or null before
    /// the first); null for any other building. For the snapshot.
    /// </summary>
    public (IReadOnlyList<string> Bill, string Button, bool Enabled, string? Note, string? Ships)? Launch => _launchBox.Visible
        ? (_billRows.Select(r => r.Amount.Text).ToList(), _launch.Text, !_launch.Disabled,
            _launchNote.Visible ? _launchNote.Text : null, _shipsLaunched.Visible ? _shipsLaunched.Text : null)
        : null;

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
        BuildTop();
        _state = UiTheme.MakeLabel("", UiTheme.BodySize);
        _state.Name = "State";
        _state.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(_state);
        _rows = new StatusRows("Research", "Site", "Plot", "Eats", "In range", "Working on", "Recipe", "Buffer", "Links", "Reach");
        Body.AddChild(_rows);
        _progress = StatusRows.MakeProgressBar();
        Body.AddChild(_progress);

        BuildLaunch(Section("Launch"));
        BuildProduction(Section("Production"));
        BuildOutput(Section("Output"));
        BuildFuel(Section("Fuel"));
        BuildStorage(Section("Storage"));
        BuildTrucks(Section("Trucks"));
        BuildResearch(Section("Research"));

        Body.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, UiTheme.Gap),
            MouseFilter = MouseFilterEnum.Ignore
        });
        // Upgrade to the next level, above Destroy, while the building has one.
        _upgrade = UiTheme.SizeToLabel(new Button
        {
            Name = "Upgrade",
            FocusMode = FocusModeEnum.None
        });
        _upgrade.Pressed += () =>
        {
            if (Building is Building upgrading && _sim.TryUpgrade(upgrading.Id))
            {
                ShowBuilding(upgrading);
            }
        };
        _upgrade.MouseEntered += () => _upgradeHovered = true;
        _upgrade.MouseExited += () =>
        {
            _upgradeHovered = false;
            Host.HideHover(_upgrade);
        };
        Body.AddChild(_upgrade);
        Host.Register(Id + "/upgrade", _upgrade);

        _destroy = UiTheme.SizeToLabel(new Button
        {
            Name = "Destroy",
            FocusMode = FocusModeEnum.None
        });
        StyleBoxFlat danger = UiTheme.Box(UiTheme.Danger, UiTheme.CellRadius, padding: UiTheme.Gap);
        danger.ContentMarginLeft = UiTheme.Padding;
        danger.ContentMarginRight = UiTheme.Padding;
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

    /// <summary>The turntable, and the status strip as a column beside it that wraps at the picture's height.</summary>
    private void BuildTop()
    {
        var top = new HBoxContainer
        {
            Name = "Top",
            MouseFilter = MouseFilterEnum.Ignore
        };
        top.AddThemeConstantOverride("separation", UiTheme.Padding);
        Body.AddChild(top);
        _turntable = new TurntableView
        {
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        top.AddChild(_turntable);
        _strip = new StatusStrip(Host, column: true, TurntableView.PictureSize)
        {
            Name = "Strip",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        top.AddChild(_strip);
        _toggle = new IconToggle(Host, "On: click to turn off", "Off: click to turn on")
        {
            Name = "Toggle"
        };
        _toggle.Pressed += OnToggle;
        _strip.AddChild(_toggle);
        Host.Register(Id + "/toggle", _toggle);
    }

    /// <summary>A section under its heading; it shows while any of its controls does.</summary>
    private VBoxContainer Section(string title)
    {
        var section = new VBoxContainer
        {
            Name = title,
            MouseFilter = MouseFilterEnum.Ignore
        };
        section.AddThemeConstantOverride("separation", UiTheme.Gap);
        section.AddChild(new SectionHeading(title));
        Body.AddChild(section);
        _sections.Add(section);
        return section;
    }

    private void BuildLaunch(VBoxContainer section)
    {
        _launchBox = new VBoxContainer
        {
            Name = "Launch",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _launchBox.AddThemeConstantOverride("separation", UiTheme.Gap / 2);
        // The button first, then the bill, so Launch ship shows (disabled until it can launch) without scrolling.
        _launch = UiTheme.SizeToLabel(new Button
        {
            Name = "LaunchShip",
            Text = "Launch ship",
            FocusMode = FocusModeEnum.None
        });
        _launch.Pressed += OnLaunch;
        _launchBox.AddChild(_launch);
        Host.Register(Id + "/launch", _launch);
        _launchNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _launchNote.HorizontalAlignment = HorizontalAlignment.Center;
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
        section.AddChild(_launchBox);
    }

    private void BuildProduction(VBoxContainer section)
    {
        _recipeLabel = UiTheme.MakeLabel("Recipe", UiTheme.SmallSize, UiTheme.TextDim);
        section.AddChild(_recipeLabel);
        _recipe = Dropdown("Recipe", OnRecipeSelected);
        section.AddChild(_recipe);
        Host.Register(Id + "/recipe", _recipe);
        _recipeNeeds = NoteText("RecipeNeeds");
        section.AddChild(_recipeNeeds);

        _mode = new SegmentedPicker(Host, Id + "/mode");
        _mode.Chosen += OnModeSelected;
        section.AddChild(_mode);

        _input = new SegmentedPicker(Host, Id + "/input");
        _input.Chosen += OnInputSelected;
        section.AddChild(_input);
        _inputNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _inputNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        section.AddChild(_inputNote);

        _orderRow = new HBoxContainer
        {
            Name = "Order",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _orderRow.AddThemeConstantOverride("separation", UiTheme.Gap / 2);
        _less = StepperButton("Less", "-", "One cycle fewer. Hold to repeat.");
        _less.Step += () => StepQuantity(-1);
        _orderRow.AddChild(_less);
        _quantityLabel = UiTheme.MakeLabel("", UiTheme.BodySize);
        _quantityLabel.CustomMinimumSize = new Vector2(36f, 0f);
        _quantityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _quantityLabel.VerticalAlignment = VerticalAlignment.Center;
        _orderRow.AddChild(_quantityLabel);
        _more = StepperButton("More", "+", "One cycle more. Hold to repeat.");
        _more.Step += () => StepQuantity(1);
        _orderRow.AddChild(_more);
        _orderRow.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(UiTheme.Gap / 2, 0f),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _craft = UiTheme.SizeToLabel(new Button
        {
            Name = "Craft",
            Text = "Craft",
            FocusMode = FocusModeEnum.None
        }, SizeFlags.ShrinkBegin);
        _craft.Pressed += OnCraft;
        _orderRow.AddChild(_craft);
        Host.Register(Id + "/craft", _craft);
        section.AddChild(_orderRow);
        _queueLabel = UiTheme.MakeLabel("Queue", UiTheme.SmallSize, UiTheme.TextDim);
        section.AddChild(_queueLabel);
        _queue = new VBoxContainer
        {
            Name = "Queue",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _queue.AddThemeConstantOverride("separation", 2);
        section.AddChild(_queue);

        _holdRow = new HBoxContainer
        {
            Name = "HoldAt",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _holdRow.AddThemeConstantOverride("separation", UiTheme.Gap / 2);
        Label holdAt = UiTheme.MakeLabel("Hold at", UiTheme.BodySize);
        holdAt.VerticalAlignment = VerticalAlignment.Center;
        _holdRow.AddChild(holdAt);
        _holdLess = StepperButton("HoldLess", "-", "One cycle fewer. Hold to repeat.");
        _holdLess.Step += () => StepHold(-1);
        _holdRow.AddChild(_holdLess);
        _holdLabel = UiTheme.MakeLabel("", UiTheme.BodySize);
        _holdLabel.CustomMinimumSize = new Vector2(48f, 0f);
        _holdLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _holdLabel.VerticalAlignment = VerticalAlignment.Center;
        _holdRow.AddChild(_holdLabel);
        _holdMore = StepperButton("HoldMore", "+", "One cycle more. Hold to repeat.");
        _holdMore.Step += () => StepHold(1);
        _holdRow.AddChild(_holdMore);
        section.AddChild(_holdRow);
        _holdLine = NoteText("HoldLine");
        section.AddChild(_holdLine);
    }

    private void BuildOutput(VBoxContainer section)
    {
        _output = Dropdown("Output", OnOutputSelected);
        section.AddChild(_output);
        Host.Register(Id + "/output", _output);
    }

    private void BuildFuel(VBoxContainer section)
    {
        _fuel = new SegmentedPicker(Host, Id + "/fuel");
        _fuel.Chosen += OnFuelSelected;
        section.AddChild(_fuel);
        _fuelNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _fuelNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        section.AddChild(_fuelNote);
    }

    private void BuildStorage(VBoxContainer section)
    {
        _siloLabel = UiTheme.MakeLabel("Holds", UiTheme.SmallSize, UiTheme.TextDim);
        section.AddChild(_siloLabel);
        _silo = Dropdown("SiloResource", OnSiloSelected);
        foreach (ResourceType type in SiloChoices)
        {
            _silo.AddItem(ResourceDefs.For(type).Name);
        }
        section.AddChild(_silo);
        Host.Register(Id + "/siloResource", _silo);

        _addsLabel = UiTheme.MakeLabel("Adds to storage", UiTheme.SmallSize, UiTheme.TextDim);
        section.AddChild(_addsLabel);
        _adds = new GridContainer
        {
            Columns = 2,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _adds.AddThemeConstantOverride("h_separation", UiTheme.Padding);
        _adds.AddThemeConstantOverride("v_separation", 2);
        section.AddChild(_adds);
    }

    private void BuildTrucks(VBoxContainer section)
    {
        // Bay icons are larger than the strip's: a truck seen from the game camera is long and low.
        _bays = new StatusStrip(Host, column: false, 0f, iconSize: 32f)
        {
            Name = "Bays"
        };
        section.AddChild(_bays);
        _truckButtons = new HBoxContainer
        {
            Name = "TruckButtons",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _truckButtons.AddThemeConstantOverride("separation", UiTheme.Gap);
        section.AddChild(_truckButtons);
        _buyTruck = TruckButton("BuyTruck", "Buy truck", () => _buyTip, OnBuyTruck);
        _sellTruck = TruckButton("SellTruck", "Sell truck", () => _sellTip, OnSellTruck);
        _buyNote = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.Short);
        _buyNote.HorizontalAlignment = HorizontalAlignment.Center;
        _buyNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        section.AddChild(_buyNote);
    }

    /// <summary>Buy or Sell truck: sized to its label in the row, its tooltip saying what it costs or gives back.</summary>
    private Button TruckButton(string name, string text, System.Func<string> tip, System.Action pressed)
    {
        Button button = UiTheme.SizeToLabel(new Button
        {
            Name = name,
            Text = text,
            FocusMode = FocusModeEnum.None
        });
        button.Pressed += pressed;
        button.MouseEntered += () => Host.ShowHover(button, TipLine.Wrapped(tip()).ToList());
        button.MouseExited += () => Host.HideHover(button);
        _truckButtons.AddChild(button);
        Host.Register(Id + "/" + char.ToLowerInvariant(name[0]) + name[1..], button);
        return button;
    }

    private void BuildResearch(VBoxContainer section)
    {
        _openResearch = UiTheme.SizeToLabel(new Button
        {
            Name = "OpenResearch",
            Text = "Research tree [T]",
            FocusMode = FocusModeEnum.None
        });
        _openResearch.Pressed += () => ResearchRequested?.Invoke();
        section.AddChild(_openResearch);
        Host.Register(Id + "/openResearch", _openResearch);
    }

    private static OptionButton Dropdown(string name, System.Action<long> selected)
    {
        var picker = new OptionButton
        {
            Name = name,
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        picker.ItemSelected += index => selected(index);
        return picker;
    }

    /// <summary>A dim BBCode note line under a control (the recipe's needs, the hold's storage line).</summary>
    private static RichTextLabel NoteText(string name)
    {
        var note = new RichTextLabel
        {
            Name = name,
            BbcodeEnabled = true,
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        note.AddThemeFontSizeOverride("normal_font_size", UiTheme.SmallSize);
        note.AddThemeColorOverride("default_color", UiTheme.TextDim);
        return note;
    }

    /// <summary>The quantity's "-" or "+": a <see cref="StepButton"/> that repeats while held.</summary>
    private StepButton StepperButton(string name, string text, string tip)
    {
        var button = new StepButton
        {
            Name = name,
            Text = text,
            TooltipText = tip,
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
        // The room the pickers choose their form by, fixed while this building shows: the panel's
        // width (Dialog.Place) less its padding and the scroll bar.
        _room = Mathf.Max(Host.UiSize.X / 3f, 280f) - 2 * UiTheme.Padding - ScrollBarRoom;
        TitleLabel.Text = building.Def.Name;
        _turntable.Show(building.Type, building.Level);
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
        bool storage = def.Storage != StorageRole.None;
        _toggle.Visible = !storage;
        _toggle.On = building.Enabled;
        _toggle.Icon ??= _icons.Icon("power_switch");
        _strip.Show(StripEntries(building));

        bool works = !storage && !def.IsHub;
        _state.Visible = works;
        if (works)
        {
            BuildingStatus status = _sim.BuildingStatusOf(building);
            _state.Text = status.State == BuildingState.Working ? "Working" : "Idle: " + status.IdleReason;
            _stateTone = status.State == BuildingState.Working ? Tone.Normal
                : CalmIdle.Any(calm => status.IdleReason?.StartsWith(calm) == true) ? Tone.Dim
                : Tone.Warning;
            _state.AddThemeColorOverride("font_color", UiTheme.ColorOf(_stateTone));
        }

        _rows.Set("Research", def.Researches ? ResearchText() : null);
        _openResearch.Visible = def.Researches;
        RefreshFuel(building);
        RefreshStorage(building);
        _rows.Set("Buffer", BufferText(building));
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
        _progress.Visible = works && (def.IsCollector || building.Recipe is not null) && !(building.Queues && building.Queue.Count == 0 && !building.Crafting);

        foreach (VBoxContainer section in _sections)
        {
            section.Visible = section.GetChildren().Skip(1).OfType<Control>().Any(control => control.Visible);
        }

        RefreshUpgrade(building);

        _destroy.Visible = !def.Permanent;
        _destroy.Text = _confirmLeft > 0 ? "Confirm destroy" : "Destroy";
        DestroyTip = "Refund: " + RefundText(building);
        if (_destroyHovered)
        {
            Host.ShowHover(_destroy, TipLine.Wrapped(DestroyTip).ToList());
        }
    }

    /// <summary>
    /// The status strip's entries, each only when it applies: Network, each capacity drawn or
    /// supplied, a Battery Bank's charge, the fuel burning, and a collector's truck. (The on/off
    /// toggle sits ahead of them.)
    /// </summary>
    private List<StatusEntry> StripEntries(Building building)
    {
        var entries = new List<StatusEntry>
        {
            building.Online
                ? new StatusEntry("network", _icons.Icon("network"), null, Tone.Good, "Online: connected to the network")
                : new StatusEntry("network", _icons.Icon("network"), null, Tone.Warning, "Offline: outside the network")
        };
        foreach (CapacityType capacity in Simulation.CapacityTypes)
        {
            if (CapacityEntry(building, capacity) is StatusEntry entry)
            {
                entries.Add(entry);
            }
        }
        if (ChargeEntry(building) is StatusEntry charge)
        {
            entries.Add(charge);
        }
        if (BurningEntry(building) is StatusEntry burning)
        {
            entries.Add(burning);
        }
        if (TruckText.CollectorRow(_sim, building) is string truck)
        {
            entries.Add(new StatusEntry("truck", _icons.Icon("truck"), null, _sim.TruckFor(building) is null ? Tone.Warning : Tone.Normal, truck));
        }
        return entries;
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
        _input.Visible = alternative;
        _inputNote.Visible = alternative;
        if (!alternative)
        {
            return;
        }
        Recipe current = PickedRecipe(building)!;
        IReadOnlyList<ResourceType> options = current.OptionsFor(_inputIndex);
        _input.SetOptions(options.Select(option => ResourceDefs.For(option).Name).Prepend("Auto").ToList(), _room);
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

    /// <summary>
    /// The recipe the pickers show: a held processor's held recipe, the one chosen to queue, or a
    /// running processor's own.
    /// </summary>
    private int PickedIndex(Building building)
    {
        return building.Queues ? building.Hold?.RecipeIndex ?? _selected : building.PendingRecipe ?? building.RecipeIndex;
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
            if (building.Hold is not null)
            {
                _sim.SetHold(building.Id, _selected, _sim.DefaultHoldTarget(building, _selected));
            }
        }
        else
        {
            _sim.SetRecipe(building.Id, (int)index);
        }
    }

    private void OnModeSelected(int index)
    {
        if (Building is not Building building || !building.Queues)
        {
            return;
        }
        if (index == 1)
        {
            _sim.SetHold(building.Id, _selected, _sim.DefaultHoldTarget(building, _selected));
        }
        else
        {
            _sim.ClearHold(building.Id);
        }
        Refresh();
    }

    /// <summary>The highest hold target for a resource storage does not limit.</summary>
    private const int MaxHoldTarget = 9999;

    /// <summary>The most a hold's target can step up to: what storage holds of the output plus its room.</summary>
    private int HoldMax(Building building, HoldOrder hold)
    {
        ResourceType output = building.Recipes[hold.RecipeIndex].Outputs[0].Type;
        _sim.Stockpile.TryGetValue(output, out float held);
        return (int)Mathf.Min(MaxHoldTarget, Mathf.Floor(held + _sim.RoomFor(output) + 0.001f));
    }

    private void StepHold(int direction)
    {
        if (Building is not Building building || building.Hold is not HoldOrder hold)
        {
            return;
        }
        int step = Simulation.PerCycle(building.Recipes[hold.RecipeIndex]);
        int target = hold.Target + direction * step;
        target = direction > 0 ? Mathf.Min(target, Mathf.Max(hold.Target, HoldMax(building, hold))) : Mathf.Max(target, step);
        _sim.SetHold(building.Id, hold.RecipeIndex, target);
        Refresh();
    }

    private void OnInputSelected(int index)
    {
        if (Building is not Building building || PickedRecipe(building) is not Recipe recipe || _inputIndex < 0)
        {
            return;
        }
        IReadOnlyList<ResourceType> options = recipe.OptionsFor(_inputIndex);
        _sim.SetInputChoice(building.Id, PickedIndex(building), _inputIndex, index <= 0 ? null : options[index - 1]);
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
        _mode.Visible = queues;
        if (queues)
        {
            _mode.SetOptions(new[] { "Queue", "Hold" }, _room);
        }
        RefreshHold(building);
        queues = queues && building.Hold is null;
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

    /// <summary>A held processor's Mode picker, Hold at row, and storage line ("In storage: 32 / 50 Iron").</summary>
    private void RefreshHold(Building building)
    {
        if (building.Queues)
        {
            _mode.Selected = building.Hold is null ? 0 : 1;
        }
        bool held = building.Queues && building.Hold is not null;
        _holdRow.Visible = held;
        _holdLine.Visible = held;
        if (!held || building.Hold is not HoldOrder hold)
        {
            return;
        }
        Recipe recipe = building.Recipes[hold.RecipeIndex];
        int step = Simulation.PerCycle(recipe);
        _holdLabel.Text = hold.Target.ToString();
        _holdLess.Disabled = hold.Target <= step;
        _holdMore.Disabled = hold.Target + step > HoldMax(building, hold);
        ResourceType output = recipe.Outputs[0].Type;
        _sim.Stockpile.TryGetValue(output, out float stock);
        int have = Mathf.FloorToInt(stock + 0.001f);
        string count = have >= hold.Target ? $"[color=#{UiTheme.Text.ToHtml(false)}]{have}[/color]" : have.ToString();
        string line = $"In storage: {count} / {hold.Target} {ResourceDefs.For(output).Name}";
        if (_holdLine.Text != line)
        {
            _holdLine.Text = line;
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
        int step = PerCycle(building);
        _quantity = Mathf.Clamp(_quantity + direction * step, step, MaxCycles * step);
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

    /// <summary>How a truck's state colors its bay: dim parked, warning while it waits, else normal.</summary>
    private static Tone TruckTone(TruckState state)
    {
        return state switch
        {
            TruckState.Parked => Tone.Dim,
            TruckState.WaitingForPad or TruckState.WaitingForRoom => Tone.Warning,
            _ => Tone.Normal
        };
    }

    /// <summary>
    /// A Depot's bays (a truck icon per bay, colored by its truck's state, an empty bay faint), Buy
    /// truck with what it costs or is short of under it, and Sell truck with what selling gives back.
    /// </summary>
    private void RefreshTrucks(Building building)
    {
        int slots = building.Def.TruckSlots;
        _bays.Visible = slots > 0;
        _truckButtons.Visible = slots > 0;
        _buyNote.Visible = slots > 0;
        if (slots <= 0)
        {
            return;
        }
        List<Truck> trucks = _sim.TrucksOf(building);
        Texture2D? icon = _icons.Icon("truck");
        var bays = new List<StatusEntry>();
        for (int bay = 0; bay < slots; bay++)
        {
            bays.Add(trucks.Find(t => t.Bay == bay) is Truck truck
                ? new StatusEntry(Camel(truck.State.ToString()), icon, null, TruckTone(truck.State), $"Truck {bay + 1}: {TruckText.State(truck)}")
                : new StatusEntry("empty", icon, null, Tone.Dim, "Empty bay", Faint: true));
        }
        _bays.Show(bays);

        string Amounts(IEnumerable<ResourceAmount> amounts) =>
            string.Join(", ", amounts.Select(c => $"{(int)c.Amount} {ResourceDefs.For(c.Type).Name}"));
        Truck? toSell = _sim.TruckToSell(building);
        var back = toSell is null ? new List<ResourceAmount>() : Simulation.RefundOf(toSell).ToList();
        _sellTip = toSell is null ? "No truck to sell"
            : back.Count == 0 ? "Gives nothing back (the truck the Depot came with)"
            : $"Gives back {Amounts(back)}";
        _sellTruck.Disabled = toSell is null;
        _buyTip = $"Costs {Amounts(TruckDefs.Cost)}";
        _buyTruck.Disabled = !_sim.CanBuyTruck(building);
        var shortOf = TruckDefs.Cost.Where(c => _sim.Stockpile.GetValueOrDefault(c.Type) < c.Amount).ToList();
        bool full = trucks.Count >= slots;
        _buyNote.Text = full ? "Every bay is full"
            : shortOf.Count == 0 ? _buyTip
            : string.Join(", ", shortOf.Select(c => $"{(int)c.Amount} {ResourceDefs.For(c.Type).Name} (have {Mathf.FloorToInt(_sim.Stockpile.GetValueOrDefault(c.Type))})"));
        _buyNote.AddThemeColorOverride("font_color", full || shortOf.Count == 0 ? UiTheme.TextDim : UiTheme.Short);
    }

    private static string Camel(string name)
    {
        return char.ToLowerInvariant(name[0]) + name[1..];
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
        _adds.Visible = lines.Count > 0;
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
    /// A capacity entry (Power, Water, Sand): a consumer's draw ("-10") in the capacity's state color
    /// (warning offline or in a brownout, danger in a blackout), or a provider's supply ("+16", less
    /// when its own draws or the daylight slow it; warning offline); dim while switched off; null
    /// when it neither draws nor supplies.
    /// </summary>
    private StatusEntry? CapacityEntry(Building building, CapacityType capacity)
    {
        BuildingDef def = building.Def;
        string id = Camel(capacity.ToString());
        Texture2D? icon = capacity switch
        {
            CapacityType.Power => _icons.Icon("power"),
            CapacityType.Water => _icons.For(ResourceType.Water),
            _ => _icons.For(ResourceType.Sand)
        };
        float draw = def.Draw(capacity);
        if (draw > 0f)
        {
            string value = $"-{draw:0}";
            if (!building.Online)
            {
                return new StatusEntry(id, icon, value, Tone.Warning, $"Draws {draw:0} {capacity}: offline, so it gets none");
            }
            CapacityReading reading = _sim.Capacity(capacity);
            (Tone tone, string tip) = reading.State switch
            {
                CapacityState.Blackout => (Tone.Danger, $"Draws {draw:0} {capacity}: blackout"),
                CapacityState.Brownout => (Tone.Warning, $"Draws {draw:0} {capacity}, running at {reading.Speed * 100f:0}%"),
                _ => (Tone.Normal, $"Draws {draw:0} {capacity}")
            };
            return new StatusEntry(id, icon, value, building.Enabled ? tone : Tone.Dim, tip);
        }
        float supply = def.Supply(capacity);
        if (supply > 0f)
        {
            if (!building.Online)
            {
                return new StatusEntry(id, icon, "+0", Tone.Warning, $"Offline: supplies no {capacity}");
            }
            float speed = _sim.WorkSpeed(building) * (def.Solar ? _sim.Daylight : 1f);
            float supplied = supply * Mathf.Min(speed, 1f);
            string tip = speed < 1f ? $"Supplies {supplied:0.#} of {supply:0} {capacity}" : $"Supplies {supply:0} {capacity}";
            return new StatusEntry(id, icon, $"+{supplied:0.#}", building.Enabled && supplied > 0f ? Tone.Normal : Tone.Dim, tip);
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

    /// <summary>
    /// A Battery Bank's charge entry: how full it is ("53%"), its tooltip what it holds and whether it
    /// is charging, discharging (in the warning color), full, or holding (dim while it cannot work).
    /// </summary>
    private StatusEntry? ChargeEntry(Building building)
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
        Tone tone = !live ? Tone.Dim : doing == "discharging" ? Tone.Warning : Tone.Normal;
        return new StatusEntry("charge", _icons.For(ResourceType.Battery), $"{Mathf.FloorToInt(100f * building.Charge / def.StoreCapacity)}%", tone,
            $"{Mathf.FloorToInt(building.Charge)} / {def.StoreCapacity:0}, {doing}");
    }

    /// <summary>The fuel burning in a burner: its icon and the seconds left ("22 s"), or null while nothing burns.</summary>
    private StatusEntry? BurningEntry(Building building)
    {
        if (!building.Def.BurnsFuel || building.Burning is not ResourceType fuel || building.BurnLeft <= 0f)
        {
            return null;
        }
        int left = Mathf.CeilToInt(building.BurnLeft);
        return new StatusEntry("burning", _icons.For(fuel), $"{left} s", Tone.Normal,
            $"{ResourceDefs.For(fuel).Name}, {left} s left{Boost(Simulation.FuelSpeed(building))}");
    }

    /// <summary>", +30% speed" for a fuel speed over 1, else nothing.</summary>
    private static string Boost(float speed)
    {
        return speed > 1f ? $", +{Mathf.RoundToInt((speed - 1f) * 100f)}% speed" : "";
    }

    /// <summary>The fuel picker and its note naming what Best burns now.</summary>
    private void RefreshFuel(Building building)
    {
        bool burns = building.Def.BurnsFuel;
        _fuel.Visible = burns;
        _fuelNote.Visible = burns;
        if (!burns)
        {
            return;
        }
        // The picker offers the fuels this building takes (a Fission Reactor's Fuel Rods, a fire's
        // Coal, Charcoal, and Wood).
        _fuels = building.Def.Fuels;
        _fuel.SetOptions(_fuels.Select(fuel => ResourceDefs.For(fuel).Name).Prepend("Best").ToList(), _room);
        _fuel.Selected = building.FuelChoice is ResourceType chosen ? 1 + _fuels.ToList().IndexOf(chosen) : 0;
        _fuelNote.Text = building.FuelChoice is null
            ? _sim.BestFuelAt(building) is ResourceType best
                ? $"Best: burns {ResourceDefs.For(best).Name} ({ResourceDefs.For(best).BurnSeconds:0} s per unit{(building.Def.IsProvider ? "" : Boost(ResourceDefs.For(best).FuelSpeed))})"
                : "Best: no fuel at hand"
            : $"Burns only {ResourceDefs.For(building.FuelChoice.Value).Name}";
    }

    private void OnFuelSelected(int index)
    {
        if (Building is Building building)
        {
            _sim.SetFuelChoice(building.Id, index == 0 ? null : _fuels[index - 1]);
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

    // "Upgrade to Drill Rig": the next level's description and its price (the difference in cost),
    // short amounts marked; disabled, with the research it needs, while that level is locked.
    private void RefreshUpgrade(Building building)
    {
        int next = building.Level + 1;
        _upgrade.Visible = next <= BuildingDefs.Levels(building.Type);
        if (!_upgrade.Visible)
        {
            UpgradeTip = new List<TipLine>();
            return;
        }
        _upgrade.Text = "Upgrade to " + BuildingDefs.For(building.Type, next).Name;
        var tip = new List<TipLine>(TipLine.Wrapped(BuildingDefs.DescriptionOf(building.Type, next), UiTheme.TextDim));
        List<TipLine> locked = BuildMenu.LockLines(_sim, building.Type, next);
        if (locked.Count > 0)
        {
            tip.AddRange(locked);
        }
        else
        {
            tip.AddRange(BuildMenu.CostLines(_sim, _sim.UpgradePrice(building)));
        }
        UpgradeTip = tip;
        _upgrade.Disabled = !_sim.CanUpgrade(building);
        if (_upgradeHovered)
        {
            Host.ShowHover(_upgrade, UpgradeTip);
        }
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
