using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// The darkness over a render tile's unexplored cells: a flat quad just above the ground,
/// covering the tile, with a small mask texture (one texel per map cell, plus a one-cell
/// border from the neighboring tiles so edges match across tiles) sampled with linear
/// filtering. Unexplored cells are dark up to their edge, and the darkness fades out
/// across the outer half of the explored cells beside them, so the edge is soft. Where
/// explored cells meet around an unexplored cell's corner, the blend thins the dark a
/// little inside the unexplored cell; a solid plane of the same color under the tile (below
/// every terrain model) makes sure darkness, not the background, shows there.
/// </summary>
public static class FogOverlay
{
    /// <summary>The color of the unexplored: the UI's panel color, near-black.</summary>
    public static readonly Color Color = new(0.08f, 0.10f, 0.09f);

    // Above the ground and its flat details; tall models in explored cells stand in front of it.
    private const float Height = 0.2f;

    // The solid plane under the tile: below the ground, water, and river channels.
    private const float FloorHeight = -3f;

    private static Shader? _shader;

    private static Shader FogShader => _shader ??= new Shader
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_mix;

            uniform sampler2D mask : filter_linear, repeat_disable;
            uniform vec3 fog_color : source_color;
            uniform float cells;

            void fragment() {
                // Texel i holds the cell i - 1 of the tile, so cell centers land on texel centers.
                vec2 at = (UV * cells + 1.0) / (cells + 2.0);
                float explored = texture(mask, at).r;
                ALBEDO = fog_color;
                // 0.5 at an explored cell's edge, 1 at its center: dark up to the edge, clear by the center.
                ALPHA = 1.0 - smoothstep(0.5, 1.0, explored);
            }
            """
    };

    /// <summary>The tile's darkness (the overlay and the solid plane under it), or null when the tile and its border are all explored.</summary>
    public static Node3D? Create(Simulation sim, int tileX, int tileZ)
    {
        if (!sim.World.Fog)
        {
            return null; // nothing is hidden (and beyond a bounded map's edge is not "unexplored")
        }
        int size = TerrainView.TileSize;
        int fromX = tileX * size;
        int fromZ = tileZ * size;
        var image = Image.CreateEmpty(size + 2, size + 2, false, Image.Format.L8);
        bool anyHidden = false;
        for (int i = 0; i < size + 2; i++)
        {
            for (int j = 0; j < size + 2; j++)
            {
                bool explored = sim.IsExplored(fromX - 1 + i, fromZ - 1 + j);
                anyHidden |= !explored;
                image.SetPixel(i, j, explored ? Colors.White : Colors.Black);
            }
        }
        if (!anyHidden)
        {
            return null;
        }

        float tileWorld = size * Simulation.FineCellsPerMapCell;
        var material = new ShaderMaterial
        {
            Shader = FogShader
        };
        material.SetShaderParameter("mask", ImageTexture.CreateFromImage(image));
        material.SetShaderParameter("fog_color", Color);
        material.SetShaderParameter("cells", (float)size);
        var root = new Node3D
        {
            Name = "Fog"
        };
        var plane = new PlaneMesh
        {
            Size = new Vector2(tileWorld, tileWorld)
        };
        root.AddChild(new MeshInstance3D
        {
            Name = "Overlay",
            Mesh = plane,
            MaterialOverride = material,
            Position = new Vector3(tileWorld * 0.5f, Height, tileWorld * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "Floor",
            Mesh = plane,
            MaterialOverride = FloorMaterial,
            Position = new Vector3(tileWorld * 0.5f, FloorHeight, tileWorld * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return root;
    }

    private static StandardMaterial3D? _floorMaterial;

    private static StandardMaterial3D FloorMaterial => _floorMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = Color
    };
}
