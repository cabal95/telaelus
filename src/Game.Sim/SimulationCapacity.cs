using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Sim;

/// <summary>
/// Capacity on the HQ network (Power, Water, Sand). Each tick, before buildings work: switched-on
/// fuel-burning providers burn (a Burner Generator whenever it is on, online or not); then, for
/// each capacity type in order, supply is what every online, switched-on provider that runs
/// supplies (scaled by its own speed, so a Pumping Station in a power brownout supplies less), and
/// demand what every online, switched-on consumer draws, working or not. Overload is demand over
/// supply less 1; consumers run at 1 less twice the overload, and past 25% over every consumer of
/// that type stops. A consumer of several types runs at the slowest. Offline consumers get
/// nothing. Fuel burns one unit at a time for its burn time, taken from the fuel slot first and
/// then (online) from storage: the chosen fuel, or with Best the longest-burning one at hand.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The most overload a brownout allows before it is a blackout.</summary>
    public const float BrownoutLimit = 0.25f;

    public static readonly CapacityType[] CapacityTypes = Enum.GetValues<CapacityType>();

    private readonly CapacityReading[] _capacity = Enumerable.Repeat(CapacityReading.Idle, CapacityTypes.Length).ToArray();

    /// <summary>A capacity type's supply, demand, speed, and state this tick.</summary>
    public CapacityReading Capacity(CapacityType type)
    {
        return _capacity[(int)type];
    }

    /// <summary>The state a type's demand and supply give (also for a placement preview).</summary>
    public static CapacityState StateFor(float demand, float supply)
    {
        float overload = Overload(demand, supply);
        return overload <= 0f ? CapacityState.Normal : overload <= BrownoutLimit ? CapacityState.Brownout : CapacityState.Blackout;
    }

    private static float Overload(float demand, float supply)
    {
        return supply > 0f ? demand / supply - 1f : demand > 0f ? float.PositiveInfinity : 0f;
    }

    /// <summary>Burns providers' fuel and sums each capacity type's supply and demand for this tick.</summary>
    private void StepCapacity(double dt)
    {
        var unfueled = new HashSet<Building>();
        foreach (Building building in _buildings)
        {
            if (building.Enabled && building.Def.IsProvider && building.Def.BurnsFuel && !Burn(building, dt))
            {
                unfueled.Add(building);
            }
        }
        foreach (CapacityType type in CapacityTypes)
        {
            float supply = 0f;
            float demand = 0f;
            foreach (Building building in _buildings)
            {
                if (!building.Enabled || !building.Online)
                {
                    continue;
                }
                demand += building.Def.Draw(type);
                if (building.Def.Supply(type) > 0f && !unfueled.Contains(building))
                {
                    // Its draws come earlier in the order, so their speeds are this tick's.
                    supply += building.Def.Supply(type) * WorkSpeed(building);
                }
            }
            CapacityState state = StateFor(demand, supply);
            float speed = state switch
            {
                CapacityState.Normal => 1f,
                CapacityState.Brownout => 1f - 2f * Overload(demand, supply),
                _ => 0f
            };
            _capacity[(int)type] = new CapacityReading(supply, demand, speed, state);
        }
    }

    /// <summary>How fast a building works this tick: the slowest speed of the types it draws (1 for none), 0 offline.</summary>
    public float WorkSpeed(Building building)
    {
        float speed = 1f;
        foreach (CapacityType type in building.Def.Draws.Keys)
        {
            speed = Math.Min(speed, building.Online ? Capacity(type).Speed : 0f);
        }
        return speed;
    }

    /// <summary>Why a consumer cannot work ("No Water: offline", "Blackout: Sand"), or null when it can.</summary>
    private string? CapacityBlock(Building building)
    {
        foreach (CapacityType type in CapacityTypes)
        {
            if (building.Def.Draw(type) <= 0f)
            {
                continue;
            }
            if (!building.Online)
            {
                return $"No {type}: offline";
            }
            if (Capacity(type).State == CapacityState.Blackout)
            {
                return $"Blackout: {type}";
            }
        }
        return null;
    }

    // ---- Fuel -------------------------------------------------------------

    /// <summary>The fuel a burner would take next, or null when none is at hand.</summary>
    private ResourceType? NextFuel(Building building)
    {
        foreach (ResourceType fuel in ResourceDefs.Fuels)
        {
            if (building.FuelChoice is ResourceType chosen && chosen != fuel)
            {
                continue;
            }
            _stockpile.TryGetValue(fuel, out float stock);
            if (Held(building.FuelSlot, fuel) >= 1f - RoomTolerance || (building.Online && stock >= 1f - RoomTolerance))
            {
                return fuel;
            }
        }
        return null;
    }

    /// <summary>Whether a burner has a unit burning or one at hand to take.</summary>
    private bool HasFuel(Building building)
    {
        return building.BurnLeft > 0f || NextFuel(building) is not null;
    }

    /// <summary>Takes the next unit of fuel into the fire, from the slot first and then (online) from storage.</summary>
    private bool TakeFuel(Building building)
    {
        if (NextFuel(building) is not ResourceType fuel)
        {
            building.Burning = null;
            return false;
        }
        if (Held(building.FuelSlot, fuel) >= 1f - RoomTolerance)
        {
            Put(building.FuelSlot, fuel, -1f);
        }
        else
        {
            AddToStockpile(fuel, -1f);
        }
        building.Burning = fuel;
        building.BurnLeft = ResourceDefs.For(fuel).BurnSeconds!.Value;
        return true;
    }

    /// <summary>Burns for one tick, taking a new unit when the last has burned out; false when there is nothing to burn.</summary>
    private bool Burn(Building building, double dt)
    {
        if (building.BurnLeft <= 0f && !TakeFuel(building))
        {
            return false;
        }
        building.BurnLeft = Math.Max(0f, building.BurnLeft - (float)dt);
        return true;
    }

    /// <summary>Sets a fuel burner's fuel: one fuel by name, or null for Best. False for a building without a fuel slot or a resource that is not a fuel.</summary>
    public bool SetFuelChoice(int id, ResourceType? fuel)
    {
        Building? building = BuildingById(id);
        if (building is null || !building.Def.BurnsFuel || (fuel is ResourceType type && ResourceDefs.For(type).BurnSeconds is null))
        {
            return false;
        }
        building.FuelChoice = fuel;
        return true;
    }

    /// <summary>The fuel Best would burn next at this building, or null (for the picker's tooltip).</summary>
    public ResourceType? BestFuelAt(Building building)
    {
        ResourceType? choice = building.FuelChoice;
        building.FuelChoice = null;
        ResourceType? best = NextFuel(building);
        building.FuelChoice = choice;
        return best;
    }

    /// <summary>The next unit of fuel to load into an offline burner's slot by hand, or null.</summary>
    private (ResourceType Type, float Amount)? NextFuelLoad(Building building)
    {
        if (!building.Def.BurnsFuel || building.Online)
        {
            return null;
        }
        float held = 0f;
        foreach (float amount in building.FuelSlot.Values)
        {
            held += amount;
        }
        if (held > BuildingDef.FuelSlotSize - 1f + RoomTolerance)
        {
            return null;
        }
        foreach (ResourceType fuel in ResourceDefs.Fuels)
        {
            if (building.FuelChoice is ResourceType chosen && chosen != fuel)
            {
                continue;
            }
            _stockpile.TryGetValue(fuel, out float stock);
            if (stock >= 1f - RoomTolerance)
            {
                return (fuel, 1f);
            }
        }
        return null;
    }
}
