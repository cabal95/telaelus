using Godot;

namespace Game.Sim;

/// <summary>
/// A placed building. Its anchor (minimum-corner fine cell) and facing are its
/// authoritative placement; the footprint size and center Position are derived
/// from them. An extractor (Mine, Lumber Camp) works resource fields within its
/// work area; a processor (Smelter, Refinery) runs a Recipe against the shared
/// stockpile.
/// </summary>
public sealed class Building
{
    public int Id { get; }
    public BuildingType Type { get; }
    public BuildingDef Def { get; }
    public int AnchorX { get; }
    public int AnchorZ { get; }
    public Facing Facing { get; }
    public Recipe? Recipe { get; }

    /// <summary>Whether the building runs; a switched-off building does nothing (see Simulation.SetBuildingEnabled).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A Silo's chosen Bulk resource (see Simulation.SetSiloResource).</summary>
    public ResourceType SiloResource { get; set; } = ResourceType.Wood;

    // Extractor: the field it is currently working (null when idle).
    public ResourceField? CurrentField { get; set; }

    // Extractor: seconds toward the next unit from its current field.
    // Processor: seconds into the current cycle, and whether a cycle is running.
    public bool Crafting { get; set; }
    public float Progress { get; set; }

    public Building(int id, BuildingType type, int anchorX, int anchorZ, Facing facing, Recipe? recipe)
    {
        Id = id;
        Type = type;
        Def = BuildingDefs.For(type);
        AnchorX = anchorX;
        AnchorZ = anchorZ;
        Facing = facing;
        Recipe = recipe;
    }

    public int Width => Def.Footprint(Facing).Width;

    public int Depth => Def.Footprint(Facing).Depth;

    /// <summary>World-space center of the footprint (derived, not stored).</summary>
    public Vector3 Position => Def.Center(AnchorX, AnchorZ, Facing);
}
