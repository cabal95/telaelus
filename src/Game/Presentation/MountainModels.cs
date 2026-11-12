using System;
using Game.Sim;

namespace Game.Presentation;

/// <summary>
/// Picks the generated mountain model for one map cell. The family (stone or
/// snow) follows the terrain beneath the mountain; the shape variant and a yaw in
/// 90-degree steps follow the map cell's coordinates, so a mountain looks the same
/// every run. Each model fills one map cell exactly with every edge flat on the
/// ground, so it is placed unscaled and stands alone or beside other mountains.
/// </summary>
public static class MountainModels
{
    /// <summary>The model path and yaw for a mountain on <paramref name="terrain"/> at a map cell.</summary>
    public static (string Path, float Yaw) Model(TerrainType terrain, int mapX, int mapZ)
    {
        string family = terrain switch
        {
            TerrainType.Snow => "mountain_snow_",
            TerrainType.Grass or TerrainType.Sand or TerrainType.Mud or TerrainType.Rock => "mountain_",
            _ => throw new InvalidOperationException($"No mountain model family for terrain {terrain}.")
        };
        return ("terrain/" + family + ModelLibrary.Variant(mapX, mapZ), ModelLibrary.CellYaw(mapX, mapZ));
    }
}
