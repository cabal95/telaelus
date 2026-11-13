using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The inventory (I): a panel listing every resource, including those at 0, as its icon
/// and "Iron (23)". Raw resources (deposits, then terrain-sourced) come first in table
/// order, then a thin rule, then the refined ones. Hovering a row highlights it. The
/// rows refresh every frame while it is open.
/// </summary>
public partial class InventoryPanel : Dialog
{
    public const float IconSize = 22f;

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private readonly Dictionary<ResourceType, (TextureRect Icon, Label Label)> _rows = new();

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "inventory";

    /// <summary>The rows in order, each with its amount and section, for the snapshot.</summary>
    public IEnumerable<(ResourceType Type, int Amount, string Section)> Rows =>
        Order.Select(type => (type, HudOverlay.Whole(_sim, type), IsRaw(type) ? "raw" : "other"));

    private static readonly List<ResourceType> Order = Enum.GetValues<ResourceType>()
        .OrderBy(type => IsRaw(type) ? 0 : 1)
        .ToList();

    private static bool IsRaw(ResourceType type) => ResourceDefs.For(type).Kind != ResourceKind.Refined;

    public void Initialize(Simulation sim, ResourceIcons icons)
    {
        _sim = sim;
        _icons = icons;
    }

    protected override void Build()
    {
        bool ruled = false;
        foreach (ResourceType type in Order)
        {
            if (!IsRaw(type) && !ruled)
            {
                Body.AddChild(new ColorRect
                {
                    Color = UiTheme.PanelEdge,
                    CustomMinimumSize = new Vector2(0f, 1f),
                    MouseFilter = MouseFilterEnum.Ignore
                });
                ruled = true;
            }
            Body.AddChild(MakeRow(type));
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
        _rows[type] = (icon, label);
        Host.Register(Id + "/" + type, row);
        return row;
    }

    public override void Refresh()
    {
        foreach ((ResourceType type, (TextureRect icon, Label label)) in _rows)
        {
            icon.Texture ??= _icons.For(type);
            label.Text = $"{ResourceDefs.For(type).Name} ({HudOverlay.Whole(_sim, type)})";
        }
    }
}
