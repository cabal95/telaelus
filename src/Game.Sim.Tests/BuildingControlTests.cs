namespace Game.Sim.Tests;

public class BuildingControlTests
{
    // A Mine anchored here works Ore (10, 9) on the test map (see ExtractorTests).
    private const int NearOreX = 44;
    private const int NearOreZ = 34;

    [Fact]
    public void SwitchedOffExtractor_StopsAndStartsItsUnitOver()
    {
        var sim = new Simulation();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North);

        SimTestHelpers.Seconds(sim, 6.0);
        Assert.True(sim.SetBuildingEnabled(mine.Id, false));
        Assert.Equal(0f, mine.Progress);
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(0f, mine.Progress);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        // The 6 s done before switching off are lost: a whole 8 s unit is needed again.
        sim.SetBuildingEnabled(mine.Id, true);
        SimTestHelpers.Seconds(sim, 7.9);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void SwitchedOffProcessor_ReturnsItsCycleInputs()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        sim.SetBuildingEnabled(smelter.Id, false);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.False(smelter.Crafting);
        Assert.Equal(0f, smelter.Progress);
        SimTestHelpers.Seconds(sim, 20.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));

        // Switched back on, it takes the Ore again and needs a whole 16 s cycle.
        sim.SetBuildingEnabled(smelter.Id, true);
        SimTestHelpers.Seconds(sim, 15.8);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        SimTestHelpers.Seconds(sim, 0.4);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void Status_ReportsWorkingIdleAndOff()
    {
        var sim = new Simulation();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North);
        Building idleMine = sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        Building refinery = sim.Debug.PlaceBuilding(BuildingType.Refinery, 20, 28, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(mine));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "No fields in range"), sim.BuildingStatusOf(idleMine));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Crude Oil"), sim.BuildingStatusOf(refinery));

        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 1f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(BuildingState.Working, sim.BuildingStatusOf(refinery).State);

        sim.SetBuildingEnabled(mine.Id, false);
        Assert.Equal(new BuildingStatus(BuildingState.Off, "Switched off"), sim.BuildingStatusOf(mine));
    }

    [Fact]
    public void Destroy_RefundsHalfTheCostRoundedUp_AndFreesTheCells()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        // The Mine costs 25 Wood and 15 Stone.
        Assert.True(sim.TryDestroyBuilding(mine.Id));

        Assert.Equal(13f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Empty(sim.Buildings);
        Assert.Null(sim.BuildingAt(SimTestHelpers.ClearX, SimTestHelpers.ClearZ));
        Assert.True(sim.CanPlace(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
        Assert.False(sim.TryDestroyBuilding(mine.Id));
    }

    [Fact]
    public void Destroy_MidCycle_ReturnsTheCyclesInputs()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        sim.TryDestroyBuilding(smelter.Id);

        // Half of 40 Stone and 15 Wood, rounded up, and the 1 Iron Ore in the furnace.
        Assert.Equal(20f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void BuildingAt_FindsTheBuildingCoveringACell()
    {
        var sim = new Simulation();
        Building refinery = sim.Debug.PlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.East);

        // Facing East, the 2 x 3 Refinery covers 3 x 2 cells.
        Assert.Same(refinery, sim.BuildingAt(SimTestHelpers.ClearX + 2, SimTestHelpers.ClearZ + 1));
        Assert.Null(sim.BuildingAt(SimTestHelpers.ClearX + 3, SimTestHelpers.ClearZ));
    }
}
