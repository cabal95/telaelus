using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The player's character: where it stands, how fast it walks, and the walk it is on
/// (<see cref="Simulation.WalkTo"/>).
/// </summary>
public sealed class Player
{
    public Vector3 Position;
    public float Speed = 6f; // world units per second

    /// <summary>The point the player asked to walk to, or null when not walking.</summary>
    public Vector3? Target { get; internal set; }

    /// <summary>The points still to walk through, the last being where the walk ends.</summary>
    public List<Vector3> Path { get; } = new();

    /// <summary>Where the walk ends (the last point of the path), or null when not walking.</summary>
    public Vector3? Destination => Path.Count > 0 ? Path[^1] : null;

    /// <summary>The ground direction moved this tick (X to world X, Y to world Z), zero when standing.</summary>
    public Vector2 Heading { get; internal set; }

    public bool Walking => Target is not null;
}
