using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Mirrors the simulation's resource fields onto visuals, batched per render tile.
/// Each field has StageCount model slots at deterministic, jittered positions inside
/// its map cell, and shows the first Stage of them, so a field visibly loses one model
/// per stage as it depletes. A field belongs to the render tile containing its map
/// cell; for every tile the terrain view has built, this view builds that tile's
/// field batch (<see cref="FieldTileView"/>), rebuilds it when a field in the tile is
/// added, removed, or changes stage, and frees it when the terrain tile is freed.
/// </summary>
public partial class ResourceFieldViews : Node3D
{
    /// <summary>Each slot model is capped to this footprint, keeping it inside its slot.</summary>
    public const float SlotFootprint = 1.2f;

    private const float SlotJitter = 0.3f;

    // Base slot layout (a quincunx) relative to the field center, in world units.
    private static readonly Vector2[] SlotOffsets =
    {
        new(-1f, -1f),
        new(1f, 1f),
        new(0f, 0f),
        new(1f, -1f),
        new(-1f, 1f)
    };

    private sealed class Batch
    {
        public required Node3D Node;
        public required List<(int Id, int Stage)> Shown;
    }

    private readonly Dictionary<(int X, int Z), Batch> _batches = new();
    private readonly Dictionary<int, int> _visibleModels = new();

    /// <summary>How many render tiles have a built field batch.</summary>
    public int BatchCount => _batches.Count;

    public void Sync(Simulation sim, TerrainView terrain)
    {
        IReadOnlyList<ResourceField> fields = sim.Fields;
        var byTile = new Dictionary<(int, int), List<ResourceField>>();
        foreach (ResourceField field in fields)
        {
            (int, int) key = (FloorDiv(field.MapX, TerrainView.TileSize), FloorDiv(field.MapZ, TerrainView.TileSize));
            // Motherlodes have their own view (SiteViews).
            if (field.IsMotherlode || !terrain.HasTile(key) || !sim.IsExplored(field.MapX, field.MapZ))
            {
                continue;
            }
            if (!byTile.TryGetValue(key, out List<ResourceField>? list))
            {
                list = new List<ResourceField>();
                byTile[key] = list;
            }
            list.Add(field);
        }

        _visibleModels.Clear();
        foreach ((int, int) key in terrain.TileKeys)
        {
            List<ResourceField> inTile = byTile.TryGetValue(key, out List<ResourceField>? list) ? list : new List<ResourceField>();
            var shown = new List<(int, int)>(inTile.Count);
            foreach (ResourceField field in inTile)
            {
                // A growing field's batch is rebuilt each tenth of its growth, so its trees rise.
                int growthStep = Mathf.FloorToInt(GrowthScale(field) * 10f);
                shown.Add((field.Id, field.Stage * 100 + growthStep));
                _visibleModels[field.Id] = field.Stage;
            }

            if (_batches.TryGetValue(key, out Batch? batch))
            {
                if (SameShown(batch.Shown, shown))
                {
                    continue;
                }
                batch.Node.QueueFree();
            }
            Node3D node = FieldTileView.Create(sim, inTile, key.Item1, key.Item2);
            AddChild(node);
            _batches[key] = new Batch
            {
                Node = node,
                Shown = shown
            };
        }

        var gone = new List<(int, int)>();
        foreach ((int, int) key in _batches.Keys)
        {
            if (!terrain.HasTile(key))
            {
                gone.Add(key);
            }
        }
        foreach ((int, int) key in gone)
        {
            _batches[key].Node.QueueFree();
            _batches.Remove(key);
        }
    }

    /// <summary>A field's model scale for its growth: a third of full height at planting, rising to full (1 for a field that is not growing).</summary>
    public static float GrowthScale(ResourceField field)
    {
        if (!field.Growing || field.Owner?.Def.Plot is not ({ } plot))
        {
            return 1f;
        }
        return 1f / 3f + 2f / 3f * field.Growth(plot.GrowSeconds);
    }

    /// <summary>The world position of a field's slot <paramref name="index"/> (0 to StageCount - 1).</summary>
    public static Vector3 SlotPosition(ResourceField field, int index)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)field.Id * 2654435761UL + 7UL
        };
        Vector3 position = field.Center;
        for (int i = 0; i <= index; i++)
        {
            Vector2 offset = SlotOffsets[i % SlotOffsets.Length];
            float jitterX = rng.RandfRange(-SlotJitter, SlotJitter);
            float jitterZ = rng.RandfRange(-SlotJitter, SlotJitter);
            position = field.Center + new Vector3(offset.X + jitterX, 0f, offset.Y + jitterZ);
        }
        return position;
    }

    /// <summary>How many models a field's view shows (its stage), or null if its tile has no batch.</summary>
    public int? VisibleModels(int fieldId)
    {
        return _visibleModels.TryGetValue(fieldId, out int stage) ? stage : null;
    }

    private static bool SameShown(List<(int Id, int Stage)> a, List<(int Id, int Stage)> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }
}
