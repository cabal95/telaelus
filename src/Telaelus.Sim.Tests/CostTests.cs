
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
    public void Unlocking_IsKeptWhenTheStockRunsOut()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 10f);
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
