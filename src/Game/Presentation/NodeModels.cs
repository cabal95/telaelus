using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a visual for one resource model (a slot in a resource field) from its glTF
/// models (required; see <see cref="ModelLibrary"/>). A model is scaled to a target height from its bounding box, so differently
/// sized source models all render at the right in-world size (our render-scaling
/// control), then seated on the ground and given a deterministic yaw per node.
/// </summary>
public static class NodeModels
{
    private sealed record ModelSpec(string[] Paths, float TargetHeight);

    private static readonly Dictionary<ResourceType, ModelSpec> Specs = new()
    {
        [ResourceType.Ore] = new ModelSpec(
            new[]
            {
                "nature/rock_a",
                "nature/rock_b",
                "nature/rock_c"
            },
            0.8f),
        [ResourceType.Wood] = new ModelSpec(
            new[]
            {
                "nature/tree_a",
                "nature/tree_b",
                "nature/tree_c"
            },
            2.5f)
    };

    /// <summary>
    /// Builds one resource model. The variant and yaw are chosen deterministically
    /// from <paramref name="seedId"/>, and the model's footprint is capped to
    /// <paramref name="maxFootprint"/> world units so it stays inside its slot.
    /// Larger fields look larger: a Wood model's height grows with the field's
    /// <paramref name="initialAmount"/> (see <see cref="WoodHeight"/>).
    /// </summary>
    public static Node3D CreateVisual(ResourceType type, int seedId, float maxFootprint, float initialAmount)
    {
        if (!Specs.TryGetValue(type, out ModelSpec? spec))
        {
            throw new System.InvalidOperationException($"No resource models for {type}.");
        }
        if (type == ResourceType.Wood)
        {
            spec = spec with
            {
                TargetHeight = WoodHeight(initialAmount)
            };
        }

        var root = new Node3D
        {
            Name = $"ResourceVisual{seedId}"
        };
        root.RotationDegrees = new Vector3(0f, DeterministicYaw(seedId), 0f);

        Node3D model = ModelLibrary.Instance(spec.Paths[seedId % spec.Paths.Length]);
        SeatAndNormalize(model, spec.TargetHeight, maxFootprint);
        root.AddChild(model);
        return root;
    }

    /// <summary>Tree height for a Wood field: 2.5 units at 250 or less, rising to 3.5 at 500 or more.</summary>
    public static float WoodHeight(float initialAmount)
    {
        return 2.5f + Mathf.Clamp((initialAmount - 250f) / 250f, 0f, 1f);
    }

    private static float DeterministicYaw(int seedId)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)seedId * 2654435761UL + 1UL
        };
        return rng.RandfRange(0f, 360f);
    }

    // The model is scaled to the target height unless that would make it wider
    // than maxFootprint, in which case width wins and it shrinks to fit its slot.
    private static void SeatAndNormalize(Node3D model, float targetHeight, float maxFootprint)
    {
        Aabb bounds = MeshBounds(model);
        float height = bounds.Size.Y;
        float footprint = Mathf.Max(bounds.Size.X, bounds.Size.Z);

        float heightScale = height > 0.0001f ? targetHeight / height : 1f;
        float footprintScale = footprint > 0.0001f ? maxFootprint / footprint : heightScale;
        float scale = Mathf.Min(heightScale, footprintScale);

        Vector3 center = bounds.Position + bounds.Size * 0.5f;
        model.Scale = new Vector3(scale, scale, scale);
        model.Position = new Vector3(-center.X * scale, -bounds.Position.Y * scale, -center.Z * scale);
    }

    private static Aabb MeshBounds(Node3D root)
    {
        var total = new Aabb();
        bool started = false;
        foreach (MeshInstance3D mesh in MeshInstances(root))
        {
            Transform3D relative = RelativeTransform(root, mesh);
            Aabb transformed = TransformAabb(relative, mesh.GetAabb());
            if (!started)
            {
                total = transformed;
                started = true;
            }
            else
            {
                total = total.Merge(transformed);
            }
        }
        return total;
    }

    // Transform of a descendant relative to an ancestor, by accumulating local
    // transforms. Avoids GlobalTransform, which is invalid while the subtree is
    // not yet inside the scene tree.
    private static Transform3D RelativeTransform(Node3D ancestor, Node descendant)
    {
        Transform3D transform = Transform3D.Identity;
        Node? current = descendant;
        while (current is not null && current != ancestor)
        {
            if (current is Node3D node3d)
            {
                transform = node3d.Transform * transform;
            }
            current = current.GetParent();
        }
        return transform;
    }

    private static Aabb TransformAabb(Transform3D transform, Aabb box)
    {
        Vector3 p = box.Position;
        Vector3 s = box.Size;
        var result = new Aabb(transform * p, Vector3.Zero);
        for (int i = 1; i < 8; i++)
        {
            var corner = new Vector3(
                p.X + ((i & 1) != 0 ? s.X : 0f),
                p.Y + ((i & 2) != 0 ? s.Y : 0f),
                p.Z + ((i & 4) != 0 ? s.Z : 0f));
            result = result.Expand(transform * corner);
        }
        return result;
    }

    private static IEnumerable<MeshInstance3D> MeshInstances(Node node)
    {
        if (node is MeshInstance3D self)
        {
            yield return self;
        }
        foreach (Node child in node.GetChildren())
        {
            foreach (MeshInstance3D nested in MeshInstances(child))
            {
                yield return nested;
            }
        }
    }
}
