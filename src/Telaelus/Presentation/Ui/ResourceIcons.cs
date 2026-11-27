using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The resource icons: each resource's item model (<c>items/&lt;resource&gt;</c>, see
/// the items README) drawn once at startup on the <see cref="PictureStage"/>, filling a
/// 64 px square, and cached. Shown at 20 to 24 UI units, so they stay sharp up to the 2x
/// UI scale. Without a renderer (headless runs) there are no icons and
/// <see cref="For"/> returns null.
/// </summary>
public partial class ResourceIcons : Node
{
    public const int IconSize = 64;

    private Dictionary<ResourceType, Texture2D> _icons = new();
    private Dictionary<string, Texture2D> _uiIcons = new();

    /// <summary>The icon models that stand for no resource (<c>icons/&lt;name&gt;</c>, see the icons README).</summary>
    public static readonly string[] UiIconNames = Ui.UiIcons.Fixed
        .Concat(ResearchDefs.All.Where(p => ResearchDefs.For(p).UnlocksLevel is null && ResearchDefs.For(p).Speeds is null).Select(ResearchTree.ResearchIconName))
        .Concat(Ui.UiIcons.Pictures.Keys)
        .ToArray();

    /// <summary>The model a UI icon is drawn from: <c>icons/&lt;name&gt;</c>, or another model for a picture icon.</summary>
    public static string IconModel(string name)
    {
        return Ui.UiIcons.Pictures.TryGetValue(name, out string? path) ? path : "icons/" + name;
    }

    /// <summary>Raised once the icons are ready.</summary>
    public event Action? IconsReady;

    public bool IsReady { get; private set; }

    public Texture2D? For(ResourceType type) => _icons.GetValueOrDefault(type);

    /// <summary>A UI icon by name ("power"), or null headless.</summary>
    public Texture2D? Icon(string name) => _uiIcons.GetValueOrDefault(name);

    /// <summary>The item model's path for a resource, as "items/iron_ore".</summary>
    public static string ModelPath(ResourceType type)
    {
        return "items/" + SnakeCase(type.ToString());
    }

    public override void _Ready()
    {
        // The models are required even where nothing is drawn, so a missing one fails early.
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            ModelLibrary.MeshBounds(ModelPath(type));
        }
        foreach (string name in UiIconNames)
        {
            ModelLibrary.MeshBounds(IconModel(name));
        }
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }
        RenderAll();
    }

    private async void RenderAll()
    {
        var stages = Enum.GetValues<ResourceType>()
            .Select(type => (type, (Action<Node>)(view =>
                PictureStage.Fill(view, ModelPath(type), 0f, PictureStage.Framing.Tight))));
        _icons = await PictureStage.RenderOnce(this, stages, IconSize);
        var iconStages = UiIconNames.Select(name => (name, (Action<Node>)(view =>
            PictureStage.Fill(view, IconModel(name), 0f, PictureStage.Framing.Tight))));
        _uiIcons = await PictureStage.RenderOnce(this, iconStages, IconSize);
        IsReady = true;
        IconsReady?.Invoke();
    }

    private static string SnakeCase(string name)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                text.Append('_');
            }
            text.Append(char.ToLowerInvariant(name[i]));
        }
        return text.ToString();
    }
}
