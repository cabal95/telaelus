namespace Telaelus.Sim.Tests;

public class PowerTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    /// <summary>
    /// Five Refineries (25 each), two Drill Rigs (10 each), and three Burner Generators (35 each) on
    /// Coal, around one Small Hub linked back to the HQ, all online: 145 against 105.
    /// </summary>
    private static (Simulation Sim, Building[] Refineries, Building[] Rigs) PoweredBlock()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        var refineries = new Building[5];
        for (int i = 0; i < 5; i++)
        {
            refineries[i] = sim.Debug.PlaceBuilding(BuildingType.Refinery, 23 + 2 * i, 28, Facing.North);
        }
        Building[] rigs =
        {
            sim.Debug.PlaceBuilding(BuildingType.DrillRig, 23, 32, Facing.North),
            sim.Debug.PlaceBuilding(BuildingType.DrillRig, 26, 32, Facing.North)
        };
        SimTestHelpers.Burners(sim, 3, 38, 36);
        sim.Debug.PlaceBuilding(BuildingType.SmallHub, 29, 31, Facing.North);
        SimTestHelpers.Connect(sim, 29.5f, 31.5f);
        SimTestHelpers.Ticks(sim, 1);
        return (sim, refineries, rigs);
    }

    [Fact]
    public void Brownout_SlowsByTwiceTheOverload_AndPastAQuarterOverIsABlackout()
    {
        (Simulation sim, Building[] refineries, Building[] rigs) = PoweredBlock();
        Assert.All(SimTestHelpers.Placed(sim), b => Assert.True(b.Online));

        // Demand counts the idle Refineries (no Crude Oil) as well: 145 / 105.
        Assert.Equal(105f, sim.Capacity(CapacityType.Power).Supply);
        Assert.Equal(145f, sim.Capacity(CapacityType.Power).Demand);
        Assert.Equal(CapacityState.Blackout, sim.Capacity(CapacityType.Power).State);
        Assert.Equal(0f, sim.Capacity(CapacityType.Power).Speed);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Power"), sim.BuildingStatusOf(rigs[0]));

        sim.SetBuildingEnabled(refineries[0].Id, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(CapacityState.Brownout, sim.Capacity(CapacityType.Power).State);
        // 120 / 105 and then 110 / 105: speed is 1 less twice the overload.
        Assert.Equal(1f - 2f * 15f / 105f, sim.Capacity(CapacityType.Power).Speed, 3);

        sim.SetBuildingEnabled(rigs[0].Id, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(1f - 2f * 5f / 105f, sim.Capacity(CapacityType.Power).Speed, 3);

        sim.SetBuildingEnabled(refineries[1].Id, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(CapacityState.Normal, sim.Capacity(CapacityType.Power).State);
        Assert.Equal(1f, sim.Capacity(CapacityType.Power).Speed);
    }

    [Fact]
    public void OfflineBuildings_NeitherDrawNorSupply()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        Building rig = sim.Debug.PlaceBuilding(BuildingType.DrillRig, 20, 40, Facing.North);
        Building burner = sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 24, 40, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(0f, sim.Capacity(CapacityType.Power).Supply);
        Assert.Equal(0f, sim.Capacity(CapacityType.Power).Demand);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "No Power: offline"), sim.BuildingStatusOf(rig));
        // Offline it cannot reach storage for fuel either.
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Offline: supplies nothing"), sim.BuildingStatusOf(burner));
    }

    [Fact]
    public void ABurnerGenerator_BurnsWheneverItIsOn_BestFuelFirst_AndAUnitBurningIsSpent()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 2f);
        Building burner = sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 50, 14, Facing.North);
        Assert.True(burner.Online);
        SimTestHelpers.Ticks(sim, 1);

        // Nothing draws power, yet it burns: Best takes the Coal (40 s) before the Wood (10 s).
        Assert.Equal(ResourceType.Coal, burner.Burning);
        Assert.Equal(35f, sim.Capacity(CapacityType.Power).Supply);
        SimTestHelpers.Seconds(sim, 40.5);
        Assert.Equal(ResourceType.Wood, burner.Burning);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Wood));

        // Switched off, it stops burning.
        sim.SetBuildingEnabled(burner.Id, false);
        float left = burner.BurnLeft;
        SimTestHelpers.Seconds(sim, 5.0);
        Assert.Equal(left, burner.BurnLeft);
        Assert.Equal(0f, sim.Capacity(CapacityType.Power).Supply);

        // Destroyed, the half-burned unit is not returned.
        sim.TryDestroyBuilding(burner.Id);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void AFuelChoice_BurnsOnlyThatFuel()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 1f);
        Building burner = sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 50, 14, Facing.North);
        Assert.True(sim.SetFuelChoice(burner.Id, ResourceType.Wood));
        Assert.False(sim.SetFuelChoice(burner.Id, ResourceType.Iron));

        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(ResourceType.Wood, burner.Burning);
        Assert.Equal(ResourceType.Coal, sim.BestFuelAt(burner));

        // When the Wood burns out it waits rather than touching the Coal.
        SimTestHelpers.Seconds(sim, 11.0);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Coal));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(burner));
    }

    [Fact]
    public void TheSmelter_RunsOnlyWhileFuelBurns()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 5f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 48, 14, Facing.North);
        SimTestHelpers.Order(sim, smelter);
        Assert.True(smelter.Online);
        SimTestHelpers.Seconds(sim, 5.0);

        Assert.False(smelter.Crafting);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(smelter));

        // One Wood burns 10 s: the cycle gets 10 of its 16 s, then waits for fuel.
        sim.Debug.SeedStockpile(ResourceType.Wood, 1f);
        SimTestHelpers.Seconds(sim, 14.0);
        Assert.True(smelter.Crafting);
        Assert.Equal(10f, smelter.Progress, 1);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(smelter));

        sim.Debug.SeedStockpile(ResourceType.Wood, 1f);
        SimTestHelpers.Seconds(sim, 6.5);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void ADrillRig_GathersAtItsSpeed_ScaledByPower()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        sim.Debug.PlaceField(ResourceType.IronOre, 11, 4, 100f);
        Building rig = sim.Debug.PlaceBuilding(BuildingType.DrillRig, 49, 13, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 53, 13, Facing.North);
        Assert.True(rig.Online);

        // Iron Ore takes 6 s by hand; at speed 2 that is 3 s a unit: 3 units in 9.1 s.
        SimTestHelpers.Seconds(sim, 9.1);

        Assert.Equal(3f, rig.Output[ResourceType.IronOre]);
    }

    [Fact]
    public void AWaterWheel_GoesOnlyAcrossAStraightRiverCell()
    {
        Simulation sim = EmptyMap();
        // A river flowing South through map cells (4, 4) to (4, 6): (4, 5) is Straight North-South.
        for (int z = 4; z <= 6; z++)
        {
            sim.Debug.SetFeature(4, z, MapFeature.River, Facing.South);
        }
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(4, 5));

        // Map cell (4, 5) is fine cells x 16..19, z 20..23. Facing North the wheel is 4 wide across X.
        Assert.True(sim.CanPlace(BuildingType.WaterWheel, 16, 20, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.WaterWheel, 16, 22, Facing.South));
        Assert.False(sim.CanPlace(BuildingType.WaterWheel, 16, 20, Facing.East));
        Assert.False(sim.CanPlace(BuildingType.WaterWheel, 15, 20, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.WaterWheel, 16, 23, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.WaterWheel, 40, 40, Facing.North));

        Building wheel = sim.Debug.PlaceBuilding(BuildingType.WaterWheel, 16, 20, Facing.North);
        SimTestHelpers.Connect(sim, 18f, 21f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(wheel.Online);
        Assert.Equal(5f, sim.Capacity(CapacityType.Power).Supply);
    }
}
