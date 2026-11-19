using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// Depot trucks. Each tick, after buildings work, every truck steps through its trip: an idle
/// truck of a switched-on, online Depot takes the collector with the biggest load storage can take
/// (at least <see cref="TruckDefs.DispatchAt"/>, or a full buffer's) that no other truck is heading
/// to and that it can reach, nearest first on a tie; it drives beside it, loads that resource (up
/// to <see cref="TruckDefs.Capacity"/>, and only what storage has room for once the other trucks'
/// loads on the way are counted), drives beside the HQ, unloads all of it, and is idle again.
/// Loading and unloading take <see cref="TruckDefs.LoadSecondsPerUnit"/> a unit. With nothing to do
/// it drives home and parks. A collector is any extractor, online or offline.
/// </summary>
public sealed partial class Simulation
{
    private readonly List<Truck> _trucks = new();
    private int _nextTruckId;

    // Collectors a truck found no path to, by (collector id, truck id); cleared when buildings change.
    private readonly HashSet<(int Collector, int Truck)> _unreachable = new();

    public IReadOnlyList<Truck> Trucks => _trucks;

    /// <summary>A Depot's trucks, in bay order.</summary>
    public List<Truck> TrucksOf(Building depot)
    {
        var trucks = _trucks.FindAll(t => ReferenceEquals(t.Depot, depot));
        trucks.Sort((a, b) => a.Bay.CompareTo(b.Bay));
        return trucks;
    }

    /// <summary>Whether a Depot can buy another truck now: a free bay and the cost in storage.</summary>
    public bool CanBuyTruck(Building depot)
    {
        return depot.Def.TruckSlots > 0 && TrucksOf(depot).Count < depot.Def.TruckSlots && HasAll(TruckDefs.Cost);
    }

    /// <summary>Buys a truck at a Depot: pays its cost and parks it in the next free bay. False when it cannot.</summary>
    public bool TryBuyTruck(int depotId)
    {
        Building? depot = BuildingById(depotId);
        if (depot is null || !CanBuyTruck(depot))
        {
            return false;
        }
        foreach (ResourceAmount cost in TruckDefs.Cost)
        {
            AddToStockpile(cost.Type, -cost.Amount);
        }
        AddTruck(depot, cameWithDepot: false);
        return true;
    }

    /// <summary>The truck heading to or loading from a collector, or null.</summary>
    /// <summary>
    /// The truck selling one from a Depot takes: a parked one before one out on a trip, the highest
    /// bay first; null with none.
    /// </summary>
    public Truck? TruckToSell(Building depot)
    {
        List<Truck> trucks = TrucksOf(depot);
        Truck? parked = trucks.FindLast(t => t.State == TruckState.Parked);
        return parked ?? (trucks.Count > 0 ? trucks[^1] : null);
    }

    /// <summary>
    /// Sells a Depot's truck (<see cref="TruckToSell"/>): half a bought truck's cost back, rounded
    /// up (the truck the Depot came with gives nothing), and any load it carries in full. A Depot may
    /// sell every truck. False when it has none.
    /// </summary>
    public bool TrySellTruck(int depotId)
    {
        if (BuildingById(depotId) is not Building depot || TruckToSell(depot) is not Truck truck)
        {
            return false;
        }
        foreach (ResourceAmount back in RefundOf(truck))
        {
            AddToStockpile(back.Type, back.Amount);
        }
        _trucks.Remove(truck);
        return true;
    }

    /// <summary>What a truck gives back when sold or its Depot destroyed: half a bought truck's cost, rounded up, and its load.</summary>
    public static IEnumerable<ResourceAmount> RefundOf(Truck truck)
    {
        if (!truck.CameWithDepot)
        {
            foreach (ResourceAmount cost in TruckDefs.Cost)
            {
                yield return new ResourceAmount(cost.Type, Mathf.Ceil(cost.Amount * 0.5f));
            }
        }
        if (truck.Cargo is ResourceType cargo && truck.Amount > 0f)
        {
            yield return new ResourceAmount(cargo, truck.Amount);
        }
    }

    public Truck? TruckFor(Building collector)
    {
        return _trucks.Find(t => ReferenceEquals(t.Target, collector));
    }

    /// <summary>Whether a collector worth a trip was found unreachable by every truck that looked.</summary>
    public bool NoTruckCanReach(Building collector)
    {
        bool anyLooked = false;
        foreach (Truck truck in _trucks)
        {
            if (!_unreachable.Contains((collector.Id, truck.Id)))
            {
                return false;
            }
            anyLooked = true;
        }
        return anyLooked;
    }

    /// <summary>Where a Depot's bay is: the center of a slot across the apron at its front.</summary>
    public static Vector3 BayPosition(Building depot, int bay)
    {
        int slots = Math.Max(1, depot.Def.TruckSlots);
        float local = depot.Def.Width * ((bay + 0.5f) / slots - 0.5f);
        float front = -depot.Def.Depth * 0.25f;
        (float x, float z) = depot.Facing switch
        {
            Facing.North => (local, front),
            Facing.South => (-local, -front),
            Facing.East => (-front, local),
            _ => (front, -local)
        };
        return depot.Position + new Vector3(x, 0f, z);
    }

    /// <summary>The way a Depot's front faces, which is how its parked trucks face.</summary>
    private static Vector3 FrontOf(Building depot)
    {
        return depot.Facing switch
        {
            Facing.North => new Vector3(0f, 0f, -1f),
            Facing.South => new Vector3(0f, 0f, 1f),
            Facing.East => new Vector3(1f, 0f, 0f),
            _ => new Vector3(-1f, 0f, 0f)
        };
    }

    private void AddTruck(Building depot, bool cameWithDepot)
    {
        var taken = new HashSet<int>();
        foreach (Truck truck in TrucksOf(depot))
        {
            taken.Add(truck.Bay);
        }
        int bay = 0;
        while (taken.Contains(bay))
        {
            bay++;
        }
        _trucks.Add(new Truck(_nextTruckId++, depot, bay, cameWithDepot, BayPosition(depot, bay))
        {
            Heading = FrontOf(depot)
        });
    }

    /// <summary>What a Depot's trucks refund with it: half each bought truck's cost, rounded up, and every load in full.</summary>
    private IEnumerable<ResourceAmount> TruckRefund(Building depot)
    {
        foreach (Truck truck in TrucksOf(depot))
        {
            foreach (ResourceAmount back in RefundOf(truck))
            {
                yield return back;
            }
        }
    }

    /// <summary>Keeps trucks in step with a building added or removed: a new Depot's first truck, a removed Depot's trucks, a removed target.</summary>
    private void TrucksOnBuildingsChanged(Building building, bool added)
    {
        _unreachable.Clear();
        if (added)
        {
            if (building.Def.TruckSlots > 0)
            {
                AddTruck(building, cameWithDepot: true);
            }
            return;
        }
        _trucks.RemoveAll(t => ReferenceEquals(t.Depot, building));
        foreach (Truck truck in _trucks)
        {
            if (ReferenceEquals(truck.Target, building))
            {
                DropJob(truck);
            }
        }
    }

    private Building? Hq => _buildings.Find(b => b.Def.Storage == StorageRole.Hq);

    private void StepTrucks(double dt)
    {
        foreach (Truck truck in _trucks)
        {
            StepTruck(truck, (float)dt);
        }
    }

    private void StepTruck(Truck truck, float dt)
    {
        switch (truck.State)
        {
            case TruckState.Parked:
            case TruckState.ToDepot:
                if (truck.Depot.Enabled && truck.Depot.Online && TryDispatch(truck))
                {
                    break;
                }
                if (truck.State == TruckState.ToDepot && Drive(truck, dt))
                {
                    truck.State = TruckState.Parked;
                    truck.Position = BayPosition(truck.Depot, truck.Bay);
                    truck.Heading = FrontOf(truck.Depot);
                }
                break;
            case TruckState.ToCollector:
                if (truck.Target is not Building target || !target.Online || target.OutputCount < 1f - RoomTolerance)
                {
                    DropJob(truck);
                    break;
                }
                if (Drive(truck, dt))
                {
                    truck.State = TruckState.Loading;
                    truck.Timer = LoadFor(target, truck).Amount * TruckDefs.LoadSecondsPerUnit;
                }
                break;
            case TruckState.Loading:
                if (truck.Target is { Online: false })
                {
                    // The load is taken at the end, so a collector cut off mid-load sends it home empty.
                    DropJob(truck);
                    break;
                }
                truck.Timer -= dt;
                if (truck.Timer <= 0f)
                {
                    Load(truck);
                }
                break;
            case TruckState.ToHq:
                if (Drive(truck, dt))
                {
                    truck.State = TruckState.Unloading;
                    truck.Timer = truck.Amount * TruckDefs.LoadSecondsPerUnit;
                }
                break;
            case TruckState.Unloading:
                truck.Timer -= dt;
                if (truck.Timer <= 0f)
                {
                    Unload(truck);
                }
                break;
            case TruckState.WaitingForRoom:
                Unload(truck);
                break;
        }
    }

    /// <summary>Gives an idle truck the best collector it can reach; false when there is none.</summary>
    private bool TryDispatch(Truck truck)
    {
        var candidates = new List<(Building Building, float Load)>();
        foreach (Building building in _buildings)
        {
            if (!building.Def.IsCollector || !building.Online || TruckFor(building) is not null || _unreachable.Contains((building.Id, truck.Id)))
            {
                continue;
            }
            float load = LoadFor(building, truck).Amount;
            bool full = building.Def.OutputBuffer > 0f && building.OutputCount >= building.Def.OutputBuffer - RoomTolerance;
            if (load >= TruckDefs.DispatchAt - RoomTolerance || (full && load >= 1f))
            {
                candidates.Add((building, load));
            }
        }
        candidates.Sort((a, b) =>
        {
            int byLoad = b.Load.CompareTo(a.Load);
            return byLoad != 0 ? byLoad : truck.Position.DistanceTo(a.Building.Position).CompareTo(truck.Position.DistanceTo(b.Building.Position));
        });
        foreach ((Building candidate, _) in candidates)
        {
            List<Vector3>? path = FindPathBeside(truck.Position, candidate, truck.Depot);
            if (path is null)
            {
                _unreachable.Add((candidate.Id, truck.Id));
                continue;
            }
            truck.Target = candidate;
            truck.State = TruckState.ToCollector;
            SetPath(truck, path);
            return true;
        }
        return false;
    }

    /// <summary>Abandons the collector a truck was heading to or loading from and goes home (to dispatch again from there).</summary>
    private void DropJob(Truck truck)
    {
        truck.Target = null;
        GoHome(truck);
    }

    private void GoHome(Truck truck)
    {
        Vector3 bay = BayPosition(truck.Depot, truck.Bay);
        (int x, int z) = CellAt(bay.X, bay.Z);
        List<Vector3>? path = FindPathTo(truck.Position, x, z, truck.Depot);
        truck.State = TruckState.ToDepot;
        SetPath(truck, path ?? new List<Vector3>());
    }

    /// <summary>
    /// What a truck would load from a collector: the resource giving the biggest load, in whole units
    /// up to its capacity and to what storage has room for once the loads other trucks are carrying
    /// there are counted (the most-held first on a tie); none (0) when storage can take none of it.
    /// </summary>
    private (ResourceType? Type, float Amount) LoadFor(Building collector, Truck truck)
    {
        ResourceType? best = null;
        float bestLoad = 0f;
        float bestHeld = 0f;
        foreach ((ResourceType type, float held) in collector.Output)
        {
            float load = MathF.Floor(Math.Min(Math.Min(held, TruckDefs.Capacity), RoomForTruck(type, truck)) + RoomTolerance);
            if (load > bestLoad || (load == bestLoad && load > 0f && held > bestHeld))
            {
                best = type;
                bestLoad = load;
                bestHeld = held;
            }
        }
        return (best, bestLoad);
    }

    /// <summary>Storage's room for a resource, less what the other loaded trucks are taking there.</summary>
    private float RoomForTruck(ResourceType type, Truck truck)
    {
        float room = RoomFor(type);
        foreach (Truck other in _trucks)
        {
            if (!ReferenceEquals(other, truck) && other.Cargo == type && other.State is TruckState.ToHq or TruckState.Unloading)
            {
                room -= other.Amount;
            }
        }
        return Math.Max(0f, room);
    }

    private void Load(Truck truck)
    {
        Building? target = truck.Target;
        truck.Target = null;
        (ResourceType? type, float take) = target is null ? (null, 0f) : LoadFor(target, truck);
        if (target is null || type is not ResourceType cargo || take < 1f || Hq is not Building hq)
        {
            GoHome(truck);
            return;
        }
        if (FindPathBeside(truck.Position, hq, truck.Depot) is not List<Vector3> path)
        {
            // Nothing it loads here could reach storage.
            _unreachable.Add((target.Id, truck.Id));
            GoHome(truck);
            return;
        }
        Put(target.Output, cargo, -take);
        truck.Cargo = cargo;
        truck.Amount = take;
        truck.State = TruckState.ToHq;
        SetPath(truck, path);
    }

    /// <summary>
    /// Unloads the whole load, even past the storage limit when room taken away on the way left too
    /// little (as a destroy refund does), so a truck never waits; then the truck is idle (it heads
    /// home and dispatches from the way).
    /// </summary>
    private void Unload(Truck truck)
    {
        if (truck.Cargo is ResourceType cargo && truck.Amount > 0f)
        {
            AddToStockpile(cargo, truck.Amount);
        }
        truck.Cargo = null;
        truck.Amount = 0f;
        GoHome(truck);
    }

    private static void SetPath(Truck truck, List<Vector3> path)
    {
        truck.Path = path;
        truck.PathIndex = 0;
    }

    /// <summary>
    /// Drives along the path for one tick at the truck's speed over the terrain under it; true on
    /// arrival. A path cell that has become impassable (a building placed on it) makes it search
    /// again for the same end; with no way left, it drops what it was doing.
    /// </summary>
    private bool Drive(Truck truck, float dt)
    {
        float left = TruckDefs.Speed * TerrainSpeedAt(truck.Position.X, truck.Position.Z) * dt;
        while (truck.PathIndex < truck.Path.Count)
        {
            Vector3 next = truck.Path[truck.PathIndex];
            (int nx, int nz) = CellAt(next.X, next.Z);
            if (!IsPassable(nx, nz, truck.Depot))
            {
                Repath(truck);
                return false;
            }
            Vector3 toNext = next - truck.Position;
            float distance = toNext.Length();
            if (distance > 1e-4f)
            {
                truck.Heading = toNext / distance;
            }
            if (distance > left)
            {
                truck.Position += toNext / distance * left;
                return false;
            }
            truck.Position = next;
            left -= distance;
            truck.PathIndex++;
        }
        return true;
    }

    private void Repath(Truck truck)
    {
        (int bayX, int bayZ) = CellAt(BayPosition(truck.Depot, truck.Bay).X, BayPosition(truck.Depot, truck.Bay).Z);
        List<Vector3>? path = truck.State switch
        {
            TruckState.ToCollector when truck.Target is Building target => FindPathBeside(truck.Position, target, truck.Depot),
            TruckState.ToHq when Hq is Building hq => FindPathBeside(truck.Position, hq, truck.Depot),
            TruckState.ToDepot => FindPathTo(truck.Position, bayX, bayZ, truck.Depot),
            _ => null
        };
        if (path is not null)
        {
            SetPath(truck, path);
        }
        else if (truck.State == TruckState.ToCollector)
        {
            if (truck.Target is Building target)
            {
                _unreachable.Add((target.Id, truck.Id));
            }
            DropJob(truck);
        }
        // Otherwise (loaded with no way to the HQ, or no way home) it keeps its path and waits, trying again each tick.
    }
}
