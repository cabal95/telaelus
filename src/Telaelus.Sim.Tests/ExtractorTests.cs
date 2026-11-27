namespace Telaelus.Sim.Tests;

public class ExtractorTests
{
    // A Mine anchored here (center 45, 35) has Ore (10, 9) about 1.4 away and
    // Ore (11, 10) 5 away, both within its 6-unit work radius.
    private const int NearOreX = 44;
    private const int NearOreZ = 34;

    [Fact]
    public void ALevel1Extractor_IsTheBase_TheLevelsFaster_AndThePlayerFasterByHand()
    {
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels())
        {
            BuildingDef def = BuildingDefs.For(type, level);
            if (def.IsExtractor)
            {
                Assert.True((level switch { 2 => 1.75f, 3 => 2.15f, _ => 1f }) == def.Speed, def.Name);
            }
        }
        Assert.Equal(4f / 3f, Simulation.HandGatherSpeed);
    }

    [Fact]
    public void Mine_WorksTheNearestOreFieldInRange_OneUnitAtATime()
    {
        var sim = new Simulation();
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);
        Building mine = Assert.Single(SimTestHelpers.Placed(sim));

        // Iron Ore takes 8 s at a level 1 extractor (6 s by hand, at +33%).
        SimTestHelpers.Seconds(sim, 7.9);
        Assert.Same(SimTestHelpers.FieldAtMapCell(sim, 10, 9), mine.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));

        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Mine_MovesToTheNextFieldWhenOneDepletes()
    {
        var sim = new Simulation();
        ResourceField first = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        ResourceField second = SimTestHelpers.FieldAtMapCell(sim, 11, 10);
        sim.Debug.SetFieldAmount(10, 9, 2f);
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);
        Building mine = Assert.Single(SimTestHelpers.Placed(sim));

        // Two units empty the first field at 16 s; the next unit comes from the second at 24 s.
        SimTestHelpers.Seconds(sim, 24.2);

        Assert.True(first.Depleted);
        Assert.DoesNotContain(first, sim.Fields);
        Assert.Same(second, mine.CurrentField);
        Assert.Equal(999f, second.Amount);
        Assert.Equal(3f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Mine_IdlesWithNoFieldInRange()
    {
        var sim = new Simulation();
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);
        Building mine = Assert.Single(SimTestHelpers.Placed(sim));

        SimTestHelpers.Seconds(sim, 9.0);

        Assert.Null(mine.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
    }

    [Fact]
    public void LumberCamp_IgnoresOreFields()
    {
        var sim = new Simulation();
        SimTestHelpers.PlaceOnline(sim, BuildingType.LumberCamp, NearOreX, NearOreZ);
        Building camp = Assert.Single(SimTestHelpers.Placed(sim));

        SimTestHelpers.Seconds(sim, 9.0);

        Assert.Null(camp.CurrentField);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.Wood));
    }

    [Fact]
    public void TwoMines_ShareAField()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, NearOreX, NearOreZ);
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 40, 32);

        SimTestHelpers.Seconds(sim, 8.1);

        // One unit each, together faster than the player.
        Assert.Equal(998f, field.Amount);
        Assert.Equal(2f, SimTestHelpers.Gathered(sim, ResourceType.IronOre));
    }

    [Fact]
    public void AMine_WorksTheCommonNewDeposits_AndADrillRigThePreciousOnes()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.PlaceField(ResourceType.Limestone, 5, 5);
        sim.Debug.PlaceField(ResourceType.Crystals, 10, 5);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 22, 24);
        Building rig = sim.Debug.PlaceBuilding(BuildingType.Mine, 41, 24, Facing.North, 2);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(ResourceType.Limestone, mine.CurrentField?.Type);
        Assert.Equal(ResourceType.Crystals, rig.CurrentField?.Type);
        Assert.DoesNotContain(ResourceType.Crystals, BuildingDefs.For(BuildingType.Mine).Works);
    }
}
