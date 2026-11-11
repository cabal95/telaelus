namespace Game.Sim;

/// <summary>
/// Which way a building faces. East and West swap a footprint's width and depth.
/// Stored now so belts can use a building's input and output sides later.
/// </summary>
public enum Facing
{
    North,
    East,
    South,
    West
}
