using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Sim;

/// <summary>A freshly generated chunk and the deposit fields to add for it.</summary>
public sealed record GeneratedChunk(Chunk Chunk, IReadOnlyList<(ResourceType Type, int MapX, int MapZ)> Fields)
{
    /// <summary>The starter set's fields in this chunk, each holding <see cref="WorldGenerator.StarterFieldAmount"/>.</summary>
    public IReadOnlyList<(ResourceType Type, int MapX, int MapZ)> StarterFields { get; init; } = Array.Empty<(ResourceType, int, int)>();
}

/// <summary>
/// Generates a world chunk by chunk from a seed and a planet. Everything is a pure
/// function of the seed, the planet, and the map cell's position, so chunks can be
/// generated in any order and always agree along their borders.
///
/// Biomes come from regions: a site on a grid every <see cref="RegionSpacing"/>
/// map cells (nudged by a hash) owns the cells nearest it, measured after a gentle
/// warp. A site's biome comes from climate fields sampled there (continentalness,
/// temperature, moisture, ruggedness, volcanic hot spots), in climate bands whose
/// widths are set from the planet's weights. Within a biome, cells take its base
/// terrain plus noise patches; mountains follow ridged noise in Highlands and
/// Tundra (broken into segments) with rare single peaks elsewhere; rivers follow
/// the zero line of their own noise field and flow downhill over a hidden
/// elevation field; deposits are placed by a hash per cell. The spawn is the center
/// of chunk (0, 0) if the player can walk out from there, otherwise the first of a
/// seeded series of nearby cells that has an exit; the area around it is clear
/// Grassland.
/// </summary>
public sealed class WorldGenerator
{
    public const int RegionSpacing = 24;

    /// <summary>Map cells within this distance of the spawn are clear Grassland grass.</summary>
    public const int SpawnClearRadius = 10;

    /// <summary>No field is placed within this many map cells of the spawn.</summary>
    public const int SpawnFieldGap = 2;

    // Extra cells generated around a chunk so neighbor lookups agree with the next chunk.
    private const int Pad = 4;

    // Noise salts. Each fractal octave adds 101 to its salt, so these stay well apart.
    private const int SaltContinental = 1000;
    private const int SaltTemperature = 2000;
    private const int SaltMoisture = 3000;
    private const int SaltRugged = 4000;
    private const int SaltHotspot = 5000;
    private const int SaltWarpX = 6000;
    private const int SaltWarpZ = 7000;
    private const int SaltSiteX = 8000;
    private const int SaltSiteZ = 9000;
    private const int SaltPatch = 10000;
    private const int SaltRidge = 11000;
    private const int SaltRangeBreak = 12000;
    private const int SaltPeak = 13000;
    private const int SaltRiver = 14000;
    private const int SaltRiverMask = 15000;
    private const int SaltElevation = 16000;
    private const int SaltDeposit = 17000;
    private const int SaltDepositType = 18000;
    private const int SaltSpawn = 19000;
    private const int SaltStarter = 20000;

    /// <summary>The fields every generated world gets near its spawn: building materials only.</summary>
    public static readonly IReadOnlyList<ResourceType> StarterSet = new[]
    {
        ResourceType.Wood,
        ResourceType.Wood,
        ResourceType.Stone
    };

    /// <summary>What each starter field holds, whatever biome it lands in.</summary>
    public const float StarterFieldAmount = 1000f;

    /// <summary>Starter fields lie this many map cells from the spawn cell or more...</summary>
    public const int StarterRingMin = 4;

    /// <summary>...and this many or fewer (inside the starting sight of 6 map cells), widening a cell at a time up to <see cref="StarterRingLimit"/> when the ring has too few open cells.</summary>
    public const int StarterRingMax = 6;

    public const int StarterRingLimit = 16;

    private readonly record struct Climate(float Continental, float Temperature, float Moisture, float Rugged, float Hotspot);

    private sealed record Band(BiomeType Biome, Func<Climate, float> Key, float Threshold);

    private readonly List<Band> _bands = new();
    private readonly Dictionary<(int I, int J), BiomeType> _siteBiomes = new();
    private (int I, int J) _spawnSite;

    public int Seed { get; }
    public PlanetDef Planet { get; }

    /// <summary>A spawn must let the player walk at least this many map cells away from it (an exit).</summary>
    public const int SpawnExitDistance = 40;

    /// <summary>How many spawn candidates are tried before settling for the last one.</summary>
    public const int SpawnAttempts = 100;

    /// <summary>Candidates after the first lie within this many map cells of the center of chunk (0, 0).</summary>
    public const int SpawnSearchRadius = 96;

    /// <summary>The spawn map cell, chosen when the generator is built (see <see cref="FindSpawn"/>).</summary>
    public (int X, int Z) SpawnCell { get; private set; }

    /// <summary>How many candidates the spawn search tried (1 when the first one had an exit).</summary>
    public int SpawnAttemptsUsed { get; private set; }

    public Vector3 SpawnPoint => Simulation.MapCellCenter(SpawnCell.X, SpawnCell.Z);

    /// <summary>
    /// The HQ's anchor fine cell: facing South, its footprint is centered on the spawn point's X and
    /// ends 3 fine cells north of it, inside the spawn's clear Grassland.
    /// </summary>
    public (int X, int Z) HqAnchor
    {
        get
        {
            (int width, int depth) = BuildingDefs.For(BuildingType.Hq).Footprint(Facing.South);
            int spawnX = SpawnCell.X * Simulation.FineCellsPerMapCell + Simulation.FineCellsPerMapCell / 2;
            int spawnZ = SpawnCell.Z * Simulation.FineCellsPerMapCell + Simulation.FineCellsPerMapCell / 2;
            return (spawnX - width / 2, spawnZ - 3 - depth);
        }
    }

    public WorldGenerator(int seed, PlanetDef planet)
    {
        Seed = seed;
        Planet = planet;
        BuildBands();
        ((int, int) cell, int attempts) = FindSpawn(SpawnCandidate, HasExit, SpawnAttempts);
        // The last candidate checked is always the one chosen, so its chunks are kept for
        // the world to take instead of generating them again.
        Dictionary<(int, int), GeneratedChunk> checkedChunks = _lastChecked;
        SetSpawn(cell);
        _ready = checkedChunks;
        SpawnAttemptsUsed = attempts;
        PlaceStarterSet();
    }

    /// <summary>The starter set's cells, chosen with the spawn.</summary>
    public IReadOnlyList<(ResourceType Type, int MapX, int MapZ)> StarterFields { get; private set; } = Array.Empty<(ResourceType, int, int)>();

    /// <summary>
    /// Chooses the starter set's cells among the spawn's chunks (already generated for the spawn
    /// check): open cells (walkable, no feature, not near the spawn or under the HQ, no rolled
    /// field) in the ring around the spawn, in an order hashed from the seed, none touching another
    /// starter field. Rolled fields touching a starter field are dropped, so fields never touch.
    /// </summary>
    private void PlaceStarterSet()
    {
        var rolled = new HashSet<(int, int)>();
        foreach (GeneratedChunk generated in _ready.Values)
        {
            foreach ((ResourceType _, int x, int z) in generated.Fields)
            {
                rolled.Add((x, z));
            }
        }

        var chosen = new List<(ResourceType Type, int MapX, int MapZ)>();
        for (int ring = StarterRingMax; ring <= StarterRingLimit && chosen.Count < StarterSet.Count; ring++)
        {
            chosen.Clear();
            var candidates = new List<(int X, int Z)>();
            for (int x = SpawnCell.X - ring; x <= SpawnCell.X + ring; x++)
            {
                for (int z = SpawnCell.Z - ring; z <= SpawnCell.Z + ring; z++)
                {
                    int dx = x - SpawnCell.X;
                    int dz = z - SpawnCell.Z;
                    int squared = dx * dx + dz * dz;
                    if (squared >= StarterRingMin * StarterRingMin && squared <= ring * ring && IsStarterOpen(x, z, rolled))
                    {
                        candidates.Add((x, z));
                    }
                }
            }
            candidates.Sort((a, b) => Noise.Hash01(Seed, SaltStarter, a.X, a.Z).CompareTo(Noise.Hash01(Seed, SaltStarter, b.X, b.Z)));
            foreach (ResourceType type in StarterSet)
            {
                foreach ((int x, int z) in candidates)
                {
                    if (!chosen.Exists(c => Math.Abs(c.MapX - x) <= 1 && Math.Abs(c.MapZ - z) <= 1))
                    {
                        chosen.Add((type, x, z));
                        break;
                    }
                }
            }
        }
        StarterFields = chosen;

        foreach ((int, int) key in new List<(int, int)>(_ready.Keys))
        {
            GeneratedChunk generated = _ready[key];
            var kept = generated.Fields
                .Where(f => !chosen.Exists(c => Math.Abs(c.MapX - f.MapX) <= 1 && Math.Abs(c.MapZ - f.MapZ) <= 1))
                .ToList();
            var starters = chosen
                .Where(c => Chunk.Of(c.MapX) == generated.Chunk.ChunkX && Chunk.Of(c.MapZ) == generated.Chunk.ChunkZ)
                .ToList();
            _ready[key] = generated with
            {
                Fields = kept,
                StarterFields = starters
            };
        }
    }

    private bool IsStarterOpen(int x, int z, HashSet<(int, int)> rolled)
    {
        if (NearSpawn(x, z) || rolled.Contains((x, z))
            || !_ready.TryGetValue((Chunk.Of(x), Chunk.Of(z)), out GeneratedChunk? generated))
        {
            return false;
        }
        Chunk chunk = generated.Chunk;
        int lx = x - Chunk.Origin(chunk.ChunkX);
        int lz = z - Chunk.Origin(chunk.ChunkZ);
        return TerrainDefs.For(chunk.TerrainAt(lx, lz)).Walkable && chunk.FeatureAt(lx, lz) == MapFeature.None;
    }

    // Chunks generated while checking the chosen spawn, handed out once by Generate.
    private Dictionary<(int, int), GeneratedChunk> _ready = new();
    private Dictionary<(int, int), GeneratedChunk> _lastChecked = new();

    // ---- Biomes ----------------------------------------------------------

    /// <summary>The biome of a map cell.</summary>
    public BiomeType BiomeAt(int x, int z)
    {
        return BiomeAt(x, z, null);
    }

    /// <summary>
    /// The biome of a map cell, reading site positions from <paramref name="sites"/> when
    /// given (a chunk's precomputed grid) and computing them otherwise; both give the same result.
    /// </summary>
    private BiomeType BiomeAt(int x, int z, SiteGrid? sites)
    {
        if (InSpawnClearing(x, z))
        {
            return BiomeType.Grassland;
        }
        float wx = x + WarpReach * Noise.Fbm(Seed, SaltWarpX, x, z, 18f, 2);
        float wz = z + WarpReach * Noise.Fbm(Seed, SaltWarpZ, x, z, 18f, 2);
        (int i, int j) = NearestSite(wx, wz, sites);
        return SiteBiome(i, j);
    }

    // How far (map cells) the warp can move a cell before its nearest site is found.
    private const float WarpReach = 7f;

    /// <summary>Site positions for a block of region cells, computed once for a chunk.</summary>
    private sealed class SiteGrid
    {
        private readonly int _i0;
        private readonly int _j0;
        private readonly int _width;
        private readonly (float X, float Z)[] _positions;

        public SiteGrid(WorldGenerator generator, int minX, int minZ, int maxX, int maxZ)
        {
            _i0 = (int)MathF.Floor((minX - WarpReach) / RegionSpacing) - 1;
            _j0 = (int)MathF.Floor((minZ - WarpReach) / RegionSpacing) - 1;
            int i1 = (int)MathF.Floor((maxX + WarpReach) / RegionSpacing) + 1;
            int j1 = (int)MathF.Floor((maxZ + WarpReach) / RegionSpacing) + 1;
            _width = i1 - _i0 + 1;
            _positions = new (float, float)[_width * (j1 - _j0 + 1)];
            for (int i = _i0; i <= i1; i++)
            {
                for (int j = _j0; j <= j1; j++)
                {
                    _positions[(j - _j0) * _width + (i - _i0)] = generator.SitePosition(i, j);
                }
            }
        }

        public (float X, float Z) this[int i, int j] => _positions[(j - _j0) * _width + (i - _i0)];
    }

    private (float X, float Z) SitePosition(int i, int j)
    {
        float span = RegionSpacing - 6;
        return (i * RegionSpacing + 3 + Noise.Hash01(Seed, SaltSiteX, i, j) * span,
                j * RegionSpacing + 3 + Noise.Hash01(Seed, SaltSiteZ, i, j) * span);
    }

    private (int I, int J) NearestSite(float x, float z, SiteGrid? sites = null)
    {
        int ci = (int)MathF.Floor(x / RegionSpacing);
        int cj = (int)MathF.Floor(z / RegionSpacing);
        (int I, int J) best = (ci, cj);
        float bestDistance = float.MaxValue;
        for (int i = ci - 1; i <= ci + 1; i++)
        {
            for (int j = cj - 1; j <= cj + 1; j++)
            {
                (float sx, float sz) = sites is null ? SitePosition(i, j) : sites[i, j];
                float d = (sx - x) * (sx - x) + (sz - z) * (sz - z);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = (i, j);
                }
            }
        }
        return best;
    }

    private BiomeType SiteBiome(int i, int j)
    {
        if ((i, j) == _spawnSite)
        {
            return BiomeType.Grassland;
        }
        if (_siteBiomes.TryGetValue((i, j), out BiomeType cached))
        {
            return cached;
        }
        (float sx, float sz) = SitePosition(i, j);
        BiomeType biome = Classify(ClimateAt(sx, sz));
        _siteBiomes[(i, j)] = biome;
        return biome;
    }

    private Climate ClimateAt(float x, float z)
    {
        return new Climate(
            Noise.Fbm(Seed, SaltContinental, x, z, 140f, 3),
            Noise.Fbm(Seed, SaltTemperature, x, z, 120f, 2),
            Noise.Fbm(Seed, SaltMoisture, x, z, 100f, 2),
            Noise.Fbm(Seed, SaltRugged, x, z, 70f, 2),
            Noise.Fbm(Seed, SaltHotspot, x, z, 60f, 2));
    }

    private BiomeType Classify(Climate climate)
    {
        foreach (Band band in _bands)
        {
            if (band.Key(climate) >= band.Threshold)
            {
                return band.Biome;
            }
        }
        return BiomeType.Grassland;
    }

    /// <summary>
    /// Sets each climate band's threshold so its biome takes the planet's share of
    /// region sites: bands are carved in order (Ocean, Volcanic, Highlands, Tundra,
    /// Desert, Swamp, Forest), each taking the sites highest on its key among those
    /// not yet taken, and whatever is left is Grassland. Thresholds come from a fixed
    /// sample of sites, so they depend only on the seed and the planet.
    /// </summary>
    private void BuildBands()
    {
        var order = new (BiomeType Biome, Func<Climate, float> Key)[]
        {
            (BiomeType.Ocean, c => -c.Continental),
            (BiomeType.Volcanic, c => c.Hotspot),
            (BiomeType.Highlands, c => c.Rugged),
            (BiomeType.Tundra, c => -c.Temperature),
            (BiomeType.Desert, c => c.Temperature - c.Moisture),
            (BiomeType.Swamp, c => c.Moisture),
            (BiomeType.Forest, c => c.Moisture)
        };

        var remaining = new List<Climate>();
        for (int i = -48; i < 48; i++)
        {
            for (int j = -48; j < 48; j++)
            {
                (float sx, float sz) = SitePosition(i, j);
                remaining.Add(ClimateAt(sx, sz));
            }
        }

        float remainingShare = 1f;
        foreach ((BiomeType biome, Func<Climate, float> key) in order)
        {
            float share = Planet.Share(biome);
            float fraction = remainingShare > 0f ? share / remainingShare : 0f;
            remainingShare -= share;
            if (fraction <= 0f || remaining.Count == 0)
            {
                _bands.Add(new Band(biome, key, float.PositiveInfinity));
                continue;
            }

            var keys = new List<float>(remaining.Count);
            foreach (Climate c in remaining)
            {
                keys.Add(key(c));
            }
            keys.Sort((a, b) => b.CompareTo(a));
            int take = Math.Clamp((int)MathF.Round(fraction * keys.Count), 0, keys.Count);
            float threshold = take == 0 ? float.PositiveInfinity : keys[take - 1];
            _bands.Add(new Band(biome, key, threshold));
            remaining.RemoveAll(c => key(c) >= threshold);
        }
    }

    // ---- Spawn -----------------------------------------------------------

    /// <summary>
    /// Tries candidates in order and returns the first that passes the check, with how
    /// many were tried; after <paramref name="attempts"/> failures it settles for the last.
    /// </summary>
    public static ((int X, int Z) Cell, int Attempts) FindSpawn(
        Func<int, (int X, int Z)> candidate, Func<(int X, int Z), bool> hasExit, int attempts)
    {
        (int X, int Z) cell = candidate(0);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            cell = candidate(attempt);
            if (hasExit(cell))
            {
                return (cell, attempt + 1);
            }
        }
        return (cell, attempts);
    }

    /// <summary>The first candidate is the center of chunk (0, 0); later ones are hashed cells around it.</summary>
    private (int X, int Z) SpawnCandidate(int attempt)
    {
        int center = Chunk.Size / 2;
        if (attempt == 0)
        {
            return (center, center);
        }
        int dx = (int)(Noise.Hash01(Seed, SaltSpawn, attempt, 0) * (2 * SpawnSearchRadius + 1)) - SpawnSearchRadius;
        int dz = (int)(Noise.Hash01(Seed, SaltSpawn, attempt, 1) * (2 * SpawnSearchRadius + 1)) - SpawnSearchRadius;
        return (center + dx, center + dz);
    }

    /// <summary>Moves the spawn (and so the clear Grassland around it) to a cell.</summary>
    private void SetSpawn((int X, int Z) cell)
    {
        SpawnCell = cell;
        _spawnSite = NearestSite(cell.X, cell.Z);
        _siteBiomes.Clear();
    }

    /// <summary>
    /// Whether a candidate spawn appears to have an exit: with the spawn placed there,
    /// a walk over open map cells (walkable terrain, no mountain, river, or field)
    /// reaches a cell at least <see cref="SpawnExitDistance"/> cells away on either axis.
    /// The chunks around the candidate are generated for the check and discarded.
    /// </summary>
    private bool HasExit((int X, int Z) cell)
    {
        SetSpawn(cell);
        int reach = SpawnExitDistance;
        var chunks = new Dictionary<(int, int), GeneratedChunk>();
        var fields = new HashSet<(int, int)>();
        for (int cx = Chunk.Of(cell.X - reach); cx <= Chunk.Of(cell.X + reach); cx++)
        {
            for (int cz = Chunk.Of(cell.Z - reach); cz <= Chunk.Of(cell.Z + reach); cz++)
            {
                GeneratedChunk generated = Generate(cx, cz);
                chunks[(cx, cz)] = generated;
                foreach ((ResourceType _, int fx, int fz) in generated.Fields)
                {
                    fields.Add((fx, fz));
                }
            }
        }

        _lastChecked = chunks;

        bool Open(int x, int z)
        {
            Chunk chunk = chunks[(Chunk.Of(x), Chunk.Of(z))].Chunk;
            int lx = x - Chunk.Origin(chunk.ChunkX);
            int lz = z - Chunk.Origin(chunk.ChunkZ);
            return TerrainDefs.For(chunk.TerrainAt(lx, lz)).Walkable
                && chunk.FeatureAt(lx, lz) == MapFeature.None
                && !fields.Contains((x, z));
        }

        var seen = new HashSet<(int, int)> { cell };
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue(cell);
        while (queue.Count > 0)
        {
            (int x, int z) = queue.Dequeue();
            if (Math.Abs(x - cell.X) >= reach || Math.Abs(z - cell.Z) >= reach)
            {
                return true;
            }
            foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                (int, int) next = (x + dx, z + dz);
                if (!seen.Contains(next) && Open(next.Item1, next.Item2))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return false;
    }

    private bool InSpawnClearing(int x, int z)
    {
        int dx = x - SpawnCell.X;
        int dz = z - SpawnCell.Z;
        return dx * dx + dz * dz <= SpawnClearRadius * SpawnClearRadius;
    }

    /// <summary>Whether a map cell is too near the spawn for a field: within the gap, or under the HQ.</summary>
    private bool NearSpawn(int x, int z)
    {
        return (Math.Abs(x - SpawnCell.X) <= SpawnFieldGap && Math.Abs(z - SpawnCell.Z) <= SpawnFieldGap) || UnderHq(x, z);
    }

    private bool UnderHq(int mapX, int mapZ)
    {
        (int anchorX, int anchorZ) = HqAnchor;
        (int width, int depth) = BuildingDefs.For(BuildingType.Hq).Footprint(Facing.South);
        return mapX >= Simulation.MapCellOf(anchorX) && mapX <= Simulation.MapCellOf(anchorX + width - 1)
            && mapZ >= Simulation.MapCellOf(anchorZ) && mapZ <= Simulation.MapCellOf(anchorZ + depth - 1);
    }

    // ---- Terrain ---------------------------------------------------------

    private TerrainType BaseTerrainAt(int x, int z, BiomeType biome)
    {
        TerrainType terrain = BiomeDefs.For(biome).BaseTerrain;
        if (InSpawnClearing(x, z))
        {
            return terrain;
        }
        float patch = Noise.Fbm(Seed, SaltPatch, x, z, 9f, 2);
        return biome switch
        {
            BiomeType.Grassland when patch > 0.55f => TerrainType.ShallowWater,
            BiomeType.Highlands when patch < -0.3f => TerrainType.Grass,
            BiomeType.Desert when patch > 0.45f => TerrainType.Rock,
            BiomeType.Swamp when patch > 0.2f => TerrainType.ShallowWater,
            BiomeType.Tundra when patch > 0.45f => TerrainType.Rock,
            BiomeType.Volcanic when patch > 0.4f => TerrainType.Lava,
            _ => terrain
        };
    }

    private bool MountainAt(int x, int z, BiomeType biome, TerrainType terrain)
    {
        if (!TerrainDefs.For(terrain).TakesFeatures || InSpawnClearing(x, z))
        {
            return false;
        }
        if (biome is BiomeType.Highlands or BiomeType.Tundra)
        {
            return Noise.Ridged(Seed, SaltRidge, x, z, 26f, 3) > 0.88f
                && Noise.Fbm(Seed, SaltRangeBreak, x, z, 30f, 2) > 0f;
        }
        if (biome is BiomeType.Grassland or BiomeType.Forest or BiomeType.Volcanic)
        {
            return Noise.Hash01(Seed, SaltPeak, x, z) < 0.003f;
        }
        return false;
    }

    // ---- Rivers ----------------------------------------------------------

    /// <summary>
    /// Whether a cell lies on a river line: where the mask allows, and the river noise
    /// changes sign between the cell and its east or south neighbor. <paramref name="sign"/>
    /// caches each cell's noise sign for the chunk (0 unknown, 1 negative, 2 non-negative),
    /// indexed from (<paramref name="ox"/>, <paramref name="oz"/>).
    /// </summary>
    private bool RiverLineAt(int x, int z, sbyte[,] sign, int ox, int oz)
    {
        if (Noise.Fbm(Seed, SaltRiverMask, x, z, 60f, 2) <= 0.3f)
        {
            return false;
        }
        bool here = RiverSign(x, z, sign, ox, oz);
        return here != RiverSign(x + 1, z, sign, ox, oz) || here != RiverSign(x, z + 1, sign, ox, oz);
    }

    private bool RiverSign(int x, int z, sbyte[,] sign, int ox, int oz)
    {
        ref sbyte cached = ref sign[x - ox, z - oz];
        if (cached == 0)
        {
            cached = Noise.Fbm(Seed, SaltRiver, x, z, 48f, 3) >= 0f ? (sbyte)2 : (sbyte)1;
        }
        return cached == 2;
    }

    private float ElevationAt(int x, int z)
    {
        return Noise.Fbm(Seed, SaltElevation, x, z, 140f, 4) + 0.35f * Noise.Ridged(Seed, SaltRidge, x, z, 26f, 3);
    }

    // ---- Chunks ----------------------------------------------------------

    /// <summary>Generates one chunk: every cell's biome, terrain, feature, river flow, and its fields.</summary>
    public GeneratedChunk Generate(int chunkX, int chunkZ)
    {
        if (_ready.Remove((chunkX, chunkZ), out GeneratedChunk? ready))
        {
            return ready;
        }

        int n = Chunk.Size + 2 * Pad;
        int ox = Chunk.Origin(chunkX) - Pad;
        int oz = Chunk.Origin(chunkZ) - Pad;

        var sites = new SiteGrid(this, ox, oz, ox + n, oz + n);
        var biome = new BiomeType[n, n];
        var terrain = new TerrainType[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                biome[i, j] = BiomeAt(ox + i, oz + j, sites);
                terrain[i, j] = BaseTerrainAt(ox + i, oz + j, biome[i, j]);
            }
        }

        // Coasts: deep water within 2 cells of land is shallow.
        var coast = (TerrainType[,])terrain.Clone();
        for (int i = 2; i < n - 2; i++)
        {
            for (int j = 2; j < n - 2; j++)
            {
                if (biome[i, j] != BiomeType.Ocean)
                {
                    continue;
                }
                bool nearLand = false;
                for (int di = -2; di <= 2 && !nearLand; di++)
                {
                    for (int dj = -2; dj <= 2; dj++)
                    {
                        if (biome[i + di, j + dj] != BiomeType.Ocean)
                        {
                            nearLand = true;
                            break;
                        }
                    }
                }
                if (nearLand)
                {
                    coast[i, j] = TerrainType.ShallowWater;
                }
            }
        }
        terrain = coast;

        var feature = new MapFeature[n, n];
        var riverSign = new sbyte[n + 1, n + 1];
        for (int i = 2; i < n - 2; i++)
        {
            for (int j = 2; j < n - 2; j++)
            {
                int x = ox + i;
                int z = oz + j;
                if (MountainAt(x, z, biome[i, j], terrain[i, j]))
                {
                    feature[i, j] = MapFeature.Mountain;
                }
                else if (TerrainDefs.For(terrain[i, j]).TakesFeatures && !InSpawnClearing(x, z) && RiverLineAt(x, z, riverSign, ox, oz))
                {
                    feature[i, j] = MapFeature.River;
                }
            }
        }

        var chunk = new Chunk(chunkX, chunkZ);
        for (int li = 0; li < Chunk.Size; li++)
        {
            for (int lj = 0; lj < Chunk.Size; lj++)
            {
                int i = li + Pad;
                int j = lj + Pad;
                chunk.Biomes[li, lj] = biome[i, j];
                chunk.Terrain[li, lj] = terrain[i, j];
                chunk.Features[li, lj] = feature[i, j];
                if (feature[i, j] == MapFeature.River)
                {
                    chunk.RiverFlow[li, lj] = FlowOf(ox, oz, i, j, terrain, feature);
                }
            }
        }

        return new GeneratedChunk(chunk, Deposits(ox, oz, biome, terrain, feature));
    }

    /// <summary>
    /// A river cell flows to its lowest neighbor that is a river cell or water (water
    /// counts as lowest), when that is lower than the cell; with no lower such
    /// neighbor it flows to its lowest river neighbor; with none, toward its lowest
    /// neighbor, which leaves it a Pool.
    /// </summary>
    private Facing FlowOf(int ox, int oz, int i, int j, TerrainType[,] terrain, MapFeature[,] feature)
    {
        float here = ElevationAt(ox + i, oz + j);
        Facing? bestWet = null;
        float bestWetElevation = float.MaxValue;
        Facing? bestRiver = null;
        float bestRiverElevation = float.MaxValue;
        Facing lowest = Facing.North;
        float lowestElevation = float.MaxValue;
        foreach (Facing edge in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
        {
            (int dx, int dz) = Simulation.EdgeOffset(edge);
            int ni = i + dx;
            int nj = j + dz;
            bool water = TerrainDefs.IsWater(terrain[ni, nj]);
            bool river = feature[ni, nj] == MapFeature.River;
            float elevation = water ? float.MinValue : ElevationAt(ox + ni, oz + nj);
            if ((water || river) && elevation < bestWetElevation)
            {
                bestWetElevation = elevation;
                bestWet = edge;
            }
            if (river && elevation < bestRiverElevation)
            {
                bestRiverElevation = elevation;
                bestRiver = edge;
            }
            if (elevation < lowestElevation)
            {
                lowestElevation = elevation;
                lowest = edge;
            }
        }
        if (bestWet is Facing down && bestWetElevation < here)
        {
            return down;
        }
        return bestRiver ?? lowest;
    }

    /// <summary>
    /// A cell is a deposit candidate when its biome's density roll passes on a
    /// walkable, featureless cell away from the spawn; a candidate becomes a field
    /// only if no neighboring candidate rolled lower, so fields never touch.
    /// </summary>
    private List<(ResourceType, int, int)> Deposits(int ox, int oz, BiomeType[,] biome, TerrainType[,] terrain, MapFeature[,] feature)
    {
        int n = biome.GetLength(0);
        var roll = new float[n, n];
        for (int i = Pad - 1; i <= Pad + Chunk.Size; i++)
        {
            for (int j = Pad - 1; j <= Pad + Chunk.Size; j++)
            {
                int x = ox + i;
                int z = oz + j;
                float r = Noise.Hash01(Seed, SaltDeposit, x, z);
                bool candidate = r < BiomeDefs.For(biome[i, j]).FieldDensity
                    && TerrainDefs.For(terrain[i, j]).Walkable
                    && feature[i, j] == MapFeature.None
                    && !NearSpawn(x, z);
                roll[i, j] = candidate ? r : float.NaN;
            }
        }

        var fields = new List<(ResourceType, int, int)>();
        for (int i = Pad; i < Pad + Chunk.Size; i++)
        {
            for (int j = Pad; j < Pad + Chunk.Size; j++)
            {
                float r = roll[i, j];
                if (float.IsNaN(r) || !LowestAmongNeighbors(roll, i, j))
                {
                    continue;
                }
                int x = ox + i;
                int z = oz + j;
                if (BiomeDefs.For(biome[i, j]).PickDeposit(Noise.Hash01(Seed, SaltDepositType, x, z)) is ResourceType type)
                {
                    fields.Add((type, x, z));
                }
            }
        }
        return fields;
    }

    private static bool LowestAmongNeighbors(float[,] roll, int i, int j)
    {
        float r = roll[i, j];
        for (int di = -1; di <= 1; di++)
        {
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0)
                {
                    continue;
                }
                float other = roll[i + di, j + dj];
                if (!float.IsNaN(other) && (other < r || (other == r && (di < 0 || (di == 0 && dj < 0)))))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
