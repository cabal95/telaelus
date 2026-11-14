using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim;

/// <summary>
/// The world's storage: chunks of map cells, and which kind of world they make up.
/// A bounded world (the test map or the empty map) is one chunk with a fixed
/// rectangle in bounds; a generated world creates each chunk the first time
/// anything needs it and keeps the chunks around the player generated.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Chunks within this many chunks of the player's are kept generated.</summary>
    public const int LoadRadius = 1;

    private readonly Dictionary<(int ChunkX, int ChunkZ), Chunk> _chunks = new();
    private WorldGenerator? _generator;

    /// <summary>What kind of world this is, and its seed and planet when generated.</summary>
    public WorldSettings World { get; private set; } = WorldSettings.TestMap;

    /// <summary>The generator of a generated world, or null for a bounded one.</summary>
    public WorldGenerator? Generator => _generator;

    /// <summary>Raised when a chunk is generated (its chunk coordinates), so views can draw it.</summary>
    public event Action<int, int>? ChunkGenerated;

    /// <summary>How many chunks exist (generated, or the bounded world's one).</summary>
    public int ChunkCount => _chunks.Count;

    public bool IsChunkGenerated(int chunkX, int chunkZ)
    {
        return _chunks.ContainsKey((chunkX, chunkZ));
    }

    /// <summary>Where the player starts: the bounded map's center, or the generator's spawn point.</summary>
    public Vector3 SpawnPoint => _generator?.SpawnPoint
        ?? new Vector3(WorldSettings.BoundedSize * FineCellsPerMapCell * 0.5f, 0f, WorldSettings.BoundedSize * FineCellsPerMapCell * 0.5f);

    /// <summary>The chunk containing a world point.</summary>
    public static (int ChunkX, int ChunkZ) ChunkAt(Vector3 point)
    {
        (int x, int z) = CellAt(point.X, point.Z);
        return (Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z)));
    }

    /// <summary>
    /// Replaces the whole world: clears every chunk, field, building, the stockpile,
    /// and launches, then builds the new world and puts the player at its spawn.
    /// </summary>
    private void ResetWorld(WorldSettings settings)
    {
        World = settings;
        _chunks.Clear();
        _fields.Clear();
        _fieldsByMapCell.Clear();
        _buildings.Clear();
        _occupied.Clear();
        _stockpile.Clear();
        _unlocked.Clear();
        HarvestTarget = null;
        HarvestProgress = 0f;
        ShipsLaunched = 0;
        _revealedFrom = null;
        ExploredCount = 0;
        _exploredBounds = null;
        _generator = settings.Kind == WorldKind.Generated ? new WorldGenerator(settings.Seed, settings.Planet!) : null;

        if (settings.IsBounded)
        {
            _chunks[(0, 0)] = new Chunk(0, 0);
            if (settings.Kind == WorldKind.TestMap)
            {
                GenerateTerrain();
                SpawnInitialFields();
            }
        }

        Player.Position = SpawnPoint;
        EnsureChunksAroundPlayer();
        RevealAroundPlayer();
    }

    /// <summary>Generates the player's chunk and every chunk within <see cref="LoadRadius"/> of it.</summary>
    private void EnsureChunksAroundPlayer()
    {
        if (_generator is null)
        {
            return;
        }
        (int cx, int cz) = ChunkAt(Player.Position);
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
        {
            for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                GetOrGenerateChunk(cx + dx, cz + dz);
            }
        }
    }

    private Chunk GetOrGenerateChunk(int chunkX, int chunkZ)
    {
        if (_chunks.TryGetValue((chunkX, chunkZ), out Chunk? chunk))
        {
            return chunk;
        }
        if (_generator is null)
        {
            throw new InvalidOperationException($"chunk ({chunkX}, {chunkZ}) is outside the bounded world");
        }

        GeneratedChunk generated = _generator.Generate(chunkX, chunkZ);
        _chunks[(chunkX, chunkZ)] = generated.Chunk;
        foreach ((ResourceType type, int mapX, int mapZ) in generated.Fields)
        {
            AddField(type, mapX, mapZ);
        }
        ChunkGenerated?.Invoke(chunkX, chunkZ);
        return generated.Chunk;
    }

    /// <summary>The chunk holding an in-bounds map cell (generated on demand) and the cell's index in it.</summary>
    private (Chunk Chunk, int X, int Z) Cell(int mapX, int mapZ)
    {
        int cx = Chunk.Of(mapX);
        int cz = Chunk.Of(mapZ);
        return (GetOrGenerateChunk(cx, cz), mapX - Chunk.Origin(cx), mapZ - Chunk.Origin(cz));
    }
}
