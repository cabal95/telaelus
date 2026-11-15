using Godot;

namespace Game.Sim.Tests;

public class FieldTests
{
    [Theory]
    [InlineData(250f, 5)]
    [InlineData(200f, 4)]
    [InlineData(125f, 3)]
    [InlineData(1f, 1)]
    [InlineData(0f, 0)]
    public void Stage_IsTheCeilingOfTheRemainingFifths(float amount, int expectedStage)
    {
        var field = new ResourceField(0, ResourceType.Wood, 0, 0, 250f)
        {
            Amount = amount
        };
        Assert.Equal(expectedStage, field.Stage);
    }

    [Fact]
    public void Depleted_WhenEmpty()
    {
        var field = new ResourceField(0, ResourceType.IronOre, 0, 0, 250f)
        {
            Amount = 0f
        };
        Assert.True(field.Depleted);
        Assert.Equal(0f, field.RemainingFraction);
    }

    [Fact]
    public void Bounds_SpanItsMapCell()
    {
        var field = new ResourceField(0, ResourceType.IronOre, 10, 9, 250f);
        Assert.Equal(40f, field.MinX);
        Assert.Equal(36f, field.MinZ);
        Assert.Equal(new Vector3(42f, 0f, 38f), field.Center);
    }

    [Fact]
    public void DistanceToField_IsZeroInsideAndMeasuresToTheNearestEdgeOrCorner()
    {
        // Field square spans world [0, 4] x [0, 4].
        var field = new ResourceField(0, ResourceType.IronOre, 0, 0, 250f);

        Assert.Equal(0f, Simulation.DistanceToField(new Vector3(2f, 0f, 2f), field));
        Assert.Equal(1f, Simulation.DistanceToField(new Vector3(5f, 0f, 2f), field), 4);
        Assert.Equal(1f, Simulation.DistanceToField(new Vector3(2f, 0f, -1f), field), 4);
        Assert.Equal(5f, Simulation.DistanceToField(new Vector3(7f, 0f, 8f), field), 4);
    }

    [Fact]
    public void FieldAt_CoversAll16FineCellsOfItsMapCellAndNothingElse()
    {
        var sim = new Simulation();
        ResourceField field = SimTestHelpers.FieldAtMapCell(sim, 10, 9);
        Assert.Equal(ResourceType.IronOre, field.Type);

        for (int x = 40; x < 44; x++)
        {
            for (int z = 36; z < 40; z++)
            {
                Assert.Same(field, sim.FieldAt(x, z));
            }
        }

        Assert.Null(sim.FieldAt(39, 36));
        Assert.Null(sim.FieldAt(40, 35));
        Assert.Null(sim.FieldAt(44, 39));
    }

    [Fact]
    public void StartingFields_HoldEnoughForALaunch()
    {
        var sim = new Simulation();
        var totals = new System.Collections.Generic.Dictionary<ResourceType, float>();
        foreach (ResourceField field in sim.Fields)
        {
            totals.TryGetValue(field.Type, out float sum);
            totals[field.Type] = sum + field.Amount;
        }

        Assert.Equal(22, sim.Fields.Count);
        Assert.Equal(4600f, totals[ResourceType.IronOre]);
        Assert.Equal(1200f, totals[ResourceType.CopperOre]);
        Assert.Equal(1600f, totals[ResourceType.Coal]);
        Assert.Equal(2600f, totals[ResourceType.Stone]);
        Assert.Equal(7000f, totals[ResourceType.Wood]);
        Assert.Equal(4000f, totals[ResourceType.CrudeOil]);

        // A launch needs 100 Iron (from Iron Ore) and 500 Rocket Fuel (from Crude Oil).
        Assert.True(totals[ResourceType.IronOre] >= Simulation.LaunchIronCost);
        Assert.True(totals[ResourceType.CrudeOil] >= Simulation.LaunchRocketFuelCost);
    }

    [Fact]
    public void FieldsInRange_FiltersByTypeAndDistance()
    {
        var sim = new Simulation();
        var point = new Vector3(45f, 0f, 35f);

        // Ore (10, 9) is ~1.4 away and Ore (11, 10) is 5 away; nothing else is within 6.
        Assert.Equal(2, sim.FieldsInRange(point, 6f, ResourceType.IronOre).Count);
        Assert.Single(sim.FieldsInRange(point, 3f, ResourceType.IronOre));
        Assert.Empty(sim.FieldsInRange(point, 6f, ResourceType.Wood));
    }
}
