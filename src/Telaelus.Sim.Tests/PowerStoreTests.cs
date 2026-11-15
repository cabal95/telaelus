namespace Telaelus.Sim.Tests;

/// <summary>The day and night clock, the Solar Array, Battery Banks, and the Fission Reactor.</summary>
public class PowerStoreTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    private static CapacityReading Power(Simulation sim)
    {
        return sim.Capacity(CapacityType.Power);
    }

    [Fact]
    public void TheClock_StartsMidMorning_AdvancesAndWraps()
    {
        Simulation sim = EmptyMap();
        Assert.Equal(0.3f, sim.TimeOfDay, 4);

        SimTestHelpers.Seconds(sim, 60.0);
        Assert.Equal(0.4f, sim.TimeOfDay, 3);

        sim.Debug.SetTimeOfDay(0.95f);
        SimTestHelpers.Seconds(sim, 60.0);
        Assert.Equal(0.05f, sim.TimeOfDay, 3);
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.1f, 0.0f)]
    [InlineData(0.2f, 0.5f)]
    [InlineData(0.5f, 1.0f)]
    [InlineData(0.8f, 0.5f)]
    [InlineData(0.9f, 0.0f)]
    public void Daylight_FollowsTheClock(float timeOfDay, float daylight)
    {
        Assert.Equal(daylight, Simulation.DaylightAt(timeOfDay), 3);
    }

    [Fact]
    public void TheSolarArray_SuppliesByDaylight_WithNoFuel()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTimeOfDay(0.5f);
        Building array = sim.Debug.PlaceBuilding(BuildingType.SolarArray, 48, 4, Facing.North);
        Assert.True(array.Online);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(60f, Power(sim).Supply, 3);

        sim.Debug.SetTimeOfDay(0.8f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(30f, Power(sim).Supply, 1);

        sim.Debug.SetTimeOfDay(0.9f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(0f, Power(sim).Supply);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Night: supplies nothing"), sim.BuildingStatusOf(array));
    }

    [Fact]
    public void ABatteryBank_ChargesFromASurplus_UpToItsRateAndCapacity()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 10f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        Building bank = sim.Debug.PlaceBuilding(BuildingType.BatteryBank, 48, 4, Facing.North);
        Assert.True(bank.Online);

        // 100 Power spare, but a bank takes 50 a second.
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(500f, bank.Charge, 0);
        Assert.Equal(50f, Power(sim).StoreFlow, 1);
        Assert.Equal(6000f, Power(sim).StoreCapacity);
        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(bank));

        bank.Charge = 5990f;
        SimTestHelpers.Seconds(sim, 1.0);
        Assert.Equal(6000f, bank.Charge, 1);
        Assert.Equal(6000f, Power(sim).Stored, 1);
    }

    [Fact]
    public void ABatteryBank_CoversAShortage_UntilItRunsDry()
    {
        Simulation sim = EmptyMap();
        Building bank = sim.Debug.PlaceBuilding(BuildingType.BatteryBank, 48, 4, Facing.North);
        bank.Charge = 100f;
        sim.Debug.PlaceBuilding(BuildingType.DrillRig, 44, 4, Facing.North);

        // A Drill Rig draws 10 Power: the bank gives it, so the network stays Normal.
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(CapacityState.Normal, Power(sim).State);
        Assert.Equal(10f, Power(sim).Supply, 1);
        Assert.Equal(-10f, Power(sim).StoreFlow, 1);

        SimTestHelpers.Seconds(sim, 10.1);
        Assert.Equal(0f, bank.Charge, 1);
        Assert.Equal(CapacityState.Blackout, Power(sim).State);
    }

    [Fact]
    public void ABatteryBank_GivesAtMostItsRate()
    {
        Simulation sim = EmptyMap();
        Building bank = sim.Debug.PlaceBuilding(BuildingType.BatteryBank, 48, 4, Facing.North);
        bank.Charge = 1000f;
        // An Aluminum Smelter draws 60 Power; one bank gives 50, a 20% brownout.
        sim.Debug.PlaceBuilding(BuildingType.AluminumSmelter, 44, 2, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(50f, Power(sim).Supply, 1);
        Assert.Equal(CapacityState.Brownout, Power(sim).State);
    }

    [Fact]
    public void ABatteryBank_SwitchedOff_NeitherChargesNorDischarges_AndKeepsItsCharge()
    {
        Simulation sim = EmptyMap();
        Building bank = sim.Debug.PlaceBuilding(BuildingType.BatteryBank, 48, 4, Facing.North);
        bank.Charge = 300f;
        sim.Debug.PlaceBuilding(BuildingType.DrillRig, 44, 4, Facing.North);
        Assert.True(sim.SetBuildingEnabled(bank.Id, false));

        SimTestHelpers.Seconds(sim, 5.0);

        Assert.Equal(300f, bank.Charge);
        Assert.Equal(CapacityState.Blackout, Power(sim).State);
        Assert.Equal(300f, Power(sim).Stored);
    }

    /// <summary>The empty map with two Water Pumps on a pond at map cell (13, 4): 8 Water.</summary>
    private static Simulation Watered()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 14, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 54, 14, Facing.North);
        return sim;
    }

    [Fact]
    public void TheFissionReactor_BurnsAFuelRodEvery300Seconds_For400Power()
    {
        Simulation sim = Watered();
        sim.Debug.SeedStockpile(ResourceType.FuelRod, 2f);
        Building reactor = sim.Debug.PlaceBuilding(BuildingType.FissionReactor, 46, 4, Facing.North);
        Assert.True(reactor.Online);

        SimTestHelpers.Ticks(sim, 2);
        Assert.Equal(400f, Power(sim).Supply, 1);
        Assert.Equal(8f, sim.Capacity(CapacityType.Water).Demand);
        Assert.Equal(ResourceType.FuelRod, reactor.Burning);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.FuelRod));

        SimTestHelpers.Seconds(sim, 299.0);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.FuelRod));
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.FuelRod));
    }

    [Fact]
    public void TheFissionReactor_SuppliesLess_InAWaterBrownout()
    {
        Simulation sim = Watered();
        sim.Debug.SeedStockpile(ResourceType.FuelRod, 1f);
        sim.Debug.PlaceBuilding(BuildingType.FissionReactor, 46, 4, Facing.North);
        // A Refinery's 2 Water makes 10 against 8: 25% over, so Water runs at half speed.
        sim.Debug.PlaceBuilding(BuildingType.Refinery, 53, 4, Facing.North);

        SimTestHelpers.Ticks(sim, 3);

        Assert.Equal(CapacityState.Brownout, sim.Capacity(CapacityType.Water).State);
        Assert.Equal(200f, Power(sim).Supply, 1);
    }

    [Fact]
    public void FuelRods_BurnOnlyInTheReactor_AndTheReactorBurnsNothingElse()
    {
        Simulation sim = Watered();
        sim.Debug.SeedStockpile(ResourceType.FuelRod, 5f);
        sim.Debug.SeedStockpile(ResourceType.IronOre, 5f);
        Building burner = sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 40, 4, Facing.North);
        Assert.False(sim.SetFuelChoice(smelter.Id, ResourceType.FuelRod));
        SimTestHelpers.Ticks(sim, 2);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(burner));
        Assert.Null(smelter.Burning);
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.FuelRod));

        sim.Debug.RemoveBuilding(44, 0);
        sim.Debug.RemoveBuilding(40, 4);
        sim.Debug.SeedStockpile(ResourceType.FuelRod, -5f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 5f);
        Building reactor = sim.Debug.PlaceBuilding(BuildingType.FissionReactor, 46, 4, Facing.North);
        Assert.False(sim.SetFuelChoice(reactor.Id, ResourceType.Coal));
        SimTestHelpers.Ticks(sim, 2);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(reactor));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Coal));
    }
}
