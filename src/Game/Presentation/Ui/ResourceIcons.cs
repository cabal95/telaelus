using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The resource icons: each resource's item model (<c>items/item_&lt;resource&gt;</c>, see
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
        .Concat(ResearchDefs.All.Select(ResearchTree.ResearchIconName))
        .ToArray();

    /// <summary>Raised once the icons are ready.</summary>
    public event Action? IconsReady;

    public bool IsReady { get; private set; }

    public Texture2D? For(ResourceType type) => _icons.GetValueOrDefault(type);

    /// <summary>A UI icon by name ("icon_power"), or null headless.</summary>
    public Texture2D? Icon(string name) => _uiIcons.GetValueOrDefault(name);

    /// <summary>The item model's path for a resource, as "items/item_iron_ore".</summary>
    public static string ModelPath(ResourceType type)
    {
        return "items/item_" + SnakeCase(type.ToString());
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
            ModelLibrary.MeshBounds("icons/" + name);
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
                PictureStage.Fill(view, ModelPath(type), 0f, false, PictureStage.Framing.Tight))));
        _icons = await PictureStage.RenderOnce(this, stages, IconSize);
        var iconStages = UiIconNames.Select(name => (name, (Action<Node>)(view =>
            PictureStage.Fill(view, "icons/" + name, 0f, false, PictureStage.Framing.Tight))));
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
