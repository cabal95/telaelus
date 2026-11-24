using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

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

    /// <summary>The fuels a burner takes, longest-burning first (a fire's Coal, Charcoal, and Wood; a Fission Reactor's Fuel Rods).</summary>
    public IReadOnlyList<ResourceType> Fuels { get; init; } = ResourceDefs.Combustibles;

    /// <summary>A provider whose supply follows the daylight (the Solar Array).</summary>
    public bool Solar { get; init; }

    /// <summary>The Power a Battery Bank stores, in Power-seconds (0 for anything else).</summary>
    public float StoreCapacity { get; init; }

    /// <summary>The most Power a Battery Bank takes in or gives out.</summary>
    public float StoreRate { get; init; }

    /// <summary>A Research Lab: it adds to the research speed while it works.</summary>
    public bool Researches { get; init; }

    public Placement Placement { get; init; } = Placement.Standard;

    /// <summary>How many trucks it holds (a Depot); its first comes with it.</summary>
    public int TruckSlots { get; init; }

    /// <summary>Seconds per unit for a collector with no field (the Quarry; the Deep Well and Deep Mine on a worked-out cell).</summary>
    public float ProduceSeconds { get; init; }

    /// <summary>The worked-out deposits an <see cref="Placement.OverSite"/> building may stand over.</summary>
    public IReadOnlyList<ResourceType> Sites { get; init; } = Array.Empty<ResourceType>();

    /// <summary>What a provider eats from storage while it runs: one unit of the type every so many seconds (the Rock Crusher).</summary>
    public (ResourceType Type, float Seconds)? Eats { get; init; }

    /// <summary>A Forester's plot: the Wood its field grows to, the seconds it grows, and the seconds it stands bare after it is cut.</summary>
    public (float Amount, float GrowSeconds, float RegrowSeconds)? Plot { get; init; }

    /// <summary>A collector: it fills its output buffer, from fields (an extractor) or on its own.</summary>
    public bool IsCollector => IsExtractor || ProduceSeconds > 0f;


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
        CoverageRadius = 19f,
        LinkRadius = 22f
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
        CoverageRadius = 9f,
        LinkRadius = 14f
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

    private static readonly BuildingDef Forester = new("Forester", 4, 4, new ResourceAmount[]
    {
        new(ResourceType.Wood, 30f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Placement = Placement.OpenGrassCell,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 2f,
            [CapacityType.Water] = 5f
        },
        Plot = (150f, 180f, 30f)
    };
    private static readonly BuildingDef Quarry = new("Quarry", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Wood, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Placement = Placement.BesideMountain,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f
        },
        ProduceSeconds = 6f,
        OutputBuffer = 40f
    };
    private static readonly BuildingDef CharcoalKiln = new("Charcoal Kiln", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Stone, 30f),
        new(ResourceType.Wood, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
    };
    private static readonly BuildingDef RockCrusher = new("Rock Crusher", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 15f
        },
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Sand] = 4f
        },
        Eats = (ResourceType.Stone, 10f)
    };
    private static readonly BuildingDef DeepWell = new("Deep Well", 4, 4, new ResourceAmount[]
    {
        new(ResourceType.Iron, 60f),
        new(ResourceType.Stone, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Placement = Placement.OverSite,
        Sites = new[]
        {
            ResourceType.CrudeOil
        },
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 15f
        },
        ProduceSeconds = 80f / 3f,
        OutputBuffer = 20f
    };
    private static readonly BuildingDef DeepMine = new("Deep Mine", 4, 4, new ResourceAmount[]
    {
        new(ResourceType.Iron, 80f),
        new(ResourceType.Stone, 40f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        Placement = Placement.OverSite,
        Sites = new[]
        {
            ResourceType.IronOre,
            ResourceType.CopperOre,
            ResourceType.Bauxite,
            ResourceType.TitaniumOre,
            ResourceType.Quartz,
            ResourceType.Limestone
        },
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 30f
        },
        ProduceSeconds = 20f,
        OutputBuffer = 40f
    };

    private static readonly BuildingDef SteelMill = new("Steel Mill", 4, 6, new ResourceAmount[]
    {
        new(ResourceType.Stone, 60f),
        new(ResourceType.Iron, 40f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 20f
        }
    };
    private static readonly BuildingDef Electrolyzer = new("Electrolyzer", 4, 5, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Copper, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 50f,
            [CapacityType.Water] = 4f
        }
    };
    private static readonly BuildingDef FuelPlant = new("Fuel Plant", 4, 5, new ResourceAmount[]
    {
        new(ResourceType.Steel, 40f),
        new(ResourceType.Copper, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 20f
        }
    };
    private static readonly BuildingDef ChemicalPlant = new("Chemical Plant", 5, 6, new ResourceAmount[]
    {
        new(ResourceType.Steel, 40f),
        new(ResourceType.Copper, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 25f,
            [CapacityType.Water] = 2f
        }
    };
    private static readonly BuildingDef AluminumSmelter = new("Aluminum Smelter", 3, 8, new ResourceAmount[]
    {
        new(ResourceType.Steel, 50f),
        new(ResourceType.Stone, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 60f
        }
    };
    private static readonly BuildingDef ElectricFurnace = new("Electric Furnace", 4, 4, new ResourceAmount[]
    {
        new(ResourceType.Steel, 50f),
        new(ResourceType.Copper, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 40f
        }
    };

    private static readonly BuildingDef Fabricator = new("Fabricator", 4, 6, new ResourceAmount[]
    {
        new(ResourceType.Steel, 40f),
        new(ResourceType.Copper, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 30f
        }
    };
    private static readonly BuildingDef ElectronicsWorks = new("Electronics Works", 4, 5, new ResourceAmount[]
    {
        new(ResourceType.Steel, 30f),
        new(ResourceType.Copper, 30f),
        new(ResourceType.Glass, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 20f,
            [CapacityType.Water] = 2f
        }
    };
    private static readonly BuildingDef TileKiln = new("Tile Kiln", 3, 7, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Steel, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 30f
        }
    };
    private static readonly BuildingDef AssemblyPlant = new("Assembly Plant", 6, 8, new ResourceAmount[]
    {
        new(ResourceType.SteelBeam, 20f),
        new(ResourceType.HullPlate, 20f),
        new(ResourceType.Circuit, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 3,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 40f
        }
    };
    private static readonly BuildingDef Shipyard = new("Shipyard", 8, 12, new ResourceAmount[]
    {
        new(ResourceType.SteelBeam, 40f),
        new(ResourceType.HullPlate, 20f),
        new(ResourceType.Circuit, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 4,
        OutputBuffer = 2f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 50f
        }
    };

    private static readonly BuildingDef Sawmill = new("Sawmill", 3, 5, new ResourceAmount[]
    {
        new(ResourceType.Wood, 30f),
        new(ResourceType.Stone, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
    };
    private static readonly BuildingDef CementKiln = new("Cement Kiln", 3, 7, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Iron, 20f),
        new(ResourceType.Lumber, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        OutputBuffer = 10f,
        BurnsFuel = true
    };
    private static readonly BuildingDef ConcreteMixer = new("Concrete Mixer", 3, 4, new ResourceAmount[]
    {
        new(ResourceType.Iron, 30f),
        new(ResourceType.Lumber, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f,
            [CapacityType.Sand] = 2f,
            [CapacityType.Water] = 2f
        }
    };
    private static readonly BuildingDef BrineWell = new("Brine Well", 4, 6, new ResourceAmount[]
    {
        new(ResourceType.Steel, 30f),
        new(ResourceType.Concrete, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Placement = Placement.OnDesertSand,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 15f,
            [CapacityType.Water] = 2f
        }
    };
    private static readonly BuildingDef SemiconductorFab = new("Semiconductor Fab", 5, 6, new ResourceAmount[]
    {
        new(ResourceType.Steel, 40f),
        new(ResourceType.Concrete, 30f),
        new(ResourceType.Glass, 20f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 40f,
            [CapacityType.Water] = 4f
        }
    };
    private static readonly BuildingDef AirSeparator = new("Air Separator", 4, 5, new ResourceAmount[]
    {
        new(ResourceType.Steel, 30f),
        new(ResourceType.Concrete, 20f),
        new(ResourceType.ElectricMotor, 6f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 40f
        }
    };
    private static readonly BuildingDef EnrichmentPlant = new("Enrichment Plant", 6, 7, new ResourceAmount[]
    {
        new(ResourceType.Concrete, 40f),
        new(ResourceType.ArmorPlate, 20f),
        new(ResourceType.ElectricMotor, 10f),
        new(ResourceType.Computer, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 3,
        OutputBuffer = 10f,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 80f,
            [CapacityType.Water] = 2f
        }
    };

    private static readonly BuildingDef SolarArray = new("Solar Array", 6, 6, new ResourceAmount[]
    {
        new(ResourceType.SolarCell, 20f),
        new(ResourceType.AluminumFrame, 10f),
        new(ResourceType.Concrete, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        Solar = true,
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 60f
        }
    };
    private static readonly BuildingDef BatteryBank = new("Battery Bank", 3, 4, new ResourceAmount[]
    {
        new(ResourceType.Battery, 20f),
        new(ResourceType.Steel, 20f),
        new(ResourceType.Concrete, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 2,
        StoreCapacity = 6000f,
        StoreRate = 50f
    };
    private static readonly BuildingDef FissionReactor = new("Fission Reactor", 6, 8, new ResourceAmount[]
    {
        new(ResourceType.Concrete, 40f),
        new(ResourceType.ArmorPlate, 20f),
        new(ResourceType.HeatShield, 10f),
        new(ResourceType.SensorArray, 6f),
        new(ResourceType.Computer, 10f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 3,
        BurnsFuel = true,
        Fuels = new[]
        {
            ResourceType.FuelRod
        },
        Supplies = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 400f
        },
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Water] = 8f
        }
    };

    private static readonly BuildingDef ResearchLab = new("Research Lab", 4, 5, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Wood, 30f),
        new(ResourceType.Iron, 30f)
    }, Array.Empty<ResourceType>(), 0f)
    {
        Tier = 1,
        Researches = true,
        Draws = new Dictionary<CapacityType, float>
        {
            [CapacityType.Power] = 10f
        }
    };

    /// <summary>
    /// What each buildable building makes or gives the player and what it needs, in a line for the
    /// build menu's tooltip.
    /// </summary>
    private static readonly Dictionary<BuildingType, string> Descriptions = new()
    {
        [BuildingType.Mine] = "Digs Iron Ore, Copper Ore, Coal, Stone, and other ore deposits nearby.",
        [BuildingType.LumberCamp] = "Cuts Wood from nearby forests.",
        [BuildingType.Smelter] = "Smelts Iron Ore or Copper Ore into Iron or Copper; burns fuel.",
        [BuildingType.Refinery] = "Refines Crude Oil into Kerosene; needs Power and Water.",
        [BuildingType.OilWell] = "Pumps Crude Oil; build it right beside an oil seep.",
        [BuildingType.Silo] = "Raises the storage limit of one chosen Bulk resource, such as Wood.",
        [BuildingType.Stockyard] = "Raises the Wood, Stone, ore, and other Bulk storage limits.",
        [BuildingType.TankFarm] = "Raises the Crude Oil, Kerosene, and other Fluid storage limits.",
        [BuildingType.Warehouse] = "Adds Cargo space for Iron, Steel, parts, and other goods.",
        [BuildingType.Vault] = "Raises the Rare Earths, Uranium Ore, and other Secure storage limits.",
        [BuildingType.SmallHub] = "Extends the HQ's network to buildings nearby.",
        [BuildingType.Relay] = "Links far-off hubs back to the HQ's network over a long distance.",
        [BuildingType.WaterWheel] = "Makes Power with no fuel; build it across a straight river.",
        [BuildingType.BurnerGenerator] = "Makes lots of Power by burning Coal, Charcoal, or Wood.",
        [BuildingType.LumberMill] = "A faster, powered Lumber Camp: cuts nearby Wood; needs Power.",
        [BuildingType.DrillRig] = "Powered Mine: digs ores plus Rare Earths, Uranium Ore, Crystals.",
        [BuildingType.WaterPump] = "Supplies a little Water; build it beside water.",
        [BuildingType.PumpingStation] = "Supplies four Pumps' worth of Water beside water; needs Power.",
        [BuildingType.SandCollector] = "Supplies a little Sand; build it on sand.",
        [BuildingType.SandDredge] = "Supplies four Collectors' worth of Sand on sand; needs Power.",
        [BuildingType.Glassworks] = "Makes Glass from Sand; burns fuel.",
        [BuildingType.Depot] = "Its trucks haul resources from collectors back to the HQ.",
        [BuildingType.Forester] = "Plants a Wood field that regrows on open grass; needs Power, Water.",
        [BuildingType.Quarry] = "Cuts Stone (or Limestone) beside a mountain; needs Power.",
        [BuildingType.CharcoalKiln] = "Turns Wood into Charcoal for fuel and steelmaking.",
        [BuildingType.RockCrusher] = "Supplies Sand anywhere by crushing Stone; needs Power.",
        [BuildingType.DeepWell] = "Pumps Crude Oil from a worked-out oil site or Salt Dome; needs Power.",
        [BuildingType.DeepMine] = "Keeps mining a worked-out ore site or a motherlode; needs Power.",
        [BuildingType.SteelMill] = "Makes Steel from Iron, Coal or Charcoal, and Limestone; needs Power.",
        [BuildingType.Electrolyzer] = "Makes Hydrogen or Liquid Oxygen from Water; needs Power.",
        [BuildingType.FuelPlant] = "Makes Rocket Fuel from Kerosene and Hydrogen; needs Power.",
        [BuildingType.ChemicalPlant] = "Makes Plastic, Rubber, and more from Crude Oil and Sulfur; needs Power.",
        [BuildingType.AluminumSmelter] = "Makes Aluminum from Bauxite; needs lots of Power.",
        [BuildingType.ElectricFurnace] = "Makes Silicon from Quartz or Titanium from Titanium Ore; needs Power.",
        [BuildingType.Fabricator] = "Makes parts such as Hull Plate and Steel Beam from metals; needs Power.",
        [BuildingType.ElectronicsWorks] = "Makes Copper Wire and Circuits from Copper and Plastic; needs Power.",
        [BuildingType.TileKiln] = "Fires Silicon and Aluminum into Ceramic Tile; needs Power.",
        [BuildingType.AssemblyPlant] = "Assembles advanced parts such as Control Module and Fuel Tank.",
        [BuildingType.Shipyard] = "Builds Hull Sections and Engines, and launches your ship.",
        [BuildingType.Sawmill] = "Saws Wood into Lumber.",
        [BuildingType.CementKiln] = "Bakes Limestone into Cement; burns fuel.",
        [BuildingType.ConcreteMixer] = "Mixes Cement into Concrete; needs Power, Sand, and Water.",
        [BuildingType.BrineWell] = "Draws Lithium from desert salt flats; needs Power and Water.",
        [BuildingType.SemiconductorFab] = "Makes Silicon Wafers, Processors, and Solar Cells; needs Power.",
        [BuildingType.AirSeparator] = "Makes Liquid Oxygen from the air; needs Power.",
        [BuildingType.EnrichmentPlant] = "Turns Uranium Ore into Enriched Uranium and Fuel Rods; needs Power.",
        [BuildingType.SolarArray] = "Makes Power from sunlight, none at night; no fuel.",
        [BuildingType.BatteryBank] = "Stores spare Power and gives it back during shortages.",
        [BuildingType.FissionReactor] = "Makes huge Power by burning Fuel Rods; needs Water to cool.",
        [BuildingType.ResearchLab] = "Speeds up research; each extra lab adds more. Needs Power."
    };

    /// <summary>A building's one-line description, or "" for one that cannot be built (the HQ).</summary>
    public static string DescriptionOf(BuildingType type)
    {
        return Descriptions.GetValueOrDefault(type, "");
    }

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
            BuildingType.Forester => Forester,
            BuildingType.Quarry => Quarry,
            BuildingType.CharcoalKiln => CharcoalKiln,
            BuildingType.RockCrusher => RockCrusher,
            BuildingType.DeepWell => DeepWell,
            BuildingType.DeepMine => DeepMine,
            BuildingType.SteelMill => SteelMill,
            BuildingType.Electrolyzer => Electrolyzer,
            BuildingType.FuelPlant => FuelPlant,
            BuildingType.ChemicalPlant => ChemicalPlant,
            BuildingType.AluminumSmelter => AluminumSmelter,
            BuildingType.ElectricFurnace => ElectricFurnace,
            BuildingType.Fabricator => Fabricator,
            BuildingType.ElectronicsWorks => ElectronicsWorks,
            BuildingType.TileKiln => TileKiln,
            BuildingType.AssemblyPlant => AssemblyPlant,
            BuildingType.Shipyard => Shipyard,
            BuildingType.Sawmill => Sawmill,
            BuildingType.CementKiln => CementKiln,
            BuildingType.ConcreteMixer => ConcreteMixer,
            BuildingType.BrineWell => BrineWell,
            BuildingType.SemiconductorFab => SemiconductorFab,
            BuildingType.AirSeparator => AirSeparator,
            BuildingType.EnrichmentPlant => EnrichmentPlant,
            BuildingType.SolarArray => SolarArray,
            BuildingType.BatteryBank => BatteryBank,
            BuildingType.FissionReactor => FissionReactor,
            BuildingType.ResearchLab => ResearchLab,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no building definition")
        };
    }
}
