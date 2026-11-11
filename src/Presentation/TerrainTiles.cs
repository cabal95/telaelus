using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a visual for one map cell (FineCellsPerMapCell x FineCellsPerMapCell
/// world units). For now these are flat colored tiles (a placeholder for CC0 tile
/// models): grass, water, and a mountain cone. Water cells get green shoreline
/// strips on their land-facing edges, which previews the grass-to-water
/// transitions that the real edge/corner tiles will provide.
/// </summary>
public static class TerrainTiles
{
    private const float Size = Simulation.FineCellsPerMapCell;
    private const float ShoreWidth = 0.4f;

    private static readonly Color GrassColor = new(0.33f, 0.49f, 0.27f);
    private static readonly Color WaterColor = new(0.20f, 0.38f, 0.60f);
    private static readonly Color MountainColor = new(0.45f, 0.42f, 0.40f);

    public static Node3D CreateTile(Simulation sim, int mapX, int mapZ)
    {
        return sim.MapTerrainAt(mapX, mapZ) switch
        {
            TerrainType.Water => BuildWater(sim, mapX, mapZ),
            TerrainType.Mountain => BuildMountain(),
            _ => BuildGrass()
        };
    }

    private static Node3D BuildGrass()
    {
        var root = new Node3D
        {
            Name = "Grass"
        };
        root.AddChild(FlatTile(GrassColor));
        return root;
    }

    private static Node3D BuildMountain()
    {
        var root = new Node3D
        {
            Name = "Mountain"
        };
        root.AddChild(FlatTile(GrassColor));
        root.AddChild(new MeshInstance3D
        {
            Name = "Peak",
            Mesh = new CylinderMesh
            {
                TopRadius = 0f,
                BottomRadius = Size * 0.45f,
                Height = 2.4f
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = MountainColor
            },
            Position = new Vector3(0f, 1.2f, 0f)
        });
        return root;
    }

    private static Node3D BuildWater(Simulation sim, int mapX, int mapZ)
    {
        var root = new Node3D
        {
            Name = "Water"
        };
        root.AddChild(FlatTile(WaterColor));

        float edge = (Size - ShoreWidth) * 0.5f;
        var alongX = new Vector3(Size, 0.12f, ShoreWidth);
        var alongZ = new Vector3(ShoreWidth, 0.12f, Size);
        AddShore(root, IsLand(sim, mapX, mapZ + 1), new Vector3(0f, 0.01f, edge), alongX);
        AddShore(root, IsLand(sim, mapX, mapZ - 1), new Vector3(0f, 0.01f, -edge), alongX);
        AddShore(root, IsLand(sim, mapX + 1, mapZ), new Vector3(edge, 0.01f, 0f), alongZ);
        AddShore(root, IsLand(sim, mapX - 1, mapZ), new Vector3(-edge, 0.01f, 0f), alongZ);
        return root;
    }

    private static void AddShore(Node3D root, bool land, Vector3 position, Vector3 size)
    {
        if (!land)
        {
            return;
        }
        root.AddChild(new MeshInstance3D
        {
            Name = "Shore",
            Mesh = new BoxMesh
            {
                Size = size
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = GrassColor
            },
            Position = position
        });
    }

    private static MeshInstance3D FlatTile(Color color)
    {
        return new MeshInstance3D
        {
            Name = "Tile",
            Mesh = new BoxMesh
            {
                Size = new Vector3(Size, 0.1f, Size)
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color
            },
            Position = new Vector3(0f, -0.05f, 0f)
        };
    }

    private static bool IsLand(Simulation sim, int mapX, int mapZ)
    {
        return sim.MapTerrainAt(mapX, mapZ) != TerrainType.Water;
    }
}
