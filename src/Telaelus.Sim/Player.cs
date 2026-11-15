using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The player-controlled character on the planet surface. Position is on the
/// ground plane (Y stays 0 in sim space; the presentation lifts the visual).
/// </summary>
public sealed class Player
{
    public Vector3 Position;
    public float Speed = 6f; // world units per second
}
