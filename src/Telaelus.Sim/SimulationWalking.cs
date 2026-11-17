using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The player walks where they are sent (<see cref="WalkTo"/>): along the cheapest path over explored,
/// walkable cells (the trucks' search, with the player's rule), to the point itself, or when it is
/// blocked or under the fog, to the reachable cell nearest it. A walk that ends short of its target
/// because the fog was in the way looks again once more ground has been explored, so a click into the
/// fog keeps the player walking into it.
/// </summary>
public sealed partial class Simulation
{
    // ExploredCount when the current path was planned: a walk that stopped short plans again once it grows.
    private int _walkPlannedAt;

    /// <summary>
    /// Sends the player walking to a world point. Returns false (and stops any walk) when no step
    /// toward it can be taken.
    /// </summary>
    public bool WalkTo(float x, float z)
    {
        Player.Target = new Vector3(x, 0f, z);
        return PlanWalk();
    }

    /// <summary>Stops the player where they stand.</summary>
    public void StopWalking()
    {
        Player.Target = null;
        Player.Path.Clear();
        Player.Heading = Vector2.Zero;
    }

    private bool PlayerCanWalk(int x, int z)
    {
        return InBounds(x, z)
            && IsChunkGenerated(Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z)))
            && IsExplored(MapCellOf(x), MapCellOf(z))
            && IsWalkable(x, z);
    }

    // Plans the path to the target: false (and the walk stopped) when there is no step to take.
    private bool PlanWalk()
    {
        Vector3 target = Player.Target!.Value;
        (int gx, int gz) = CellAt(target.X, target.Z);
        PathResult result = FindPath(Player.Position, (x, z) => x == gx && z == gz,
            (x, z) => Octile(Math.Abs(x - gx), Math.Abs(z - gz)), PlayerCanWalk, closest: true);
        _walkPlannedAt = ExploredCount;
        List<Vector3> path = result.Path!;
        if (result.Reached)
        {
            // End on the point itself, not its cell's center.
            if (path.Count > 0)
            {
                path[^1] = target;
            }
            else
            {
                path.Add(target);
            }
        }
        if (path.Count == 0)
        {
            StopWalking();
            return false;
        }
        Player.Path.Clear();
        Player.Path.AddRange(path);
        return true;
    }

    private void WalkPlayer(double dt)
    {
        Player.Heading = Vector2.Zero;
        if (Player.Target is null)
        {
            return;
        }
        Vector3 p = Player.Position;
        float budget = Player.Speed * TerrainSpeedAt(p.X, p.Z) * (float)dt;
        bool replanned = false;
        while (budget > 0f && Player.Path.Count > 0)
        {
            Vector3 next = Player.Path[0];
            (int nx, int nz) = CellAt(next.X, next.Z);
            if (!IsWalkable(nx, nz))
            {
                // A building was placed across the path: look again, once a tick.
                if (replanned || !PlanWalk())
                {
                    StopWalking();
                    break;
                }
                replanned = true;
                continue;
            }
            Vector3 to = next - p;
            float distance = to.Length();
            if (distance > 0f)
            {
                Player.Heading = new Vector2(to.X, to.Z) / distance;
            }
            if (distance <= budget)
            {
                p = next;
                budget -= distance;
                Player.Path.RemoveAt(0);
            }
            else
            {
                p += to / distance * budget;
                budget = 0f;
            }
        }
        Player.Position = p;

        if (Player.Target is not null && Player.Path.Count == 0)
        {
            // Stopped short (the target under the fog): go on once the walk has shown more ground.
            bool shortOfTarget = CellAt(p.X, p.Z) != CellAt(Player.Target.Value.X, Player.Target.Value.Z);
            if (!(shortOfTarget && ExploredCount != _walkPlannedAt && PlanWalk()))
            {
                Player.Target = null;
            }
        }
    }
}
