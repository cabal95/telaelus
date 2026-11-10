namespace Game.Sim;

/// <summary>Static rate and recipe definitions for each building type.</summary>
public static class Recipes
{
    /// <summary>Units per second an Extractor pulls from its node.</summary>
    public const float ExtractorRate = 5f;

    private static readonly Recipe SmelterRecipe = new(
        new ResourceAmount[]
        {
            new(ResourceType.Ore, 1f)
        },
        new ResourceAmount[]
        {
            new(ResourceType.Metal, 1f)
        },
        1f);

    private static readonly Recipe RefineryRecipe = new(
        new ResourceAmount[]
        {
            new(ResourceType.Metal, 1f),
            new(ResourceType.Wood, 1f)
        },
        new ResourceAmount[]
        {
            new(ResourceType.Fuel, 1f)
        },
        2f);

    /// <summary>The recipe for a processing building, or null for non-processors.</summary>
    public static Recipe? For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Smelter => SmelterRecipe,
            BuildingType.Refinery => RefineryRecipe,
            _ => null
        };
    }
}
