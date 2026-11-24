namespace Telaelus.Sim.Tests;

public class CapacityTests
{
    /// <summary>
    /// The empty map with a pond at map cell (12, 4) (fine x 48..51, z 16..19) and Sand at (13, 4)
    /// (fine x 52..55, z 16..19), both just south-west of the HQ and in its coverage.
    /// </summary>
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetTerrain(12, 4, TerrainType.ShallowWater);
        sim.Debug.SetTerrain(13, 4, TerrainType.Sand);
        return sim;
    }

    private static CapacityReading Water(Simulation sim)
    {
        return sim.Capacity(CapacityType.Water);
    }

    private static CapacityReading Sand(Simulation sim)
    {
        return sim.Capacity(CapacityType.Sand);
    }

    [Fact]
    public void WaterSupplyAndDemand_CountOnlyOnlineSwitchedOnBuildings()
    {
        Simulation sim = EmptyMap();
        Building pump = sim.Debug.PlaceBuilding(BuildingType.WaterPump, 48, 14, Facing.North);
        Building refinery = sim.Debug.PlaceBuilding(BuildingType.Refinery, 46, 8, Facing.North);
        sim.Debug.SetTerrain(2, 10, TerrainType.ShallowWater);
        Building farPump = sim.Debug.PlaceBuilding(BuildingType.WaterPump, 8, 38, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.True(pump.Online);
        Assert.True(refinery.Online);
        Assert.False(farPump.Online);
        Assert.Equal(new CapacityReading(4f, 2f, 1f, CapacityState.Normal), Water(sim));

        sim.SetBuildingEnabled(pump.Id, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new CapacityReading(0f, 2f, 0f, CapacityState.Blackout), Water(sim));
    }

    [Fact]
    public void TheRefinery_StopsWithoutWater()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 5f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        Building refinery = sim.Debug.PlaceBuilding(BuildingType.Refinery, 46, 8, Facing.North);
        SimTestHelpers.Order(sim, refinery);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 52, 12, Facing.North);
        SimTestHelpers.Seconds(sim, 10.0);

        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Water"), sim.BuildingStatusOf(refinery));
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.Kerosene));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.CrudeOil));

        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 48, 14, Facing.North);
        SimTestHelpers.Seconds(sim, 8.5);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.Kerosene));
    }

    [Fact]
    public void AConsumerOfTwoTypes_RunsAtTheSlower()
    {
        // Power plenty (four generators, 140 against 125); Water 10 against 8, a quarter over: 0.5.
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.CrudeOil, 10f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        var refineries = new Building[5];
        for (int i = 0; i < 5; i++)
        {
            refineries[i] = sim.Debug.PlaceBuilding(BuildingType.Refinery, 44 + 2 * i, 8, Facing.North);
            SimTestHelpers.Order(sim, refineries[i]);
        }
        SimTestHelpers.Burners(sim, 4);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 48, 14, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 50, 14, Facing.North);
        Assert.All(SimTestHelpers.Placed(sim), b => Assert.True(b.Online));
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(CapacityState.Normal, sim.Capacity(CapacityType.Power).State);
        Assert.Equal(new CapacityReading(8f, 10f, 0.5f, CapacityState.Brownout), Water(sim));

        // Two seconds of work at half speed is one second of progress.
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.Equal(1f, refineries[0].Progress, 1);
    }

    [Fact]
    public void APoweredProvider_SuppliesAtItsPowerSpeed_AndNothingInAPowerBlackout()
    {
        // Four Refineries (100) and a Pumping Station (10) on three Burner Generators (105): 110 / 105,
        // a 4.8% overload, so power runs at 0.905.
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        for (int i = 0; i < 4; i++)
        {
            sim.Debug.PlaceBuilding(BuildingType.Refinery, 44 + 2 * i, 8, Facing.North);
        }
        Building[] burners = SimTestHelpers.Burners(sim, 3);
        sim.Debug.PlaceBuilding(BuildingType.PumpingStation, 47, 13, Facing.North);
        Assert.All(SimTestHelpers.Placed(sim), b => Assert.True(b.Online));
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(1f - 2f * 5f / 105f, sim.Capacity(CapacityType.Power).Speed, 3);
        Assert.Equal(16f * (1f - 2f * 5f / 105f), Water(sim).Supply, 3);
        Assert.Equal(8f, Water(sim).Demand);

        foreach (Building burner in burners)
        {
            sim.SetBuildingEnabled(burner.Id, false);
        }
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(CapacityState.Blackout, sim.Capacity(CapacityType.Power).State);
        Assert.Equal(0f, Water(sim).Supply);
        Assert.Equal(CapacityState.Blackout, Water(sim).State);
    }

    [Fact]
    public void WaterProviders_GoBesideWaterByAnEdge()
    {
        Simulation sim = EmptyMap();
        // Map cell (5, 5) is a pond: fine cells x 20..23, z 20..23.
        sim.Debug.SetTerrain(5, 5, TerrainType.ShallowWater);

        Assert.True(sim.CanPlace(BuildingType.WaterPump, 18, 20, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.WaterPump, 21, 24, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.PumpingStation, 17, 21, Facing.North));
        // Corner to corner is not beside, and a cell away is not either.
        Assert.False(sim.CanPlace(BuildingType.WaterPump, 18, 24, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.WaterPump, 17, 20, Facing.North));
        // Nor on the water itself.
        Assert.False(sim.CanPlace(BuildingType.WaterPump, 20, 20, Facing.North));

        // Beside a river channel counts too: a river through map cell (10, 5), channel x 41..42.
        sim.Debug.SetFeature(10, 4, MapFeature.River, Facing.South);
        sim.Debug.SetFeature(10, 5, MapFeature.River, Facing.South);
        sim.Debug.SetFeature(10, 6, MapFeature.River, Facing.South);
        Assert.True(sim.IsRiverChannel(41, 21));
        Assert.True(sim.CanPlace(BuildingType.WaterPump, 39, 20, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.WaterPump, 30, 30, Facing.North));
    }

    [Fact]
    public void SandProviders_GoWhollyOnSand()
    {
        Simulation sim = EmptyMap();
        // Map cells (5, 5) and (6, 5) are Sand: fine cells x 20..27, z 20..23.
        sim.Debug.SetTerrain(5, 5, TerrainType.Sand);
        sim.Debug.SetTerrain(6, 5, TerrainType.Sand);

        Assert.True(sim.CanPlace(BuildingType.SandCollector, 20, 20, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.SandDredge, 22, 20, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.SandCollector, 19, 20, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.SandDredge, 22, 22, Facing.North));
    }

    [Fact]
    public void TheGlassworks_MakesGlassFromSandAndFuel()
    {
        Simulation sim = EmptyMap();
        Building glassworks = sim.Debug.PlaceBuilding(BuildingType.Glassworks, 44, 10, Facing.North);
        SimTestHelpers.Order(sim, glassworks);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(glassworks.Online);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Sand"), sim.BuildingStatusOf(glassworks));

        Building collector = sim.Debug.PlaceBuilding(BuildingType.SandCollector, 52, 16, Facing.North);
        Assert.True(collector.Online);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new CapacityReading(2f, 2f, 1f, CapacityState.Normal), Sand(sim));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(glassworks));

        // One Coal burns 40 s: three 12 s cycles finish, the fourth starts.
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        SimTestHelpers.Seconds(sim, 37.0);
        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Glass));

        // Sand and Water are never stored.
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Sand));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Water));
    }
}
