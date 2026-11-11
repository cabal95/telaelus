using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Mirrors the simulation's resource fields onto visuals. Each field has
/// StageCount model slots at deterministic, jittered positions inside its map
/// cell; the view shows the first Stage slots, so a field visibly loses one model
/// per stage as it depletes. Visuals are freed when the field is removed.
/// </summary>
public partial class ResourceFieldViews : Node3D
{
    // Each slot model is capped to this footprint, keeping it inside its slot.
    private const float SlotFootprint = 1.2f;
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

    private readonly Dictionary<int, Node3D[]> _views = new();

    public void Sync(IReadOnlyList<ResourceField> fields)
    {
        var present = new HashSet<int>();
        foreach (ResourceField field in fields)
        {
            present.Add(field.Id);
            if (!_views.TryGetValue(field.Id, out Node3D[]? slots))
            {
                slots = CreateSlots(field);
                _views[field.Id] = slots;
            }

            int stage = field.Stage;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Visible = i < stage;
            }
        }

        RemoveMissing(present);
    }

    private Node3D[] CreateSlots(ResourceField field)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)field.Id * 2654435761UL + 7UL
        };

        var slots = new Node3D[ResourceField.StageCount];
        for (int i = 0; i < slots.Length; i++)
        {
            Vector2 offset = SlotOffsets[i % SlotOffsets.Length];
            float jitterX = rng.RandfRange(-SlotJitter, SlotJitter);
            float jitterZ = rng.RandfRange(-SlotJitter, SlotJitter);

            Node3D slot = NodeModels.CreateVisual(field.Type, field.Id * ResourceField.StageCount + i, SlotFootprint, field.InitialAmount);
            slot.Position = field.Center + new Vector3(offset.X + jitterX, 0f, offset.Y + jitterZ);
            AddChild(slot);
            slots[i] = slot;
        }
        return slots;
    }

    /// <summary>How many models a field's view shows, or null if it has no view yet.</summary>
    public int? VisibleModels(int fieldId)
    {
        if (!_views.TryGetValue(fieldId, out Node3D[]? slots))
        {
            return null;
        }

        int visible = 0;
        foreach (Node3D slot in slots)
        {
            if (slot.Visible)
            {
                visible++;
            }
        }
        return visible;
    }

    private void RemoveMissing(HashSet<int> present)
    {
        var gone = new List<int>();
        foreach (int id in _views.Keys)
        {
            if (!present.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (int id in gone)
        {
            foreach (Node3D slot in _views[id])
            {
                slot.QueueFree();
            }
            _views.Remove(id);
        }
    }
}
