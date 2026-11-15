using System;

namespace Game.Sim;

/// <summary>Static rate and recipe definitions for each building type.</summary>
public static class Recipes
{
    private static readonly Recipe SmelterRecipe = new(
        new ResourceAmount[]
        {
            new(ResourceType.IronOre, 1f)
        },
        new ResourceAmount[]
        {
            new(ResourceType.Iron, 1f)
        },
        16f);

    private static readonly Recipe RefineryRecipe = new(
        new ResourceAmount[]
        {
            new(ResourceType.CrudeOil, 1f)
        },
        new ResourceAmount[]
        {
            new(ResourceType.RocketFuel, 1f)
        },
        8f);

    private static readonly Recipe GlassworksRecipe = new(
        Array.Empty<ResourceAmount>(),
        new ResourceAmount[]
        {
            new(ResourceType.Glass, 1f)
        },
        12f);

    private static readonly Recipe CharcoalKilnRecipe = new(
        new ResourceAmount[]
        {
            new(ResourceType.Wood, 3f)
        },
        new ResourceAmount[]
        {
            new(ResourceType.Charcoal, 1f)
        },
        15f);

    /// <summary>The recipe for a processing building, or null for non-processors.</summary>
    public static Recipe? For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Smelter => SmelterRecipe,
            BuildingType.Refinery => RefineryRecipe,
            BuildingType.Glassworks => GlassworksRecipe,
            BuildingType.CharcoalKiln => CharcoalKilnRecipe,
            _ => null
        };
    }
}
