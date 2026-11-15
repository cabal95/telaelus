using System.Collections.Generic;
using System.Linq;

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
    public void EndToEnd_StartingFieldsReachALaunch_HaulingByHand()
    {
        var sim = new Simulation();

        // Mines beside the Iron Ore fields, one Oil Well between the two Crude Oil seeps, two each
        // of Smelters and Refineries, so the one Oil Well keeps both Refineries busy, and two Tank
        // Farms to hold the Crude Oil and the launch's 500 Rocket Fuel. Collectors never deliver
        // on their own, so the player walks round and collects every 20 seconds.
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
            (BuildingType.Refinery, 34, 28),
            (BuildingType.TankFarm, 36, 28),
            (BuildingType.TankFarm, 40, 28)
        };
        foreach ((BuildingType type, int x, int z) in placements)
        {
            sim.Debug.PlaceBuilding(type, x, z, Facing.North);
        }
        SimTestHelpers.Connect(sim, 41f, 29f);
        Assert.All(SimTestHelpers.Placed(sim).Where(b => !b.Def.IsExtractor && !b.Def.IsHub), b => Assert.True(b.Online, $"{b.Type} at ({b.AnchorX}, {b.AnchorZ}) is offline"));

        List<Building> collectors = SimTestHelpers.Placed(sim).Where(b => b.Def.IsExtractor).ToList();
        const double MaxSeconds = 4000;
        double seconds = 0;
        while (!sim.CanLaunchShip() && seconds < MaxSeconds)
        {
            SimTestHelpers.Seconds(sim, 20.0);
            seconds += 20.0;
            foreach (Building collector in collectors)
            {
                SimTestHelpers.CollectByHand(sim, collector);
            }
        }

        Assert.True(sim.TryLaunchShip(), $"no launch after {seconds} seconds of play: fuel {SimTestHelpers.Stock(sim, ResourceType.RocketFuel)}, iron {SimTestHelpers.Stock(sim, ResourceType.Iron)}, ore {SimTestHelpers.Stock(sim, ResourceType.IronOre)}, crude {SimTestHelpers.Stock(sim, ResourceType.CrudeOil)}; "
            + string.Join("; ", SimTestHelpers.Placed(sim).Select(b => $"{b.Type} {b.Online} {sim.BuildingStatusOf(b).IdleReason} out {b.OutputCount}")));
        Assert.Equal(1, sim.ShipsLaunched);
    }
}
