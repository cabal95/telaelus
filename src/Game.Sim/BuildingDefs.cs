using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim;

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

    public static BuildingDef For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Mine => Mine,
            BuildingType.LumberCamp => LumberCamp,
            BuildingType.Smelter => Smelter,
            BuildingType.Refinery => Refinery,
            BuildingType.OilWell => OilWell,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no building definition")
        };
    }
}
