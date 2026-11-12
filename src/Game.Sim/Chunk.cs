namespace Game.Sim;

/// <summary>
/// One chunk of the world: a square of <see cref="Size"/> x <see cref="Size"/> map
/// cells holding each cell's terrain, biome, feature, and river flow. Chunk (0, 0)
/// holds map cells 0 to Size - 1 on each axis; chunk coordinates are map cell
/// coordinates divided by Size, rounded down, so they run negative too.
/// </summary>
public sealed class Chunk
{
    public const int Size = 64;

    public int ChunkX { get; }
    public int ChunkZ { get; }

    internal readonly TerrainType[,] Terrain = new TerrainType[Size, Size];
    internal readonly BiomeType[,] Biomes = new BiomeType[Size, Size];
    internal readonly MapFeature[,] Features = new MapFeature[Size, Size];
    internal readonly Facing?[,] RiverFlow = new Facing?[Size, Size];

    public Chunk(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    /// <summary>Terrain of a cell, by its index within the chunk (0 to Size - 1).</summary>
    public TerrainType TerrainAt(int localX, int localZ)
    {
        return Terrain[localX, localZ];
    }

    public BiomeType BiomeAt(int localX, int localZ)
    {
        return Biomes[localX, localZ];
    }

    public MapFeature FeatureAt(int localX, int localZ)
    {
        return Features[localX, localZ];
    }

    public Facing? RiverFlowAt(int localX, int localZ)
    {
        return RiverFlow[localX, localZ];
    }

    /// <summary>The chunk coordinate containing a map cell coordinate (floor division).</summary>
    public static int Of(int mapCoordinate)
    {
        int quotient = mapCoordinate / Size;
        return mapCoordinate % Size < 0 ? quotient - 1 : quotient;
    }

    /// <summary>The first map cell coordinate of a chunk coordinate.</summary>
    public static int Origin(int chunkCoordinate)
    {
        return chunkCoordinate * Size;
    }
}
