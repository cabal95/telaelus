using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Measures how much of the frame budget the game uses.
///
/// Godot's own process time includes the wait for the next display refresh, so it
/// reads about 16.7 ms under vsync whatever the load; it cannot show headroom. Instead
/// the game times its own frame work: a frame-start node processes first each frame
/// and this monitor processes last, so the time between them is every node's update
/// (the simulation steps included). Adding the renderer's CPU time for the viewport
/// gives the frame's CPU <b>work</b> time. The GPU time is Godot's measurement for the
/// viewport. On the Mobile and Forward+ renderers (Direct3D 12, Vulkan, Metal) it is taken
/// with GPU timestamps, so it never includes the wait for the display; under vsync it is
/// higher than uncapped because the GPU clocks down when it has time to spare, which is
/// still the real time it took. There the <b>headline</b> is the larger of the CPU and GPU
/// times: how much of the 16.7 ms budget the frame used, whichever side is busier. On the
/// Compatibility renderer the GPU time is coarse (about 2 ms steps) and can include waiting,
/// and the CPU waits on the GPU inside the render step anyway, so the headline is the CPU
/// time and the GPU time is for reference only. The wall time between frames is recorded
/// too: with vsync off (<c>--uncapped</c>) it is the true cost of a frame.
///
/// It keeps one second of samples for an average and the worst frame.
/// </summary>
public partial class PerfMonitor : Node
{
    /// <summary>The frame budget: one frame at 60 fps.</summary>
    public const double BudgetMs = 1000.0 / 60.0;

    private const ulong WindowMs = 1000;

    public readonly record struct Sample(
        double HeadlineMs,
        double WorkMs,
        double ScriptMs,
        double RenderCpuMs,
        double? GpuMs,
        double SimMs,
        double FrameMs,
        double Fps,
        int Draws,
        int Objects);

    private readonly Queue<(ulong AtMs, Sample Sample)> _window = new();
    private readonly Stopwatch _frameWatch = new();
    private Rid _viewport;
    private ulong _lastFrameUsec;

    /// <summary>Whether vsync and the frame cap are off (<c>--uncapped</c>), so frames run as fast as they can.</summary>
    public bool Uncapped { get; private set; }

    /// <summary>Whether the GPU time is a timestamp measurement (every renderer but Compatibility).</summary>
    public bool GpuMeasured { get; private set; }

    /// <summary>The renderer and its driver, as "Mobile D3D12".</summary>
    public string Renderer { get; private set; } = "";

    /// <summary>The time spent in simulation steps this frame, set by the game root.</summary>
    public double SimMs { get; set; }

    public Sample Latest { get; private set; }

    /// <summary>The average over the last second.</summary>
    public Sample Average { get; private set; }

    /// <summary>The slowest frame's headline time in the last second.</summary>
    public double WorstMs { get; private set; }

    /// <summary>Raised after each frame is sampled, for collectors such as the scenario runner.</summary>
    public event System.Action<Sample>? Sampled;

    public override void _Ready()
    {
        ProcessPriority = int.MaxValue;
        _viewport = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(_viewport, true);
        string method = RenderingServer.GetCurrentRenderingMethod();
        string driver = RenderingServer.GetCurrentRenderingDriverName();
        GpuMeasured = method != "gl_compatibility";
        string methodName = method switch
        {
            "mobile" => "Mobile",
            "forward_plus" => "Forward+",
            "gl_compatibility" => "Compatibility",
            _ => method
        };
        string driverName = driver switch
        {
            "d3d12" => "D3D12",
            "vulkan" => "Vulkan",
            "metal" => "Metal",
            "opengl3" or "opengl3_angle" or "opengl3_es" => "OpenGL",
            _ => driver
        };
        Renderer = $"{methodName} {driverName}";
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--uncapped")
            {
                Uncapped = true;
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                Engine.MaxFps = 0;
            }
        }
    }

    /// <summary>Starts the frame timer; called by <see cref="PerfFrameStart"/>, the first update of each frame.</summary>
    public void BeginFrame()
    {
        _frameWatch.Restart();
    }

    public override void _Process(double delta)
    {
        double script = _frameWatch.Elapsed.TotalMilliseconds;
        double render = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport);
        double gpuRaw = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport);
        ulong now = Time.GetTicksUsec();
        double frame = _lastFrameUsec == 0 ? 0.0 : (now - _lastFrameUsec) / 1000.0;
        _lastFrameUsec = now;

        double? gpu = gpuRaw > 0.0 ? gpuRaw : null;
        double work = script + render;
        var sample = new Sample(
            GpuMeasured && gpu is double g ? System.Math.Max(work, g) : work,
            work,
            script,
            render,
            gpu,
            SimMs,
            frame,
            Engine.GetFramesPerSecond(),
            (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
        Latest = sample;

        ulong nowMs = now / 1000;
        _window.Enqueue((nowMs, sample));
        while (_window.Count > 0 && nowMs - _window.Peek().AtMs > WindowMs)
        {
            _window.Dequeue();
        }
        Summarize();
        Sampled?.Invoke(sample);
    }

    private void Summarize()
    {
        double headline = 0, work = 0, script = 0, render = 0, gpu = 0, sim = 0, frame = 0, fps = 0, draws = 0, objects = 0, worst = 0;
        int gpuCount = 0;
        foreach ((ulong _, Sample s) in _window)
        {
            headline += s.HeadlineMs;
            work += s.WorkMs;
            script += s.ScriptMs;
            render += s.RenderCpuMs;
            sim += s.SimMs;
            frame += s.FrameMs;
            fps += s.Fps;
            draws += s.Draws;
            objects += s.Objects;
            worst = System.Math.Max(worst, s.HeadlineMs);
            if (s.GpuMs is double g)
            {
                gpu += g;
                gpuCount++;
            }
        }
        int n = System.Math.Max(1, _window.Count);
        Average = new Sample(headline / n, work / n, script / n, render / n, gpuCount > 0 ? gpu / gpuCount : null,
            sim / n, frame / n, fps / n, (int)(draws / n), (int)(objects / n));
        WorstMs = worst;
    }
}
