using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>Builds the terrain as one tile per map cell from the simulation's terrain map.</summary>
public partial class TerrainView : Node3D
{
    public void Build(Simulation sim)
    {
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
