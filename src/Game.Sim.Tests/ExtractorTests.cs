namespace Game.Sim.Tests;

public class ExtractorTests
{
    // A Mine anchored here (center 45, 35) has Ore (10, 9) about 1.4 away and
    // Ore (11, 10) 5 away, both within its 6-unit work radius.
    private const int NearOreX = 44;
    private const int NearOreZ = 34;

    [Fact]
    public void BaseBuildings_AreSlowerThanThePlayer()
    {
        foreach (BuildingType type in System.Enum.GetValues<BuildingType>())
        {
            Assert.Equal(0.75f, BuildingDefs.For(type).Speed);
        }
    }

    [Fact]
    public void Mine_WorksTheNearestOreFieldInRange_OneUnitAtATime()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North);
        Building mine = Assert.Single(sim.Buildings);

        // Iron Ore takes 6 s by hand, so 8 s at speed 0.75.
        SimTestHelpers.Seconds(sim, 7.9);
        Assert.Same(SimTestHelpers.FieldAtMapCell(sim, 10, 9), mine.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));

        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Mine_MovesToTheNextFieldWhenOneDepletes()
    {
        var sim = new Simulation();
        ResourceField first = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        ResourceField second = SimTestHelpers.FieldAtMapCell(sim, 11, 10);
        sim.Debug.SetFieldAmount(10, 9, 2f);
        sim.Debug.PlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North);
        Building mine = Assert.Single(sim.Buildings);

        // Two units empty the first field at 16 s; the next unit comes from the second at 24 s.
        SimTestHelpers.Seconds(sim, 24.2);

        Assert.True(first.Depleted);
        Assert.DoesNotContain(first, sim.Fields);
        Assert.Same(second, mine.CurrentField);
        Assert.Equal(249f, second.Amount);
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Mine_IdlesWithNoFieldInRange()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        Building mine = Assert.Single(sim.Buildings);

        SimTestHelpers.Seconds(sim, 9.0);

        Assert.Null(mine.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void LumberCamp_IgnoresOreFields()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.LumberCamp, NearOreX, NearOreZ, Facing.North);
        Building camp = Assert.Single(sim.Buildings);

        SimTestHelpers.Seconds(sim, 9.0);

        Assert.Null(camp.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void TwoMines_ShareAField()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        sim.Debug.PlaceBuilding(BuildingType.Mine, NearOreX, NearOreZ, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Mine, 40, 34, Facing.North);

        SimTestHelpers.Seconds(sim, 8.1);

        // One unit each, together faster than the player.
        Assert.Equal(248f, field.Amount);
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }
}
