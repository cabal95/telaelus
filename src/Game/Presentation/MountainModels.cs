using System;
using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds the generated mountain model for one map cell. The family (stone or
/// snow) follows the terrain beneath the mountain; the shape variant and a yaw in
/// 90-degree steps follow the map cell's coordinates, so a mountain looks the same
/// every run. Each model fills one map cell exactly with every edge flat on the
/// ground, so it is placed unscaled and stands alone or beside other mountains.
/// The models are required: a missing file throws.
/// </summary>
public static class MountainModels
{
    private const string Folder = "res://assets/models/terrain/";

    private static readonly string[] StonePaths =
    {
        Folder + "mountain_a.glb",
        Folder + "mountain_b.glb",
        Folder + "mountain_c.glb"
    };

    private static readonly string[] SnowPaths =
    {
        Folder + "mountain_snow_a.glb",
        Folder + "mountain_snow_b.glb",
        Folder + "mountain_snow_c.glb"
    };

    private static readonly Dictionary<TerrainType, string[]> Families = new()
    {
        [TerrainType.Grass] = StonePaths,
        [TerrainType.Sand] = StonePaths,
        [TerrainType.Mud] = StonePaths,
        [TerrainType.Rock] = StonePaths,
        [TerrainType.Snow] = SnowPaths
    };

    private static readonly Dictionary<string, PackedScene> Cache = new();

    public static Node3D Create(TerrainType terrain, int mapX, int mapZ)
    {
        if (!Families.TryGetValue(terrain, out string[]? paths))
        {
            throw new InvalidOperationException($"No mountain model family for terrain {terrain}.");
        }

        uint hash = Hash(mapX, mapZ);
        string path = paths[hash % (uint)paths.Length];
        if (Load(path).Instantiate() is not Node3D model)
        {
            throw new InvalidOperationException($"Mountain model {path} is not a Node3D scene.");
        }

        model.Name = "Mountain";
        model.RotationDegrees = new Vector3(0f, 90f * (hash / (uint)paths.Length % 4), 0f);
        return model;
    }

    private static PackedScene Load(string path)
    {
        if (Cache.TryGetValue(path, out PackedScene? scene))
        {
            return scene;
        }
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path) is not PackedScene loaded)
        {
            throw new InvalidOperationException($"Mountain model {path} is missing or could not be loaded.");
        }
        Cache[path] = loaded;
        return loaded;
    }

    private static uint Hash(int mapX, int mapZ)
    {
        unchecked
        {
            uint h = (uint)mapX * 73856093u ^ (uint)mapZ * 19349663u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h;
        }
    }
}
