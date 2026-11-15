using System.Collections.Generic;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Mirrors the simulation's buildings onto their models (see <see cref="BuildingModels"/>),
/// turned to each building's facing, creating and freeing them as buildings appear or are
/// removed.
/// </summary>
public partial class BuildingViews : Node3D
{
    private readonly Dictionary<int, Node3D> _views = new();

    public void Sync(IReadOnlyList<Building> buildings)
    {
        var present = new HashSet<int>();
        foreach (Building building in buildings)
        {
            present.Add(building.Id);
            if (_views.ContainsKey(building.Id))
            {
                continue;
            }

            Node3D view = BuildingModels.Create(building.Type, building.Facing);
            view.Name = $"Building{building.Id}";
            view.Position = building.Position;
            _views[building.Id] = view;
            AddChild(view);
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
