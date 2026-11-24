using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// Raised during play when something the player may want to hear about happens (see
    /// <see cref="SimEventKind"/>). Never raised while a world is being reset or loaded.
    /// </summary>
    public event Action<SimEvent>? Happened;

    private bool _quiet;

    /// <summary>What a load had to tell the player, raised on the first step after it.</summary>
    private readonly List<SimEvent> _afterLoad = new();

    /// <summary>Raises what a load held back (<see cref="_afterLoad"/>), once.</summary>
    private void RaiseAfterLoad()
    {
        if (_afterLoad.Count == 0)
        {
            return;
        }
        var held = _afterLoad.ToArray();
        _afterLoad.Clear();
        foreach (SimEvent e in held)
        {
            Raise(e);
        }
    }

    private void Raise(SimEvent e)
    {
        if (!_quiet)
        {
            Happened?.Invoke(e);
        }
    }

    /// <summary>Runs a whole-state change (a new world, a load) without raising <see cref="Happened"/>.</summary>
    private void Quietly(Action change)
    {
        bool was = _quiet;
        _quiet = true;
        try
        {
            change();
        }
        finally
        {
            _quiet = was;
        }
    }
}
