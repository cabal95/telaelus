using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class LevelTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    private static void Complete(Simulation sim, ResearchProject project)
    {
        sim.Debug.SetResearchProgress(project, ResearchDefs.For(project).Work);
    }

    /// <summary>Completes the project that unlocks a tier.</summary>
    private static void UnlockTier(Simulation sim, int tier)
    {
        Complete(sim, ResearchDefs.All.First(project => ResearchDefs.For(project).Unlocks == tier));
    }

    /// <summary>A Smelter online by the HQ, with Tier 2 and Steelmaking done, so it can become a Steel Mill.</summary>
    private static (Simulation Sim, Building Smelter) SmelterReadyToUpgrade()
    {
        Simulation sim = EmptyMap();
        UnlockTier(sim, 2);
        Complete(sim, ResearchProject.Steelmaking);
        Building smelter = SimTestHelpers.PlaceOnline(sim, BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);
        return (sim, smelter);
    }

    private static float Amount(IEnumerable<ResourceAmount> amounts, ResourceType type)
    {
        return amounts.Where(amount => amount.Type == type).Sum(amount => amount.Amount);
    }

    [Theory]
    [InlineData(BuildingType.Mine, "Mine", 1, "Drill Rig", 2, "Deep Mine", 3)]
    [InlineData(BuildingType.LumberCamp, "Lumber Camp", 1, "Lumber Mill", 2, "Advanced Lumber Mill", 3)]
    [InlineData(BuildingType.WaterPump, "Water Pump", 1, "Pumping Station", 2, "Advanced Pumping Station", 3)]
    [InlineData(BuildingType.SandCollector, "Sand Collector", 1, "Sand Sifter", 2, "Sand Dredge", 3)]
    [InlineData(BuildingType.OilWell, "Oil Well", 2, "Oil Rig", 2, "Deep Well", 3)]
    [InlineData(BuildingType.Smelter, "Smelter", 1, "Steel Mill", 2, "Electric Smelter", 2)]
    [InlineData(BuildingType.Kiln, "Charcoal Kiln", 1, "Cement Kiln", 1, "Tile Kiln", 3)]
    public void EachFamily_HasThreeNamedLevels_OnA4x4Footprint(BuildingType type, string name1, int tier1, string name2, int tier2, string name3, int tier3)
    {
        Assert.Equal(3, BuildingDefs.Levels(type));
        (string Name, int Tier)[] expected =
        {
            (name1, tier1),
            (name2, tier2),
            (name3, tier3)
        };
        for (int level = 1; level <= 3; level++)
        {
            BuildingDef def = BuildingDefs.For(type, level);
            Assert.Equal(expected[level - 1].Name, def.Name);
            Assert.Equal(expected[level - 1].Tier, def.Tier);
            Assert.Equal((4, 4), def.Footprint(Facing.North));
            Assert.Equal((4, 4), def.Footprint(Facing.East));
            Assert.Equal(level == 1, ResearchDefs.UnlockingLevel(type, level) is null);
        }
    }

    [Fact]
    public void SmelterAndKilnRecipes_GrowByLevel_KeepingEachIndex()
    {
        Assert.Equal(new[] { ResourceType.Iron, ResourceType.Copper }, Outputs(BuildingType.Smelter, 1));
        Assert.Equal(new[] { ResourceType.Iron, ResourceType.Copper, ResourceType.Steel }, Outputs(BuildingType.Smelter, 2));
        Assert.Equal(new[] { ResourceType.Iron, ResourceType.Copper, ResourceType.Steel, ResourceType.Aluminum, ResourceType.Silicon, ResourceType.Titanium }, Outputs(BuildingType.Smelter, 3));
        Assert.Equal(new[] { ResourceType.Charcoal }, Outputs(BuildingType.Kiln, 1));
        Assert.Equal(new[] { ResourceType.Charcoal, ResourceType.Cement }, Outputs(BuildingType.Kiln, 2));
        Assert.Equal(new[] { ResourceType.Charcoal, ResourceType.Cement, ResourceType.CeramicTile }, Outputs(BuildingType.Kiln, 3));

        static ResourceType[] Outputs(BuildingType type, int level)
        {
            return Recipes.For(type, level).Select(recipe => recipe.Outputs[0].Type).ToArray();
        }
    }

    [Fact]
    public void AHigherLevel_IsLockedUntilItsProjectIsDone_EvenWithItsTierOpen()
    {
        Simulation sim = EmptyMap();
        UnlockTier(sim, 2);
        Assert.True(sim.IsUnlocked(BuildingType.Mine));
        Assert.False(sim.IsUnlocked(BuildingType.Mine, 2));
        Assert.False(sim.IsUnlocked(BuildingType.Kiln, 2));

        Complete(sim, ResearchProject.PoweredMining);
        Complete(sim, ResearchProject.Cement);

        Assert.True(sim.IsUnlocked(BuildingType.Mine, 2));
        Assert.True(sim.IsUnlocked(BuildingType.Kiln, 2));
        Assert.False(sim.IsUnlocked(BuildingType.Mine, 3));
    }

    [Fact]
    public void ALevelAboveTheOpenTier_StaysLockedAfterItsProject()
    {
        Simulation sim = EmptyMap();
        Complete(sim, ResearchProject.PoweredMining);

        Assert.False(sim.IsUnlocked(BuildingType.Mine, 2));
    }

    [Fact]
    public void TryPlaceBuilding_AtALevel_PaysThatLevelsCost_AndRecordsIt()
    {
        Simulation sim = EmptyMap();
        UnlockTier(sim, 2);
        BuildingDef drillRig = BuildingDefs.For(BuildingType.Mine, 2);
        foreach (ResourceAmount cost in drillRig.Cost)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.False(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North, 2));

        Complete(sim, ResearchProject.PoweredMining);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North, 2));

        Building rig = sim.BuildingAt(SimTestHelpers.ClearX, SimTestHelpers.ClearZ)!;
        Assert.Equal(2, rig.Level);
        Assert.Same(drillRig, rig.Def);
        Assert.Equal(drillRig.Cost, rig.Paid);
        foreach (ResourceAmount cost in drillRig.Cost)
        {
            Assert.Equal(0f, SimTestHelpers.Stock(sim, cost.Type));
        }
    }

    [Fact]
    public void UpgradePrice_IsTheNextLevelsCostLessThisOnes_PerResource()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        IReadOnlyList<ResourceAmount> price = sim.UpgradePrice(smelter);

        // Smelter 40 Stone, 15 Wood; Steel Mill 60 Stone, 15 Wood, 40 Iron.
        Assert.Equal(2, price.Count);
        Assert.Equal(20f, Amount(price, ResourceType.Stone));
        Assert.Equal(40f, Amount(price, ResourceType.Iron));

        Building top = sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX + 6, SimTestHelpers.ClearZ, Facing.North, 3);
        Assert.Empty(sim.UpgradePrice(top));
    }

    [Fact]
    public void EachLevelsCost_IncludesTheLevelBelows()
    {
        // So building low and upgrading costs the same as building high directly.
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels().Where(entry => entry.Level > 1))
        {
            BuildingDef below = BuildingDefs.For(type, level - 1);
            BuildingDef def = BuildingDefs.For(type, level);
            Assert.All(below.Cost, cost => Assert.True(Amount(def.Cost, cost.Type) >= cost.Amount,
                $"{def.Name} costs less {cost.Type} than {below.Name}"));
        }
    }

    [Fact]
    public void BuildingAMine_AndUpgradingTwice_CostsWhatADeepMineCosts()
    {
        Simulation sim = EmptyMap();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        Building rig = sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX + 6, SimTestHelpers.ClearZ, Facing.North, 2);

        var paid = new List<ResourceAmount>(BuildingDefs.For(BuildingType.Mine, 1).Cost);
        paid.AddRange(sim.UpgradePrice(mine));
        paid.AddRange(sim.UpgradePrice(rig));

        Assert.Equal(25f, Amount(paid, ResourceType.Wood));
        Assert.Equal(20f, Amount(paid, ResourceType.Stone));
        Assert.Equal(30f, Amount(paid, ResourceType.Iron));
        Assert.Equal(60f, Amount(paid, ResourceType.Steel));
        Assert.Equal(30f, Amount(paid, ResourceType.Concrete));
    }

    [Fact]
    public void TryUpgrade_IsRefusedWhileLocked_OrShort()
    {
        Simulation sim = EmptyMap();
        UnlockTier(sim, 2);
        Building smelter = SimTestHelpers.PlaceOnline(sim, BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ);
        foreach (ResourceAmount cost in sim.UpgradePrice(smelter))
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }

        // Locked: the price is held, but Steelmaking is not done.
        Assert.False(sim.TryUpgrade(smelter.Id));
        Assert.Equal(1, smelter.Level);

        // Short: unlocked, with one unit of the price missing.
        Complete(sim, ResearchProject.Steelmaking);
        ResourceAmount first = sim.UpgradePrice(smelter)[0];
        sim.Debug.SeedStockpile(first.Type, -1f);
        Assert.False(sim.TryUpgrade(smelter.Id));
        Assert.Equal(1, smelter.Level);
        Assert.Equal(first.Amount - 1f, SimTestHelpers.Stock(sim, first.Type));

        // With the unit back, it goes up.
        sim.Debug.SeedStockpile(first.Type, 1f);
        Assert.True(sim.TryUpgrade(smelter.Id));
    }

    [Fact]
    public void TryUpgrade_PaysThePrice_AndKeepsTheQueueAndChoices()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        IReadOnlyList<ResourceAmount> price = sim.UpgradePrice(smelter);
        foreach (ResourceAmount cost in price)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.QueueCraft(smelter.Id, 1, 5));
        smelter.InputChoices[(1, 0)] = ResourceType.CopperOre;
        int id = smelter.Id;

        Assert.True(sim.TryUpgrade(id));

        Assert.Equal(id, smelter.Id);
        Assert.Equal(2, smelter.Level);
        Assert.Equal("Steel Mill", smelter.Def.Name);
        Assert.Equal(3, smelter.Recipes.Count);
        foreach (ResourceAmount cost in price)
        {
            Assert.Equal(0f, SimTestHelpers.Stock(sim, cost.Type));
        }
        Assert.Single(smelter.Queue);
        Assert.Equal(1, smelter.Queue[0].RecipeIndex);
        Assert.Equal(ResourceType.CopperOre, smelter.InputChoices[(1, 0)]);
        Assert.Same(smelter, sim.BuildingAt(SimTestHelpers.ClearX, SimTestHelpers.ClearZ));
    }

    [Fact]
    public void TryUpgrade_KeepsAHold()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        foreach (ResourceAmount cost in sim.UpgradePrice(smelter))
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.SetHold(smelter.Id, 0, 25));

        Assert.True(sim.TryUpgrade(smelter.Id));

        Assert.NotNull(smelter.Hold);
        Assert.Equal(0, smelter.Hold!.RecipeIndex);
        Assert.Equal(25, smelter.Hold.Target);
    }

    [Fact]
    public void UpgradingABurner_ToAPoweredLevel_PutsOutItsFire_AndTheRunningCycleFinishes()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        SimTestHelpers.Burners(sim, 1);
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 50f);
        SimTestHelpers.Order(sim, smelter, 0, 1);
        SimTestHelpers.Seconds(sim, 1.0);
        Assert.True(smelter.Crafting);
        Assert.NotNull(smelter.Burning);

        foreach (ResourceAmount cost in sim.UpgradePrice(smelter))
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.TryUpgrade(smelter.Id));

        Assert.False(smelter.Def.BurnsFuel);
        Assert.Null(smelter.Burning);
        Assert.True(smelter.Crafting);

        SimTestHelpers.Seconds(sim, 60.0);
        Assert.False(smelter.Crafting);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.Iron));
        Assert.Null(smelter.Burning);
    }

    [Fact]
    public void DestroyRefund_IsHalfOfEverythingPaid_PlacingAndUpgrading()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        IReadOnlyList<ResourceAmount> price = sim.UpgradePrice(smelter);
        foreach (ResourceAmount cost in price)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.TryUpgrade(smelter.Id));

        IReadOnlyList<ResourceAmount> refund = sim.DestroyRefund(smelter);
        IEnumerable<ResourceAmount> paid = BuildingDefs.For(BuildingType.Smelter, 1).Cost.Concat(price);
        foreach (ResourceType type in paid.Select(amount => amount.Type).Distinct())
        {
            Assert.Equal(System.MathF.Ceiling(Amount(paid, type) * 0.5f), Amount(refund, type));
        }
        Assert.Equal(paid.Select(amount => amount.Type).Distinct().Count(), refund.Count);
    }

    [Fact]
    public void ASave_KeepsEachBuildingsLevelAndWhatItPaid()
    {
        (Simulation sim, Building smelter) = SmelterReadyToUpgrade();
        foreach (ResourceAmount cost in sim.UpgradePrice(smelter))
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.TryUpgrade(smelter.Id));
        Building rig = sim.Debug.PlaceBuilding(BuildingType.Mine, SimTestHelpers.ClearX + 6, SimTestHelpers.ClearZ, Facing.North, 2);

        Simulation loaded = Simulation.FromSave(sim.Save());

        foreach (Building before in new[] { smelter, rig })
        {
            Building after = loaded.BuildingById(before.Id)!;
            Assert.Equal(before.Level, after.Level);
            Assert.Same(before.Def, after.Def);
            Assert.Equal(before.Paid, after.Paid);
            Assert.Equal(before.Recipes.Count, after.Recipes.Count);
        }
    }
}
