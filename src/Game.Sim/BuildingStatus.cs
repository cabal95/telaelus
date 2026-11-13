namespace Game.Sim;

/// <summary>What a building is doing right now.</summary>
public enum BuildingState
{
    /// <summary>Gathering from a field, or running a recipe cycle.</summary>
    Working,

    /// <summary>On, but with nothing to do (see <see cref="BuildingStatus.IdleReason"/>).</summary>
    Idle,

    /// <summary>Switched off by the player.</summary>
    Off
}

/// <summary>A building's state, and when it is not working, why ("No fields in range").</summary>
public readonly record struct BuildingStatus(BuildingState State, string? IdleReason);
