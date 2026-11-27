using System.Linq;

namespace Telaelus.Sim.Tests;

public class NetworkTests
{
    // The empty map's HQ is anchored at (56, 2), 6 x 10: its center is (59, 7), coverage 38, link 44.
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void Hubs_LinkWithinTheLargerOfTheirLinkRadii()
    {
        Simulation sim = EmptyMap();
        // A Small Hub 39.5 units from the HQ's center: past its own 22, inside the HQ's 44.
        Building near = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 19, 6, Facing.North);
        // 29.4 units on from that one (past both Small Hubs' 22) and 61 from the HQ (past its 44).
        Building far = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 2, 30, Facing.North);

        Assert.True(near.Online);
        Assert.False(far.Online);

        // A Relay (link 90) at (31, 61), 63 from the HQ's center and 39 from the far hub, links both.
        sim.Debug.PlaceBuilding(BuildingType.Relay, 30, 60, Facing.North);
        Assert.True(far.Online);
    }

    [Fact]
    public void SmallHubsAtFullLinkRange_LeaveNoGapInCoverage()
    {
        Simulation sim = EmptyMap();
        // Hub A's center (19.5, 6.5) is 39.5 from the HQ's; hub B's (19.5, 28.5) is exactly 22 (the
        // Small Hub link radius) from A and 45 from the HQ, so B links through A alone.
        Building a = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 19, 6, Facing.North);
        Building b = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 19, 28, Facing.North);
        // A Silo straddling the midpoint (19.5, 17.5), past the HQ's 38 coverage.
        Building silo = sim.Debug.PlaceBuilding(BuildingType.Silo, 19, 17, Facing.North);

        Assert.True(a.Online);
        Assert.True(b.Online);
        Assert.True(silo.Online);
        sim.SetBuildingEnabled(a.Id, false);
        Assert.False(silo.Online);
    }

    [Fact]
    public void Coverage_ReachesTheNearestPointOfAFootprint()
    {
        Simulation sim = EmptyMap();
        Building hq = SimTestHelpers.Hq(sim);

        // From the HQ's center (59, 7): this Smelter's nearest corner (22, 12) is 37.3 away, inside
        // the 38 coverage, though its center (20.5, 13.5) is 39 away.
        Building corner = sim.Debug.PlaceBuilding(BuildingType.Smelter, 19, 12, Facing.North);
        // This one's nearest point (18, 12) is 41.3 away.
        Building beyond = sim.Debug.PlaceBuilding(BuildingType.Smelter, 15, 12, Facing.North);

        Assert.True(hq.Position.DistanceTo(corner.Position) > 38f);
        Assert.True(corner.Online);
        Assert.False(beyond.Online);
    }

    [Fact]
    public void SwitchingAHubOff_CutsItsBranch_AndDestroyingOneDoesToo()
    {
        Simulation sim = EmptyMap();
        // HQ center (59, 7); hub A's center (20.5, 7.5) is 38.5 from it; hub B's (3.5, 20.5) is 21.4
        // from A and 57.1 from the HQ, so B reaches the HQ only through A. The Smelter is covered
        // by B alone (its nearest point (3, 24) is 3.5 from B and 24 from A).
        Building a = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 20, 7, Facing.North);
        Building b = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 3, 20, Facing.North);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 0, 24, Facing.North);
        Assert.True(b.Online);
        Assert.True(smelter.Online);

        sim.SetBuildingEnabled(a.Id, false);
        Assert.False(b.Online);
        Assert.False(smelter.Online);

        sim.SetBuildingEnabled(a.Id, true);
        Assert.True(smelter.Online);

        sim.TryDestroyBuilding(a.Id);
        Assert.False(b.Online);
        Assert.False(smelter.Online);
    }

    [Fact]
    public void AnExtractor_FillsItsBuffer_ThenWaitsForPickup()
    {
        Simulation sim = EmptyMap();
        ResourceField ore = sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 8);

        // 8 s a unit at base speed; 20 units fill the buffer at 160 s.
        SimTestHelpers.Seconds(sim, 200.0);

        Assert.Equal(20f, mine.Output[ResourceType.IronOre]);
        Assert.Equal(80f, ore.Amount);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Full: waiting for pickup"), sim.BuildingStatusOf(mine));
    }

    [Fact]
    public void CollectingByHand_TakesTheWholeBuffer_InOneHalfSecondHold()
    {
        Simulation sim = EmptyMap();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 12, 8, Facing.North);
        mine.Output[ResourceType.IronOre] = 12f;
        mine.Output[ResourceType.Stone] = 5f;

        SimTestHelpers.Teleport(sim, 11.5f, 11f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(mine, sim.InteractTarget);
        Assert.Null(sim.HarvestTarget);
        // The hint names the first type in table order (Stone) and counts the rest.
        Assert.Equal((5f, 1), sim.InteractHeld());

        // Nothing moves before the hold is done, then everything does.
        SimTestHelpers.Seconds(sim, 0.4);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        SimTestHelpers.Seconds(sim, 0.15);
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(0f, mine.OutputCount);
    }

    [Fact]
    public void CollectingByHand_LeavesWhatStorageHasNoRoomFor()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 55f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 12, 8, Facing.North);
        mine.Output[ResourceType.IronOre] = 8f;
        mine.Output[ResourceType.Stone] = 3f;

        SimTestHelpers.CollectByHand(sim, mine);

        Assert.Equal(60f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(3f, mine.Output[ResourceType.IronOre]);
        Assert.True(sim.InteractBlocked);
    }

    [Fact]
    public void CollectingByHand_StopsWhenStorageIsFull()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 60f);
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 8);
        SimTestHelpers.Seconds(sim, 20.0);

        SimTestHelpers.CollectByHand(sim, mine);

        Assert.True(sim.InteractBlocked);
        Assert.Equal(60f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(2f, mine.OutputCount);
    }

    /// <summary>Switches every Small Hub off or on, taking whatever they cover off or onto the network.</summary>
    private static void SwitchHubs(Simulation sim, bool enabled)
    {
        foreach (int id in sim.Buildings.Where(b => b.Type == BuildingType.SmallHub).Select(b => b.Id).ToArray())
        {
            Assert.True(sim.SetBuildingEnabled(id, enabled));
        }
    }

    [Fact]
    public void AnOfflineProcessor_DoesNothing_AndCannotBeLoadedByHand()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 18, 20, Facing.North);
        SimTestHelpers.Order(sim, smelter);
        Assert.False(smelter.Online);

        SimTestHelpers.Teleport(sim, 21.5f, 21.5f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        SimTestHelpers.Seconds(sim, 30.0);

        Assert.Null(sim.InteractTarget);
        Assert.False(smelter.Crafting);
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.Coal));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Offline: outside the network"), sim.BuildingStatusOf(smelter));
    }

    [Fact]
    public void AProcessorGoingOffline_HoldsItsCycle_ThenFinishesItOnline()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        Building smelter = SimTestHelpers.PlaceOnline(sim, BuildingType.Smelter, 18, 20);
        SimTestHelpers.Order(sim, smelter);
        SimTestHelpers.Seconds(sim, 8.0);
        Assert.True(smelter.Crafting);
        float progress = smelter.Progress;
        Assert.True(progress > 0f);

        SwitchHubs(sim, false);
        Assert.False(smelter.Online);
        SimTestHelpers.Seconds(sim, 30.0);

        Assert.True(smelter.Crafting);
        Assert.Equal(progress, smelter.Progress, 3);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));

        SwitchHubs(sim, true);
        SimTestHelpers.Seconds(sim, 16.0);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void CancellingAnOfflineCycle_ReturnsItsInputsToStorage()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        Building smelter = SimTestHelpers.PlaceOnline(sim, BuildingType.Smelter, 18, 20);
        SimTestHelpers.Order(sim, smelter);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(smelter.Crafting);
        Assert.Equal(9f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        SwitchHubs(sim, false);

        Assert.False(smelter.Online);
        Assert.True(sim.CancelCraft(smelter.Id, 0));

        Assert.False(smelter.Crafting);
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Destroying_ReturnsTheBuffers_PastTheLimit()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 60f);
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 8);
        SimTestHelpers.Seconds(sim, 40.5);

        sim.TryDestroyBuilding(mine.Id);

        Assert.Equal(65f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void StorageBuildings_CountOnlyOnline()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 20, 20, Facing.North);
        Assert.Equal(0f, sim.StorageLimit(ResourceType.CrudeOil));

        SimTestHelpers.Connect(sim, 21f, 21f);

        Assert.Equal(300f, sim.StorageLimit(ResourceType.CrudeOil));
    }

    [Fact]
    public void WouldBeOnline_TellsThePlacementGhostWhetherASpotIsConnected()
    {
        Simulation sim = EmptyMap();

        Assert.True(sim.WouldBeOnline(BuildingType.Smelter, 50, 12, Facing.North));
        Assert.False(sim.WouldBeOnline(BuildingType.Smelter, 5, 30, Facing.North));
        Assert.True(sim.WouldBeOnline(BuildingType.SmallHub, 30, 6, Facing.North));
        Assert.False(sim.WouldBeOnline(BuildingType.SmallHub, 10, 30, Facing.North));
    }
}
