using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a visual for one map cell (FineCellsPerMapCell x FineCellsPerMapCell
/// world units). For now these are flat colored tiles (a placeholder for CC0 tile
/// models), one color per terrain. Transitions follow draw priority: on each edge
/// whose neighbor has a higher priority, the cell draws a strip in the neighbor's
/// color, so each terrain needs one edge set rather than one per pair. Features
/// draw on top: a mountain is a peak tinted by the terrain beneath it, and a river
/// is water laid over its channel fine cells.
/// </summary>
public static class TerrainTiles
{
    private const float Size = Simulation.FineCellsPerMapCell;
    private const float EdgeWidth = 0.4f;

    private static readonly Dictionary<TerrainType, Color> TerrainColors = new()
    {
        [TerrainType.Grass] = new Color(0.33f, 0.49f, 0.27f),
        [TerrainType.Sand] = new Color(0.78f, 0.70f, 0.48f),
        [TerrainType.Snow] = new Color(0.90f, 0.92f, 0.95f),
        [TerrainType.Rock] = new Color(0.48f, 0.46f, 0.44f),
        [TerrainType.Mud] = new Color(0.38f, 0.32f, 0.22f),
        [TerrainType.ShallowWater] = new Color(0.20f, 0.38f, 0.60f),
        [TerrainType.DeepWater] = new Color(0.10f, 0.22f, 0.42f),
        [TerrainType.Lava] = new Color(0.85f, 0.30f, 0.08f)
    };

    private static readonly Color RiverColor = new(0.24f, 0.45f, 0.68f);
    private static readonly Color SnowCapColor = new(0.97f, 0.98f, 1.0f);

    public static Node3D CreateTile(Simulation sim, int mapX, int mapZ)
    {
        TerrainType terrain = sim.MapTerrainAt(mapX, mapZ);
        var root = new Node3D
        {
            Name = terrain.ToString()
        };
        root.AddChild(FlatTile(terrain));
        AddEdges(root, sim, mapX, mapZ, terrain);

        switch (sim.MapFeatureAt(mapX, mapZ))
        {
            case MapFeature.Mountain:
                AddMountain(root, terrain);
                break;
            case MapFeature.River:
                AddRiver(root, sim, mapX, mapZ);
                break;
        }
        return root;
    }

    private static Color ColorOf(TerrainType terrain)
    {
        return TerrainColors.TryGetValue(terrain, out Color color) ? color : Colors.Magenta;
    }

    private static StandardMaterial3D Material(Color color, bool glow = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color
        };
        if (glow)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 0.6f;
        }
        return material;
    }

    private static MeshInstance3D Box(string name, Vector3 size, Vector3 position, Color color, bool glow = false)
    {
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh
            {
                Size = size
            },
            MaterialOverride = Material(color, glow),
            Position = position
        };
    }

    private static MeshInstance3D FlatTile(TerrainType terrain)
    {
        return Box("Tile", new Vector3(Size, 0.1f, Size), new Vector3(0f, -0.05f, 0f), ColorOf(terrain), terrain == TerrainType.Lava);
    }

    private static void AddEdges(Node3D root, Simulation sim, int mapX, int mapZ, TerrainType terrain)
    {
        int own = TerrainDefs.For(terrain).DrawPriority;
        AddEdge(root, sim, own, mapX, mapZ, Facing.North);
        AddEdge(root, sim, own, mapX, mapZ, Facing.South);
        AddEdge(root, sim, own, mapX, mapZ, Facing.East);
        AddEdge(root, sim, own, mapX, mapZ, Facing.West);
        AddCorner(root, sim, own, mapX, mapZ, -1, -1);
        AddCorner(root, sim, own, mapX, mapZ, 1, -1);
        AddCorner(root, sim, own, mapX, mapZ, -1, 1);
        AddCorner(root, sim, own, mapX, mapZ, 1, 1);
    }

    /// <summary>The terrain of an in-bounds neighbor that outranks this cell, or null.</summary>
    private static TerrainType? Outranking(Simulation sim, int own, int mapX, int mapZ)
    {
        if (!sim.MapInBounds(mapX, mapZ))
        {
            return null;
        }
        TerrainType terrain = sim.MapTerrainAt(mapX, mapZ);
        return TerrainDefs.For(terrain).DrawPriority > own ? terrain : null;
    }

    /// <summary>
    /// Draws the neighbor's edge strip when it outranks this cell. The strip stops
    /// short of both corners (corners are drawn separately). Where a river flows in
    /// across the edge, the middle of the strip is left open so the channel meets
    /// the water.
    /// </summary>
    private static void AddEdge(Node3D root, Simulation sim, int own, int mapX, int mapZ, Facing edge)
    {
        (int dx, int dz) = Simulation.EdgeOffset(edge);
        int neighborX = mapX + dx;
        int neighborZ = mapZ + dz;
        if (Outranking(sim, own, neighborX, neighborZ) is not TerrainType neighbor)
        {
            return;
        }

        bool alongX = dz != 0;
        Vector3 across = new Vector3(dx, 0f, dz) * ((Size - EdgeWidth) * 0.5f) + new Vector3(0f, StripY, 0f);
        Vector3 along = alongX ? Vector3.Right : Vector3.Back;
        Color color = ColorOf(neighbor);

        bool riverMouth = sim.RiverConnects(neighborX, neighborZ, (Facing)(((int)edge + 2) % 4));
        if (!riverMouth)
        {
            root.AddChild(Box("Edge", StripSize(alongX, Size - 2f * EdgeWidth), across, color));
            return;
        }

        // Two pieces between the corners and the 2-wide channel.
        float piece = Size * 0.25f - EdgeWidth;
        float offset = Size * 0.5f - EdgeWidth - piece * 0.5f;
        root.AddChild(Box("Edge", StripSize(alongX, piece), across + along * offset, color));
        root.AddChild(Box("Edge", StripSize(alongX, piece), across - along * offset, color));
    }

    /// <summary>
    /// Draws a corner square in the color of the highest-priority terrain among the
    /// three cells around that corner (the two edge neighbors and the diagonal one),
    /// when it outranks this cell. This keeps transitions continuous where three
    /// terrains meet.
    /// </summary>
    private static void AddCorner(Node3D root, Simulation sim, int own, int mapX, int mapZ, int sx, int sz)
    {
        TerrainType? best = null;
        int bestPriority = own;
        foreach ((int nx, int nz) in new[] { (mapX + sx, mapZ), (mapX, mapZ + sz), (mapX + sx, mapZ + sz) })
        {
            if (Outranking(sim, bestPriority, nx, nz) is TerrainType terrain)
            {
                best = terrain;
                bestPriority = TerrainDefs.For(terrain).DrawPriority;
            }
        }
        if (best is not TerrainType corner)
        {
            return;
        }

        float offset = (Size - EdgeWidth) * 0.5f;
        root.AddChild(Box("Corner", new Vector3(EdgeWidth, StripThickness, EdgeWidth), new Vector3(sx * offset, StripY, sz * offset), ColorOf(corner)));
    }

    private const float StripY = 0.01f;
    private const float StripThickness = 0.01f;

    private static Vector3 StripSize(bool alongX, float length)
    {
        return alongX ? new Vector3(length, StripThickness, EdgeWidth) : new Vector3(EdgeWidth, StripThickness, length);
    }

    private static void AddMountain(Node3D root, TerrainType terrain)
    {
        // The peak takes the ground's color, darkened to read as stone; on snow it
        // is pale stone with a white cap.
        Color ground = ColorOf(terrain);
        Color stone = terrain == TerrainType.Snow
            ? new Color(0.62f, 0.64f, 0.68f)
            : ground.Lerp(ColorOf(TerrainType.Rock), 0.55f).Darkened(0.1f);

        const float height = 2.4f;
        root.AddChild(new MeshInstance3D
        {
            Name = "Peak",
            Mesh = new CylinderMesh
            {
                TopRadius = 0f,
                BottomRadius = Size * 0.45f,
                Height = height
            },
            MaterialOverride = Material(stone),
            Position = new Vector3(0f, height * 0.5f, 0f)
        });

        if (terrain == TerrainType.Snow)
        {
            const float capHeight = height * 0.4f;
            root.AddChild(new MeshInstance3D
            {
                Name = "SnowCap",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0f,
                    BottomRadius = Size * 0.45f * 0.4f + 0.03f,
                    Height = capHeight
                },
                MaterialOverride = Material(SnowCapColor),
                Position = new Vector3(0f, height - capHeight * 0.5f + 0.01f, 0f)
            });
        }
    }

    private static void AddRiver(Node3D root, Simulation sim, int mapX, int mapZ)
    {
        // Matches Simulation.IsRiverChannel: the middle 2 x 2 fine cells plus a
        // 2-wide arm to each connected edge.
        // Above every edge strip, so the channel crosses its own cell's edges.
        const float y = 0.045f;
        const float thickness = 0.01f;
        float arm = Size * 0.25f;
        float armCenter = Size * 0.5f - arm * 0.5f;
        root.AddChild(Box("Channel", new Vector3(Size * 0.5f, thickness, Size * 0.5f), new Vector3(0f, y, 0f), RiverColor));

        if (sim.RiverConnects(mapX, mapZ, Facing.North))
        {
            root.AddChild(Box("ChannelNorth", new Vector3(Size * 0.5f, thickness, arm), new Vector3(0f, y, -armCenter), RiverColor));
        }
        if (sim.RiverConnects(mapX, mapZ, Facing.South))
        {
            root.AddChild(Box("ChannelSouth", new Vector3(Size * 0.5f, thickness, arm), new Vector3(0f, y, armCenter), RiverColor));
        }
        if (sim.RiverConnects(mapX, mapZ, Facing.East))
        {
            root.AddChild(Box("ChannelEast", new Vector3(arm, thickness, Size * 0.5f), new Vector3(armCenter, y, 0f), RiverColor));
        }
        if (sim.RiverConnects(mapX, mapZ, Facing.West))
        {
            root.AddChild(Box("ChannelWest", new Vector3(arm, thickness, Size * 0.5f), new Vector3(-armCenter, y, 0f), RiverColor));
        }
    }
}
