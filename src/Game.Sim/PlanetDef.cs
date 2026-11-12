using System.Collections.Generic;

namespace Game.Sim;

/// <summary>
/// A planet's biome mix: each biome's weight sets its share of the planet's area
/// (a biome's share is its weight over the sum of all weights).
/// </summary>
public sealed record PlanetDef(string Name, IReadOnlyDictionary<BiomeType, int> BiomeWeights)
{
    public float Share(BiomeType biome)
    {
        int total = 0;
        foreach (int weight in BiomeWeights.Values)
        {
            total += weight;
        }
        return BiomeWeights.TryGetValue(biome, out int w) && total > 0 ? (float)w / total : 0f;
    }
}

/// <summary>The planets. Part A has one, the starter planet.</summary>
public static class PlanetDefs
{
    public static PlanetDef Starter { get; } = new("Starter", new Dictionary<BiomeType, int>
    {
        [BiomeType.Grassland] = 25,
        [BiomeType.Forest] = 20,
        [BiomeType.Highlands] = 15,
        [BiomeType.Ocean] = 12,
        [BiomeType.Desert] = 10,
        [BiomeType.Swamp] = 8,
        [BiomeType.Tundra] = 6,
        [BiomeType.Volcanic] = 4
    });
}
