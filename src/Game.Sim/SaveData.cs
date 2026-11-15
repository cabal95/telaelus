using System.Collections.Generic;

namespace Game.Sim;

// The sim's whole state as plain data for a save (see Simulation.Save and Simulation.Load, and
// SaveFormat for the file). Everything derived each tick (who is online, the occupied cells,
// discovery events) is rebuilt on load and not stored.

public sealed class SaveData
{
    public int FormatVersion { get; set; } = SaveFormat.Version;
    public WorldData World { get; set; } = new();
    public List<ChunkData> Chunks { get; set; } = new();
    public List<FieldData> Fields { get; set; } = new();
    public List<WorkedOutData> WorkedOut { get; set; } = new();
    public List<BuildingData> Buildings { get; set; } = new();
    public List<TruckData> Trucks { get; set; } = new();
    /// <summary>Collector and truck ids a truck found no path for.</summary>
    public List<int[]> Unreachable { get; set; } = new();
    public List<AmountData> Stockpile { get; set; } = new();
    public List<BuildingType> Unlocked { get; set; } = new();
    public List<ResourceType> Discovered { get; set; } = new();
    public int ShipsLaunched { get; set; }
    public float TimeOfDay { get; set; }
    public ResearchData Research { get; set; } = new();
    public List<int[]> RoadTiles { get; set; } = new();
    public List<BridgeData> Bridges { get; set; } = new();
    public float[] Player { get; set; } = new float[3];
    /// <summary>The map cell sight was last revealed from (it reveals again only on entering another), or null.</summary>
    public int[]? RevealedFrom { get; set; }
    public int NextBuildingId { get; set; }
    public int NextFieldId { get; set; }
    public int NextTruckId { get; set; }
    public int NextBridgeId { get; set; }
    /// <summary>Last tick's capacity readings (a Fission Reactor reads last tick's Water).</summary>
    public List<CapacityData> Capacity { get; set; } = new();
}

public sealed class WorldData
{
    public WorldKind Kind { get; set; }
    public int Seed { get; set; }
    public string? Planet { get; set; }
    public bool Fog { get; set; }
}

/// <summary>A chunk's layers, one byte per map cell (x major): terrain, biome, feature, river flow (255 for none), explored.</summary>
public sealed class ChunkData
{
    public int X { get; set; }
    public int Z { get; set; }
    public byte[] Terrain { get; set; } = System.Array.Empty<byte>();
    public byte[] Biomes { get; set; } = System.Array.Empty<byte>();
    public byte[] Features { get; set; } = System.Array.Empty<byte>();
    public byte[] Flows { get; set; } = System.Array.Empty<byte>();
    public byte[] Explored { get; set; } = System.Array.Empty<byte>();
}

public sealed class FieldData
{
    public int Id { get; set; }
    public ResourceType Type { get; set; }
    public MotherlodeType? Motherlode { get; set; }
    public int MapX { get; set; }
    public int MapZ { get; set; }
    public float InitialAmount { get; set; }
    public float Amount { get; set; }
    public int? Owner { get; set; }
    public bool Growing { get; set; }
    public float GrowLeft { get; set; }
}

public sealed class WorkedOutData
{
    public int MapX { get; set; }
    public int MapZ { get; set; }
    public ResourceType Deposit { get; set; }
}

public sealed class AmountData
{
    public ResourceType Type { get; set; }
    public float Amount { get; set; }

    public AmountData()
    {
    }

    public AmountData(ResourceType type, float amount)
    {
        Type = type;
        Amount = amount;
    }
}

public sealed class BuildingData
{
    public int Id { get; set; }
    public BuildingType Type { get; set; }
    public int AnchorX { get; set; }
    public int AnchorZ { get; set; }
    public Facing Facing { get; set; }
    public bool Enabled { get; set; }
    public int RecipeIndex { get; set; }
    public int? PendingRecipe { get; set; }
    public List<int[]> InputChoices { get; set; } = new();
    public List<AmountData> CycleInputs { get; set; } = new();
    public List<AmountData> Output { get; set; } = new();
    public List<AmountData> Input { get; set; } = new();
    public List<AmountData> FuelSlot { get; set; } = new();
    public ResourceType? Burning { get; set; }
    public float BurnLeft { get; set; }
    public float Charge { get; set; }
    public ResourceType? FuelChoice { get; set; }
    public ResourceType SiloResource { get; set; }
    public int? CurrentField { get; set; }
    public bool Crafting { get; set; }
    public float Progress { get; set; }
    public ResourceType? OutputChoice { get; set; }
    public int? SiteMotherlode { get; set; }
    public ResourceType? SiteDeposit { get; set; }
    public int? Plot { get; set; }
    public float PlotWait { get; set; }
    public float FeedLeft { get; set; }
    public bool Starved { get; set; }
}

public sealed class TruckData
{
    public int Id { get; set; }
    public int Depot { get; set; }
    public int Bay { get; set; }
    public bool CameWithDepot { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float[] Heading { get; set; } = new float[3];
    public TruckState State { get; set; }
    public ResourceType? Cargo { get; set; }
    public float Amount { get; set; }
    public int? Target { get; set; }
    public float Timer { get; set; }
    /// <summary>The cell centers it follows, x and z in pairs (y is always 0).</summary>
    public float[] Path { get; set; } = System.Array.Empty<float>();
    public int PathIndex { get; set; }
}

public sealed class ResearchData
{
    public ResearchProject? Active { get; set; }
    public List<ResearchProgressData> Projects { get; set; } = new();
}

public sealed class ResearchProgressData
{
    public ResearchProject Project { get; set; }
    public double Progress { get; set; }
    public bool Paid { get; set; }
    public bool Complete { get; set; }
}

public sealed class BridgeData
{
    public int Id { get; set; }
    public bool AlongX { get; set; }
    public List<int[]> Tiles { get; set; } = new();
}

public sealed class CapacityData
{
    public CapacityType Type { get; set; }
    public float Supply { get; set; }
    public float Demand { get; set; }
    public float Speed { get; set; }
    public CapacityState State { get; set; }
    public float Stored { get; set; }
    public float StoreCapacity { get; set; }
    public float StoreFlow { get; set; }
}
