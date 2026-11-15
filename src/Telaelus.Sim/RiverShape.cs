namespace Telaelus.Sim;

/// <summary>
/// The shape of a river cell, derived from its connections: Pool (none), End
/// (one), Straight (two opposite), Bend (two adjacent), Junction (three), Cross
/// (four).
/// </summary>
public enum RiverShape
{
    Pool,
    End,
    Straight,
    Bend,
    Junction,
    Cross
}
