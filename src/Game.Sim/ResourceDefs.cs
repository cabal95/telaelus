using System;
using System.Collections.Generic;

namespace Game.Sim;

/// <summary>Where a resource comes from.</summary>
public enum ResourceKind
{
    /// <summary>Finite resource fields on the map, gathered by hand or by an extractor.</summary>
    Deposit,

    /// <summary>Never runs out; collected by a building on or beside the right terrain.</summary>
    TerrainSourced,

    /// <summary>Made by a processing building from other resources.</summary>
    Refined
}

/// <summary>
/// Static definition of a resource type: its display name, its kind, its tier (0 for raw,
/// 1 to 4 for made goods), whether the player can gather it by hand (only deposits can
/// be, and not all of them), and for a deposit, its gather time: the seconds to gather
/// one unit by hand, which an extractor divides by its speed.
/// </summary>
public sealed record ResourceDef(string Name, ResourceKind Kind, int Tier, bool HandHarvestable, float? GatherSeconds = null)
{
    /// <summary>Only deposits can be resource fields.</summary>
    public bool HasFields => Kind == ResourceKind.Deposit;
}

/// <summary>The definition table for every resource type.</summary>
public static class ResourceDefs
{
    public const int MaxTier = 4;

    private static readonly Dictionary<ResourceType, ResourceDef> Table = new()
    {
        [ResourceType.Wood] = Deposit("Wood", 3f),
        [ResourceType.Stone] = Deposit("Stone", 4.5f),
        [ResourceType.IronOre] = Deposit("Iron Ore", 6f),
        [ResourceType.CopperOre] = Deposit("Copper Ore", 6f),
        [ResourceType.Coal] = Deposit("Coal", 4.5f),
        [ResourceType.CrudeOil] = Deposit("Crude Oil", 3f, handHarvestable: false),
        [ResourceType.Bauxite] = Deposit("Bauxite", 6f),
        [ResourceType.Limestone] = Deposit("Limestone", 4.5f),
        [ResourceType.Quartz] = Deposit("Quartz", 6f),
        [ResourceType.TitaniumOre] = Deposit("Titanium Ore", 7.5f),
        [ResourceType.Sulfur] = Deposit("Sulfur", 4.5f),
        [ResourceType.RareEarths] = Deposit("Rare Earths", 9f, handHarvestable: false),
        [ResourceType.UraniumOre] = Deposit("Uranium Ore", 9f, handHarvestable: false),
        [ResourceType.Crystals] = Deposit("Crystals", 9f, handHarvestable: false),
        [ResourceType.Sand] = TerrainSourced("Sand"),
        [ResourceType.Water] = TerrainSourced("Water"),
        [ResourceType.Lithium] = TerrainSourced("Lithium"),
        [ResourceType.Iron] = Refined("Iron", 1),
        [ResourceType.RocketFuel] = Refined("Rocket Fuel", 1),
        [ResourceType.Copper] = Refined("Copper", 1),
        [ResourceType.Steel] = Refined("Steel", 1),
        [ResourceType.Glass] = Refined("Glass", 1),
        [ResourceType.Lumber] = Refined("Lumber", 1),
        [ResourceType.Charcoal] = Refined("Charcoal", 1),
        [ResourceType.Cement] = Refined("Cement", 1),
        [ResourceType.Concrete] = Refined("Concrete", 1),
        [ResourceType.Kerosene] = Refined("Kerosene", 1),
        [ResourceType.Plastic] = Refined("Plastic", 1),
        [ResourceType.Hydrogen] = Refined("Hydrogen", 1),
        [ResourceType.LiquidOxygen] = Refined("Liquid Oxygen", 1),
        [ResourceType.Aluminum] = Refined("Aluminum", 1),
        [ResourceType.Silicon] = Refined("Silicon", 1),
        [ResourceType.SulfuricAcid] = Refined("Sulfuric Acid", 1),
        [ResourceType.Rubber] = Refined("Rubber", 1),
        [ResourceType.Titanium] = Refined("Titanium", 1),
        [ResourceType.CarbonFiber] = Refined("Carbon Fiber", 1),
        [ResourceType.EnrichedUranium] = Refined("Enriched Uranium", 1),
        [ResourceType.HullPlate] = Refined("Hull Plate", 2),
        [ResourceType.SteelBeam] = Refined("Steel Beam", 2),
        [ResourceType.CopperWire] = Refined("Copper Wire", 2),
        [ResourceType.Circuit] = Refined("Circuit", 2),
        [ResourceType.InsulatedCable] = Refined("Insulated Cable", 2),
        [ResourceType.MachineParts] = Refined("Machine Parts", 2),
        [ResourceType.SiliconWafer] = Refined("Silicon Wafer", 2),
        [ResourceType.Processor] = Refined("Processor", 2),
        [ResourceType.Magnet] = Refined("Magnet", 2),
        [ResourceType.ElectricMotor] = Refined("Electric Motor", 2),
        [ResourceType.Battery] = Refined("Battery", 2),
        [ResourceType.SolarCell] = Refined("Solar Cell", 2),
        [ResourceType.CompositePanel] = Refined("Composite Panel", 2),
        [ResourceType.CeramicTile] = Refined("Ceramic Tile", 2),
        [ResourceType.AluminumFrame] = Refined("Aluminum Frame", 2),
        [ResourceType.ArmorPlate] = Refined("Armor Plate", 2),
        [ResourceType.ControlModule] = Refined("Control Module", 3),
        [ResourceType.Computer] = Refined("Computer", 3),
        [ResourceType.ThrusterAssembly] = Refined("Thruster Assembly", 3),
        [ResourceType.FuelTank] = Refined("Fuel Tank", 3),
        [ResourceType.HeatShield] = Refined("Heat Shield", 3),
        [ResourceType.FuelRod] = Refined("Fuel Rod", 3),
        [ResourceType.LifeSupportUnit] = Refined("Life Support Unit", 3),
        [ResourceType.SensorArray] = Refined("Sensor Array", 3),
        [ResourceType.HullSection] = Refined("Hull Section", 4),
        [ResourceType.Engine] = Refined("Engine", 4)
    };

    public static ResourceDef For(ResourceType type)
    {
        return Table.TryGetValue(type, out ResourceDef? def)
            ? def
            : throw new ArgumentOutOfRangeException(nameof(type), type, "no resource definition");
    }

    private static ResourceDef Deposit(string name, float gatherSeconds, bool handHarvestable = true)
    {
        return new ResourceDef(name, ResourceKind.Deposit, 0, handHarvestable, gatherSeconds);
    }

    private static ResourceDef TerrainSourced(string name)
    {
        return new ResourceDef(name, ResourceKind.TerrainSourced, 0, false);
    }

    private static ResourceDef Refined(string name, int tier)
    {
        return new ResourceDef(name, ResourceKind.Refined, tier, false);
    }
}
