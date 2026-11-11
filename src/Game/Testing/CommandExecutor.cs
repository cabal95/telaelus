using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Game.Presentation;
using Game.Sim;
using Godot;

namespace Game.Testing;

/// <summary>
/// Runs scenario commands against the live game. Most commands finish instantly
/// (in the current frame); <c>wait</c> and <c>screenshot</c> make the executor
/// <see cref="Busy"/> until enough frames pass. The owning node calls
/// <see cref="BeginFrame"/> once per frame before running more commands. Shared by
/// the scenario runner and (later) the live control channel.
/// </summary>
public sealed class CommandExecutor
{
    private readonly GameRoot _root;
    private readonly string _outputDir;
    private readonly bool _screenshots;
    private readonly List<InputEvent> _pendingReleases = new();
    private int _waitFrames;
    private bool _awaitingScreenshot;
    private string? _screenshotPath;
    private int _screenshotCount;

    public CommandExecutor(GameRoot root, string outputDir, bool screenshots)
    {
        _root = root;
        _outputDir = outputDir;
        _screenshots = screenshots;
    }

    /// <summary>Frames since the executor started (frame 0 is the first frame it ran in).</summary>
    public int Frame { get; private set; } = -1;

    /// <summary>True while waiting for frames to pass or a screenshot to be captured.</summary>
    public bool Busy => _waitFrames > 0 || _awaitingScreenshot;

    /// <summary>Advances the frame: delivers queued releases and counts down any wait.</summary>
    public void BeginFrame()
    {
        Frame++;
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

    // ---- Map and other setup --------------------------------------------

    public StepOutcome InitMap()
    {
        _root.Sim.Debug.ResetToEmptyMap();
        return StepOutcome.Ok;
    }

    public StepOutcome ChangeTiles(TerrainType type, IReadOnlyList<(int X, int Z)> cells)
    {
        foreach ((int x, int z) in cells)
        {
            _root.Sim.Debug.SetTerrain(x, z, type);
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

    /// <summary>Key down now; key up at the start of the next frame.</summary>
    public StepOutcome Press(Key key)
    {
        Inject(KeyEvent(key, true));
        _pendingReleases.Add(KeyEvent(key, false));
        return StepOutcome.Ok;
    }

    public StepOutcome KeyDown(Key key)
    {
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
        _pendingReleases.Add(MouseEvent(screen, false));
        return StepOutcome.Ok;
    }

    // ---- Time and capture ----------------------------------------------

    public StepOutcome Wait(int frames)
    {
        _waitFrames = frames;
        return StepOutcome.Ok;
    }

    public StepOutcome Snapshot(string label)
    {
        return new StepOutcome(true, null, new JsonObject
        {
            ["label"] = label,
            ["snapshot"] = StateSnapshot.Build(_root, Frame)
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
            ["snapshot"] = StateSnapshot.Build(_root, Frame)
        });
    }

    public StepOutcome Expect(string path, string op, JsonNode? expected, double tolerance)
    {
        JsonObject snapshot = StateSnapshot.Build(_root, Frame);
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

    private static void Inject(InputEvent inputEvent)
    {
        // Godot buffers injected events until its next input flush; flushing now
        // delivers them in this frame.
        Input.ParseInputEvent(inputEvent);
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
