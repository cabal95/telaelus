using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Draws what a map cell remembers or holds beyond an ordinary field: a worked-out marker on each
/// explored worked-out cell (a dry stain for Crude Oil, a shallow pit for a solid deposit), and
/// each explored motherlode as its rock mound with its yields' field models set round its ring.
/// Views are created and freed as cells are explored, worked out, or cleared.
/// </summary>
public partial class SiteViews : Node3D
{
    /// <summary>How many models stand round a motherlode's ring.</summary>
    private const int RingSlots = 12;

    private const float RingRadius = 3.0f;
    private const float RingFootprint = 1.5f;
    private const float RingScale = 1.3f;

    private readonly Dictionary<(int, int), Node3D> _markers = new();
    private readonly Dictionary<int, Node3D> _lodes = new();

    /// <summary>How many worked-out markers are drawn (for the snapshot).</summary>
    public int MarkerCount => _markers.Count;

    public void Sync(Simulation sim)
    {
        var present = new HashSet<(int, int)>();
        foreach (((int mapX, int mapZ), ResourceType deposit) in sim.WorkedOut)
        {
            // A building or a new field over the cell hides its marker.
            int x = mapX * Simulation.FineCellsPerMapCell;
            int z = mapZ * Simulation.FineCellsPerMapCell;
            if (!sim.IsExplored(mapX, mapZ) || sim.FieldAt(x, z) is not null || sim.BuildingAt(x + 2, z + 2) is not null)
            {
                continue;
            }
            present.Add((mapX, mapZ));
            if (_markers.ContainsKey((mapX, mapZ)))
            {
                continue;
            }
            Node3D marker = ModelLibrary.Instance(deposit == ResourceType.CrudeOil ? "nature/worked_out_oil" : "nature/worked_out_pit");
            marker.Name = $"WorkedOut_{mapX}_{mapZ}";
            marker.Position = Simulation.MapCellCenter(mapX, mapZ);
            AddChild(marker);
            _markers[(mapX, mapZ)] = marker;
        }
        Prune(_markers, present);

        var lodes = new HashSet<int>();
        foreach (ResourceField field in sim.Fields)
        {
            if (field.Motherlode is not MotherlodeType kind || !sim.IsExplored(field.MapX, field.MapZ))
            {
                continue;
            }
            lodes.Add(field.Id);
            if (!_lodes.ContainsKey(field.Id))
            {
                Node3D view = Motherlode(sim, field, kind);
                AddChild(view);
                _lodes[field.Id] = view;
            }
        }
        Prune(_lodes, lodes);
    }

    private static Node3D Motherlode(Simulation sim, ResourceField field, MotherlodeType kind)
    {
        Node3D root = ModelLibrary.Instance("nature/motherlode_base");
        root.Name = $"Motherlode{field.Id}";
        root.Position = field.Center;
        MotherlodeDef def = MotherlodeDefs.For(kind);
        BiomeType biome = sim.MapBiomeAt(field.MapX, field.MapZ);
        for (int i = 0; i < RingSlots; i++)
        {
            // Slots go to the yields in proportion to their shares, spread round the ring.
            ResourceType type = YieldForSlot(def, i);
            float angle = Mathf.Tau * (i + 0.5f) / RingSlots;
            var slot = new Vector3(Mathf.Cos(angle) * RingRadius, 0f, Mathf.Sin(angle) * RingRadius);
            (string path, Transform3D transform) = NodeModels.Slot(type, biome, field.Id * RingSlots + i, RingFootprint, 1000f, slot, RingScale);
            root.AddChild(new MeshInstance3D
            {
                Name = $"Yield{i}",
                Mesh = ModelLibrary.Mesh(path).Mesh,
                Transform = transform
            });
        }
        return root;
    }

    private static ResourceType YieldForSlot(MotherlodeDef def, int slot)
    {
        // Largest-remainder order over the shares, so 12 slots split 60/40 as 7 and 5, interleaved.
        float best = float.MinValue;
        ResourceType pick = def.Yields[0].Type;
        foreach ((ResourceType type, float share) in def.Yields)
        {
            float due = share * (slot + 1) - Mathf.Floor(share * slot);
            if (due > best)
            {
                best = due;
                pick = type;
            }
        }
        return pick;
    }

    private static void Prune<TKey>(Dictionary<TKey, Node3D> views, HashSet<TKey> present) where TKey : notnull
    {
        var gone = new List<TKey>();
        foreach (TKey key in views.Keys)
        {
            if (!present.Contains(key))
            {
                gone.Add(key);
            }
        }
        foreach (TKey key in gone)
        {
            views[key].QueueFree();
            views.Remove(key);
        }
    }
}
