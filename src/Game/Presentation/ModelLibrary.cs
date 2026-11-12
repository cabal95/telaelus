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

    /// <summary>
    /// Turns off shadow casting for every mesh under <paramref name="node"/>. Flat ground
    /// pieces use it: their small surface details would otherwise speckle the ground with
    /// tiny shadows.
    /// </summary>
    public static Node3D WithoutShadows(Node3D node)
    {
        foreach (Node child in node.FindChildren("*", "GeometryInstance3D", true, false))
        {
            ((GeometryInstance3D)child).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        if (node is GeometryInstance3D self)
        {
            self.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return node;
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
