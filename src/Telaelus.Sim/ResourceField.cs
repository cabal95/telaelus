using Godot;

namespace Telaelus.Sim;

/// <summary>
/// A harvestable deposit filling whole map cells: one map cell for an ordinary field, 2 x 2 for a
/// motherlode. An ordinary field depletes as it is harvested and is removed from the simulation
/// once empty; the amount is continuous, and <see cref="Stage"/> is the coarse, visible step count
/// the presentation shows. A motherlode is endless and only a Deep Mine or Deep Well on its center
/// works it. A Forester's field is growing for a while before anything may work it.
/// </summary>
public sealed class ResourceField
{
    /// <summary>Number of visible depletion stages (models) a full field shows.</summary>
    public const int StageCount = 5;

    public int Id { get; }

    /// <summary>The deposit (for a motherlode, its largest yield).</summary>
    public ResourceType Type { get; }

    /// <summary>Its minimum-corner map cell.</summary>
    public int MapX { get; }

    public int MapZ { get; }

    /// <summary>How many map cells it spans on a side: 1, or 2 for a motherlode.</summary>
    public int Size { get; }

    /// <summary>The motherlode's kind, or null for an ordinary field.</summary>
    public MotherlodeType? Motherlode { get; }

    public float InitialAmount { get; }

    public float Amount { get; set; }

    /// <summary>The Forester whose plot this field is, or null.</summary>
    public Building? Owner { get; init; }

    /// <summary>Whether it is still growing (a Forester's field): nothing works or harvests it until it is grown.</summary>
    public bool Growing { get; set; }

    /// <summary>Seconds of growth left while growing.</summary>
    public float GrowLeft { get; set; }

    public ResourceField(int id, ResourceType type, int mapX, int mapZ, float amount)
    {
        Id = id;
        Type = type;
        MapX = mapX;
        MapZ = mapZ;
        Size = 1;
        InitialAmount = amount;
        Amount = amount;
    }

    /// <summary>A motherlode: 2 x 2 map cells, endless.</summary>
    public ResourceField(int id, MotherlodeType kind, int mapX, int mapZ)
    {
        Id = id;
        Motherlode = kind;
        Type = MotherlodeDefs.For(kind).Yields[0].Type;
        MapX = mapX;
        MapZ = mapZ;
        Size = 2;
    }

    public bool IsMotherlode => Motherlode is not null;

    /// <summary>Whether extractors and the player may work it: grown, and not a motherlode.</summary>
    public bool Workable => !Growing && !IsMotherlode && !Depleted;

    public bool Depleted => !IsMotherlode && Amount <= 0f;

    public float RemainingFraction => IsMotherlode ? 1f : InitialAmount > 0f ? Amount / InitialAmount : 0f;

    /// <summary>Visible stage: StageCount when full, 1 just before depletion, 0 when depleted.</summary>
    public int Stage => Depleted ? 0 : Mathf.CeilToInt(RemainingFraction * StageCount);

    /// <summary>How far it has grown, 0 at planting to 1 grown.</summary>
    public float Growth(float growSeconds) => Growing && growSeconds > 0f ? 1f - GrowLeft / growSeconds : 1f;

    /// <summary>Its side in world units (4, or 8 for a motherlode).</summary>
    public float Width => Size * Simulation.FineCellsPerMapCell;

    /// <summary>World-space minimum corner.</summary>
    public float MinX => MapX * Simulation.FineCellsPerMapCell;

    public float MinZ => MapZ * Simulation.FineCellsPerMapCell;

    /// <summary>World-space center.</summary>
    public Vector3 Center => new(MinX + Width * 0.5f, 0f, MinZ + Width * 0.5f);

    /// <summary>Whether it covers a map cell.</summary>
    public bool Covers(int mapX, int mapZ)
    {
        return mapX >= MapX && mapX < MapX + Size && mapZ >= MapZ && mapZ < MapZ + Size;
    }
}
