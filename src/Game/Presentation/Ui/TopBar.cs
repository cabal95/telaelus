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
/// when storage is full, each with a tooltip naming it. Right, the launch progress (Rocket Fuel and Iron against
/// the launch cost, short amounts in the short color), "[L] Launch ship" once both are
/// met, and the ships launched once there is one. It takes the mouse but no keys.
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
    private TextureRect _fuelIcon = null!;
    private TextureRect _ironIcon = null!;
    private Label _fuel = null!;
    private Label _iron = null!;
    private Label _launch = null!;
    private Label _ships = null!;

    /// <summary>The entries as shown (type, amount, its own limit or null, and whether storage is full), for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, int? Limit, bool Full)> Entries => _sim is Simulation sim
        ? _pins.Pinned.Select(type => (type, Whole(sim, type), WholeLimit(sim, type), IsFull(sim, type)))
        : Enumerable.Empty<(ResourceType, int, int?, bool)>();

    /// <summary>The launch progress as shown, for the snapshot.</summary>
    public string LaunchText => string.Join("   ", new[] { _fuel.Text, _iron.Text, _launch.Visible ? _launch.Text : "", _ships.Visible ? _ships.Text : "" }
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
        row.AddThemeConstantOverride("separation", UiTheme.Padding);
        bar.AddChild(row);

        _pinRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pinRow.AddThemeConstantOverride("separation", UiTheme.Padding);
        row.AddChild(_pinRow);
        RebuildPins();

        row.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        });

        (HBoxContainer fuelEntry, _fuelIcon, _fuel) = MakeEntry(ResourceType.RocketFuel, "Rocket Fuel for a launch");
        row.AddChild(fuelEntry);
        (HBoxContainer ironEntry, _ironIcon, _iron) = MakeEntry(ResourceType.Iron, "Iron for a launch");
        row.AddChild(ironEntry);
        _launch = UiTheme.MakeLabel("[L] Launch ship", UiTheme.BodySize, UiTheme.Accent);
        row.AddChild(_launch);
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
        entry.AddThemeConstantOverride("separation", 4);
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
            amount.AddThemeColorOverride("font_color", IsFull(sim, type) ? UiTheme.Warning : UiTheme.Text);
        }

        _fuelIcon.Texture ??= _icons.For(ResourceType.RocketFuel);
        _ironIcon.Texture ??= _icons.For(ResourceType.Iron);
        SetProgress(_fuel, Whole(sim, ResourceType.RocketFuel), (int)Simulation.LaunchRocketFuelCost);
        SetProgress(_iron, Whole(sim, ResourceType.Iron), (int)Simulation.LaunchIronCost);
        _launch.Visible = sim.CanLaunchShip();
        _ships.Visible = sim.ShipsLaunched > 0;
        _ships.Text = "Ships: " + sim.ShipsLaunched;
    }

    private static void SetProgress(Label label, int have, int need)
    {
        label.Text = $"{have} / {need}";
        label.AddThemeColorOverride("font_color", have < need ? UiTheme.Short : UiTheme.Text);
    }
}
