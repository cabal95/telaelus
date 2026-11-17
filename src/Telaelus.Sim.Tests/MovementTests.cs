using System.Linq;
using Godot;

namespace Telaelus.Sim.Tests;

public class MovementTests
{
    private static Simulation EmptyMap(bool fog = false)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap(fog);
        return sim;
    }

    private static bool InsideBuilding(Simulation sim, Vector3 point)
    {
        (int x, int z) = Simulation.CellAt(point.X, point.Z);
        return sim.Buildings.Any(b => x >= b.AnchorX && x < b.AnchorX + b.Width && z >= b.AnchorZ && z < b.AnchorZ + b.Depth);
    }

    [Fact]
    public void Player_WalksAtItsSpeed()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Teleport(sim, 30.5f, 30.5f);

        Assert.True(sim.WalkTo(40.5f, 30.5f));
        SimTestHelpers.Ticks(sim, 15);

        // 6 units per second for half a second.
        Assert.Equal(33.5f, sim.Player.Position.X, 3);
        Assert.Equal(30.5f, sim.Player.Position.Z, 3);
        Assert.True(sim.Player.Walking);
        Assert.Equal(new Vector2(1f, 0f), sim.Player.Heading);
    }

    [Fact]
    public void Player_StopsOnThePointWalkedTo()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Teleport(sim, 30.5f, 30.5f);

        sim.WalkTo(36.2f, 33.7f);
        SimTestHelpers.Seconds(sim, 3.0);

        Assert.Equal(36.2f, sim.Player.Position.X, 3);
        Assert.Equal(33.7f, sim.Player.Position.Z, 3);
        Assert.False(sim.Player.Walking);
        Assert.Null(sim.Player.Destination);
        Assert.Equal(Vector2.Zero, sim.Player.Heading);
    }

    [Fact]
    public void Player_WalksRoundABuilding()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceBuilding(BuildingType.Smelter, 33, 29, Facing.North);
        SimTestHelpers.Teleport(sim, 31.5f, 30.5f);

        sim.WalkTo(37.5f, 30.5f);
        for (int tick = 0; tick < 120; tick++)
        {
            SimTestHelpers.Ticks(sim, 1);
            Assert.False(InsideBuilding(sim, sim.Player.Position), $"inside the Smelter at {sim.Player.Position}");
        }

        Assert.Equal(37.5f, sim.Player.Position.X, 3);
        Assert.Equal(30.5f, sim.Player.Position.Z, 3);
    }

    [Fact]
    public void ClickingWater_WalksToTheNearestReachableCell()
    {
        Simulation sim = EmptyMap();
        // Map cell (9, 8): fine x 36 to 39, z 32 to 35.
        sim.Debug.SetTerrain(9, 8, TerrainType.ShallowWater);
        SimTestHelpers.Teleport(sim, 30.5f, 33.5f);

        Assert.True(sim.WalkTo(37.5f, 33.5f));
        SimTestHelpers.Seconds(sim, 3.0);

        Assert.Equal(35.5f, sim.Player.Position.X, 3);
        Assert.Equal(33.5f, sim.Player.Position.Z, 3);
        Assert.False(sim.Player.Walking);
    }

    [Fact]
    public void Player_WithNoWayOut_DoesNotWalk()
    {
        Simulation sim = EmptyMap();
        // Water all round map cell (8, 8).
        for (int x = 7; x <= 9; x++)
        {
            for (int z = 7; z <= 9; z++)
            {
                if (x != 8 || z != 8)
                {
                    sim.Debug.SetTerrain(x, z, TerrainType.ShallowWater);
                }
            }
        }
        SimTestHelpers.Teleport(sim, 33.5f, 33.5f);

        Assert.True(sim.WalkTo(20.5f, 33.5f));
        SimTestHelpers.Seconds(sim, 2.0);

        // As far as the island goes, and no further.
        Assert.Equal(32.5f, sim.Player.Position.X, 3);
        Assert.False(sim.Player.Walking);
        Assert.False(sim.WalkTo(20.5f, 33.5f));
    }

    [Fact]
    public void APath_NeverCrossesTheFog_ButTheWalkKeepsGoingIntoIt()
    {
        Simulation sim = EmptyMap(fog: true);
        SimTestHelpers.Ticks(sim, 1);
        Assert.False(sim.IsExplored(0, 0));

        Assert.True(sim.WalkTo(2.5f, 2.5f));
        Assert.All(sim.Player.Path, point =>
            Assert.True(sim.IsExplored(Simulation.MapCellOf((int)point.X), Simulation.MapCellOf((int)point.Z)), $"{point} is under the fog"));
        SimTestHelpers.Seconds(sim, 20.0);

        Assert.Equal(2.5f, sim.Player.Position.X, 3);
        Assert.Equal(2.5f, sim.Player.Position.Z, 3);
    }

    [Fact]
    public void ABuildingPlacedAcrossThePath_IsWalkedRound()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Teleport(sim, 20.5f, 30.5f);
        sim.WalkTo(40.5f, 30.5f);
        SimTestHelpers.Ticks(sim, 5);

        sim.Debug.PlaceBuilding(BuildingType.Smelter, 28, 29, Facing.North);
        for (int tick = 0; tick < 150; tick++)
        {
            SimTestHelpers.Ticks(sim, 1);
            Assert.False(InsideBuilding(sim, sim.Player.Position), $"inside the Smelter at {sim.Player.Position}");
        }

        Assert.Equal(40.5f, sim.Player.Position.X, 3);
        Assert.Equal(30.5f, sim.Player.Position.Z, 3);
    }

    [Fact]
    public void Teleporting_StopsAWalk()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Teleport(sim, 30.5f, 30.5f);
        sim.WalkTo(40.5f, 30.5f);

        sim.Debug.TeleportPlayer(20.5f, 20.5f);
        SimTestHelpers.Seconds(sim, 1.0);

        Assert.False(sim.Player.Walking);
        Assert.Equal(20.5f, sim.Player.Position.X, 3);
    }

    [Fact]
    public void Player_IsKeptInsideTheMap()
    {
        Simulation sim = EmptyMap();
        SimTestHelpers.Teleport(sim, 1.5f, 30.5f);

        sim.WalkTo(-10f, 30.5f);
        SimTestHelpers.Seconds(sim, 2.0);

        Assert.Equal(0.5f, sim.Player.Position.X, 3);
    }
}
