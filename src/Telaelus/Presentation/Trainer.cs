using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// The trainer: one instruction at a time on the step card, each done by doing it, from walking to
/// the game menu. Presentation only: it reads the sim, the camera, the build controller, and the open
/// dialog, and keeps its own counters for the step in hand (not saved; a resumed step counts again).
/// A step already done when it comes up completes at once.
/// </summary>
public sealed class Trainer
{
    /// <summary>How long a done step's card reads "Done" before the next one.</summary>
    public const double DoneSeconds = 1.0;

    private readonly GameRoot _game;
    private readonly IReadOnlyList<TrainerStep> _steps;
    private double _doneFor = -1.0;

    // The counters of the step in hand.
    private Vector3 _lastPlayer;
    private float _walked;
    private float _startZoom;
    private bool _zoomedIn;
    private bool _zoomedOut;
    private Facing _startFacing;
    private float _lastWood;
    private bool _collected;
    private bool _sawResearch;

    /// <summary>The step in hand (0-based), or null while the trainer is off.</summary>
    public int? Step { get; private set; }

    public int Total => _steps.Count;

    /// <summary>Whether the step in hand is done and its card reads "Done".</summary>
    public bool ShowingDone => _doneFor >= 0.0;

    public Trainer(GameRoot game)
    {
        _game = game;
        _steps = Steps();
    }

    /// <summary>Starts (or restarts) the trainer at a step.</summary>
    public void Start(int step = 0)
    {
        Begin(Math.Clamp(step, 0, _steps.Count - 1));
    }

    /// <summary>Ends the trainer, taking its card and pointer away.</summary>
    public void Stop()
    {
        Step = null;
        _doneFor = -1.0;
        _game.Notifications.HideStep();
        _game.Pointer.Target = null;
    }

    private void Begin(int step)
    {
        Step = step;
        _doneFor = -1.0;
        _lastPlayer = _game.Sim.Player.Position;
        _walked = 0f;
        _startZoom = _game.Camera.ZoomLevel;
        _zoomedIn = false;
        _zoomedOut = false;
        _startFacing = _game.Builder.Facing;
        _lastWood = Stock(ResourceType.Wood);
        _collected = false;
        _sawResearch = false;
        TrainerStep s = _steps[step];
        _game.Notifications.ShowStep($"Trainer {step + 1} / {_steps.Count}", s.Text, s.Hint);
    }

    /// <summary>Checks the step in hand once a frame, and moves on once it has shown "Done".</summary>
    public void Update(double delta)
    {
        if (Step is not int step)
        {
            return;
        }
        Count();
        TrainerStep current = _steps[step];
        _game.Pointer.Target = _doneFor < 0.0 ? current.Pointer?.Invoke() : null;
        if (_doneFor < 0.0)
        {
            if (current.Done())
            {
                _doneFor = 0.0;
                _game.Notifications.MarkStepDone();
            }
            return;
        }
        _doneFor += delta;
        if (_doneFor < DoneSeconds)
        {
            return;
        }
        if (step + 1 < _steps.Count)
        {
            Begin(step + 1);
            return;
        }
        Stop();
        _game.Notifications.Show(ToastStyle.Lasting, null, "Trainer complete",
            "You know the controls. Gather Stone for a Mine, and a Depot's trucks will haul for you.");
    }

    private void Count()
    {
        Vector3 player = _game.Sim.Player.Position;
        _walked += player.DistanceTo(_lastPlayer);
        _lastPlayer = player;
        _zoomedIn |= _game.Camera.ZoomLevel < _startZoom - 0.01f;
        _zoomedOut |= _game.Camera.ZoomLevel > _startZoom + 0.01f;
        float wood = Stock(ResourceType.Wood);
        Simulation sim = _game.Sim;
        if (wood > _lastWood && sim.Intent.Harvest && sim.InteractTarget is { Type: BuildingType.LumberCamp }
            && sim.InteractAction == InteractAction.Collect)
        {
            _collected = true;
        }
        _lastWood = wood;
        _sawResearch |= _game.Dialogs.Current == _game.ResearchTree;
    }

    private float Stock(ResourceType type)
    {
        return _game.Sim.Stockpile.TryGetValue(type, out float amount) ? amount : 0f;
    }

    private Vector3? NearestWood()
    {
        Vector3 player = _game.Sim.Player.Position;
        ResourceField? field = _game.Sim.Fields
            .Where(f => f.Type == ResourceType.Wood && f.Amount > 0f && _game.Sim.IsExplored(f.MapX, f.MapZ))
            .OrderBy(f => f.Center.DistanceTo(player))
            .FirstOrDefault();
        return field?.Center;
    }

    private Building? Camp => _game.Sim.Buildings.FirstOrDefault(b => b.Type == BuildingType.LumberCamp);

    private IReadOnlyList<TrainerStep> Steps()
    {
        CameraRig camera = _game.Camera;
        BuildController builder = _game.Builder;
        DialogHost dialogs = _game.Dialogs;
        return new TrainerStep[]
        {
            new("Right-click the ground to walk there.", "Ctrl + left click works too.",
                () => _walked >= 4f),
            new("Drag with the right mouse button to look around.", "Ctrl + left drag works too.",
                () => camera.Detached && camera.GroundCenter.DistanceTo(_game.Sim.Player.Position) >= 8f),
            new("Scroll the mouse wheel to zoom in and out.", null,
                () => _zoomedIn && _zoomedOut),
            new("Press [Space] to bring the view back to you.", null,
                () => !camera.Detached),
            new("Walk to the trees and hold [E] to gather Wood.", "Gather 15 to unlock the Lumber Camp.",
                () => _game.Sim.IsUnlocked(BuildingType.LumberCamp), NearestWood),
            new("Press [I] to see what you have.", null,
                () => dialogs.Current == _game.Inventory),
            new("Press [B] and choose the Lumber Camp.", null,
                () => builder.Selected == BuildingType.LumberCamp),
            new("Press [R] to turn it.", null,
                () => builder.Selected == BuildingType.LumberCamp && builder.Facing != _startFacing),
            new("Left-click near the trees to place it.", "It needs trees in range and the HQ's network. [Esc] cancels.",
                () => Camp is not null, NearestWood),
            new("Click the Lumber Camp to see what it is doing.", "[Esc] first leaves build mode.",
                () => dialogs.Current == _game.BuildingDetail && _game.BuildingDetail.Building?.Type == BuildingType.LumberCamp,
                () => Camp?.Position),
            new("Its Wood waits in the camp. Stand by it and hold [E] to collect it.", null,
                () => _collected, () => Camp?.Position),
            new("Press [N] to show the network.", "Buildings work only inside it.",
                () => _game.Network.OverlayShown),
            new("Press [T] to see research, then [Esc] for the game menu.", "Save, load, and quit from there.",
                () => _sawResearch && dialogs.Current == _game.GameMenu),
        };
    }
}

/// <summary>One trainer step: its instruction, an optional hint, when it is done, and where the pointer ring goes.</summary>
public sealed record TrainerStep(string Text, string? Hint, Func<bool> Done, Func<Vector3?>? Pointer = null);
