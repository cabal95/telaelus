using System;
using System.Collections.Generic;
using System.Linq;
using Game.Sim;

namespace Game.Presentation.Ui;

/// <summary>
/// The resources pinned to the top bar, in table order. Pinned and unpinned from the
/// inventory panel; presentation state for the session (there are no saves yet).
/// </summary>
public sealed class ResourcePins
{
    /// <summary>The pins at start: the six starting deposits, then Sand and Water.</summary>
    public static readonly IReadOnlyList<ResourceType> Defaults = new[]
    {
        ResourceType.Wood,
        ResourceType.Stone,
        ResourceType.IronOre,
        ResourceType.CopperOre,
        ResourceType.Coal,
        ResourceType.CrudeOil,
        ResourceType.Sand,
        ResourceType.Water
    };

    private readonly SortedSet<ResourceType> _pinned = new(Defaults);

    /// <summary>Raised when a resource is pinned or unpinned.</summary>
    public event Action? Changed;

    public IReadOnlyList<ResourceType> Pinned => _pinned.ToList();

    public bool IsPinned(ResourceType type)
    {
        return _pinned.Contains(type);
    }

    /// <summary>Replaces the pins (a loaded game's).</summary>
    public void SetAll(IEnumerable<ResourceType> types)
    {
        _pinned.Clear();
        foreach (ResourceType type in types)
        {
            _pinned.Add(type);
        }
        Changed?.Invoke();
    }

    public void Toggle(ResourceType type)
    {
        if (!_pinned.Remove(type))
        {
            _pinned.Add(type);
        }
        Changed?.Invoke();
    }
}
