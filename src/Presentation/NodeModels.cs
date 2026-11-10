using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a visual for a resource node from CC0 glTF models when present, and
/// falls back to a colored box when the model files are not yet in the project.
/// A model is scaled to a target height from its bounding box, so differently
/// sized source models all render at the right in-world size (our render-scaling
/// control), then seated on the ground and given a deterministic yaw per node.
/// </summary>
public static class NodeModels
{
    private sealed record ModelSpec(string[] Paths, float TargetHeight, Color FallbackColor, float FallbackWidth);

    private static readonly Dictionary<ResourceType, ModelSpec> Specs = new()
    {
        [ResourceType.Ore] = new ModelSpec(
            new[]
            {
                "res://assets/models/nature/rock_a.glb",
                "res://assets/models/nature/rock_b.glb",
                "res://assets/models/nature/rock_c.glb"
            },
            0.8f,
            new Color(0.55f, 0.57f, 0.62f),
            0.7f),
        [ResourceType.Wood] = new ModelSpec(
            new[]
            {
                "res://assets/models/nature/tree_a.glb",
                "res://assets/models/nature/tree_b.glb",
                "res://assets/models/nature/tree_c.glb"
            },
            2.5f,
            new Color(0.32f, 0.44f, 0.22f),
            0.5f)
    };

    public static Node3D CreateVisual(ResourceNode node)
    {
        if (!Specs.TryGetValue(node.Type, out ModelSpec? spec))
        {
            spec = new ModelSpec(System.Array.Empty<string>(), 0.8f, Colors.White, 0.7f);
        }

        var root = new Node3D
        {
            Name = $"NodeVisual{node.Id}"
        };
        root.RotationDegrees = new Vector3(0f, DeterministicYaw(node.Id), 0f);

        Node3D visual = TryInstanceModel(spec, node.Id) ?? CreateFallback(spec);
        root.AddChild(visual);
        return root;
    }

    private static float DeterministicYaw(int nodeId)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)nodeId * 2654435761UL + 1UL
        };
        return rng.RandfRange(0f, 360f);
    }

    private static Node3D? TryInstanceModel(ModelSpec spec, int nodeId)
    {
        var existing = new List<string>();
        foreach (string path in spec.Paths)
        {
            if (ResourceLoader.Exists(path))
            {
                existing.Add(path);
            }
        }
        if (existing.Count == 0)
        {
            return null;
        }

        string chosen = existing[nodeId % existing.Count];
        var scene = GD.Load<PackedScene>(chosen);
        if (scene is null)
        {
            return null;
        }

        Node instance = scene.Instantiate();
        if (instance is not Node3D model)
        {
            instance.QueueFree();
            return null;
        }

        SeatAndNormalize(model, spec.TargetHeight);
        return model;
    }

    private static Node3D CreateFallback(ModelSpec spec)
    {
        float height = spec.TargetHeight;
        float width = spec.FallbackWidth;
        return new MeshInstance3D
        {
            Name = "Fallback",
            Mesh = new BoxMesh
            {
                Size = new Vector3(width, height, width)
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = spec.FallbackColor
            },
            Position = new Vector3(0f, height * 0.5f, 0f)
        };
    }

    private static void SeatAndNormalize(Node3D model, float targetHeight)
    {
        Aabb bounds = MeshBounds(model);
        float height = bounds.Size.Y;
        float scale = height > 0.0001f ? targetHeight / height : 1f;
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
