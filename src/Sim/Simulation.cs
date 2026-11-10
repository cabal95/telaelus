using Godot;

namespace Game.Sim;

/// <summary>
/// The authoritative game state and logic. Fixed-step, and free of Godot nodes
/// and input devices. In single-player this runs in-process; conceptually it is
/// the future authoritative "server".
/// </summary>
public sealed class Simulation
{
    // Grid definition. Buildings will later snap to this; for now it defines the
    // walkable extent and drives the rendered ground. The grid is centered on
    // the world origin.
    public int GridWidth { get; } = 32;
    public int GridHeight { get; } = 32;
    public float TileSize { get; } = 1f;

    public Player Player { get; } = new();

    /// <summary>Input for the current tick, set by the presentation before Step.</summary>
    public PlayerIntent Intent;

    public float WorldSizeX => GridWidth * TileSize;
    public float WorldSizeZ => GridHeight * TileSize;

    public void Step(double dt)
    {
        Vector2 move = Intent.Move;
        if (move.LengthSquared() > 1f)
        {
            move = move.Normalized();
        }

        Vector3 p = Player.Position + new Vector3(move.X, 0f, move.Y) * Player.Speed * (float)dt;

        // Keep the player inside the grid extent.
        float halfX = WorldSizeX * 0.5f;
        float halfZ = WorldSizeZ * 0.5f;
        p.X = Mathf.Clamp(p.X, -halfX, halfX);
        p.Z = Mathf.Clamp(p.Z, -halfZ, halfZ);
        Player.Position = p;
    }
}
