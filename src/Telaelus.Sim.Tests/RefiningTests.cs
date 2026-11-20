using System.Linq;

namespace Telaelus.Sim.Tests;

public class RefiningTests
{
    /// <summary>
    /// The empty map (its HQ at fine x 56..61, z 2..11) with two Burner Generators on Coal (200
    /// Power), two Water Pumps on a pond at map cell (13, 4) (8 Water), and a Tank Farm for fluids,
    /// all online; fine cells x 47..52, z 4..9 are left for the building under test.
    /// </summary>
    private static Simulation Plant()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 46, 0, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 48, 0, Facing.North);
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 14, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 54, 14, Facing.North);
        return sim;
    }

    [Theory]
    [InlineData(BuildingType.Smelter, "Iron,Copper")]
    [InlineData(BuildingType.Refinery, "Kerosene")]
    [InlineData(BuildingType.SteelMill, "Steel")]
    [InlineData(BuildingType.Electrolyzer, "Hydrogen,Liquid Oxygen")]
    [InlineData(BuildingType.FuelPlant, "Rocket Fuel")]
    [InlineData(BuildingType.ChemicalPlant, "Plastic,Rubber,Carbon Fiber,Sulfuric Acid")]
    [InlineData(BuildingType.AluminumSmelter, "Aluminum")]
    [InlineData(BuildingType.ElectricFurnace, "Silicon,Titanium")]
    public void EachBuilding_RunsItsRecipes(BuildingType type, string names)
    {
        Assert.Equal(names, string.Join(",", Recipes.For(type).Select(r => r.Name)));
        Assert.All(Recipes.For(type), r => Assert.Single(r.Outputs));
    }

    [Fact]
    public void TheSteelMill_TakesCoalOrCharcoal_OnAutoWhicheverIsMore()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Iron, 4f);
        sim.Debug.SeedStockpile(ResourceType.Limestone, 2f);
        sim.Debug.SeedStockpile(ResourceType.Charcoal, 1f);
        Building mill = sim.Debug.PlaceBuilding(BuildingType.SteelMill, 48, 4, Facing.North);
        SimTestHelpers.Order(sim, mill);
        Assert.True(mill.Online);

        // Coal is 60 less what the generators burn; Auto takes it, there being more of it.
        Recipe steel = mill.Recipe!;
        Assert.Equal(ResourceType.Coal, sim.ChosenInput(mill, steel, 1));
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(ResourceType.Coal, mill.CycleInputs[1].Type);

        // Set to Charcoal mid-cycle, the next cycle takes Charcoal; with none left it waits for it.
        Assert.True(sim.SetInputChoice(mill.Id, 0, 1, ResourceType.Charcoal));
        Assert.False(sim.SetInputChoice(mill.Id, 0, 1, ResourceType.Wood));
        Assert.False(sim.SetInputChoice(mill.Id, 0, 0, ResourceType.Charcoal));
        SimTestHelpers.Seconds(sim, 10.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Steel));
        Assert.Equal(ResourceType.Charcoal, mill.CycleInputs[1].Type);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        SimTestHelpers.Seconds(sim, 20.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Steel));
        sim.Debug.SeedStockpile(ResourceType.Iron, 2f);
        sim.Debug.SeedStockpile(ResourceType.Limestone, 1f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Charcoal"), sim.BuildingStatusOf(mill));

        // Back on Auto it takes the Coal again; with neither at hand it would wait for either.
        Assert.True(sim.SetInputChoice(mill.Id, 0, 1, null));
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(mill.Crafting);
        Assert.Equal(ResourceType.Coal, mill.CycleInputs[1].Type);
        Assert.Equal("Coal or Charcoal", steel.InputName(1));
    }

    [Fact]
    public void TheNextOrdersRecipe_WaitsForTheRunningCycle_AndUnusedInputsGoBack()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 2f);
        sim.Debug.SeedStockpile(ResourceType.CopperOre, 2f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        SimTestHelpers.Order(sim, smelter, 0, 1);
        SimTestHelpers.Order(sim, smelter, 1, 1);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(smelter.Crafting);
        Assert.Equal(0, smelter.RecipeIndex);
        Assert.False(sim.SetRecipe(smelter.Id, 1));
        Assert.False(sim.QueueCraft(smelter.Id, 2, 1));

        // The Iron order finishes, then it makes Copper and idles.
        SimTestHelpers.Seconds(sim, 16.1);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(1, smelter.RecipeIndex);
        SimTestHelpers.Seconds(sim, 16.1);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Copper));
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Queue empty"), sim.BuildingStatusOf(smelter));

        // Iron Ore loaded for the old recipe goes back to storage.
        smelter.Input[ResourceType.IronOre] = 3f;
        SimTestHelpers.Ticks(sim, 1);
        Assert.False(smelter.Input.ContainsKey(ResourceType.IronOre));
        Assert.Equal(4f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void TheElectrolyzer_MakesEitherGas_AndTheFuelPlantRocketFuel()
    {
        Simulation sim = Plant();
        Building electrolyzer = sim.Debug.PlaceBuilding(BuildingType.Electrolyzer, 48, 4, Facing.North);
        Assert.True(electrolyzer.Online);

        // Six seconds a unit, from Water and Power alone.
        SimTestHelpers.Seconds(sim, 12.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Hydrogen));
        Assert.True(sim.SetRecipe(electrolyzer.Id, 1));
        SimTestHelpers.Seconds(sim, 12.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.LiquidOxygen));
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Hydrogen));

        sim.Debug.RemoveBuilding(48, 4);
        sim.Debug.SeedStockpile(ResourceType.Kerosene, 2f);
        SimTestHelpers.Order(sim, sim.Debug.PlaceBuilding(BuildingType.FuelPlant, 48, 4, Facing.North));
        SimTestHelpers.Seconds(sim, 16.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.RocketFuel));
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Hydrogen));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Kerosene));
    }

    [Fact]
    public void TheChemicalPlant_AndTheFurnaces_RunTheirRecipes()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 4f);
        Building chemical = sim.Debug.PlaceBuilding(BuildingType.ChemicalPlant, 47, 4, Facing.North);
        SimTestHelpers.Order(sim, chemical, 0, 2);
        SimTestHelpers.Seconds(sim, 20.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Plastic));
        SimTestHelpers.Order(sim, chemical, 2, 1);
        SimTestHelpers.Seconds(sim, 15.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.CarbonFiber));

        sim.Debug.RemoveBuilding(47, 4);
        sim.Debug.SeedStockpile(ResourceType.Quartz, 1f);
        sim.Debug.SeedStockpile(ResourceType.TitaniumOre, 2f);
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.ElectricFurnace, 48, 4, Facing.North);
        SimTestHelpers.Order(sim, furnace, 0, 1);
        SimTestHelpers.Order(sim, furnace, 1, 1);
        SimTestHelpers.Seconds(sim, 15.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Silicon));
        SimTestHelpers.Seconds(sim, 20.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Titanium));
    }

    [Fact]
    public void AluminumSmelters_DrawHeavyPower()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Bauxite, 8f);
        // Facing East a 3 x 8 pot line lies 8 along X.
        SimTestHelpers.Order(sim, sim.Debug.PlaceBuilding(BuildingType.AluminumSmelter, 44, 12, Facing.East));
        Building second = sim.Debug.PlaceBuilding(BuildingType.AluminumSmelter, 44, 15, Facing.East);
        SimTestHelpers.Order(sim, second);
        Assert.True(second.Online);
        SimTestHelpers.Ticks(sim, 1);

        // 120 against 200 is fine; a third would be 180, and the Burner Generators' 200 still holds.
        Assert.Equal(120f, sim.Capacity(CapacityType.Power).Demand);
        SimTestHelpers.Seconds(sim, 20.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Aluminum));
    }
}
