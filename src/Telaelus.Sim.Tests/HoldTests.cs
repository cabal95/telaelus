using System.Linq;

namespace Telaelus.Sim.Tests;

public class HoldTests
{
    /// <summary>
    /// The empty map with six Burner Generators on Coal and a Charcoal Kiln (3 Wood to 1 Charcoal,
    /// 15 s, no fuel) beside the HQ, with Wood to spare.
    /// </summary>
    private static (Simulation Sim, Building Kiln) Kiln()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 180f);
        SimTestHelpers.Burners(sim, 6);
        sim.Debug.SeedStockpile(ResourceType.Wood, 60f);
        Building kiln = sim.Debug.PlaceBuilding(BuildingType.Kiln, 50, 14, Facing.North);
        return (sim, kiln);
    }

    [Fact]
    public void EnteringHold_EmptiesTheQueue_AndStopsACycleOfAnotherRecipe()
    {
        (Simulation sim, _) = Kiln();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 5f);
        sim.Debug.SeedStockpile(ResourceType.CopperOre, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 4, Facing.North);
        Assert.True(sim.QueueCraft(smelter.Id, 0, 3));
        Assert.True(sim.QueueCraft(smelter.Id, 1, 3));
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.True(smelter.Crafting);
        Assert.Equal(4f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        // Holding Copper: the Iron cycle stops and its Ore goes back.
        Assert.True(sim.SetHold(smelter.Id, 1, 3));
        Assert.Empty(smelter.Queue);
        Assert.False(smelter.Crafting);
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(1, smelter.RecipeIndex);

        Assert.False(sim.SetHold(smelter.Id, 2, 3));
        Assert.False(sim.SetHold(smelter.Id, 0, 0));
    }

    [Fact]
    public void EnteringHold_OnTheRunningRecipe_LetsItsCycleRun()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.True(sim.QueueCraft(kiln.Id, 0, 5));
        SimTestHelpers.Seconds(sim, 5.0);
        float progress = kiln.Progress;

        Assert.True(sim.SetHold(kiln.Id, 0, 2));
        Assert.Empty(kiln.Queue);
        Assert.True(kiln.Crafting);
        Assert.Equal(progress, kiln.Progress);
    }

    [Fact]
    public void AHeldKiln_MakesUpToItsTarget_Holds_AndResumesWhenStockIsUsed()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.True(sim.SetHold(kiln.Id, 0, 2));
        SimTestHelpers.Seconds(sim, 30.5);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.False(kiln.Crafting);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Holding: 2 Charcoal"), sim.BuildingStatusOf(kiln));
        Assert.True(kiln.Online);

        // Held: no more cycles, and no more Wood taken.
        SimTestHelpers.Seconds(sim, 20.0);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.Equal(54f, SimTestHelpers.Stock(sim, ResourceType.Wood));

        // Charcoal used elsewhere: it makes one more.
        sim.Debug.SeedStockpile(ResourceType.Charcoal, -1f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(kiln.Crafting);
        SimTestHelpers.Seconds(sim, 15.5);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Holding: 2 Charcoal"), sim.BuildingStatusOf(kiln));
    }

    [Fact]
    public void ACycleStartedUnderTheTarget_FinishesPastIt()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.True(sim.SetHold(kiln.Id, 0, 1));
        SimTestHelpers.Seconds(sim, 5.0);
        Assert.True(kiln.Crafting);

        // Storage reaches the target mid-cycle; the cycle still finishes.
        sim.Debug.SeedStockpile(ResourceType.Charcoal, 1f);
        SimTestHelpers.Seconds(sim, 10.5);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.False(kiln.Crafting);
    }

    [Fact]
    public void TheTarget_StartsAtTheStock_AndAtLeastOneCycle()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.Equal(1, sim.DefaultHoldTarget(kiln, 0));
        sim.Debug.SeedStockpile(ResourceType.Charcoal, 7f);
        Assert.Equal(7, sim.DefaultHoldTarget(kiln, 0));

        // Copper Wire is two to a cycle.
        Building works = sim.Debug.PlaceBuilding(BuildingType.ElectronicsWorks, 40, 4, Facing.North);
        int wire = works.Recipes.ToList().FindIndex(r => r.Outputs[0].Type == ResourceType.CopperWire);
        Assert.Equal(2, sim.DefaultHoldTarget(works, wire));
    }

    [Fact]
    public void LeavingHold_FinishesTheRunningCycle_WithAnEmptyQueue()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.True(sim.SetHold(kiln.Id, 0, 5));
        SimTestHelpers.Seconds(sim, 5.0);
        Assert.True(kiln.Crafting);

        Assert.True(sim.ClearHold(kiln.Id));
        Assert.False(sim.ClearHold(kiln.Id));
        Assert.Null(kiln.Hold);
        SimTestHelpers.Seconds(sim, 10.5);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        SimTestHelpers.Seconds(sim, 20.0);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Queue empty"), sim.BuildingStatusOf(kiln));
    }

    [Fact]
    public void AHold_SurvivesASave()
    {
        (Simulation sim, Building kiln) = Kiln();
        Assert.True(sim.SetHold(kiln.Id, 0, 4));

        Simulation loaded = Simulation.FromSave(sim.Save());

        Building again = loaded.Buildings.Single(b => b.Id == kiln.Id);
        Assert.NotNull(again.Hold);
        Assert.Equal((0, 4), (again.Hold!.RecipeIndex, again.Hold.Target));
    }
}
