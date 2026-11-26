using System;
using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim;

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

    /// <summary>
    /// How fast a building that makes things works while this fuel burns, against Wood's 1; a
    /// provider supplies the same on any fuel (buildings.md).
    /// </summary>
    public float FuelSpeed { get; init; } = 1f;

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
            BurnSeconds = 40f,
            FuelSpeed = 1.3f
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
        [ResourceType.Copper] = Refined("Copper", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Glass] = Refined("Glass", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Lumber] = Refined("Lumber", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.Charcoal] = Refined("Charcoal", 1, ResourceStorage.Bulk(60f, 200f, 120f)) with
        {
            BurnSeconds = 30f,
            FuelSpeed = 1.15f
        },
        [ResourceType.Cement] = Refined("Cement", 1, ResourceStorage.Pooled(1f)),
        [ResourceType.RocketFuel] = Refined("Rocket Fuel", 2, ResourceStorage.Fluid(300f)),
        [ResourceType.Steel] = Refined("Steel", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.Concrete] = Refined("Concrete", 2, ResourceStorage.Pooled(2f)),
        [ResourceType.Kerosene] = Refined("Kerosene", 2, ResourceStorage.Fluid(200f)),
        [ResourceType.Plastic] = Refined("Plastic", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.Hydrogen] = Refined("Hydrogen", 2, ResourceStorage.Fluid(200f)),
        [ResourceType.LiquidOxygen] = Refined("Liquid Oxygen", 2, ResourceStorage.Fluid(300f)),
        [ResourceType.Aluminum] = Refined("Aluminum", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.Silicon] = Refined("Silicon", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.SulfuricAcid] = Refined("Sulfuric Acid", 2, ResourceStorage.Fluid(150f)),
        [ResourceType.Rubber] = Refined("Rubber", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.Titanium] = Refined("Titanium", 2, ResourceStorage.Pooled(1f)),
        [ResourceType.CarbonFiber] = Refined("Carbon Fiber", 2, ResourceStorage.Pooled(0.5f)),
        [ResourceType.HullPlate] = Refined("Hull Plate", 3, ResourceStorage.Pooled(2f)),
        [ResourceType.SteelBeam] = Refined("Steel Beam", 3, ResourceStorage.Pooled(3f)),
        [ResourceType.CopperWire] = Refined("Copper Wire", 3, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Circuit] = Refined("Circuit", 3, ResourceStorage.Pooled(0.25f)),
        [ResourceType.InsulatedCable] = Refined("Insulated Cable", 3, ResourceStorage.Pooled(0.5f)),
        [ResourceType.MachineParts] = Refined("Machine Parts", 3, ResourceStorage.Pooled(1f)),
        [ResourceType.SiliconWafer] = Refined("Silicon Wafer", 3, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Processor] = Refined("Processor", 3, ResourceStorage.Pooled(0.25f)),
        [ResourceType.Magnet] = Refined("Magnet", 3, ResourceStorage.Pooled(0.5f)),
        [ResourceType.ElectricMotor] = Refined("Electric Motor", 3, ResourceStorage.Pooled(2f)),
        [ResourceType.Battery] = Refined("Battery", 3, ResourceStorage.Pooled(1f)),
        [ResourceType.SolarCell] = Refined("Solar Cell", 3, ResourceStorage.Pooled(1f)),
        [ResourceType.CompositePanel] = Refined("Composite Panel", 3, ResourceStorage.Pooled(2f)),
        [ResourceType.CeramicTile] = Refined("Ceramic Tile", 3, ResourceStorage.Pooled(0.5f)),
        [ResourceType.AluminumFrame] = Refined("Aluminum Frame", 3, ResourceStorage.Pooled(3f)),
        [ResourceType.ArmorPlate] = Refined("Armor Plate", 3, ResourceStorage.Pooled(3f)),
        [ResourceType.EnrichedUranium] = Refined("Enriched Uranium", 4, ResourceStorage.Secure(20f)),
        [ResourceType.ControlModule] = Refined("Control Module", 4, ResourceStorage.Pooled(4f)),
        [ResourceType.Computer] = Refined("Computer", 4, ResourceStorage.Pooled(2f)),
        [ResourceType.ThrusterAssembly] = Refined("Thruster Assembly", 4, ResourceStorage.Pooled(8f)),
        [ResourceType.FuelTank] = Refined("Fuel Tank", 4, ResourceStorage.Pooled(10f)),
        [ResourceType.HeatShield] = Refined("Heat Shield", 4, ResourceStorage.Pooled(6f)),
        [ResourceType.FuelRod] = Refined("Fuel Rod", 4, ResourceStorage.Secure(20f)) with
        {
            BurnSeconds = 300f
        },
        [ResourceType.LifeSupportUnit] = Refined("Life Support Unit", 4, ResourceStorage.Pooled(6f)),
        [ResourceType.SensorArray] = Refined("Sensor Array", 4, ResourceStorage.Pooled(4f)),
        [ResourceType.HullSection] = Refined("Hull Section", 4, ResourceStorage.Pooled(20f)),
        [ResourceType.Engine] = Refined("Engine", 4, ResourceStorage.Pooled(25f))
    };

    /// <summary>The fuels, longest-burning first (the order the Best fuel choice tries them).</summary>
    public static readonly IReadOnlyList<ResourceType> Fuels = Enum.GetValues<ResourceType>()
        .Where(type => For(type).BurnSeconds is not null)
        .OrderByDescending(type => For(type).BurnSeconds)
        .ToList();

    /// <summary>The fuels a fire burns (every fuel but the Fuel Rod, which only a Fission Reactor takes), longest-burning first.</summary>
    public static readonly IReadOnlyList<ResourceType> Combustibles = Fuels
        .Where(type => type != ResourceType.FuelRod)
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
