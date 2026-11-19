using System;

namespace Telaelus.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// Raised during play when something the player may want to hear about happens (see
    /// <see cref="SimEventKind"/>). Never raised while a world is being reset or loaded.
    /// </summary>
    public event Action<SimEvent>? Happened;

    private bool _quiet;

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
