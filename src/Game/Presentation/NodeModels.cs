using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// The rules for drawing one resource model (a slot in a resource field) from its glTF
/// models (required; see <see cref="ModelLibrary"/>): which model, and the transform
/// that places it. A model is scaled to a target height from its bounding box, so differently
/// sized source models all render at the right in-world size (our render-scaling
/// control), then seated on the ground and given a deterministic yaw per node.
/// </summary>
public static class NodeModels
{
    private sealed record ModelSpec(string[] Paths, float TargetHeight);

    private static readonly Dictionary<ResourceType, ModelSpec> Specs = new()
    {
        [ResourceType.IronOre] = new ModelSpec(
            new[]
            {
                "nature/iron_ore_a",
                "nature/iron_ore_b",
                "nature/iron_ore_c"
            },
            0.8f),
        [ResourceType.CopperOre] = new ModelSpec(
            new[]
            {
                "nature/copper_ore_a",
                "nature/copper_ore_b",
                "nature/copper_ore_c"
            },
            0.8f),
        [ResourceType.Coal] = new ModelSpec(
            new[]
            {
                "nature/coal_a",
                "nature/coal_b",
                "nature/coal_c"
            },
            0.6f),
        [ResourceType.Stone] = new ModelSpec(
            new[]
            {
                "nature/stone_a",
                "nature/stone_b",
                "nature/stone_c"
            },
            0.9f),
        [ResourceType.CrudeOil] = new ModelSpec(
            new[]
            {
                "nature/crude_oil_a",
                "nature/crude_oil_b",
                "nature/crude_oil_c"
            },
            0.05f),
        [ResourceType.Wood] = TreeFamily("broadleaf")
    };

    /// <summary>The tree families, by name (see <see cref="TreeFamilyOf"/>).</summary>
    private static readonly Dictionary<string, ModelSpec> TreeFamilies = new()
    {
        ["conifer"] = TreeFamily("conifer"),
        ["swamp"] = TreeFamily("swamp"),
        ["broadleaf"] = TreeFamily("broadleaf")
    };

    /// <summary>
    /// The tree family a biome's Wood fields draw: conifers in Forest, swamp trees in
    /// Swamp, and broadleaf trees in Grassland and anywhere else.
    /// </summary>
    public static string TreeFamilyOf(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest => "conifer",
            BiomeType.Swamp => "swamp",
            _ => "broadleaf"
        };
    }

    /// <summary>
    /// A deposit without field models of its own (one no world places yet) draws its item model
    /// in every slot, so a field placed through the debug tools still shows.
    /// </summary>
    private static ModelSpec ItemStandIn(ResourceType type)
    {
        return new ModelSpec(
            new[]
            {
                Ui.ResourceIcons.ModelPath(type)
            },
            0.6f);
    }

    private static ModelSpec TreeFamily(string family)
    {
        return new ModelSpec(
            new[]
            {
                $"nature/tree_{family}_a",
                $"nature/tree_{family}_b",
                $"nature/tree_{family}_c"
            },
            2.5f);
    }

    /// <summary>
    /// The model and transform for one slot of a field (a Wood field draws its biome's
    /// tree family): the variant and yaw are chosen
    /// deterministically from <paramref name="seedId"/>, the model is scaled to its
    /// deposit's target height (Wood by the field's <paramref name="initialAmount"/>, see
    /// <see cref="WoodHeight"/>) unless that would make it wider than
    /// <paramref name="maxFootprint"/>, and it is seated on the ground, centered on
    /// <paramref name="slotPosition"/>. The transform includes the mesh's own transform
    /// within its model, so it can be used directly as a multimesh instance transform.
    /// </summary>
    public static (string Path, Transform3D Transform) Slot(
        ResourceType type, BiomeType biome, int seedId, float maxFootprint, float initialAmount, Vector3 slotPosition)
    {
        if (type == ResourceType.Wood)
        {
            return Place(TreeFamilies[TreeFamilyOf(biome)], type, seedId, maxFootprint, initialAmount, slotPosition);
        }
        return Place(Specs.GetValueOrDefault(type) ?? ItemStandIn(type), type, seedId, maxFootprint, initialAmount, slotPosition);
    }

    private static (string Path, Transform3D Transform) Place(
        ModelSpec spec, ResourceType type, int seedId, float maxFootprint, float initialAmount, Vector3 slotPosition)
    {
        float targetHeight = type == ResourceType.Wood ? WoodHeight(initialAmount) : spec.TargetHeight;
        string path = spec.Paths[seedId % spec.Paths.Length];

        (Mesh _, Transform3D meshTransform) = ModelLibrary.Mesh(path);
        Aabb bounds = ModelLibrary.MeshBounds(path);
        float height = bounds.Size.Y;
        float footprint = Mathf.Max(bounds.Size.X, bounds.Size.Z);
        float heightScale = height > 0.0001f ? targetHeight / height : 1f;
        float footprintScale = footprint > 0.0001f ? maxFootprint / footprint : heightScale;
        float scale = Mathf.Min(heightScale, footprintScale);

        // Seat: center the bounds on the slot in x and z, and rest the bottom on the ground.
        Vector3 center = bounds.Position + bounds.Size * 0.5f;
        var seat = new Vector3(-center.X * scale, -bounds.Position.Y * scale, -center.Z * scale);
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(DeterministicYaw(seedId)));
        var transform = new Transform3D(basis, slotPosition)
            * new Transform3D(Basis.Identity.Scaled(new Vector3(scale, scale, scale)), seat)
            * meshTransform;
        return (path, transform);
    }

    /// <summary>Tree height for a Wood field: 2.5 units at 250 or less, rising to 3.5 at 500 or more.</summary>
    public static float WoodHeight(float initialAmount)
    {
        return 2.5f + Mathf.Clamp((initialAmount - 250f) / 250f, 0f, 1f);
    }

    private static float DeterministicYaw(int seedId)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (ulong)seedId * 2654435761UL + 1UL
        };
        return rng.RandfRange(0f, 360f);
    }
}
