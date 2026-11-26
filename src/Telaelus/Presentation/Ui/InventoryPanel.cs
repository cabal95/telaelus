using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The inventory (I): a panel listing the Cargo pool ("Cargo (40 / 150)"), then an arrow picker
/// ("Resources" for the raw resources, then "Tier 1" to "Tier 4", a tier not yet unlocked skipped)
/// over the chosen one's resources: the discovered first, then the undiscovered, each group in table
/// order. A discovered row shows its icon, "Wood (45 / 100)" (the amount alone for a Cargo or
/// unlimited resource) in the warning color when storage is full, and a Pin toggle for the top bar;
/// an undiscovered row shows its icon as a silhouette and its name in the dim text color, with a
/// tooltip ("Not yet found" for a raw resource, "Not yet made" for an item). Hovering a row
/// highlights it. The panel reopens on the last choice viewed ("Resources" the first time). The rows
/// refresh and reorder every frame while it is open; the body scrolls.
/// </summary>
public partial class InventoryPanel : Dialog
{
    public const float IconSize = 22f;
    public const string NotFound = "Not yet found";
    public const string NotMade = "Not yet made";

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private ResourcePins _pins = null!;
    private Label _cargo = null!;
    private readonly Dictionary<ResourceType, Row> _rows = new();
    private VBoxContainer _list = null!;
    private ArrowPicker _picker = null!;
    private int _choice;

    private sealed record Row(PanelContainer Panel, TextureRect Icon, Label Label, Button Pin);

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "inventory";

    /// <summary>The shown rows in display order (type, its label, amount, section, discovered, its own limit or null, pinned), for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, string Name, int Amount, string Section, bool Discovered, int? Limit, bool Pinned)> Rows =>
        Ordered(_choice)
            .Select(type => (type, _rows[type].Label.Text, HudOverlay.Whole(_sim, type), SectionId(Tier(type)), _sim.IsDiscovered(type),
                HudOverlay.WholeLimit(_sim, type), _pins.IsPinned(type)));

    /// <summary>The arrow picker's choice as shown ("Resources", "Tier 1"), for the snapshot.</summary>
    public string Choice => Label(_choice);

    /// <summary>Whether the arrow picker can step back and forward, for the snapshot.</summary>
    public (bool Prev, bool Next) CanStep => (_picker.CanStep(-1), _picker.CanStep(1));

    /// <summary>The arrow picker's layout (current cell width, right arrow x), for the snapshot.</summary>
    public (float CurrentWidth, float NextX) PickerLayout => _picker.Layout;

    /// <summary>The Cargo row as shown, for the snapshot.</summary>
    public string CargoText => _cargo.Text;

    private static int Tier(ResourceType type) => ResourceDefs.For(type).Tier;

    private static string SectionId(int tier) => tier == 0 ? "raw" : "tier" + tier;

    private static string Label(int tier) => tier == 0 ? "Resources" : BuildMenu.TierLabel(tier);

    /// <summary>A tier's resources: discovered first, then undiscovered, each in table order.</summary>
    private IEnumerable<ResourceType> Ordered(int tier)
    {
        return Enum.GetValues<ResourceType>()
            .Where(type => Tier(type) == tier)
            .OrderBy(type => _sim.IsDiscovered(type) ? 0 : 1);
    }

    public void Initialize(Simulation sim, ResourceIcons icons, ResourcePins pins)
    {
        _sim = sim;
        _icons = icons;
        _pins = pins;
    }

    protected override void Build()
    {
        _cargo = UiTheme.MakeLabel("", UiTheme.BodySize);
        _cargo.MouseFilter = MouseFilterEnum.Ignore;
        Body.AddChild(_cargo);

        _picker = new ArrowPicker(Host, Id + "/tier")
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _picker.SetOptions(Enumerable.Range(0, ResourceDefs.MaxTier + 1).Select(Label).ToList());
        _picker.Selected = _choice;
        _picker.Chosen += index =>
        {
            _choice = index;
            Refresh();
        };
        Body.AddChild(_picker);

        _list = new VBoxContainer
        {
            Name = "Rows",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _list.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_list);
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            _list.AddChild(MakeRow(type));
        }
    }

    private PanelContainer MakeRow(ResourceType type)
    {
        var row = new PanelContainer
        {
            Name = type.ToString(),
            MouseFilter = MouseFilterEnum.Pass
        };
        StyleBoxFlat normal = UiTheme.Box(new Color(0f, 0f, 0f, 0f), UiTheme.CellRadius, padding: 2);
        normal.ContentMarginLeft = UiTheme.Gap;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = UiTheme.CellHover;
        row.AddThemeStyleboxOverride("panel", normal);
        row.MouseEntered += () =>
        {
            row.AddThemeStyleboxOverride("panel", hover);
            if (!_sim.IsDiscovered(type))
            {
                Host.ShowHover(row, new[] { new TipLine(Tier(type) == 0 ? NotFound : NotMade) });
            }
        };
        row.MouseExited += () =>
        {
            row.AddThemeStyleboxOverride("panel", normal);
            Host.HideHover(row);
        };

        var line = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        line.AddThemeConstantOverride("separation", UiTheme.Gap);
        row.AddChild(line);
        TextureRect icon = HudOverlay.MakeIcon(IconSize);
        line.AddChild(icon);
        Label label = UiTheme.MakeLabel("", UiTheme.BodySize);
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        line.AddChild(label);
        var pin = new Button
        {
            Name = "Pin",
            Text = "Pin",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None
        };
        pin.AddThemeFontSizeOverride("font_size", UiTheme.SmallSize);
        pin.Pressed += () => _pins.Toggle(type);
        line.AddChild(pin);
        Host.Register(Id + "/pin/" + type, pin);
        _rows[type] = new Row(row, icon, label, pin);
        Host.Register(Id + "/" + type, row);
        return row;
    }

    public override void Refresh()
    {
        float used = _sim.CargoUsed;
        float pool = _sim.CargoPool;
        _cargo.Text = $"Cargo ({Mathf.FloorToInt(used)} / {Mathf.FloorToInt(pool)})";
        _cargo.AddThemeColorOverride("font_color", pool - used < 1f ? UiTheme.Warning : UiTheme.Text);

        // A tier not yet unlocked is skipped: its items cannot be made.
        for (int tier = 1; tier <= ResourceDefs.MaxTier; tier++)
        {
            _picker.SetVisible(tier, tier <= _sim.UnlockedTier);
        }
        _picker.Selected = _choice;

        int index = 0;
        foreach (ResourceType type in Ordered(_choice))
        {
            Row row = _rows[type];
            if (row.Panel.GetIndex() != index)
            {
                _list.MoveChild(row.Panel, index);
            }
            index++;
            RefreshRow(type, row);
        }
        foreach ((ResourceType type, Row row) in _rows)
        {
            row.Panel.Visible = Tier(type) == _choice;
        }
    }

    private void RefreshRow(ResourceType type, Row row)
    {
        row.Icon.Texture ??= _icons.For(type);
        bool discovered = _sim.IsDiscovered(type);
        bool pinned = _pins.IsPinned(type);
        row.Pin.Visible = discovered && ResourcePins.CanPin(type);
        row.Pin.SetPressedNoSignal(pinned);
        row.Pin.Text = pinned ? "Pinned" : "Pin";
        row.Pin.Disabled = !pinned && _pins.IsFull;
        row.Pin.TooltipText = row.Pin.Disabled ? $"The top bar holds {ResourcePins.Max}: unpin one first" : "";
        if (discovered)
        {
            row.Icon.Modulate = Colors.White;
            row.Label.Text = $"{ResourceDefs.For(type).Name} ({HudOverlay.StorageText(_sim, type)})";
            row.Label.AddThemeColorOverride("font_color", HudOverlay.AmountColor(_sim, type));
        }
        else
        {
            row.Icon.Modulate = UiTheme.Silhouette;
            row.Label.Text = ResourceDefs.For(type).Name;
            row.Label.AddThemeColorOverride("font_color", UiTheme.TextDim);
        }
    }
}
