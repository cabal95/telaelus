using System;
using System.Collections.Generic;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Loads the game's glTF models. Every model is required: a missing file, or one
/// that is not a <see cref="Node3D"/> scene, throws with its path, so a missing
/// asset fails the game at startup instead of rendering a placeholder. Loaded
/// scenes are cached by path. Also holds the map-cell hash that picks a variant
/// and a 90-degree yaw deterministically per cell.
/// </summary>
public static class ModelLibrary
{
    public const string Root = "res://assets/models/";

    private static readonly Dictionary<string, PackedScene> Cache = new();

    /// <summary>Instances the model at <paramref name="path"/> (relative to <see cref="Root"/>, without ".glb").</summary>
    public static Node3D Instance(string path)
    {
        string full = Root + path + ".glb";
        if (!Cache.TryGetValue(full, out PackedScene? scene))
        {
            if (!ResourceLoader.Exists(full) || GD.Load<PackedScene>(full) is not PackedScene loaded)
            {
                throw new InvalidOperationException($"Model {full} is missing or could not be loaded.");
            }
            scene = loaded;
            Cache[full] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            throw new InvalidOperationException($"Model {full} is not a Node3D scene.");
        }
        return model;
    }

    /// <summary>Instances the model rotated by <paramref name="yawDegrees"/> about Y.</summary>
    public static Node3D Instance(string path, float yawDegrees)
    {
        Node3D model = Instance(path);
        model.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
        return model;
    }

    private static readonly Dictionary<string, (Mesh Mesh, Transform3D Transform)> Meshes = new();

    /// <summary>
    /// The mesh of a single-mesh model (and its transform within the model), for
    /// drawing many copies at once with a multimesh. Throws like <see cref="Instance(string)"/>.
    /// </summary>
    public static (Mesh Mesh, Transform3D Transform) Mesh(string path)
    {
        if (Meshes.TryGetValue(path, out (Mesh, Transform3D) cached))
        {
            return cached;
        }
        Node3D model = Instance(path);
        MeshInstance3D? found = null;
        foreach (Node node in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            found = (MeshInstance3D)node;
            break;
        }
        if (model is MeshInstance3D self)
        {
            found = self;
        }
        if (found?.Mesh is not Mesh mesh)
        {
            model.Free();
            throw new InvalidOperationException($"Model {Root + path}.glb has no mesh.");
        }

        Transform3D transform = Transform3D.Identity;
        for (Node? node = found; node is not null && node != model; node = node.GetParent())
        {
            if (node is Node3D n)
            {
                transform = n.Transform * transform;
            }
        }
        model.Free();
        Meshes[path] = (mesh, transform);
        return (mesh, transform);
    }

    /// <summary>A deterministic hash of a map cell, for picking variants and rotations.</summary>
    public static uint CellHash(int mapX, int mapZ)
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

    /// <summary>Variant suffix (a, b, or c) for a map cell.</summary>
    public static string Variant(int mapX, int mapZ)
    {
        return ((char)('a' + CellHash(mapX, mapZ) % 3)).ToString();
    }

    /// <summary>A yaw in 90-degree steps for a map cell.</summary>
    public static float CellYaw(int mapX, int mapZ)
    {
        return 90f * (CellHash(mapX, mapZ) / 3 % 4);
    }

    /// <summary>
    /// The yaw that turns a piece authored toward North so it faces <paramref name="edge"/>.
    /// A positive yaw about Y turns North toward West.
    /// </summary>
    public static float EdgeYaw(Game.Sim.Facing edge)
    {
        return edge switch
        {
            Game.Sim.Facing.North => 0f,
            Game.Sim.Facing.West => 90f,
            Game.Sim.Facing.South => 180f,
            _ => 270f
        };
    }
}
