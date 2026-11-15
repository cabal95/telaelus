using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim;

/// <summary>What a building does for storage.</summary>
public enum StorageRole
{
    None,

    /// <summary>The HQ: every per-type resource's HQ base, and its Cargo pool.</summary>
    Hq,

    /// <summary>A Silo: its chosen Bulk resource's Silo step.</summary>
    Silo,

    /// <summary>A Stockyard, Tank Farm, or Vault: the class step of every resource in its class.</summary>
    ClassStore,

    /// <summary>A Warehouse: its Cargo pool.</summary>
    Warehouse
}

/// <summary>
/// Static definition of a building type: its display name, its footprint in fine
/// cells (width along X and depth along Z when facing North), its cost (paid from
/// the stockpile when placed), its speed (an extractor's time per unit is the
/// deposit's gather time divided by it; every building is base level today), and
/// for an extractor, the deposit types it works and its work radius in world units.
/// Its placement rule and the capacity it draws and supplies belong here too.
/// </summary>
public sealed record BuildingDef(
    string Name,
    int Width,
    int Depth,
    IReadOnlyList<ResourceAmount> Cost,
    IReadOnlyList<ResourceType> Works,
    float WorkRadius)
{
    /// <summary>The speed of a base-level building: slower than the player by hand.</summary>
    public const float BaseSpeed = 0.75f;

    public float Speed { get; init; } = BaseSpeed;

    /// <summary>What it does for storage; a class store raises <see cref="StoresClass"/>.</summary>
    public StorageRole Storage { get; init; } = StorageRole.None;

    public StorageClass StoresClass { get; init; } = StorageClass.Unlimited;

    /// <summary>The Cargo it adds to the pool (the HQ and Warehouses).</summary>
    public float CargoPool { get; init; }

    /// <summary>A permanent building (the HQ) cannot be switched off or destroyed.</summary>
    public bool Permanent { get; init; }

    /// <summary>Whether the player can place it (it is in the build menu); the world places the HQ.</summary>
    public bool Buildable { get; init; } = true;

    /// <summary>A hub's coverage radius: buildings whose footprint comes this close to its center join the network.</summary>
    public float CoverageRadius { get; init; }

    /// <summary>A hub's link radius: hubs whose centers are this close link (the larger of the two radii counts).</summary>
    public float LinkRadius { get; init; }

    /// <summary>Whether it is a hub (the HQ, a Small Hub, a Relay).</summary>
    public bool IsHub => CoverageRadius > 0f || LinkRadius > 0f;

    /// <summary>How many units its output buffer holds (extractors and processors; 0 for none).</summary>
    public float OutputBuffer { get; init; }

    /// <summary>How many cycles' worth of each recipe input its input buffer holds (processors).</summary>
    public int InputCycles { get; init; }

    /// <summary>Its tier: 0 for the unpowered start, 1 for the first powered buildings.</summary>
    public int Tier { get; init; }

    private static readonly IReadOnlyDictionary<CapacityType, float> NoCapacity = new Dictionary<CapacityType, float>();

    /// <summary>The capacity it reserves while switched on and online, by type (a consumer).</summary>
    public IReadOnlyDictionary<CapacityType, float> Draws { get; init; } = NoCapacity;

    /// <summary>The capacity it supplies while it runs online, by type (a provider).</summary>
    public IReadOnlyDictionary<CapacityType, float> Supplies { get; init; } = NoCapacity;

    public bool IsProvider => Supplies.Count > 0;

    public float Draw(CapacityType type)
    {
        return Draws.TryGetValue(type, out float amount) ? amount : 0f;
    }

    public float Supply(CapacityType type)
    {
        return Supplies.TryGetValue(type, out float amount) ? amount : 0f;
    }

    /// <summary>Whether it has a fuel slot and burns fuel to work (the Smelter, the Burner Generator).</summary>
    public bool BurnsFuel { get; init; }

    public Placement Placement { get; init; } = Placement.Standard;

    /// <summary>How many trucks it holds (a Depot); its first comes with it.</summary>
    public int TruckSlots { get; init; }

    /// <summary>How many units of fuel a fuel slot holds.</summary>
    public const float FuelSlotSize = 5f;

    public bool IsExtractor => Works.Count > 0;

    /// <summary>The footprint size for a facing; East and West swap width and depth.</summary>
    public (int Width, int Depth) Footprint(Facing facing)
    {
        return facing is Facing.East or Facing.West ? (Depth, Width) : (Width, Depth);
    }

    /// <summary>World-space center of the footprint placed at an anchor with a facing.</summary>
    public Vector3 Center(int anchorX, int anchorZ, Facing facing)
    {
        (int width, int depth) = Footprint(facing);
        return new Vector3(anchorX + width * 0.5f, 0f, anchorZ + depth * 0.5f);
    }
}

/// <summary>The definition table for every building type.</summary>
public static class BuildingDefs
{
    private static readonly BuildingDef Mine = new("Mine", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 25f),
        new(ResourceType.Stone, 15f)
    }, new[]
    {
        ResourceType.IronOre,
        ResourceType.CopperOre,
        ResourceType.Coal,
        ResourceType.Stone,
        ResourceType.Bauxite,
        ResourceType.Quartz,
        ResourceType.Limestone,
        ResourceType.Sulfur,
        ResourceType.TitaniumOre
    }, 6f)
    {
        OutputBuffer = 20f
    };
    private static readonly BuildingDef LumberCamp = new("Lumber Camp", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 15f)
    }, new[]
    {
        ResourceType.Wood
    }, 6f)
    {
        OutputBuffer = 20f
    };
    private static readonly BuildingDef OilWell = new("Oil Well", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Iron, 25f),
        new(ResourceType.Stone, 20f),
        new(ResourceType.Wood, 10f)
    }, new[]
    {
        ResourceType.CrudeOil
    }, 3f)
    {
        OutputBuffer = 20f
    };
    private static readonly BuildingDef Smelter = new("Smelter", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Wood, 15f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
        InputCycles = 5,
        BurnsFuel = true
    };
    private static readonly BuildingDef Refinery = new("Refinery", 2, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Stone, 30f),
        new(ResourceType.Wood, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
        InputCycles = 5,
        Tier = 1,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 25f,
            [CapacityType.Water] = 2f
        }
    };
    private static readonly BuildingDef Glassworks = new("Glassworks", 3, 6, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Wood, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
        BurnsFuel = true,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Sand] = 2f
        }
    };

    private static readonly BuildingDef Hq = new("HQ", 6, 10, Array.Empty<ResourceAmount>(), Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.Hq,
        CargoPool = 150f,
        Permanent = true,
        Buildable = false,
        CoverageRadius = 14f,
        LinkRadius = 16f
    };
    private static readonly BuildingDef Silo = new("Silo", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.Silo
    };
    private static readonly BuildingDef Stockyard = new("Stockyard", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Wood, 40f),
        new(ResourceType.Iron, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.ClassStore,
        StoresClass = StorageClass.Bulk
    };
    private static readonly BuildingDef TankFarm = new("Tank Farm", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 30f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.ClassStore,
        StoresClass = StorageClass.Fluid
    };
    private static readonly BuildingDef Warehouse = new("Warehouse", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Wood, 40f),
        new(ResourceType.Stone, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.Warehouse,
        CargoPool = 200f
    };
    private static readonly BuildingDef Vault = new("Vault", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Steel, 40f),
        new(ResourceType.Stone, 40f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.ClassStore,
        StoresClass = StorageClass.Secure
    };

    private static readonly BuildingDef SmallHub = new("Small Hub", 1, 1, new ResourceAmount[]
    {
        new(ResourceType.Wood, 10f),
        new(ResourceType.Stone, 5f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        CoverageRadius = 8f,
        LinkRadius = 12f
    };
    private static readonly BuildingDef Relay = new("Relay", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Iron, 20f),
        new(ResourceType.Stone, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        CoverageRadius = 3f,
        LinkRadius = 30f
    };

    private static readonly BuildingDef WaterWheel = new("Water Wheel", 4, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 40f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 20f
        },
        Placement = Placement.AcrossStraightRiver
    };
    private static readonly BuildingDef BurnerGenerator = new("Burner Generator", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Stone, 60f),
        new(ResourceType.Iron, 40f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 100f
        },
        BurnsFuel = true
    };
    private static readonly BuildingDef LumberMill = new("Lumber Mill", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Wood, 30f),
        new(ResourceType.Iron, 20f)
    }, new[]
    {
        ResourceType.Wood
    }, 8f)
    {
        Speed = 2f,
        OutputBuffer = 40f,
        Tier = 1,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 5f
        }
    };
    private static readonly BuildingDef DrillRig = new("Drill Rig", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 30f),
        new(ResourceType.Stone, 20f)
    }, new[]
    {
        ResourceType.IronOre,
        ResourceType.CopperOre,
        ResourceType.Coal,
        ResourceType.Stone,
        ResourceType.Bauxite,
        ResourceType.Quartz,
        ResourceType.Limestone,
        ResourceType.Sulfur,
        ResourceType.TitaniumOre,
        ResourceType.RareEarths,
        ResourceType.UraniumOre,
        ResourceType.Crystals
    }, 8f)
    {
        Speed = 2f,
        OutputBuffer = 40f,
        Tier = 1,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f
        }
    };

    private static readonly BuildingDef WaterPump = new("Water Pump", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 20f),
        new(ResourceType.Stone, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Water] = 4f
        },
        Placement = Placement.BesideWater
    };
    private static readonly BuildingDef PumpingStation = new("Pumping Station", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Water] = 16f
        },
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f
        },
        Placement = Placement.BesideWater
    };
    private static readonly BuildingDef SandCollector = new("Sand Collector", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 20f),
        new(ResourceType.Stone, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Sand] = 2f
        },
        Placement = Placement.OnSand
    };
    private static readonly BuildingDef SandDredge = new("Sand Dredge", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Sand] = 8f
        },
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f
        },
        Placement = Placement.OnSand
    };

    private static readonly BuildingDef Depot = new("Depot", 4, 4, new ResourceAmount[]
    {
        new(ResourceType.Wood, 40f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        TruckSlots = 4
    };

    public static BuildingDef For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Mine => Mine,
            BuildingType.LumberCamp => LumberCamp,
            BuildingType.Smelter => Smelter,
            BuildingType.Refinery => Refinery,
            BuildingType.OilWell => OilWell,
            BuildingType.Hq => Hq,
            BuildingType.Silo => Silo,
            BuildingType.Stockyard => Stockyard,
            BuildingType.TankFarm => TankFarm,
            BuildingType.Warehouse => Warehouse,
            BuildingType.Vault => Vault,
            BuildingType.SmallHub => SmallHub,
            BuildingType.Relay => Relay,
            BuildingType.WaterWheel => WaterWheel,
            BuildingType.BurnerGenerator => BurnerGenerator,
            BuildingType.LumberMill => LumberMill,
            BuildingType.DrillRig => DrillRig,
            BuildingType.WaterPump => WaterPump,
            BuildingType.PumpingStation => PumpingStation,
            BuildingType.SandCollector => SandCollector,
            BuildingType.SandDredge => SandDredge,
            BuildingType.Glassworks => Glassworks,
            BuildingType.Depot => Depot,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no building definition")
        };
    }
}
