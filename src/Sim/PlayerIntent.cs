using Godot;

namespace Game.Sim;

/// <summary>
/// Per-tick player input, produced by the presentation and consumed by the
/// simulation. This is the seam that later becomes a networked command: the
/// sim never reads devices, it only reads intent.
/// </summary>
public struct PlayerIntent
{
    /// <summary>
    /// Desired movement on the ground, already in world space: X maps to world
    /// X, Y maps to world Z. Magnitude above 1 is clamped by the sim.
    /// </summary>
    public Vector2 Move;
}
