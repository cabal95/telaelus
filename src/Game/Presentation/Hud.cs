using System.Collections.Generic;
using System.Text;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Minimal heads-up display: resource totals (deposits on one line, refined goods on
/// the next), launch progress, the current build selection, and a harvest hint. Pure presentation; it only reads simulation state.
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
        AppendLine(text, stockpile, ResourceKind.Deposit);
        text.Append('\n');
        AppendLine(text, stockpile, ResourceKind.Refined);

        text.Append("\nLaunch: Rocket Fuel ").Append(Amount(stockpile, ResourceType.RocketFuel)).Append('/').Append((int)Simulation.LaunchRocketFuelCost);
        text.Append("   Iron ").Append(Amount(stockpile, ResourceType.Iron)).Append('/').Append((int)Simulation.LaunchIronCost);
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
            text.Append("\nBuild:  [1] Mine   [2] Lumber Camp   [3] Smelter   [4] Refinery   [5] Oil Well");
        }

        if (harvestTarget is not null)
        {
            text.Append("\n[E] harvest ").Append(ResourceDefs.For(harvestTarget.Type).Name);
        }

        _label.Text = text.ToString();
    }

    /// <summary>
    /// One line of totals for every resource of a kind, in table order. Terrain-sourced
    /// resources are left out until a building can collect them.
    /// </summary>
    private static void AppendLine(StringBuilder text, IReadOnlyDictionary<ResourceType, float> stockpile, ResourceKind kind)
    {
        bool first = true;
        foreach (ResourceType type in System.Enum.GetValues<ResourceType>())
        {
            ResourceDef def = ResourceDefs.For(type);
            if (def.Kind != kind)
            {
                continue;
            }
            text.Append(first ? "" : "   ").Append(def.Name).Append(": ").Append(Amount(stockpile, type));
            first = false;
        }
    }

    private static int Amount(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceType type)
    {
        stockpile.TryGetValue(type, out float value);
        return Mathf.FloorToInt(value);
    }
}
