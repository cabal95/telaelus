using System.Linq;

namespace Telaelus.Sim.Tests;

public class CraftingQueueTests
{
    /// <summary>
    /// The empty map (its HQ at fine x 56..61, z 2..11) with six Burner Generators on Coal (210
    /// Power) and two Water Pumps on a pond (8 Water), all online, with Wood and Ore to spare.
    /// </summary>
    private static Simulation Plant()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 180f);
        SimTestHelpers.Burners(sim, 6);
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 12, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 56, 16, Facing.North);
        return sim;
    }

    private static string Queue(Building building)
    {
        return string.Join(", ", building.Queue.Select(o => $"{building.Recipes[o.RecipeIndex].Name} {o.Remaining}"));
    }

    [Fact]
    public void ACharcoalKiln_TakesOnlyWhatItsOrderNeeds_ThenIdles()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Wood, 30f);
        Building kiln = sim.Debug.PlaceBuilding(BuildingType.Kiln, 46, 14, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Queue empty"), sim.BuildingStatusOf(kiln));
        Assert.Equal(30f, SimTestHelpers.Stock(sim, ResourceType.Wood));

        Assert.True(sim.QueueCraft(kiln.Id, 0, 2));
        SimTestHelpers.Seconds(sim, 60.0);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.Equal(24f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Empty(kiln.Queue);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Queue empty"), sim.BuildingStatusOf(kiln));
    }

    [Fact]
    public void ALaterOrderOfTheSameRecipe_GoesAfterTheOthers_ButOneInARowGrowsTheLast()
    {
        Simulation sim = Plant();
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);

        Assert.True(sim.QueueCraft(smelter.Id, 0, 5));
        Assert.True(sim.QueueCraft(smelter.Id, 1, 5));
        Assert.True(sim.QueueCraft(smelter.Id, 0, 5));
        Assert.Equal("Iron 5, Copper 5, Iron 5", Queue(smelter));

        Assert.True(sim.QueueCraft(smelter.Id, 0, 3));
        Assert.Equal("Iron 5, Copper 5, Iron 8", Queue(smelter));
    }

    [Fact]
    public void TheQueue_HoldsFourOrders()
    {
        Simulation sim = Plant();
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        for (int i = 0; i < Simulation.MaxQueue; i++)
        {
            Assert.True(sim.QueueCraft(smelter.Id, i % 2, 1));
        }

        Assert.False(sim.QueueCraft(smelter.Id, 0, 1));
        // The last is Copper: another Copper still grows it.
        Assert.True(sim.QueueCraft(smelter.Id, 1, 1));
        Assert.Equal("Iron 1, Copper 1, Iron 1, Copper 2", Queue(smelter));
        Assert.False(sim.QueueCraft(smelter.Id, 0, 0));
    }

    [Fact]
    public void OrdersRunInTurn_AndTheFirstWaitsForItsInputs()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.CopperOre, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        Assert.True(sim.QueueCraft(smelter.Id, 0, 1));
        Assert.True(sim.QueueCraft(smelter.Id, 1, 1));
        SimTestHelpers.Ticks(sim, 1);

        // Copper Ore is at hand, but the Iron order is first.
        Assert.False(smelter.Crafting);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Iron Ore"), sim.BuildingStatusOf(smelter));

        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        SimTestHelpers.Seconds(sim, 32.5);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Copper));
        Assert.Empty(smelter.Queue);
    }

    [Fact]
    public void ACopperWireOrder_CountsInWholeCycles()
    {
        Assert.Equal(6, Simulation.WholeCycles(Recipes.For(BuildingType.ElectronicsWorks)[0], 5));
        Assert.Equal(4, Simulation.WholeCycles(Recipes.For(BuildingType.ElectronicsWorks)[0], 4));
        Assert.Equal(5, Simulation.WholeCycles(Recipes.For(BuildingType.Smelter)[0], 5));

        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Copper, 10f);
        Building works = sim.Debug.PlaceBuilding(BuildingType.ElectronicsWorks, 48, 4, Facing.North);
        Assert.True(sim.QueueCraft(works.Id, 0, 3));
        Assert.Equal("Copper Wire 4", Queue(works));

        SimTestHelpers.Seconds(sim, 6.2);
        Assert.Equal("Copper Wire 2", Queue(works));
        SimTestHelpers.Seconds(sim, 30.0);
        Assert.Equal(4f, SimTestHelpers.Stock(sim, ResourceType.CopperWire));
        Assert.Equal(8f, SimTestHelpers.Stock(sim, ResourceType.Copper));
    }

    [Fact]
    public void CancellingTheRunningOrder_ReturnsItsCycleInputs()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Wood, 6f);
        Building kiln = sim.Debug.PlaceBuilding(BuildingType.Kiln, 46, 14, Facing.North);
        Assert.True(sim.QueueCraft(kiln.Id, 0, 2));
        SimTestHelpers.Seconds(sim, 5.0);
        Assert.True(kiln.Crafting);
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Wood));

        Assert.False(sim.CancelCraft(kiln.Id, 1));
        Assert.True(sim.CancelCraft(kiln.Id, 0));

        Assert.False(kiln.Crafting);
        Assert.Empty(kiln.Queue);
        Assert.Equal(6f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        SimTestHelpers.Seconds(sim, 30.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
    }

    [Fact]
    public void CancellingALaterOrder_LeavesTheRunningOneAlone()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        Assert.True(sim.QueueCraft(smelter.Id, 0, 1));
        Assert.True(sim.QueueCraft(smelter.Id, 1, 1));
        SimTestHelpers.Ticks(sim, 1);

        Assert.True(sim.CancelCraft(smelter.Id, 1));
        Assert.True(smelter.Crafting);
        Assert.Equal("Iron 1", Queue(smelter));
    }

    [Fact]
    public void AnIdleProcessor_StillDrawsItsCapacity()
    {
        Simulation sim = Plant();
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North, 3);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Queue empty"), sim.BuildingStatusOf(furnace));
        Assert.Equal(60f, sim.Capacity(CapacityType.Power).Demand);
    }

    [Fact]
    public void AnIdleFuelBurner_TakesNoNewFuel_ButItsBurningUnitBurnsOut()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        Assert.True(sim.SetFuelChoice(smelter.Id, ResourceType.Wood));
        Assert.True(sim.QueueCraft(smelter.Id, 0, 1));

        // Wood burns 10 s: the 16 s cycle takes two, and the second has 4 s left when it finishes.
        SimTestHelpers.Seconds(sim, 16.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.True(smelter.BurnLeft > 3f);

        SimTestHelpers.Seconds(sim, 30.0);
        Assert.Equal(0f, smelter.BurnLeft);
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void TheMakersFromNothing_RunWithoutAQueue()
    {
        Simulation sim = Plant();
        Building electrolyzer = sim.Debug.PlaceBuilding(BuildingType.Electrolyzer, 48, 4, Facing.North);
        Assert.False(electrolyzer.Queues);
        Assert.False(sim.QueueCraft(electrolyzer.Id, 0, 1));
        Assert.True(sim.SetRecipe(electrolyzer.Id, 1));
        Assert.False(new Building(-1, BuildingType.BrineWell, 0, 0, Facing.North).Queues);
        // The Glassworks makes from nothing but burns fuel, so it queues.
        Assert.True(new Building(-1, BuildingType.Glassworks, 0, 0, Facing.North).Queues);
    }

    [Fact]
    public void InputChoices_AreKeptPerRecipe()
    {
        Simulation sim = Plant();
        // The Electric Smelter's Silicon is recipe 4, its Titanium 5.
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North, 3);
        Assert.True(sim.SetInputChoice(furnace.Id, 4, 1, ResourceType.Charcoal));
        Assert.False(sim.SetInputChoice(furnace.Id, 5, 0, ResourceType.Charcoal));
        sim.Debug.SeedStockpile(ResourceType.TitaniumOre, 2f);
        Assert.True(sim.QueueCraft(furnace.Id, 5, 1));
        SimTestHelpers.Seconds(sim, 21.0);

        // Titanium ran in between; Silicon still takes Charcoal.
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Titanium));
        Recipe silicon = furnace.Recipes[4];
        Assert.Equal(ResourceType.Charcoal, sim.ChosenInput(furnace, silicon, 1));
    }

    [Fact]
    public void TheQueue_SurvivesASave()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        Assert.True(sim.QueueCraft(smelter.Id, 0, 3));
        Assert.True(sim.QueueCraft(smelter.Id, 1, 2));
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 12, Facing.North, 3);
        Assert.True(sim.SetInputChoice(furnace.Id, 4, 1, ResourceType.Charcoal));
        SimTestHelpers.Seconds(sim, 20.0);

        Simulation loaded = Simulation.FromSave(sim.Save());

        Building again = loaded.BuildingById(smelter.Id)!;
        Assert.Equal("Iron 2, Copper 2", Queue(again));
        Assert.True(again.Crafting);
        Assert.True(Simulation.HasInputChoice(loaded.BuildingById(furnace.Id)!, furnace.Recipes[4], 1));
    }

    [Fact]
    public void AnOlderSave_LoadsWithAnEmptyQueue_AndItsInputChoicesOnItsRecipe()
    {
        Simulation sim = Plant();
        Building furnace = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North, 3);
        SaveData save = sim.Save();
        BuildingData data = save.Buildings.Single(b => b.Id == furnace.Id);
        data.Queue.Clear();
        data.RecipeIndex = 4;
        data.PendingRecipe = 1;
        data.InputChoices = new() { new[] { 1, (int)ResourceType.Charcoal } };

        Building loaded = Simulation.FromSave(save).BuildingById(furnace.Id)!;

        Assert.Empty(loaded.Queue);
        Assert.Null(loaded.PendingRecipe);
        Assert.True(Simulation.HasInputChoice(loaded, loaded.Recipes[4], 1));
    }
}
