namespace Game.Sim;

/// <summary>
/// The rules of a terrain type. Speed multiplies the player's speed while they
/// stand on it (1 on unwalkable terrain, so a player teleported there can walk off); Buildable gates building and field placement; TakesFeatures says
/// whether a mountain or river may sit on it; DrawPriority decides transitions
/// (a higher-priority neighbor draws its edge over a lower one).
/// </summary>
public sealed record TerrainDef(bool Walkable, float Speed, bool Buildable, bool TakesFeatures, int DrawPriority);

/// <summary>The definition table for every terrain type.</summary>
public static class TerrainDefs
{
    private static readonly TerrainDef Grass = new(Walkable: true, Speed: 1f, Buildable: true, TakesFeatures: true, DrawPriority: 6);
    private static readonly TerrainDef Snow = new(Walkable: true, Speed: 0.75f, Buildable: true, TakesFeatures: true, DrawPriority: 5);
    private static readonly TerrainDef Sand = new(Walkable: true, Speed: 0.75f, Buildable: true, TakesFeatures: true, DrawPriority: 4);
    private static readonly TerrainDef Mud = new(Walkable: true, Speed: 0.6f, Buildable: false, TakesFeatures: true, DrawPriority: 3);
    private static readonly TerrainDef Rock = new(Walkable: true, Speed: 1f, Buildable: true, TakesFeatures: true, DrawPriority: 2);
    private static readonly TerrainDef Lava = new(Walkable: false, Speed: 1f, Buildable: false, TakesFeatures: false, DrawPriority: 1);
    private static readonly TerrainDef ShallowWater = new(Walkable: false, Speed: 1f, Buildable: false, TakesFeatures: false, DrawPriority: 0);
    private static readonly TerrainDef DeepWater = new(Walkable: false, Speed: 1f, Buildable: false, TakesFeatures: false, DrawPriority: 0);

    public static TerrainDef For(TerrainType type)
    {
        return type switch
        {
            TerrainType.Grass => Grass,
            TerrainType.Snow => Snow,
            TerrainType.Sand => Sand,
            TerrainType.Mud => Mud,
            TerrainType.Rock => Rock,
            TerrainType.Lava => Lava,
            TerrainType.ShallowWater => ShallowWater,
            TerrainType.DeepWater => DeepWater,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, "no terrain definition")
        };
    }

    /// <summary>Whether a terrain is water (a river connects into it).</summary>
    public static bool IsWater(TerrainType type)
    {
        return type is TerrainType.ShallowWater or TerrainType.DeepWater;
    }
}
