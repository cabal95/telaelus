using System.Linq;
using Godot;

namespace Game.Sim.Tests;

public class RoadTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void ARoadLine_IsAnL_LongLegFirst_OnTheEvenTileGrid()
    {
        var line = Simulation.RoadLine(9, 3, 16, 6);
        Assert.Equal(new[] { (8, 2), (10, 2), (12, 2), (14, 2), (16, 2), (16, 4), (16, 6) }, line);
        var tall = Simulation.RoadLine(0, 0, 2, 7);
        Assert.Equal(new[] { (0, 0), (0, 2), (0, 4), (0, 6), (2, 6) }, tall);
    }

    [Fact]
    public void ARoadTile_CostsAStoneACell_TwoOnSlowGround()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTerrain(5, 5, TerrainType.Mud);
        Assert.Equal(4f, sim.RoadTileCost((8, 8))[0].Amount);
        Assert.Equal(8f, sim.RoadTileCost((20, 20))[0].Amount);

        sim.Debug.SeedStockpile(ResourceType.Stone, 12f);
        // Three grass tiles (12 Stone): laid, the stock spent.
        Assert.Equal(3, sim.TryLayRoad(8, 8, 13, 8));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.True(sim.IsRoad(9, 9));
        Assert.True(sim.IsRoad(13, 9));
    }

    [Fact]
    public void ALine_SkipsPavedTiles_AndStopsWhenTheStoneRunsOut()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Stone, 4f);
        Assert.Equal(1, sim.TryLayRoad(10, 8, 10, 8));
        sim.Debug.SeedStockpile(ResourceType.Stone, 10f);

        // Tiles 8, 10 (paved: free), 12, 14, 16: 10 Stone pays for two new tiles.
        RoadPlan plan = sim.PlanRoad(8, 8, 16, 8);
        Assert.Equal(2, plan.Laid);
        Assert.Equal(new[] { RoadTileState.Ready, RoadTileState.Paved, RoadTileState.Ready, RoadTileState.Short, RoadTileState.Short },
            plan.Tiles.Select(t => t.State));
        Assert.Equal(2, sim.TryLayRoad(8, 8, 16, 8));
        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.False(sim.IsRoad(14, 8));
    }

    [Fact]
    public void ARoad_IsRefusedOffWalkableLand()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTerrain(2, 2, TerrainType.ShallowWater);
        sim.Debug.SetTerrain(3, 2, TerrainType.Lava);
        sim.Debug.SetFeature(4, 2, MapFeature.Mountain);
        sim.Debug.PlaceField(ResourceType.Stone, 5, 2);
        sim.Debug.SetFeature(6, 2, MapFeature.River, Facing.South);
        sim.Debug.PlaceBuilding(BuildingType.Silo, 28, 8, Facing.North);

        Assert.False(sim.CanLayRoadTile((8, 8)));   // water
        Assert.False(sim.CanLayRoadTile((12, 8)));  // lava
        Assert.False(sim.CanLayRoadTile((16, 8)));  // mountain
        Assert.False(sim.CanLayRoadTile((20, 8)));  // field
        Assert.False(sim.CanLayRoadTile((24, 8)));  // river channel (cells 25, 26)
        Assert.False(sim.CanLayRoadTile((28, 8)));  // a building
        Assert.True(sim.CanLayRoadTile((40, 40)));
    }

    [Fact]
    public void ABuilding_IsRefusedOverARoad()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceRoad(20, 20, 20, 20);
        Assert.False(sim.CanPlace(BuildingType.Silo, 19, 19, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.Silo, 16, 16, Facing.North));
    }

    [Fact]
    public void ThePlayer_WalksAtRoadSpeed_OverMud()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SetTerrain(2, 8, TerrainType.Mud);
        sim.Debug.SetTerrain(3, 8, TerrainType.Mud);
        sim.Debug.SetTerrain(4, 8, TerrainType.Mud);
        sim.Debug.SetTerrain(5, 8, TerrainType.Mud);
        sim.Debug.PlaceRoad(8, 32, 22, 32);
        SimTestHelpers.Teleport(sim, 9f, 33f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 0f)
        };

        SimTestHelpers.Seconds(sim, 1.0);

        // 6 units/s times the road's 1.5, not Mud's 0.6.
        Assert.Equal(18f, sim.Player.Position.X, 0);
    }

    [Fact]
    public void ATruckPath_TakesARoad_ThatSavesTime()
    {
        Simulation sim = EmptyMap();
        for (int x = 1; x <= 7; x++)
        {
            for (int z = 7; z <= 10; z++)
            {
                sim.Debug.SetTerrain(x, z, TerrainType.Mud);
            }
        }
        // Across the Mud at z 34, and a road two cells off it at z 36..37.
        sim.Debug.PlaceRoad(6, 36, 28, 36);
        var path = sim.FindPathTo(new Vector3(6.5f, 0f, 34.5f), 28, 34)!;

        int paved = path.Count(p => sim.IsPaved((int)p.X, (int)p.Z));
        Assert.True(paved > path.Count / 2, $"{paved} of {path.Count} path cells on the road");
    }

    /// <summary>A strip of ShallowWater one map cell (4 fine cells, two tiles) wide down the whole map at map column 8.</summary>
    private static Simulation Riverside()
    {
        Simulation sim = EmptyMap();
        for (int z = 0; z < 16; z++)
        {
            sim.Debug.SetTerrain(8, z, TerrainType.ShallowWater);
        }
        return sim;
    }

    [Fact]
    public void ABridge_OverShallowWater_LetsATruckPathCross()
    {
        Simulation sim = Riverside();
        Assert.Null(sim.FindPathTo(new Vector3(40.5f, 0f, 21f), 24, 21));

        sim.Debug.SeedStockpile(ResourceType.Wood, 24f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 8f);
        Assert.True(sim.TryLayBridge(32, 20, 34, 20));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.True(sim.IsWalkable(33, 21));
        Assert.Equal(Simulation.RoadSpeed, sim.CellSpeed(33, 21));

        Assert.NotNull(sim.FindPathTo(new Vector3(40.5f, 0f, 21f), 24, 21));
        Assert.False(sim.CanPlace(BuildingType.SmallHub, 33, 21, Facing.North));
    }

    [Fact]
    public void ABridge_IsRefused_WhenItDoesNotSpanOpenWaterToLand()
    {
        Simulation sim = Riverside();
        sim.Debug.SeedStockpile(ResourceType.Wood, 500f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 500f);
        Assert.Equal("A bridge runs straight", sim.BridgeProblem(Simulation.BridgeLine(32, 20, 34, 24)));
        // Along the water rather than across it: no land at its ends.
        Assert.Equal("It must land at both ends", sim.BridgeProblem(Simulation.BridgeLine(32, 20, 32, 24)));
        // Over dry land.
        Assert.Equal("Not over open water", sim.BridgeProblem(Simulation.BridgeLine(20, 20, 22, 20)));

        // Too long: a lake eight tiles wide.
        for (int x = 9; x <= 11; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                sim.Debug.SetTerrain(x, z, TerrainType.ShallowWater);
            }
        }
        Assert.Equal("At most 12 cells long", sim.BridgeProblem(Simulation.BridgeLine(32, 20, 46, 20)));
        sim.Debug.SetTerrain(9, 5, TerrainType.DeepWater);
        Assert.Equal("Not over open water", sim.BridgeProblem(Simulation.BridgeLine(36, 20, 38, 20)));
    }

    [Fact]
    public void ABridge_CrossesAStraightRiver_OnlyAtRightAngles()
    {
        Simulation sim = EmptyMap();
        // A North-South river down map column 8, and a bend at map cell (3, 3).
        for (int z = 1; z <= 6; z++)
        {
            sim.Debug.SetFeature(8, z, MapFeature.River, Facing.South);
        }
        sim.Debug.SetFeature(3, 2, MapFeature.River, Facing.South);
        sim.Debug.SetFeature(3, 3, MapFeature.River, Facing.East);
        sim.Debug.SetFeature(4, 3, MapFeature.River, Facing.East);

        // Across the straight cell (8, 4): its channel is fine x 33..34, so two tiles from 32 to 35.
        Assert.Null(sim.BridgeProblem(Simulation.BridgeLine(32, 16, 34, 16)));
        // Along it.
        Assert.NotNull(sim.BridgeProblem(Simulation.BridgeLine(32, 14, 32, 20)));
        // Over the bend.
        Assert.NotNull(sim.BridgeProblem(Simulation.BridgeLine(12, 12, 14, 12)));
    }

    [Fact]
    public void Removing_RefundsHalf_AndABridgeWithThePlayerOnItStays()
    {
        Simulation sim = Riverside();
        sim.Debug.SeedStockpile(ResourceType.Stone, 12f);
        Assert.Equal(3, sim.TryLayRoad(40, 40, 44, 40));
        Assert.Equal(3, sim.TryRemoveRoad(40, 40, 44, 40));
        Assert.Equal(6f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.False(sim.IsRoad(41, 41));

        sim.Debug.PlaceBridge(32, 20, 34, 20);
        SimTestHelpers.Teleport(sim, 33f, 21f);
        Assert.Equal(0, sim.TryRemoveBridge(32, 20, 32, 20));
        SimTestHelpers.Teleport(sim, 44f, 21f);
        Assert.Equal(1, sim.TryRemoveBridge(32, 20, 32, 20));
        Assert.Equal(12f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(6f + 4f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Empty(sim.Bridges);
    }
}
