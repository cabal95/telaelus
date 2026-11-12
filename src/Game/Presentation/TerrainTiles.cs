using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds the visual for one map cell from the generated terrain models (see
/// src/Game/assets/models/terrain/README.md). The cell's own model is a ground
/// tile, or the river piece or mountain that covers the cell instead. Transitions
/// follow draw priority: on each edge whose neighbor outranks the cell, the cell
/// draws that neighbor's edge piece, and each corner draws the corner piece of the
/// highest-ranked terrain among the three cells around it. Edge and corner pieces
/// are authored for the North edge and the North-West corner and turned into place.
/// Ground pieces cast no shadows (they are flat; only mountains stand up).
/// </summary>
public static class TerrainTiles
{
    public static Node3D CreateTile(Simulation sim, int mapX, int mapZ)
    {
        TerrainType terrain = sim.MapTerrainAt(mapX, mapZ);
        var root = new Node3D
        {
            Name = terrain.ToString()
        };

        switch (sim.MapFeatureAt(mapX, mapZ))
        {
            case MapFeature.Mountain:
                root.AddChild(MountainModels.Create(terrain, mapX, mapZ));
                break;
            case MapFeature.River:
                root.AddChild(ModelLibrary.WithoutShadows(RiverPieces.Create(sim, mapX, mapZ, terrain)));
                break;
            default:
                root.AddChild(ModelLibrary.WithoutShadows(ModelLibrary.Instance(
                    $"terrain/ground_{FileName(terrain)}_{ModelLibrary.Variant(mapX, mapZ)}",
                    ModelLibrary.CellYaw(mapX, mapZ))));
                break;
        }

        int own = TerrainDefs.For(terrain).DrawPriority;
        foreach (Facing edge in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
        {
            AddEdge(root, sim, own, mapX, mapZ, edge);
        }
        AddCorner(root, sim, own, mapX, mapZ, -1, -1, 0f);
        AddCorner(root, sim, own, mapX, mapZ, -1, 1, 90f);
        AddCorner(root, sim, own, mapX, mapZ, 1, 1, 180f);
        AddCorner(root, sim, own, mapX, mapZ, 1, -1, 270f);
        return root;
    }

    /// <summary>The terrain's name in model file names (snake_case, for example shallow_water).</summary>
    public static string FileName(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.ShallowWater => "shallow_water",
            TerrainType.DeepWater => "deep_water",
            _ => terrain.ToString().ToLowerInvariant()
        };
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
    /// Adds the neighbor's edge piece when it outranks this cell. Where a river flows
    /// in across the edge, the mouth piece leaves a gap so the channel meets the cell.
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

        bool riverMouth = sim.RiverConnects(neighborX, neighborZ, (Facing)(((int)edge + 2) % 4));
        string piece = riverMouth ? "edge_mouth_" : "edge_";
        root.AddChild(ModelLibrary.WithoutShadows(ModelLibrary.Instance("terrain/" + piece + FileName(neighbor), ModelLibrary.EdgeYaw(edge))));
    }

    /// <summary>
    /// Adds the corner piece of the highest-priority terrain among the three cells
    /// around the corner (sx, sz), when it outranks this cell. The piece is authored
    /// for the North-West corner and turned by <paramref name="yaw"/>.
    /// </summary>
    private static void AddCorner(Node3D root, Simulation sim, int own, int mapX, int mapZ, int sx, int sz, float yaw)
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
        if (best is TerrainType corner)
        {
            root.AddChild(ModelLibrary.WithoutShadows(ModelLibrary.Instance("terrain/corner_" + FileName(corner), yaw)));
        }
    }
}
