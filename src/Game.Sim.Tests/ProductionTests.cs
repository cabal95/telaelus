namespace Game.Sim.Tests;

public class ProductionTests
{
    // A little over 33 seconds, so cycle boundaries never sit on the exact tick being
    // checked, whichever way floating-point rounding falls.
    private const int JustOver33Seconds = 1000;

    [Fact]
    public void Smelter_TurnsOneOreIntoOneMetalEvery16Seconds()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        SimTestHelpers.Ticks(sim, JustOver33Seconds);

        // Burning Coal (40 s a unit), two cycles finished and a third started (its Ore already consumed).
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(7f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Refinery_TurnsCrudeOilIntoKeroseneEvery8Seconds()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 10f);
        sim.Debug.PlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, SimTestHelpers.ClearX + 4, SimTestHelpers.ClearZ, Facing.North);
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, SimTestHelpers.ClearX - 3, SimTestHelpers.ClearZ, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, SimTestHelpers.PondPumpX, SimTestHelpers.PondPumpZ, Facing.North);

        SimTestHelpers.Ticks(sim, JustOver33Seconds);

        // Four cycles finished and a fifth started.
        Assert.Equal(4f, SimTestHelpers.Stock(sim, ResourceType.Kerosene));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.CrudeOil));
    }

    [Fact]
    public void Processor_WaitsForItsInputs()
    {
        // Iron is not a Refinery input, so the Refinery has nothing to work with.
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.Iron, 5f);
        sim.Debug.PlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        SimTestHelpers.Ticks(sim, JustOver33Seconds);

        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Kerosene));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void Recipes_RunAtHalfThePaceOfOneBaseExtractor()
    {
        Recipe smelter = Recipes.For(BuildingType.Smelter)[0];
        Recipe refinery = Recipes.For(BuildingType.Refinery)[0];

        // A base Mine yields Iron Ore every 8 s and a base Oil Well Crude Oil every 4 s.
        Assert.Equal(16f, smelter.CycleTime);
        Assert.Equal(8f, refinery.CycleTime);
        Assert.Empty(Recipes.For(BuildingType.Mine));
        Assert.Empty(Recipes.For(BuildingType.LumberCamp));
    }
}
