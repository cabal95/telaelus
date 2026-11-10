using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Mirrors the simulation's resource nodes onto visuals (CC0 models when present,
/// colored boxes otherwise), creating and freeing them as nodes appear or deplete.
/// A node's visual shrinks as it is used up.
/// </summary>
public partial class ResourceNodeViews : Node3D
{
    private readonly Dictionary<int, Node3D> _views = new();

    public void Sync(IReadOnlyList<ResourceNode> nodes)
    {
        var present = new HashSet<int>();
        foreach (ResourceNode node in nodes)
        {
            present.Add(node.Id);
            if (!_views.TryGetValue(node.Id, out Node3D? view))
            {
                view = NodeModels.CreateVisual(node);
                view.Position = node.Position;
                _views[node.Id] = view;
                AddChild(view);
            }

            float fraction = Mathf.Max(0.15f, node.RemainingFraction);
            view!.Scale = new Vector3(fraction, fraction, fraction);
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
}
