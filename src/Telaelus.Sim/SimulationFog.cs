using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// Fog of war, reveal once: in a world with fog, a map cell is explored once the player has
/// seen it (its center within <see cref="SightRadius"/> of the player) and stays explored for
/// good. Explored is world state, shared by everyone. In a world without fog every in-bounds
/// cell counts as explored.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>How far the player sees, in world units (6 map cells).</summary>
    public const float SightRadius = 24f;

    // The map cell the player last revealed from: the circle only changes when they enter another.
    private (int X, int Z)? _revealedFrom;

    /// <summary>How many map cells are explored (counted only in a world with fog).</summary>
    public int ExploredCount { get; private set; }

    private (int MinX, int MinZ, int MaxX, int MaxZ)? _exploredBounds;

    /// <summary>
    /// The smallest rectangle of map cells holding every explored cell (inclusive): in a world
    /// without fog, a bounded map's whole rectangle, and null for a generated one. Display only
    /// (the free camera keeps its view inside it).
    /// </summary>
    public (int MinX, int MinZ, int MaxX, int MaxZ)? ExploredBounds => World.Fog
        ? _exploredBounds
        : World.IsBounded ? (0, 0, MapWidth - 1, MapHeight - 1) : null;

    private void MarkExplored(int mapX, int mapZ)
    {
        ExploredCount++;
        // A collector a truck could not reach through the fog may be reachable now.
        _unreachable.Clear();
        _exploredBounds = _exploredBounds is (int minX, int minZ, int maxX, int maxZ)
            ? (Math.Min(minX, mapX), Math.Min(minZ, mapZ), Math.Max(maxX, mapX), Math.Max(maxZ, mapZ))
            : (mapX, mapZ, mapX, mapZ);
        DiscoverCell(mapX, mapZ);
    }

    /// <summary>Raised with the map cells that just became explored, so views redraw only those.</summary>
    public event Action<IReadOnlyList<(int MapX, int MapZ)>>? CellsRevealed;

    /// <summary>Whether a map cell is explored: in bounds, and seen (or the world has no fog).</summary>
    public bool IsExplored(int mapX, int mapZ)
    {
        if (!MapInBounds(mapX, mapZ))
        {
            return false;
        }
        if (!World.Fog)
        {
            return true;
        }
        if (!_chunks.TryGetValue((Chunk.Of(mapX), Chunk.Of(mapZ)), out Chunk? chunk))
        {
            return false;
        }
        return chunk.Explored[mapX - Chunk.Origin(chunk.ChunkX), mapZ - Chunk.Origin(chunk.ChunkZ)];
    }

    /// <summary>Explores every map cell within sight, once per map cell the player enters.</summary>
    private void RevealAroundPlayer()
    {
        if (!World.Fog)
        {
            return;
        }
        Vector3 position = Player.Position;
        (int x, int z) = CellAt(position.X, position.Z);
        (int X, int Z) from = (MapCellOf(x), MapCellOf(z));
        if (from == _revealedFrom)
        {
            return;
        }
        _revealedFrom = from;

        int reach = Mathf.CeilToInt(SightRadius / FineCellsPerMapCell) + 1;
        var revealed = new List<(int, int)>();
        for (int mapX = from.X - reach; mapX <= from.X + reach; mapX++)
        {
            for (int mapZ = from.Z - reach; mapZ <= from.Z + reach; mapZ++)
            {
                if (!MapInBounds(mapX, mapZ))
                {
                    continue;
                }
                Vector3 center = MapCellCenter(mapX, mapZ);
                float dx = center.X - position.X;
                float dz = center.Z - position.Z;
                if (dx * dx + dz * dz > SightRadius * SightRadius)
                {
                    continue;
                }
                (Chunk chunk, int lx, int lz) = Cell(mapX, mapZ);
                if (!chunk.Explored[lx, lz])
                {
                    chunk.Explored[lx, lz] = true;
                    MarkExplored(mapX, mapZ);
                    revealed.Add((mapX, mapZ));
                }
            }
        }
        if (revealed.Count > 0)
        {
            CellsRevealed?.Invoke(revealed);
        }
    }

    /// <summary>Explores every cell of every existing chunk (tests and debugging).</summary>
    private void RevealAllExisting()
    {
        if (!World.Fog)
        {
            return;
        }
        foreach (Chunk chunk in _chunks.Values)
        {
            for (int lx = 0; lx < Chunk.Size; lx++)
            {
                for (int lz = 0; lz < Chunk.Size; lz++)
                {
                    int mapX = Chunk.Origin(chunk.ChunkX) + lx;
                    int mapZ = Chunk.Origin(chunk.ChunkZ) + lz;
                    if (MapInBounds(mapX, mapZ) && !chunk.Explored[lx, lz])
                    {
                        chunk.Explored[lx, lz] = true;
                        MarkExplored(mapX, mapZ);
                    }
                }
            }
        }
    }
}
