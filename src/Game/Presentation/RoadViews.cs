using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Roads and bridges on the map, rebuilt whenever the sim's <see cref="Simulation.RoadVersion"/>
/// changes. Each road tile takes the piece that matches which of its four edges join another road or
/// bridge tile (patch, end, straight, corner, tee, crossing), turned in quarter turns to fit. Each
/// bridge tile takes a deck along its span, its end tiles a ramp down to the bank, and every other
/// tile a pier.
/// </summary>
public partial class RoadViews : Node3D
{
    // Edges in order North, East, South, West, as tile steps.
    private static readonly (int Dx, int Dz)[] Edges = { (0, -2), (2, 0), (0, 2), (-2, 0) };

    // Each road piece and the edges it is authored open on (bit 0 North, 1 East, 2 South, 3 West).
    private static readonly (string Model, int Open)[] Pieces =
    {
        ("infrastructure/road_patch", 0b0000),
        ("infrastructure/road_end", 0b0001),
        ("infrastructure/road_straight", 0b0101),
        ("infrastructure/road_corner", 0b0011),
        ("infrastructure/road_tee", 0b0111),
        ("infrastructure/road_cross", 0b1111)
    };

    private int _version = -1;

    public void Sync(Simulation sim)
    {
        if (sim.RoadVersion == _version)
        {
            return;
        }
        _version = sim.RoadVersion;
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        var bridgeTiles = new HashSet<(int X, int Z)>();
        foreach (Bridge bridge in sim.Bridges)
        {
            bridgeTiles.UnionWith(bridge.Tiles);
        }
        foreach ((int X, int Z) tile in sim.RoadTiles)
        {
            int mask = 0;
            for (int e = 0; e < 4; e++)
            {
                (int nx, int nz) = (tile.X + Edges[e].Dx, tile.Z + Edges[e].Dz);
                if (sim.RoadTiles.Contains((nx, nz)) || bridgeTiles.Contains((nx, nz)))
                {
                    mask |= 1 << e;
                }
            }
            (string model, int turns) = PieceFor(mask);
            Add(model, tile, turns, 0f);
        }
        foreach (Bridge bridge in sim.Bridges)
        {
            AddBridge(bridge);
        }
    }

    /// <summary>The piece and the quarter turns (each turning North to West) that open exactly the given edges.</summary>
    public static (string Model, int Turns) PieceFor(int mask)
    {
        foreach ((string model, int open) in Pieces)
        {
            for (int turns = 0; turns < 4; turns++)
            {
                if (Turn(open, turns) == mask)
                {
                    return (model, turns);
                }
            }
        }
        return (Pieces[0].Model, 0);
    }

    /// <summary>Edges after quarter turns: each turn takes North to West, East to North, and so on.</summary>
    private static int Turn(int edges, int turns)
    {
        int result = 0;
        for (int e = 0; e < 4; e++)
        {
            if ((edges & (1 << e)) != 0)
            {
                result |= 1 << (((e - turns) % 4 + 4) % 4);
            }
        }
        return result;
    }

    private void AddBridge(Bridge bridge)
    {
        // Decks are authored running North-South; a span along X turns a quarter.
        int along = bridge.AlongX ? 1 : 0;
        int count = bridge.Tiles.Count;
        for (int i = 0; i < count; i++)
        {
            (int X, int Z) tile = bridge.Tiles[i];
            bool first = i == 0;
            bool last = i == count - 1;
            if (count > 1 && (first || last))
            {
                // The ramp is authored low end North; turn it so its low end faces the land beyond.
                (int X, int Z) beyond = first ? bridge.Tiles[1] : bridge.Tiles[count - 2];
                int outward = EdgeToward(tile, beyond) is int inward ? (inward + 2) % 4 : 0;
                Add("infrastructure/bridge_ramp", tile, (4 - outward) % 4, 0f);
            }
            else
            {
                Add("infrastructure/bridge_deck", tile, along, 0f);
            }
            if (i % 2 == 1 || count == 1)
            {
                Add("infrastructure/bridge_pier", tile, along, 0f);
            }
        }
    }

    /// <summary>Which edge of a tile faces a neighbouring tile (0 North, 1 East, 2 South, 3 West), or null.</summary>
    private static int? EdgeToward((int X, int Z) tile, (int X, int Z) other)
    {
        for (int e = 0; e < 4; e++)
        {
            if (tile.X + Edges[e].Dx == other.X && tile.Z + Edges[e].Dz == other.Z)
            {
                return e;
            }
        }
        return null;
    }

    private void Add(string model, (int X, int Z) tile, int turns, float y)
    {
        Node3D piece = ModelLibrary.Instance(model);
        piece.Position = new Vector3(tile.X + 1f, y, tile.Z + 1f);
        piece.Rotation = new Vector3(0f, turns * Mathf.Pi / 2f, 0f);
        AddChild(piece);
    }
}
