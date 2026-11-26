using System;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class TierTests
{
    private static readonly BuildingType[] Gateways =
    {
        BuildingType.SteelMill,
        BuildingType.Refinery,
        BuildingType.OilWell,
        BuildingType.DrillRig,
        BuildingType.Fabricator,
        BuildingType.ElectronicsWorks,
        BuildingType.AssemblyPlant
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
        Assert.False(sim.IsUnlocked(BuildingType.SteelMill));
    }

    [Fact]
    public void HoldingATier2Cost_DoesNotUnlockIt()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);

        Assert.True(sim.CanAfford(BuildingType.SteelMill));
        Assert.False(sim.IsUnlocked(BuildingType.SteelMill));
        Assert.False(sim.TryPlaceBuilding(BuildingType.SteelMill, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
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
    public void AfterSteelWorks_ASteelMillCanBePlaced()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 40f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 60f);
        Complete(sim, ResearchProject.SteelWorks);

        Assert.True(sim.TryPlaceBuilding(BuildingType.SteelMill, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
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
        Building mill = SimTestHelpers.PlaceOnline(sim, BuildingType.SteelMill, 48, 4);
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);
        sim.Debug.SeedStockpile(ResourceType.Limestone, 4f);

        Simulation loaded = Simulation.FromSave(sim.Save());
        Building loadedMill = SimTestHelpers.Placed(loaded).Single(b => b.Id == mill.Id);
        SimTestHelpers.Order(loaded, loadedMill);
        SimTestHelpers.Seconds(loaded, 25.0);

        Assert.Equal(1, loaded.UnlockedTier);
        Assert.True(SimTestHelpers.Stock(loaded, ResourceType.Steel) >= 1f);
        Assert.False(loaded.TryPlaceBuilding(BuildingType.SteelMill, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
    }

    [Fact]
    public void EveryBuildableBuilding_IsTier1To4_AndCostsNothingAboveItsTier()
    {
        foreach (BuildingType type in Enum.GetValues<BuildingType>().Where(t => BuildingDefs.For(t).Buildable))
        {
            BuildingDef def = BuildingDefs.For(type);
            Assert.InRange(def.Tier, 1, 4);
            Assert.All(def.Cost, cost => Assert.True(ResourceDefs.For(cost.Type).Tier <= def.Tier, $"{type} costs {cost.Type}"));
        }
    }

    [Fact]
    public void EveryTierAboveTheFirst_HasAGateway_BuiltFromTheTierBelow()
    {
        foreach (BuildingType gateway in Gateways)
        {
            BuildingDef def = BuildingDefs.For(gateway);
            Assert.All(def.Cost, cost => Assert.True(ResourceDefs.For(cost.Type).Tier < def.Tier, $"{gateway} costs {cost.Type}"));
        }
        Assert.Equal(new[] { 2, 3, 4 }, Gateways.Select(g => BuildingDefs.For(g).Tier).Distinct().OrderBy(t => t));
    }

    [Fact]
    public void ATierNBuilding_MakesTierNItems()
    {
        // The two exceptions: Lithium is raw, and the Air Separator is the better tier 3 source of a tier 2 item.
        (BuildingType, ResourceType)[] exceptions =
        {
            (BuildingType.BrineWell, ResourceType.Lithium),
            (BuildingType.AirSeparator, ResourceType.LiquidOxygen)
        };
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            foreach (Recipe recipe in Recipes.For(type))
            {
                foreach (ResourceAmount output in recipe.Outputs.Where(o => !exceptions.Contains((type, o.Type))))
                {
                    Assert.True(ResourceDefs.For(output.Type).Tier == BuildingDefs.For(type).Tier, $"{type} makes {output.Type}");
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
