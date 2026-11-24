using System;
using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class SaltFlatTests
{
    private const int Reach = 3;
    private const int Width = 2 * Reach * Chunk.Size;

    /// <summary>The 6 x 6 chunks around the origin of a generated world, as terrain and biome grids.</summary>
    private static (TerrainType[,] Terrain, BiomeType[,] Biome, List<GeneratedChunk> Chunks) World(int seed)
    {
        var generator = new WorldGenerator(seed, PlanetDefs.Starter);
        var terrain = new TerrainType[Width, Width];
        var biome = new BiomeType[Width, Width];
        var chunks = new List<GeneratedChunk>();
        for (int cx = -Reach; cx < Reach; cx++)
        {
            for (int cz = -Reach; cz < Reach; cz++)
            {
                GeneratedChunk generated = generator.Generate(cx, cz);
                chunks.Add(generated);
                for (int i = 0; i < Chunk.Size; i++)
                {
                    for (int j = 0; j < Chunk.Size; j++)
                    {
                        terrain[(cx + Reach) * Chunk.Size + i, (cz + Reach) * Chunk.Size + j] = generated.Chunk.TerrainAt(i, j);
                        biome[(cx + Reach) * Chunk.Size + i, (cz + Reach) * Chunk.Size + j] = generated.Chunk.BiomeAt(i, j);
                    }
                }
            }
        }
        return (terrain, biome, chunks);
    }

    /// <summary>Each salt flat as its cells (edge-connected), skipping any that touch the edge of the grid.</summary>
    private static List<List<(int X, int Z)>> Flats(TerrainType[,] terrain)
    {
        var flats = new List<List<(int, int)>>();
        var seen = new bool[Width, Width];
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Width; z++)
            {
                if (terrain[x, z] != TerrainType.SaltFlat || seen[x, z])
                {
                    continue;
                }
                var cells = new List<(int, int)>();
                var stack = new Stack<(int, int)>();
                stack.Push((x, z));
                seen[x, z] = true;
                bool edge = false;
                while (stack.Count > 0)
                {
                    (int a, int b) = stack.Pop();
                    cells.Add((a, b));
                    edge |= a == 0 || b == 0 || a == Width - 1 || b == Width - 1;
                    foreach ((int da, int db) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int na = a + da;
                        int nb = b + db;
                        if (na >= 0 && nb >= 0 && na < Width && nb < Width && terrain[na, nb] == TerrainType.SaltFlat && !seen[na, nb])
                        {
                            seen[na, nb] = true;
                            stack.Push((na, nb));
                        }
                    }
                }
                if (!edge)
                {
                    flats.Add(cells);
                }
            }
        }
        return flats;
    }

    [Fact]
    public void SaltFlats_AreAboutOnePer400DesertCells_ThreeToSixAcross()
    {
        int desert = 0;
        var flats = new List<List<(int X, int Z)>>();
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            (TerrainType[,] terrain, BiomeType[,] biome, _) = World(seed);
            desert += biome.Cast<BiomeType>().Count(b => b == BiomeType.Desert);
            flats.AddRange(Flats(terrain));
        }

        float perFlat = (float)desert / flats.Count;
        Assert.InRange(perFlat, 300f, 500f);
        foreach (List<(int X, int Z)> flat in flats)
        {
            int span = Math.Max(flat.Max(c => c.X) - flat.Min(c => c.X), flat.Max(c => c.Z) - flat.Min(c => c.Z)) + 1;
            Assert.InRange(span, 2, 6);
        }
        Assert.True(flats.Count(f => f.Count >= 9) > flats.Count / 2, "most flats are at least 3 x 3");
    }

    [Fact]
    public void ASaltFlat_LiesInTheDesert_BordersOnlySand_AndIsAtLeastTwoByTwo()
    {
        (TerrainType[,] terrain, BiomeType[,] biome, _) = World(7);
        int flatCells = 0;
        for (int x = 1; x < Width - 1; x++)
        {
            for (int z = 1; z < Width - 1; z++)
            {
                if (terrain[x, z] != TerrainType.SaltFlat)
                {
                    continue;
                }
                flatCells++;
                Assert.Equal(BiomeType.Desert, biome[x, z]);
                bool inBlock = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Assert.Contains(terrain[x + dx, z + dz], new[] { TerrainType.SaltFlat, TerrainType.Sand });
                    }
                }
                foreach ((int bx, int bz) in new[] { (-1, -1), (-1, 0), (0, -1), (0, 0) })
                {
                    inBlock |= Enumerable.Range(0, 4).All(k => terrain[x + bx + k % 2, z + bz + k / 2] == TerrainType.SaltFlat);
                }
                Assert.True(inBlock, $"({x}, {z}) lies in a 2 x 2 block of salt flat");
            }
        }
        Assert.True(flatCells > 0);
    }

    [Fact]
    public void ASaltFlat_TakesNoFieldsMotherlodesOrFeatures()
    {
        (TerrainType[,] terrain, _, List<GeneratedChunk> chunks) = World(3);
        int origin = -Reach * Chunk.Size;
        foreach (GeneratedChunk generated in chunks)
        {
            foreach ((ResourceType _, int mapX, int mapZ) in generated.Fields)
            {
                Assert.NotEqual(TerrainType.SaltFlat, terrain[mapX - origin, mapZ - origin]);
            }
            foreach ((MotherlodeType _, int mapX, int mapZ) in generated.Motherlodes)
            {
                for (int k = 0; k < 4; k++)
                {
                    Assert.NotEqual(TerrainType.SaltFlat, terrain[mapX + k % 2 - origin, mapZ + k / 2 - origin]);
                }
            }
            for (int i = 0; i < Chunk.Size; i++)
            {
                for (int j = 0; j < Chunk.Size; j++)
                {
                    if (generated.Chunk.TerrainAt(i, j) == TerrainType.SaltFlat)
                    {
                        Assert.Equal(MapFeature.None, generated.Chunk.FeatureAt(i, j));
                    }
                }
            }
        }
    }

    [Fact]
    public void TheTestMap_HasASaltFlatInItsDesert()
    {
        var sim = new Simulation();
        for (int x = 14; x <= 15; x++)
        {
            for (int z = 5; z <= 6; z++)
            {
                Assert.Equal(TerrainType.SaltFlat, sim.MapTerrainAt(x, z));
                Assert.Equal(BiomeType.Desert, sim.MapBiomeAt(x, z));
            }
        }
    }
}
