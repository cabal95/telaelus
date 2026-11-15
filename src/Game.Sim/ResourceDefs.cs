using System;
using System.Collections.Generic;
using System.Linq;

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
public sealed record ResourceDef(string Name, ResourceKind Kind, int Tier, ResourceStorage Storage, bool HandHarvestable, float? GatherSeconds = null)
{
    /// <summary>A fuel's burn time: how long one unit burns in a fuel slot; null for a resource that is not a fuel.</summary>
    public float? BurnSeconds { get; init; }

    /// <summary>Only deposits can be resource fields.</summary>
    public bool HasFields => Kind == ResourceKind.Deposit;
}

/// <summary>The definition table for every resource type.</summary>
public static class ResourceDefs
{
    public const int MaxTier = 4;

    private static readonly Dictionary<ResourceType, ResourceDef> Table = new()
    {
        [ResourceType.Wood] = Deposit("Wood", 3f, ResourceStorage.Bulk(100f, 300f, 200f)) with
        {
            BurnSeconds = 10f
        },
        [ResourceType.Stone] = Deposit("Stone", 4.5f, ResourceStorage.Bulk(100f, 300f, 200f)),
        [ResourceType.IronOre] = Deposit("Iron Ore", 6f, ResourceStorage.Bulk(60f, 200f, 120f)),
        [ResourceType.CopperOre] = Deposit("Copper Ore", 6f, ResourceStorage.Bulk(60f, 200f, 120f)),
        [ResourceType.Coal] = Deposit("Coal", 4.5f, ResourceStorage.Bulk(60f, 200f, 120f)) with
        {
            BurnSeconds = 40f
        },
        [ResourceType.CrudeOil] = Deposit("Crude Oil", 3f, ResourceStorage.Fluid(300f), handHarvestable: false),
        [ResourceType.Bauxite] = Deposit("Bauxite", 6f, ResourceStorage.Bulk(0f, 150f, 80f)),
        [ResourceType.Limestone] = Deposit("Limestone", 4.5f, ResourceStorage.Bulk(60f, 200f, 120f)),
        [ResourceType.Quartz] = Deposit("Quartz", 6f, ResourceStorage.Bulk(0f, 150f, 80f)),
        [ResourceType.TitaniumOre] = Deposit("Titanium Ore", 7.5f, ResourceStorage.Bulk(0f, 150f, 80f)),
        [ResourceType.Sulfur] = Deposit("Sulfur", 4.5f, ResourceStorage.Bulk(0f, 150f, 80f)),
        [ResourceType.RareEarths] = Deposit("Rare Earths", 9f, ResourceStorage.Secure(50f), handHarvestable: false),
        [ResourceType.UraniumOre] = Deposit("Uranium Ore", 9f, ResourceStorage.Secure(30f), handHarvestable: false),
        [ResourceType.Crystals] = Deposit("Crystals", 9f, ResourceStorage.Secure(30f), handHarvestable: false),
        [ResourceType.Sand] = TerrainSourced("Sand", ResourceStorage.Unlimited),
        [ResourceType.Water] = TerrainSourced("Water", ResourceStorage.Unlimited),
        [ResourceType.Lithium] = TerrainSourced("Lithium", ResourceStorage.Bulk(0f, 100f, 50f)),
        [ResourceType.Iron] = Refined("Iron", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.RocketFuel] = Refined("Rocket Fuel", 1, ResourceStorage.Fluid(300f)),
        [ResourceType.Copper] = Refined("Copper", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Steel] = Refined("Steel", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Glass] = Refined("Glass", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Lumber] = Refined("Lumber", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Charcoal] = Refined("Charcoal", 1, ResourceStorage.Bulk(60f, 200f, 120f)) with
        {
            BurnSeconds = 30f
        },
        [ResourceType.Cement] = Refined("Cement", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Concrete] = Refined("Concrete", 1, ResourceStorage.Pooled(2f)),
        [ResourceType.Kerosene] = Refined("Kerosene", 1, ResourceStorage.Fluid(200f)),
        [ResourceType.Plastic] = Refined("Plastic", 1, ResourceStorage.Pooled(0.5f)),
        [ResourceType.Hydrogen] = Refined("Hydrogen", 1, ResourceStorage.Fluid(200f)),
        [ResourceType.LiquidOxygen] = Refined("Liquid Oxygen", 1, ResourceStorage.Fluid(300f)),
        [ResourceType.Aluminum] = Refined("Aluminum", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Silicon] = Refined("Silicon", 1, ResourceStorage.Pooled(0.5f)),
        [ResourceType.SulfuricAcid] = Refined("Sulfuric Acid", 1, ResourceStorage.Fluid(150f)),
        [ResourceType.Rubber] = Refined("Rubber", 1, ResourceStorage.Pooled(0.5f)),
        [ResourceType.Titanium] = Refined("Titanium", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.CarbonFiber] = Refined("Carbon Fiber", 1, ResourceStorage.Pooled(0.5f)),
        [ResourceType.EnrichedUranium] = Refined("Enriched Uranium", 1, ResourceStorage.Secure(20f)),
        [ResourceType.HullPlate] = Refined("Hull Plate", 2, ResourceStorage.Pooled(2f)),
        [ResourceType.SteelBeam] = Refined("Steel Beam", 2, ResourceStorage.Pooled(3f)),
        [ResourceType.CopperWire] = Refined("Copper Wire", 2, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Circuit] = Refined("Circuit", 2, ResourceStorage.Pooled(0.25f)),
        [ResourceType.InsulatedCable] = Refined("Insulated Cable", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.MachineParts] = Refined("Machine Parts", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.SiliconWafer] = Refined("Silicon Wafer", 2, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Processor] = Refined("Processor", 2, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Magnet] = Refined("Magnet", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.ElectricMotor] = Refined("Electric Motor", 2, ResourceStorage.Pooled(2f)),
        [ResourceType.Battery] = Refined("Battery", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.SolarCell] = Refined("Solar Cell", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.CompositePanel] = Refined("Composite Panel", 2, ResourceStorage.Pooled(2f)),
        [ResourceType.CeramicTile] = Refined("Ceramic Tile", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.AluminumFrame] = Refined("Aluminum Frame", 2, ResourceStorage.Pooled(3f)),
        [ResourceType.ArmorPlate] = Refined("Armor Plate", 2, ResourceStorage.Pooled(3f)),
        [ResourceType.ControlModule] = Refined("Control Module", 3, ResourceStorage.Pooled(4f)),
        [ResourceType.Computer] = Refined("Computer", 3, ResourceStorage.Pooled(2f)),
        [ResourceType.ThrusterAssembly] = Refined("Thruster Assembly", 3, ResourceStorage.Pooled(8f)),
        [ResourceType.FuelTank] = Refined("Fuel Tank", 3, ResourceStorage.Pooled(10f)),
        [ResourceType.HeatShield] = Refined("Heat Shield", 3, ResourceStorage.Pooled(6f)),
        [ResourceType.FuelRod] = Refined("Fuel Rod", 3, ResourceStorage.Secure(20f)),
        [ResourceType.LifeSupportUnit] = Refined("Life Support Unit", 3, ResourceStorage.Pooled(6f)),
        [ResourceType.SensorArray] = Refined("Sensor Array", 3, ResourceStorage.Pooled(4f)),
        [ResourceType.HullSection] = Refined("Hull Section", 4, ResourceStorage.Pooled(20f)),
        [ResourceType.Engine] = Refined("Engine", 4, ResourceStorage.Pooled(25f))
    };

    /// <summary>The fuels, longest-burning first (the order the Best fuel choice tries them).</summary>
    public static readonly IReadOnlyList<ResourceType> Fuels = Enum.GetValues<ResourceType>()
        .Where(type => For(type).BurnSeconds is not null)
        .OrderByDescending(type => For(type).BurnSeconds)
        .ToList();

    public static ResourceDef For(ResourceType type)
    {
        return Table.TryGetValue(type, out ResourceDef? def)
            ? def
            : throw new ArgumentOutOfRangeException(nameof(type), type, "no resource definition");
    }

    private static ResourceDef Deposit(string name, float gatherSeconds, ResourceStorage storage, bool handHarvestable = true)
    {
        return new ResourceDef(name, ResourceKind.Deposit, 0, storage, handHarvestable, gatherSeconds);
    }

    private static ResourceDef TerrainSourced(string name, ResourceStorage storage)
    {
        return new ResourceDef(name, ResourceKind.TerrainSourced, 0, storage, false);
    }

    private static ResourceDef Refined(string name, int tier, ResourceStorage storage)
    {
        return new ResourceDef(name, ResourceKind.Refined, tier, storage, false);
    }
}
