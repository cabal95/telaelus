using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Processes before every other node each frame (lowest process priority) and starts
/// the <see cref="PerfMonitor"/>'s frame timer, which stops when the monitor itself
/// processes last.
/// </summary>
public partial class PerfFrameStart : Node
{
    public PerfMonitor? Monitor { get; set; }

    public override void _Ready()
    {
        ProcessPriority = int.MinValue;
    }

    public override void _Process(double delta)
    {
        Monitor?.BeginFrame();
    }
}
