using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>
/// Which resources the player has discovered. A deposit is discovered when a field of it first
/// lies in an explored map cell, Sand when a Sand cell is first explored, and Water when a
/// ShallowWater or DeepWater cell or a river is; anything is discovered when storage first holds
/// some of it. Discovery is world state: it never clears during play and resets with the world.
/// </summary>
public sealed partial class Simulation
{
    private readonly HashSet<ResourceType> _discovered = new();

    public bool IsDiscovered(ResourceType type)
    {
        return _discovered.Contains(type);
    }

    private void Discover(ResourceType type)
    {
        if (_discovered.Add(type))
        {
            Raise(new SimEvent(SimEventKind.Discovered)
            {
                Resource = type
            });
        }
    }

    /// <summary>Discovers what an explored map cell shows: its field's deposit and its Sand or Water.</summary>
    private void DiscoverCell(int mapX, int mapZ)
    {
        if (_fieldsByMapCell.TryGetValue((mapX, mapZ), out ResourceField? field))
        {
            DiscoverField(field);
        }
        TerrainType terrain = MapTerrainAt(mapX, mapZ);
        if (terrain == TerrainType.Sand)
        {
            Discover(ResourceType.Sand);
        }
        if (terrain is TerrainType.ShallowWater or TerrainType.DeepWater || MapFeatureAt(mapX, mapZ) == MapFeature.River)
        {
            Discover(ResourceType.Water);
        }
    }

    /// <summary>Discovers a cell again after its terrain or feature changes, if it is explored.</summary>
    private void DiscoverIfExplored(int mapX, int mapZ)
    {
        if (IsExplored(mapX, mapZ))
        {
            DiscoverCell(mapX, mapZ);
        }
    }

    /// <summary>In a world without fog every cell is explored, so a new chunk's cells are discovered at once.</summary>
    private void DiscoverChunkWithoutFog(Chunk chunk)
    {
        if (World.Fog)
        {
            return;
        }
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int mapX = Chunk.Origin(chunk.ChunkX) + lx;
                int mapZ = Chunk.Origin(chunk.ChunkZ) + lz;
                if (MapInBounds(mapX, mapZ))
                {
                    DiscoverCell(mapX, mapZ);
                }
            }
        }
    }
}
