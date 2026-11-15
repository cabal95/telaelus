namespace Telaelus.Sim;

/// <summary>
/// Something on top of a map cell's terrain that changes no terrain but adds its
/// own blocking. None is the default (enum value 0). A mountain blocks the whole
/// map cell; a river blocks only its channel.
/// </summary>
public enum MapFeature
{
    None,
    Mountain,
    River
}
