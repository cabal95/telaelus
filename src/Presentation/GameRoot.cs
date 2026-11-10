using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Entry point node (attached to the Main scene root). It owns the simulation
/// and the presentation, steps the sim on a fixed clock, and mirrors sim state
/// onto visible nodes. For now this single place is where the "server" and
/// "client" meet; when multiplayer arrives they split along this seam.
/// </summary>
public partial class GameRoot : Node3D
{
    private readonly Simulation _sim = new();
    private readonly SimClock _clock = new(30.0); // 30 Hz simulation tick

    private MeshInstance3D _mover = null!;

    public override void _Ready()
    {
        BuildWorld();
    }

    public override void _Process(double delta)
    {
        // Advance the simulation in fixed steps, independent of framerate.
        int steps = _clock.Advance(delta);
        for (int i = 0; i < steps; i++)
            _sim.Step(_clock.FixedDelta);

        // Presentation simply mirrors the authoritative state.
        _mover.Position = _sim.MoverPosition;
    }

    /// <summary>
    /// Builds the Milestone 0 scene in code: an angled orthographic camera for
    /// the 2.5D look, a light, a ground plane, and the sim-driven mover.
    /// </summary>
    private void BuildWorld()
    {
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 16f, // orthographic view height in world units (zoom)
            Position = new Vector3(12, 12, 12),
            Current = true,
        };
        AddChild(camera);
        // LookAt must run after the node is in the tree (it works in global space).
        camera.LookAt(Vector3.Zero, Vector3.Up);

        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-50, -40, 0);
        AddChild(light);

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(24, 24) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.25f, 0.22f) },
        };
        AddChild(ground);

        _mover = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1, 1, 1) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.5f, 0.2f) },
        };
        AddChild(_mover);
    }
}
