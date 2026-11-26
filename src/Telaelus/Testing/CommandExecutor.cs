using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Telaelus.Presentation;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Testing;

/// <summary>
/// Runs scenario commands against the live game. Most commands finish instantly
/// (in the current frame); <c>wait</c>, <c>step</c>, and <c>screenshot</c> make the
/// executor <see cref="Busy"/> until enough frames pass. The owning node (which runs
/// with ProcessMode.Always, after GameRoot in the tree) calls <see cref="BeginFrame"/>
/// once per frame before running more commands. Shared by the scenario runner and
/// the live control channel.
///
/// While the scene tree is paused, input is queued rather than injected, and the
/// next <c>step</c> unpauses, injects the queue, flushes it, and runs its frames.
/// Godot drops input that is flushed while the tree is paused, so queued input is
/// only ever delivered in a frame the game will process.
/// </summary>
public sealed class CommandExecutor
{
    private readonly AppRoot _app;

    // The game the commands act on: the one in play.
    private GameRoot _root => _app.Game ?? throw new System.InvalidOperationException("no game yet: start one with newGame");
    private readonly string _outputDir;
    private readonly bool _screenshots;

    // Running: release halves of press/click, delivered at the start of the next frame.
    private readonly List<InputEvent> _pendingReleases = new();

    // Paused: input for the next step, and release halves for the step after it.
    private List<InputEvent> _queuedInput = new();
    private List<InputEvent> _deferredInput = new();

    // Keys this executor has pressed, for the snapshot's held-key list.
    private readonly HashSet<Key> _touchedKeys = new();
    private readonly List<InputEvent> _pendingUiReleases = new();
    // UI clicks and hovers waiting a frame for their control to scroll into view.
    private readonly List<(Control Control, bool Click)> _scrolledUi = new();
    private readonly Queue<List<InputEvent>> _dragFrames = new(); // a drag's events, one batch per frame

    private int _waitFrames;
    private int _stepFrames;
    private bool _awaitingScreenshot;
    private string? _screenshotPath;
    private int _screenshotCount;

    public CommandExecutor(AppRoot app, string outputDir, bool screenshots)
    {
        _app = app;
        _outputDir = outputDir;
        _screenshots = screenshots;
        // A run's saves live in its own folder, so a run never sees (or changes) the player's saves.
        SaveStore.Folder = System.IO.Path.Combine(outputDir, "saves");
    }

    /// <summary>
    /// Game frames processed since the executor started (frame 0 is the first). Frames
    /// spent paused do not count.
    /// </summary>
    public int Frame { get; private set; } = -1;

    /// <summary>The folder screenshots are written to.</summary>
    public string OutputDir => _outputDir;

    /// <summary>True while waiting for frames to pass, a step to finish, or a screenshot.</summary>
    public bool Busy => _waitFrames > 0 || _stepFrames > 0 || _awaitingScreenshot;

    public bool Paused => _app.GetTree().Paused;

    /// <summary>Keys the executor pressed that Godot currently reports as held.</summary>
    public IEnumerable<Key> HeldKeys
    {
        get
        {
            foreach (Key key in _touchedKeys)
            {
                if (Input.IsPhysicalKeyPressed(key))
                {
                    yield return key;
                }
            }
        }
    }

    /// <summary>
    /// Advances the frame: counts the frame if the game processed it, finishes a step
    /// (re-pausing), or delivers running-mode releases and counts down a wait.
    /// </summary>
    public void BeginFrame()
    {
        if (!Paused)
        {
            Frame++;
        }

        if (_stepFrames > 0)
        {
            _stepFrames--;
            if (_stepFrames == 0)
            {
                _app.GetTree().Paused = true;
            }
            return;
        }

        if (Paused)
        {
            return;
        }

        if (_scrolledUi.Count > 0)
        {
            foreach ((Control control, bool click) in _scrolledUi)
            {
                PointAt(control, click);
            }
            _scrolledUi.Clear();
        }
        if (_pendingUiReleases.Count > 0)
        {
            foreach (InputEvent release in _pendingUiReleases)
            {
                PushUi(release);
            }
            _pendingUiReleases.Clear();
        }
        if (_pendingReleases.Count > 0)
        {
            foreach (InputEvent release in _pendingReleases)
            {
                Input.ParseInputEvent(release);
            }
            Input.FlushBufferedEvents();
            DisposeAll(_pendingReleases);
            _pendingReleases.Clear();
        }
        if (_dragFrames.Count > 0)
        {
            foreach (InputEvent dragEvent in _dragFrames.Dequeue())
            {
                PushUi(dragEvent);
            }
        }
        if (_waitFrames > 0)
        {
            _waitFrames--;
        }
    }

    // ---- Pause and stepping ---------------------------------------------

    /// <summary>Pauses the game; pending releases wait for the next step.</summary>
    public StepOutcome Pause()
    {
        if (!Paused)
        {
            _app.GetTree().Paused = true;
            _queuedInput.AddRange(_pendingReleases);
            _pendingReleases.Clear();
        }
        return StepOutcome.Ok;
    }

    /// <summary>Resumes normal running, delivering queued input now and deferred releases next frame.</summary>
    public StepOutcome Resume()
    {
        if (Paused)
        {
            _app.GetTree().Paused = false;
            DeliverNow(_queuedInput);
            _pendingReleases.AddRange(_deferredInput);
            _queuedInput = new List<InputEvent>();
            _deferredInput = new List<InputEvent>();
        }
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Runs exactly <paramref name="frames"/> game frames, then stays paused. Pauses
    /// first if running. Queued input is delivered in the first stepped frame.
    /// </summary>
    public StepOutcome Step(int frames)
    {
        Pause();
        if (frames <= 0)
        {
            return StepOutcome.Ok;
        }

        // Unpause first: input flushed while paused is dropped.
        _app.GetTree().Paused = false;
        DeliverNow(_queuedInput);
        _queuedInput = _deferredInput;
        _deferredInput = new List<InputEvent>();
        _stepFrames = frames;
        return StepOutcome.Ok;
    }

    // ---- Map and other setup --------------------------------------------

    /// <summary>
    /// Resets to the bounded empty map (fully explored, or with fog only around the player), or
    /// to a generated world when a seed is given (always with fog).
    /// </summary>
    public StepOutcome InitMap(int? seed, bool fog)
    {
        if (seed is int s)
        {
            _root.Sim.Debug.ResetToGeneratedWorld(s);
        }
        else
        {
            _root.Sim.Debug.ResetToEmptyMap(fog);
        }
        _root.Builder.ResetBuildMode();
        _root.Camera.JumpTo(_root.Sim.Player.Position);
        // A fresh world starts with its own pins, as a new game does.
        _root.PinDiscovered(fresh: true);
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Sets the given layers of each map cell: biome, then terrain, then feature, so
    /// one step can paint a cell's ground and put a mountain or river on it.
    /// </summary>
    public StepOutcome ChangeTiles(IReadOnlyList<(int X, int Z)> cells, TerrainType? type, BiomeType? biome, MapFeature? feature, Facing? flow)
    {
        foreach ((int x, int z) in cells)
        {
            if (biome is BiomeType b)
            {
                _root.Sim.Debug.SetBiome(x, z, b);
            }
            if (type is TerrainType t)
            {
                _root.Sim.Debug.SetTerrain(x, z, t);
            }
            if (feature is MapFeature f)
            {
                _root.Sim.Debug.SetFeature(x, z, f, flow);
            }
        }
        return StepOutcome.Ok;
    }

    public StepOutcome PlaceResource(ResourceType type, int mapX, int mapZ, float? amount)
    {
        if (amount is float value)
        {
            _root.Sim.Debug.PlaceField(type, mapX, mapZ, value);
        }
        else
        {
            _root.Sim.Debug.PlaceField(type, mapX, mapZ);
        }
        return StepOutcome.Ok;
    }

    public StepOutcome SetResourceAmount(int mapX, int mapZ, float amount)
    {
        _root.Sim.Debug.SetFieldAmount(mapX, mapZ, amount);
        return StepOutcome.Ok;
    }

    public StepOutcome RemoveResource(int mapX, int mapZ)
    {
        _root.Sim.Debug.RemoveField(mapX, mapZ);
        return StepOutcome.Ok;
    }

    public StepOutcome PlaceBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        _root.Sim.Debug.PlaceBuilding(type, anchorX, anchorZ, facing);
        return StepOutcome.Ok;
    }

    public StepOutcome RemoveBuilding(int anchorX, int anchorZ)
    {
        _root.Sim.Debug.RemoveBuilding(anchorX, anchorZ);
        return StepOutcome.Ok;
    }

    public StepOutcome TeleportPlayer(float x, float z)
    {
        _root.Sim.Debug.TeleportPlayer(x, z);
        _root.Camera.JumpTo(_root.Sim.Player.Position);
        return StepOutcome.Ok;
    }

    public StepOutcome StartResearch(ResearchProject project)
    {
        if (!_root.Sim.StartResearch(project))
        {
            throw new System.InvalidOperationException($"cannot start {project}: it is {_root.Sim.StateOf(project)} or its fee is short");
        }
        return StepOutcome.Ok;
    }

    public StepOutcome PauseResearch()
    {
        _root.Sim.PauseResearch();
        return StepOutcome.Ok;
    }

    public StepOutcome SetResearchProgress(ResearchProject project, float seconds)
    {
        _root.Sim.Debug.SetResearchProgress(project, seconds);
        return StepOutcome.Ok;
    }

    /// <summary>placeRoad and placeBridge lay free (as placeBuilding does); removeRoad and removeBridge remove with the refund.</summary>
    public StepOutcome Line(string command, int fromX, int fromZ, int toX, int toZ)
    {
        Simulation sim = _root.Sim;
        switch (command)
        {
            case "placeRoad":
                sim.Debug.PlaceRoad(fromX, fromZ, toX, toZ);
                break;
            case "placeBridge":
                sim.Debug.PlaceBridge(fromX, fromZ, toX, toZ);
                break;
            case "removeRoad":
                sim.TryRemoveRoad(fromX, fromZ, toX, toZ);
                break;
            default:
                sim.TryRemoveBridge(fromX, fromZ, toX, toZ);
                break;
        }
        return StepOutcome.Ok;
    }

    /// <summary>
    /// A drag with the pointer over the ground: aims at <paramref name="from"/> and presses the button
    /// now, aims at <paramref name="to"/>, and releases at the start of the next frame (the line tool
    /// reads the pointer at the press and at the release).
    /// </summary>
    public StepOutcome AimDrag(Vector3 from, Vector3 to, MouseButton button, bool shift)
    {
        _root.Builder.PointerOverride = from;
        Vector2 start = _root.Camera.UnprojectPosition(from);
        Vector2 end = _root.Camera.UnprojectPosition(to);
        Rect2 window = _root.GetViewport().GetVisibleRect();
        if (!window.HasPoint(start) || !window.HasPoint(end))
        {
            return new StepOutcome(false, $"drag from ({start.X:0}, {start.Y:0}) to ({end.X:0}, {end.Y:0}) leaves the window");
        }
        Inject(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = true,
            ShiftPressed = shift,
            Position = start,
            GlobalPosition = start
        });
        _root.Builder.PointerOverride = to;
        Release(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = false,
            ShiftPressed = shift,
            Position = end,
            GlobalPosition = end
        });
        return StepOutcome.Ok;
    }

    /// <summary>Goes into a new game whatever the current state (from the menu, or replacing the game in play); the next command runs a frame later.</summary>
    public StepOutcome NewGame(int? seed)
    {
        ClearInput();
        _app.NewGame(seed ?? AppRoot.ScenarioSeed);
        _waitFrames = 1;
        return StepOutcome.Ok;
    }

    /// <summary>Saves the game in play under a name, as the Save Game dialog does.</summary>
    public StepOutcome SaveGame(string name)
    {
        _app.SaveGame(name);
        return StepOutcome.Ok;
    }

    /// <summary>Loads a save whatever the current state; the next command runs once the loaded game is in play.</summary>
    public StepOutcome LoadGame(string name)
    {
        ClearInput();
        if (_app.LoadGame(name) is string problem)
        {
            return new StepOutcome(false, $"loadGame: {problem}");
        }
        _waitFrames = AppRoot.LoadFrames + 2;
        return StepOutcome.Ok;
    }

    /// <summary>Loads the demo save, as the main menu's Demo does; the next command runs once it is in play.</summary>
    public StepOutcome LoadDemo()
    {
        ClearInput();
        if (_app.LoadDemo() is string problem)
        {
            return new StepOutcome(false, $"loadDemo: {problem}");
        }
        _waitFrames = AppRoot.LoadFrames + 2;
        return StepOutcome.Ok;
    }

    private void ClearInput()
    {
        _pendingReleases.Clear();
        _queuedInput.Clear();
        _deferredInput.Clear();
        _pendingUiReleases.Clear();
        _scrolledUi.Clear();
        _dragFrames.Clear();
    }

    public StepOutcome SetTimeOfDay(float timeOfDay)
    {
        _root.Sim.Debug.SetTimeOfDay(timeOfDay);
        return StepOutcome.Ok;
    }

    public StepOutcome SetRecipe(int anchorX, int anchorZ, int index)
    {
        _root.Sim.Debug.SetRecipe(anchorX, anchorZ, index);
        return StepOutcome.Ok;
    }

    public StepOutcome QueueCraft(int anchorX, int anchorZ, int recipe, int count)
    {
        _root.Sim.Debug.QueueCraft(anchorX, anchorZ, recipe, count);
        return StepOutcome.Ok;
    }

    public StepOutcome PlaceMotherlode(MotherlodeType kind, int mapX, int mapZ)
    {
        _root.Sim.Debug.PlaceMotherlode(kind, mapX, mapZ);
        return StepOutcome.Ok;
    }

    public StepOutcome SetWorkedOut(int mapX, int mapZ, ResourceType deposit)
    {
        _root.Sim.Debug.SetWorkedOut(mapX, mapZ, deposit);
        return StepOutcome.Ok;
    }

    public StepOutcome FillBuffer(int anchorX, int anchorZ, ResourceType type, float amount)
    {
        _root.Sim.Debug.FillBuffer(anchorX, anchorZ, type, amount);
        return StepOutcome.Ok;
    }

    public StepOutcome SeedStockpile(ResourceType type, float amount)
    {
        _root.Sim.Debug.SeedStockpile(type, amount);
        return StepOutcome.Ok;
    }

    /// <summary>Starts the trainer at a step (0-based here; 1-based in the scenario).</summary>
    public StepOutcome StartTrainer(int step)
    {
        _root.Trainer.Start(step);
        return StepOutcome.Ok;
    }

    /// <summary>Opens the first New Game's "Learn to play?" question (test runs never ask on their own).</summary>
    public StepOutcome AskTrainer()
    {
        _root.AskStartTrainer();
        return StepOutcome.Ok;
    }

    public StepOutcome CameraZoom(float size)
    {
        _root.Camera.ZoomTo(size);
        return StepOutcome.Ok;
    }

    // ---- Input ----------------------------------------------------------

    /// <summary>
    /// Key down now and up at the start of the next frame (running), or down in the
    /// next step and up in the step after it (paused): held for exactly one frame.
    /// </summary>
    public StepOutcome Press(Key key)
    {
        _touchedKeys.Add(key);
        Inject(KeyEvent(key, true));
        Release(KeyEvent(key, false));
        return StepOutcome.Ok;
    }

    public StepOutcome KeyDown(Key key)
    {
        _touchedKeys.Add(key);
        Inject(KeyEvent(key, true));
        return StepOutcome.Ok;
    }

    public StepOutcome KeyUp(Key key)
    {
        Inject(KeyEvent(key, false));
        return StepOutcome.Ok;
    }

    /// <summary>Points the build pointer at a world position on the ground.</summary>
    public StepOutcome AimAt(Vector3 world)
    {
        _root.Builder.PointerOverride = world;
        return StepOutcome.Ok;
    }

    /// <summary>Left button down now at the current aim; up at the start of the next frame.</summary>
    public StepOutcome Click(MouseButton button = MouseButton.Left, bool ctrl = false)
    {
        Vector2 screen = _root.Builder.PointerOverride is Vector3 world
            ? _root.Camera.UnprojectPosition(world)
            : _root.GetViewport().GetMousePosition();
        // A player cannot click outside the window; a scenario that does depends on luck.
        if (!_root.GetViewport().GetVisibleRect().HasPoint(screen))
        {
            return new StepOutcome(false, $"click at ({screen.X:0}, {screen.Y:0}) is outside the window");
        }
        Inject(MouseEvent(screen, true, button, ctrl));
        Release(MouseEvent(screen, false, button, ctrl));
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Drags with a mouse button (and Ctrl if asked) from one screen point to another: the
    /// pointer moves there and presses now, moves in even steps over the next frames, and
    /// releases on the frame after; the scenario waits until the release. Pushed through the
    /// viewport like real input, so the UI gets it first.
    /// </summary>
    public StepOutcome Drag(MouseButton button, bool ctrl, Vector2 from, Vector2 to, int frames)
    {
        if (Paused)
        {
            return new StepOutcome(false, "drag needs the game running");
        }
        frames = System.Math.Max(1, frames);
        MouseButtonMask mask = button == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left;
        PushUi(DragMotion(from, from, 0, ctrl));
        PushUi(DragButton(button, true, from, ctrl));
        Vector2 last = from;
        for (int i = 1; i <= frames; i++)
        {
            Vector2 at = from.Lerp(to, i / (float)frames);
            _dragFrames.Enqueue(new List<InputEvent>
            {
                DragMotion(at, last, mask, ctrl)
            });
            last = at;
        }
        _dragFrames.Enqueue(new List<InputEvent>
        {
            DragButton(button, false, to, ctrl)
        });
        _waitFrames = frames + 1;
        return StepOutcome.Ok;
    }

    private static InputEventMouseMotion DragMotion(Vector2 at, Vector2 last, MouseButtonMask mask, bool ctrl)
    {
        return new InputEventMouseMotion
        {
            Position = at,
            GlobalPosition = at,
            Relative = at - last,
            ButtonMask = mask,
            CtrlPressed = ctrl
        };
    }

    private static InputEventMouseButton DragButton(MouseButton button, bool pressed, Vector2 at, bool ctrl)
    {
        return new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = at,
            GlobalPosition = at,
            CtrlPressed = ctrl
        };
    }

    /// <summary>
    /// Moves the pointer over a registered UI control and presses the left button there;
    /// released at the start of the next frame. Fails when the control is not on screen.
    /// </summary>
    public StepOutcome ClickUi(string id)
    {
        if (_root.Dialogs.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        if (ScrollIntoView(control))
        {
            _scrolledUi.Add((control, true));
            return StepOutcome.Ok;
        }
        PointAt(control, click: true);
        return StepOutcome.Ok;
    }

    /// <summary>Moves the pointer over a control and, for a click, presses there (released next frame).</summary>
    private void PointAt(Control control, bool click)
    {
        Vector2 screen = _root.Dialogs.ScreenCenter(control);
        PushUi(MotionEvent(screen));
        if (click)
        {
            PushUi(MouseEvent(screen, true));
            _pendingUiReleases.Add(MouseEvent(screen, false));
        }
    }

    /// <summary>
    /// Scrolls a control into view when a scroll container clips it (as a player scrolls to it).
    /// True when it scrolled: the layout settles by the next frame, when the click or hover lands.
    /// </summary>
    private static bool ScrollIntoView(Control control)
    {
        for (Node? node = control.GetParent(); node is not null; node = node.GetParent())
        {
            if (node is ScrollContainer scroll)
            {
                if (scroll.GetGlobalRect().Encloses(control.GetGlobalRect()))
                {
                    return false;
                }
                scroll.EnsureControlVisible(control);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Moves the pointer over a registered UI control and holds the left button down there for a
    /// number of frames, releasing on the last; the scenario waits until the release. Fails when
    /// the control is not on screen or a scroll container clips it (hover it first to scroll).
    /// </summary>
    public StepOutcome HoldUi(string id, int frames)
    {
        if (_root.Dialogs.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        if (ScrollIntoView(control))
        {
            return new StepOutcome(false, $"'{id}' was scrolled out of view; hoverUi it first");
        }
        frames = System.Math.Max(1, frames);
        Vector2 screen = _root.Dialogs.ScreenCenter(control);
        PushUi(MotionEvent(screen));
        PushUi(MouseEvent(screen, true));
        for (int i = 1; i < frames; i++)
        {
            _dragFrames.Enqueue(new List<InputEvent>());
        }
        _dragFrames.Enqueue(new List<InputEvent>
        {
            MouseEvent(screen, false)
        });
        _waitFrames = frames + 1;
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Picks an item of a registered picker (an OptionButton) by its text, as choosing it from the
    /// picker's list does. Fails when the control is not a visible picker or has no such item.
    /// </summary>
    public StepOutcome SelectUi(string id, string item)
    {
        if (_root.Dialogs.Find(id) is not OptionButton picker)
        {
            return new StepOutcome(false, $"no visible picker '{id}'");
        }
        for (int i = 0; i < picker.ItemCount; i++)
        {
            if (picker.GetItemText(i) == item)
            {
                picker.Select(i);
                picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)i);
                return StepOutcome.Ok;
            }
        }
        return new StepOutcome(false, $"picker '{id}' has no item '{item}'");
    }

    /// <summary>Moves the pointer over a registered UI control (its hover state and tooltip follow).</summary>
    public StepOutcome HoverUi(string id)
    {
        if (_root.Dialogs.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        if (ScrollIntoView(control))
        {
            _scrolledUi.Add((control, false));
            return StepOutcome.Ok;
        }
        PointAt(control, click: false);
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Turns the mouse wheel over a registered UI control: positive ticks scroll down,
    /// negative up, one press and release each.
    /// </summary>
    public StepOutcome ScrollUi(string id, int ticks)
    {
        if (_root.Dialogs.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        Vector2 screen = _root.Dialogs.ScreenCenter(control);
        PushUi(MotionEvent(screen));
        MouseButton button = ticks >= 0 ? MouseButton.WheelDown : MouseButton.WheelUp;
        for (int i = 0; i < System.Math.Abs(ticks); i++)
        {
            foreach (bool pressed in new[] { true, false })
            {
                PushUi(new InputEventMouseButton
                {
                    ButtonIndex = button,
                    Pressed = pressed,
                    Factor = 1f,
                    Position = screen,
                    GlobalPosition = screen
                });
            }
        }
        return StepOutcome.Ok;
    }

    // ---- Time and capture ----------------------------------------------

    /// <summary>Lets frames pass; while paused, that means stepping them.</summary>
    public StepOutcome Wait(int frames)
    {
        if (Paused)
        {
            return Step(frames);
        }
        _waitFrames = frames;
        return StepOutcome.Ok;
    }

    public StepOutcome Snapshot(string label)
    {
        return new StepOutcome(true, null, new JsonObject
        {
            ["label"] = label,
            ["snapshot"] = StateSnapshot.Build(_app, this)
        });
    }

    /// <summary>
    /// Records a snapshot now and saves the viewport after this frame is drawn. With
    /// screenshots off (headless or --no-screenshots), it is a snapshot only.
    /// </summary>
    public StepOutcome Screenshot(string label)
    {
        if (!_screenshots)
        {
            var detail = Snapshot(label).Detail!.AsObject();
            detail["screenshot"] = "skipped";
            return new StepOutcome(true, null, detail);
        }

        Directory.CreateDirectory(_outputDir);
        string file = $"{++_screenshotCount:00}-{Sanitize(label)}.png";
        _screenshotPath = Path.Combine(_outputDir, file);
        _awaitingScreenshot = true;
        RenderingServer.FramePostDraw += CaptureScreenshot;
        return new StepOutcome(true, null, new JsonObject
        {
            ["label"] = label,
            ["file"] = file,
            ["snapshot"] = StateSnapshot.Build(_app, this)
        });
    }

    public StepOutcome Expect(string path, string op, JsonNode? expected, double tolerance)
    {
        JsonObject snapshot = StateSnapshot.Build(_app, this);
        bool exists = SnapshotPath.TryResolve(snapshot, path, out JsonNode? actual);
        bool passed = op switch
        {
            "exists" => exists == expected!.GetValue<bool>(),
            "equals" => exists && SnapshotPath.JsonEquals(actual, expected),
            "notEquals" => !exists || !SnapshotPath.JsonEquals(actual, expected),
            "gte" => Compare(actual, expected, (a, e) => a >= e),
            "lte" => Compare(actual, expected, (a, e) => a <= e),
            "approx" => Compare(actual, expected, (a, e) => System.Math.Abs(a - e) <= tolerance),
            _ => false
        };

        string actualText = exists ? actual?.ToJsonString() ?? "null" : "(missing)";
        string expectedText = expected?.ToJsonString() ?? "null";
        var detail = new JsonObject
        {
            ["path"] = path,
            ["operator"] = op,
            ["expected"] = expected?.DeepClone(),
            ["actual"] = exists ? actual?.DeepClone() : "(missing)",
            ["passed"] = passed
        };
        string? message = passed ? null : $"expected {path} {op} {expectedText}, actual {actualText}";
        return new StepOutcome(passed, message, detail);
    }

    // ---- Helpers ---------------------------------------------------------

    private void CaptureScreenshot()
    {
        RenderingServer.FramePostDraw -= CaptureScreenshot;
        Image image = _app.GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_screenshotPath!);
        if (error != Error.Ok)
        {
            GD.PushError($"Screenshot save failed ({error}): {_screenshotPath}");
        }
        _awaitingScreenshot = false;
    }

    private static bool Compare(JsonNode? actual, JsonNode? expected, System.Func<double, double, bool> test)
    {
        return SnapshotPath.TryNumber(actual, out double a)
            && SnapshotPath.TryNumber(expected, out double e)
            && test(a, e);
    }

    // Running: deliver now. Paused: queue for the next step.
    private void Inject(InputEvent inputEvent)
    {
        if (Paused)
        {
            _queuedInput.Add(inputEvent);
            return;
        }
        DeliverNow(new List<InputEvent>
        {
            inputEvent
        });
    }

    // Running: release at the start of the next frame. Paused: release in the step after next.
    private void Release(InputEvent inputEvent)
    {
        if (Paused)
        {
            _deferredInput.Add(inputEvent);
        }
        else
        {
            _pendingReleases.Add(inputEvent);
        }
    }

    private static void DeliverNow(List<InputEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        // Godot buffers injected events until its next input flush; flushing now
        // delivers them in this frame.
        foreach (InputEvent inputEvent in events)
        {
            Input.ParseInputEvent(inputEvent);
        }
        Input.FlushBufferedEvents();
        DisposeAll(events);
    }

    /// <summary>
    /// Lets go of delivered events' C# wrappers at once. Godot holds its own reference while it still
    /// needs an event; a wrapper left for the garbage collector can instead be finalized while the
    /// engine shuts down, racing its own cleanup of the same object and crashing the process.
    /// </summary>
    private static void DisposeAll(List<InputEvent> events)
    {
        foreach (InputEvent inputEvent in events)
        {
            inputEvent.Dispose();
        }
    }

    private static InputEventKey KeyEvent(Key key, bool pressed)
    {
        return new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed
        };
    }

    /// <summary>
    /// Delivers a pointer event straight to the viewport, so the GUI picks and hovers by
    /// it in headless runs too (where events parsed through Input never reach the GUI).
    /// </summary>
    private void PushUi(InputEvent inputEvent)
    {
        _root.GetViewport().PushInput(inputEvent);
        // Delivered at once; see DisposeAll.
        inputEvent.Dispose();
    }

    private static InputEventMouseMotion MotionEvent(Vector2 screen)
    {
        return new InputEventMouseMotion
        {
            Position = screen,
            GlobalPosition = screen
        };
    }

    private static InputEventMouseButton MouseEvent(Vector2 screen, bool pressed, MouseButton button = MouseButton.Left, bool ctrl = false)
    {
        return new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            CtrlPressed = ctrl,
            Position = screen,
            GlobalPosition = screen
        };
    }

    private static string Sanitize(string label)
    {
        var chars = label.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
            {
                chars[i] = '-';
            }
        }
        return new string(chars);
    }
}
