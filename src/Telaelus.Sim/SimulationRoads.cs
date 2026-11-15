using System;
using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim;

/// <summary>A bridge span: its 2 x 2 tiles (corner cells, in order from one end) and whether it runs along X.</summary>
public sealed record Bridge(int Id, IReadOnlyList<(int X, int Z)> Tiles, bool AlongX);

/// <summary>
/// Roads and bridges: 2 x 2 tiles on the even grid (a tile is named by its corner cell, both
/// coordinates even). A road tile paves four cells of walkable land; a bridge is a straight span of
/// tiles over water that makes its cells walkable. Both are faster to cross (<see cref="RoadSpeed"/>
/// in place of the terrain's speed), are laid and removed in lines, cost per cell, refund half, and
/// keep buildings off them.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The speed on a road or bridge, in place of the terrain's.</summary>
    public const float RoadSpeed = 1.5f;

    /// <summary>The most tiles a bridge spans.</summary>
    public const int MaxBridgeTiles = 6;

    private readonly HashSet<(int X, int Z)> _roadTiles = new();
    private readonly HashSet<(int X, int Z)> _roadCells = new();
    private readonly List<Bridge> _bridges = new();
    private readonly Dictionary<(int X, int Z), Bridge> _bridgeCells = new();
    private int _nextBridgeId = 1;

    /// <summary>The road tiles (their corner cells).</summary>
    public IReadOnlyCollection<(int X, int Z)> RoadTiles => _roadTiles;

    public IReadOnlyList<Bridge> Bridges => _bridges;

    /// <summary>Counts every change to roads or bridges, so views know to rebuild.</summary>
    public int RoadVersion { get; private set; }

    /// <summary>The tile (corner cell) holding a fine cell.</summary>
    public static (int X, int Z) TileOf(int x, int z)
    {
        return (x - Mod2(x), z - Mod2(z));
    }

    private static int Mod2(int v)
    {
        return ((v % 2) + 2) % 2;
    }

    private static IEnumerable<(int X, int Z)> CellsOf((int X, int Z) tile)
    {
        yield return (tile.X, tile.Z);
        yield return (tile.X + 1, tile.Z);
        yield return (tile.X, tile.Z + 1);
        yield return (tile.X + 1, tile.Z + 1);
    }

    public bool IsRoad(int x, int z)
    {
        return _roadCells.Contains((x, z));
    }

    public bool IsBridge(int x, int z)
    {
        return _bridgeCells.ContainsKey((x, z));
    }

    /// <summary>Whether a fine cell is paved (road or bridge): no building may stand on it, and crossing it is faster.</summary>
    public bool IsPaved(int x, int z)
    {
        return _roadCells.Contains((x, z)) || _bridgeCells.ContainsKey((x, z));
    }

    /// <summary>The speed multiplier of a fine cell: the road speed on paving, else its terrain's.</summary>
    public float CellSpeed(int x, int z)
    {
        return IsPaved(x, z) ? RoadSpeed : TerrainDefs.For(TerrainAt(x, z)).Speed;
    }

    // ---- Lines ---------------------------------------------------------------

    /// <summary>
    /// The road tiles of a drag from one cell to another (each snapped to its tile): along X then Z, an
    /// L whose longer leg comes first, every tile between in order from the start.
    /// </summary>
    public static List<(int X, int Z)> RoadLine(int fromX, int fromZ, int toX, int toZ)
    {
        (int ax, int az) = TileOf(fromX, fromZ);
        (int bx, int bz) = TileOf(toX, toZ);
        var tiles = new List<(int X, int Z)>();
        bool xFirst = Math.Abs(bx - ax) >= Math.Abs(bz - az);
        (int cx, int cz) = (ax, az);
        tiles.Add((cx, cz));
        void Walk(int tx, int tz)
        {
            while (cx != tx || cz != tz)
            {
                cx += Math.Sign(tx - cx) * 2;
                cz += Math.Sign(tz - cz) * 2;
                tiles.Add((cx, cz));
            }
        }
        if (xFirst)
        {
            Walk(bx, az);
            Walk(bx, bz);
        }
        else
        {
            Walk(ax, bz);
            Walk(bx, bz);
        }
        return tiles;
    }

    /// <summary>The bridge tiles of a drag, or null when it is not straight along X or Z.</summary>
    public static List<(int X, int Z)>? BridgeLine(int fromX, int fromZ, int toX, int toZ)
    {
        (int ax, int az) = TileOf(fromX, fromZ);
        (int bx, int bz) = TileOf(toX, toZ);
        if (ax != bx && az != bz)
        {
            return null;
        }
        return RoadLine(fromX, fromZ, toX, toZ);
    }

    // ---- Roads ---------------------------------------------------------------

    /// <summary>Whether a road tile can be laid: each of its four cells explored, walkable land, unbuilt, and not paved.</summary>
    public bool CanLayRoadTile((int X, int Z) tile)
    {
        foreach ((int x, int z) in CellsOf(tile))
        {
            if (!InBounds(x, z) || !IsChunkGenerated(Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z)))
                || !IsExplored(MapCellOf(x), MapCellOf(z)) || !IsWalkable(x, z) || IsPaved(x, z))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>A road tile's cost: 1 Stone a cell, 2 on Sand, Snow, and Mud.</summary>
    public IReadOnlyList<ResourceAmount> RoadTileCost((int X, int Z) tile)
    {
        float stone = 0f;
        foreach ((int x, int z) in CellsOf(tile))
        {
            stone += TerrainAt(x, z) is TerrainType.Sand or TerrainType.Snow or TerrainType.Mud ? 2f : 1f;
        }
        return new[] { new ResourceAmount(ResourceType.Stone, stone) };
    }

    /// <summary>
    /// A road line's plan: each tile and whether it is already paved, can be laid, and what it costs;
    /// the tiles the stockpile can pay for in order (stopping at the first it cannot), and their cost.
    /// </summary>
    public RoadPlan PlanRoad(int fromX, int fromZ, int toX, int toZ)
    {
        var tiles = new List<RoadPlanTile>();
        var budget = new Dictionary<ResourceType, float>(_stockpile);
        bool broke = false;
        var total = new Dictionary<ResourceType, float>();
        int laid = 0;
        foreach ((int X, int Z) tile in RoadLine(fromX, fromZ, toX, toZ))
        {
            if (_roadTiles.Contains(tile))
            {
                tiles.Add(new RoadPlanTile(tile, RoadTileState.Paved));
                continue;
            }
            if (!CanLayRoadTile(tile))
            {
                tiles.Add(new RoadPlanTile(tile, RoadTileState.Blocked));
                continue;
            }
            IReadOnlyList<ResourceAmount> cost = RoadTileCost(tile);
            Add(total, cost, 1f);
            bool affordable = !broke && cost.All(c => budget.GetValueOrDefault(c.Type) >= c.Amount - RoomTolerance);
            if (affordable)
            {
                Add(budget, cost, -1f);
                laid++;
            }
            else
            {
                broke = true;
            }
            tiles.Add(new RoadPlanTile(tile, affordable ? RoadTileState.Ready : RoadTileState.Short));
        }
        return new RoadPlan(tiles, laid, total.Select(p => new ResourceAmount(p.Key, p.Value)).ToList());
    }

    /// <summary>Lays a road line: every layable tile the stockpile pays for, in order from the start. Returns the tiles laid.</summary>
    public int TryLayRoad(int fromX, int fromZ, int toX, int toZ)
    {
        return LayRoad(PlanRoad(fromX, fromZ, toX, toZ), pay: true);
    }

    private int LayRoad(RoadPlan plan, bool pay)
    {
        int laid = 0;
        foreach (RoadPlanTile planned in plan.Tiles)
        {
            // The debug path lays tiles the stockpile could not pay for too.
            if (planned.State != RoadTileState.Ready && (pay || planned.State != RoadTileState.Short))
            {
                continue;
            }
            if (pay)
            {
                foreach (ResourceAmount cost in RoadTileCost(planned.Tile))
                {
                    AddToStockpile(cost.Type, -cost.Amount);
                }
            }
            _roadTiles.Add(planned.Tile);
            foreach ((int, int) cell in CellsOf(planned.Tile))
            {
                _roadCells.Add(cell);
            }
            laid++;
        }
        if (laid > 0)
        {
            RoadsChanged();
        }
        return laid;
    }

    /// <summary>Removes the road tiles on a line, refunding half their cost, rounded up. Returns the tiles removed.</summary>
    public int TryRemoveRoad(int fromX, int fromZ, int toX, int toZ)
    {
        var refund = new Dictionary<ResourceType, float>();
        int removed = 0;
        foreach ((int X, int Z) tile in RoadLine(fromX, fromZ, toX, toZ))
        {
            if (!_roadTiles.Remove(tile))
            {
                continue;
            }
            Add(refund, RoadTileCost(tile), 1f);
            foreach ((int, int) cell in CellsOf(tile))
            {
                _roadCells.Remove(cell);
            }
            removed++;
        }
        Refund(refund);
        if (removed > 0)
        {
            RoadsChanged();
        }
        return removed;
    }

    // ---- Bridges -------------------------------------------------------------

    /// <summary>A bridge tile's cost: 3 Wood and 1 Stone a cell.</summary>
    public static IReadOnlyList<ResourceAmount> BridgeTileCost { get; } = new ResourceAmount[]
    {
        new(ResourceType.Wood, 12f),
        new(ResourceType.Stone, 4f)
    };

    /// <summary>Why a bridge cannot be laid on a line, or null when it can (cost aside).</summary>
    public string? BridgeProblem(IReadOnlyList<(int X, int Z)>? tiles)
    {
        if (tiles is null)
        {
            return "A bridge runs straight";
        }
        if (tiles.Count > MaxBridgeTiles)
        {
            return $"At most {MaxBridgeTiles * 2} cells long";
        }
        bool alongX = tiles.Count < 2 || tiles[0].Z == tiles[^1].Z;
        foreach ((int X, int Z) tile in tiles)
        {
            if (!CanBridgeTile(tile, alongX))
            {
                return "Not over open water";
            }
        }
        if (tiles.Count == 1)
        {
            // A single tile could run either way: it needs land on two opposite sides.
            alongX = IsLandTile((tiles[0].X - 2, tiles[0].Z)) && IsLandTile((tiles[0].X + 2, tiles[0].Z));
            if (!alongX && !(IsLandTile((tiles[0].X, tiles[0].Z - 2)) && IsLandTile((tiles[0].X, tiles[0].Z + 2))))
            {
                return "It must land at both ends";
            }
            return null;
        }
        (int dx, int dz) = alongX ? (Math.Sign(tiles[^1].X - tiles[0].X) * 2, 0) : (0, Math.Sign(tiles[^1].Z - tiles[0].Z) * 2);
        if (!IsLandTile((tiles[0].X - dx, tiles[0].Z - dz)) || !IsLandTile((tiles[^1].X + dx, tiles[^1].Z + dz)))
        {
            return "It must land at both ends";
        }
        return null;
    }

    /// <summary>
    /// Whether a tile can carry a bridge running along X (or Z): each cell explored, unbuilt, with no
    /// field or paving, and either walkable land, ShallowWater, or the channel of a straight river
    /// running across the bridge; and at least one cell water or channel.
    /// </summary>
    private bool CanBridgeTile((int X, int Z) tile, bool alongX)
    {
        bool wet = false;
        foreach ((int x, int z) in CellsOf(tile))
        {
            if (!InBounds(x, z) || !IsChunkGenerated(Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z)))
                || !IsExplored(MapCellOf(x), MapCellOf(z)) || _occupied.Contains((x, z)) || FieldAt(x, z) is not null || IsPaved(x, z))
            {
                return false;
            }
            if (IsRiverChannel(x, z))
            {
                int mapX = MapCellOf(x);
                int mapZ = MapCellOf(z);
                bool northSouth = RiverConnects(mapX, mapZ, Facing.North) || RiverConnects(mapX, mapZ, Facing.South);
                if (RiverShapeAt(mapX, mapZ) != RiverShape.Straight || northSouth != alongX)
                {
                    return false;
                }
                wet = true;
            }
            else if (TerrainAt(x, z) == TerrainType.ShallowWater)
            {
                wet = true;
            }
            else if (!IsWalkable(x, z))
            {
                return false;
            }
        }
        return wet;
    }

    /// <summary>Walkable land a bridge can land on: each cell walkable (paved or not).</summary>
    private bool IsLandTile((int X, int Z) tile)
    {
        foreach ((int x, int z) in CellsOf(tile))
        {
            if (!InBounds(x, z) || !IsChunkGenerated(Chunk.Of(MapCellOf(x)), Chunk.Of(MapCellOf(z))) || !IsWalkable(x, z))
            {
                return false;
            }
        }
        return true;
    }

    public bool CanAffordBridge(int tiles)
    {
        return HasAll(BridgeCost(tiles));
    }

    /// <summary>A bridge span's cost: <see cref="BridgeTileCost"/> per tile.</summary>
    public static IReadOnlyList<ResourceAmount> BridgeCost(int tiles)
    {
        return BridgeTileCost.Select(c => new ResourceAmount(c.Type, c.Amount * tiles)).ToList();
    }

    /// <summary>Lays a whole bridge span when it is valid and the stockpile pays for it. False otherwise, with nothing taken.</summary>
    public bool TryLayBridge(int fromX, int fromZ, int toX, int toZ)
    {
        List<(int X, int Z)>? tiles = BridgeLine(fromX, fromZ, toX, toZ);
        if (BridgeProblem(tiles) is not null || !HasAll(BridgeCost(tiles!.Count)))
        {
            return false;
        }
        foreach (ResourceAmount cost in BridgeCost(tiles.Count))
        {
            AddToStockpile(cost.Type, -cost.Amount);
        }
        AddBridge(tiles);
        return true;
    }

    private void AddBridge(List<(int X, int Z)> tiles)
    {
        bool alongX = tiles.Count < 2 ? IsLandTile((tiles[0].X - 2, tiles[0].Z)) : tiles[0].Z == tiles[^1].Z;
        var bridge = new Bridge(_nextBridgeId++, tiles, alongX);
        _bridges.Add(bridge);
        foreach ((int X, int Z) tile in tiles)
        {
            foreach ((int, int) cell in CellsOf(tile))
            {
                _bridgeCells[cell] = bridge;
            }
        }
        RoadsChanged();
    }

    /// <summary>
    /// Removes every bridge with a tile on the line, refunding half its cost, rounded up. A bridge with
    /// the player or a truck on it stays. Returns the bridges removed.
    /// </summary>
    public int TryRemoveBridge(int fromX, int fromZ, int toX, int toZ)
    {
        var spans = RoadLine(fromX, fromZ, toX, toZ)
            .Select(t => _bridgeCells.GetValueOrDefault(t))
            .Where(b => b is not null)
            .Distinct()
            .ToList();
        var refund = new Dictionary<ResourceType, float>();
        int removed = 0;
        foreach (Bridge bridge in spans!)
        {
            if (IsOnBridge(bridge))
            {
                continue;
            }
            _bridges.Remove(bridge);
            foreach ((int X, int Z) tile in bridge.Tiles)
            {
                foreach ((int, int) cell in CellsOf(tile))
                {
                    _bridgeCells.Remove(cell);
                }
            }
            Add(refund, BridgeCost(bridge.Tiles.Count), 1f);
            removed++;
        }
        Refund(refund);
        if (removed > 0)
        {
            RoadsChanged();
        }
        return removed;
    }

    private bool IsOnBridge(Bridge bridge)
    {
        if (_bridgeCells.GetValueOrDefault(CellAt(Player.Position.X, Player.Position.Z)) == bridge)
        {
            return true;
        }
        return _trucks.Any(t => _bridgeCells.GetValueOrDefault(CellAt(t.Position.X, t.Position.Z)) == bridge);
    }

    // ---- Shared --------------------------------------------------------------

    private static void Add(Dictionary<ResourceType, float> into, IReadOnlyList<ResourceAmount> amounts, float sign)
    {
        foreach (ResourceAmount a in amounts)
        {
            into[a.Type] = into.GetValueOrDefault(a.Type) + sign * a.Amount;
        }
    }

    /// <summary>Half of what was spent, rounded up per resource, back into storage (past limits, as building refunds).</summary>
    private void Refund(Dictionary<ResourceType, float> spent)
    {
        foreach ((ResourceType type, float amount) in spent)
        {
            AddToStockpile(type, MathF.Ceiling(amount / 2f));
        }
    }

    private void RoadsChanged()
    {
        RoadVersion++;
        _unreachable.Clear();
    }

    private void ResetRoads()
    {
        _roadTiles.Clear();
        _roadCells.Clear();
        _bridges.Clear();
        _bridgeCells.Clear();
        RoadVersion++;
    }

    private void PlaceRoadForDebug(int fromX, int fromZ, int toX, int toZ)
    {
        if (LayRoad(PlanRoad(fromX, fromZ, toX, toZ), pay: false) == 0)
        {
            throw new InvalidOperationException($"no road tile can be laid from ({fromX}, {fromZ}) to ({toX}, {toZ})");
        }
    }

    private void PlaceBridgeForDebug(int fromX, int fromZ, int toX, int toZ)
    {
        List<(int X, int Z)>? tiles = BridgeLine(fromX, fromZ, toX, toZ);
        if (BridgeProblem(tiles) is string problem)
        {
            throw new InvalidOperationException($"no bridge from ({fromX}, {fromZ}) to ({toX}, {toZ}): {problem}");
        }
        AddBridge(tiles!);
    }
}

/// <summary>What a road line would do with a tile.</summary>
public enum RoadTileState
{
    /// <summary>Already a road: free, skipped.</summary>
    Paved,

    /// <summary>Can be laid and is paid for.</summary>
    Ready,

    /// <summary>Can be laid, but the stockpile runs out before it.</summary>
    Short,

    /// <summary>Cannot be laid here.</summary>
    Blocked
}

public readonly record struct RoadPlanTile((int X, int Z) Tile, RoadTileState State);

/// <summary>A road line's plan: its tiles in order, how many would be laid, and the cost of every layable tile.</summary>
public sealed record RoadPlan(IReadOnlyList<RoadPlanTile> Tiles, int Laid, IReadOnlyList<ResourceAmount> Cost);
