namespace Game.Sim.Tests;

public class LaunchTests
{
    [Fact]
    public void Launch_RequiresTheFullCost()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.RocketFuel, Simulation.LaunchRocketFuelCost);
        sim.Debug.SeedStockpile(ResourceType.Iron, Simulation.LaunchIronCost - 1f);

        Assert.False(sim.CanLaunchShip());
        Assert.False(sim.TryLaunchShip());
        Assert.Equal(0, sim.ShipsLaunched);
    }

    [Fact]
    public void Launch_ConsumesTheCostAndCountsTheShip()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.RocketFuel, 600f);
        sim.Debug.SeedStockpile(ResourceType.Iron, 150f);

        Assert.True(sim.CanLaunchShip());
        Assert.True(sim.TryLaunchShip());

        Assert.Equal(1, sim.ShipsLaunched);
        Assert.Equal(100f, SimTestHelpers.Stock(sim, ResourceType.RocketFuel));
        Assert.Equal(50f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void EndToEnd_StartingFieldsReachALaunch()
    {
        var sim = new Simulation();

        // Mines beside the Iron Ore fields, one Oil Well between the two Crude Oil seeps, and two
        // each of Smelters and Refineries, so the one Oil Well keeps both Refineries busy.
        (BuildingType Type, int X, int Z)[] placements =
        {
            (BuildingType.Mine, 40, 34),
            (BuildingType.Mine, 48, 40),
            (BuildingType.Mine, 34, 44),
            (BuildingType.Mine, 20, 48),
            (BuildingType.OilWell, 58, 28),
            (BuildingType.Smelter, 28, 28),
            (BuildingType.Smelter, 24, 28),
            (BuildingType.Refinery, 32, 28),
            (BuildingType.Refinery, 34, 28)
        };
        foreach ((BuildingType type, int x, int z) in placements)
        {
            sim.Debug.PlaceBuilding(type, x, z, Facing.North);
        }

        // The Oil Well pumps one Crude Oil every 4 seconds, so 500 Rocket Fuel takes about 2000.
        int ticks = 0;
        const int MaxTicks = 30 * 2200;
        while (!sim.CanLaunchShip() && ticks < MaxTicks)
        {
            sim.Step(SimTestHelpers.TickSeconds);
            ticks++;
        }

        Assert.True(sim.TryLaunchShip(), $"no launch after {ticks / 30} seconds");
        Assert.Equal(1, sim.ShipsLaunched);
    }
}
