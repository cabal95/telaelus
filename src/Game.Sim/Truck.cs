using System.Collections.Generic;
using Godot;

namespace Game.Sim;

/// <summary>What a truck is doing.</summary>
public enum TruckState
{
    /// <summary>In its bay at its Depot, waiting for work.</summary>
    Parked,

    /// <summary>Driving to a collector to load.</summary>
    ToCollector,

    /// <summary>Loading from a collector's buffer.</summary>
    Loading,

    /// <summary>Driving its load to the HQ.</summary>
    ToHq,

    /// <summary>Unloading into storage at the HQ.</summary>
    Unloading,

    /// <summary>At the HQ with cargo that storage has no room for yet.</summary>
    WaitingForRoom,

    /// <summary>Driving home to park.</summary>
    ToDepot
}

/// <summary>The fixed numbers every truck shares (placeholders).</summary>
public static class TruckDefs
{
    /// <summary>The most units a truck carries, of one resource.</summary>
    public const float Capacity = 20f;

    /// <summary>World units per second on terrain of speed 1 (the player walks at 6).</summary>
    public const float Speed = 8f;

    /// <summary>Seconds to load at a collector, and again to unload at the HQ.</summary>
    public const float LoadSeconds = 2f;

    /// <summary>A collector's buffer is worth a trip at this many units (or when full).</summary>
    public const float DispatchAt = 10f;

    /// <summary>What a bought truck costs; a Depot's first truck comes with it.</summary>
    public static readonly IReadOnlyList<ResourceAmount> Cost = new ResourceAmount[]
    {
        new(ResourceType.Iron, 15f),
        new(ResourceType.Wood, 10f)
    };
}

/// <summary>
/// A Depot's truck: it drives a collector's buffer to the HQ on its own (see
/// Simulation.StepTrucks). Trucks are units, not buildings: they occupy no cells and pass through
/// the player and each other.
/// </summary>
public sealed class Truck
{
    public int Id { get; }

    /// <summary>The Depot it belongs to and parks at.</summary>
    public Building Depot { get; }

    /// <summary>Its bay at the Depot (0 to the Depot's truck slots less 1).</summary>
    public int Bay { get; }

    /// <summary>Whether it came with the Depot (refunds nothing) rather than being bought.</summary>
    public bool CameWithDepot { get; }

    public Vector3 Position { get; set; }

    /// <summary>The unit direction it last moved in (or faces when parked).</summary>
    public Vector3 Heading { get; set; } = new(0f, 0f, -1f);

    public TruckState State { get; set; } = TruckState.Parked;

    /// <summary>The resource it carries, or null when empty.</summary>
    public ResourceType? Cargo { get; set; }

    public float Amount { get; set; }

    /// <summary>The collector it is heading to or loading from, or null.</summary>
    public Building? Target { get; set; }

    /// <summary>Seconds left loading or unloading.</summary>
    public float Timer { get; set; }

    /// <summary>The cell centers it is following.</summary>
    public IReadOnlyList<Vector3> Path { get; internal set; } = new List<Vector3>();

    /// <summary>The index in <see cref="Path"/> of the next cell to reach.</summary>
    public int PathIndex { get; internal set; }

    public Truck(int id, Building depot, int bay, bool cameWithDepot, Vector3 position)
    {
        Id = id;
        Depot = depot;
        Bay = bay;
        CameWithDepot = cameWithDepot;
        Position = position;
    }
}
