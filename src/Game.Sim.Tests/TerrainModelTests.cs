using System;
using Godot;

namespace Game.Sim.Tests;

public class TerrainModelTests
{
    [Fact]
    public void EveryTerrainAndBiome_HasADefinition()
    {
        foreach (TerrainType terrain in Enum.GetValues<TerrainType>())
        {
            Assert.NotNull(TerrainDefs.For(terrain));
        }
        foreach (BiomeType biome in Enum.GetValues<BiomeType>())
        {
            Assert.NotNull(BiomeDefs.For(biome));
        }
    }

    [Theory]
    [InlineData(TerrainType.Grass, true, true)]
    [InlineData(TerrainType.Sand, true, true)]
    [InlineData(TerrainType.Snow, true, true)]
    [InlineData(TerrainType.Rock, true, true)]
    [InlineData(TerrainType.Mud, true, false)]
    [InlineData(TerrainType.ShallowWater, false, false)]
    [InlineData(TerrainType.DeepWater, false, false)]
    [InlineData(TerrainType.Lava, false, false)]
    public void TerrainRules_GateWalkingAndBuilding(TerrainType terrain, bool walkable, bool buildable)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetTerrain(2, 2, terrain);

        Assert.Equal(walkable, sim.IsWalkable(9, 9));
        Assert.Equal(buildable, sim.IsBuildable(9, 9));
        Assert.Equal(buildable, sim.CanPlace(BuildingType.Mine, 9, 9, Facing.North));
    }

    [Theory]
    [InlineData(TerrainType.Grass, 1f)]
    [InlineData(TerrainType.Rock, 1f)]
    [InlineData(TerrainType.Sand, 0.75f)]
    [InlineData(TerrainType.Snow, 0.75f)]
    [InlineData(TerrainType.Mud, 0.6f)]
    public void PlayerSpeed_IsMultipliedByTheTerrainUnderfoot(TerrainType terrain, float multiplier)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetTerrain(8, 8, terrain);
        sim.Debug.TeleportPlayer(32.5f, 33.5f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 0f)
        };

        SimTestHelpers.Ticks(sim, 15);

        // 6 units per second for half a second, scaled; the walk stays inside map cell (8, 8).
        Assert.Equal(32.5f + 3f * multiplier, sim.Player.Position.X, 3);
    }

    [Theory]
    [InlineData(TerrainType.Grass)]
    [InlineData(TerrainType.Snow)]
    public void Mountain_BlocksItsWholeMapCellAndKeepsItsTerrain(TerrainType terrain)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetTerrain(2, 2, terrain);
        sim.Debug.SetFeature(2, 2, MapFeature.Mountain);

        for (int x = 8; x < 12; x++)
        {
            for (int z = 8; z < 12; z++)
            {
                Assert.False(sim.IsWalkable(x, z));
                Assert.False(sim.IsBuildable(x, z));
                Assert.Equal(terrain, sim.TerrainAt(x, z));
                Assert.Equal(MapFeature.Mountain, sim.FeatureAt(x, z));
            }
        }
        Assert.True(sim.IsWalkable(12, 8));
    }

    [Fact]
    public void Field_WithoutAnAmount_TakesItsBiomeAmountOrTheDefault()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetBiome(2, 2, BiomeType.Forest);
        sim.Debug.SetBiome(3, 3, BiomeType.Forest);
        sim.Debug.SetBiome(4, 4, BiomeType.Highlands);

        Assert.Equal(500f, sim.Debug.PlaceField(ResourceType.Wood, 2, 2).InitialAmount);
        Assert.Equal(BiomeDef.DefaultFieldAmount, sim.Debug.PlaceField(ResourceType.IronOre, 3, 3).InitialAmount);
        Assert.Equal(400f, sim.Debug.PlaceField(ResourceType.IronOre, 4, 4).InitialAmount);
        Assert.Equal(250f, sim.Debug.PlaceField(ResourceType.Wood, 5, 5).InitialAmount);
    }

    [Fact]
    public void Ocean_OffersNoFields()
    {
        Assert.Empty(BiomeDefs.For(BiomeType.Ocean).Fields);
    }

    [Fact]
    public void TestMap_PaintsEveryBiome()
    {
        var sim = new Simulation();
        Assert.Equal(BiomeType.Forest, sim.MapBiomeAt(3, 4));
        Assert.Equal(BiomeType.Swamp, sim.MapBiomeAt(8, 0));
        Assert.Equal(BiomeType.Tundra, sim.MapBiomeAt(14, 0));
        Assert.Equal(BiomeType.Desert, sim.MapBiomeAt(13, 6));
        Assert.Equal(BiomeType.Highlands, sim.MapBiomeAt(1, 10));
        Assert.Equal(BiomeType.Ocean, sim.MapBiomeAt(5, 15));
        Assert.Equal(BiomeType.Volcanic, sim.MapBiomeAt(15, 15));
        Assert.Equal(BiomeType.Grassland, sim.MapBiomeAt(8, 8));

        Assert.Equal(TerrainType.Snow, sim.MapTerrainAt(14, 1));
        Assert.Equal(MapFeature.Mountain, sim.MapFeatureAt(14, 1));
        Assert.Equal(MapFeature.Mountain, sim.MapFeatureAt(11, 12));
        Assert.Equal(TerrainType.Lava, sim.MapTerrainAt(13, 14));
        Assert.Equal(TerrainType.DeepWater, sim.MapTerrainAt(0, 15));
    }
}
