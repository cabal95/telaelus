using Godot;

namespace Game.Sim;

/// <summary>
/// A harvestable deposit on the ground. Depletes as it is harvested and is
/// removed from the simulation once empty.
/// </summary>
public sealed class ResourceNode
{
    public int Id { get; }
    public ResourceType Type { get; }
    public Vector3 Position { get; }
    public float InitialAmount { get; }
    public float Amount { get; set; }

    public ResourceNode(int id, ResourceType type, Vector3 position, float amount)
    {
        Id = id;
        Type = type;
        Position = position;
        InitialAmount = amount;
        Amount = amount;
    }

    public bool Depleted => Amount <= 0f;

    public float RemainingFraction => InitialAmount > 0f ? Amount / InitialAmount : 0f;
}
