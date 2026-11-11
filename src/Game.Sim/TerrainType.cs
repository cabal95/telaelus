namespace Game.Sim;

/// <summary>
/// The ground of a map cell. Grass is the default (enum value 0). Mountains and
/// rivers are features on top of terrain, not terrain (see <see cref="MapFeature"/>).
/// </summary>
public enum TerrainType
{
    Grass,
    Sand,
    Snow,
    Rock,
    Mud,
    ShallowWater,
    DeepWater,
    Lava
}
