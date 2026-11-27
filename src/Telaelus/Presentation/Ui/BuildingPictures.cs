using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// Renders every building at every level once, at startup, into a picture from the game camera's angle on the
/// <see cref="PictureStage"/>, cached as textures, so menus never hold live 3D views. Grayscale is
/// a shader on the picture (<see cref="GrayscaleMaterial"/>). Without a renderer (headless runs)
/// there are no pictures and <see cref="Color"/> returns null.
/// </summary>
public partial class BuildingPictures : Node
{
    public const int PictureSize = 256;

    private Dictionary<(BuildingType Type, int Level), Texture2D> _pictures = new();

    /// <summary>Raised once the pictures are ready.</summary>
    public event Action? PicturesReady;

    public bool IsReady { get; private set; }

    public Texture2D? Color(BuildingType type, int level = 1) => _pictures.GetValueOrDefault((type, level));

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
        var stages = BuildingDefs.AllLevels()
            .Select(key => (key, (Action<Node>)(view => PictureStage.Fill(view, key.Type, key.Level))));
        _pictures = await PictureStage.RenderOnce(this, stages, PictureSize);
        IsReady = true;
        PicturesReady?.Invoke();
    }
}
