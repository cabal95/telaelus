using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>Builds the terrain as one tile per grid cell from the simulation's terrain map.</summary>
public partial class TerrainView : Node3D
{
    public void Build(Simulation sim)
    {
        int halfX = sim.GridWidth / 2;
        int halfZ = sim.GridHeight / 2;
        for (int x = -halfX; x <= halfX; x++)
        {
            for (int z = -halfZ; z <= halfZ; z++)
            {
                Node3D tile = TerrainTiles.CreateTile(sim, x, z);
                tile.Position = sim.TileCenter(x, z);
                AddChild(tile);
            }
        }
    }
}
