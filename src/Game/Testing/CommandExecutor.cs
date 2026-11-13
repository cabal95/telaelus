using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Game.Presentation;
using Game.Sim;
using Godot;

namespace Game.Testing;

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
    private readonly GameRoot _root;
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

    private int _waitFrames;
    private int _stepFrames;
    private bool _awaitingScreenshot;
    private string? _screenshotPath;
    private int _screenshotCount;

    public CommandExecutor(GameRoot root, string outputDir, bool screenshots)
    {
        _root = root;
        _outputDir = outputDir;
        _screenshots = screenshots;
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

    public bool Paused => _root.GetTree().Paused;

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
                _root.GetTree().Paused = true;
            }
            return;
        }

        if (Paused)
        {
            return;
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
            _pendingReleases.Clear();
            Input.FlushBufferedEvents();
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
            _root.GetTree().Paused = true;
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
            _root.GetTree().Paused = false;
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
        _root.GetTree().Paused = false;
        DeliverNow(_queuedInput);
        _queuedInput = _deferredInput;
        _deferredInput = new List<InputEvent>();
        _stepFrames = frames;
        return StepOutcome.Ok;
    }

    // ---- Map and other setup --------------------------------------------

    /// <summary>Resets to the bounded empty map, or to a generated world when a seed is given.</summary>
    public StepOutcome InitMap(int? seed)
    {
        if (seed is int s)
        {
            _root.Sim.Debug.ResetToGeneratedWorld(s);
        }
        else
        {
            _root.Sim.Debug.ResetToEmptyMap();
        }
        _root.Builder.ResetBuildMode();
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
        return StepOutcome.Ok;
    }

    public StepOutcome SeedStockpile(ResourceType type, float amount)
    {
        _root.Sim.Debug.SeedStockpile(type, amount);
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
    public StepOutcome Click()
    {
        Vector2 screen = _root.Builder.PointerOverride is Vector3 world
            ? _root.Camera.UnprojectPosition(world)
            : _root.GetViewport().GetMousePosition();
        Inject(MouseEvent(screen, true));
        Release(MouseEvent(screen, false));
        return StepOutcome.Ok;
    }

    /// <summary>
    /// Moves the pointer over a registered UI control and presses the left button there;
    /// released at the start of the next frame. Fails when the control is not on screen.
    /// </summary>
    public StepOutcome ClickUi(string id)
    {
        if (_root.Overlays.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        Vector2 screen = _root.Overlays.ScreenCenter(control);
        PushUi(MotionEvent(screen));
        PushUi(MouseEvent(screen, true));
        _pendingUiReleases.Add(MouseEvent(screen, false));
        return StepOutcome.Ok;
    }

    /// <summary>Moves the pointer over a registered UI control (its hover state and tooltip follow).</summary>
    public StepOutcome HoverUi(string id)
    {
        if (_root.Overlays.Find(id) is not Control control)
        {
            return new StepOutcome(false, $"no visible UI control '{id}'");
        }
        PushUi(MotionEvent(_root.Overlays.ScreenCenter(control)));
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
            ["snapshot"] = StateSnapshot.Build(_root, this)
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
            ["snapshot"] = StateSnapshot.Build(_root, this)
        });
    }

    public StepOutcome Expect(string path, string op, JsonNode? expected, double tolerance)
    {
        JsonObject snapshot = StateSnapshot.Build(_root, this);
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
        Image image = _root.GetViewport().GetTexture().GetImage();
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
    }

    private static InputEventMouseMotion MotionEvent(Vector2 screen)
    {
        return new InputEventMouseMotion
        {
            Position = screen,
            GlobalPosition = screen
        };
    }

    private static InputEventMouseButton MouseEvent(Vector2 screen, bool pressed)
    {
        return new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = pressed,
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
