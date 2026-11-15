using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The top bar: a full-width strip across the top of the window in the panel style.
/// Left, the pinned resources (see <see cref="ResourcePins"/>) in table order, each as an icon and
/// "amount / limit" (the amount alone for a Cargo or unlimited resource), in the warning color
/// when storage is full, each with a tooltip naming it. Right, the clock (a sun icon by day, a
/// moon icon by night, and "14:00"), the network's power (a lightning-bolt icon and "45 / 100", colored by its state,
/// "Blackout:" before it in a blackout), what Battery Banks hold (the Battery icon and "3200 / 6000",
/// in the warning color while discharging) once there is one, then the launch progress (the ship
/// bill, short amounts in the short color), "[L] Launch ship" once it can launch, and the ships
/// launched once there is one. It takes the mouse but no keys.
/// </summary>
public partial class TopBar : HudOverlay
{
    public const float Height = 36f;
    public const float IconSize = 22f;

    private DialogHost _host = null!;
    private ResourceIcons _icons = null!;
    private ResourcePins _pins = null!;
    private Simulation? _sim;
    private HBoxContainer _pinRow = null!;
    private readonly Dictionary<ResourceType, (TextureRect Icon, Label Amount)> _entries = new();
    // The ship bill's entries, in bill order.
    private readonly List<(ResourceType Type, float Need, TextureRect Icon, Label Amount)> _bill = new();
    private Label _shipyardNote = null!;
    private Label _launch = null!;
    private Label _power = null!;
    private Label _powerState = null!;
    private TextureRect _powerIcon = null!;
    private TextureRect _clockIcon = null!;
    private Label _clock = null!;
    private HBoxContainer _storeEntry = null!;
    private TextureRect _storeIcon = null!;
    private Label _store = null!;
    private Label _ships = null!;

    /// <summary>The entries as shown (type, amount, its own limit or null, whether storage is full, the text, and its tone), for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, int? Limit, bool Full, string Text, string Tone)> Entries => _sim is Simulation sim
        ? _pins.Pinned.Select(type => (type, Whole(sim, type), WholeLimit(sim, type), IsFull(sim, type), StorageText(sim, type), ToneOf(AmountColor(sim, type))))
        : Enumerable.Empty<(ResourceType, int, int?, bool, string, string)>();

    /// <summary>The power readout as shown ("45 / 100", "Blackout: 140 / 100"), for the snapshot.</summary>
    public string PowerText => (_powerState.Visible ? _powerState.Text + " " : "") + _power.Text;

    /// <summary>The clock as shown ("14:00"), for the snapshot.</summary>
    public string ClockText => _clock.Text;

    /// <summary>What Battery Banks hold as shown ("3200 / 6000"), or null with no bank, for the snapshot.</summary>
    public string? StoreText => _storeEntry.Visible ? _store.Text : null;

    /// <summary>The clock's text for a time of day: the hour of a 24-hour day ("14:00"); its sun or moon icon says day or night.</summary>
    public static string ClockFor(float timeOfDay)
    {
        int minutes = (int)(timeOfDay * 24f * 60f) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>The launch progress as shown, for the snapshot.</summary>
    public string LaunchText => string.Join("   ", _bill.Where(e => e.Amount.IsVisibleInTree()).Select(e => e.Amount.Text)
        .Concat(new[] { _launch.Visible ? _launch.Text : "", _shipyardNote.Visible ? _shipyardNote.Text : "", _ships.Visible ? _ships.Text : "" })
        .Where(text => text.Length > 0));

    public void Initialize(DialogHost host, ResourceIcons icons, ResourcePins pins)
    {
        _host = host;
        _icons = icons;
        _pins = pins;
        _pins.Changed += RebuildPins;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var bar = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            MouseForcePassScrollEvents = false
        };
        StyleBoxFlat style = UiTheme.Box(UiTheme.Panel, 0);
        style.BorderColor = UiTheme.PanelEdge;
        style.BorderWidthBottom = 1;
        style.ContentMarginLeft = UiTheme.Padding;
        style.ContentMarginRight = UiTheme.Padding;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        bar.AddThemeStyleboxOverride("panel", style);
        bar.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bar);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        // Sections sit a Gap apart so the pins, clock, power, store, and ship bill fit at 1280 wide.
        row.AddThemeConstantOverride("separation", UiTheme.Gap);
        bar.AddChild(row);

        _pinRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pinRow.AddThemeConstantOverride("separation", UiTheme.Gap);
        row.AddChild(_pinRow);
        RebuildPins();

        row.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        });

        var clockEntry = new HBoxContainer
        {
            Name = "Clock",
            MouseFilter = MouseFilterEnum.Ignore
        };
        clockEntry.AddThemeConstantOverride("separation", 3);
        _clockIcon = MakeIcon(IconSize);
        _clockIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        clockEntry.AddChild(_clockIcon);
        _clock = UiTheme.MakeLabel("", UiTheme.BodySize);
        clockEntry.AddChild(_clock);
        row.AddChild(clockEntry);

        var powerEntry = new HBoxContainer
        {
            Name = "Power",
            MouseFilter = MouseFilterEnum.Ignore
        };
        powerEntry.AddThemeConstantOverride("separation", 3);
        _powerState = UiTheme.MakeLabel("Blackout:", UiTheme.BodySize, UiTheme.Danger);
        powerEntry.AddChild(_powerState);
        _powerIcon = MakeIcon(IconSize);
        _powerIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        powerEntry.AddChild(_powerIcon);
        _power = UiTheme.MakeLabel("", UiTheme.BodySize);
        powerEntry.AddChild(_power);
        row.AddChild(powerEntry);

        _storeEntry = new HBoxContainer
        {
            Name = "Store",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _storeEntry.AddThemeConstantOverride("separation", 3);
        _storeIcon = MakeIcon(IconSize);
        _storeIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _storeEntry.AddChild(_storeIcon);
        _store = UiTheme.MakeLabel("", UiTheme.BodySize);
        _storeEntry.AddChild(_store);
        row.AddChild(_storeEntry);
        // The ship bill sits closer together than the gaps between sections, so the bar fits it.
        var billRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        billRow.AddThemeConstantOverride("separation", UiTheme.Gap);
        row.AddChild(billRow);
        foreach (ResourceAmount cost in Simulation.LaunchCost)
        {
            (HBoxContainer entry, TextureRect icon, Label amount) = MakeEntry(cost.Type, ResourceDefs.For(cost.Type).Name + " for a launch");
            billRow.AddChild(entry);
            _bill.Add((cost.Type, cost.Amount, icon, amount));
        }
        _launch = UiTheme.MakeLabel("[L] Launch ship", UiTheme.BodySize, UiTheme.Accent);
        row.AddChild(_launch);
        _shipyardNote = UiTheme.MakeLabel("Build a Shipyard to launch", UiTheme.BodySize, UiTheme.Warning);
        row.AddChild(_shipyardNote);
        _ships = UiTheme.MakeLabel("", UiTheme.BodySize);
        row.AddChild(_ships);
    }

    private void RebuildPins()
    {
        foreach (Node child in _pinRow.GetChildren())
        {
            _pinRow.RemoveChild(child);
            child.QueueFree();
        }
        foreach (ResourceType type in _entries.Keys)
        {
            _host.Unregister("topBar/" + type);
        }
        _entries.Clear();
        foreach (ResourceType type in _pins.Pinned)
        {
            (HBoxContainer entry, TextureRect icon, Label amount) = MakeEntry(type, ResourceDefs.For(type).Name);
            _pinRow.AddChild(entry);
            _entries[type] = (icon, amount);
            _host.Register("topBar/" + type, entry);
        }
    }

    private (HBoxContainer Entry, TextureRect Icon, Label Amount) MakeEntry(ResourceType type, string tooltip)
    {
        var entry = new HBoxContainer
        {
            Name = type.ToString(),
            MouseFilter = MouseFilterEnum.Stop
        };
        entry.AddThemeConstantOverride("separation", 3);
        TextureRect icon = MakeIcon(IconSize);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        entry.AddChild(icon);
        Label amount = UiTheme.MakeLabel("0", UiTheme.BodySize);
        amount.MouseFilter = MouseFilterEnum.Ignore;
        entry.AddChild(amount);
        var lines = new[] { new TipLine(tooltip) };
        entry.MouseEntered += () => _host.ShowHover(entry, lines, below: true);
        entry.MouseExited += () => _host.HideHover(entry);
        return (entry, icon, amount);
    }

    public override void Refresh(Simulation sim, Vector2 uiSize)
    {
        Position = Vector2.Zero;
        Size = new Vector2(uiSize.X, Height);

        _sim = sim;
        foreach ((ResourceType type, (TextureRect icon, Label amount)) in _entries)
        {
            icon.Texture ??= _icons.For(type);
            amount.Text = StorageText(sim, type);
            amount.AddThemeColorOverride("font_color", AmountColor(sim, type));
        }

        bool day = sim.Daylight > 0f;
        _clockIcon.Texture = _icons.Icon(day ? "icon_sun" : "icon_moon");
        _clock.Text = ClockFor(sim.TimeOfDay);

        CapacityReading power = sim.Capacity(CapacityType.Power);
        _powerIcon.Texture ??= _icons.Icon("icon_power");
        _powerState.Visible = power.State == CapacityState.Blackout;
        _power.Text = $"{Mathf.CeilToInt(power.Demand)} / {Mathf.FloorToInt(power.Supply)}";
        _power.AddThemeColorOverride("font_color", CapacityColor(power.Demand, power.Supply));

        _storeEntry.Visible = power.StoreCapacity > 0f;
        _storeIcon.Texture ??= _icons.For(ResourceType.Battery);
        _store.Text = $"{Mathf.FloorToInt(power.Stored)} / {Mathf.FloorToInt(power.StoreCapacity)}";
        _store.AddThemeColorOverride("font_color", power.StoreFlow < 0f ? UiTheme.Warning : UiTheme.Text);
        // Once the whole bill is met its entries give way to the launch (or the Shipyard note), so
        // the bar never has to fit both.
        bool met = sim.LaunchBillMet;
        foreach ((ResourceType type, float need, TextureRect icon, Label amount) in _bill)
        {
            icon.Texture ??= _icons.For(type);
            SetProgress(amount, Whole(sim, type), (int)need);
            ((Control)amount.GetParent()).Visible = !met;
        }
        _launch.Visible = sim.CanLaunchShip();
        _shipyardNote.Visible = sim.LaunchBillMet && sim.LaunchShipyard is null;
        _ships.Visible = sim.ShipsLaunched > 0;
        _ships.Text = "Ships: " + sim.ShipsLaunched;
    }

    private static void SetProgress(Label label, int have, int need)
    {
        label.Text = $"{have} / {need}";
        label.AddThemeColorOverride("font_color", have < need ? UiTheme.Short : UiTheme.Text);
    }
}
