namespace Game.Sim.Tests;

public class LaunchTests
{
    [Fact]
    public void Launch_RequiresTheFullCost()
    {
        var sim = new Simulation();
        sim.TestSeedStockpile(ResourceType.Fuel, Simulation.LaunchFuelCost);
        sim.TestSeedStockpile(ResourceType.Metal, Simulation.LaunchMetalCost - 1f);

        Assert.False(sim.CanLaunchShip());
        Assert.False(sim.TryLaunchShip());
        Assert.Equal(0, sim.ShipsLaunched);
    }

    [Fact]
    public void Launch_ConsumesTheCostAndCountsTheShip()
    {
        var sim = new Simulation();
        sim.TestSeedStockpile(ResourceType.Fuel, 600f);
        sim.TestSeedStockpile(ResourceType.Metal, 150f);

        Assert.True(sim.CanLaunchShip());
        Assert.True(sim.TryLaunchShip());

        Assert.Equal(1, sim.ShipsLaunched);
        Assert.Equal(100f, SimTestHelpers.Stock(sim, ResourceType.Fuel));
        Assert.Equal(50f, SimTestHelpers.Stock(sim, ResourceType.Metal));
    }

    [Fact]
    public void EndToEnd_StartingFieldsReachALaunch()
    {
        var sim = new Simulation();

        // One extractor beside each starting field, plus one Smelter and one Refinery.
        (BuildingType Type, int X, int Z)[] placements =
        {
            (BuildingType.Mine, 40, 34),
            (BuildingType.Mine, 48, 40),
            (BuildingType.Mine, 34, 44),
            (BuildingType.Mine, 20, 48),
            (BuildingType.LumberCamp, 12, 20),
            (BuildingType.LumberCamp, 20, 12),
            (BuildingType.LumberCamp, 8, 24),
            (BuildingType.LumberCamp, 52, 12),
            (BuildingType.Smelter, 28, 28),
            (BuildingType.Refinery, 32, 28)
        };
        foreach ((BuildingType type, int x, int z) in placements)
        {
            Assert.True(sim.TryPlaceBuilding(type, x, z, Facing.North), $"{type} at ({x}, {z})");
        }

        // The Refinery makes 0.5 Fuel per second, so 500 Fuel takes about 1000 seconds.
        int ticks = 0;
        const int MaxTicks = 30 * 1500;
        while (!sim.CanLaunchShip() && ticks < MaxTicks)
        {
            sim.Step(SimTestHelpers.TickSeconds);
            ticks++;
        }

        Assert.True(sim.TryLaunchShip(), $"no launch after {ticks / 30} seconds");
        Assert.Equal(1, sim.ShipsLaunched);
    }
}
