namespace Telaelus.Sim.Tests;

public class ProcessSpeedTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    private static void Complete(Simulation sim, params ResearchProject[] projects)
    {
        foreach (ResearchProject project in projects)
        {
            sim.Debug.SetResearchProgress(project, ResearchDefs.For(project).Work);
        }
    }

    /// <summary>A powered Mine at a level by an Iron Ore field on the empty map, one Burner Generator on Coal.</summary>
    private static Building PoweredMine(Simulation sim, int level)
    {
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        sim.Debug.PlaceField(ResourceType.IronOre, 11, 4, 100f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 49, 13, Facing.North, level);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 53, 13, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
        return mine;
    }

    [Theory]
    [InlineData(ResearchProject.BetterMining1, BuildingType.Mine, 0.25f, 1, 0, 2, null)]
    [InlineData(ResearchProject.BetterLogging1, BuildingType.LumberCamp, 0.25f, 1, 0, 3, null)]
    [InlineData(ResearchProject.BetterSmelting1, BuildingType.Smelter, 0.25f, 1, 0, 7, null)]
    [InlineData(ResearchProject.BetterMining2, BuildingType.Mine, 0.5f, 2, 0, 2, ResearchProject.BetterMining1)]
    [InlineData(ResearchProject.BetterLogging2, BuildingType.LumberCamp, 0.5f, 2, 0, 3, ResearchProject.BetterLogging1)]
    [InlineData(ResearchProject.BetterSmelting2, BuildingType.Smelter, 0.5f, 2, 0, 7, ResearchProject.BetterSmelting1)]
    [InlineData(ResearchProject.BetterDrilling1, BuildingType.OilWell, 0.25f, 2, 0, 9, null)]
    [InlineData(ResearchProject.BetterDrilling2, BuildingType.OilWell, 0.5f, 3, 0, 9, ResearchProject.BetterDrilling1)]
    public void EachSpeedProject_HasItsFamilyPartTierPlaceAndNeeds(ResearchProject project, BuildingType family, float bonus,
        int tier, int column, int row, ResearchProject? needs)
    {
        ResearchDef def = ResearchDefs.For(project);
        Assert.Equal((family, bonus), def.Speeds);
        Assert.Equal(tier, def.Tier);
        Assert.Equal((column, row), (def.Column, def.Row));
        Assert.Equal(needs is ResearchProject first ? new[] { first } : System.Array.Empty<ResearchProject>(), def.Prerequisites);
    }

    [Fact]
    public void ALevel1Mine_KeepsTodaysRate_AndBetterMiningIMakesIt25PercentFaster()
    {
        var sim = new Simulation();
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 44, 34);
        Complete(sim, ResearchProject.BetterMining1);

        // Iron Ore is 8 s a unit at a level 1 extractor, 6.4 s at 125%.
        SimTestHelpers.Seconds(sim, 6.3);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        Assert.Equal(1.25f, sim.WorkSpeed(mine), 3);
    }

    [Fact]
    public void BetterMiningII_ReplacesI_RatherThanStacking()
    {
        var sim = new Simulation();
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 44, 34);
        Complete(sim, ResearchProject.BetterMining1, ResearchProject.BetterMining2);
        SimTestHelpers.Ticks(sim, 1);

        SpeedParts parts = sim.SpeedPartsOf(mine);
        Assert.Equal(0.5f, parts.Research);
        Assert.Equal(ResearchProject.BetterMining2, parts.ResearchFrom);
        Assert.Equal(1.5f, parts.Total, 3);
    }

    [Fact]
    public void ADrillRigWithBetterMiningI_AddsItsParts_To200Percent()
    {
        Simulation sim = EmptyMap();
        Building rig = PoweredMine(sim, 2);
        Complete(sim, ResearchProject.BetterMining1);
        SimTestHelpers.Ticks(sim, 1);

        SpeedParts parts = sim.SpeedPartsOf(rig);
        Assert.Equal(0.75f, parts.Level, 3);
        Assert.Equal(0.25f, parts.Research);
        Assert.Equal(1f, parts.Shortage);
        Assert.Equal(2f, parts.Total, 3);

        // 8 s a unit at 200% is 4 s: 3 units in a little over 12 s.
        SimTestHelpers.Seconds(sim, 12.2);
        Assert.Equal(3f, rig.Output[ResourceType.IronOre]);
    }

    [Fact]
    public void ADeepMineWithBetterMiningII_TopsOutAt265Percent_OnAField()
    {
        Simulation sim = EmptyMap();
        Building deep = PoweredMine(sim, 3);
        Complete(sim, ResearchProject.BetterMining1, ResearchProject.BetterMining2);
        SimTestHelpers.Ticks(sim, 1);

        Assert.NotNull(deep.CurrentField);
        Assert.Equal(2.65f, sim.WorkSpeed(deep), 3);
    }

    [Fact]
    public void AShortage_ScalesTheWholeSum()
    {
        // Four Refineries (25 each) and two Drill Rigs (10 each) on three Burner Generators (35
        // each): 120 against 105, a brownout at 1 less twice the overload.
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        for (int i = 0; i < 4; i++)
        {
            sim.Debug.PlaceBuilding(BuildingType.Refinery, 23 + 2 * i, 28, Facing.North);
        }
        Building rig = sim.Debug.PlaceBuilding(BuildingType.Mine, 23, 32, Facing.North, 2);
        sim.Debug.PlaceBuilding(BuildingType.Mine, 27, 32, Facing.North, 2);
        SimTestHelpers.Burners(sim, 3, 38, 36);
        sim.Debug.PlaceBuilding(BuildingType.SmallHub, 29, 31, Facing.North);
        SimTestHelpers.Connect(sim, 29.5f, 31.5f);
        Complete(sim, ResearchProject.BetterMining1);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(CapacityState.Brownout, sim.Capacity(CapacityType.Power).State);
        SpeedParts parts = sim.SpeedPartsOf(rig);
        float speed = 1f - 2f * 15f / 105f;
        Assert.Equal(speed, parts.Shortage, 3);
        Assert.Equal(CapacityType.Power, parts.ShortOf);
        // No field in range, so no level part: 125% scaled by the brownout.
        Assert.Equal(1.25f * speed, parts.Total, 3);
    }

    [Fact]
    public void ASmelterOnCoal_WithBetterSmeltingI_AddsTheFuelAndResearchParts()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 2f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 2f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 14, Facing.North);
        Assert.True(sim.SetFuelChoice(smelter.Id, ResourceType.Coal));
        Complete(sim, ResearchProject.BetterSmelting1);
        SimTestHelpers.Order(sim, smelter);

        // Iron is 16 s a unit at 100%; Coal's +30% and the research's +25% make 155%: 10.3 s.
        SimTestHelpers.Seconds(sim, 10.1);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        SimTestHelpers.Seconds(sim, 0.4);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        SpeedParts parts = sim.SpeedPartsOf(smelter);
        Assert.Equal(ResourceType.Coal, parts.FuelFrom);
        Assert.Equal(1.55f, parts.Total, 3);
    }

    [Fact]
    public void AFasterSteelMill_DrawsTheSamePower()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Burners(sim, 1);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 8, Facing.North, 2);
        SimTestHelpers.Ticks(sim, 1);
        float before = sim.Capacity(CapacityType.Power).Demand;

        Complete(sim, ResearchProject.BetterSmelting1, ResearchProject.BetterSmelting2);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(20f, before);
        Assert.Equal(before, sim.Capacity(CapacityType.Power).Demand);
    }

    [Fact]
    public void ASecondStep_IsLockedUntilItsFirstIsDone()
    {
        Simulation sim = EmptyMap();
        Complete(sim, ResearchProject.SteelWorks);
        Assert.Equal(ResearchState.Available, sim.StateOf(ResearchProject.BetterMining1));
        Assert.Equal(ResearchState.Locked, sim.StateOf(ResearchProject.BetterMining2));

        Complete(sim, ResearchProject.BetterMining1);
        Assert.Equal(ResearchState.Available, sim.StateOf(ResearchProject.BetterMining2));
        Assert.Equal(ResearchState.Locked, sim.StateOf(ResearchProject.BetterDrilling2));
    }

    [Fact]
    public void ASave_KeepsTheBoost()
    {
        var sim = new Simulation();
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 44, 34);
        Complete(sim, ResearchProject.BetterMining1);

        Simulation loaded = Simulation.FromSave(sim.Save());
        SimTestHelpers.Ticks(loaded, 1);

        Building mine = loaded.BuildingAt(44, 34)!;
        Assert.Equal(1.25f, loaded.WorkSpeed(mine), 3);
    }
}
