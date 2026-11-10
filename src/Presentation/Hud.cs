using System.Collections.Generic;
using System.Text;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Minimal heads-up display: resource totals and a harvest hint when a node is
/// in range. Pure presentation; it only reads simulation state.
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

    public void Refresh(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceNode? harvestTarget)
    {
        var text = new StringBuilder();
        text.Append("Ore: ").Append(Mathf.FloorToInt(Amount(stockpile, ResourceType.Ore)));
        text.Append("    Wood: ").Append(Mathf.FloorToInt(Amount(stockpile, ResourceType.Wood)));
        if (harvestTarget is not null)
        {
            text.Append("\n[E] harvest ").Append(harvestTarget.Type);
        }
        _label.Text = text.ToString();
    }

    private static float Amount(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceType type)
    {
        stockpile.TryGetValue(type, out float value);
        return value;
    }
}
