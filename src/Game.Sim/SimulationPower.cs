using System;

namespace Game.Sim;

/// <summary>Where the network's power stands.</summary>
public enum PowerState
{
    /// <summary>Demand at or under supply: consumers run at full speed.</summary>
    Normal,

    /// <summary>Demand up to 25% over supply: consumers slow by twice the overload.</summary>
    Brownout,

    /// <summary>Demand more than 25% over supply, or demand with no supply: every consumer stops.</summary>
    Blackout
}

/// <summary>
/// Power on the HQ network. Each tick, before buildings work: switched-on fuel burners burn (a
/// Burner Generator whenever it is on, online or not), then supply is the output of every online,
/// switched-on generator that runs, and demand the draw of every online, switched-on consumer,
/// working or not. Overload is demand over supply less 1; consumers run at 1 less twice the
/// overload, and past 25% over everything powered stops. Offline consumers get no power. Fuel
/// burns one unit at a time for its burn time, taken from the fuel slot first and then (online)
/// from storage: the chosen fuel, or with Best the longest-burning one at hand.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The most overload a brownout allows before it is a blackout.</summary>
    public const float BrownoutLimit = 0.25f;

    public float PowerSupply { get; private set; }

    public float PowerDemand { get; private set; }

    /// <summary>The speed every online powered consumer runs at this tick (1 normal, less in a brownout, 0 in a blackout).</summary>
    public float PowerSpeed { get; private set; } = 1f;

    public PowerState PowerState { get; private set; }

    /// <summary>Burns fuel and sums supply and demand for this tick.</summary>
    private void StepPower(double dt)
    {
        float supply = 0f;
        float demand = 0f;
        foreach (Building building in _buildings)
        {
            if (!building.Enabled)
            {
                continue;
            }
            if (building.Def.PowerSupply > 0f)
            {
                bool running = !building.Def.BurnsFuel || Burn(building, dt);
                if (running && building.Online)
                {
                    supply += building.Def.PowerSupply;
                }
            }
            if (building.Def.PowerDraw > 0f && building.Online)
            {
                demand += building.Def.PowerDraw;
            }
        }
        PowerSupply = supply;
        PowerDemand = demand;
        float overload = supply > 0f ? demand / supply - 1f : demand > 0f ? float.PositiveInfinity : 0f;
        PowerState = overload <= 0f ? PowerState.Normal : overload <= BrownoutLimit ? PowerState.Brownout : PowerState.Blackout;
        PowerSpeed = PowerState switch
        {
            PowerState.Normal => 1f,
            PowerState.Brownout => 1f - 2f * overload,
            _ => 0f
        };
    }

    /// <summary>How fast a building works this tick: 1 when unpowered, the network's power speed when online, 0 offline.</summary>
    private float WorkSpeed(Building building)
    {
        if (building.Def.PowerDraw <= 0f)
        {
            return 1f;
        }
        return building.Online ? PowerSpeed : 0f;
    }

    /// <summary>Why a powered building cannot work, or null when it can.</summary>
    private string? PowerBlock(Building building)
    {
        if (building.Def.PowerDraw <= 0f)
        {
            return null;
        }
        if (!building.Online)
        {
            return "No power: offline";
        }
        return PowerState == PowerState.Blackout ? "Blackout" : null;
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
