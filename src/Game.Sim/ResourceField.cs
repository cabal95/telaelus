using Godot;

namespace Game.Sim;

/// <summary>
/// A harvestable deposit filling exactly one map cell (and so all of that map
/// cell's fine cells). Depletes as it is harvested and is removed from the
/// simulation once empty. The amount is continuous; <see cref="Stage"/> is the
/// coarse, visible step count the presentation shows.
/// </summary>
public sealed class ResourceField
{
    /// <summary>Number of visible depletion stages (models) a full field shows.</summary>
    public const int StageCount = 5;

    public int Id { get; }
    public ResourceType Type { get; }
    public int MapX { get; }
    public int MapZ { get; }
    public float InitialAmount { get; }
    public float Amount { get; set; }

    public ResourceField(int id, ResourceType type, int mapX, int mapZ, float amount)
    {
        Id = id;
        Type = type;
        MapX = mapX;
        MapZ = mapZ;
        InitialAmount = amount;
        Amount = amount;
    }

    public bool Depleted => Amount <= 0f;

    public float RemainingFraction => InitialAmount > 0f ? Amount / InitialAmount : 0f;

    /// <summary>Visible stage: StageCount when full, 1 just before depletion, 0 when depleted.</summary>
    public int Stage => Depleted ? 0 : Mathf.CeilToInt(RemainingFraction * StageCount);

    /// <summary>World-space minimum corner of the field's map cell.</summary>
    public float MinX => MapX * Simulation.FineCellsPerMapCell;

    public float MinZ => MapZ * Simulation.FineCellsPerMapCell;

    /// <summary>World-space center of the field's map cell.</summary>
    public Vector3 Center => new(
        MinX + Simulation.FineCellsPerMapCell * 0.5f,
        0f,
        MinZ + Simulation.FineCellsPerMapCell * 0.5f);
}
