using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The inventory (I): a panel listing the Cargo pool ("Cargo (40 / 150)"), then every resource
/// under tier headings (Raw, then Tier 1 to Tier 4). Within a heading the discovered resources
/// come first, then the undiscovered, each group in table order. A discovered row shows its
/// icon, "Wood (45 / 100)" (the amount alone for a Cargo or unlimited resource) in the warning
/// color when storage is full, and a Pin toggle for the top bar; an undiscovered row shows its
/// icon as a silhouette and "Undiscovered" in the dim text color, so the player sees how much is
/// left to find but not what it is. Hovering a row highlights it. The rows refresh and reorder
/// every frame while it is open; the body scrolls.
/// </summary>
public partial class InventoryPanel : Dialog
{
    public const float IconSize = 22f;
    public const string UndiscoveredLabel = "Undiscovered";

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private ResourcePins _pins = null!;
    private Label _cargo = null!;
    private readonly Dictionary<ResourceType, Row> _rows = new();
    private readonly Dictionary<int, VBoxContainer> _sections = new();

    private sealed record Row(PanelContainer Panel, TextureRect Icon, Label Label, Button Pin);

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "inventory";

    /// <summary>The rows in display order (type, amount, section, discovered, its own limit or null, pinned), for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, string Section, bool Discovered, int? Limit, bool Pinned)> Rows =>
        Enumerable.Range(0, ResourceDefs.MaxTier + 1)
            .SelectMany(Ordered)
            .Select(type => (type, HudOverlay.Whole(_sim, type), SectionId(Tier(type)), _sim.IsDiscovered(type),
                HudOverlay.WholeLimit(_sim, type), _pins.IsPinned(type)));

    /// <summary>The Cargo row as shown, for the snapshot.</summary>
    public string CargoText => _cargo.Text;

    private static int Tier(ResourceType type) => ResourceDefs.For(type).Tier;

    private static string SectionId(int tier) => tier == 0 ? "raw" : "tier" + tier;

    private static string Heading(int tier) => tier == 0 ? "Raw" : "Tier " + tier;

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

        for (int tier = 0; tier <= ResourceDefs.MaxTier; tier++)
        {
            Label heading = UiTheme.MakeLabel(Heading(tier), UiTheme.SmallSize);
            heading.AddThemeColorOverride("font_color", UiTheme.TextDim);
            heading.MouseFilter = MouseFilterEnum.Ignore;
            Body.AddChild(heading);
            Body.AddChild(new ColorRect
            {
                Color = UiTheme.PanelEdge,
                CustomMinimumSize = new Vector2(0f, 1f),
                MouseFilter = MouseFilterEnum.Ignore
            });
            var section = new VBoxContainer
            {
                Name = SectionId(tier),
                MouseFilter = MouseFilterEnum.Ignore
            };
            section.AddThemeConstantOverride("separation", UiTheme.Gap);
            Body.AddChild(section);
            _sections[tier] = section;
            foreach (ResourceType type in Enum.GetValues<ResourceType>().Where(type => Tier(type) == tier))
            {
                section.AddChild(MakeRow(type));
            }
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
        row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
        row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);

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

        for (int tier = 0; tier <= ResourceDefs.MaxTier; tier++)
        {
            VBoxContainer section = _sections[tier];
            int index = 0;
            foreach (ResourceType type in Ordered(tier))
            {
                Row row = _rows[type];
                if (row.Panel.GetIndex() != index)
                {
                    section.MoveChild(row.Panel, index);
                }
                index++;
                RefreshRow(type, row);
            }
        }
    }

    private void RefreshRow(ResourceType type, Row row)
    {
        row.Icon.Texture ??= _icons.For(type);
        bool discovered = _sim.IsDiscovered(type);
        bool pinned = _pins.IsPinned(type);
        row.Pin.Visible = discovered;
        row.Pin.SetPressedNoSignal(pinned);
        row.Pin.Text = pinned ? "Pinned" : "Pin";
        if (discovered)
        {
            row.Icon.Modulate = Colors.White;
            row.Label.Text = $"{ResourceDefs.For(type).Name} ({HudOverlay.StorageText(_sim, type)})";
            row.Label.AddThemeColorOverride("font_color", HudOverlay.AmountColor(_sim, type));
        }
        else
        {
            row.Icon.Modulate = UiTheme.Silhouette;
            row.Label.Text = UndiscoveredLabel;
            row.Label.AddThemeColorOverride("font_color", UiTheme.TextDim);
        }
    }
}
