namespace Game.Sim.Tests;

public class ProductionTests
{
    // 95 ticks is a little over 3 seconds, so cycle boundaries never sit on the
    // exact tick being checked, whichever way floating-point rounding falls.
    private const int JustOverThreeSeconds = 95;

    [Fact]
    public void Smelter_TurnsOneOreIntoOneMetalEachSecond()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        SimTestHelpers.Ticks(sim, JustOverThreeSeconds);

        // Three cycles finished and a fourth started (its Ore already consumed).
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(6f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Refinery_TurnsCrudeOilIntoRocketFuelEveryTwoSeconds()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 5f);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        SimTestHelpers.Ticks(sim, JustOverThreeSeconds);

        // One cycle finished and a second started.
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.RocketFuel));
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.CrudeOil));
    }

    [Fact]
    public void Processor_WaitsForItsInputs()
    {
        // Iron is not a Refinery input, so the Refinery has nothing to work with.
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.Iron, 5f);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        SimTestHelpers.Ticks(sim, JustOverThreeSeconds);

        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.RocketFuel));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void Recipes_MatchTheirDefinitions()
    {
        Recipe? smelter = Recipes.For(BuildingType.Smelter);
        Recipe? refinery = Recipes.For(BuildingType.Refinery);

        Assert.NotNull(smelter);
        Assert.Equal(1f, smelter.CycleTime);
        Assert.NotNull(refinery);
        Assert.Equal(2f, refinery.CycleTime);
        Assert.Null(Recipes.For(BuildingType.Mine));
        Assert.Null(Recipes.For(BuildingType.LumberCamp));
    }
}
