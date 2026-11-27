using System;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class TierTests
{
    // Each tier's gateways (a type and level): built from the tier below.
    private static readonly (BuildingType Type, int Level)[] Gateways =
    {
        (BuildingType.Smelter, 2),
        (BuildingType.Refinery, 1),
        (BuildingType.OilWell, 1),
        (BuildingType.Mine, 2),
        (BuildingType.Fabricator, 1),
        (BuildingType.ElectronicsWorks, 1),
        (BuildingType.AssemblyPlant, 1)
    };

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

    private static void AssertUnlockedUpTo(Simulation sim, int tier)
    {
        Assert.Equal(tier, sim.UnlockedTier);
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            BuildingDef def = BuildingDefs.For(type);
            Assert.True((def.Buildable && def.Tier <= tier) == sim.IsUnlocked(type), $"{type} at unlocked tier {tier}");
        }
    }

    [Fact]
    public void Tier1_IsUnlockedFromTheStart_AndNothingAbove()
    {
        Simulation sim = EmptyMap();

        AssertUnlockedUpTo(sim, 1);
        Assert.True(sim.IsUnlocked(BuildingType.LumberCamp));
        Assert.False(sim.IsUnlocked(BuildingType.Hq));
        Assert.False(sim.IsUnlocked(BuildingType.Smelter, 2));
    }

    [Fact]
    public void HoldingATier2Cost_DoesNotUnlockIt()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 100f);

        Assert.True(sim.CanAfford(BuildingType.Smelter, 2));
        Assert.False(sim.IsUnlocked(BuildingType.Smelter, 2));
        Assert.False(sim.TryPlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North, 2));
        Assert.Empty(SimTestHelpers.Placed(sim));
    }

    [Fact]
    public void EachUnlockProject_RaisesTheUnlockedTierByOne()
    {
        Simulation sim = EmptyMap();

        Complete(sim, ResearchProject.SteelWorks);
        AssertUnlockedUpTo(sim, 2);
        Complete(sim, ResearchProject.Electronics);
        AssertUnlockedUpTo(sim, 3);
        Complete(sim, ResearchProject.Computers);
        AssertUnlockedUpTo(sim, 4);
    }

    [Fact]
    public void AfterSteelWorks_ARefineryCanBePlaced_ButASteelMillWaitsForSteelmaking()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 80f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 90f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 35f);
        Complete(sim, ResearchProject.SteelWorks);

        Assert.True(sim.TryPlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
        Assert.False(sim.IsUnlocked(BuildingType.Smelter, 2));
        Complete(sim, ResearchProject.Steelmaking);
        Assert.True(sim.TryPlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX + 6, SimTestHelpers.ClearZ, Facing.North, 2));
    }

    [Fact]
    public void AProjectInALockedTier_IsLocked_UntilItsTierUnlocks()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Steel, 100f);
        sim.Debug.SeedStockpile(ResourceType.Plastic, 100f);
        sim.Debug.SeedStockpile(ResourceType.Aluminum, 100f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 200f);
        Complete(sim, ResearchProject.MaterialTransfer);

        // Advanced Material Transfer's prerequisite is done, but it is tier 2.
        Assert.Equal(ResearchState.Locked, sim.StateOf(ResearchProject.AdvancedMaterialTransfer));
        Assert.Equal(ResearchState.Locked, sim.StateOf(ResearchProject.Electronics));
        Assert.False(sim.StartResearch(ResearchProject.Electronics));

        Complete(sim, ResearchProject.SteelWorks);

        Assert.Equal(ResearchState.Available, sim.StateOf(ResearchProject.AdvancedMaterialTransfer));
        Assert.Equal(ResearchState.Available, sim.StateOf(ResearchProject.Electronics));
        Assert.Equal(ResearchState.Locked, sim.StateOf(ResearchProject.Computers));
        Assert.True(sim.StartResearch(ResearchProject.Electronics));
    }

    [Fact]
    public void ALoadedBuildingInALockedTier_KeepsWorking_ButNoMoreCanBePlaced()
    {
        // An older save could hold a Steel Mill with no research done: the tier comes from research.
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 120f);
        SimTestHelpers.Burners(sim, 4);
        Building mill = SimTestHelpers.PlaceOnline(sim, BuildingType.Smelter, 48, 4, Facing.North, 2);
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);
        sim.Debug.SeedStockpile(ResourceType.Limestone, 4f);

        Simulation loaded = Simulation.FromSave(sim.Save());
        Building loadedMill = SimTestHelpers.Placed(loaded).Single(b => b.Id == mill.Id);
        SimTestHelpers.Order(loaded, loadedMill, 2);
        SimTestHelpers.Seconds(loaded, 25.0);

        Assert.Equal(1, loaded.UnlockedTier);
        Assert.True(SimTestHelpers.Stock(loaded, ResourceType.Steel) >= 1f);
        Assert.False(loaded.TryPlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North, 2));
    }

    [Fact]
    public void EveryBuildableBuilding_IsTier1To4_AndCostsNothingAboveItsTier()
    {
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels().Where(t => BuildingDefs.For(t.Type).Buildable))
        {
            BuildingDef def = BuildingDefs.For(type, level);
            Assert.InRange(def.Tier, 1, 4);
            Assert.All(def.Cost, cost => Assert.True(ResourceDefs.For(cost.Type).Tier <= def.Tier, $"{def.Name} costs {cost.Type}"));
        }
    }

    [Fact]
    public void EveryTierAboveTheFirst_HasAGateway_BuiltFromTheTierBelow()
    {
        foreach ((BuildingType type, int level) in Gateways)
        {
            BuildingDef def = BuildingDefs.For(type, level);
            Assert.All(def.Cost, cost => Assert.True(ResourceDefs.For(cost.Type).Tier < def.Tier, $"{def.Name} costs {cost.Type}"));
        }
        Assert.Equal(new[] { 2, 3, 4 }, Gateways.Select(g => BuildingDefs.For(g.Type, g.Level).Tier).Distinct().OrderBy(t => t));
    }

    [Fact]
    public void ATierNBuilding_MakesTierNItems()
    {
        // The exception: the Brine Well's Lithium is raw. A level is judged by the recipes it adds; the
        // ones it keeps from the level below are that level's.
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels())
        {
            int kept = level > 1 ? Recipes.For(type, level - 1).Count : 0;
            BuildingDef def = BuildingDefs.For(type, level);
            foreach (Recipe recipe in Recipes.For(type, level).Skip(kept))
            {
                foreach (ResourceAmount output in recipe.Outputs.Where(o => !(type == BuildingType.BrineWell && o.Type == ResourceType.Lithium)))
                {
                    Assert.True(ResourceDefs.For(output.Type).Tier == def.Tier, $"{def.Name} makes {output.Type}");
                }
            }
        }
    }

    [Fact]
    public void EachUnlockProject_BelongsToTheTierBelowTheOneItUnlocks_AndIsPaidInItsOwnTier()
    {
        ResearchProject[] unlocks = ResearchDefs.All.Where(p => ResearchDefs.For(p).Unlocks is not null).ToArray();

        Assert.Equal(new[] { ResearchProject.SteelWorks, ResearchProject.Electronics, ResearchProject.Computers }, unlocks);
        foreach (ResearchProject project in unlocks)
        {
            ResearchDef def = ResearchDefs.For(project);
            Assert.Equal(def.Tier + 1, def.Unlocks);
            Assert.All(def.Fee, fee => Assert.True(ResourceDefs.For(fee.Type).Tier <= def.Tier, $"{project} costs {fee.Type}"));
        }
    }
}
