using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// Paths over fine cells for trucks and the player: A* in 8 directions without cutting corners, each
/// step costing its length over the terrain speed of the cell it enters, so a slower cell costs more.
/// For a truck a cell is passable when it is walkable and its chunk already exists (a search never
/// generates the world), and the cells of its own Depot are passable to it, so it can leave and reach
/// its bay; for the player it must also be explored (<see cref="Simulation.WalkTo"/>).
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
            (x, z) => OctileToRect(x, z, ax - 1, az - 1, ax + width, az + depth), (x, z) => IsPassable(x, z, home)).Path;
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
        return FindPath(from, (x, z) => x == goalX && z == goalZ, (x, z) => Octile(Math.Abs(x - goalX), Math.Abs(z - goalZ)),
            (x, z) => IsPassable(x, z, home)).Path;
    }

    /// <summary>
    /// A search's result: the path (the cell centers to go through, the start cell left out), or null
    /// when no goal was reached. With <c>closest</c>, an unreached goal gives the path to the cell the
    /// search found nearest it instead (empty when that is the start), and <see cref="Reached"/> is false.
    /// </summary>
    private readonly record struct PathResult(List<Vector3>? Path, bool Reached);

    private PathResult FindPath(Vector3 from, Func<int, int, bool> isGoal, Func<int, int, float> estimate,
        Func<int, int, bool> passable, bool closest = false)
    {
        (int sx, int sz) = CellAt(from.X, from.Z);
        if (isGoal(sx, sz) && passable(sx, sz))
        {
            return new PathResult(new List<Vector3>(), true);
        }
        var open = new PriorityQueue<(int X, int Z), float>();
        var cost = new Dictionary<(int, int), float> { [(sx, sz)] = 0f };
        var came = new Dictionary<(int, int), (int, int)>();
        open.Enqueue((sx, sz), estimate(sx, sz));
        int expansions = 0;
        (int X, int Z) nearest = (sx, sz);
        float nearestLeft = estimate(sx, sz);
        while (open.TryDequeue(out (int X, int Z) cell, out float priority))
        {
            float here = cost[cell];
            float left = estimate(cell.X, cell.Z);
            if (priority - left > here + 1e-4f)
            {
                continue;
            }
            if (isGoal(cell.X, cell.Z) && cell != (sx, sz))
            {
                return new PathResult(Rebuild(came, cell, (sx, sz)), true);
            }
            if (left < nearestLeft)
            {
                nearest = cell;
                nearestLeft = left;
            }
            if (++expansions > MaxPathExpansions)
            {
                break;
            }
            foreach ((int dx, int dz) in PathSteps)
            {
                int nx = cell.X + dx;
                int nz = cell.Z + dz;
                if (!passable(nx, nz))
                {
                    continue;
                }
                bool diagonal = dx != 0 && dz != 0;
                if (diagonal && (!passable(cell.X + dx, cell.Z) || !passable(cell.X, cell.Z + dz)))
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
        return closest ? new PathResult(Rebuild(came, nearest, (sx, sz)), false) : new PathResult(null, false);
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
