using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Builds a building's model: one per type and level, authored facing North (front
/// toward -Z) and filling its footprint minus a small inset, origin at the footprint
/// center on the ground. It is turned to the building's facing. Used by both placed
/// buildings and the build ghost, so the two always match. A leveled family's model is
/// "&lt;family&gt;_&lt;level&gt;" ("mine_2").
/// </summary>
public static class BuildingModels
{
    public static Node3D Create(BuildingType type, Facing facing, int level = 1)
    {
        Node3D model = ModelLibrary.Instance("buildings/" + FileName(type, level), ModelLibrary.EdgeYaw(facing));
        model.Name = type.ToString();
        return model;
    }

    public static string FileName(BuildingType type, int level = 1)
    {
        if (BuildingDefs.Levels(type) > 1)
        {
            return FamilyName(type) + "_" + level;
        }
        return type switch
        {
            BuildingType.TankFarm => "tank_farm",
            BuildingType.SmallHub => "small_hub",
            BuildingType.WaterWheel => "water_wheel",
            BuildingType.BurnerGenerator => "burner_generator",
            BuildingType.RockCrusher => "rock_crusher",
            BuildingType.FuelPlant => "fuel_plant",
            BuildingType.ChemicalPlant => "chemical_plant",
            BuildingType.ElectronicsWorks => "electronics_works",
            BuildingType.AssemblyPlant => "assembly_plant",
            BuildingType.ConcreteMixer => "concrete_mixer",
            BuildingType.BrineWell => "brine_well",
            BuildingType.SemiconductorFab => "semiconductor_fab",
            BuildingType.EnrichmentPlant => "enrichment_plant",
            BuildingType.SolarArray => "solar_array",
            BuildingType.BatteryBank => "battery_bank",
            BuildingType.FissionReactor => "fission_reactor",
            BuildingType.ResearchLab => "research_lab",
            _ => type.ToString().ToLowerInvariant()
        };
    }

    // A leveled family's file prefix: its type name in snake case ("lumber_camp").
    private static string FamilyName(BuildingType type)
    {
        var text = new System.Text.StringBuilder();
        foreach (char c in type.ToString())
        {
            if (char.IsUpper(c) && text.Length > 0)
            {
                text.Append('_');
            }
            text.Append(char.ToLowerInvariant(c));
        }
        return text.ToString();
    }
}
