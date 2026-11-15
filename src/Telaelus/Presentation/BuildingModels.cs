using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Builds a building's model: one per type, authored facing North (front toward
/// -Z) and filling its footprint minus a small inset, origin at the footprint
/// center on the ground. It is turned to the building's facing. Used by both
/// placed buildings and the build ghost, so the two always match.
/// </summary>
public static class BuildingModels
{
    public static Node3D Create(BuildingType type, Facing facing)
    {
        Node3D model = ModelLibrary.Instance("buildings/" + FileName(type), ModelLibrary.EdgeYaw(facing));
        model.Name = type.ToString();
        return model;
    }

    public static string FileName(BuildingType type)
    {
        return type switch
        {
            BuildingType.LumberCamp => "lumber_camp",
            BuildingType.OilWell => "oil_well",
            BuildingType.TankFarm => "tank_farm",
            BuildingType.SmallHub => "small_hub",
            BuildingType.WaterWheel => "water_wheel",
            BuildingType.BurnerGenerator => "burner_generator",
            BuildingType.LumberMill => "lumber_mill",
            BuildingType.DrillRig => "drill_rig",
            BuildingType.WaterPump => "water_pump",
            BuildingType.PumpingStation => "pumping_station",
            BuildingType.SandCollector => "sand_collector",
            BuildingType.SandDredge => "sand_dredge",
            BuildingType.CharcoalKiln => "charcoal_kiln",
            BuildingType.RockCrusher => "rock_crusher",
            BuildingType.DeepWell => "deep_well",
            BuildingType.DeepMine => "deep_mine",
            BuildingType.SteelMill => "steel_mill",
            BuildingType.FuelPlant => "fuel_plant",
            BuildingType.ChemicalPlant => "chemical_plant",
            BuildingType.AluminumSmelter => "aluminum_smelter",
            BuildingType.ElectricFurnace => "electric_furnace",
            BuildingType.ElectronicsWorks => "electronics_works",
            BuildingType.TileKiln => "tile_kiln",
            BuildingType.AssemblyPlant => "assembly_plant",
            BuildingType.CementKiln => "cement_kiln",
            BuildingType.ConcreteMixer => "concrete_mixer",
            BuildingType.BrineWell => "brine_well",
            BuildingType.SemiconductorFab => "semiconductor_fab",
            BuildingType.AirSeparator => "air_separator",
            BuildingType.EnrichmentPlant => "enrichment_plant",
            BuildingType.SolarArray => "solar_array",
            BuildingType.BatteryBank => "battery_bank",
            BuildingType.FissionReactor => "fission_reactor",
            BuildingType.ResearchLab => "research_lab",
            _ => type.ToString().ToLowerInvariant()
        };
    }
}
