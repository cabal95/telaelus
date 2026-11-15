namespace Game.Sim;

/// <summary>
/// A Capacity resource on the HQ network: providers supply it, consumers reserve it while switched
/// on, and nothing stores it. Ordered so a provider's draws come before what it supplies (a
/// Pumping Station draws Power and supplies Water), which is the order each tick computes them in.
/// </summary>
public enum CapacityType
{
    Power,
    Water,
    Sand
}

/// <summary>The resources that are capacity rather than stock.</summary>
public static class CapacityResources
{
    /// <summary>The capacity type a resource is (Water, Sand), or null for a stored resource.</summary>
    public static CapacityType? CapacityOf(ResourceType type)
    {
        return type switch
        {
            ResourceType.Water => CapacityType.Water,
            ResourceType.Sand => CapacityType.Sand,
            _ => null
        };
    }
}

/// <summary>Where a capacity type stands on the network.</summary>
public enum CapacityState
{
    /// <summary>Demand at or under supply: consumers run at full speed.</summary>
    Normal,

    /// <summary>Demand up to 25% over supply: consumers slow by twice the overload.</summary>
    Brownout,

    /// <summary>Demand more than 25% over supply, or demand with no supply: every consumer stops.</summary>
    Blackout
}

/// <summary>One capacity type's figures for this tick.</summary>
/// <remarks>
/// For Power, also what Battery Banks hold (`Stored` of `StoreCapacity`, in Power-seconds) and how
/// fast they moved this tick (`StoreFlow`: positive charging, negative discharging, in Power).
/// </remarks>
public readonly record struct CapacityReading(float Supply, float Demand, float Speed, CapacityState State,
    float Stored = 0f, float StoreCapacity = 0f, float StoreFlow = 0f)
{
    public static readonly CapacityReading Idle = new(0f, 0f, 1f, CapacityState.Normal);
}

/// <summary>How a building's footprint is checked against the ground.</summary>
public enum Placement
{
    /// <summary>Every cell buildable, free, explored, and clear of fields.</summary>
    Standard,

    /// <summary>Across a Straight river map cell (the Water Wheel).</summary>
    AcrossStraightRiver,

    /// <summary>The standard rule, with at least one cell edge-adjacent to ShallowWater, DeepWater, or river channel.</summary>
    BesideWater,

    /// <summary>The standard rule, with every cell on Sand terrain.</summary>
    OnSand,

    /// <summary>The standard rule, with every cell on Sand terrain in a Desert map cell (the Brine Well's salt flats).</summary>
    OnDesertSand,

    /// <summary>The standard rule, with at least one cell edge-adjacent to a mountain (the Quarry).</summary>
    BesideMountain,

    /// <summary>The footprint is exactly one map cell, every fine cell Grass, by the standard rule (the Forester).</summary>
    OpenGrassCell,

    /// <summary>
    /// The footprint is exactly a worked-out map cell of a deposit in <see cref="BuildingDef.Sites"/>,
    /// or exactly the center 4 x 4 of a motherlode the building works (the Deep Well, the Deep Mine).
    /// </summary>
    OverSite
}
