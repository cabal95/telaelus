using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>Mirrors the simulation's buildings onto meshes, colored by type.</summary>
public partial class BuildingViews : Node3D
{
    private readonly Dictionary<int, MeshInstance3D> _views = new();

    public void Sync(IReadOnlyList<Building> buildings)
    {
        foreach (Building building in buildings)
        {
            if (_views.ContainsKey(building.Id))
            {
                continue;
            }

            var view = new MeshInstance3D
            {
                Name = $"Building{building.Id}",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(0.9f, 0.9f, 0.9f)
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = ColorFor(building.Type)
                },
                Position = building.Position + new Vector3(0f, 0.45f, 0f)
            };
            _views[building.Id] = view;
            AddChild(view);
        }
    }

    public static Color ColorFor(BuildingType type)
    {
        return type switch
        {
            BuildingType.Extractor => new Color(0.80f, 0.55f, 0.20f),
            BuildingType.Smelter => new Color(0.80f, 0.30f, 0.25f),
            BuildingType.Refinery => new Color(0.35f, 0.45f, 0.80f),
            _ => Colors.White
        };
    }
}
