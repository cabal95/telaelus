using System.Collections.Generic;

namespace Game.Sim;

/// <summary>
/// What a biome puts in its map cells: its base terrain and the deposits it offers
/// as resource fields, each with its field amount, plus how densely generation
/// places fields (the chance a free map cell holds one) and how often each deposit
/// type is chosen. A biome carries no gameplay rule of its own; walk and build
/// rules come from terrain and features.
/// </summary>
public sealed record BiomeDef(
    TerrainType BaseTerrain,
    IReadOnlyDictionary<ResourceType, float> Fields,
    float FieldDensity,
    IReadOnlyDictionary<ResourceType, int> FieldWeights)
{
    /// <summary>The amount a field gets when placed without one and the biome does not list its type.</summary>
    public const float DefaultFieldAmount = 250f;

    /// <summary>The biome's amount for a field type, or the global default when it does not list the type.</summary>
    public float FieldAmount(ResourceType type)
    {
        return Fields.TryGetValue(type, out float amount) ? amount : DefaultFieldAmount;
    }

    /// <summary>Picks a deposit type by weight from a roll in [0, 1); null when the biome offers none.</summary>
    public ResourceType? PickDeposit(float roll)
    {
        int total = 0;
        foreach (int weight in FieldWeights.Values)
        {
            total += weight;
        }
        if (total == 0)
        {
            return null;
        }

        float target = roll * total;
        foreach ((ResourceType type, int weight) in FieldWeights)
        {
            if (target < weight)
            {
                return type;
            }
            target -= weight;
        }
        return null;
    }
}

/// <summary>The definition table for every biome.</summary>
public static class BiomeDefs
{
    private static readonly BiomeDef Grassland = new(
        TerrainType.Grass,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.Wood] = 250f,
            [ResourceType.Stone] = 250f,
            [ResourceType.IronOre] = 250f
        },
        0.04f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 2,
            [ResourceType.Stone] = 1,
            [ResourceType.IronOre] = 1
        });

    private static readonly BiomeDef Forest = new(
        TerrainType.Grass,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.Wood] = 500f
        },
        0.30f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 1
        });

    private static readonly BiomeDef Highlands = new(
        TerrainType.Rock,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 400f,
            [ResourceType.CopperOre] = 300f,
            [ResourceType.Coal] = 400f,
            [ResourceType.Stone] = 400f
        },
        0.07f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 3,
            [ResourceType.CopperOre] = 2,
            [ResourceType.Coal] = 2,
            [ResourceType.Stone] = 2
        });

    private static readonly BiomeDef Desert = new(
        TerrainType.Sand,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.CopperOre] = 250f,
            [ResourceType.CrudeOil] = 500f
        },
        0.04f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.CopperOre] = 1,
            [ResourceType.CrudeOil] = 2
        });

    private static readonly BiomeDef Swamp = new(
        TerrainType.Mud,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.Wood] = 250f
        },
        0.06f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 1
        });

    private static readonly BiomeDef Tundra = new(
        TerrainType.Snow,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 250f
        },
        0.03f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 1
        });

    private static readonly BiomeDef Volcanic = new(
        TerrainType.Rock,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 400f,
            [ResourceType.Stone] = 400f
        },
        0.04f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 1,
            [ResourceType.Stone] = 1
        });

    private static readonly BiomeDef Ocean = new(
        TerrainType.DeepWater,
        new Dictionary<ResourceType, float>(),
        0f,
        new Dictionary<ResourceType, int>());

    public static BiomeDef For(BiomeType type)
    {
        return type switch
        {
            BiomeType.Grassland => Grassland,
            BiomeType.Forest => Forest,
            BiomeType.Highlands => Highlands,
            BiomeType.Desert => Desert,
            BiomeType.Swamp => Swamp,
            BiomeType.Tundra => Tundra,
            BiomeType.Volcanic => Volcanic,
            BiomeType.Ocean => Ocean,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, "no biome definition")
        };
    }
}
