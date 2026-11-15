using System;
using System.Collections.Generic;

namespace Game.Sim;

/// <summary>
/// Storage limits. A Bulk, Fluid, or Secure resource has a limit of its own: the sum, over every
/// online building (see SimulationNetwork), of what it adds for that resource (the HQ's base, a Silo's step for its
/// chosen resource, a class store's step for every resource in its class). Cargo resources share
/// one pool, the sum of the HQ's and the Warehouses' Cargo, where each unit takes up its Cargo
/// weight. Sand and Water have no limit. Gathering and production only add what fits; returns
/// (refunds, inputs handed back) and debug seeding may go past a limit, and nothing is ever
/// removed for being over it.
/// </summary>
public sealed partial class Simulation
{
    private const float RoomTolerance = 1e-4f;

    /// <summary>What a building adds to a per-type resource's limit right now.</summary>
    public static float StorageAdded(Building building, ResourceType type)
    {
        ResourceStorage storage = ResourceDefs.For(type).Storage;
        if (!storage.IsPerType)
        {
            return 0f;
        }
        return building.Def.Storage switch
        {
            StorageRole.Hq => storage.HqBase,
            StorageRole.Silo => type == building.SiloResource ? storage.SiloStep : 0f,
            StorageRole.ClassStore => storage.Class == building.Def.StoresClass ? storage.ClassStep : 0f,
            _ => 0f
        };
    }

    /// <summary>A per-type resource's limit; infinite for Cargo resources (see <see cref="CargoPool"/>) and unlimited ones.</summary>
    public float StorageLimit(ResourceType type)
    {
        if (!ResourceDefs.For(type).Storage.IsPerType)
        {
            return float.PositiveInfinity;
        }
        float limit = 0f;
        foreach (Building building in _buildings)
        {
            if (building.Online)
            {
                limit += StorageAdded(building, type);
            }
        }
        return limit;
    }

    /// <summary>The Cargo pool's size: what the HQ and the Warehouses add.</summary>
    public float CargoPool
    {
        get
        {
            float pool = 0f;
            foreach (Building building in _buildings)
            {
                if (building.Online)
                {
                    pool += building.Def.CargoPool;
                }
            }
            return pool;
        }
    }

    /// <summary>The Cargo the stored Cargo resources take up.</summary>
    public float CargoUsed
    {
        get
        {
            float used = 0f;
            foreach ((ResourceType type, float amount) in _stockpile)
            {
                used += amount * ResourceDefs.For(type).Storage.Cargo;
            }
            return used;
        }
    }

    /// <summary>How many more units of a resource fit (0 at or over its limit; infinite when unlimited).</summary>
    public float RoomFor(ResourceType type)
    {
        ResourceStorage storage = ResourceDefs.For(type).Storage;
        _stockpile.TryGetValue(type, out float have);
        return storage.Class switch
        {
            StorageClass.Unlimited => float.PositiveInfinity,
            StorageClass.Cargo => Math.Max(0f, (CargoPool - CargoUsed) / storage.Cargo),
            _ => Math.Max(0f, StorageLimit(type) - have)
        };
    }

    private bool HasRoom(ResourceType type, float amount)
    {
        return RoomFor(type) >= amount - RoomTolerance;
    }

    /// <summary>Whether all of a recipe's outputs fit together (Cargo outputs share the pool).</summary>
    private bool OutputsFit(Recipe recipe)
    {
        float cargo = 0f;
        foreach (ResourceAmount output in recipe.Outputs)
        {
            ResourceStorage storage = ResourceDefs.For(output.Type).Storage;
            if (storage.Class == StorageClass.Cargo)
            {
                cargo += output.Amount * storage.Cargo;
            }
            else if (!HasRoom(output.Type, output.Amount))
            {
                return false;
            }
        }
        return cargo <= CargoPool - CargoUsed + RoomTolerance;
    }

    /// <summary>The first of a recipe's outputs that does not fit, or null when all fit.</summary>
    private ResourceType? FirstOutputWithoutRoom(Recipe recipe)
    {
        if (OutputsFit(recipe))
        {
            return null;
        }
        foreach (ResourceAmount output in recipe.Outputs)
        {
            if (!HasRoom(output.Type, output.Amount))
            {
                return output.Type;
            }
        }
        return recipe.Outputs[0].Type;
    }

    /// <summary>
    /// Sets a Silo's resource; its step moves to the new resource at once. False unless the
    /// building is a Silo and the resource is Bulk.
    /// </summary>
    public bool SetSiloResource(int id, ResourceType type)
    {
        Building? building = BuildingById(id);
        if (building is null || building.Def.Storage != StorageRole.Silo || ResourceDefs.For(type).Storage.Class != StorageClass.Bulk)
        {
            return false;
        }
        building.SiloResource = type;
        return true;
    }

    /// <summary>The resources a storage building adds to, each with the amount (for its detail panel).</summary>
    public static IReadOnlyList<ResourceAmount> StorageAddedBy(Building building)
    {
        var added = new List<ResourceAmount>();
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            float amount = StorageAdded(building, type);
            if (amount > 0f)
            {
                added.Add(new ResourceAmount(type, amount));
            }
        }
        return added;
    }

    /// <summary>The "Storage full" reason for a resource.</summary>
    private static string StorageFull(ResourceType type)
    {
        return "Storage full: " + ResourceDefs.For(type).Name;
    }
}
