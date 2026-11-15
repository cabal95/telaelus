
namespace Telaelus.Sim.Tests;

public class CostTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void NothingIsUnlocked_AtTheStart()
    {
        Simulation sim = EmptyMap();

        foreach (BuildingType type in System.Enum.GetValues<BuildingType>())
        {
            Assert.False(sim.IsUnlocked(type), type.ToString());
        }
    }

    [Fact]
    public void Buildings_UnlockOnceTheirWholeCostIsHeld()
    {
        Simulation sim = EmptyMap();

        // Some of every material is not enough.
        sim.Debug.SeedStockpile(ResourceType.Wood, 14f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 14f);
        Assert.False(sim.IsUnlocked(BuildingType.LumberCamp));
        Assert.False(sim.IsUnlocked(BuildingType.Mine));

        sim.Debug.SeedStockpile(ResourceType.Wood, 1f);
        Assert.True(sim.IsUnlocked(BuildingType.LumberCamp));
        Assert.False(sim.IsUnlocked(BuildingType.Mine));

        sim.Debug.SeedStockpile(ResourceType.Wood, 10f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 1f);
        Assert.True(sim.IsUnlocked(BuildingType.Mine));
        Assert.False(sim.IsUnlocked(BuildingType.Smelter));
        Assert.False(sim.IsUnlocked(BuildingType.OilWell));
    }

    [Fact]
    public void HandGathering_UnlocksAtTheUnitThatCompletesTheCost()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.PlaceField(ResourceType.Wood, 8, 7);
        sim.Debug.SeedStockpile(ResourceType.Wood, 13f);
        SimTestHelpers.Teleport(sim, 33.5f, 33f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        SimTestHelpers.Seconds(sim, 3.1);
        Assert.False(sim.IsUnlocked(BuildingType.LumberCamp));

        SimTestHelpers.Seconds(sim, 3.0);
        Assert.Equal(15f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.True(sim.IsUnlocked(BuildingType.LumberCamp));
    }

    [Fact]
    public void Unlocking_IsKeptWhenTheStockRunsOut()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 15f);
        Assert.True(sim.TryPlaceBuilding(BuildingType.LumberCamp, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.True(sim.IsUnlocked(BuildingType.LumberCamp));
    }

    [Fact]
    public void Placing_DeductsTheCost()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 30f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 20f);

        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Single(SimTestHelpers.Placed(sim));
    }

    [Fact]
    public void Placing_Unaffordable_IsRefusedAndDeductsNothing()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 30f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 15f);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));

        // Still unlocked, but the first Mine spent the Stone.
        Assert.True(sim.IsUnlocked(BuildingType.Mine));
        Assert.False(sim.CanAfford(BuildingType.Mine));
        Assert.False(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX + 4, SimTestHelpers.ClearZ, Facing.North));

        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Single(SimTestHelpers.Placed(sim));
    }

    [Fact]
    public void Placing_Locked_IsRefused()
    {
        Simulation sim = EmptyMap();

        Assert.False(sim.TryPlaceBuilding(BuildingType.LumberCamp, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
        Assert.Empty(SimTestHelpers.Placed(sim));
    }

    [Fact]
    public void Placing_OnABlockedSpot_DeductsNothing()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 30f);
        sim.Debug.PlaceBuilding(BuildingType.LumberCamp, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        Assert.False(sim.TryPlaceBuilding(BuildingType.LumberCamp, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
        Assert.Equal(30f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void DebugPlacement_IsFree()
    {
        Simulation sim = EmptyMap();

        sim.Debug.PlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);

        Assert.Single(SimTestHelpers.Placed(sim));
        Assert.Empty(sim.Stockpile);
    }

    [Fact]
    public void EveryBuildableBuilding_HasACost_AndEveryDeposit_AGatherTime()
    {
        foreach (BuildingType type in System.Enum.GetValues<BuildingType>())
        {
            BuildingDef def = BuildingDefs.For(type);
            Assert.Equal(def.Buildable, def.Cost.Count > 0);
        }
        foreach (ResourceType type in System.Enum.GetValues<ResourceType>())
        {
            ResourceDef def = ResourceDefs.For(type);
            Assert.Equal(def.Kind == ResourceKind.Deposit, def.GatherSeconds is not null);
        }
    }
}
