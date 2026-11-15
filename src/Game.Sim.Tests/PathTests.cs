using System.Collections.Generic;
using Godot;

namespace Game.Sim.Tests;

public class PathTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void OnOpenGround_APathIsTheStraightLine()
    {
        Simulation sim = EmptyMap();
        List<Vector3>? path = sim.FindPathTo(new Vector3(10.5f, 0f, 10.5f), 20, 10);

        Assert.NotNull(path);
        Assert.Equal(10, path.Count);
        Assert.Equal(new Vector3(20.5f, 0f, 10.5f), path[^1]);
    }

    [Fact]
    public void APath_GoesAroundABuilding_AndEndsBesideItsTarget()
    {
        Simulation sim = EmptyMap();
        // A Warehouse (x 14..16, z 9..11) stands on the straight line from (10, 10) to (20, 10).
        sim.Debug.PlaceBuilding(BuildingType.Warehouse, 14, 9, Facing.North);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 24, 9, Facing.North);

        List<Vector3>? path = sim.FindPathBeside(new Vector3(10.5f, 0f, 10.5f), mine);

        Assert.NotNull(path);
        Assert.DoesNotContain(path, p => p.X > 14f && p.X < 17f && p.Z > 9f && p.Z < 12f);
        (int x, int z) = Simulation.CellAt(path[^1].X, path[^1].Z);
        Assert.Equal(23, x);
        Assert.InRange(z, 9, 10);
    }

    [Fact]
    public void ACollectorOnAnIsland_HasNoPath()
    {
        Simulation sim = EmptyMap();
        // Map cell (5, 5) is grass ringed by water.
        for (int x = 4; x <= 6; x++)
        {
            for (int z = 4; z <= 6; z++)
            {
                if (x != 5 || z != 5)
                {
                    sim.Debug.SetTerrain(x, z, TerrainType.ShallowWater);
                }
            }
        }
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 21, 21, Facing.North);

        Assert.Null(sim.FindPathBeside(new Vector3(40.5f, 0f, 40.5f), mine));
    }

    [Fact]
    public void APath_GoesRoundSlowGround_WhenThatIsCheaper()
    {
        Simulation sim = EmptyMap();
        // A block of Mud (speed 0.6) at fine x 20..35, z 4..15, across the straight line.
        for (int x = 5; x <= 8; x++)
        {
            for (int z = 1; z <= 3; z++)
            {
                sim.Debug.SetTerrain(x, z, TerrainType.Mud);
            }
        }

        // Straight through is 16 cells of Mud (about 26.7); round it is about 25 on grass.
        List<Vector3>? path = sim.FindPathTo(new Vector3(18.5f, 0f, 10.5f), 37, 10);

        Assert.NotNull(path);
        Assert.DoesNotContain(path, p => sim.TerrainAt((int)p.X, (int)p.Z) == TerrainType.Mud);
    }
}
