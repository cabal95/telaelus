namespace Game.Sim;

/// <summary>
/// Every kind of resource. What each one is (a deposit, a terrain-sourced resource,
/// or a refined good) and how it is gathered live in <see cref="ResourceDefs"/>.
/// </summary>
public enum ResourceType
{
    Wood,
    Stone,
    IronOre,
    CopperOre,
    Coal,
    CrudeOil,
    Sand,
    Water,
    Iron,
    RocketFuel
}
