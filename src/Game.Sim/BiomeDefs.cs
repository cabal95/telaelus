using System.Collections.Generic;

namespace Game.Sim;

/// <summary>
/// What a biome puts in its map cells: its base terrain and the resource fields
/// it offers, each with its field amount. A biome carries no gameplay rule of its
/// own; walk and build rules come from terrain and features.
/// </summary>
public sealed record BiomeDef(TerrainType BaseTerrain, IReadOnlyDictionary<ResourceType, float> Fields)
{
    /// <summary>The amount a field gets when placed without one and the biome does not list its type.</summary>
    public const float DefaultFieldAmount = 250f;

    /// <summary>The biome's amount for a field type, or the global default when it does not list the type.</summary>
    public float FieldAmount(ResourceType type)
    {
        return Fields.TryGetValue(type, out float amount) ? amount : DefaultFieldAmount;
    }
}

/// <summary>The definition table for every biome.</summary>
public static class BiomeDefs
{
    private static readonly BiomeDef Grassland = new(TerrainType.Grass, new Dictionary<ResourceType, float>
    {
        [ResourceType.Ore] = 250f,
        [ResourceType.Wood] = 250f
    });
    private static readonly BiomeDef Forest = new(TerrainType.Grass, new Dictionary<ResourceType, float>
    {
        [ResourceType.Wood] = 500f
    });
    private static readonly BiomeDef Highlands = new(TerrainType.Rock, new Dictionary<ResourceType, float>
    {
        [ResourceType.Ore] = 400f
    });
    private static readonly BiomeDef Desert = new(TerrainType.Sand, new Dictionary<ResourceType, float>
    {
        [ResourceType.Ore] = 250f
    });
    private static readonly BiomeDef Swamp = new(TerrainType.Mud, new Dictionary<ResourceType, float>
    {
        [ResourceType.Wood] = 250f
    });
    private static readonly BiomeDef Tundra = new(TerrainType.Snow, new Dictionary<ResourceType, float>
    {
        [ResourceType.Ore] = 250f
    });
    private static readonly BiomeDef Volcanic = new(TerrainType.Rock, new Dictionary<ResourceType, float>
    {
        [ResourceType.Ore] = 400f
    });
    private static readonly BiomeDef Ocean = new(TerrainType.DeepWater, new Dictionary<ResourceType, float>());

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
