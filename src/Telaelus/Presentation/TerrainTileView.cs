using System.Collections.Generic;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Draws one render tile (<see cref="TerrainView.TileSize"/> x TileSize map cells)
/// of terrain. It gathers every cell's placements (see <see cref="TerrainTiles"/>)
/// and groups them by model into one multimesh per model, so a tile is a few dozen
/// draw calls rather than thousands of nodes. Godot skips the tile's multimeshes
/// whenever the tile is outside the camera's view. Unexplored cells draw nothing; the
/// tile's <see cref="FogOverlay"/> darkens them.
/// </summary>
public static class TerrainTileView
{
    public static Node3D Create(Simulation sim, int tileX, int tileZ)
    {
        int size = TerrainView.TileSize;
        int fromX = tileX * size;
        int fromZ = tileZ * size;
        var origin = new Vector3(fromX * Simulation.FineCellsPerMapCell, 0f, fromZ * Simulation.FineCellsPerMapCell);

        var placements = new List<Placement>();
        for (int mapX = fromX; mapX < fromX + size; mapX++)
        {
            for (int mapZ = fromZ; mapZ < fromZ + size; mapZ++)
            {
                if (sim.IsExplored(mapX, mapZ))
                {
                    TerrainTiles.AddPlacements(sim, mapX, mapZ, origin, placements);
                }
            }
        }

        var groups = new Dictionary<string, List<Placement>>();
        foreach (Placement placement in placements)
        {
            if (!groups.TryGetValue(placement.Path, out List<Placement>? group))
            {
                group = new List<Placement>();
                groups[placement.Path] = group;
            }
            group.Add(placement);
        }

        var root = new Node3D
        {
            Name = $"Tile_{tileX}_{tileZ}",
            Position = origin
        };
        foreach ((string path, List<Placement> group) in groups)
        {
            (Mesh mesh, Transform3D meshTransform) = ModelLibrary.Mesh(path);
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = group.Count
            };
            for (int i = 0; i < group.Count; i++)
            {
                Placement p = group[i];
                var transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(p.Yaw)), p.Position);
                multimesh.SetInstanceTransform(i, transform * meshTransform);
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Name = path.Replace('/', '_'),
                Multimesh = multimesh,
                CastShadow = group[0].CastsShadow
                    ? GeometryInstance3D.ShadowCastingSetting.On
                    : GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        if (FogOverlay.Create(sim, tileX, tileZ) is Node3D fog)
        {
            root.AddChild(fog);
        }
        return root;
    }
}
