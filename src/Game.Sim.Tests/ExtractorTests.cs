namespace Game.Sim.Tests;

public class ExtractorTests
{
    // A Mine anchored here (center 45, 35) has Ore (10, 9) about 1.4 away and
    // Ore (11, 10) 5 away, both within its 6-unit work radius.
    private const int NearOreX = 44;
    private const int NearOreZ = 34;

    [Fact]
    public void Mine_WorksTheNearestOreFieldInRange()
    {
        var sim = new Simulation();
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North));
        Building mine = Assert.Single(sim.Buildings);

        SimTestHelpers.Ticks(sim, 30);

        Assert.Same(SimTestHelpers.FieldAtMapCell(sim, 10, 9), mine.CurrentField);
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Ore), 2);
    }

    [Fact]
    public void Mine_MovesToTheNextFieldWhenOneDepletes()
    {
        var sim = new Simulation();
        ResourceField first = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        ResourceField second = SimTestHelpers.FieldAtMapCell(sim, 11, 10);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North));
        Building mine = Assert.Single(sim.Buildings);

        // 250 at 5 per second empties the first field in 50 seconds.
        SimTestHelpers.Seconds(sim, 52.0);

        Assert.True(first.Depleted);
        Assert.DoesNotContain(first, sim.Fields);
        Assert.Same(second, mine.CurrentField);
        Assert.True(second.Amount < 250f);
    }

    [Fact]
    public void Mine_IdlesWithNoFieldInRange()
    {
        var sim = new Simulation();
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
        Building mine = Assert.Single(sim.Buildings);

        SimTestHelpers.Seconds(sim, 2.0);

        Assert.Null(mine.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Ore));
    }

    [Fact]
    public void LumberCamp_IgnoresOreFields()
    {
        var sim = new Simulation();
        Assert.True(sim.TryPlaceBuilding(BuildingType.LumberCamp, NearOreX, NearOreZ, Facing.North));
        Building camp = Assert.Single(sim.Buildings);

        SimTestHelpers.Seconds(sim, 2.0);

        Assert.Null(camp.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Ore));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void TwoMines_ShareAField()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North));
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, 40, 34, Facing.North));

        SimTestHelpers.Ticks(sim, 30);

        // Two mines at 5 per second each.
        Assert.Equal(240f, field.Amount, 2);
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.Ore), 2);
    }
}
