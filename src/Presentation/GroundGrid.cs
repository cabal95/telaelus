using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds the visual ground for a centered width x height tile field: a flat
/// plane plus a line grid so individual tiles read clearly. Pure presentation;
/// the grid dimensions come from the simulation.
/// </summary>
public static class GroundGrid
{
    public static Node3D Build(int width, int height, float tile)
    {
        var root = new Node3D
        {
            Name = "Ground"
        };

        float sizeX = width * tile;
        float sizeZ = height * tile;

        root.AddChild(new MeshInstance3D
        {
            Name = "GroundPlane",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(sizeX, sizeZ)
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.20f, 0.23f, 0.20f)
            }
        });

        var lineMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.32f, 0.36f, 0.32f)
        };

        float hx = sizeX * 0.5f;
        float hz = sizeZ * 0.5f;
        const float y = 0.01f; // just above the ground to avoid z-fighting

        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.Lines, lineMat);
        for (int i = 0; i <= width; i++)
        {
            float x = -hx + i * tile;
            lines.SurfaceAddVertex(new Vector3(x, y, -hz));
            lines.SurfaceAddVertex(new Vector3(x, y, hz));
        }
        for (int j = 0; j <= height; j++)
        {
            float z = -hz + j * tile;
            lines.SurfaceAddVertex(new Vector3(-hx, y, z));
            lines.SurfaceAddVertex(new Vector3(hx, y, z));
        }
        lines.SurfaceEnd();

        root.AddChild(new MeshInstance3D
        {
            Name = "GridLines",
            Mesh = lines
        });

        return root;
    }
}
