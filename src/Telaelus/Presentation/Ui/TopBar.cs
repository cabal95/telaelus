using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The top bar: a full-width strip across the top of the window in the panel style.
/// Left, the pinned resources (see <see cref="ResourcePins"/>) in table order, each as an icon and
/// its amount, in the danger color when storage has no room for another unit (dim while it has no
/// storage at all), each with a tooltip naming it with its amount and limit ("Wood 45 / 100"). Right, the clock (a sun icon by day, a moon icon by night,
/// and "14:00"), then each capacity (Power, Water, Sand) once the network has any supply or demand
/// of it: its icon and demand over supply ("45 / 100"), colored by its state, "Blackout:" before it
/// in a blackout; then what Battery Banks hold (the Battery icon and "3200 / 6000", in the warning
/// color while discharging) once there is one. It takes the mouse but no keys.
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
    // Each capacity's readout, in CapacityType order.
    private readonly Dictionary<CapacityType, (HBoxContainer Entry, Label State, TextureRect Icon, Label Amount)> _capacities = new();
    private TextureRect _clockIcon = null!;
    private Label _clock = null!;
    private HBoxContainer _storeEntry = null!;
    private TextureRect _storeIcon = null!;
    private Label _store = null!;

    /// <summary>The entries as shown (type, amount, its own limit or null, whether storage is full, the text, and its tone), for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, int? Limit, bool Full, string Text, string Tone)> Entries => _sim is Simulation sim
        ? _pins.Pinned.Select(type => (type, Whole(sim, type), WholeLimit(sim, type), IsFull(sim, type), PinText(sim, type), ToneOf(PinColor(sim, type))))
        : Enumerable.Empty<(ResourceType, int, int?, bool, string, string)>();

    /// <summary>A capacity's readout as shown ("45 / 100", "Blackout: 140 / 100"), or null while hidden, for the snapshot.</summary>
    public string? CapacityText(CapacityType type)
    {
        (HBoxContainer entry, Label state, _, Label amount) = _capacities[type];
        return entry.Visible ? (state.Visible ? state.Text + " " : "") + amount.Text : null;
    }

    /// <summary>A capacity readout's tone ("normal", "warning", "danger"), or null while hidden, for the snapshot.</summary>
    public string? CapacityTone(CapacityType type)
    {
        (HBoxContainer entry, _, _, Label amount) = _capacities[type];
        return entry.Visible ? ToneOf(amount.GetThemeColor("font_color")) : null;
    }

    /// <summary>The clock as shown ("14:00"), for the snapshot.</summary>
    public string ClockText => _clock.Text;

    /// <summary>What Battery Banks hold as shown ("3200 / 6000"), or null with no bank, for the snapshot.</summary>
    public string? StoreText => _storeEntry.Visible ? _store.Text : null;

    /// <summary>A pin's text: the amount alone; its limit is in the tooltip.</summary>
    public static string PinText(Simulation sim, ResourceType type)
    {
        return Whole(sim, type).ToString();
    }

    /// <summary>
    /// A pin's color: dim while it has no storage at all (Crude Oil before a Tank Farm), the danger
    /// color when the storage it has is full, else normal.
    /// </summary>
    public static Color PinColor(Simulation sim, ResourceType type)
    {
        if (WholeLimit(sim, type) == 0)
        {
            return UiTheme.TextDim;
        }
        return IsFull(sim, type) ? UiTheme.Danger : UiTheme.Text;
    }

    /// <summary>A pin's tooltip: its name and amount, with the limit when it has one of its own ("Wood 45 / 100").</summary>
    private string PinTip(ResourceType type)
    {
        string name = ResourceDefs.For(type).Name;
        return _sim is Simulation sim ? $"{name} {StorageText(sim, type)}" : name;
    }

    /// <summary>The clock's text for a time of day: the hour of a 24-hour day ("14:00"); its sun or moon icon says day or night.</summary>
    public static string ClockFor(float timeOfDay)
    {
        int minutes = (int)(timeOfDay * 24f * 60f) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

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
        // Entries sit two Gaps apart, so the amounts (with no limits beside them) stay easy to tell apart.
        row.AddThemeConstantOverride("separation", UiTheme.Gap * 2);
        bar.AddChild(row);

        _pinRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pinRow.AddThemeConstantOverride("separation", UiTheme.Gap * 2);
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

        foreach (CapacityType type in Simulation.CapacityTypes)
        {
            var entry = new HBoxContainer
            {
                Name = type.ToString(),
                MouseFilter = MouseFilterEnum.Stop
            };
            entry.AddThemeConstantOverride("separation", 3);
            Label state = UiTheme.MakeLabel("Blackout:", UiTheme.BodySize, UiTheme.Danger);
            entry.AddChild(state);
            TextureRect icon = MakeIcon(IconSize);
            icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            entry.AddChild(icon);
            Label amount = UiTheme.MakeLabel("", UiTheme.BodySize);
            amount.MouseFilter = MouseFilterEnum.Ignore;
            entry.AddChild(amount);
            var lines = new[] { new TipLine(type.ToString()) };
            entry.MouseEntered += () => _host.ShowHover(entry, lines, below: true);
            entry.MouseExited += () => _host.HideHover(entry);
            row.AddChild(entry);
            _capacities[type] = (entry, state, icon, amount);
        }

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
            (HBoxContainer entry, TextureRect icon, Label amount) = MakeEntry(type);
            _pinRow.AddChild(entry);
            _entries[type] = (icon, amount);
            _host.Register("topBar/" + type, entry);
        }
    }

    private (HBoxContainer Entry, TextureRect Icon, Label Amount) MakeEntry(ResourceType type)
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
        entry.MouseEntered += () => _host.ShowHover(entry, new[] { new TipLine(PinTip(type)) }, below: true);
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
            amount.Text = PinText(sim, type);
            amount.AddThemeColorOverride("font_color", PinColor(sim, type));
        }

        bool day = sim.Daylight > 0f;
        _clockIcon.Texture = _icons.Icon(day ? "sun" : "moon");
        _clock.Text = ClockFor(sim.TimeOfDay);

        CapacityReading power = sim.Capacity(CapacityType.Power);
        foreach ((CapacityType type, (HBoxContainer entry, Label state, TextureRect icon, Label amount)) in _capacities)
        {
            CapacityReading reading = sim.Capacity(type);
            // Off the bar until the network has something to say about it (a Battery Bank counts for Power).
            entry.Visible = reading.Supply > 0f || reading.Demand > 0f || reading.StoreCapacity > 0f;
            icon.Texture ??= type == CapacityType.Power ? _icons.Icon("power") : _icons.For(type == CapacityType.Water ? ResourceType.Water : ResourceType.Sand);
            state.Visible = reading.State == CapacityState.Blackout;
            amount.Text = $"{Mathf.CeilToInt(reading.Demand)} / {Mathf.FloorToInt(reading.Supply)}";
            amount.AddThemeColorOverride("font_color", CapacityColor(reading.Demand, reading.Supply));
        }

        _storeEntry.Visible = power.StoreCapacity > 0f;
        _storeIcon.Texture ??= _icons.For(ResourceType.Battery);
        _store.Text = $"{Mathf.FloorToInt(power.Stored)} / {Mathf.FloorToInt(power.StoreCapacity)}";
        _store.AddThemeColorOverride("font_color", power.StoreFlow < 0f ? UiTheme.Warning : UiTheme.Text);
    }
}
