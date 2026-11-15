using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// The town scene's script (scenes/Town.tscn): keeps it gently alive behind the main menu. While
/// <see cref="Alive"/>, its truck drives the road loop unhurried and the light follows a clock
/// running at the game's pace from mid-morning (<see cref="DayLight"/>). Not alive, it holds still at
/// midday with the truck at the start of its loop, which is how the splash image is rendered.
/// </summary>
public partial class TownLife : Node3D
{
    /// <summary>The truck's speed on its loop: a third of a real truck's road speed (12), an unhurried drive behind the menu.</summary>
    private const float TruckSpeed = 4f;

    [Export]
    public bool Alive { get; set; } = true;

    [Export]
    public float TimeOfDay { get; set; } = Telaelus.Sim.Simulation.StartTimeOfDay;

    private DirectionalLight3D? _sun;
    private PathFollow3D? _follow;

    public override void _Ready()
    {
        _sun = GetNodeOrNull<DirectionalLight3D>("Sun");
        _follow = GetNodeOrNull<PathFollow3D>("TruckRoute/Follow");
        if (!Alive)
        {
            TimeOfDay = 0.5f;
        }
        Apply();
    }

    public override void _Process(double delta)
    {
        if (!Alive)
        {
            return;
        }
        TimeOfDay = (TimeOfDay + (float)(delta / Telaelus.Sim.Simulation.DayLength)) % 1f;
        if (_follow is not null)
        {
            _follow.Progress += TruckSpeed * (float)delta;
        }
        Apply();
    }

    private void Apply()
    {
        if (_sun is not null)
        {
            DayLight.Apply(_sun, TimeOfDay);
        }
    }
}
