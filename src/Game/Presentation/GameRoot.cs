using System.Collections.Generic;
using Game.Presentation.Ui;
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
    private DebugOverlay _debug = null!;
    private TopBar _topBar = null!;
    private HintOverlay _hint = null!;
    private InventoryPanel _inventory = null!;
    private FieldDetail _fieldDetail = null!;
    private FieldPictures _fieldPictures = null!;
    private DialogHost _dialogs = null!;
    private BuildMenu _buildMenu = null!;
    private BuildingDetail _buildingDetail = null!;
    private ResourceIcons _icons = null!;
    private TerrainView _terrainView = null!;
    private PerfMonitor _perf = null!;
    private DirectionalLight3D _light = null!;
    private readonly System.Diagnostics.Stopwatch _simWatch = new();
    private MouseButton? _dragButton; // the button holding a camera drag, if any

    // Read-only access for the testing code (scenario runner and live channel).
    public Simulation Sim => _sim;
    public CameraRig Camera => _camera;
    public BuildController Builder => _buildController;
    public ResourceFieldViews FieldViews => _fieldViews;
    public TerrainView Terrain => _terrainView;
    public PerfMonitor Perf => _perf;
    public DialogHost Dialogs => _dialogs;
    public BuildMenu BuildMenu => _buildMenu;
    public BuildingDetail BuildingDetail => _buildingDetail;
    public ResourceIcons Icons => _icons;
    public DebugOverlay Debug => _debug;
    public TopBar TopBar => _topBar;
    public HintOverlay Hint => _hint;
    public InventoryPanel Inventory => _inventory;
    public FieldDetail FieldDetail => _fieldDetail;

    public override void _Ready()
    {
        _sim = new Simulation(WorldSettings.Generated(ReadSeed()));
        BuildWorld();
        ScenarioRunner.AttachIfRequested(this);
        ControlChannel.AttachIfRequested(this);
    }

    public override void _Process(double delta)
    {
        // A modal takes all input, so the world reads none.
        bool worldInput = !_dialogs.CapturesWorldInput;
        Vector2 moveDir = worldInput ? ReadWorldMoveInput() : Vector2.Zero;
        if (moveDir != Vector2.Zero && _camera.Detached)
        {
            // Moving brings a dragged-away camera back to the player.
            _dragButton = null;
            _camera.ReturnToPlayer(PlayerOnScreen());
        }
        _sim.Intent = new PlayerIntent
        {
            Move = moveDir,
            Harvest = worldInput && Input.IsPhysicalKeyPressed(Key.E)
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
        // Shadows are drawn only up to this distance from the camera, which moves back as the
        // view grows (see CameraRig.ViewDistance), so the range follows it.
        _light.DirectionalShadowMaxDistance = _camera.ViewDistance + _camera.Size;
        _fieldViews.Sync(_sim, _terrainView);
        _buildingViews.Sync(ExploredBuildings());
        _topBar.Refresh(_sim, _dialogs.UiSize);
        _hint.Refresh(_sim, _dialogs.UiSize);
        _debug.Refresh(_sim, _perf);
        _dialogs.PanelTop = TopBar.Height;
        _buildController.Focused = _dialogs.Current == _buildingDetail ? _buildingDetail.Building : null;
        _buildController.FocusedField = _dialogs.Current == _fieldDetail ? _fieldDetail.Field : null;
        UpdatePlacementTip();
    }

    /// <summary>The placed buildings standing on explored cells (only debug placement can put one elsewhere).</summary>
    private List<Building> ExploredBuildings()
    {
        var shown = new List<Building>(_sim.Buildings.Count);
        foreach (Building building in _sim.Buildings)
        {
            (int x, int z) = (building.AnchorX, building.AnchorZ);
            if (_sim.IsExplored(Simulation.MapCellOf(x), Simulation.MapCellOf(z)))
            {
                shown.Add(building);
            }
        }
        return shown;
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

    /// <summary>
    /// While a camera drag is held, its motion and release are followed from here, before the UI,
    /// so the drag keeps going when the pointer crosses a panel.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_dragButton is not MouseButton button)
        {
            return;
        }
        if (@event is InputEventMouseMotion motion)
        {
            _camera.DragTo(motion.Position);
        }
        else if (@event is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == button)
        {
            _camera.CancelDrag();
            _dragButton = null;
        }
    }

    /// <summary>Whether the player is inside the view and not under an open panel.</summary>
    private bool PlayerOnScreen()
    {
        Rect2 view = GetViewport().GetVisibleRect();
        Vector2 at = _camera.UnprojectPosition(_sim.Player.Position);
        return view.HasPoint(at) && !(_dialogs.PanelScreenRect() is Rect2 panel && panel.HasPoint(at));
    }

    /// <summary>The ground rectangle the detached camera's center stays in: the explored area's bounding box.</summary>
    private Rect2? CameraBounds()
    {
        if (_sim.ExploredBounds is not (int minX, int minZ, int maxX, int maxZ))
        {
            return null;
        }
        float cell = Simulation.FineCellsPerMapCell;
        return new Rect2(minX * cell, minZ * cell, (maxX - minX + 1) * cell, (maxZ - minZ + 1) * cell);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A press over the world with the right button, or the left with Ctrl, may drag the map.
        if (@event is InputEventMouseButton press && press.Pressed && _dragButton is null
            && (press.ButtonIndex == MouseButton.Right || (press.ButtonIndex == MouseButton.Left && press.CtrlPressed)))
        {
            _dragButton = press.ButtonIndex;
            _camera.BeginDrag(press.Position);
            return;
        }
        if (@event is InputEventKey space && space.Pressed && !space.Echo && space.PhysicalKeycode == Key.Space)
        {
            _dragButton = null;
            _camera.ReturnToPlayer(PlayerOnScreen());
            return;
        }
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
            _debug.Shown = !_debug.Shown;
        }
        else if (@event is InputEventKey b && b.Pressed && !b.Echo && b.PhysicalKeycode == Key.B)
        {
            OpenBuildMenu();
        }
        else if (@event is InputEventKey i && i.Pressed && !i.Echo && i.PhysicalKeycode == Key.I)
        {
            ToggleInventory();
        }
    }

    /// <summary>Opens the build menu, leaving build mode.</summary>
    public void OpenBuildMenu()
    {
        _buildController.Cancel();
        _dialogs.Open(_buildMenu);
    }

    /// <summary>Opens the inventory panel, or closes it when it is open.</summary>
    public void ToggleInventory()
    {
        if (_dialogs.Current == _inventory)
        {
            _dialogs.Close(_inventory);
        }
        else
        {
            _dialogs.Open(_inventory);
        }
    }

    /// <summary>Shows the placement tooltip beside the pointer while a building is selected.</summary>
    private void UpdatePlacementTip()
    {
        if (_buildController.Selected is null || _dialogs.CapturesWorldInput)
        {
            _dialogs.PlacementTip.HideTip();
            return;
        }
        // Below and to the right of the pointer, clear of the ghost under it.
        Vector2 point = _dialogs.ToUi(_buildController.PointerScreen()) + new Vector2(28f, 28f);
        _dialogs.PlacementTip.ShowAt(point, _buildController.PlacementLines(), _dialogs.UiSize);
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
        _sim.CellsRevealed += _terrainView.Reveal;

        _light = new DirectionalLight3D
        {
            ShadowEnabled = true
        };
        _light.RotationDegrees = new Vector3(-50, -40, 0);
        AddChild(_light);
        // The look is one sun and no ambient light: a face turned from the sun reads near black
        // (art-style.md). Without an environment the Mobile renderer adds ambient fill, so the
        // world has one with ambient light off.
        AddChild(new WorldEnvironment
        {
            Name = "Environment",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                TonemapMode = Godot.Environment.ToneMapper.Linear
            }
        });

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

        _camera = new CameraRig
        {
            Bounds = CameraBounds
        };
        _camera.SetTarget(_sim.Player.Position);
        AddChild(_camera);
        _terrainView.Initialize(_sim, _camera);

        _buildController = new BuildController
        {
            Name = "BuildController"
        };
        _buildController.Initialize(_sim, _camera);
        AddChild(_buildController);

        _debug = new DebugOverlay
        {
            Name = "DebugOverlay"
        };
        AddChild(_debug);

        var pictures = new BuildingPictures
        {
            Name = "BuildingPictures"
        };
        AddChild(pictures);
        _icons = new ResourceIcons
        {
            Name = "ResourceIcons"
        };
        AddChild(_icons);
        _fieldPictures = new FieldPictures
        {
            Name = "FieldPictures"
        };
        AddChild(_fieldPictures);
        _dialogs = new DialogHost
        {
            Name = "Dialogs"
        };
        AddChild(_dialogs);

        _topBar = new TopBar
        {
            Name = "TopBar"
        };
        _topBar.Initialize(_dialogs, _icons);
        _dialogs.AddOverlay(_topBar);
        _hint = new HintOverlay
        {
            Name = "HintOverlay"
        };
        _hint.Initialize(_icons);
        _dialogs.AddOverlay(_hint);

        _buildMenu = new BuildMenu
        {
            Name = "BuildMenu"
        };
        _buildMenu.Initialize(_sim, _buildController, pictures);
        _dialogs.Add(_buildMenu, "Buildings");

        _buildingDetail = new BuildingDetail
        {
            Name = "BuildingDetail"
        };
        _buildingDetail.Initialize(_sim);
        _dialogs.Add(_buildingDetail, "");
        _buildController.BuildingClicked += _buildingDetail.ShowBuilding;

        _inventory = new InventoryPanel
        {
            Name = "Inventory"
        };
        _inventory.Initialize(_sim, _icons);
        _dialogs.Add(_inventory, "Inventory");

        _fieldDetail = new FieldDetail
        {
            Name = "FieldDetail"
        };
        _fieldDetail.Initialize(_sim, _fieldPictures);
        _dialogs.Add(_fieldDetail, "");
        _buildController.FieldClicked += _fieldDetail.ShowField;
        // A click on the world that selects nothing closes a panel.
        _buildController.EmptyClicked += () =>
        {
            if (_dialogs.Current is { Kind: DialogKind.Panel } panel)
            {
                _dialogs.Close(panel);
            }
        };
    }
}
