using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Mirrors the simulation's resource nodes onto meshes, creating and freeing
/// them as nodes appear or deplete. A node's mesh shrinks as it is used up.
/// </summary>
public partial class ResourceNodeViews : Node3D
{
    private readonly Dictionary<int, MeshInstance3D> _views = new();

    public void Sync(IReadOnlyList<ResourceNode> nodes)
    {
        var present = new HashSet<int>();
        foreach (ResourceNode node in nodes)
        {
            present.Add(node.Id);
            if (!_views.TryGetValue(node.Id, out MeshInstance3D? view))
            {
                view = CreateView(node);
                _views[node.Id] = view;
                AddChild(view);
            }

            float fraction = Mathf.Max(0.1f, node.RemainingFraction);
            view!.Scale = new Vector3(1f, fraction, 1f);
            view.Position = node.Position + new Vector3(0f, 0.4f * fraction, 0f);
        }

        RemoveMissing(present);
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
            _views[id].QueueFree();
            _views.Remove(id);
        }
    }

    private static MeshInstance3D CreateView(ResourceNode node)
    {
        Color color = node.Type switch
        {
            ResourceType.Ore => new Color(0.55f, 0.57f, 0.62f),
            ResourceType.Wood => new Color(0.32f, 0.44f, 0.22f),
            _ => Colors.White
        };

        return new MeshInstance3D
        {
            Name = $"Node{node.Id}",
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.8f, 0.8f, 0.8f)
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color
            }
        };
    }
}
