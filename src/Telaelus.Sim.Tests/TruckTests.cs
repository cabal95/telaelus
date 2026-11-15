using System.Linq;

namespace Telaelus.Sim.Tests;

public class TruckTests
{
    // The empty map's HQ stands at fine x 56..61, z 2..11 and covers this Depot spot; the Mines
    // placed further out are off its network.
    private const int DepotX = 46;
    private const int DepotZ = 14;

    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    private static (Simulation Sim, Building Depot, Building Mine) Haul(float ore)
    {
        Simulation sim = EmptyMap();
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, DepotX, DepotZ, Facing.North);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 20, 40, Facing.North);
        mine.Output[ResourceType.IronOre] = ore;
        return (sim, depot, mine);
    }

    [Fact]
    public void ANewDepot_ComesWithOneTruck_AndBuysUpToFour()
    {
        Simulation sim = EmptyMap();
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, DepotX, DepotZ, Facing.North);
        Truck first = Assert.Single(sim.TrucksOf(depot));
        Assert.Equal(TruckState.Parked, first.State);
        Assert.Equal(Simulation.BayPosition(depot, 0), first.Position);

        Assert.False(sim.TryBuyTruck(depot.Id));
        sim.Debug.SeedStockpile(ResourceType.Iron, 50f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 50f);
        Assert.True(sim.TryBuyTruck(depot.Id));
        Assert.True(sim.TryBuyTruck(depot.Id));
        Assert.True(sim.TryBuyTruck(depot.Id));
        Assert.False(sim.TryBuyTruck(depot.Id));

        Assert.Equal(new[] { 0, 1, 2, 3 }, sim.TrucksOf(depot).Select(t => t.Bay));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(20f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void ATruck_HaulsAnOfflineCollectorsBufferToTheHq_AndParks()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        Assert.False(mine.Online);
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(TruckState.ToCollector, truck.State);
        Assert.Same(mine, truck.Target);
        Assert.Same(truck, sim.TruckFor(mine));

        SimTestHelpers.Seconds(sim, 40.0);
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(0f, mine.OutputCount);
        Assert.Equal(TruckState.Parked, truck.State);
        Assert.Equal(Simulation.BayPosition(depot, 0), truck.Position);
    }

    [Fact]
    public void ATruck_WaitsForTenUnits()
    {
        (Simulation sim, Building depot, Building mine) = Haul(9f);
        Truck truck = Assert.Single(sim.TrucksOf(depot));
        SimTestHelpers.Seconds(sim, 1.0);
        Assert.Equal(TruckState.Parked, truck.State);

        mine.Output[ResourceType.IronOre] = 10f;
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(TruckState.ToCollector, truck.State);
    }

    [Fact]
    public void Dispatch_TakesTheFullest_ThenOneNoOtherTruckIsOn()
    {
        (Simulation sim, Building depot, Building near) = Haul(12f);
        Building fuller = sim.Debug.PlaceBuilding(BuildingType.Mine, 10, 50, Facing.North);
        fuller.Output[ResourceType.Coal] = 15f;
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(fuller, sim.TrucksOf(depot)[0].Target);

        sim.Debug.SeedStockpile(ResourceType.Iron, 15f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 10f);
        Assert.True(sim.TryBuyTruck(depot.Id));
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(near, sim.TrucksOf(depot)[1].Target);
    }

    [Fact]
    public void ATruck_LoadsOneResource_UpToTwenty()
    {
        (Simulation sim, Building depot, Building mine) = Haul(25f);
        mine.Output[ResourceType.Coal] = 4f;
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        SimTestHelpers.Seconds(sim, 40.0);

        // What is left (9 in all) is under a trip's worth, so the truck parks.
        Assert.Equal(20f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Coal));
        Assert.Equal(5f, mine.Output[ResourceType.IronOre]);
        Assert.Equal(4f, mine.Output[ResourceType.Coal]);
        Assert.Equal(TruckState.Parked, truck.State);
    }

    [Fact]
    public void ATruck_WaitsAtTheHqForRoom_ThenUnloadsTheRest()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        float limit = sim.StorageLimit(ResourceType.IronOre);
        sim.Debug.SeedStockpile(ResourceType.IronOre, limit - 5f);
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        SimTestHelpers.Seconds(sim, 30.0);
        Assert.Equal(TruckState.WaitingForRoom, truck.State);
        Assert.Equal(ResourceType.IronOre, truck.Cargo);
        Assert.Equal(7f, truck.Amount);
        Assert.Equal(limit, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        sim.Debug.SeedStockpile(ResourceType.IronOre, -10f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Null(truck.Cargo);
        Assert.Equal(TruckState.ToDepot, truck.State);
        Assert.Equal(limit - 3f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void ATruck_DropsItsJob_WhenTheCollectorIsDestroyedOrEmptied()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        Truck truck = Assert.Single(sim.TrucksOf(depot));
        SimTestHelpers.Ticks(sim, 2);
        Assert.Equal(TruckState.ToCollector, truck.State);

        Assert.True(sim.TryDestroyBuilding(mine.Id));
        Assert.Null(truck.Target);
        Assert.Equal(TruckState.ToDepot, truck.State);

        Building second = sim.Debug.PlaceBuilding(BuildingType.Mine, 20, 40, Facing.North);
        second.Output[ResourceType.IronOre] = 12f;
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(second, truck.Target);
        second.Output.Clear();
        SimTestHelpers.Ticks(sim, 1);
        Assert.Null(truck.Target);
        Assert.Equal(TruckState.ToDepot, truck.State);
    }

    [Fact]
    public void ATruck_FindsAWayRoundABuildingPlacedInItsPath()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        Truck truck = Assert.Single(sim.TrucksOf(depot));
        SimTestHelpers.Ticks(sim, 1);

        // A Warehouse across the path a few cells ahead.
        Godot.Vector3 ahead = truck.Path[6];
        Building wall = sim.Debug.PlaceBuilding(BuildingType.Warehouse, (int)ahead.X - 1, (int)ahead.Z - 1, Facing.North);
        bool enteredWall = false;
        for (int i = 0; i < 30 * 20 && truck.State == TruckState.ToCollector; i++)
        {
            SimTestHelpers.Ticks(sim, 1);
            (int x, int z) = Simulation.CellAt(truck.Position.X, truck.Position.Z);
            enteredWall |= x >= wall.AnchorX && x < wall.AnchorX + 3 && z >= wall.AnchorZ && z < wall.AnchorZ + 3;
        }

        Assert.Equal(TruckState.Loading, truck.State);
        Assert.False(enteredWall);
    }

    [Fact]
    public void ACollectorNoTruckCanReach_IsSkipped()
    {
        Simulation sim = EmptyMap();
        for (int x = 4; x <= 6; x++)
        {
            for (int z = 4; z <= 6; z++)
            {
                if (x != 5 || z != 5)
                {
                    sim.Debug.SetTerrain(x, z, TerrainType.ShallowWater);
                }
            }
        }
        Building island = sim.Debug.PlaceBuilding(BuildingType.Mine, 21, 21, Facing.North);
        island.Output[ResourceType.IronOre] = 15f;
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, DepotX, DepotZ, Facing.North);
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        SimTestHelpers.Ticks(sim, 2);

        Assert.Equal(TruckState.Parked, truck.State);
        Assert.True(sim.NoTruckCanReach(island));
    }

    [Fact]
    public void ASwitchedOffDepot_FinishesTheTrip_ThenParks()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        Truck truck = Assert.Single(sim.TrucksOf(depot));
        SimTestHelpers.Ticks(sim, 1);
        sim.SetBuildingEnabled(depot.Id, false);
        Building other = sim.Debug.PlaceBuilding(BuildingType.Mine, 30, 50, Facing.North);
        other.Output[ResourceType.Stone] = 20f;

        SimTestHelpers.Seconds(sim, 40.0);

        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(20f, other.Output[ResourceType.Stone]);
        Assert.Equal(TruckState.Parked, truck.State);
    }

    [Fact]
    public void AnOfflineDepotsTrucks_StayParked()
    {
        Simulation sim = EmptyMap();
        Building far = sim.Debug.PlaceBuilding(BuildingType.Depot, 20, 20, Facing.North);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 20, 40, Facing.North);
        mine.Output[ResourceType.IronOre] = 12f;
        Truck idle = Assert.Single(sim.TrucksOf(far));
        SimTestHelpers.Seconds(sim, 2.0);

        Assert.False(far.Online);
        Assert.Equal(TruckState.Parked, idle.State);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Offline: trucks stay parked"), sim.BuildingStatusOf(far));
    }

    [Fact]
    public void ADestroyedDepot_RefundsItsBoughtTrucksAndTheirLoads()
    {
        (Simulation sim, Building depot, Building mine) = Haul(12f);
        sim.Debug.SeedStockpile(ResourceType.Iron, 15f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 10f);
        Assert.True(sim.TryBuyTruck(depot.Id));
        Truck first = sim.TrucksOf(depot)[0];
        for (int i = 0; i < 30 * 30 && first.State != TruckState.ToHq; i++)
        {
            SimTestHelpers.Ticks(sim, 1);
        }
        Assert.Equal(12f, first.Amount);

        Assert.True(sim.TryDestroyBuilding(depot.Id));

        Assert.Empty(sim.Trucks);
        // Half the Depot (20 Wood, 10 Stone), half the bought truck (8 Iron, 5 Wood), and the load.
        Assert.Equal(25f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }
}
