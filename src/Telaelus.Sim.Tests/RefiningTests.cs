using System.Linq;

namespace Telaelus.Sim.Tests;

public class RefiningTests
{
    /// <summary>
    /// The empty map (its HQ at fine x 56..61, z 2..11) with six Burner Generators on Coal (210
    /// Power), two Water Pumps on a pond at map cell (13, 4) (8 Water), and a Tank Farm for fluids,
    /// all online; fine cells x 47..52, z 4..9 are left for the building under test.
    /// </summary>
    private static Simulation Plant()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 180f);
        SimTestHelpers.Burners(sim, 6);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 28, 0, Facing.North);
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 12, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 56, 16, Facing.North);
        return sim;
    }

    [Theory]
    [InlineData(BuildingType.Smelter, "Iron,Copper")]
    [InlineData(BuildingType.Refinery, "Kerosene")]
    [InlineData(BuildingType.Electrolyzer, "Hydrogen,Liquid Oxygen")]
    [InlineData(BuildingType.FuelPlant, "Rocket Fuel")]
    [InlineData(BuildingType.ChemicalPlant, "Plastic,Rubber,Carbon Fiber,Sulfuric Acid")]
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
        // The Steel Mill's Steel is recipe 2.
        Building mill = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North, 2);
        SimTestHelpers.Order(sim, mill, 2);
        Assert.True(mill.Online);

        // Coal is 60 less what the generators burn; Auto takes it, there being more of it.
        Recipe steel = mill.Recipe!;
        Assert.Equal(ResourceType.Coal, sim.ChosenInput(mill, steel, 1));
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(ResourceType.Coal, mill.CycleInputs[1].Type);

        // Set to Charcoal mid-cycle, the next cycle takes Charcoal; with none left it waits for it.
        Assert.True(sim.SetInputChoice(mill.Id, 2, 1, ResourceType.Charcoal));
        Assert.False(sim.SetInputChoice(mill.Id, 2, 1, ResourceType.Wood));
        Assert.False(sim.SetInputChoice(mill.Id, 2, 0, ResourceType.Charcoal));
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
        Assert.True(sim.SetInputChoice(mill.Id, 2, 1, null));
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(mill.Crafting);
        Assert.Equal(ResourceType.Coal, mill.CycleInputs[1].Type);
        Assert.Equal("Coal or Charcoal", steel.InputName(1));
    }

    [Fact]
    public void TheNextOrdersRecipe_WaitsForTheRunningCycle()
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
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North, 3);
        SimTestHelpers.Order(sim, furnace, 4, 1);
        SimTestHelpers.Order(sim, furnace, 5, 1);
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
        // Two Electric Smelters on Aluminum (recipe 3), 60 Power each.
        SimTestHelpers.Order(sim, sim.Debug.PlaceBuilding(BuildingType.Smelter, 44, 12, Facing.East, 3), 3);
        Building second = sim.Debug.PlaceBuilding(BuildingType.Smelter, 44, 16, Facing.East, 3);
        SimTestHelpers.Order(sim, second, 3);
        Assert.True(second.Online);
        SimTestHelpers.Ticks(sim, 1);

        // 120 against 200 is fine; a third would be 180, and the Burner Generators' 200 still holds.
        Assert.Equal(120f, sim.Capacity(CapacityType.Power).Demand);
        SimTestHelpers.Seconds(sim, 20.2);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Aluminum));
    }
}
