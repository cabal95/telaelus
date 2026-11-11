using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Mirrors the simulation's buildings onto boxes sized to their footprint, colored by type,
/// creating and freeing them as buildings appear or are removed.
/// </summary>
public partial class BuildingViews : Node3D
{
    public const float BoxHeight = 1f;

    // Inset each side so neighboring buildings read as separate boxes.
    public const float Inset = 0.1f;

    private readonly Dictionary<int, MeshInstance3D> _views = new();

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

            var view = new MeshInstance3D
            {
                Name = $"Building{building.Id}",
                Mesh = new BoxMesh
                {
                    Size = BoxSize(building.Width, building.Depth)
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = ColorFor(building.Type)
                },
                Position = building.Position + new Vector3(0f, BoxHeight * 0.5f, 0f)
            };
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

    public static Vector3 BoxSize(int width, int depth)
    {
        return new Vector3(width - Inset, BoxHeight, depth - Inset);
    }

    public static Color ColorFor(BuildingType type)
    {
        return type switch
        {
            BuildingType.Mine => new Color(0.80f, 0.55f, 0.20f),
            BuildingType.LumberCamp => new Color(0.55f, 0.38f, 0.20f),
            BuildingType.Smelter => new Color(0.80f, 0.30f, 0.25f),
            BuildingType.Refinery => new Color(0.35f, 0.45f, 0.80f),
            _ => Colors.White
        };
    }
}
