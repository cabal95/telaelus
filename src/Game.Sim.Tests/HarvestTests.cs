using Godot;

namespace Game.Sim.Tests;

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
    public void Harvest_PullsFromTheFieldIntoTheStockpile()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        SimTestHelpers.Ticks(sim, 30);

        // 10 per second for one second.
        Assert.Equal(240f, field.Amount, 2);
        Assert.Equal(10f, SimTestHelpers.Stock(sim, ResourceType.IronOre), 2);
    }

    [Fact]
    public void Harvest_DoesNothingWithoutTheIntent()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        SimTestHelpers.Seconds(sim, 1.0);

        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
    }

    [Fact]
    public void DepletedField_IsRemovedAndItsCellsReopen()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        SimTestHelpers.Teleport(sim, 41.5f, 35f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        SimTestHelpers.Seconds(sim, 26.0);

        Assert.True(field.Depleted);
        Assert.DoesNotContain(field, sim.Fields);
        Assert.Null(sim.FieldAt(41, 37));
        Assert.True(sim.IsWalkable(41, 37));
        Assert.Equal(250f, SimTestHelpers.Stock(sim, ResourceType.IronOre), 2);
    }
}
