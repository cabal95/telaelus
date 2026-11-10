using Godot;

namespace Game.Sim;

/// <summary>
/// The authoritative game state and logic. It advances in fixed steps and
/// knows nothing about rendering, input, or the Godot scene tree. In
/// single-player this runs in-process; conceptually it is the future "server".
///
/// It may use Godot's math value types (Vector3 and friends), but never
/// Nodes or the SceneTree, so it stays testable and networkable.
/// </summary>
public sealed class Simulation
{
    // Milestone 0 placeholder: a single entity that moves, to prove the
    // fixed-step sim drives what we see. Real entities arrive in later
    // milestones (resources, facilities, vehicles).
    public Vector3 MoverPosition { get; private set; }

    private double _time;

    private const float Radius = 4f;
    private const float Speed = 1.0f; // radians per second

    public void Step(double dt)
    {
        _time += dt;
        float a = (float)_time * Speed;
        MoverPosition = new Vector3(Mathf.Cos(a) * Radius, 0.5f, Mathf.Sin(a) * Radius);
    }
}
