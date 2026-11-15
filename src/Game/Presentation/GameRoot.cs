using System.Linq;
using System.Collections.Generic;
using Game.Presentation.Ui;
using Game.Sim;
using Game.Testing;
using Godot;

namespace Game.Presentation;

/// <summary>
/// One game, created by <see cref="AppRoot"/> for New Game or a loaded save. It owns the simulation
/// and the presentation, reads input into per-tick intent, steps the sim on a fixed clock (standing
/// still while the game menu or a save or load dialog is open), mirrors sim state onto visible
/// nodes, and autosaves every <see cref="AutosaveSeconds"/> of play.
/// </summary>
public partial class GameRoot : Node3D
{
    private Simulation _sim = null!;
    private readonly SimClock _clock = new(30.0); // 30 Hz simulation tick

    private PlayerView _playerView = null!;
    private CameraRig _camera = null!;
    private ResourceFieldViews _fieldViews = null!;
    private BuildingViews _buildingViews = null!;
    private TruckViews _truckViews = null!;
    private SiteViews _siteViews = null!;
    private RoadViews _roadViews = null!;
    private DisplayStandViews _standViews = null!;
    private BuildController _buildController = null!;
    private DebugOverlay _debug = null!;
    private TopBar _topBar = null!;
    private HintOverlay _hint = null!;
    private InventoryPanel _inventory = null!;
    private ResearchTree _researchTree = null!;
    private FieldDetail _fieldDetail = null!;
    private FieldPictures _fieldPictures = null!;
    private BuildingPictures _buildingPictures = null!;
    private readonly ResourcePins _pins = new();
    private NetworkView _network = null!;
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
    public NetworkView Network => _network;

    /// <summary>Whether the startup pictures (resource icons, field and building pictures) are drawn.</summary>
    public bool PicturesReady => _icons.IsReady && _fieldPictures.IsReady && _buildingPictures.IsReady;
    public DebugOverlay Debug => _debug;
    public TopBar TopBar => _topBar;
    public HintOverlay Hint => _hint;
    public InventoryPanel Inventory => _inventory;
    public ResearchTree ResearchTree => _researchTree;
    public FieldDetail FieldDetail => _fieldDetail;

    private int _seed;
    private SaveFile? _save;
    private GameMenu _gameMenu = null!;
    private SaveDialog _saveDialog = null!;
    private LoadDialog _loadDialog = null!;
    private Image? _menuThumbnail;

    /// <summary>The app the game belongs to (saving, loading, and the way back to the menu).</summary>
    public AppRoot App { get; private set; } = null!;

    /// <summary>Game seconds played (sim time), carried through saves.</summary>
    public double PlayTime { get; private set; }

    /// <summary>The play time when last saved or loaded.</summary>
    public double SavedPlayTime { get; private set; }

    /// <summary>Whether there is play since the last save (or the start): leaving asks first.</summary>
    public bool HasUnsavedProgress => PlayTime - SavedPlayTime > 1.0;

    public GameMenu GameMenu => _gameMenu;
    public SaveDialog SaveDialog => _saveDialog;
    public LoadDialog LoadDialog => _loadDialog;

    /// <summary>Seconds of play between autosaves.</summary>
    public const double AutosaveSeconds = 300.0;

    /// <summary>
    /// Sets up a game before it enters the tree: its world's seed (or the save it is loaded from), the
    /// app, and the pictures every game shares.
    /// </summary>
    public void Initialize(AppRoot app, int seed, SaveFile? save, ResourceIcons icons, BuildingPictures buildingPictures, FieldPictures fieldPictures)
    {
        App = app;
        _seed = seed;
        _save = save;
        _icons = icons;
        _buildingPictures = buildingPictures;
        _fieldPictures = fieldPictures;
    }

    public override void _Ready()
    {
        _sim = _save is SaveFile save ? Simulation.FromSave(save.Sim) : new Simulation(WorldSettings.Generated(_seed));
        BuildWorld();
        if (_save is SaveFile loaded)
        {
            ApplyPresentation(loaded.Presentation);
            PlayTime = loaded.Header.PlayTimeSeconds;
            SavedPlayTime = PlayTime;
        }
        _save = null;
    }

    /// <summary>The game as a save: its header, the sim's state, and the pins and zoom.</summary>
    public SaveFile MakeSave()
    {
        return new SaveFile
        {
            Header = new SaveHeader
            {
                GameVersion = AppRoot.GameVersion,
                SavedAt = System.DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture),
                PlayTimeSeconds = PlayTime,
                Seed = _sim.World.Seed,
                ShipsLaunched = _sim.ShipsLaunched,
                Buildings = _sim.Buildings.Count
            },
            Sim = _sim.Save(),
            Presentation = new System.Text.Json.Nodes.JsonObject
            {
                ["pins"] = new System.Text.Json.Nodes.JsonArray(_pins.Pinned.Select(t => (System.Text.Json.Nodes.JsonNode)t.ToString()).ToArray()),
                ["zoom"] = _camera.ZoomLevel
            }
        };
    }

    private void ApplyPresentation(System.Text.Json.Nodes.JsonObject? presentation)
    {
        if (presentation is null)
        {
            return;
        }
        if (presentation["pins"] is System.Text.Json.Nodes.JsonArray pins)
        {
            var types = new List<ResourceType>();
            foreach (System.Text.Json.Nodes.JsonNode? pin in pins)
            {
                if (pin is not null && System.Enum.TryParse(pin.GetValue<string>(), out ResourceType type))
                {
                    types.Add(type);
                }
            }
            _pins.SetAll(types);
        }
        if (presentation["zoom"] is System.Text.Json.Nodes.JsonNode zoom)
        {
            _camera.ZoomTo(zoom.GetValue<float>());
        }
    }

    /// <summary>Marks the game saved now (no unsaved progress).</summary>
    public void MarkSaved()
    {
        SavedPlayTime = PlayTime;
    }

    /// <summary>A 320 x 180 picture of the game as last drawn, for a save's thumbnail (null headless).</summary>
    public Image? CaptureThumbnail()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return null;
        }
        Image image = GetViewport().GetTexture().GetImage();
        image.Resize(320, 180, Image.Interpolation.Bilinear);
        return image;
    }

    /// <summary>The thumbnail for a save from the game menu: the game as it was when the menu opened.</summary>
    public Image? SaveThumbnail => _menuThumbnail ?? CaptureThumbnail();

    /// <summary>Opens the game menu (Escape with nothing else open), keeping the frame behind it as the thumbnail.</summary>
    public void OpenGameMenu(string status = "")
    {
        if (_dialogs.Current is null)
        {
            _menuThumbnail = CaptureThumbnail();
        }
        _dialogs.Open(_gameMenu);
        _gameMenu.SetStatus(status);
    }

    /// <summary>
    /// On the way out, finishes .NET's pending finalizers while the engine is still whole: a C# wrapper
    /// finalized during Godot's own C# cleanup races it over the same object and crashes the process.
    /// Twice, for objects that the first round's finalizers let go.
    /// </summary>
    public override void _ExitTree()
    {
        Game.Testing.ShutdownTrace.Mark("GameRoot: exit tree");
        for (int round = 0; round < 2; round++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }
        Game.Testing.ShutdownTrace.Mark("GameRoot: finalizers done");
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

        // The game menu and the save and load dialogs stand the game still.
        bool paused = _dialogs.Current?.PausesGame == true;
        int steps = paused ? 0 : _clock.Advance(delta);
        _simWatch.Restart();
        double before = PlayTime;
        for (int i = 0; i < steps; i++)
        {
            _sim.Step(_clock.FixedDelta);
            PlayTime += _clock.FixedDelta;
        }
        if (steps > 0 && System.Math.Floor(PlayTime / AutosaveSeconds) > System.Math.Floor(before / AutosaveSeconds))
        {
            App.Autosave();
        }
        _perf.SimMs = _simWatch.Elapsed.TotalMilliseconds;

        _playerView.UpdateFrom(_sim.Player.Position, moveDir, delta);
        _camera.SetTarget(_sim.Player.Position);
        // Shadows are drawn only up to this distance from the camera, which moves back as the
        // view grows (see CameraRig.ViewDistance), so the range follows it.
        _light.DirectionalShadowMaxDistance = _camera.ViewDistance + _camera.Size;
        DayLight.Apply(_light, _sim.TimeOfDay);
        _fieldViews.Sync(_sim, _terrainView);
        _buildingViews.Sync(ExploredBuildings());
        _truckViews.Sync(_sim, delta);
        _siteViews.Sync(_sim);
        _roadViews.Sync(_sim);
        _standViews.Sync(_sim, delta);
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
        if (@event is InputEventKey { Pressed: true, Echo: false } escape && escape.PhysicalKeycode == Key.Escape
            && _dialogs.Current is null && !_buildController.InBuildMode)
        {
            OpenGameMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
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
        else if (@event is InputEventKey t && t.Pressed && !t.Echo && t.PhysicalKeycode == Key.T)
        {
            OpenResearchTree();
        }
        else if (@event is InputEventKey n && n.Pressed && !n.Echo && n.PhysicalKeycode == Key.N)
        {
            _network.OverlayShown = !_network.OverlayShown;
        }
    }

    /// <summary>Opens the build menu, leaving build mode.</summary>
    public void OpenBuildMenu()
    {
        _buildController.Cancel();
        _dialogs.Open(_buildMenu);
    }

    /// <summary>Opens the research tree, leaving build mode.</summary>
    public void OpenResearchTree()
    {
        _buildController.Cancel();
        _dialogs.Open(_researchTree);
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
    /// <summary>The deposit of the worked-out cell under the pointer, when nothing stands or lies on it.</summary>
    private ResourceType? WorkedOutUnderPointer()
    {
        if (_buildController.PointedCell() is not (int x, int z) || _sim.FieldAt(x, z) is not null || _sim.BuildingAt(x, z) is not null)
        {
            return null;
        }
        return _sim.WorkedOut.TryGetValue((Simulation.MapCellOf(x), Simulation.MapCellOf(z)), out ResourceType deposit) ? deposit : null;
    }

    /// <summary>How near (in screen pixels) the pointer must be to a truck to hover it.</summary>
    private const float TruckHoverPixels = 28f;

    private void UpdatePlacementTip()
    {
        if (_dialogs.CapturesWorldInput)
        {
            _dialogs.PlacementTip.HideTip();
            return;
        }
        if (!_buildController.InBuildMode)
        {
            // Outside build mode the same tooltip names the truck under the pointer.
            Vector2 screen = _buildController.PointerScreen();
            if (_buildController.PointingAtWorld && _truckViews.TruckAt(_sim, _camera, screen, TruckHoverPixels) is Truck truck)
            {
                var lines = new List<TipLine>
                {
                    new("Truck", UiTheme.Accent),
                    new(TruckText.State(truck))
                };
                _dialogs.PlacementTip.ShowAt(_dialogs.ToUi(screen) + new Vector2(28f, 28f), lines, _dialogs.UiSize);
            }
            else if (WorkedOutUnderPointer() is ResourceType deposit)
            {
                var lines = new List<TipLine>
                {
                    new("Worked-out " + ResourceDefs.For(deposit).Name, UiTheme.Accent)
                };
                _dialogs.PlacementTip.ShowAt(_dialogs.ToUi(screen) + new Vector2(28f, 28f), lines, _dialogs.UiSize);
            }
            else
            {
                _dialogs.PlacementTip.HideTip();
            }
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

        _truckViews = new TruckViews
        {
            Name = "Trucks"
        };
        AddChild(_truckViews);

        _siteViews = new SiteViews
        {
            Name = "Sites"
        };
        AddChild(_siteViews);

        _roadViews = new RoadViews
        {
            Name = "Roads"
        };
        AddChild(_roadViews);

        _standViews = new DisplayStandViews
        {
            Name = "DisplayStands"
        };
        AddChild(_standViews);

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

        _dialogs = new DialogHost
        {
            Name = "Dialogs"
        };
        AddChild(_dialogs);

        _topBar = new TopBar
        {
            Name = "TopBar"
        };
        _topBar.Initialize(_dialogs, _icons, _pins);
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
        _buildMenu.Initialize(_sim, _buildController, _buildingPictures, _icons);
        _dialogs.Add(_buildMenu, "Buildings");

        _buildingDetail = new BuildingDetail
        {
            Name = "BuildingDetail"
        };
        _buildingDetail.Initialize(_sim);
        _dialogs.Add(_buildingDetail, "");
        _buildController.BuildingClicked += _buildingDetail.ShowBuilding;

        _network = new NetworkView
        {
            Name = "NetworkView"
        };
        _network.Initialize(_sim, _buildController, _buildingDetail);
        AddChild(_network);

        _inventory = new InventoryPanel
        {
            Name = "Inventory"
        };
        _inventory.Initialize(_sim, _icons, _pins);
        _dialogs.Add(_inventory, "Inventory");

        _researchTree = new ResearchTree
        {
            Name = "ResearchTree"
        };
        _researchTree.Initialize(_sim, _icons);
        _dialogs.Add(_researchTree, "Research");
        _buildingDetail.ResearchRequested += OpenResearchTree;

        _gameMenu = new GameMenu
        {
            Name = "GameMenu",
            HasUnsavedProgress = () => HasUnsavedProgress
        };
        _dialogs.Add(_gameMenu, "Paused");
        _saveDialog = new SaveDialog
        {
            Name = "SaveDialog"
        };
        _dialogs.Add(_saveDialog, "Save Game");
        _loadDialog = new LoadDialog
        {
            Name = "LoadDialog",
            HasUnsavedProgress = () => HasUnsavedProgress
        };
        _dialogs.Add(_loadDialog, "Load Game");
        _gameMenu.SaveRequested += () => _dialogs.Open(_saveDialog);
        _gameMenu.LoadRequested += () => _dialogs.Open(_loadDialog);
        _gameMenu.MainMenuRequested += () => App.ReturnToMenu();
        _gameMenu.QuitRequested += () => GetTree().Quit();
        _saveDialog.SaveRequested += name =>
        {
            App.SaveGame(name);
            OpenGameMenu($"Saved as {name}");
        };
        _loadDialog.LoadRequested += name =>
        {
            if (App.LoadGame(name) is string problem)
            {
                OpenGameMenu(problem);
            }
        };

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
