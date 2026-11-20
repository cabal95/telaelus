using System.Linq;

namespace Telaelus.Sim.Tests;

public class NetworkTests
{
    // The empty map's HQ is anchored at (56, 2), 6 x 10: its center is (59, 7), coverage 19, link 22.
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
        // A Small Hub 18.5 units from the HQ's center: past its own 14, inside the HQ's 22.
        Building near = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 40, 6, Facing.North);
        // 15 units on from that one: past both Small Hubs' 14.
        Building far = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 25, 6, Facing.North);

        Assert.True(near.Online);
        Assert.False(far.Online);

        // A Relay (link 30) between them links both.
        sim.Debug.PlaceBuilding(BuildingType.Relay, 32, 14, Facing.North);
        Assert.True(far.Online);
    }

    [Fact]
    public void Coverage_ReachesTheNearestPointOfAFootprint()
    {
        Simulation sim = EmptyMap();
        Building hq = SimTestHelpers.Hq(sim);

        // From the HQ's center (59, 7): this Smelter's nearest corner (41, 12) is 18.7 away, inside
        // the 19 coverage, though its center (39.5, 13.5) is 20.6 away.
        Building corner = sim.Debug.PlaceBuilding(BuildingType.Smelter, 38, 12, Facing.North);
        // This one's nearest point (37, 12) is 22.6 away.
        Building beyond = sim.Debug.PlaceBuilding(BuildingType.Smelter, 34, 12, Facing.North);

        Assert.True(hq.Position.DistanceTo(corner.Position) > 19f);
        Assert.True(corner.Online);
        Assert.False(beyond.Online);
    }

    [Fact]
    public void SwitchingAHubOff_CutsItsBranch_AndDestroyingOneDoesToo()
    {
        Simulation sim = EmptyMap();
        // HQ center (59, 7); hub A's center (40.5, 7.5) is 18.5 from it; hub B's (28.5, 7.5) is 12
        // from A and 30.5 from the HQ, so B reaches the HQ only through A. The Smelter is covered
        // by B alone (its nearest point is 4.7 from B and 14.2 from A).
        Building a = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 40, 7, Facing.North);
        Building b = sim.Debug.PlaceBuilding(BuildingType.SmallHub, 28, 7, Facing.North);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 24, 12, Facing.North);
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
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 10);

        // 8 s a unit at base speed; 20 units fill the buffer at 160 s.
        SimTestHelpers.Seconds(sim, 200.0);

        Assert.Equal(20f, mine.Output[ResourceType.IronOre]);
        Assert.Equal(80f, ore.Amount);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Full: waiting for pickup"), sim.BuildingStatusOf(mine));
    }

    [Fact]
    public void CollectingByHand_MovesOneUnitPerTenthOfTheGatherTime_IntoStorage()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 10);
        SimTestHelpers.Seconds(sim, 40.5);
        Assert.Equal(5f, mine.OutputCount);

        SimTestHelpers.Teleport(sim, 11.5f, 11f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(mine, sim.InteractTarget);
        Assert.Equal(InteractAction.Collect, sim.InteractAction);
        Assert.Null(sim.HarvestTarget);

        // Iron Ore takes 6 s by hand, so 0.6 s a unit: two units in 1.25 s.
        SimTestHelpers.Seconds(sim, 1.25);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void CollectingByHand_StopsWhenStorageIsFull()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 60f);
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 10);
        SimTestHelpers.Seconds(sim, 20.0);

        SimTestHelpers.CollectByHand(sim, mine);

        Assert.True(sim.InteractBlocked);
        Assert.Equal(60f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(2f, mine.OutputCount);
    }

    [Fact]
    public void AnOfflineProcessor_WorksFromItsInputBuffer_IntoItsOutputBuffer()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 20, 20, Facing.North);
        SimTestHelpers.Order(sim, smelter);
        Assert.False(smelter.Online);
        SimTestHelpers.Ticks(sim, 1);

        // Offline, it does not reach into storage.
        Assert.False(smelter.Crafting);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Iron Ore (load by hand)"), sim.BuildingStatusOf(smelter));

        // Loading fills its input buffer to 5 cycles' worth (5 Iron Ore, 0.6 s each), then its fuel
        // slot (5 Coal, 0.45 s each).
        SimTestHelpers.Teleport(sim, 21.5f, 21.5f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(InteractAction.Load, sim.InteractAction);
        SimTestHelpers.Seconds(sim, 6.0);
        sim.Intent = default;
        // Its first cycle started once fuel was in, taking an ore, and loading topped it back up: 6 Iron Ore in all.
        Assert.True(smelter.Crafting);
        Assert.Equal(4f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Coal));

        // It works the 6 loaded units into its output buffer (16 s each) and then waits.
        SimTestHelpers.Seconds(sim, 100.0);
        Assert.Equal(6f, smelter.Output[ResourceType.Iron]);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Iron Ore (load by hand)"), sim.BuildingStatusOf(smelter));
    }

    [Fact]
    public void Destroying_ReturnsTheBuffers_PastTheLimit()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 60f);
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 100f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 10);
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
        Assert.False(sim.WouldBeOnline(BuildingType.Smelter, 20, 20, Facing.North));
        Assert.True(sim.WouldBeOnline(BuildingType.SmallHub, 44, 6, Facing.North));
        Assert.False(sim.WouldBeOnline(BuildingType.SmallHub, 30, 6, Facing.North));
    }
}
