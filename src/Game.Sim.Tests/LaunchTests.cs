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
        sim.Debug.SeedStockpile(ResourceType.LiquidOxygen, Simulation.LaunchLiquidOxygenCost);
        sim.Debug.SeedStockpile(ResourceType.Iron, Simulation.LaunchIronCost - 1f);
        Assert.False(sim.CanLaunchShip());

        sim.Debug.SeedStockpile(ResourceType.Iron, 1f);
        sim.Debug.SeedStockpile(ResourceType.LiquidOxygen, -1f);
        Assert.False(sim.CanLaunchShip());
        Assert.False(sim.TryLaunchShip());
        Assert.Equal(0, sim.ShipsLaunched);
    }

    [Fact]
    public void Launch_ConsumesTheCostAndCountsTheShip()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.RocketFuel, 600f);
        sim.Debug.SeedStockpile(ResourceType.LiquidOxygen, 250f);
        sim.Debug.SeedStockpile(ResourceType.Iron, 150f);

        Assert.True(sim.CanLaunchShip());
        Assert.True(sim.TryLaunchShip());

        Assert.Equal(1, sim.ShipsLaunched);
        Assert.Equal(100f, SimTestHelpers.Stock(sim, ResourceType.RocketFuel));
        Assert.Equal(50f, SimTestHelpers.Stock(sim, ResourceType.LiquidOxygen));
        Assert.Equal(50f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void EndToEnd_FieldsReachALaunch_ThroughTheRefiningChain_HaulingByHand()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();

        // Fields west of the HQ: Iron Ore, Coal, and two Crude Oil seeps; a pond for the pumps.
        sim.Debug.PlaceField(ResourceType.IronOre, 8, 6, 300f);
        sim.Debug.PlaceField(ResourceType.Coal, 8, 9, 1600f);
        sim.Debug.PlaceField(ResourceType.CrudeOil, 11, 9, 1000f);
        sim.Debug.PlaceField(ResourceType.CrudeOil, 11, 12, 1000f);
        sim.Debug.SetTerrain(14, 5, TerrainType.ShallowWater);
        sim.Debug.SetTerrain(14, 6, TerrainType.ShallowWater);

        // Collectors: a Mine on the Iron Ore, two on the Coal, an Oil Well at each seep. The chain:
        // a Smelter for Iron; two Refineries for Kerosene; three Electrolyzers (two on Hydrogen, one
        // on Liquid Oxygen); two Fuel Plants for Rocket Fuel; three Burner Generators on Coal for
        // their 240 Power; four Water Pumps for their 16 Water; three Tank Farms for the fluids.
        // Collectors never deliver on their own, so the player walks round every 20 seconds.
        (BuildingType Type, int X, int Z)[] placements =
        {
            (BuildingType.Mine, 30, 24),
            (BuildingType.Mine, 30, 36),
            (BuildingType.Mine, 36, 36),
            (BuildingType.OilWell, 42, 36),
            (BuildingType.OilWell, 42, 48),
            (BuildingType.Smelter, 20, 14),
            (BuildingType.Refinery, 36, 13),
            (BuildingType.Refinery, 38, 13),
            (BuildingType.FuelPlant, 40, 13),
            (BuildingType.FuelPlant, 44, 13),
            (BuildingType.Electrolyzer, 36, 19),
            (BuildingType.Electrolyzer, 40, 19),
            (BuildingType.Electrolyzer, 44, 19),
            (BuildingType.BurnerGenerator, 48, 13),
            (BuildingType.BurnerGenerator, 50, 13),
            (BuildingType.BurnerGenerator, 48, 16),
            (BuildingType.WaterPump, 54, 20),
            (BuildingType.WaterPump, 54, 22),
            (BuildingType.WaterPump, 54, 24),
            (BuildingType.WaterPump, 54, 26),
            (BuildingType.TankFarm, 24, 30),
            (BuildingType.TankFarm, 27, 30),
            (BuildingType.TankFarm, 20, 30)
        };
        foreach ((BuildingType type, int x, int z) in placements)
        {
            sim.Debug.PlaceBuilding(type, x, z, Facing.North);
        }
        Building oxygen = sim.Buildings.Last(b => b.Type == BuildingType.Electrolyzer);
        Assert.True(sim.SetRecipe(oxygen.Id, 1));
        // A chain of Small Hubs from the HQ along the open ground round the chain.
        foreach ((int x, int z) in new[] { (52, 18), (48, 22), (40, 24), (35, 18), (28, 17), (26, 25) })
        {
            sim.Debug.PlaceBuilding(BuildingType.SmallHub, x, z, Facing.North);
        }
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

        Assert.True(sim.TryLaunchShip(), $"no launch after {seconds} seconds of play: fuel {SimTestHelpers.Stock(sim, ResourceType.RocketFuel)}, oxygen {SimTestHelpers.Stock(sim, ResourceType.LiquidOxygen)}, iron {SimTestHelpers.Stock(sim, ResourceType.Iron)}, kerosene {SimTestHelpers.Stock(sim, ResourceType.Kerosene)}, hydrogen {SimTestHelpers.Stock(sim, ResourceType.Hydrogen)}, crude {SimTestHelpers.Stock(sim, ResourceType.CrudeOil)}, coal {SimTestHelpers.Stock(sim, ResourceType.Coal)}; "
            + string.Join("; ", SimTestHelpers.Placed(sim).Select(b => $"{b.Type} {b.Online} {sim.BuildingStatusOf(b).IdleReason} out {b.OutputCount}")));
        Assert.Equal(1, sim.ShipsLaunched);
    }
}
