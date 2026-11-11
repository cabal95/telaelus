using Godot;

namespace Game.Sim;

/// <summary>
/// Static definition of a building type: its display name, its footprint in fine
/// cells (width along X and depth along Z when facing North), and for an
/// extractor, the resource it extracts and its work radius in world units.
/// Placement rules belong here too; every type currently uses the standard rule
/// (see Simulation.CanPlace), and an exception such as a water wheel would be a
/// new rule on its own definition.
/// </summary>
public sealed record BuildingDef(string Name, int Width, int Depth, ResourceType? Extracts, float WorkRadius)
{
    public bool IsExtractor => Extracts is not null;

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
    private static readonly BuildingDef Mine = new("Mine", 2, 2, ResourceType.Ore, 6f);
    private static readonly BuildingDef LumberCamp = new("Lumber Camp", 2, 2, ResourceType.Wood, 6f);
    private static readonly BuildingDef Smelter = new("Smelter", 3, 3, null, 0f);
    private static readonly BuildingDef Refinery = new("Refinery", 2, 3, null, 0f);

    public static BuildingDef For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Mine => Mine,
            BuildingType.LumberCamp => LumberCamp,
            BuildingType.Smelter => Smelter,
            _ => Refinery
        };
    }
}
