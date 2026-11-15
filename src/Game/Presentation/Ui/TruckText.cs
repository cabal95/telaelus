using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>How a truck's state reads in the Depot panel and its hover tooltip.</summary>
public static class TruckText
{
    /// <summary>"To Mine: Iron Ore", "Loading", "To HQ: 20 Iron Ore", "Unloading", "Waiting: storage full (Iron Ore)", "Heading home", "Parked".</summary>
    public static string State(Truck truck)
    {
        string cargo = truck.Cargo is ResourceType type ? ResourceDefs.For(type).Name : "";
        return truck.State switch
        {
            TruckState.ToCollector when truck.Target is Building target => $"To {target.Def.Name}: {MostHeld(target)}",
            TruckState.ToCollector => "To a collector",
            TruckState.Loading => "Loading",
            TruckState.ToHq => $"To HQ: {Mathf.FloorToInt(truck.Amount)} {cargo}",
            TruckState.Unloading => "Unloading",
            TruckState.WaitingForRoom => $"Waiting: storage full ({cargo})",
            TruckState.ToDepot => "Heading home",
            _ => "Parked"
        };
    }

    /// <summary>A collector's Truck row: "On its way", "Loading", "No truck can reach", or null.</summary>
    public static string? CollectorRow(Simulation sim, Building collector)
    {
        if (!collector.Def.IsExtractor)
        {
            return null;
        }
        if (sim.TruckFor(collector) is Truck truck)
        {
            return truck.State == TruckState.Loading ? "Loading" : "On its way";
        }
        return sim.NoTruckCanReach(collector) ? "No truck can reach" : null;
    }

    private static string MostHeld(Building collector)
    {
        return collector.Output.Count == 0
            ? "nothing"
            : ResourceDefs.For(collector.Output.MaxBy(entry => entry.Value).Key).Name;
    }
}
