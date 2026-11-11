using Godot;

namespace Game.Sim.Tests;

public class GridTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(63, 15)]
    [InlineData(-1, -1)]
    [InlineData(-4, -1)]
    [InlineData(-5, -2)]
    public void MapCellOf_UsesFloorDivision(int fine, int expectedMapCell)
    {
        Assert.Equal(expectedMapCell, Simulation.MapCellOf(fine));
    }

    [Fact]
    public void CellAt_FloorsWorldCoordinates()
    {
        Assert.Equal((2, 7), Simulation.CellAt(2.9f, 7.0f));
        Assert.Equal((-1, -1), Simulation.CellAt(-0.5f, -0.01f));
    }

    [Fact]
    public void CellCenter_IsHalfAUnitIn()
    {
        Assert.Equal(new Vector3(3.5f, 0f, 4.5f), Simulation.CellCenter(3, 4));
    }

    [Fact]
    public void InBounds_CoversTheTestMapOnly()
    {
        var sim = new Simulation();
        Assert.Equal(64, sim.GridWidth);
        Assert.Equal(64, sim.GridHeight);
        Assert.True(sim.InBounds(0, 0));
        Assert.True(sim.InBounds(63, 63));
        Assert.False(sim.InBounds(64, 0));
        Assert.False(sim.InBounds(0, -1));
    }

    [Fact]
    public void TerrainAt_ReadsTheContainingMapCell()
    {
        var sim = new Simulation();

        // Water covers map cells x 9..11, z 5..6, i.e. fine x 36..47, z 20..27.
        Assert.Equal(TerrainType.Water, sim.TerrainAt(36, 20));
        Assert.Equal(TerrainType.Water, sim.TerrainAt(47, 27));
        Assert.Equal(TerrainType.Grass, sim.TerrainAt(35, 20));
        Assert.Equal(TerrainType.Grass, sim.TerrainAt(36, 28));

        // Mountain at map cell (4, 9).
        Assert.Equal(TerrainType.Mountain, sim.TerrainAt(16, 36));

        // Out of bounds reads as grass.
        Assert.Equal(TerrainType.Grass, sim.TerrainAt(-10, -10));
    }

    [Fact]
    public void Player_SpawnsAtTheMapCenter()
    {
        var sim = new Simulation();
        Assert.Equal(new Vector3(32f, 0f, 32f), sim.Player.Position);
    }
}
