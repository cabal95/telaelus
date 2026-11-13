using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The small world a model is drawn in for the UI (building pictures, resource icons,
/// and the detail panel's turntable): its own <see cref="World3D"/>, lit like the
/// game's world (its sun angle and no environment: under the Compatibility renderer, an
/// environment in any world carries over into the game's world, which has none), an
/// orthographic camera at the game camera's angle fitted to the model, and the model.
/// <see cref="RenderOnce{TKey}"/> draws a set of stages once into cached textures.
/// </summary>
public static class PictureStage
{
    /// <summary>How much room a picture leaves around its model.</summary>
    public enum Framing
    {
        /// <summary>Room for the model turned any way, plus a margin (pictures, turntables).</summary>
        Loose,

        /// <summary>The model filling the square (icons).</summary>
        Tight
    }

    /// <summary>A separate world for a SubViewport, so the stage never shows in the game's world.</summary>
    public static World3D MakeWorld()
    {
        return new World3D();
    }

    /// <summary>Stages a building, its front toward the camera; returns the pivot it turns on.</summary>
    public static Node3D Fill(Node parent, BuildingType type, bool silhouette)
    {
        // The game camera looks from +X +Z, and models face -Z, so a South-facing model
        // turns its front to the camera.
        return Fill(parent, "buildings/" + BuildingModels.FileName(type), ModelLibrary.EdgeYaw(Facing.South), silhouette, Framing.Loose);
    }

    /// <summary>Stages any model by its path (as for <see cref="ModelLibrary.Instance(string)"/>); returns its pivot.</summary>
    public static Node3D Fill(Node parent, string modelPath, float yawDegrees, bool silhouette, Framing framing)
    {
        var sun = new DirectionalLight3D
        {
            LightEnergy = 1.0f
        };
        sun.RotationDegrees = new Vector3(-50, -40, 0);
        parent.AddChild(sun);

        var pivot = new Node3D
        {
            Name = "Pivot"
        };
        parent.AddChild(pivot);
        Node3D model = ModelLibrary.Instance(modelPath, yawDegrees);
        pivot.AddChild(model);
        if (silhouette)
        {
            Paint(model, new StandardMaterial3D
            {
                AlbedoColor = UiTheme.Silhouette,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            });
        }

        Aabb bounds = ModelLibrary.MeshBounds(modelPath);
        float radius = Mathf.Max(bounds.Size.X, bounds.Size.Z) * 0.5f;
        float height = bounds.Size.Y;
        var target = new Vector3(0f, height * (framing == Framing.Tight ? 0.5f : 0.4f), 0f);
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = framing == Framing.Tight
                ? Mathf.Max(radius * 2f, height) * 1.2f
                : Mathf.Max(radius * 2.2f, height * 1.5f) + 0.4f,
            Current = true
        };
        parent.AddChild(camera);
        Vector3 position = target + CameraRig.Offset.Normalized() * 30f;
        camera.Transform = new Transform3D(Basis.LookingAt(target - position, Vector3.Up), position);
        return pivot;
    }

    /// <summary>
    /// Draws each stage once into a square texture of the given size and returns the
    /// textures by key, after two frames (one to draw, one so the read-back is
    /// complete). Stages that draw nothing (no renderer) are left out.
    /// </summary>
    public static async Task<Dictionary<TKey, Texture2D>> RenderOnce<TKey>(
        Node owner, IEnumerable<(TKey Key, Action<Node> Fill)> stages, int size)
        where TKey : notnull
    {
        var views = new List<(TKey Key, SubViewport View)>();
        foreach ((TKey key, Action<Node> fill) in stages)
        {
            var view = new SubViewport
            {
                Size = new Vector2I(size, size),
                TransparentBg = true,
                World3D = MakeWorld(),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
                Msaa3D = Viewport.Msaa.Msaa4X
            };
            fill(view);
            owner.AddChild(view);
            views.Add((key, view));
        }

        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        var textures = new Dictionary<TKey, Texture2D>();
        foreach ((TKey key, SubViewport view) in views)
        {
            Image? image = view.GetTexture()?.GetImage();
            if (image is not null && !image.IsEmpty())
            {
                textures[key] = ImageTexture.CreateFromImage(image);
            }
            view.QueueFree();
        }
        return textures;
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
