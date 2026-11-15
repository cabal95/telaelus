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
/// Placement rules belong here too; every type currently uses the standard rule
/// (see Simulation.CanPlace), and an exception such as a water wheel would be a
/// new rule on its own definition.
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
        ResourceType.Stone
    }, 6f);
    private static readonly BuildingDef LumberCamp = new("Lumber Camp", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Wood, 15f)
    }, new[]
    {
        ResourceType.Wood
    }, 6f);
    private static readonly BuildingDef OilWell = new("Oil Well", 2, 2, new ResourceAmount[]
    {
        new(ResourceType.Iron, 25f),
        new(ResourceType.Stone, 20f),
        new(ResourceType.Wood, 10f)
    }, new[]
    {
        ResourceType.CrudeOil
    }, 3f);
    private static readonly BuildingDef Smelter = new("Smelter", 3, 3, new ResourceAmount[]
    {
        new(ResourceType.Stone, 40f),
        new(ResourceType.Wood, 15f)
    }, Array.Empty<ResourceType>(), 0f);
    private static readonly BuildingDef Refinery = new("Refinery", 2, 3, new ResourceAmount[]
    {
        new(ResourceType.Iron, 40f),
        new(ResourceType.Stone, 30f),
        new(ResourceType.Wood, 20f)
    }, Array.Empty<ResourceType>(), 0f);

    private static readonly BuildingDef Hq = new("HQ", 6, 10, Array.Empty<ResourceAmount>(), Array.Empty<ResourceType>(), 0f)
    {
        Storage = StorageRole.Hq,
        CargoPool = 150f,
        Permanent = true,
        Buildable = false
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
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no building definition")
        };
    }
}
