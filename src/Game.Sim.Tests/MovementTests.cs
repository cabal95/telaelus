using Godot;

namespace Game.Sim.Tests;

public class MovementTests
{
    [Fact]
    public void Player_MovesAtItsSpeed()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 30f, 30f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 0f)
        };

        SimTestHelpers.Ticks(sim, 15);

        // 6 units per second for half a second.
        Assert.Equal(33f, sim.Player.Position.X, 3);
        Assert.Equal(30f, sim.Player.Position.Z, 3);
    }

    [Fact]
    public void Water_BlocksMovement()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 34.5f, 24f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 0f)
        };

        SimTestHelpers.Seconds(sim, 2.0);

        // The pond starts at fine x 36.
        Assert.True(sim.Player.Position.X < 36f);
        Assert.True(sim.Player.Position.X > 35f);
    }

    [Fact]
    public void Player_SlidesAlongAnObstacle()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 35.5f, 22f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 1f)
        };

        SimTestHelpers.Seconds(sim, 1.0);

        Assert.True(sim.Player.Position.X < 36f);
        Assert.True(sim.Player.Position.Z > 24f);
    }

    [Fact]
    public void Field_BlocksMovement()
    {
        var sim = new Simulation();

        // Ore field (10, 9) covers fine z 36..39 at x 40..43.
        SimTestHelpers.Teleport(sim, 41.5f, 33f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(0f, 1f)
        };

        SimTestHelpers.Seconds(sim, 2.0);

        Assert.True(sim.Player.Position.Z < 36f);
        Assert.True(sim.Player.Position.Z > 35f);
    }

    [Fact]
    public void Building_BlocksMovement()
    {
        var sim = new Simulation();
        Assert.True(sim.TryPlaceBuilding(BuildingType.Smelter, 28, 28, Facing.North));
        SimTestHelpers.Teleport(sim, 25.5f, 29.5f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(1f, 0f)
        };

        SimTestHelpers.Seconds(sim, 2.0);

        Assert.True(sim.Player.Position.X < 28f);
        Assert.True(sim.Player.Position.X > 27f);
    }

    [Fact]
    public void Player_IsClampedToTheMap()
    {
        var sim = new Simulation();
        SimTestHelpers.Teleport(sim, 1f, 30f);
        sim.Intent = new PlayerIntent
        {
            Move = new Vector2(-1f, 0f)
        };

        SimTestHelpers.Seconds(sim, 2.0);

        Assert.Equal(0f, sim.Player.Position.X);
    }
}
