using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Entry point node (attached to the Main scene root). It owns the simulation
/// and the presentation, reads input into per-tick intent, steps the sim on a
/// fixed clock, and mirrors sim state onto visible nodes.
/// </summary>
public partial class GameRoot : Node3D
{
    private readonly Simulation _sim = new();
    private readonly SimClock _clock = new(30.0); // 30 Hz simulation tick

    // Lift the player capsule so it rests on the ground (capsule is centered).
    private static readonly Vector3 PlayerVisualLift = new(0, 0.7f, 0);

    private MeshInstance3D _playerView = null!;
    private CameraRig _camera = null!;
    private ResourceNodeViews _nodeViews = null!;
    private BuildingViews _buildingViews = null!;
    private BuildController _buildController = null!;
    private Hud _hud = null!;

    public override void _Ready()
    {
        BuildWorld();
    }

    public override void _Process(double delta)
    {
        _sim.Intent = new PlayerIntent
        {
            Move = ReadWorldMoveInput(),
            Harvest = Input.IsPhysicalKeyPressed(Key.E)
        };

        int steps = _clock.Advance(delta);
        for (int i = 0; i < steps; i++)
        {
            _sim.Step(_clock.FixedDelta);
        }

        _playerView.Position = _sim.Player.Position + PlayerVisualLift;
        _camera.SetTarget(_sim.Player.Position);
        _nodeViews.Sync(_sim.Nodes);
        _buildingViews.Sync(_sim.Buildings);
        _hud.Refresh(_sim.Stockpile, _sim.HarvestTarget, _buildController.Selected);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                _camera.Zoom(-1f); // zoom in
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                _camera.Zoom(1f); // zoom out
            }
        }
    }

    /// <summary>
    /// Reads WASD / arrows as screen-relative input and rotates it into world
    /// space using the camera's orientation, so "up" moves away from the camera
    /// regardless of the fixed view angle.
    /// </summary>
    private Vector2 ReadWorldMoveInput()
    {
        var raw = Vector2.Zero; // x = screen right, y = screen up (forward)
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
        {
            raw.Y += 1;
        }
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
        {
            raw.Y -= 1;
        }
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
        {
            raw.X += 1;
        }
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
        {
            raw.X -= 1;
        }

        Basis b = _camera.GlobalTransform.Basis;
        Vector3 forward = -b.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        Vector3 right = b.X;
        right.Y = 0;
        right = right.Normalized();
        Vector3 world = right * raw.X + forward * raw.Y;
        return new Vector2(world.X, world.Z);
    }

    private void BuildWorld()
    {
        AddChild(GroundGrid.Build(_sim.GridWidth, _sim.GridHeight, _sim.TileSize));

        var light = new DirectionalLight3D
        {
            ShadowEnabled = true
        };
        light.RotationDegrees = new Vector3(-50, -40, 0);
        AddChild(light);

        _nodeViews = new ResourceNodeViews
        {
            Name = "ResourceNodes"
        };
        AddChild(_nodeViews);

        _buildingViews = new BuildingViews
        {
            Name = "Buildings"
        };
        AddChild(_buildingViews);

        _playerView = new MeshInstance3D
        {
            Name = "Player",
            Mesh = new CapsuleMesh
            {
                Radius = 0.35f,
                Height = 1.4f
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.90f, 0.55f, 0.20f)
            },
            Position = _sim.Player.Position + PlayerVisualLift
        };
        AddChild(_playerView);

        _camera = new CameraRig();
        _camera.SetTarget(_sim.Player.Position);
        AddChild(_camera);

        _buildController = new BuildController
        {
            Name = "BuildController"
        };
        _buildController.Initialize(_sim, _camera);
        AddChild(_buildController);

        _hud = new Hud
        {
            Name = "Hud"
        };
        AddChild(_hud);
    }
}
