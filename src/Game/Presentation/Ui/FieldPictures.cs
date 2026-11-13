using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// A still picture of a full resource field (all five slots, as a field at 5/5 shows
/// them) for each deposit, drawn once at startup on the <see cref="PictureStage"/> for the
/// field detail panel. Each is one fixed variation: a stand-in field with a fixed id
/// and its biome's starting amount, its slots placed by the same rules the map uses
/// (<see cref="ResourceFieldViews.SlotPosition"/>, <see cref="NodeModels.Slot"/>). Wood
/// is drawn once per tree family, so a field's picture shows the trees its biome grows;
/// the biome only picks the picture. Headless runs have no pictures.
/// </summary>
public partial class FieldPictures : Node
{
    public const int PictureSize = 256;

    /// <summary>The stand-in field's id: it fixes the slot positions, variants, and yaws.</summary>
    private const int PictureFieldId = 3;

    private Dictionary<(ResourceType Type, string Family), Texture2D> _pictures = new();

    public bool IsReady { get; private set; }

    /// <summary>The picture for a field of a type in a biome, or null (headless, or not yet drawn).</summary>
    public Texture2D? For(ResourceType type, BiomeType biome)
    {
        return _pictures.GetValueOrDefault(Key(type, biome));
    }

    private static (ResourceType, string) Key(ResourceType type, BiomeType biome)
    {
        return (type, type == ResourceType.Wood ? NodeModels.TreeFamilyOf(biome) : "");
    }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }
        RenderAll();
    }

    /// <summary>One biome per picture: Grassland for every deposit, plus Forest and Swamp for Wood's other families.</summary>
    private static IEnumerable<(ResourceType Type, BiomeType Biome)> Pictures()
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>().Where(t => ResourceDefs.For(t).HasFields))
        {
            yield return (type, BiomeType.Grassland);
        }
        yield return (ResourceType.Wood, BiomeType.Forest);
        yield return (ResourceType.Wood, BiomeType.Swamp);
    }

    private async void RenderAll()
    {
        var stages = Pictures().Select(picture => (Key(picture.Type, picture.Biome),
            (Action<Node>)(view => PictureStage.FillMeshes(view, Slots(picture.Type, picture.Biome)))));
        _pictures = await PictureStage.RenderOnce(this, stages, PictureSize);
        IsReady = true;
    }

    /// <summary>The five slot meshes of a full stand-in field, around the origin.</summary>
    private static List<(Mesh Mesh, Transform3D Transform)> Slots(ResourceType type, BiomeType biome)
    {
        float amount = BiomeDefs.For(biome).FieldAmount(type);
        var field = new ResourceField(PictureFieldId, type, 0, 0, amount);
        var parts = new List<(Mesh, Transform3D)>();
        for (int i = 0; i < ResourceField.StageCount; i++)
        {
            Vector3 slot = ResourceFieldViews.SlotPosition(field, i) - field.Center;
            (string path, Transform3D transform) = NodeModels.Slot(
                type, biome, field.Id * ResourceField.StageCount + i, ResourceFieldViews.SlotFootprint, amount, slot);
            parts.Add((ModelLibrary.Mesh(path).Mesh, transform));
        }
        return parts;
    }
}
