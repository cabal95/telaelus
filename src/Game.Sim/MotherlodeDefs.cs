using System;
using System.Collections.Generic;

namespace Game.Sim;

/// <summary>The kinds of motherlode: rare, endless, 8 x 8 sites that can yield any of several resources.</summary>
public enum MotherlodeType
{
    Laterite,
    Porphyry,
    CoalMeasures,
    SaltDome,
    MineralSands,
    MassiveSulfide
}

/// <summary>A motherlode kind: its name, its yields (largest share first), where it forms, and what works it.</summary>
public sealed record MotherlodeDef(
    string Name,
    IReadOnlyList<(ResourceType Type, float Share)> Yields,
    IReadOnlyList<BiomeType> Biomes,
    BuildingType WorkedBy)
{
    /// <summary>Seconds a unit of the largest share takes; a smaller share takes longer in proportion.</summary>
    public const float BaseSeconds = 5f;

    public bool HasYield(ResourceType type)
    {
        foreach ((ResourceType yield, float _) in Yields)
        {
            if (yield == type)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Seconds a unit of one yield takes: 5 s times the largest share over its share.</summary>
    public float UnitSeconds(ResourceType type)
    {
        foreach ((ResourceType yield, float share) in Yields)
        {
            if (yield == type)
            {
                return BaseSeconds * Yields[0].Share / share;
            }
        }
        throw new ArgumentException($"{Name} does not yield {type}", nameof(type));
    }
}

/// <summary>The definition table for every motherlode kind.</summary>
public static class MotherlodeDefs
{
    private static readonly Dictionary<MotherlodeType, MotherlodeDef> Table = new()
    {
        [MotherlodeType.Laterite] = new MotherlodeDef("Laterite",
            new[] { (ResourceType.IronOre, 0.60f), (ResourceType.Bauxite, 0.40f) },
            new[] { BiomeType.Grassland, BiomeType.Desert }, BuildingType.DeepMine),
        [MotherlodeType.Porphyry] = new MotherlodeDef("Porphyry",
            new[] { (ResourceType.CopperOre, 0.55f), (ResourceType.IronOre, 0.25f), (ResourceType.Quartz, 0.20f) },
            new[] { BiomeType.Highlands }, BuildingType.DeepMine),
        [MotherlodeType.CoalMeasures] = new MotherlodeDef("Coal Measures",
            new[] { (ResourceType.Coal, 0.50f), (ResourceType.IronOre, 0.30f), (ResourceType.Limestone, 0.20f) },
            new[] { BiomeType.Highlands, BiomeType.Forest }, BuildingType.DeepMine),
        [MotherlodeType.SaltDome] = new MotherlodeDef("Salt Dome",
            new[] { (ResourceType.CrudeOil, 0.75f), (ResourceType.Sulfur, 0.25f) },
            new[] { BiomeType.Desert }, BuildingType.DeepWell),
        [MotherlodeType.MineralSands] = new MotherlodeDef("Mineral Sands",
            new[] { (ResourceType.TitaniumOre, 0.50f), (ResourceType.Quartz, 0.40f), (ResourceType.RareEarths, 0.10f) },
            new[] { BiomeType.Tundra }, BuildingType.DeepMine),
        [MotherlodeType.MassiveSulfide] = new MotherlodeDef("Massive Sulfide",
            new[] { (ResourceType.Sulfur, 0.45f), (ResourceType.CopperOre, 0.35f), (ResourceType.IronOre, 0.20f) },
            new[] { BiomeType.Volcanic, BiomeType.Swamp }, BuildingType.DeepMine)
    };

    public static MotherlodeDef For(MotherlodeType type)
    {
        return Table[type];
    }

    /// <summary>The kinds that form in a biome, in enum order.</summary>
    public static List<MotherlodeType> In(BiomeType biome)
    {
        var kinds = new List<MotherlodeType>();
        foreach (MotherlodeType kind in Enum.GetValues<MotherlodeType>())
        {
            if (((IList<BiomeType>)Table[kind].Biomes).Contains(biome))
            {
                kinds.Add(kind);
            }
        }
        return kinds;
    }
}
