using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds a building's model: one per type, authored facing North (front toward
/// -Z) and filling its footprint minus a small inset, origin at the footprint
/// center on the ground. It is turned to the building's facing. Used by both
/// placed buildings and the build ghost, so the two always match.
/// </summary>
public static class BuildingModels
{
    public static Node3D Create(BuildingType type, Facing facing)
    {
        Node3D model = ModelLibrary.Instance("buildings/" + FileName(type), ModelLibrary.EdgeYaw(facing));
        model.Name = type.ToString();
        return model;
    }

    public static string FileName(BuildingType type)
    {
        return type switch
        {
            BuildingType.LumberCamp => "lumber_camp",
            _ => type.ToString().ToLowerInvariant()
        };
    }
}
