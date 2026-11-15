using System.Collections.Generic;

namespace Telaelus.Sim;

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
    public const float DefaultFieldAmount = 1000f;

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
            [ResourceType.Wood] = 1000f,
            [ResourceType.Stone] = 1000f,
            [ResourceType.IronOre] = 1000f,
            [ResourceType.Limestone] = 1000f
        },
        0.013f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 2,
            [ResourceType.Stone] = 1,
            [ResourceType.IronOre] = 1,
            [ResourceType.Limestone] = 1
        });

    private static readonly BiomeDef Forest = new(
        TerrainType.Grass,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.Wood] = 2000f
        },
        0.15f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 1
        });

    private static readonly BiomeDef Highlands = new(
        TerrainType.Rock,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 1600f,
            [ResourceType.CopperOre] = 1200f,
            [ResourceType.Coal] = 1600f,
            [ResourceType.Stone] = 1600f,
            [ResourceType.Bauxite] = 1600f,
            [ResourceType.Limestone] = 1600f,
            [ResourceType.Quartz] = 1600f
        },
        0.023f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 3,
            [ResourceType.CopperOre] = 2,
            [ResourceType.Coal] = 2,
            [ResourceType.Stone] = 2,
            [ResourceType.Bauxite] = 1,
            [ResourceType.Limestone] = 1,
            [ResourceType.Quartz] = 1
        });

    private static readonly BiomeDef Desert = new(
        TerrainType.Sand,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.CopperOre] = 1000f,
            [ResourceType.CrudeOil] = 2000f,
            [ResourceType.Bauxite] = 1000f,
            [ResourceType.TitaniumOre] = 1000f
        },
        0.013f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.CopperOre] = 1,
            [ResourceType.CrudeOil] = 2,
            [ResourceType.Bauxite] = 1,
            [ResourceType.TitaniumOre] = 1
        });

    private static readonly BiomeDef Swamp = new(
        TerrainType.Mud,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.Wood] = 1000f,
            [ResourceType.Sulfur] = 1000f
        },
        0.02f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 2,
            [ResourceType.Sulfur] = 1
        });

    private static readonly BiomeDef Tundra = new(
        TerrainType.Snow,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 1000f,
            [ResourceType.Quartz] = 1000f,
            [ResourceType.TitaniumOre] = 1000f,
            [ResourceType.RareEarths] = 600f,
            [ResourceType.UraniumOre] = 600f
        },
        0.01f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 2,
            [ResourceType.Quartz] = 1,
            [ResourceType.TitaniumOre] = 1,
            [ResourceType.RareEarths] = 1,
            [ResourceType.UraniumOre] = 1
        });

    private static readonly BiomeDef Volcanic = new(
        TerrainType.Rock,
        new Dictionary<ResourceType, float>
        {
            [ResourceType.IronOre] = 1600f,
            [ResourceType.Stone] = 1600f,
            [ResourceType.Sulfur] = 1600f,
            [ResourceType.Crystals] = 400f
        },
        0.013f,
        new Dictionary<ResourceType, int>
        {
            [ResourceType.IronOre] = 2,
            [ResourceType.Stone] = 2,
            [ResourceType.Sulfur] = 2,
            [ResourceType.Crystals] = 1
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
