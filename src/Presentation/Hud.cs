using System.Collections.Generic;
using System.Text;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Minimal heads-up display: resource totals, the current build selection, and a
/// harvest hint. Pure presentation; it only reads simulation state.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label
        {
            Position = new Vector2(16f, 12f)
        };
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.92f));
        AddChild(_label);
    }

    public void Refresh(
        IReadOnlyDictionary<ResourceType, float> stockpile,
        ResourceNode? harvestTarget,
        BuildingType? selectedBuild)
    {
        var text = new StringBuilder();
        text.Append("Ore: ").Append(Amount(stockpile, ResourceType.Ore));
        text.Append("   Wood: ").Append(Amount(stockpile, ResourceType.Wood));
        text.Append("   Metal: ").Append(Amount(stockpile, ResourceType.Metal));
        text.Append("   Fuel: ").Append(Amount(stockpile, ResourceType.Fuel));

        if (selectedBuild is not null)
        {
            text.Append("\nBuild: ").Append(selectedBuild.Value).Append("   (left-click to place, Esc to cancel)");
        }
        else
        {
            text.Append("\nBuild:  [1] Extractor   [2] Smelter   [3] Refinery");
        }

        if (harvestTarget is not null)
        {
            text.Append("\n[E] harvest ").Append(harvestTarget.Type);
        }

        _label.Text = text.ToString();
    }

    private static int Amount(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceType type)
    {
        stockpile.TryGetValue(type, out float value);
        return Mathf.FloorToInt(value);
    }
}
