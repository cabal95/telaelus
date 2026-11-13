using Godot;

namespace Game.Sim.Tests;

public class PlacementTests
{
    private const int X = SimTestHelpers.ClearX;
    private const int Z = SimTestHelpers.ClearZ;

    [Theory]
    [InlineData(BuildingType.Mine, Facing.North, 2, 2)]
    [InlineData(BuildingType.LumberCamp, Facing.East, 2, 2)]
    [InlineData(BuildingType.Smelter, Facing.West, 3, 3)]
    [InlineData(BuildingType.Refinery, Facing.North, 2, 3)]
    [InlineData(BuildingType.Refinery, Facing.East, 3, 2)]
    [InlineData(BuildingType.Refinery, Facing.South, 2, 3)]
    [InlineData(BuildingType.Refinery, Facing.West, 3, 2)]
    public void Footprint_SwapsWidthAndDepthForEastAndWest(BuildingType type, Facing facing, int width, int depth)
    {
        Assert.Equal((width, depth), BuildingDefs.For(type).Footprint(facing));
    }

    [Fact]
    public void CanPlace_OnClearGrass()
    {
        var sim = new Simulation();
        Assert.True(sim.CanPlace(BuildingType.Smelter, X, Z, Facing.North));
    }

    [Fact]
    public void CanPlace_RejectsWater()
    {
        var sim = new Simulation();
        Assert.False(sim.CanPlace(BuildingType.Smelter, 36, 20, Facing.North));

        // Partially overlapping the pond's edge (fine x 36) is rejected too.
        Assert.False(sim.CanPlace(BuildingType.Smelter, 34, 20, Facing.North));
    }

    [Fact]
    public void CanPlace_RejectsMountain()
    {
        var sim = new Simulation();
        Assert.False(sim.CanPlace(BuildingType.Mine, 16, 36, Facing.North));
    }

    [Fact]
    public void CanPlace_RejectsAnyFootprintCellOnAField()
    {
        var sim = new Simulation();
        Assert.False(sim.CanPlace(BuildingType.Mine, 40, 36, Facing.North));

        // A Smelter at (38, 34) covers fine (40, 36), the Ore field's corner.
        Assert.False(sim.CanPlace(BuildingType.Smelter, 38, 34, Facing.North));
    }

    [Fact]
    public void CanPlace_RejectsOccupiedCells()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Smelter, X, Z, Facing.North);
        Assert.False(sim.CanPlace(BuildingType.Mine, X + 2, Z + 2, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.Mine, X + 3, Z, Facing.North));
    }

    [Fact]
    public void CanPlace_RejectsOutOfBounds()
    {
        var sim = new Simulation();
        Assert.False(sim.CanPlace(BuildingType.Mine, -1, 10, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Smelter, 62, 62, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.Smelter, 61, 61, Facing.North));
    }

    [Fact]
    public void CanPlace_ChecksTheRotatedFootprint()
    {
        var sim = new Simulation();

        // The pond starts at fine x 36 (z 20..27). A Refinery anchored at (34, 22) fits
        // facing North (x 34..35) but reaches the water facing East (x 34..36).
        Assert.True(sim.CanPlace(BuildingType.Refinery, 34, 22, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Refinery, 34, 22, Facing.East));
    }

    [Fact]
    public void ExtractorWithNoFieldInRange_IsPlaceable()
    {
        var sim = new Simulation();
        Assert.Empty(sim.FieldsInRange(BuildingDefs.For(BuildingType.Mine).Center(X, Z, Facing.North), 6f, ResourceType.IronOre));
        sim.Debug.PlaceBuilding(BuildingType.Mine, X, Z, Facing.North);
    }

    [Fact]
    public void TryPlaceBuilding_OccupiesEveryFootprintCell()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Refinery, X, Z, Facing.East);

        for (int x = X; x < X + 3; x++)
        {
            for (int z = Z; z < Z + 2; z++)
            {
                Assert.False(sim.IsWalkable(x, z));
            }
        }

        Assert.True(sim.IsWalkable(X + 3, Z));
        Assert.True(sim.IsWalkable(X, Z + 2));
    }

    [Fact]
    public void Building_DerivesItsSizeAndCenterFromAnchorAndFacing()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Refinery, X, Z, Facing.East);
        Building building = Assert.Single(sim.Buildings);

        Assert.Equal(3, building.Width);
        Assert.Equal(2, building.Depth);
        Assert.Equal(new Vector3(X + 1.5f, 0f, Z + 1f), building.Position);
    }

    [Fact]
    public void TryPlaceBuilding_FailsWithoutChangingState()
    {
        var sim = new Simulation();
        Assert.False(sim.TryPlaceBuilding(BuildingType.Smelter, 36, 20, Facing.North));
        Assert.Empty(sim.Buildings);
    }

    // The player stands in fine cell (32, 32) for these tests.
    [Theory]
    [InlineData(31, 31)]
    [InlineData(32, 32)]
    [InlineData(30, 30)]
    [InlineData(30, 31)]
    public void CanPlace_RejectsAFootprintContainingThePlayer(int anchorX, int anchorZ)
    {
        // A 3x3 Smelter: centered on the player, and with the player on a corner or an edge.
        var sim = new Simulation();
        sim.Debug.TeleportPlayer(32.5f, 32.5f);
        Assert.False(sim.CanPlace(BuildingType.Smelter, anchorX, anchorZ, Facing.North));
    }

    [Theory]
    [InlineData(33, 31)]
    [InlineData(29, 31)]
    [InlineData(31, 33)]
    [InlineData(31, 29)]
    public void CanPlace_AllowsAFootprintRightNextToThePlayer(int anchorX, int anchorZ)
    {
        var sim = new Simulation();
        sim.Debug.TeleportPlayer(32.5f, 32.5f);
        Assert.True(sim.CanPlace(BuildingType.Smelter, anchorX, anchorZ, Facing.North));
    }

    [Fact]
    public void TryPlaceBuilding_OnThePlayer_FailsWithoutChangingState()
    {
        var sim = new Simulation();
        sim.Debug.TeleportPlayer(32.5f, 32.5f);
        Assert.False(sim.TryPlaceBuilding(BuildingType.Smelter, 31, 31, Facing.North));
        Assert.Empty(sim.Buildings);
        Assert.True(sim.IsWalkable(32, 32));
    }

    [Fact]
    public void PlayerOnACellBoundary_CountsAsTheCellTheirPositionFloorsTo()
    {
        // At exactly (32, 32) the player is in cell (32, 32), not (31, 31).
        var sim = new Simulation();
        sim.Debug.TeleportPlayer(32f, 32f);
        Assert.True(sim.CanPlace(BuildingType.Smelter, 29, 29, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Smelter, 32, 32, Facing.North));
    }

    [Fact]
    public void DebugPlaceBuilding_OnThePlayer_Throws()
    {
        var sim = new Simulation();
        sim.Debug.TeleportPlayer(32.5f, 32.5f);
        Assert.Throws<System.InvalidOperationException>(
            () => sim.Debug.PlaceBuilding(BuildingType.Mine, 32, 32, Facing.North));
    }
}
