using System;
using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// Renders every building once, at startup, into two pictures: in color, and as an
/// off-black silhouette. Each is drawn from the game camera's angle in its own small
/// offscreen world, read back after one frame, and cached as a texture, so menus
/// never hold live 3D views. Grayscale is a shader on the color picture
/// (<see cref="GrayscaleMaterial"/>). Without a renderer (headless runs) there are no
/// pictures and <see cref="Color"/> and <see cref="Silhouette"/> return null.
/// </summary>
public partial class BuildingPictures : Node
{
    public const int PictureSize = 256;

    private readonly Dictionary<BuildingType, Texture2D> _color = new();
    private readonly Dictionary<BuildingType, Texture2D> _silhouette = new();

    /// <summary>Raised once the pictures are ready.</summary>
    public event Action? PicturesReady;

    public bool IsReady { get; private set; }

    public Texture2D? Color(BuildingType type) => _color.GetValueOrDefault(type);

    public Texture2D? Silhouette(BuildingType type) => _silhouette.GetValueOrDefault(type);

    private static ShaderMaterial? _grayscale;

    /// <summary>A canvas material that draws a picture in grayscale at 70% brightness.</summary>
    public static ShaderMaterial GrayscaleMaterial => _grayscale ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = """
                shader_type canvas_item;
                void fragment() {
                    vec4 c = texture(TEXTURE, UV);
                    float g = dot(c.rgb, vec3(0.299, 0.587, 0.114)) * 0.7;
                    COLOR = vec4(vec3(g), c.a);
                }
                """
        }
    };

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }
        RenderAll();
    }

    private async void RenderAll()
    {
        var views = new List<(BuildingType Type, bool Silhouette, SubViewport View)>();
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            views.Add((type, false, MakeView(type, false)));
            views.Add((type, true, MakeView(type, true)));
        }
        foreach ((_, _, SubViewport view) in views)
        {
            AddChild(view);
        }

        // One frame to draw, one more so the read-back is complete.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        foreach ((BuildingType type, bool silhouette, SubViewport view) in views)
        {
            Image? image = view.GetTexture()?.GetImage();
            if (image is not null && !image.IsEmpty())
            {
                ImageTexture texture = ImageTexture.CreateFromImage(image);
                (silhouette ? _silhouette : _color)[type] = texture;
            }
            view.QueueFree();
        }
        IsReady = true;
        PicturesReady?.Invoke();
    }

    private static SubViewport MakeView(BuildingType type, bool silhouette)
    {
        var view = new SubViewport
        {
            Size = new Vector2I(PictureSize, PictureSize),
            TransparentBg = true,
            World3D = Stage.MakeWorld(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X
        };
        Stage.Fill(view, type, silhouette);
        return view;
    }

    /// <summary>
    /// The small world a building picture (or the detail panel's turntable) is drawn
    /// in: its own <see cref="World3D"/>, lit like the game's world (its sun angle and
    /// no environment: under the Compatibility renderer, an environment in any world
    /// carries over into the game's world, which has none), an
    /// orthographic camera at the game camera's angle fitted to the model, and the
    /// model itself (front toward the camera).
    /// </summary>
    public static class Stage
    {
        /// <summary>A separate world for a SubViewport, so the stage never shows in the game's world.</summary>
        public static World3D MakeWorld()
        {
            return new World3D();
        }

        public static Node3D Fill(Node parent, BuildingType type, bool silhouette)
        {
            var sun = new DirectionalLight3D
            {
                LightEnergy = 1.0f
            };
            sun.RotationDegrees = new Vector3(-50, -40, 0);
            parent.AddChild(sun);

            // The game camera looks from +X +Z, and models face -Z, so a South-facing model
            // turns its front to the camera.
            var pivot = new Node3D
            {
                Name = "Pivot"
            };
            parent.AddChild(pivot);
            Node3D model = BuildingModels.Create(type, Facing.South);
            pivot.AddChild(model);
            if (silhouette)
            {
                Paint(model, new StandardMaterial3D
                {
                    AlbedoColor = UiTheme.Silhouette,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                });
            }

            Aabb bounds = ModelLibrary.MeshBounds("buildings/" + BuildingModels.FileName(type));
            float radius = Mathf.Max(bounds.Size.X, bounds.Size.Z) * 0.5f;
            float height = bounds.Size.Y;
            var target = new Vector3(0f, height * 0.4f, 0f);
            var camera = new Camera3D
            {
                Projection = Camera3D.ProjectionType.Orthogonal,
                // Wide enough for the model turned any way, plus a margin.
                Size = Mathf.Max(radius * 2.2f, height * 1.5f) + 0.4f,
                Current = true
            };
            parent.AddChild(camera);
            Vector3 position = target + CameraRig.Offset.Normalized() * 30f;
            camera.Transform = new Transform3D(Basis.LookingAt(target - position, Vector3.Up), position);
            return pivot;
        }

        private static void Paint(Node node, Material material)
        {
            if (node is GeometryInstance3D geometry)
            {
                geometry.MaterialOverride = material;
            }
            foreach (Node child in node.GetChildren())
            {
                Paint(child, material);
            }
        }
    }
}
