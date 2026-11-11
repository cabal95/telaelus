using Godot;

namespace Game.Sim;

/// <summary>
/// The world's per-map-cell layers: terrain (the ground), biome (what kind of
/// place it is), and feature (a mountain or river on top of the terrain), plus
/// the walk and build rules read from them and the fixed test map.
/// </summary>
public sealed partial class Simulation
{
    private readonly TerrainType[,] _terrain;
    private readonly BiomeType[,] _biomes;
    private readonly MapFeature[,] _features;
    private readonly Facing?[,] _riverFlow;

    // ---- Layer reads -----------------------------------------------------

    /// <summary>Terrain of a map cell; out-of-bounds reads return Grass.</summary>
    public TerrainType MapTerrainAt(int mapX, int mapZ)
    {
        return MapInBounds(mapX, mapZ) ? _terrain[mapX, mapZ] : TerrainType.Grass;
    }

    /// <summary>Terrain of a fine cell: its map cell's terrain (features change no terrain).</summary>
    public TerrainType TerrainAt(int x, int z)
    {
        return MapTerrainAt(MapCellOf(x), MapCellOf(z));
    }

    /// <summary>Biome of a map cell; out-of-bounds reads return Grassland.</summary>
    public BiomeType MapBiomeAt(int mapX, int mapZ)
    {
        return MapInBounds(mapX, mapZ) ? _biomes[mapX, mapZ] : BiomeType.Grassland;
    }

    /// <summary>Biome of a fine cell: its map cell's biome.</summary>
    public BiomeType BiomeAt(int x, int z)
    {
        return MapBiomeAt(MapCellOf(x), MapCellOf(z));
    }

    /// <summary>Feature of a map cell; out-of-bounds reads return None.</summary>
    public MapFeature MapFeatureAt(int mapX, int mapZ)
    {
        return MapInBounds(mapX, mapZ) ? _features[mapX, mapZ] : MapFeature.None;
    }

    /// <summary>Feature of a fine cell: its map cell's feature.</summary>
    public MapFeature FeatureAt(int x, int z)
    {
        return MapFeatureAt(MapCellOf(x), MapCellOf(z));
    }

    // ---- Rivers ----------------------------------------------------------

    /// <summary>The edge a river cell's water leaves by, or null when the map cell is not a river.</summary>
    public Facing? RiverFlowAt(int mapX, int mapZ)
    {
        return MapInBounds(mapX, mapZ) ? _riverFlow[mapX, mapZ] : null;
    }

    /// <summary>
    /// Whether a river cell connects on an edge: on its flow edge when the
    /// neighbor there is a river, water, or off the map; on any edge whose
    /// neighbor is a river flowing into it. False for a cell that is not a river.
    /// </summary>
    public bool RiverConnects(int mapX, int mapZ, Facing edge)
    {
        Facing? flow = RiverFlowAt(mapX, mapZ);
        if (flow is null)
        {
            return false;
        }

        (int dx, int dz) = EdgeOffset(edge);
        int nx = mapX + dx;
        int nz = mapZ + dz;
        if (flow == edge)
        {
            return !MapInBounds(nx, nz)
                || RiverFlowAt(nx, nz) is not null
                || TerrainDefs.IsWater(MapTerrainAt(nx, nz));
        }
        return RiverFlowAt(nx, nz) == Opposite(edge);
    }

    /// <summary>The shape of a river cell from its connections; Pool for a cell with none (or not a river).</summary>
    public RiverShape RiverShapeAt(int mapX, int mapZ)
    {
        bool north = RiverConnects(mapX, mapZ, Facing.North);
        bool east = RiverConnects(mapX, mapZ, Facing.East);
        bool south = RiverConnects(mapX, mapZ, Facing.South);
        bool west = RiverConnects(mapX, mapZ, Facing.West);
        int count = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);
        return count switch
        {
            0 => RiverShape.Pool,
            1 => RiverShape.End,
            2 => (north && south) || (east && west) ? RiverShape.Straight : RiverShape.Bend,
            3 => RiverShape.Junction,
            _ => RiverShape.Cross
        };
    }

    /// <summary>
    /// Whether a fine cell is river channel: the middle 2 x 2 of a river map cell,
    /// plus a 2-wide arm from the center to each connected edge.
    /// </summary>
    public bool IsRiverChannel(int x, int z)
    {
        int mapX = MapCellOf(x);
        int mapZ = MapCellOf(z);
        if (RiverFlowAt(mapX, mapZ) is null)
        {
            return false;
        }

        int localX = x - mapX * FineCellsPerMapCell;
        int localZ = z - mapZ * FineCellsPerMapCell;
        bool middleX = localX is 1 or 2;
        bool middleZ = localZ is 1 or 2;
        if (middleX && middleZ)
        {
            return true;
        }
        if (middleX && localZ == 0)
        {
            return RiverConnects(mapX, mapZ, Facing.North);
        }
        if (middleX && localZ == FineCellsPerMapCell - 1)
        {
            return RiverConnects(mapX, mapZ, Facing.South);
        }
        if (middleZ && localX == FineCellsPerMapCell - 1)
        {
            return RiverConnects(mapX, mapZ, Facing.East);
        }
        if (middleZ && localX == 0)
        {
            return RiverConnects(mapX, mapZ, Facing.West);
        }
        return false;
    }

    /// <summary>The map-cell step across an edge: North is -Z, East +X, South +Z, West -X.</summary>
    public static (int Dx, int Dz) EdgeOffset(Facing edge)
    {
        return edge switch
        {
            Facing.North => (0, -1),
            Facing.East => (1, 0),
            Facing.South => (0, 1),
            _ => (-1, 0)
        };
    }

    private static Facing Opposite(Facing edge)
    {
        return (Facing)(((int)edge + 2) % 4);
    }

    // ---- Rules -----------------------------------------------------------

    /// <summary>Whether a feature blocks a fine cell: a mountain's whole map cell, or a river's channel.</summary>
    private bool FeatureBlocks(int x, int z)
    {
        return FeatureAt(x, z) switch
        {
            MapFeature.Mountain => true,
            MapFeature.River => IsRiverChannel(x, z),
            _ => false
        };
    }

    /// <summary>Buildable terrain with no blocking feature; gates building placement.</summary>
    public bool IsBuildable(int x, int z)
    {
        return TerrainDefs.For(TerrainAt(x, z)).Buildable && !FeatureBlocks(x, z);
    }

    /// <summary>
    /// Walkable = walkable terrain, no blocking feature, no resource field, and no
    /// building on the fine cell. A field blocks all its cells until it depletes and
    /// is removed; a building blocks for as long as it stands.
    /// </summary>
    public bool IsWalkable(int x, int z)
    {
        return TerrainDefs.For(TerrainAt(x, z)).Walkable
            && !FeatureBlocks(x, z)
            && FieldAt(x, z) is null
            && !_occupied.Contains((x, z));
    }

    private bool IsWalkableAt(float worldX, float worldZ)
    {
        (int x, int z) = CellAt(worldX, worldZ);
        return IsWalkable(x, z);
    }

    /// <summary>The speed multiplier of the terrain under a world point.</summary>
    private float TerrainSpeedAt(float worldX, float worldZ)
    {
        (int x, int z) = CellAt(worldX, worldZ);
        return TerrainDefs.For(TerrainAt(x, z)).Speed;
    }

    // ---- Fixed test map --------------------------------------------------

    private void Paint(int fromX, int toX, int fromZ, int toZ, BiomeType biome, TerrainType terrain)
    {
        for (int mapX = fromX; mapX <= toX; mapX++)
        {
            for (int mapZ = fromZ; mapZ <= toZ; mapZ++)
            {
                _biomes[mapX, mapZ] = biome;
                _terrain[mapX, mapZ] = terrain;
            }
        }
    }

    private void SetMountain(int mapX, int mapZ)
    {
        _features[mapX, mapZ] = MapFeature.Mountain;
    }

    private void SetRiver(int mapX, int mapZ, Facing flow)
    {
        _features[mapX, mapZ] = MapFeature.River;
        _riverFlow[mapX, mapZ] = flow;
    }

    /// <summary>
    /// The fixed test map (x east, z south), showing every biome. Everything not
    /// painted is Grassland grass, including the center map cells around the spawn.
    /// </summary>
    private void GenerateTerrain()
    {
        // Forest (north-west): grass, dense with large Wood fields.
        Paint(0, 5, 0, 6, BiomeType.Forest, TerrainType.Grass);

        // Swamp (north): mud with two pools.
        Paint(6, 10, 0, 2, BiomeType.Swamp, TerrainType.Mud);
        Paint(8, 9, 1, 1, BiomeType.Swamp, TerrainType.ShallowWater);

        // The pond, in grassland.
        Paint(9, 11, 5, 6, BiomeType.Grassland, TerrainType.ShallowWater);

        // A river from the swamp, through grass, into the pond.
        for (int mapZ = 0; mapZ <= 3; mapZ++)
        {
            SetRiver(7, mapZ, Facing.South);
        }
        SetRiver(7, 4, Facing.East);
        SetRiver(8, 4, Facing.East);
        SetRiver(9, 4, Facing.South);

        // Tundra (north-east): snow with snow mountains.
        Paint(13, 15, 0, 3, BiomeType.Tundra, TerrainType.Snow);
        SetMountain(14, 1);
        SetMountain(15, 1);

        // Desert (east): sand, a rock outcrop, and a river running off the map edge.
        Paint(13, 15, 5, 12, BiomeType.Desert, TerrainType.Sand);
        Paint(14, 14, 8, 8, BiomeType.Desert, TerrainType.Rock);
        SetRiver(13, 10, Facing.East);
        SetRiver(14, 10, Facing.East);
        SetRiver(15, 10, Facing.East);

        // Highlands (west): rock with a mountain cluster.
        Paint(0, 5, 8, 12, BiomeType.Highlands, TerrainType.Rock);
        SetMountain(4, 9);
        SetMountain(4, 10);
        SetMountain(5, 9);

        // A lone mountain on grass.
        SetMountain(11, 12);

        // Ocean (south-west): a shallow shore, then deep water.
        Paint(0, 9, 13, 13, BiomeType.Ocean, TerrainType.ShallowWater);
        Paint(0, 9, 14, 15, BiomeType.Ocean, TerrainType.DeepWater);

        // Volcanic (south-east): rock with lava.
        Paint(12, 15, 13, 15, BiomeType.Volcanic, TerrainType.Rock);
        Paint(13, 14, 14, 14, BiomeType.Volcanic, TerrainType.Lava);
    }

    /// <summary>The test map's fields; each takes its biome's amount (1150 Ore, 1750 Wood in all).</summary>
    private void SpawnInitialFields()
    {
        (ResourceType Type, int MapX, int MapZ)[] placements =
        {
            (ResourceType.Ore, 10, 9),
            (ResourceType.Ore, 11, 10),
            (ResourceType.Ore, 9, 11),
            (ResourceType.Ore, 4, 12),
            (ResourceType.Wood, 3, 4),
            (ResourceType.Wood, 4, 3),
            (ResourceType.Wood, 2, 5),
            (ResourceType.Wood, 12, 3)
        };

        foreach ((ResourceType type, int mapX, int mapZ) in placements)
        {
            AddField(type, mapX, mapZ);
        }
    }
}
