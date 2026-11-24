using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The HQ network and hand interaction with buildings. Hubs (the HQ, Small Hubs, Relays) link
/// when the distance between their centers is at most the larger of their link radii; the hubs
/// linked to the HQ, directly or through others, are online, and so is every building whose
/// footprint comes within an online hub's coverage radius of its center. Switched-off hubs
/// neither link nor cover. The network is recomputed when a building is placed, destroyed, or
/// switched, never per tick. Holding the harvest intent beside a building collects its output
/// buffer into storage, one unit at a time at ten times hand-harvest speed.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Seconds per unit moved by hand for a resource with no gather time.</summary>
    public const float HandMoveSeconds = 0.3f;

    /// <summary>How much faster moving units by hand is than gathering them.</summary>
    public const float HandMoveSpeedup = 10f;

    /// <summary>The building the player would collect from, or null.</summary>
    public Building? InteractTarget { get; private set; }

    /// <summary>Progress toward moving the next unit, 0 to 1.</summary>
    public float InteractProgress { get; private set; }

    /// <summary>Whether collecting is stalled because storage has no room for the next unit.</summary>
    public bool InteractBlocked => InteractTarget is Building building
        && NextCollect(building) is (ResourceType type, float amount) && !HasRoom(type, amount);

    /// <summary>The resource the interaction moves next, or null.</summary>
    public ResourceType? InteractResource => InteractTarget is Building building ? NextCollect(building)?.Type : null;

    /// <summary>For the hint: the units of the next resource left in the target's output buffer.</summary>
    public float InteractHeld()
    {
        return InteractTarget is Building building && InteractResource is ResourceType type ? Held(building.Output, type) : 0f;
    }

    /// <summary>Distance from a point to the nearest point of a building's footprint (0 inside).</summary>
    public static float DistanceToFootprint(Vector3 point, Building building)
    {
        float dx = Mathf.Max(Mathf.Max(building.AnchorX - point.X, 0f), point.X - (building.AnchorX + building.Width));
        float dz = Mathf.Max(Mathf.Max(building.AnchorZ - point.Z, 0f), point.Z - (building.AnchorZ + building.Depth));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static bool Links(Building a, Building b)
    {
        return a.Position.DistanceTo(b.Position) <= Math.Max(a.Def.LinkRadius, b.Def.LinkRadius);
    }

    private static bool Covers(Building hub, Building building)
    {
        return DistanceToFootprint(hub.Position, building) <= hub.Def.CoverageRadius;
    }

    /// <summary>Recomputes which buildings are online: a flood fill over the switched-on hubs from the HQ, then a coverage pass.</summary>
    private void RecomputeNetwork()
    {
        var online = new List<Building>();
        var queue = new Queue<Building>();
        foreach (Building building in _buildings)
        {
            building.Online = building.Def.Storage == StorageRole.Hq;
            if (building.Online)
            {
                online.Add(building);
                queue.Enqueue(building);
            }
        }
        while (queue.Count > 0)
        {
            Building hub = queue.Dequeue();
            foreach (Building other in _buildings)
            {
                if (!other.Online && other.Enabled && other.Def.IsHub && Links(hub, other))
                {
                    other.Online = true;
                    online.Add(other);
                    queue.Enqueue(other);
                }
            }
        }
        foreach (Building building in _buildings)
        {
            if (!building.Online)
            {
                building.Online = online.Exists(hub => Covers(hub, building));
            }
        }
    }

    /// <summary>The online hubs a hub links to (for the overlay and a selected hub).</summary>
    public List<Building> LinkedHubs(Building hub)
    {
        return _buildings.FindAll(other => !ReferenceEquals(other, hub) && other.Online && other.Enabled
            && other.Def.IsHub && Links(hub, other));
    }

    /// <summary>
    /// Whether a building placed here would be online: a hub that would link to an online hub, or
    /// any building whose footprint an online hub covers.
    /// </summary>
    public bool WouldBeOnline(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        var probe = new Building(-1, type, anchorX, anchorZ, facing, Array.Empty<Recipe>());
        return _buildings.Exists(hub => hub.Online && hub.Enabled && hub.Def.IsHub
            && (Covers(hub, probe) || (probe.Def.IsHub && Links(hub, probe))));
    }

    // ---- Buffers ----------------------------------------------------------

    private static float Held(Dictionary<ResourceType, float> buffer, ResourceType type)
    {
        buffer.TryGetValue(type, out float amount);
        return amount;
    }

    private static void Put(Dictionary<ResourceType, float> buffer, ResourceType type, float amount)
    {
        float left = Held(buffer, type) + amount;
        if (left <= RoomTolerance)
        {
            buffer.Remove(type);
        }
        else
        {
            buffer[type] = left;
        }
    }

    /// <summary>Moves what fits of an online processor's output buffer into storage.</summary>
    private void DrainOutput(Building building)
    {
        foreach (ResourceType type in new List<ResourceType>(building.Output.Keys))
        {
            float move = Math.Min(Held(building.Output, type), MathF.Floor(RoomFor(type) + RoomTolerance));
            if (move > 0f)
            {
                Put(building.Output, type, -move);
                AddToStockpile(type, move);
            }
        }
    }

    /// <summary>Moves every buffered unit of a building into storage, past limits (a return).</summary>
    private void ReturnBuffers(Building building)
    {
        foreach ((ResourceType type, float amount) in building.Output)
        {
            AddToStockpile(type, amount);
        }
        building.Output.Clear();
    }

    // ---- Hand interaction -------------------------------------------------

    private static float HandSeconds(ResourceType type)
    {
        return ResourceDefs.For(type).GatherSeconds is float seconds ? seconds / HandMoveSpeedup : HandMoveSeconds;
    }

    /// <summary>The next unit to collect from a building's output buffer (first in table order), or null.</summary>
    private static (ResourceType Type, float Amount)? NextCollect(Building building)
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            float have = Held(building.Output, type);
            if (have > RoomTolerance)
            {
                return (type, Math.Min(have, 1f));
            }
        }
        return null;
    }

    /// <summary>
    /// Picks what the harvest intent works on: the nearest hand-harvestable field or building with
    /// something to collect, within reach; a building wins a tie. A new target drops any partial
    /// progress.
    /// </summary>
    private void UpdateInteraction()
    {
        UpdateHarvestTarget();
        float fieldDistance = HarvestTarget is ResourceField field ? DistanceToField(Player.Position, field) : float.MaxValue;

        Building? best = null;
        float bestDistance = Math.Min(HarvestRange, fieldDistance);
        foreach (Building building in _buildings)
        {
            float distance = DistanceToReach(Player.Position, building);
            if (distance <= bestDistance && NextCollect(building) is not null)
            {
                bestDistance = distance;
                best = building;
            }
        }

        if (!ReferenceEquals(best, InteractTarget))
        {
            InteractProgress = 0f;
        }
        InteractTarget = best;
        if (best is not null)
        {
            HarvestTarget = null;
            HarvestProgress = 0f;
        }
    }

    /// <summary>Works toward collecting one unit from the target building.</summary>
    private void Interact(double dt)
    {
        if (InteractTarget is not Building building)
        {
            return;
        }
        if (NextCollect(building) is not (ResourceType type, float amount) || InteractBlocked)
        {
            return;
        }

        InteractProgress += (float)dt / HandSeconds(type);
        if (InteractProgress < 1f)
        {
            return;
        }
        InteractProgress = 0f;
        Put(building.Output, type, -amount);
        AddToStockpile(type, amount);
    }
}
