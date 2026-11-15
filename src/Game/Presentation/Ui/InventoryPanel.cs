using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The inventory (I): a panel listing every resource under tier headings (Raw, then Tier 1
/// to Tier 4). Within a heading the discovered resources come first, then the undiscovered,
/// each group in table order. A discovered row shows its icon and "Iron (23)"; an
/// undiscovered row shows its icon as a silhouette and "Undiscovered" in the dim text color,
/// so the player sees how much is left to find but not what it is. Hovering a row highlights
/// it. The rows refresh and reorder every frame while it is open; the body scrolls.
/// </summary>
public partial class InventoryPanel : Dialog
{
    public const float IconSize = 22f;
    public const string UndiscoveredLabel = "Undiscovered";

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private readonly Dictionary<ResourceType, Row> _rows = new();
    private readonly Dictionary<int, VBoxContainer> _sections = new();

    private sealed record Row(PanelContainer Panel, TextureRect Icon, Label Label);

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "inventory";

    /// <summary>The rows in display order, each with its amount, section, and whether it is discovered, for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, string Section, bool Discovered)> Rows =>
        Enumerable.Range(0, ResourceDefs.MaxTier + 1)
            .SelectMany(Ordered)
            .Select(type => (type, HudOverlay.Whole(_sim, type), SectionId(Tier(type)), _sim.IsDiscovered(type)));

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

    public void Initialize(Simulation sim, ResourceIcons icons)
    {
        _sim = sim;
        _icons = icons;
    }

    protected override void Build()
    {
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
        line.AddChild(label);
        _rows[type] = new Row(row, icon, label);
        Host.Register(Id + "/" + type, row);
        return row;
    }

    public override void Refresh()
    {
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
        if (_sim.IsDiscovered(type))
        {
            row.Icon.Modulate = Colors.White;
            row.Label.Text = $"{ResourceDefs.For(type).Name} ({HudOverlay.Whole(_sim, type)})";
            row.Label.RemoveThemeColorOverride("font_color");
        }
        else
        {
            row.Icon.Modulate = UiTheme.Silhouette;
            row.Label.Text = UndiscoveredLabel;
            row.Label.AddThemeColorOverride("font_color", UiTheme.TextDim);
        }
    }
}
