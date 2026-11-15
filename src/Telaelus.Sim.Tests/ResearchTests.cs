namespace Telaelus.Sim.Tests;

public class ResearchTests
{
    private const ResearchProject Basic = ResearchProject.MaterialTransfer;
    private const ResearchProject Advanced = ResearchProject.AdvancedMaterialTransfer;

    /// <summary>The empty map with a Burner Generator on Coal (100 Power) beside the HQ.</summary>
    private static Simulation Powered()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        return sim;
    }

    private static void SeedFee(Simulation sim, ResearchProject project)
    {
        foreach (ResourceAmount cost in ResearchDefs.For(project).Fee)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
    }

    [Fact]
    public void Starting_TakesTheFeeOnce_AndRefusesWithTheFeeShort()
    {
        Simulation sim = Powered();
        sim.Debug.SeedStockpile(ResourceType.Iron, 40f);
        Assert.Equal(ResearchState.Available, sim.StateOf(Basic));
        Assert.False(sim.StartResearch(Basic));

        sim.Debug.SeedStockpile(ResourceType.Copper, 20f);
        Assert.True(sim.StartResearch(Basic));
        Assert.Equal(Basic, sim.ActiveResearch);
        Assert.Equal(ResearchState.Running, sim.StateOf(Basic));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Iron));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Copper));
        Assert.True(sim.ResearchFeePaid(Basic));
    }

    [Fact]
    public void AdvancedMaterialTransfer_IsLocked_UntilMaterialTransferIsComplete()
    {
        Simulation sim = Powered();
        SeedFee(sim, Advanced);
        Assert.Equal(ResearchState.Locked, sim.StateOf(Advanced));
        Assert.False(sim.StartResearch(Advanced));

        sim.Debug.SetResearchProgress(Basic, ResearchDefs.For(Basic).Work);

        Assert.Equal(ResearchState.Complete, sim.StateOf(Basic));
        Assert.Equal(ResearchState.Available, sim.StateOf(Advanced));
        Assert.True(sim.StartResearch(Advanced));
        Assert.False(sim.StartResearch(Basic));
    }

    [Fact]
    public void Pausing_KeepsProgressAndTheFee_AndResumingChargesNothing()
    {
        Simulation sim = Powered();
        sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 48, 4, Facing.North);
        SeedFee(sim, Basic);
        Assert.True(sim.StartResearch(Basic));
        SimTestHelpers.Seconds(sim, 100.0);

        sim.PauseResearch();
        float progress = sim.ResearchProgress(Basic);
        Assert.Equal(100f, progress, 0);
        Assert.Equal(ResearchState.Paused, sim.StateOf(Basic));
        SimTestHelpers.Seconds(sim, 50.0);
        Assert.Equal(progress, sim.ResearchProgress(Basic));

        // No fee is held now, and none is needed to resume.
        Assert.True(sim.StartResearch(Basic));
        SimTestHelpers.Seconds(sim, 50.0);
        Assert.Equal(150f, sim.ResearchProgress(Basic), 0);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 1f)]
    [InlineData(2, 1.5f)]
    [InlineData(3, 1.75f)]
    [InlineData(5, 2.25f)]
    public void ResearchSpeed_DiminishesWithEachLab_DownToAFloor(int labs, float speed)
    {
        Assert.Equal(speed, Simulation.SpeedOf(System.Linq.Enumerable.Repeat(1f, labs)), 4);
    }

    [Fact]
    public void OneLab_FinishesMaterialTransfer_InTwoGameDays()
    {
        Simulation sim = Powered();
        Building lab = sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 48, 4, Facing.North);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "No research running"), sim.BuildingStatusOf(lab));
        SeedFee(sim, Basic);
        Assert.True(sim.StartResearch(Basic));
        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(lab));
        Assert.Equal(1f, sim.ResearchSpeed);

        SimTestHelpers.Seconds(sim, 2 * Simulation.DayLength - 1.0);
        Assert.False(sim.IsResearched(Basic));
        SimTestHelpers.Seconds(sim, 1.1);

        Assert.True(sim.IsResearched(Basic));
        Assert.Null(sim.ActiveResearch);
    }

    [Fact]
    public void TwoLabs_Research_AtOneAndAHalf()
    {
        Simulation sim = Powered();
        sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 48, 4, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 52, 12, Facing.North);
        SeedFee(sim, Basic);
        Assert.True(sim.StartResearch(Basic));
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(2, sim.WorkingLabs.Count);

        SimTestHelpers.Seconds(sim, 100.0);

        Assert.Equal(1.5f, sim.ResearchSpeed);
        Assert.Equal(150f, sim.ResearchProgress(Basic), 0);
    }

    [Fact]
    public void WithNoLabWorking_ResearchWaits()
    {
        Simulation sim = Powered();
        Building lab = sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 48, 4, Facing.North);
        SeedFee(sim, Basic);
        Assert.True(sim.StartResearch(Basic));
        Assert.True(sim.SetBuildingEnabled(lab.Id, false));

        SimTestHelpers.Seconds(sim, 60.0);

        Assert.Equal(0f, sim.ResearchSpeed);
        Assert.Equal(0f, sim.ResearchProgress(Basic));
        Assert.Equal(Basic, sim.ActiveResearch);
    }

    [Fact]
    public void MaterialTransfer_DrainsAnOnlineMine_ButNotAnOfflineOne_NorBauxite()
    {
        Simulation sim = Powered();
        // An online Mine on Iron Ore beside the HQ, an offline one far away, and an online Drill Rig on Bauxite.
        sim.Debug.PlaceField(ResourceType.IronOre, 12, 4, 200f);
        Building near = sim.Debug.PlaceBuilding(BuildingType.Mine, 52, 16, Facing.North);
        sim.Debug.PlaceField(ResourceType.IronOre, 3, 3, 200f);
        Building far = sim.Debug.PlaceBuilding(BuildingType.Mine, 12, 10, Facing.North);
        sim.Debug.PlaceField(ResourceType.Bauxite, 11, 2, 200f);
        Building rig = sim.Debug.PlaceBuilding(BuildingType.DrillRig, 44, 4, Facing.North);
        Assert.True(near.Online);
        Assert.False(far.Online);
        Assert.True(rig.Online);

        SimTestHelpers.Seconds(sim, 40.0);
        Assert.True(near.OutputCount >= 4f);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        sim.Debug.SetResearchProgress(Basic, ResearchDefs.For(Basic).Work);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(0f, near.OutputCount);
        Assert.True(SimTestHelpers.Stock(sim, ResourceType.IronOre) >= 4f);
        Assert.True(sim.Transfers(ResourceType.IronOre));
        Assert.False(sim.Transfers(ResourceType.Bauxite));

        SimTestHelpers.Seconds(sim, 40.0);
        Assert.True(far.OutputCount >= 8f);
        Assert.True(rig.OutputCount >= 4f);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Bauxite));

        // Bauxite keeps only in a Stockyard or Silo, not at the HQ.
        Assert.True(sim.Debug.PlaceBuilding(BuildingType.Stockyard, 52, 8, Facing.North).Online);
        sim.Debug.SetResearchProgress(Advanced, ResearchDefs.For(Advanced).Work);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(0f, rig.OutputCount);
        Assert.True(far.OutputCount >= 8f);
    }

    [Fact]
    public void Transfer_StopsWhenStorageIsFull()
    {
        Simulation sim = Powered();
        sim.Debug.PlaceField(ResourceType.IronOre, 12, 4, 400f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 52, 16, Facing.North);
        sim.Debug.SetResearchProgress(Basic, ResearchDefs.For(Basic).Work);
        float room = sim.RoomFor(ResourceType.IronOre);
        sim.Debug.SeedStockpile(ResourceType.IronOre, room);

        SimTestHelpers.Seconds(sim, 40.0);

        Assert.Equal(0f, sim.RoomFor(ResourceType.IronOre), 3);
        Assert.True(mine.OutputCount >= 4f);
    }
}
