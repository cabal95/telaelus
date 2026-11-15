using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>What the player's interaction with a building does.</summary>
public enum InteractAction
{
    /// <summary>Moves units from the building's output buffer into storage.</summary>
    Collect,

    /// <summary>Moves recipe inputs from storage into an offline processor's input buffer.</summary>
    Load
}

/// <summary>
/// The HQ network and hand interaction with buildings. Hubs (the HQ, Small Hubs, Relays) link
/// when the distance between their centers is at most the larger of their link radii; the hubs
/// linked to the HQ, directly or through others, are online, and so is every building whose
/// footprint comes within an online hub's coverage radius of its center. Switched-off hubs
/// neither link nor cover. The network is recomputed when a building is placed, destroyed, or
/// switched, never per tick. Holding the harvest intent beside a building collects its output
/// buffer into storage, or loads an offline processor's inputs from storage, one unit at a time
/// at ten times hand-harvest speed.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Seconds per unit moved by hand for a resource with no gather time.</summary>
    public const float HandMoveSeconds = 0.3f;

    /// <summary>How much faster moving units by hand is than gathering them.</summary>
    public const float HandMoveSpeedup = 10f;

    /// <summary>The building the player would collect from or load, or null.</summary>
    public Building? InteractTarget { get; private set; }

    public InteractAction InteractAction { get; private set; }

    /// <summary>Progress toward moving the next unit, 0 to 1.</summary>
    public float InteractProgress { get; private set; }

    /// <summary>Whether collecting is stalled because storage has no room for the next unit.</summary>
    public bool InteractBlocked => InteractTarget is Building building && InteractAction == InteractAction.Collect
        && NextCollect(building) is (ResourceType type, float amount) && !HasRoom(type, amount);

    /// <summary>The resource the interaction moves next, or null.</summary>
    public ResourceType? InteractResource => InteractTarget is Building building
        ? (InteractAction == InteractAction.Collect ? NextCollect(building) : NextLoad(building))?.Type
        : null;

    /// <summary>
    /// For the hint: when collecting, the units of the next resource left in the output buffer;
    /// when loading, the units of it in the input buffer and the most it holds.
    /// </summary>
    public (float Held, float? Capacity) InteractAmounts()
    {
        if (InteractTarget is not Building building || InteractResource is not ResourceType type)
        {
            return (0f, null);
        }
        if (InteractAction == InteractAction.Collect)
        {
            return (Held(building.Output, type), null);
        }
        if (LoadsFuel(building))
        {
            float slot = 0f;
            foreach (float amount in building.FuelSlot.Values)
            {
                slot += amount;
            }
            return (slot, BuildingDef.FuelSlotSize);
        }
        float capacity = 0f;
        foreach (ResourceAmount input in ResolvedInputs(building, building.Recipe!))
        {
            if (input.Type == type)
            {
                capacity = InputCapacity(building, input);
            }
        }
        return (Held(building.Input, type), capacity);
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
        var probe = new Building(-1, type, anchorX, anchorZ, facing, null);
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

    /// <summary>How much of a recipe input an input buffer holds at most.</summary>
    private static float InputCapacity(Building building, ResourceAmount input)
    {
        return input.Amount * building.Def.InputCycles;
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
        foreach ((ResourceType type, float amount) in building.Input)
        {
            AddToStockpile(type, amount);
        }
        building.Output.Clear();
        building.Input.Clear();
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

    /// <summary>The next unit to load into an offline building by hand: a recipe input first, then fuel; null when neither is short and in storage.</summary>
    private (ResourceType Type, float Amount)? NextLoad(Building building)
    {
        return NextInputLoad(building) ?? NextFuelLoad(building);
    }

    /// <summary>Whether the next load goes into the fuel slot rather than the input buffer.</summary>
    private bool LoadsFuel(Building building)
    {
        return NextInputLoad(building) is null && NextFuelLoad(building) is not null;
    }

    /// <summary>The next recipe input to load into an offline processor, or null when none is short and in storage.</summary>
    private (ResourceType Type, float Amount)? NextInputLoad(Building building)
    {
        if (building.Online || building.Recipe is not Recipe recipe || building.Def.InputCycles == 0 || building.OutputCount > RoomTolerance)
        {
            return null;
        }
        foreach (ResourceAmount input in ResolvedInputs(building, recipe))
        {
            float room = InputCapacity(building, input) - Held(building.Input, input.Type);
            _stockpile.TryGetValue(input.Type, out float stock);
            float amount = Math.Min(Math.Min(room, stock), 1f);
            if (amount > RoomTolerance)
            {
                return (input.Type, amount);
            }
        }
        return null;
    }

    /// <summary>What the player would do at a building, or null when there is nothing to collect or load.</summary>
    private InteractAction? ActionAt(Building building)
    {
        if (NextCollect(building) is not null)
        {
            return InteractAction.Collect;
        }
        return NextLoad(building) is not null ? InteractAction.Load : null;
    }

    /// <summary>
    /// Picks what the harvest intent works on: the nearest hand-harvestable field or building with
    /// something to collect or load, within reach; a building wins a tie. A new target drops any
    /// partial progress.
    /// </summary>
    private void UpdateInteraction()
    {
        UpdateHarvestTarget();
        float fieldDistance = HarvestTarget is ResourceField field ? DistanceToField(Player.Position, field) : float.MaxValue;

        Building? best = null;
        InteractAction action = InteractAction.Collect;
        float bestDistance = Math.Min(HarvestRange, fieldDistance);
        foreach (Building building in _buildings)
        {
            float distance = DistanceToReach(Player.Position, building);
            if (distance <= bestDistance && ActionAt(building) is InteractAction found)
            {
                bestDistance = distance;
                best = building;
                action = found;
            }
        }

        if (!ReferenceEquals(best, InteractTarget) || action != InteractAction)
        {
            InteractProgress = 0f;
        }
        InteractTarget = best;
        InteractAction = action;
        if (best is not null)
        {
            HarvestTarget = null;
            HarvestProgress = 0f;
        }
    }

    /// <summary>Works toward moving one unit to or from the target building.</summary>
    private void Interact(double dt)
    {
        if (InteractTarget is not Building building)
        {
            return;
        }
        (ResourceType Type, float Amount)? next = InteractAction == InteractAction.Collect ? NextCollect(building) : NextLoad(building);
        if (next is not (ResourceType type, float amount) || InteractBlocked)
        {
            return;
        }

        InteractProgress += (float)dt / HandSeconds(type);
        if (InteractProgress < 1f)
        {
            return;
        }
        InteractProgress = 0f;
        if (InteractAction == InteractAction.Collect)
        {
            Put(building.Output, type, -amount);
            AddToStockpile(type, amount);
        }
        else
        {
            bool fuel = LoadsFuel(building);
            AddToStockpile(type, -amount);
            Put(fuel ? building.FuelSlot : building.Input, type, amount);
        }
    }
}
