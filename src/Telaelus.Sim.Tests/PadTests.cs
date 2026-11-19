using System.Linq;
using Godot;

namespace Telaelus.Sim.Tests;

/// <summary>Trucks unload on the HQ's pad, one at a time, backing on with the bed to the entrance.</summary>
public class PadTests
{
    // The empty map's HQ (56, 2) faces South: its center is (59, 7), its pad (59, 10.6), and a truck
    // stops at (59, 12.6) before backing on.
    private static (Simulation Sim, Building Depot) Setup(int mines)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, 46, 14, Facing.North);
        for (int i = 0; i < mines; i++)
        {
            Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 50 - 4 * i, 20, Facing.North);
            Assert.True(mine.Online);
            mine.Output[ResourceType.IronOre] = 12f;
        }
        return (sim, depot);
    }

    [Fact]
    public void ATruck_BacksOntoThePad_UnloadsThere_AndDrivesOff()
    {
        (Simulation sim, Building depot) = Setup(1);
        Truck truck = Assert.Single(sim.TrucksOf(depot));
        Vector3 pad = sim.HqPad!.Value;
        Assert.Equal(new Vector3(59f, 0f, 10.6f), pad);

        bool backed = false;
        for (int i = 0; i < 30 * 60 && truck.State != TruckState.Unloading; i++)
        {
            SimTestHelpers.Ticks(sim, 1);
            backed |= truck.State == TruckState.BackingOnPad;
        }

        Assert.True(backed);
        Assert.Equal(TruckState.Unloading, truck.State);
        Assert.True(truck.Position.DistanceTo(pad) < 0.01f);
        // Its bed to the entrance: it faces away from the HQ (South).
        Assert.Equal(new Vector3(0f, 0f, 1f), truck.Heading);
        Assert.Same(truck, sim.PadHolder);

        SimTestHelpers.Seconds(sim, 4.0);
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.NotEqual(TruckState.Unloading, truck.State);
        SimTestHelpers.Seconds(sim, 30.0);
        Assert.Equal(TruckState.Parked, truck.State);
        Assert.Null(sim.PadHolder);
    }

    [Fact]
    public void ASecondTruck_WaitsInFrontOfThePad_UntilTheFirstLeaves()
    {
        (Simulation sim, Building depot) = Setup(2);
        sim.Debug.SeedStockpile(ResourceType.Iron, 15f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 10f);
        Assert.True(sim.TryBuyTruck(depot.Id));

        bool waited = false;
        for (int i = 0; i < 30 * 120; i++)
        {
            SimTestHelpers.Ticks(sim, 1);
            Assert.True(sim.Trucks.Count(t => t.State is TruckState.BackingOnPad or TruckState.LeavingPad
                || (t.State == TruckState.Unloading && t.Position.DistanceTo(sim.HqPad!.Value) < 0.5f)) <= 1);
            if (sim.Trucks.Any(t => t.State == TruckState.WaitingForPad) && sim.PadHolder is not null)
            {
                waited = true;
                Truck waiting = sim.PadQueue[0];
                // In line in front of the pad, facing the HQ.
                Assert.True(waiting.Position.Z > sim.HqPad!.Value.Z + 1f);
            }
        }

        Assert.True(waited);
        Assert.Equal(24f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.All(sim.Trucks, t => Assert.Equal(TruckState.Parked, t.State));
    }

    [Fact]
    public void WithThePadsFrontBlocked_ATruckUnloadsBesideTheHq()
    {
        (Simulation sim, Building depot) = Setup(1);
        // A Small Hub on the spot in front of the pad.
        sim.Debug.PlaceBuilding(BuildingType.SmallHub, 59, 12, Facing.North);
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        for (int i = 0; i < 30 * 60 && truck.State != TruckState.Unloading; i++)
        {
            SimTestHelpers.Ticks(sim, 1);
        }

        Assert.Equal(TruckState.Unloading, truck.State);
        Assert.True(truck.Position.DistanceTo(sim.HqPad!.Value) > 1f);
        SimTestHelpers.Seconds(sim, 4.0);
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }
}
