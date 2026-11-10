using Godot;

namespace Game.Sim;

/// <summary>
/// A placed building. An Extractor draws from a bound resource node; a processor
/// (Smelter, Refinery) runs a Recipe against the shared stockpile.
/// </summary>
public sealed class Building
{
    public int Id { get; }
    public BuildingType Type { get; }
    public int GridX { get; }
    public int GridZ { get; }
    public Vector3 Position { get; }
    public Recipe? Recipe { get; }

    // Extractor: the node it draws from (null once that node is depleted).
    public ResourceNode? Source { get; set; }

    // Processor: current cycle state.
    public bool Crafting { get; set; }
    public float Progress { get; set; }

    public Building(int id, BuildingType type, int gridX, int gridZ, Vector3 position, Recipe? recipe)
    {
        Id = id;
        Type = type;
        GridX = gridX;
        GridZ = gridZ;
        Position = position;
        Recipe = recipe;
    }
}
