using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Draws the terrain in render tiles of <see cref="TileSize"/> x TileSize map
/// cells near the camera. Each frame it works out the tiles under the camera's
/// view and around the player, plus a one-tile margin, builds the missing ones whose cells are ready, and
/// frees tiles that have fallen two tiles outside that range (their chunks stay in
/// the sim and are drawn again on return). An edit to the map marks the view
/// dirty and the tiles are rebuilt on the next frame; newly explored cells rebuild
/// only the tiles they touch (with their neighbors' borders). It processes while the
/// tree is paused, so edits made while paused still show.
/// </summary>
public partial class TerrainView : Node3D
{
    /// <summary>Render tile size in map cells.</summary>
    public const int TileSize = 16;

    // Half-width (world units) of the area around the player that is always in range.
    private const float PlayerReach = 48f;

    private Simulation _sim = null!;
    private Camera3D _camera = null!;
    private readonly Dictionary<(int X, int Z), Node3D> _tiles = new();
    private bool _dirty = true;
    private readonly HashSet<(int X, int Z)> _stale = new();

    /// <summary>How many render tiles are built right now.</summary>
    public int TileCount => _tiles.Count;

    /// <summary>The render tiles built right now (tile coordinates are map cells divided by TileSize, rounded down).</summary>
    public IEnumerable<(int X, int Z)> TileKeys => _tiles.Keys;

    public bool HasTile((int X, int Z) key)
    {
        return _tiles.ContainsKey(key);
    }

    /// <summary>The ground area (x and z, world units) the built tiles should cover: the view plus a margin.</summary>
    public Rect2 ViewArea { get; private set; }

    public void Initialize(Simulation sim, Camera3D camera)
    {
        _sim = sim;
        _camera = camera;
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>Marks the tiles that show newly explored cells (or hold them in a fog border) for a rebuild.</summary>
    public void Reveal(IReadOnlyList<(int MapX, int MapZ)> cells)
    {
        foreach ((int mapX, int mapZ) in cells)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    _stale.Add((TileOf(mapX + dx), TileOf(mapZ + dz)));
                }
            }
        }
    }

    private static int TileOf(int mapCoordinate)
    {
        return Mathf.FloorToInt(mapCoordinate / (float)TileSize);
    }

    public override void _Process(double delta)
    {
        if (_dirty)
        {
            _dirty = false;
            foreach (Node3D tile in _tiles.Values)
            {
                tile.QueueFree();
            }
            _tiles.Clear();
        }
        foreach ((int X, int Z) key in _stale)
        {
            if (_tiles.Remove(key, out Node3D? old))
            {
                old.QueueFree();
            }
        }
        _stale.Clear();

        // The camera's view, plus the area around the player: after a reset or a
        // teleport the camera glides over for a moment, and the player's surroundings
        // should be drawn the whole time.
        Vector3 player = _sim.Player.Position;
        Rect2 view = CameraGroundRect().Merge(new Rect2(player.X - PlayerReach, player.Z - PlayerReach, 2f * PlayerReach, 2f * PlayerReach));
        float tileWorld = TileSize * Simulation.FineCellsPerMapCell;
        ViewArea = view.Grow(tileWorld);
        int minX = Mathf.FloorToInt(view.Position.X / tileWorld) - 1;
        int minZ = Mathf.FloorToInt(view.Position.Y / tileWorld) - 1;
        int maxX = Mathf.FloorToInt(view.End.X / tileWorld) + 1;
        int maxZ = Mathf.FloorToInt(view.End.Y / tileWorld) + 1;

        for (int tx = minX; tx <= maxX; tx++)
        {
            for (int tz = minZ; tz <= maxZ; tz++)
            {
                if (!_tiles.ContainsKey((tx, tz)) && CanBuild(tx, tz))
                {
                    Node3D tile = TerrainTileView.Create(_sim, tx, tz);
                    AddChild(tile);
                    _tiles[(tx, tz)] = tile;
                }
            }
        }

        var far = new List<(int, int)>();
        foreach ((int tx, int tz) in _tiles.Keys)
        {
            if (tx < minX - 1 || tx > maxX + 1 || tz < minZ - 1 || tz > maxZ + 1)
            {
                far.Add((tx, tz));
            }
        }
        foreach ((int, int) key in far)
        {
            _tiles[key].QueueFree();
            _tiles.Remove(key);
        }
    }

    /// <summary>
    /// A tile can be built when it overlaps a bounded world's rectangle, or, in a
    /// generated world, when every chunk it reads (its own cells and the ring of
    /// cells around them, for transitions) is generated. Drawing never generates.
    /// </summary>
    private bool CanBuild(int tileX, int tileZ)
    {
        int fromX = tileX * TileSize;
        int fromZ = tileZ * TileSize;
        if (_sim.World.IsBounded)
        {
            return fromX < _sim.MapWidth && fromZ < _sim.MapHeight && fromX + TileSize > 0 && fromZ + TileSize > 0;
        }
        for (int cx = Chunk.Of(fromX - 1); cx <= Chunk.Of(fromX + TileSize); cx++)
        {
            for (int cz = Chunk.Of(fromZ - 1); cz <= Chunk.Of(fromZ + TileSize); cz++)
            {
                if (!_sim.IsChunkGenerated(cx, cz))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>The bounding rectangle (x and z) of where the viewport's corners meet the ground.</summary>
    private Rect2 CameraGroundRect()
    {
        Vector2 size = _camera.GetViewport().GetVisibleRect().Size;
        var corners = new[] { Vector2.Zero, new Vector2(size.X, 0f), new Vector2(0f, size.Y), size };
        var rect = new Rect2();
        bool first = true;
        foreach (Vector2 corner in corners)
        {
            Vector3 origin = _camera.ProjectRayOrigin(corner);
            Vector3 normal = _camera.ProjectRayNormal(corner);
            float t = Mathf.Abs(normal.Y) > 1e-4f ? -origin.Y / normal.Y : 0f;
            Vector3 hit = origin + normal * t;
            var point = new Vector2(hit.X, hit.Z);
            if (first)
            {
                rect = new Rect2(point, Vector2.Zero);
                first = false;
            }
            else
            {
                rect = rect.Expand(point);
            }
        }
        return rect;
    }
}
