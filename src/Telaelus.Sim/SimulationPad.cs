using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The HQ's landing pad, where trucks unload. A loaded truck drives to the pad's front (the spot
/// just outside the HQ's front edge in line with the pad), turns its tail to the entrance, and
/// backs onto the pad; it unloads there, then drives off nose first back to the front and heads
/// home. One truck holds the pad at a time; the others wait in a line in front of it, in the order
/// they arrived (the order is kept in each waiting truck's Timer, so saves need nothing new). The
/// pad moves are short straight runs over open ground, so the HQ's footprint stays solid to paths.
/// When no path reaches the pad's front, a truck unloads beside the HQ as before.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>How far in front of the HQ's center its pad is (the model's pad, 3.6 units forward).</summary>
    public const float PadOffset = 3.6f;

    /// <summary>How far in front of the pad a truck stops before backing on.</summary>
    public const float PadApproach = 2f;

    /// <summary>How far apart waiting trucks stand, nose to tail.</summary>
    public const float QueueSpacing = 1.8f;

    /// <summary>A backing truck's speed, as a share of its driving speed.</summary>
    public const float BackingSpeed = 0.5f;

    private const float AtSpot = 0.05f;

    /// <summary>The pad's center on the ground, or null with no HQ.</summary>
    public Vector3? HqPad => Hq is Building hq ? hq.Position + FrontOf(hq) * PadOffset : null;

    /// <summary>Where a truck stops before backing onto the pad.</summary>
    private Vector3 PadFront(Building hq) => hq.Position + FrontOf(hq) * (PadOffset + PadApproach);

    /// <summary>Where the waiting truck of a rank (0 is next) stands.</summary>
    private Vector3 QueueSpot(Building hq, int rank) => PadFront(hq) + FrontOf(hq) * (PadApproach + QueueSpacing * rank);

    /// <summary>The truck on the pad or backing on or pulling off it, if any.</summary>
    public Truck? PadHolder => _trucks.FirstOrDefault(OnPad);

    private bool OnPad(Truck truck)
    {
        return truck.State is TruckState.BackingOnPad or TruckState.LeavingPad
            || (truck.State == TruckState.Unloading && HqPad is Vector3 pad && truck.Position.DistanceTo(pad) < 0.5f);
    }

    /// <summary>The trucks waiting for the pad, the next first.</summary>
    public IReadOnlyList<Truck> PadQueue => _trucks.Where(t => t.State == TruckState.WaitingForPad).OrderBy(t => t.Timer).ToList();

    /// <summary>A loaded truck's way to the HQ: to the pad's front, or beside the HQ when the front cannot be reached.</summary>
    private List<Vector3>? PathToHq(Truck truck, Building hq)
    {
        Vector3 front = PadFront(hq);
        (int x, int z) = CellAt(front.X, front.Z);
        if (FindPathTo(truck.Position, x, z, truck.Depot) is List<Vector3> path)
        {
            path.Add(front);
            return path;
        }
        return FindPathBeside(truck.Position, hq, truck.Depot);
    }

    /// <summary>A truck has reached the end of its way to the HQ: the pad's front, or a spot beside it.</summary>
    private void ArriveAtHq(Truck truck)
    {
        if (Hq is not Building hq || truck.Position.DistanceTo(PadFront(hq)) > 0.5f)
        {
            StartUnloading(truck);
            return;
        }
        truck.State = TruckState.WaitingForPad;
        truck.Timer = PadQueue.Count == 0 ? 0f : PadQueue.Max(t => t.Timer) + 1f;
        truck.Heading = -FrontOf(hq);
    }

    private static void StartUnloading(Truck truck)
    {
        truck.State = TruckState.Unloading;
        truck.Timer = truck.Amount * TruckDefs.LoadSecondsPerUnit;
    }

    /// <summary>
    /// A waiting truck backs into its place in the line, facing the HQ; the next one drives up to
    /// the pad's front once the pad is free, then turns and backs on.
    /// </summary>
    private void StepWaitingForPad(Truck truck, float dt)
    {
        if (Hq is not Building hq)
        {
            StartUnloading(truck);
            return;
        }
        int rank = Math.Max(0, PadQueue.ToList().IndexOf(truck));
        if (rank == 0 && PadHolder is null)
        {
            if (MoveStraight(truck, PadFront(hq), dt, 1f, nose: true))
            {
                truck.State = TruckState.BackingOnPad;
                truck.Heading = FrontOf(hq);
            }
            return;
        }
        Vector3 spot = QueueSpot(hq, rank);
        if (truck.Position.DistanceTo(spot) > AtSpot)
        {
            // Into line facing the HQ: backing out to a spot further away, driving up to a nearer one.
            bool nearer = spot.DistanceTo(PadFront(hq)) < truck.Position.DistanceTo(PadFront(hq));
            MoveStraight(truck, spot, dt, nearer ? 1f : BackingSpeed, nose: nearer);
        }
    }

    /// <summary>Backs onto the pad, its tail to the entrance; unloads once there.</summary>
    private void StepBackingOnPad(Truck truck, float dt)
    {
        if (Hq is not Building hq || HqPad is not Vector3 pad)
        {
            StartUnloading(truck);
            return;
        }
        if (MoveStraight(truck, pad, dt, BackingSpeed, nose: false))
        {
            truck.Heading = FrontOf(hq);
            StartUnloading(truck);
        }
    }

    /// <summary>Drives off the pad to its front, nose first, then heads home.</summary>
    private void StepLeavingPad(Truck truck, float dt)
    {
        if (Hq is not Building hq)
        {
            GoHome(truck);
            return;
        }
        if (MoveStraight(truck, PadFront(hq), dt, 1f, nose: true))
        {
            GoHome(truck);
        }
    }

    /// <summary>
    /// Moves a truck straight toward a point at a share of its speed, facing the way it moves (nose)
    /// or the other way (backing); true on arrival.
    /// </summary>
    private bool MoveStraight(Truck truck, Vector3 to, float dt, float speed, bool nose)
    {
        Vector3 toward = to - truck.Position;
        float distance = toward.Length();
        float step = TruckDefs.Speed * speed * dt;
        if (distance > 1e-4f)
        {
            truck.Heading = nose ? toward / distance : -toward / distance;
        }
        if (distance <= step)
        {
            truck.Position = to;
            return true;
        }
        truck.Position += toward / distance * step;
        return false;
    }
}
