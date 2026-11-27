using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim.Tests;

public class FogTests
{
    private const double Tick = 1.0 / 30.0;

    // The empty map is 16 x 16 map cells (64 x 64 units); the player starts at its center (32, 32).
    private static Simulation FoggyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap(fog: true);
        return sim;
    }

    private static bool InSight(Vector3 from, int mapX, int mapZ)
    {
        Vector3 c = Simulation.MapCellCenter(mapX, mapZ);
        return new Vector2(c.X - from.X, c.Z - from.Z).Length() <= Simulation.SightRadius;
    }

    [Fact]
    public void AMapWithoutFog_IsExploredEverywhereInBounds()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();

        Assert.True(sim.IsExplored(0, 0));
        Assert.True(sim.IsExplored(15, 15));
        Assert.False(sim.IsExplored(16, 0));
    }

    [Fact]
    public void AtTheStart_OnlyTheCellsInSightOfTheSpawnAreExplored()
    {
        Simulation sim = FoggyMap();
        Vector3 spawn = sim.Player.Position;

        int expected = 0;
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                bool inSight = InSight(spawn, x, z);
                Assert.Equal(inSight, sim.IsExplored(x, z));
                expected += inSight ? 1 : 0;
            }
        }
        Assert.Equal(expected, sim.ExploredCount);
        // 6 map cells of sight: (13, 8)'s center is 22 units away, (14, 8)'s 26.
        Assert.True(sim.IsExplored(13, 8));
        Assert.False(sim.IsExplored(14, 8));
    }

    [Fact]
    public void Walking_RevealsAroundThePlayer_AndRevealedCellsStayExplored()
    {
        Simulation sim = FoggyMap();
        Assert.False(sim.IsExplored(15, 8));

        sim.Debug.TeleportPlayer(60f, 32f);
        sim.Step(Tick);
        Assert.True(sim.IsExplored(15, 8));

        sim.Debug.TeleportPlayer(10f, 32f);
        sim.Step(Tick);
        Assert.True(sim.IsExplored(15, 8));
        Assert.True(sim.IsExplored(0, 8));
    }

    [Fact]
    public void CellsRevealed_ListsOnlyNewlyExploredCells_OncePerNewMapCell()
    {
        Simulation sim = FoggyMap();
        var batches = new List<IReadOnlyList<(int MapX, int MapZ)>>();
        sim.CellsRevealed += batches.Add;

        // One map cell east: only the column that comes into sight is new.
        sim.Debug.TeleportPlayer(36f, 32f);
        sim.Step(Tick);
        sim.Step(Tick);

        Assert.Single(batches);
        var seen = new HashSet<(int, int)>();
        foreach ((int x, int z) in batches[0])
        {
            Assert.True(seen.Add((x, z)), "listed twice");
            Assert.True(sim.IsExplored(x, z));
            Assert.False(InSight(new Vector3(32f, 0f, 32f), x, z), "was already explored");
        }
        Assert.NotEmpty(seen);
    }

    [Fact]
    public void ExploredBounds_HoldsEveryExploredCell()
    {
        Simulation sim = FoggyMap();
        // 6 cells of sight around (32, 32): cells 2 to 13 on each axis.
        Assert.Equal((2, 2, 13, 13), sim.ExploredBounds);

        sim.Debug.TeleportPlayer(60f, 32f);
        sim.Step(Tick);
        Assert.Equal((2, 2, 15, 13), sim.ExploredBounds);

        var open = new Simulation();
        open.Debug.ResetToEmptyMap();
        Assert.Equal((0, 0, 15, 15), open.ExploredBounds);
    }

    [Fact]
    public void CanPlace_RefusesAFootprintOnUnexploredCells()
    {
        Simulation sim = FoggyMap();

        // Map cell (15, 15) is out of sight of the center; (9, 9) is in sight.
        Assert.False(sim.CanPlace(BuildingType.Silo, 61, 61, Facing.North));
        Assert.True(sim.CanPlace(BuildingType.Silo, 37, 37, Facing.North));

        sim.Debug.RevealAll();
        Assert.True(sim.CanPlace(BuildingType.Silo, 61, 61, Facing.North));
    }

    [Fact]
    public void AGeneratedWorld_HasFog_AndRevealAllExploresItsChunks()
    {
        var sim = new Simulation(WorldSettings.Generated(7));
        (int x, int z) = Simulation.CellAt(sim.Player.Position.X, sim.Player.Position.Z);
        int mapX = Simulation.MapCellOf(x);
        int mapZ = Simulation.MapCellOf(z);

        Assert.True(sim.World.Fog);
        Assert.True(sim.IsExplored(mapX, mapZ));
        Assert.False(sim.IsExplored(mapX + 20, mapZ));

        sim.Debug.RevealAll();
        Assert.True(sim.IsExplored(mapX + 20, mapZ));
    }
}
