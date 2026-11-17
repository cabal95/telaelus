namespace Telaelus.Sim;

/// <summary>
/// Per-tick player input, produced by the presentation and consumed by the
/// simulation. This is the seam that later becomes a networked command: the
/// sim never reads devices, it only reads intent. Walking is a command
/// (<see cref="Simulation.WalkTo"/>), not intent.
/// </summary>
public struct PlayerIntent
{
    /// <summary>True while the player is holding the harvest input.</summary>
    public bool Harvest;
}
