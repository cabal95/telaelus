using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Draws the resource fields of one render tile: every visible slot of every field in
/// the tile, grouped by model into one multimesh per model, so a forest is a handful
/// of draw calls rather than hundreds of nodes. Slot positions, the models shown per
/// stage, and their sizes follow <see cref="ResourceFieldViews"/> and
/// <see cref="NodeModels"/>.
/// </summary>
public static class FieldTileView
{
    public static Node3D Create(Simulation sim, IReadOnlyList<ResourceField> fields, int tileX, int tileZ)
    {
        float tileWorld = TerrainView.TileSize * Simulation.FineCellsPerMapCell;
        var origin = new Vector3(tileX * tileWorld, 0f, tileZ * tileWorld);

        var groups = new Dictionary<string, List<Transform3D>>();
        foreach (ResourceField field in fields)
        {
            int stage = field.Stage;
            float grown = ResourceFieldViews.GrowthScale(field);
            for (int i = 0; i < stage; i++)
            {
                Vector3 slot = ResourceFieldViews.SlotPosition(field, i) - origin;
                (string path, Transform3D transform) = NodeModels.Slot(
                    field.Type, sim.MapBiomeAt(field.MapX, field.MapZ), field.Id * ResourceField.StageCount + i, ResourceFieldViews.SlotFootprint, field.InitialAmount, slot, grown);
                if (!groups.TryGetValue(path, out List<Transform3D>? group))
                {
                    group = new List<Transform3D>();
                    groups[path] = group;
                }
                group.Add(transform);
            }
        }

        var root = new Node3D
        {
            Name = $"Fields_{tileX}_{tileZ}",
            Position = origin
        };
        foreach ((string path, List<Transform3D> transforms) in groups)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = ModelLibrary.Mesh(path).Mesh,
                InstanceCount = transforms.Count
            };
            for (int i = 0; i < transforms.Count; i++)
            {
                multimesh.SetInstanceTransform(i, transforms[i]);
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Name = path.Replace('/', '_'),
                Multimesh = multimesh
            });
        }
        return root;
    }
}
