using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim;

/// <summary>
/// Paths for trucks over fine cells: A* in 8 directions without cutting corners, each step costing
/// its length over the terrain speed of the cell it enters, so a slower cell costs more. A cell is
/// passable when it is walkable and its chunk already exists (a search never generates the world);
/// the cells of a truck's own Depot are passable to it, so it can leave and reach its bay.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The most cells one search may expand before it gives up as unreachable.</summary>
    private const int MaxPathExpansions = 60000;

    private static readonly (int Dx, int Dz)[] PathSteps =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    private bool IsPassable(int x, int z, Building? home)
    {
        if (home is not null && InFootprint(home, x, z))
        {
            return true;
        }
        return InBounds(x, z)
            && IsChunkGenerated(Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z)))
            && IsWalkable(x, z);
    }

    private static bool InFootprint(Building building, int x, int z)
    {
        return x >= building.AnchorX && x < building.AnchorX + building.Width
            && z >= building.AnchorZ && z < building.AnchorZ + building.Depth;
    }

    /// <summary>Whether a cell is a goal: beside the building's footprint by an edge, or inside it when it is the truck's own Depot.</summary>
    private static bool IsBeside(Building building, int x, int z)
    {
        bool alongX = x >= building.AnchorX && x < building.AnchorX + building.Width;
        bool alongZ = z >= building.AnchorZ && z < building.AnchorZ + building.Depth;
        return (alongX && (z == building.AnchorZ - 1 || z == building.AnchorZ + building.Depth))
            || (alongZ && (x == building.AnchorX - 1 || x == building.AnchorX + building.Width));
    }

    /// <summary>
    /// The cheapest path from a world point to a passable cell beside a building's footprint, as
    /// the cell centers to drive through (the start cell left out), or null when none is reachable.
    /// </summary>
    public List<Vector3>? FindPathBeside(Vector3 from, Building building, Building? home = null)
    {
        (int ax, int az, int width, int depth) = ReachOf(building);
        return FindPath(from, (x, z) => IsBesideRect(ax, az, width, depth, x, z),
            (x, z) => OctileToRect(x, z, ax - 1, az - 1, ax + width, az + depth), home);
    }

    private static bool IsBesideRect(int ax, int az, int width, int depth, int x, int z)
    {
        bool alongX = x >= ax && x < ax + width;
        bool alongZ = z >= az && z < az + depth;
        return (alongX && (z == az - 1 || z == az + depth)) || (alongZ && (x == ax - 1 || x == ax + width));
    }

    /// <summary>The cheapest path from a world point to one fine cell, or null when it cannot be reached.</summary>
    public List<Vector3>? FindPathTo(Vector3 from, int goalX, int goalZ, Building? home = null)
    {
        return FindPath(from, (x, z) => x == goalX && z == goalZ, (x, z) => Octile(Math.Abs(x - goalX), Math.Abs(z - goalZ)), home);
    }

    private List<Vector3>? FindPath(Vector3 from, Func<int, int, bool> isGoal, Func<int, int, float> estimate, Building? home)
    {
        (int sx, int sz) = CellAt(from.X, from.Z);
        if (isGoal(sx, sz) && IsPassable(sx, sz, home))
        {
            return new List<Vector3>();
        }
        var open = new PriorityQueue<(int X, int Z), float>();
        var cost = new Dictionary<(int, int), float> { [(sx, sz)] = 0f };
        var came = new Dictionary<(int, int), (int, int)>();
        open.Enqueue((sx, sz), estimate(sx, sz));
        int expansions = 0;
        while (open.TryDequeue(out (int X, int Z) cell, out float priority))
        {
            float here = cost[cell];
            if (priority - estimate(cell.X, cell.Z) > here + 1e-4f)
            {
                continue;
            }
            if (isGoal(cell.X, cell.Z) && cell != (sx, sz))
            {
                return Rebuild(came, cell, (sx, sz));
            }
            if (++expansions > MaxPathExpansions)
            {
                return null;
            }
            foreach ((int dx, int dz) in PathSteps)
            {
                int nx = cell.X + dx;
                int nz = cell.Z + dz;
                if (!IsPassable(nx, nz, home))
                {
                    continue;
                }
                bool diagonal = dx != 0 && dz != 0;
                if (diagonal && (!IsPassable(cell.X + dx, cell.Z, home) || !IsPassable(cell.X, cell.Z + dz, home)))
                {
                    continue;
                }
                float step = (diagonal ? MathF.Sqrt(2f) : 1f) / CellSpeed(nx, nz);
                float next = here + step;
                if (cost.TryGetValue((nx, nz), out float known) && known <= next)
                {
                    continue;
                }
                cost[(nx, nz)] = next;
                came[(nx, nz)] = cell;
                open.Enqueue((nx, nz), next + estimate(nx, nz));
            }
        }
        return null;
    }

    private static List<Vector3> Rebuild(Dictionary<(int, int), (int, int)> came, (int X, int Z) goal, (int X, int Z) start)
    {
        var cells = new List<Vector3>();
        (int X, int Z) cell = goal;
        while (cell != start)
        {
            cells.Add(new Vector3(cell.X + 0.5f, 0f, cell.Z + 0.5f));
            cell = came[cell];
        }
        cells.Reverse();
        return cells;
    }

    /// <summary>The least the rest of a path could cost: its octile distance at road speed, the fastest ground (so A* stays exact).</summary>
    private static float Octile(int dx, int dz)
    {
        int low = Math.Min(dx, dz);
        int high = Math.Max(dx, dz);
        return (high - low + low * MathF.Sqrt(2f)) / RoadSpeed;
    }

    private static float OctileToRect(int x, int z, int minX, int minZ, int maxX, int maxZ)
    {
        int dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
        int dz = z < minZ ? minZ - z : z > maxZ ? z - maxZ : 0;
        return Octile(dx, dz);
    }
}
