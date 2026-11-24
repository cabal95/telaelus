namespace Telaelus.Sim;

/// <summary>What kind of moment a <see cref="SimEvent"/> marks.</summary>
public enum SimEventKind
{
    /// <summary>A building type was unlocked (its whole cost was held at once for the first time).</summary>
    Unlocked,

    /// <summary>A resource was discovered for the first time.</summary>
    Discovered,

    /// <summary>A research project completed.</summary>
    ResearchComplete,

    /// <summary>A capacity type went into blackout (raised again only after it is back to normal).</summary>
    Blackout,

    /// <summary>A ship launched.</summary>
    ShipLaunched,

    /// <summary>A loaded building no longer fit its larger footprint and its cost went back to storage.</summary>
    RefundedOnLoad
}

/// <summary>
/// A moment during play the presentation may tell the player about, raised by
/// <see cref="Simulation.Happened"/>. Only the field its kind names is meaningful.
/// </summary>
public sealed record SimEvent(SimEventKind Kind)
{
    public BuildingType Building { get; init; }
    public ResourceType Resource { get; init; }
    public ResearchProject Project { get; init; }
    public CapacityType Capacity { get; init; }
}
