using System;

namespace Game.Sim;

/// <summary>Where a resource comes from.</summary>
public enum ResourceKind
{
    /// <summary>Finite resource fields on the map, gathered by hand or by an extractor.</summary>
    Deposit,

    /// <summary>Never runs out; collected by a building on or beside the right terrain.</summary>
    TerrainSourced,

    /// <summary>Made by a processing building from other resources.</summary>
    Refined
}

/// <summary>
/// Static definition of a resource type: its display name, its kind, whether the
/// player can gather it by hand (only deposits can be, and not all of them), and for
/// a deposit, its gather time: the seconds to gather one unit by hand, which an
/// extractor divides by its speed.
/// </summary>
public sealed record ResourceDef(string Name, ResourceKind Kind, bool HandHarvestable, float? GatherSeconds = null)
{
    /// <summary>Only deposits can be resource fields.</summary>
    public bool HasFields => Kind == ResourceKind.Deposit;
}

/// <summary>The definition table for every resource type.</summary>
public static class ResourceDefs
{
    public static ResourceDef For(ResourceType type)
    {
        return type switch
        {
            ResourceType.Wood => new ResourceDef("Wood", ResourceKind.Deposit, true, 3f),
            ResourceType.Stone => new ResourceDef("Stone", ResourceKind.Deposit, true, 4.5f),
            ResourceType.IronOre => new ResourceDef("Iron Ore", ResourceKind.Deposit, true, 6f),
            ResourceType.CopperOre => new ResourceDef("Copper Ore", ResourceKind.Deposit, true, 6f),
            ResourceType.Coal => new ResourceDef("Coal", ResourceKind.Deposit, true, 4.5f),
            ResourceType.CrudeOil => new ResourceDef("Crude Oil", ResourceKind.Deposit, false, 3f),
            ResourceType.Sand => new ResourceDef("Sand", ResourceKind.TerrainSourced, false),
            ResourceType.Water => new ResourceDef("Water", ResourceKind.TerrainSourced, false),
            ResourceType.Iron => new ResourceDef("Iron", ResourceKind.Refined, false),
            ResourceType.RocketFuel => new ResourceDef("Rocket Fuel", ResourceKind.Refined, false),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no resource definition")
        };
    }
}
