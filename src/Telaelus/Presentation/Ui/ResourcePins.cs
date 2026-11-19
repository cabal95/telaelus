using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The resources pinned to the top bar, in table order, at most <see cref="Max"/>. None at start:
/// the first resources discovered pin themselves (<see cref="AutoPin"/>) while there is room, and
/// the inventory panel pins and unpins by hand. Saved with the game.
/// </summary>
public sealed class ResourcePins
{
    /// <summary>The most resources the top bar holds.</summary>
    public const int Max = 8;

    /// <summary>
    /// Whether a resource can be pinned: not Sand or Water, which are capacity with their own
    /// readouts on the top bar beside Power.
    /// </summary>
    public static bool CanPin(ResourceType type)
    {
        return CapacityResources.CapacityOf(type) is null;
    }

    private readonly SortedSet<ResourceType> _pinned = new();

    /// <summary>Raised when a resource is pinned or unpinned.</summary>
    public event Action? Changed;

    public IReadOnlyList<ResourceType> Pinned => _pinned.ToList();

    public bool IsPinned(ResourceType type)
    {
        return _pinned.Contains(type);
    }

    /// <summary>Whether the top bar holds <see cref="Max"/> already, so nothing more can be pinned.</summary>
    public bool IsFull => _pinned.Count >= Max;

    /// <summary>Pins a newly discovered resource when it can be pinned and there is room.</summary>
    public void AutoPin(ResourceType type)
    {
        if (CanPin(type) && !IsFull && _pinned.Add(type))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Replaces the pins (a loaded game's).</summary>
    public void SetAll(IEnumerable<ResourceType> types)
    {
        _pinned.Clear();
        foreach (ResourceType type in types)
        {
            if (CanPin(type) && !IsFull)
            {
                _pinned.Add(type);
            }
        }
        Changed?.Invoke();
    }

    public void Toggle(ResourceType type)
    {
        if (!CanPin(type))
        {
            return;
        }
        if (!_pinned.Remove(type))
        {
            if (IsFull)
            {
                return;
            }
            _pinned.Add(type);
        }
        Changed?.Invoke();
    }
}
