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
        // A dark outline keeps the text readable over bright ground such as snow and sand.
        _label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.06f, 0.05f));
        _label.AddThemeConstantOverride("outline_size", 5);
        AddChild(_label);
    }

    public void Refresh(
        IReadOnlyDictionary<ResourceType, float> stockpile,
        ResourceField? harvestTarget,
        BuildingType? selectedBuild,
        Facing facing,
        int fieldsInRange,
        bool canLaunch,
        int shipsLaunched,
        string diagnostics)
    {
        var text = new StringBuilder();
        AppendLine(text, stockpile, ResourceKind.Deposit);
        text.Append('\n');
        AppendLine(text, stockpile, ResourceKind.Refined);
        text.Append('\n').Append(diagnostics);

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

    /// <summary>
    /// The diagnostics line for checking the world while exploring: the world (its seed,
    /// or the bounded map's name), the biome and terrain under the player, and their
    /// chunk and map cell.
    /// </summary>
    public static string Diagnostics(Simulation sim)
    {
        (int x, int z) = Simulation.CellAt(sim.Player.Position.X, sim.Player.Position.Z);
        int mapX = Simulation.MapCellOf(x);
        int mapZ = Simulation.MapCellOf(z);
        (int chunkX, int chunkZ) = Simulation.ChunkAt(sim.Player.Position);
        string world = sim.World.Kind switch
        {
            WorldKind.Generated => "seed " + sim.World.Seed,
            WorldKind.TestMap => "test map",
            _ => "empty map"
        };
        return $"{world}   {sim.MapBiomeAt(mapX, mapZ)} / {sim.MapTerrainAt(mapX, mapZ)}   chunk ({chunkX}, {chunkZ})   cell ({mapX}, {mapZ})";
    }

    private static int Amount(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceType type)
    {
        stockpile.TryGetValue(type, out float value);
        return Mathf.FloorToInt(value);
    }
}
