using System;

namespace Game.Sim.Tests;

public class RiverTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void LoneRiverIntoDryLand_IsAPoolWithOnlyItsCenter()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetFeature(2, 2, MapFeature.River, Facing.East);

        Assert.Equal(RiverShape.Pool, sim.RiverShapeAt(2, 2));
        Assert.True(sim.IsRiverChannel(9, 9));
        Assert.True(sim.IsRiverChannel(10, 10));
        Assert.False(sim.IsRiverChannel(11, 9));
    }

    [Fact]
    public void StraightRun_ConnectsAlongTheFlow()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetFeature(2, 2, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(3, 2, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(4, 2, MapFeature.River, Facing.East);

        Assert.Equal(RiverShape.End, sim.RiverShapeAt(2, 2));
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(3, 2));

        // Its flow edge faces dry land, so the last cell does not connect east.
        Assert.False(sim.RiverConnects(4, 2, Facing.East));
        Assert.Equal(RiverShape.End, sim.RiverShapeAt(4, 2));
    }

    [Fact]
    public void Mouths_ConnectIntoWaterAndOffTheMap()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTerrain(3, 2, TerrainType.ShallowWater);
        sim.Debug.SetFeature(2, 2, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(15, 5, MapFeature.River, Facing.East);

        Assert.True(sim.RiverConnects(2, 2, Facing.East));
        Assert.Equal(RiverShape.End, sim.RiverShapeAt(2, 2));
        Assert.True(sim.RiverConnects(15, 5, Facing.East));
    }

    [Fact]
    public void Bend_TurnsWhereTheFlowTurns()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetFeature(2, 2, MapFeature.River, Facing.South);
        sim.Debug.SetFeature(2, 3, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(3, 3, MapFeature.River, Facing.East);

        Assert.Equal(RiverShape.Bend, sim.RiverShapeAt(2, 3));

        // The bend's channel runs north and east, not south or west.
        Assert.True(sim.IsRiverChannel(9, 12));
        Assert.True(sim.IsRiverChannel(11, 13));
        Assert.False(sim.IsRiverChannel(9, 15));
        Assert.False(sim.IsRiverChannel(8, 13));
    }

    [Fact]
    public void TwoRiversFlowingIntoOne_MakeAJunction()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetFeature(1, 3, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(3, 3, MapFeature.River, Facing.West);
        sim.Debug.SetFeature(2, 3, MapFeature.River, Facing.South);
        sim.Debug.SetTerrain(2, 4, TerrainType.ShallowWater);

        Assert.Equal(RiverShape.Junction, sim.RiverShapeAt(2, 3));
    }

    [Fact]
    public void ParallelRivers_StaySeparate()
    {
        Simulation sim = EmptyMap();
        for (int mapZ = 2; mapZ <= 4; mapZ++)
        {
            sim.Debug.SetFeature(2, mapZ, MapFeature.River, Facing.South);
            sim.Debug.SetFeature(3, mapZ, MapFeature.River, Facing.South);
        }

        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(2, 3));
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(3, 3));
        Assert.False(sim.RiverConnects(2, 3, Facing.East));
        Assert.False(sim.IsRiverChannel(11, 13));
        Assert.False(sim.IsRiverChannel(12, 13));
    }

    [Theory]
    [InlineData(TerrainType.Grass)]
    [InlineData(TerrainType.Sand)]
    [InlineData(TerrainType.Snow)]
    public void Channel_BlocksWhileBanksKeepTheirTerrain(TerrainType terrain)
    {
        Simulation sim = EmptyMap();
        for (int mapX = 1; mapX <= 3; mapX++)
        {
            sim.Debug.SetTerrain(mapX, 2, terrain);
            sim.Debug.SetFeature(mapX, 2, MapFeature.River, Facing.East);
        }

        // Map cell (2, 2) runs east-west: channel rows z 9 and 10, banks z 8 and 11.
        Assert.False(sim.IsWalkable(8, 9));
        Assert.False(sim.IsBuildable(11, 10));
        Assert.True(sim.IsWalkable(8, 8));
        Assert.True(sim.IsBuildable(11, 11));
        Assert.Equal(terrain, sim.TerrainAt(8, 9));
        Assert.Equal(terrain, sim.TerrainAt(8, 8));

        // A Mine on the north bank and the ground beyond fits; one reaching the channel does not.
        Assert.True(sim.CanPlace(BuildingType.Mine, 8, 7, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Mine, 8, 8, Facing.North));
    }

    [Fact]
    public void TestMap_RiverShapes()
    {
        var sim = new Simulation();
        Assert.Equal(RiverShape.End, sim.RiverShapeAt(7, 0));
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(7, 2));
        Assert.Equal(RiverShape.Bend, sim.RiverShapeAt(7, 4));
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(8, 4));
        Assert.Equal(RiverShape.Bend, sim.RiverShapeAt(9, 4));
        Assert.Equal(RiverShape.End, sim.RiverShapeAt(13, 10));
        Assert.Equal(RiverShape.Straight, sim.RiverShapeAt(15, 10));
        Assert.Equal(TerrainType.Mud, sim.MapTerrainAt(7, 1));
        Assert.Equal(TerrainType.Sand, sim.MapTerrainAt(14, 10));
    }

    [Fact]
    public void SetFeature_RejectsInvalidRequests()
    {
        Simulation sim = EmptyMap();
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetFeature(2, 2, MapFeature.River));
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetFeature(2, 2, MapFeature.Mountain, Facing.East));

        sim.Debug.SetTerrain(3, 3, TerrainType.ShallowWater);
        sim.Debug.SetTerrain(4, 4, TerrainType.Lava);
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetFeature(3, 3, MapFeature.River, Facing.East));
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetFeature(4, 4, MapFeature.Mountain));

        sim.Debug.PlaceField(ResourceType.Ore, 5, 5);
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetFeature(5, 5, MapFeature.Mountain));

        sim.Debug.SetFeature(6, 6, MapFeature.Mountain);
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetTerrain(6, 6, TerrainType.ShallowWater));
        Assert.Throws<InvalidOperationException>(() => sim.Debug.PlaceField(ResourceType.Ore, 6, 6));
        sim.Debug.SetTerrain(6, 6, TerrainType.Snow);
    }

    [Fact]
    public void ResetToEmptyMap_ClearsBiomesAndFeatures()
    {
        Simulation sim = EmptyMap();
        for (int mapX = 0; mapX < sim.MapWidth; mapX++)
        {
            for (int mapZ = 0; mapZ < sim.MapHeight; mapZ++)
            {
                Assert.Equal(BiomeType.Grassland, sim.MapBiomeAt(mapX, mapZ));
                Assert.Equal(MapFeature.None, sim.MapFeatureAt(mapX, mapZ));
                Assert.Null(sim.RiverFlowAt(mapX, mapZ));
            }
        }
    }
}
