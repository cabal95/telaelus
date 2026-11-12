namespace Game.Sim;

/// <summary>What kind of world a simulation holds.</summary>
public enum WorldKind
{
    /// <summary>The fixed 16 x 16 test layout (bounded).</summary>
    TestMap,

    /// <summary>An empty, all-grass 16 x 16 map (bounded).</summary>
    Empty,

    /// <summary>An unbounded world generated chunk by chunk from a seed and a planet.</summary>
    Generated
}

/// <summary>
/// How to build a simulation's world. Bounded worlds (the test map and the empty
/// map) are a fixed rectangle of map cells; a generated world has no edge.
/// </summary>
public sealed record WorldSettings(WorldKind Kind, int Seed, PlanetDef? Planet)
{
    /// <summary>The size of the bounded worlds in map cells.</summary>
    public const int BoundedSize = 16;

    public static WorldSettings TestMap { get; } = new(WorldKind.TestMap, 0, null);

    public static WorldSettings Empty { get; } = new(WorldKind.Empty, 0, null);

    public static WorldSettings Generated(int seed, PlanetDef? planet = null)
    {
        return new WorldSettings(WorldKind.Generated, seed, planet ?? PlanetDefs.Starter);
    }

    public bool IsBounded => Kind != WorldKind.Generated;
}
