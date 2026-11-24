using System.Linq;

namespace Telaelus.Sim.Tests;

/// <summary>Collectors work only on the HQ's network; what they hold stays collectable by hand.</summary>
public class CollectorNetworkTests
{
    // The empty map's HQ center is (59, 7), its coverage 19: the west side of the map is far outside it.
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    /// <summary>A Lumber Camp beside a Wood field at map cell (2, 10), about 50 units from the HQ.</summary>
    private static Building FarCamp(Simulation sim)
    {
        sim.Debug.PlaceField(ResourceType.Wood, 2, 10);
        return sim.Debug.PlaceBuilding(BuildingType.LumberCamp, 13, 41, Facing.North);
    }

    private static void SwitchHubs(Simulation sim, bool on)
    {
        foreach (Building hub in SimTestHelpers.ConnectedHubs(sim))
        {
            sim.SetBuildingEnabled(hub.Id, on);
        }
    }

    [Fact]
    public void OfflineExtractors_MakeNothing_AndSaySo()
    {
        Simulation sim = EmptyMap();
        Building camp = FarCamp(sim);
        sim.Debug.PlaceField(ResourceType.Stone, 2, 2);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 13, 9, Facing.North);
        sim.Debug.PlaceField(ResourceType.CrudeOil, 6, 12);
        Building well = sim.Debug.PlaceBuilding(BuildingType.OilWell, 29, 49, Facing.North);

        SimTestHelpers.Seconds(sim, 60.0);

        foreach (Building building in new[] { camp, mine, well })
        {
            Assert.False(building.Online);
            Assert.Equal(0f, building.OutputCount);
            Assert.Equal(new BuildingStatus(BuildingState.Idle, "Offline: outside the network"), sim.BuildingStatusOf(building));
        }
    }

    [Fact]
    public void AHubBringsACollectorOnline_AndGoingOfflineDropsTheUnitInHand()
    {
        Simulation sim = EmptyMap();
        Building camp = FarCamp(sim);
        SimTestHelpers.Connect(sim, camp.Position.X, camp.Position.Z);
        Assert.True(camp.Online);

        SimTestHelpers.Seconds(sim, 2.0);
        Assert.True(camp.Progress > 0f);
        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(camp));

        // Switching off the chain of hubs cuts it off.
        SwitchHubs(sim, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.False(camp.Online);
        Assert.Equal(0f, camp.Progress);

        SwitchHubs(sim, true);
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.True(camp.OutputCount >= 1f);
    }

    [Fact]
    public void AnOfflineCollectorsBuffer_CanStillBeCollectedByHand()
    {
        Simulation sim = EmptyMap();
        Building camp = FarCamp(sim);
        camp.Output[ResourceType.Wood] = 3f;

        SimTestHelpers.CollectByHand(sim, camp);

        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(0f, camp.OutputCount);
    }

    [Fact]
    public void Trucks_ServeOnlyOnlineCollectors_AndTurnHomeWhenOneGoesOffline()
    {
        Simulation sim = EmptyMap();
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, 46, 14, Facing.North);
        Building camp = FarCamp(sim);
        camp.Output[ResourceType.Wood] = 15f;
        Truck truck = Assert.Single(sim.TrucksOf(depot));

        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(TruckState.Parked, truck.State);

        SimTestHelpers.Connect(sim, camp.Position.X, camp.Position.Z);
        SimTestHelpers.Ticks(sim, 2);
        Assert.Equal(TruckState.ToCollector, truck.State);
        Assert.Same(camp, truck.Target);
        SimTestHelpers.Seconds(sim, 3.0);
        Assert.Equal(TruckState.ToCollector, truck.State);

        SwitchHubs(sim, false);
        SimTestHelpers.Ticks(sim, 2);

        Assert.Equal(TruckState.ToDepot, truck.State);
        Assert.Null(truck.Target);
        Assert.Equal(15f, camp.OutputCount);
    }

    [Fact]
    public void TheHqAndSmallHubs_ReachFurther()
    {
        Assert.Equal(38f, BuildingDefs.For(BuildingType.Hq).CoverageRadius);
        Assert.Equal(44f, BuildingDefs.For(BuildingType.Hq).LinkRadius);
        Assert.Equal(11f, BuildingDefs.For(BuildingType.SmallHub).CoverageRadius);
        Assert.Equal(22f, BuildingDefs.For(BuildingType.SmallHub).LinkRadius);
    }
}
