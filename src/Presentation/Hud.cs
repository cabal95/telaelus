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
        ResourceField? harvestTarget,
        BuildingType? selectedBuild,
        Facing facing,
        int fieldsInRange,
        bool canLaunch,
        int shipsLaunched)
    {
        var text = new StringBuilder();
        text.Append("Ore: ").Append(Amount(stockpile, ResourceType.Ore));
        text.Append("   Wood: ").Append(Amount(stockpile, ResourceType.Wood));
        text.Append("   Metal: ").Append(Amount(stockpile, ResourceType.Metal));
        text.Append("   Fuel: ").Append(Amount(stockpile, ResourceType.Fuel));

        text.Append("\nLaunch: Fuel ").Append(Amount(stockpile, ResourceType.Fuel)).Append('/').Append((int)Simulation.LaunchFuelCost);
        text.Append("   Metal ").Append(Amount(stockpile, ResourceType.Metal)).Append('/').Append((int)Simulation.LaunchMetalCost);
        if (canLaunch)
        {
            text.Append("   [L] LAUNCH SHIP");
        }
        if (shipsLaunched > 0)
        {
            text.Append("   (launched: ").Append(shipsLaunched).Append(')');
        }

        if (selectedBuild is not null)
        {
            BuildingDef def = BuildingDefs.For(selectedBuild.Value);
            text.Append("\nBuild: ").Append(def.Name).Append("   facing ").Append(facing);
            if (def.IsExtractor)
            {
                text.Append("   fields in range: ").Append(fieldsInRange);
            }
            text.Append("   (left-click to place, R to rotate, Esc to cancel)");
        }
        else
        {
            text.Append("\nBuild:  [1] Mine   [2] Lumber Camp   [3] Smelter   [4] Refinery");
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
