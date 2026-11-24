namespace Telaelus.Sim.Tests;

public class BuildingControlTests
{
    // A Mine anchored here works Ore (10, 9) on the test map (see ExtractorTests).
    private const int NearOreX = 44;
    private const int NearOreZ = 34;

    [Fact]
    public void SwitchedOffExtractor_StopsAndStartsItsUnitOver()
    {
        var sim = new Simulation();
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);

        SimTestHelpers.Seconds(sim, 6.0);
        Assert.True(sim.SetBuildingEnabled(mine.Id, false));
        Assert.Equal(0f, mine.Progress);
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(0f, mine.Progress);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));

        // The 6 s done before switching off are lost: a whole 8 s unit is needed again.
        sim.SetBuildingEnabled(mine.Id, true);
        SimTestHelpers.Seconds(sim, 7.9);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
    }

    [Fact]
    public void SwitchedOffProcessor_ReturnsItsCycleInputs()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        SimTestHelpers.Order(sim, smelter);

        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        sim.SetBuildingEnabled(smelter.Id, false);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        Assert.False(smelter.Crafting);
        Assert.Equal(0f, smelter.Progress);
        SimTestHelpers.Seconds(sim, 20.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));

        // Switched back on, it takes the Ore again and needs a whole cycle: 16 s at Coal's +30%, 12.3 s.
        sim.SetBuildingEnabled(smelter.Id, true);
        SimTestHelpers.Seconds(sim, 12.2);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        SimTestHelpers.Seconds(sim, 0.3);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void Status_ReportsWorkingIdleAndOff()
    {
        var sim = new Simulation();
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);
        Building idleMine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);
        Building refinery = sim.Debug.PlaceBuilding(BuildingType.Refinery, 20, 28, Facing.North);
        SimTestHelpers.Order(sim, refinery);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(mine));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "No fields in range"), sim.BuildingStatusOf(idleMine));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Power"), sim.BuildingStatusOf(refinery));

        // A Burner Generator with Coal powers it, but it has no Water yet.
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 20, 32, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Water"), sim.BuildingStatusOf(refinery));

        // A Water Pump by the pond cools it; then it waits for its Crude Oil.
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, SimTestHelpers.PondPumpX, SimTestHelpers.PondPumpZ, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
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
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);

        // The Mine costs 25 Wood and 15 Stone.
        Assert.True(sim.TryDestroyBuilding(mine.Id));

        Assert.Equal(13f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Empty(SimTestHelpers.Placed(sim));
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
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        SimTestHelpers.Order(sim, smelter);
        SimTestHelpers.Connect(sim, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        sim.TryDestroyBuilding(smelter.Id);

        // Half of 40 Stone and 15 Wood, rounded up, and the 1 Iron Ore in the furnace.
        Assert.Equal(20f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void ExtractorsWorking_ListsTheSwitchedOnExtractorsOnAField()
    {
        var sim = new Simulation();
        ResourceField ore = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        Building first = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);
        Building second = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 40, 34);
        sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        Assert.Empty(sim.ExtractorsWorking(ore));

        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new[] { first, second }, sim.ExtractorsWorking(ore));

        sim.SetBuildingEnabled(second.Id, false);
        Assert.Equal(new[] { first }, sim.ExtractorsWorking(ore));
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
