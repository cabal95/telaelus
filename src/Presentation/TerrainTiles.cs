using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a visual for one terrain cell. For now these are flat colored tiles
/// (a placeholder for CC0 tile models): grass, water, and a mountain cone. Water
/// cells get green shoreline strips on their land-facing edges, which previews
/// the grass-to-water transitions that the real edge/corner tiles will provide.
/// </summary>
public static class TerrainTiles
{
    private static readonly Color GrassColor = new(0.33f, 0.49f, 0.27f);
    private static readonly Color WaterColor = new(0.20f, 0.38f, 0.60f);
    private static readonly Color MountainColor = new(0.45f, 0.42f, 0.40f);

    public static Node3D CreateTile(Simulation sim, int x, int z)
    {
        return sim.TerrainAt(x, z) switch
        {
            TerrainType.Water => BuildWater(sim, x, z),
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
                BottomRadius = 0.45f,
                Height = 0.8f
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = MountainColor
            },
            Position = new Vector3(0f, 0.4f, 0f)
        });
        return root;
    }

    private static Node3D BuildWater(Simulation sim, int x, int z)
    {
        var root = new Node3D
        {
            Name = "Water"
        };
        root.AddChild(FlatTile(WaterColor));

        AddShore(root, IsLand(sim, x, z + 1), new Vector3(0f, 0.01f, 0.45f), new Vector3(1f, 0.12f, 0.1f));
        AddShore(root, IsLand(sim, x, z - 1), new Vector3(0f, 0.01f, -0.45f), new Vector3(1f, 0.12f, 0.1f));
        AddShore(root, IsLand(sim, x + 1, z), new Vector3(0.45f, 0.01f, 0f), new Vector3(0.1f, 0.12f, 1f));
        AddShore(root, IsLand(sim, x - 1, z), new Vector3(-0.45f, 0.01f, 0f), new Vector3(0.1f, 0.12f, 1f));
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
                Size = new Vector3(1f, 0.1f, 1f)
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color
            },
            Position = new Vector3(0f, -0.05f, 0f)
        };
    }

    private static bool IsLand(Simulation sim, int x, int z)
    {
        return sim.TerrainAt(x, z) != TerrainType.Water;
    }
}
