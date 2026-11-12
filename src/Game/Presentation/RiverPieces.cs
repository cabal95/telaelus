using System;
using System.Collections.Generic;
using Game.Sim;

namespace Game.Presentation;

/// <summary>
/// Picks and orients the river piece for a river cell. Each shape is authored with
/// canonical connections (end: North; straight: North and South; bend: North and
/// East; junction: North, East, and South) and turned in 90-degree steps until it
/// matches the cell's connections. The piece covers its whole cell, banks included,
/// so its family follows the bank terrain.
/// </summary>
public static class RiverPieces
{
    private static readonly Facing[] Edges = { Facing.North, Facing.East, Facing.South, Facing.West };

    private static readonly Dictionary<RiverShape, Facing[]> Canonical = new()
    {
        [RiverShape.Pool] = Array.Empty<Facing>(),
        [RiverShape.End] = new[] { Facing.North },
        [RiverShape.Straight] = new[] { Facing.North, Facing.South },
        [RiverShape.Bend] = new[] { Facing.North, Facing.East },
        [RiverShape.Junction] = new[] { Facing.North, Facing.East, Facing.South },
        [RiverShape.Cross] = new[] { Facing.North, Facing.East, Facing.South, Facing.West }
    };

    /// <summary>The model path and yaw for the river piece at a map cell with the given bank terrain.</summary>
    public static (string Path, float Yaw) Model(Simulation sim, int mapX, int mapZ, TerrainType bank)
    {
        RiverShape shape = sim.RiverShapeAt(mapX, mapZ);
        int connections = 0;
        foreach (Facing edge in Edges)
        {
            if (sim.RiverConnects(mapX, mapZ, edge))
            {
                connections |= Bit(edge);
            }
        }

        string path = $"terrain/river_{shape.ToString().ToLowerInvariant()}_{TerrainTiles.FileName(bank)}";
        return (path, 90f * Turns(shape, connections));
    }

    /// <summary>How many +90-degree turns map the shape's canonical connections onto <paramref name="connections"/>.</summary>
    public static int Turns(RiverShape shape, int connections)
    {
        int mask = 0;
        foreach (Facing edge in Canonical[shape])
        {
            mask |= Bit(edge);
        }
        for (int turns = 0; turns < 4; turns++)
        {
            if (mask == connections)
            {
                return turns;
            }
            mask = TurnOnce(mask);
        }
        throw new InvalidOperationException($"River connections {connections} do not fit shape {shape}.");
    }

    private static int Bit(Facing edge)
    {
        return 1 << (int)edge;
    }

    // A +90-degree yaw turns North to West, West to South, South to East, and East to North.
    private static int TurnOnce(int mask)
    {
        int result = 0;
        foreach (Facing edge in Edges)
        {
            if ((mask & Bit(edge)) != 0)
            {
                Facing turned = edge switch
                {
                    Facing.North => Facing.West,
                    Facing.West => Facing.South,
                    Facing.South => Facing.East,
                    _ => Facing.North
                };
                result |= Bit(turned);
            }
        }
        return result;
    }
}
