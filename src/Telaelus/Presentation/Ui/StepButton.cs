using System;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// An up/down button that repeats while held: it raises <see cref="Step"/> once as the mouse button
/// goes down, then, still held after <see cref="RepeatDelay"/>, <see cref="RepeatRate"/> times a
/// second until released. Leaving the button or turning disabled (a value at its limit) stops it.
/// Timed by the frame's delta, so it runs while the game is paused.
/// </summary>
public partial class StepButton : Button
{
    /// <summary>Seconds held before the steps repeat.</summary>
    public const float RepeatDelay = 0.5f;

    /// <summary>Steps a second while repeating.</summary>
    public const float RepeatRate = 15f;

    private bool _held;
    private float _untilNext;

    public StepButton()
    {
        ButtonDown += OnDown;
        ButtonUp += Stop;
        MouseExited += Stop;
    }

    /// <summary>One step of the value (on press, and on each repeat while held).</summary>
    public event Action? Step;

    public override void _Process(double delta)
    {
        if (!_held)
        {
            return;
        }
        if (Disabled)
        {
            Stop();
            return;
        }
        _untilNext -= (float)delta;
        while (_held && !Disabled && _untilNext <= 0f)
        {
            _untilNext += 1f / RepeatRate;
            Step?.Invoke();
        }
    }

    private void OnDown()
    {
        _held = true;
        _untilNext = RepeatDelay;
        Step?.Invoke();
    }

    private void Stop()
    {
        _held = false;
    }
}
