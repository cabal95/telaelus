using Godot;

namespace Telaelus.Sim.Tests;

public class HarvestTests
{
    [Fact]
    public void HarvestTarget_IsAFieldWithinRangeOfItsEdge()
    {
        var sim = new Simulation();

        // One unit from the Ore field (10, 9) edge at z 36, but 2.8 from its center row.
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Same(SimTestHelpers.FieldAtMapCell(sim, 10, 9), sim.HarvestTarget);
    }

    [Fact]
    public void HarvestTarget_IsNullOutOfRange()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 41.5f, 33f);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Null(sim.HarvestTarget);
    }

    [Fact]
    public void Harvest_TakesOneWholeUnitAtTheGatherTime()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        // Iron Ore takes 6 seconds a unit by hand.
        SimTestHelpers.Seconds(sim, 5.9);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.InRange(sim.HarvestProgress, 0.95f, 1f);

        SimTestHelpers.Seconds(sim, 0.2);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(999f, field.Amount);
        Assert.InRange(sim.HarvestProgress, 0f, 0.05f);
    }

    [Fact]
    public void Harvest_ReleasingDropsThePartialUnit()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        var harvest = new PlayerIntent
        {
            Harvest = true
        };

        sim.Intent = harvest;
        SimTestHelpers.Seconds(sim, 5.0);
        sim.Intent = default;
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(0f, sim.HarvestProgress);

        sim.Intent = harvest;
        SimTestHelpers.Seconds(sim, 5.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void Harvest_ANewTargetDropsThePartialUnit()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        SimTestHelpers.Seconds(sim, 3.0);
        Assert.True(sim.HarvestProgress > 0f);

        SimTestHelpers.Teleport(sim, 41.5f, 33f);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Null(sim.HarvestTarget);
        Assert.Equal(0f, sim.HarvestProgress);
    }

    [Fact]
    public void Harvest_DoesNothingWithoutTheIntent()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        SimTestHelpers.Seconds(sim, 7.0);

        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(0f, sim.HarvestProgress);
    }

    [Fact]
    public void DepletedField_IsRemovedAndItsCellsReopen()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        sim.Debug.SetFieldAmount(10, 9, 2f);
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        SimTestHelpers.Seconds(sim, 12.5);

        Assert.True(field.Depleted);
        Assert.DoesNotContain(field, sim.Fields);
        Assert.Null(sim.FieldAt(41, 37));
        Assert.True(sim.IsWalkable(41, 37));
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }
}
