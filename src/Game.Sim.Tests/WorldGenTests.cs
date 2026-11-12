using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim.Tests;

public class WorldGenTests
{
    private static readonly Facing[] Edges = { Facing.North, Facing.East, Facing.South, Facing.West };

    private static Simulation Generated(int seed)
    {
        return new Simulation(WorldSettings.Generated(seed));
    }

    [Fact]
    public void SameSeed_GivesIdenticalChunks()
    {
        GeneratedChunk a = new WorldGenerator(42, PlanetDefs.Starter).Generate(1, -2);
        GeneratedChunk b = new WorldGenerator(42, PlanetDefs.Starter).Generate(1, -2);
        AssertSame(a, b);

        GeneratedChunk other = new WorldGenerator(43, PlanetDefs.Starter).Generate(1, -2);
        Assert.NotEqual(Snapshot(a), Snapshot(other));
    }

    [Fact]
    public void GenerationOrder_DoesNotChangeChunks()
    {
        var forward = new WorldGenerator(7, PlanetDefs.Starter);
        GeneratedChunk a1 = forward.Generate(0, 0);
        GeneratedChunk b1 = forward.Generate(1, 0);

        var reverse = new WorldGenerator(7, PlanetDefs.Starter);
        GeneratedChunk b2 = reverse.Generate(1, 0);
        GeneratedChunk a2 = reverse.Generate(0, 0);

        AssertSame(a1, a2);
        AssertSame(b1, b2);
    }

    [Fact]
    public void ChunkBiomes_MatchTheGeneratorEverywhere()
    {
        var generator = new WorldGenerator(11, PlanetDefs.Starter);
        GeneratedChunk chunk = generator.Generate(-1, 2);
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                Assert.Equal(generator.BiomeAt(Chunk.Origin(-1) + x, Chunk.Origin(2) + z), chunk.Chunk.BiomeAt(x, z));
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(19)]
    [InlineData(77)]
    public void Rivers_FlowIntoRiversOrWater_AcrossChunkBorders(int seed)
    {
        Simulation sim = Generated(seed);
        int rivers = 0;
        // The 3 x 3 chunks around the spawn, skipping the outer ring of cells.
        for (int mapX = -Chunk.Size + 1; mapX < 2 * Chunk.Size - 1; mapX++)
        {
            for (int mapZ = -Chunk.Size + 1; mapZ < 2 * Chunk.Size - 1; mapZ++)
            {
                if (sim.RiverFlowAt(mapX, mapZ) is not Facing flow)
                {
                    continue;
                }
                rivers++;
                Assert.True(TerrainDefs.For(sim.MapTerrainAt(mapX, mapZ)).TakesFeatures, $"river on {sim.MapTerrainAt(mapX, mapZ)} at ({mapX}, {mapZ})");

                bool anyWet = false;
                foreach (Facing edge in Edges)
                {
                    anyWet |= IsWet(sim, mapX, mapZ, edge);
                }
                // A river flows to a river cell or water, unless it has none beside it (a Pool).
                Assert.True(!anyWet || IsWet(sim, mapX, mapZ, flow), $"river at ({mapX}, {mapZ}) flows {flow} onto dry land beside water or river");
            }
        }
        Assert.True(rivers > 0, "expected some rivers near the spawn");
    }

    [Fact]
    public void Spawn_IsOpenGrassland()
    {
        var generator = new WorldGenerator(1, PlanetDefs.Starter);
        (int sx, int sz) = generator.SpawnCell;
        Assert.Equal((Chunk.Size / 2, Chunk.Size / 2), (sx, sz));
        AssertOpenGrassland(generator, sx, sz);

        Simulation sim = Generated(1);
        (int px, int pz) = Simulation.CellAt(sim.Player.Position.X, sim.Player.Position.Z);
        Assert.Equal((sx, sz), (Simulation.MapCellOf(px), Simulation.MapCellOf(pz)));
        Assert.True(sim.IsWalkable(px, pz));
    }

    [Fact]
    public void Spawn_MovesWhenTheCenterHasNoExit()
    {
        // Seed 8's center of chunk (0, 0) is walled in; the second candidate has an exit.
        var generator = new WorldGenerator(8, PlanetDefs.Starter);
        Assert.Equal(2, generator.SpawnAttemptsUsed);
        Assert.NotEqual((Chunk.Size / 2, Chunk.Size / 2), generator.SpawnCell);
        AssertOpenGrassland(generator, generator.SpawnCell.X, generator.SpawnCell.Z);
    }

    [Fact]
    public void FindSpawn_TakesTheFirstCandidateWithAnExit_OrSettlesForTheLast()
    {
        ((int X, int Z) Cell, int Attempts) found = WorldGenerator.FindSpawn(i => (i, 0), c => c.X == 3, 100);
        Assert.Equal((3, 0), found.Cell);
        Assert.Equal(4, found.Attempts);

        ((int X, int Z) Cell, int Attempts) none = WorldGenerator.FindSpawn(i => (i, 0), _ => false, 100);
        Assert.Equal((99, 0), none.Cell);
        Assert.Equal(100, none.Attempts);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Deposits_SitOnFreeWalkableCells_ApartAndFromTheirBiome(int seed)
    {
        Simulation sim = Generated(seed);
        var fieldCells = new HashSet<(int, int)>();
        foreach (ResourceField field in sim.Fields)
        {
            fieldCells.Add((field.MapX, field.MapZ));
        }
        Assert.NotEmpty(fieldCells);
        foreach (ResourceField field in sim.Fields)
        {
            Assert.Equal(MapFeature.None, sim.MapFeatureAt(field.MapX, field.MapZ));
            Assert.True(TerrainDefs.For(sim.MapTerrainAt(field.MapX, field.MapZ)).Walkable);
            BiomeType biome = sim.MapBiomeAt(field.MapX, field.MapZ);
            Assert.True(BiomeDefs.For(biome).FieldWeights.ContainsKey(field.Type), $"{field.Type} in {biome}");
            Assert.Equal(BiomeDefs.For(biome).FieldAmount(field.Type), field.InitialAmount);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if ((dx, dz) != (0, 0))
                    {
                        Assert.DoesNotContain((field.MapX + dx, field.MapZ + dz), fieldCells);
                    }
                }
            }
        }
    }

    [Fact]
    public void BiomeMix_FollowsThePlanetWeights_OnAverage()
    {
        var counts = new Dictionary<BiomeType, double>();
        double total = 0;
        for (int seed = 1; seed <= 10; seed++)
        {
            var generator = new WorldGenerator(seed, PlanetDefs.Starter);
            // Every 4th cell of a 6 x 6 chunk area is plenty to measure shares.
            for (int x = -3 * Chunk.Size; x < 3 * Chunk.Size; x += 4)
            {
                for (int z = -3 * Chunk.Size; z < 3 * Chunk.Size; z += 4)
                {
                    BiomeType biome = generator.BiomeAt(x, z);
                    counts.TryGetValue(biome, out double c);
                    counts[biome] = c + 1;
                    total++;
                }
            }
        }
        foreach (BiomeType biome in Enum.GetValues<BiomeType>())
        {
            counts.TryGetValue(biome, out double c);
            double share = 100.0 * c / total;
            double want = 100.0 * PlanetDefs.Starter.Share(biome);
            Assert.True(Math.Abs(share - want) <= 4.0, $"{biome}: {share:F1}% vs {want:F1}%");
        }
    }

    [Fact]
    public void Chunks_GenerateAroundThePlayer_AndOnDemand()
    {
        Simulation sim = Generated(9);
        Assert.Equal(9, sim.ChunkCount);
        Assert.False(sim.IsChunkGenerated(3, 0));

        sim.Debug.TeleportPlayer(Chunk.Size * 3 * Simulation.FineCellsPerMapCell + 10f, 10f);
        sim.Step(SimTestHelpers.TickSeconds);
        for (int cx = 2; cx <= 4; cx++)
        {
            for (int cz = -1; cz <= 1; cz++)
            {
                Assert.True(sim.IsChunkGenerated(cx, cz), $"chunk ({cx}, {cz})");
            }
        }

        // Reading a far cell generates its chunk.
        _ = sim.MapTerrainAt(-1000, 500);
        Assert.True(sim.IsChunkGenerated(Chunk.Of(-1000), Chunk.Of(500)));
        Assert.True(sim.MapInBounds(-1000, 500));
    }

    [Fact]
    public void GeneratedWorld_HasNoEdge_AndBoundedWorldsKeepTheirs()
    {
        Simulation generated = Generated(1);
        Assert.True(generated.InBounds(-5000, 7000));
        Assert.Throws<InvalidOperationException>(() => generated.MapWidth);

        var testMap = new Simulation();
        Assert.False(testMap.InBounds(-1, 0));
        Assert.Equal(16, testMap.MapWidth);
        Assert.Equal(1, testMap.ChunkCount);
        Assert.Equal(WorldKind.TestMap, testMap.World.Kind);
    }

    [Fact]
    public void ResetToGeneratedWorld_SwitchesKindsBothWays()
    {
        var sim = new Simulation();
        sim.Debug.ResetToGeneratedWorld(5);
        Assert.Equal(WorldKind.Generated, sim.World.Kind);
        Assert.Equal(5, sim.World.Seed);
        Assert.Equal(sim.Generator!.SpawnPoint, sim.Player.Position);

        sim.Debug.ResetToEmptyMap();
        Assert.Equal(WorldKind.Empty, sim.World.Kind);
        Assert.Empty(sim.Fields);
        Assert.Equal(new Vector3(32f, 0f, 32f), sim.Player.Position);
    }

    private static void AssertOpenGrassland(WorldGenerator generator, int sx, int sz)
    {
        var chunks = new Dictionary<(int, int), GeneratedChunk>();
        GeneratedChunk ChunkFor(int x, int z)
        {
            (int, int) key = (Chunk.Of(x), Chunk.Of(z));
            if (!chunks.TryGetValue(key, out GeneratedChunk? g))
            {
                g = generator.Generate(key.Item1, key.Item2);
                chunks[key] = g;
            }
            return g;
        }

        int r = WorldGenerator.SpawnClearRadius;
        for (int x = sx - r; x <= sx + r; x++)
        {
            for (int z = sz - r; z <= sz + r; z++)
            {
                if ((x - sx) * (x - sx) + (z - sz) * (z - sz) > r * r)
                {
                    continue;
                }
                Chunk chunk = ChunkFor(x, z).Chunk;
                int lx = x - Chunk.Origin(chunk.ChunkX);
                int lz = z - Chunk.Origin(chunk.ChunkZ);
                Assert.Equal(BiomeType.Grassland, chunk.BiomeAt(lx, lz));
                Assert.Equal(TerrainType.Grass, chunk.TerrainAt(lx, lz));
                Assert.Equal(MapFeature.None, chunk.FeatureAt(lx, lz));
            }
        }
        foreach (GeneratedChunk g in chunks.Values)
        {
            foreach ((ResourceType _, int fx, int fz) in g.Fields)
            {
                Assert.False(Math.Abs(fx - sx) <= WorldGenerator.SpawnFieldGap && Math.Abs(fz - sz) <= WorldGenerator.SpawnFieldGap, "field at the spawn");
            }
        }
    }

    private static bool IsWet(Simulation sim, int mapX, int mapZ, Facing edge)
    {
        (int dx, int dz) = Simulation.EdgeOffset(edge);
        return sim.RiverFlowAt(mapX + dx, mapZ + dz) is not null || TerrainDefs.IsWater(sim.MapTerrainAt(mapX + dx, mapZ + dz));
    }

    private static string Snapshot(GeneratedChunk g)
    {
        var text = new System.Text.StringBuilder();
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                text.Append((int)g.Chunk.BiomeAt(x, z)).Append((int)g.Chunk.TerrainAt(x, z)).Append((int)g.Chunk.FeatureAt(x, z))
                    .Append(g.Chunk.RiverFlowAt(x, z)?.ToString() ?? "-");
            }
        }
        foreach ((ResourceType type, int fx, int fz) in g.Fields)
        {
            text.Append(type).Append(fx).Append(',').Append(fz).Append(';');
        }
        return text.ToString();
    }

    private static void AssertSame(GeneratedChunk a, GeneratedChunk b)
    {
        Assert.Equal(Snapshot(a), Snapshot(b));
    }
}
