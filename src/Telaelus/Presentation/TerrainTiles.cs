using System.Collections.Generic;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>One terrain model to draw: its path under the models folder, where, how turned, and whether it casts shadows.</summary>
public readonly record struct Placement(string Path, Vector3 Position, float Yaw, bool CastsShadow);

/// <summary>
/// Lists the terrain models for one map cell, from the generated terrain models (see
/// src/Telaelus/assets/models/terrain/README.md). The cell's own model is a ground tile,
/// or the river piece or mountain that covers the cell instead. Transitions follow
/// draw priority: on each edge whose neighbor outranks the cell, the cell draws that
/// neighbor's edge piece, and each corner draws the corner piece of the
/// highest-ranked terrain among the three cells around it. Edge and corner pieces
/// are authored for the North edge and the North-West corner and turned into place.
/// Ground pieces cast no shadows (they are flat; only mountains stand up).
/// </summary>
public static class TerrainTiles
{
    /// <summary>Adds the cell's placements, positioned relative to <paramref name="origin"/>.</summary>
    public static void AddPlacements(Simulation sim, int mapX, int mapZ, Vector3 origin, List<Placement> into)
    {
        Vector3 at = Simulation.MapCellCenter(mapX, mapZ) - origin;
        TerrainType terrain = sim.MapTerrainAt(mapX, mapZ);

        switch (sim.MapFeatureAt(mapX, mapZ))
        {
            case MapFeature.Mountain:
                (string mountain, float mountainYaw) = MountainModels.Model(terrain, mapX, mapZ);
                into.Add(new Placement(mountain, at, mountainYaw, true));
                break;
            case MapFeature.River:
                (string river, float riverYaw) = RiverPieces.Model(sim, mapX, mapZ, terrain);
                into.Add(new Placement(river, at, riverYaw, false));
                break;
            default:
                into.Add(new Placement(
                    $"terrain/ground_{FileName(terrain)}_{ModelLibrary.Variant(mapX, mapZ)}",
                    at,
                    ModelLibrary.CellYaw(mapX, mapZ),
                    false));
                break;
        }

        int own = TerrainDefs.For(terrain).DrawPriority;
        foreach (Facing edge in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
        {
            AddEdge(sim, own, mapX, mapZ, edge, at, into);
        }
        AddCorner(sim, own, mapX, mapZ, -1, -1, 0f, at, into);
        AddCorner(sim, own, mapX, mapZ, -1, 1, 90f, at, into);
        AddCorner(sim, own, mapX, mapZ, 1, 1, 180f, at, into);
        AddCorner(sim, own, mapX, mapZ, 1, -1, 270f, at, into);
    }

    /// <summary>The terrain's name in model file names (snake_case, for example shallow_water).</summary>
    public static string FileName(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.ShallowWater => "shallow_water",
            TerrainType.DeepWater => "deep_water",
            TerrainType.SaltFlat => "salt_flat",
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
    private static void AddEdge(Simulation sim, int own, int mapX, int mapZ, Facing edge, Vector3 at, List<Placement> into)
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
        into.Add(new Placement("terrain/" + piece + FileName(neighbor), at, ModelLibrary.EdgeYaw(edge), false));
    }

    /// <summary>
    /// Adds the corner piece of the highest-priority terrain among the three cells
    /// around the corner (sx, sz), when it outranks this cell. The piece is authored
    /// for the North-West corner and turned by <paramref name="yaw"/>.
    /// </summary>
    private static void AddCorner(Simulation sim, int own, int mapX, int mapZ, int sx, int sz, float yaw, Vector3 at, List<Placement> into)
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
            into.Add(new Placement("terrain/corner_" + FileName(corner), at, yaw, false));
        }
    }
}
