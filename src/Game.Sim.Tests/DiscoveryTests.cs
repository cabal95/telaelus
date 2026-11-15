using System;
using System.Linq;

namespace Game.Sim.Tests;

public class DiscoveryTests
{
    // The empty map is 16 x 16 map cells; the player starts at its center, map cell (8, 8), and
    // sees 6 map cells, so the corner cell (0, 0) starts unexplored on a map with fog.
    private static Simulation EmptyMap(bool fog)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap(fog);
        return sim;
    }

    private static void WalkToCorner(Simulation sim)
    {
        SimTestHelpers.Teleport(sim, 2f, 2f);
        sim.Step(SimTestHelpers.TickSeconds);
    }

    [Fact]
    public void AtTheStart_NothingIsDiscovered_OnAnAllGrassMap()
    {
        Simulation sim = EmptyMap(fog: false);

        Assert.DoesNotContain(Enum.GetValues<ResourceType>(), sim.IsDiscovered);
    }

    [Fact]
    public void AField_InAnExploredCell_DiscoversItsDeposit()
    {
        Simulation sim = EmptyMap(fog: false);

        sim.Debug.PlaceField(ResourceType.Bauxite, 3, 3, 50f);

        Assert.True(sim.IsDiscovered(ResourceType.Bauxite));
        Assert.False(sim.IsDiscovered(ResourceType.Quartz));
    }

    [Fact]
    public void AField_InAnUnexploredCell_IsDiscoveredWhenRevealed()
    {
        Simulation sim = EmptyMap(fog: true);
        sim.Debug.PlaceField(ResourceType.Quartz, 0, 0, 50f);
        Assert.False(sim.IsDiscovered(ResourceType.Quartz));

        WalkToCorner(sim);

        Assert.True(sim.IsExplored(0, 0));
        Assert.True(sim.IsDiscovered(ResourceType.Quartz));
    }

    [Theory]
    [InlineData(TerrainType.Sand, ResourceType.Sand)]
    [InlineData(TerrainType.ShallowWater, ResourceType.Water)]
    [InlineData(TerrainType.DeepWater, ResourceType.Water)]
    public void RevealingTerrain_DiscoversSandAndWater(TerrainType terrain, ResourceType expected)
    {
        Simulation sim = EmptyMap(fog: true);
        sim.Debug.SetTerrain(0, 0, terrain);
        Assert.False(sim.IsDiscovered(expected));

        WalkToCorner(sim);

        Assert.True(sim.IsDiscovered(expected));
    }

    [Fact]
    public void RevealingARiver_DiscoversWater()
    {
        Simulation sim = EmptyMap(fog: true);
        sim.Debug.SetFeature(0, 0, MapFeature.River, Facing.North);
        Assert.False(sim.IsDiscovered(ResourceType.Water));

        WalkToCorner(sim);

        Assert.True(sim.IsDiscovered(ResourceType.Water));
    }

    [Fact]
    public void ChangingAnExploredCell_DiscoversWhatItNowShows()
    {
        Simulation sim = EmptyMap(fog: false);

        sim.Debug.SetTerrain(4, 4, TerrainType.Sand);

        Assert.True(sim.IsDiscovered(ResourceType.Sand));
    }

    [Fact]
    public void HoldingAResource_DiscoversIt()
    {
        Simulation sim = EmptyMap(fog: false);
        Assert.False(sim.IsDiscovered(ResourceType.Steel));

        sim.Debug.SeedStockpile(ResourceType.Steel, 1f);

        Assert.True(sim.IsDiscovered(ResourceType.Steel));
    }

    [Fact]
    public void Discovery_OutlastsTheResource_AndResetsWithTheWorld()
    {
        Simulation sim = EmptyMap(fog: false);
        ResourceField field = sim.Debug.PlaceField(ResourceType.Limestone, 3, 3, 50f);
        sim.Debug.RemoveField(field.MapX, field.MapZ);
        Assert.True(sim.IsDiscovered(ResourceType.Limestone));

        sim.Debug.ResetToEmptyMap();

        Assert.False(sim.IsDiscovered(ResourceType.Limestone));
    }

    [Fact]
    public void TheTestMap_DiscoversItsDepositsAndWater_ButNoNewDeposit()
    {
        var sim = new Simulation();

        ResourceType[] starting =
        {
            ResourceType.Wood,
            ResourceType.Stone,
            ResourceType.IronOre,
            ResourceType.CopperOre,
            ResourceType.Coal,
            ResourceType.CrudeOil,
            ResourceType.Water
        };
        Assert.All(starting, type => Assert.True(sim.IsDiscovered(type), $"{type} should be discovered"));
        // The test map places one field of each newer deposit too.
        Assert.True(sim.IsDiscovered(ResourceType.Bauxite));
        Assert.True(sim.IsDiscovered(ResourceType.Crystals));
        Assert.False(sim.IsDiscovered(ResourceType.Lithium));
        Assert.False(sim.IsDiscovered(ResourceType.Iron));
    }
}
