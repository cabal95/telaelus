using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Builds the terrain as one tile per map cell from the simulation's terrain map.
/// Rebuilds from scratch when the map changes outside normal play: a change marks
/// the view dirty, and it rebuilds once on its next frame, so many edits in one
/// frame (a scenario's setup steps) cost one rebuild. It processes while the tree
/// is paused, so edits made while paused still show.
/// </summary>
public partial class TerrainView : Node3D
{
    private Simulation? _sim;
    private bool _dirty;

    public void MarkDirty()
    {
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        if (_dirty && _sim is not null)
        {
            Build(_sim);
        }
    }

    public void Build(Simulation sim)
    {
        _sim = sim;
        _dirty = false;
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        for (int mapX = 0; mapX < sim.MapWidth; mapX++)
        {
            for (int mapZ = 0; mapZ < sim.MapHeight; mapZ++)
            {
                Node3D tile = TerrainTiles.CreateTile(sim, mapX, mapZ);
                tile.Position = Simulation.MapCellCenter(mapX, mapZ);
                AddChild(tile);
            }
        }
    }
}
