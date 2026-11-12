using Game.Sim;
using Game.Testing;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Entry point node (attached to the Main scene root). It owns the simulation
/// and the presentation, reads input into per-tick intent, steps the sim on a
/// fixed clock, and mirrors sim state onto visible nodes.
/// </summary>
public partial class GameRoot : Node3D
{
    private Simulation _sim = null!;
    private readonly SimClock _clock = new(30.0); // 30 Hz simulation tick

    private PlayerView _playerView = null!;
    private CameraRig _camera = null!;
    private ResourceFieldViews _fieldViews = null!;
    private BuildingViews _buildingViews = null!;
    private BuildController _buildController = null!;
    private Hud _hud = null!;
    private TerrainView _terrainView = null!;
    private PerfMonitor _perf = null!;
    private readonly System.Diagnostics.Stopwatch _simWatch = new();

    // Read-only access for the testing code (scenario runner and live channel).
    public Simulation Sim => _sim;
    public CameraRig Camera => _camera;
    public BuildController Builder => _buildController;
    public ResourceFieldViews FieldViews => _fieldViews;
    public TerrainView Terrain => _terrainView;
    public PerfMonitor Perf => _perf;

    public override void _Ready()
    {
        _sim = new Simulation(WorldSettings.Generated(ReadSeed()));
        BuildWorld();
        ScenarioRunner.AttachIfRequested(this);
        ControlChannel.AttachIfRequested(this);
    }

    public override void _Process(double delta)
    {
        Vector2 moveDir = ReadWorldMoveInput();
        _sim.Intent = new PlayerIntent
        {
            Move = moveDir,
            Harvest = Input.IsPhysicalKeyPressed(Key.E)
        };

        int steps = _clock.Advance(delta);
        _simWatch.Restart();
        for (int i = 0; i < steps; i++)
        {
            _sim.Step(_clock.FixedDelta);
        }
        _perf.SimMs = _simWatch.Elapsed.TotalMilliseconds;

        _playerView.UpdateFrom(_sim.Player.Position, moveDir, delta);
        _camera.SetTarget(_sim.Player.Position);
        _fieldViews.Sync(_sim.Fields, _terrainView);
        _buildingViews.Sync(_sim.Buildings);
        _hud.Refresh(
            _sim.Stockpile,
            _sim.HarvestTarget,
            _buildController.Selected,
            _buildController.Facing,
            _buildController.FieldsInRange,
            _sim.CanLaunchShip(),
            _sim.ShipsLaunched,
            Hud.Diagnostics(_sim));
        _hud.RefreshPerf(_perf);
    }

    /// <summary>The world seed from <c>--seed=&lt;n&gt;</c> after the <c>--</c> separator, or a random one.</summary>
    private static int ReadSeed()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--seed=") && int.TryParse(arg["--seed=".Length..], out int seed))
            {
                return seed;
            }
        }
        return (int)(GD.Randi() & 0x7FFFFFFF);
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
        else if (@event is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.L)
        {
            _sim.TryLaunchShip();
        }
        else if (@event is InputEventKey f3 && f3.Pressed && !f3.Echo && f3.PhysicalKeycode == Key.F3)
        {
            _hud.ShowPerf = !_hud.ShowPerf;
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
        _perf = new PerfMonitor
        {
            Name = "PerfMonitor",
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_perf);
        AddChild(new PerfFrameStart
        {
            Name = "PerfFrameStart",
            ProcessMode = ProcessModeEnum.Always,
            Monitor = _perf
        });

        _terrainView = new TerrainView
        {
            Name = "Terrain",
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_terrainView);
        _sim.MapChanged += _terrainView.MarkDirty;

        var light = new DirectionalLight3D
        {
            ShadowEnabled = true
        };
        light.RotationDegrees = new Vector3(-50, -40, 0);
        AddChild(light);

        _fieldViews = new ResourceFieldViews
        {
            Name = "ResourceFields"
        };
        AddChild(_fieldViews);

        _buildingViews = new BuildingViews
        {
            Name = "Buildings"
        };
        AddChild(_buildingViews);

        _playerView = new PlayerView
        {
            Name = "Player"
        };
        AddChild(_playerView);

        _camera = new CameraRig();
        _camera.SetTarget(_sim.Player.Position);
        AddChild(_camera);
        _terrainView.Initialize(_sim, _camera);

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
