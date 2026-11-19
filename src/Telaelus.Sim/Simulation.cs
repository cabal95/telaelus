using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The authoritative game state and logic. Fixed-step, and free of Godot nodes
/// and input devices. In single-player this runs in-process; conceptually it is
/// the future authoritative "server".
///
/// The world has two grid levels. A fine cell is 1 x 1 world unit; buildings,
/// collision, and movement live on fine cells. A map cell is a square of
/// FineCellsPerMapCell x FineCellsPerMapCell fine cells; terrain and resource
/// fields live on map cells, which are stored in chunks (see SimulationWorld).
/// Coordinates are signed integers, and fine-to-map
/// conversion uses floor division so negative coordinates map correctly.
/// </summary>
public sealed partial class Simulation
{
    public const int FineCellsPerMapCell = 4;

    /// <summary>A bounded world's size in map cells; a generated world has none and throws.</summary>
    public int MapWidth => World.IsBounded ? WorldSettings.BoundedSize : throw new InvalidOperationException("a generated world has no width");
    public int MapHeight => World.IsBounded ? WorldSettings.BoundedSize : throw new InvalidOperationException("a generated world has no height");

    /// <summary>A bounded world's size in fine cells; it occupies [0, GridWidth) on X.</summary>
    public int GridWidth => MapWidth * FineCellsPerMapCell;
    public int GridHeight => MapHeight * FineCellsPerMapCell;

    public Player Player { get; } = new();

    /// <summary>Input for the current tick, set by the presentation before Step.</summary>
    public PlayerIntent Intent;

    private readonly List<ResourceField> _fields = new();
    private readonly Dictionary<(int MapX, int MapZ), ResourceField> _fieldsByMapCell = new();
    public IReadOnlyList<ResourceField> Fields => _fields;

    private readonly List<Building> _buildings = new();
    public IReadOnlyList<Building> Buildings => _buildings;

    private readonly HashSet<(int X, int Z)> _occupied = new();

    private readonly Dictionary<ResourceType, float> _stockpile = new();
    public IReadOnlyDictionary<ResourceType, float> Stockpile => _stockpile;

    /// <summary>Buildings whose whole cost the stockpile has held at once; it never clears during play.</summary>
    private readonly HashSet<BuildingType> _unlocked = new();

    /// <summary>The field currently in range to hand-harvest, or null.</summary>
    public ResourceField? HarvestTarget { get; private set; }

    /// <summary>Progress toward the next hand-gathered unit, 0 to 1; drops to 0 on release or a target change.</summary>
    public float HarvestProgress { get; private set; }

    /// <summary>Whether the harvest target's resource has no room in storage, so harvesting it does nothing.</summary>
    public bool HarvestBlocked => HarvestTarget is ResourceField field && !HasRoom(field.Type, Mathf.Min(field.Amount, 1f));

    /// <summary>How many ships have been launched (the initial win condition).</summary>
    public int ShipsLaunched { get; private set; }

    /// <summary>Seconds in a day and night.</summary>
    public const float DayLength = 600f;

    /// <summary>The time of day a new world starts at: mid-morning.</summary>
    public const float StartTimeOfDay = 0.3f;

    /// <summary>The time of day, 0 to 1 over <see cref="DayLength"/> (0 midnight, 0.5 midday).</summary>
    public float TimeOfDay { get; private set; } = StartTimeOfDay;

    /// <summary>
    /// The daylight, 0 to 1: dark from 0.85 to 0.15, rising through dawn (0.15 to 0.25), full from
    /// 0.25 to 0.75, falling through dusk (0.75 to 0.85).
    /// </summary>
    public float Daylight => DaylightAt(TimeOfDay);

    public static float DaylightAt(float timeOfDay)
    {
        float t = timeOfDay - MathF.Floor(timeOfDay);
        if (t < 0.15f || t >= 0.85f)
        {
            return 0f;
        }
        if (t < 0.25f)
        {
            return (t - 0.15f) / 0.1f;
        }
        if (t <= 0.75f)
        {
            return 1f;
        }
        return (0.85f - t) / 0.1f;
    }

    private int _nextBuildingId;
    private int _nextFieldId;

    private const float HarvestRange = 1.5f;

    /// <summary>The ship bill: what a launch takes from storage.</summary>
    public static readonly IReadOnlyList<ResourceAmount> LaunchCost = new ResourceAmount[]
    {
        new(ResourceType.HullSection, 4f),
        new(ResourceType.Engine, 1f),
        new(ResourceType.ControlModule, 1f),
        new(ResourceType.RocketFuel, 500f),
        new(ResourceType.LiquidOxygen, 200f)
    };

    /// <summary>A simulation of the fixed test map.</summary>
    public Simulation() : this(WorldSettings.TestMap)
    {
    }

    public Simulation(WorldSettings settings)
    {
        Debug = new DebugTools(this);
        ResetWorld(settings);
    }

    public void Step(double dt)
    {
        EnsureChunksAroundPlayer();
        WalkPlayer(dt);
        RevealAroundPlayer();
        UpdateInteraction();
        if (Intent.Harvest && InteractTarget is not null)
        {
            Interact(dt);
        }
        else if (Intent.Harvest)
        {
            Harvest(dt);
        }
        else
        {
            HarvestProgress = 0f;
            InteractProgress = 0f;
        }
        TimeOfDay = (TimeOfDay + (float)(dt / DayLength)) % 1f;
        StepBuildings(dt);
        StepTrucks(dt);
    }

    // ---- Grid -----------------------------------------------------------

    /// <summary>Whether a fine cell lies in the world: always in a generated world, inside the rectangle in a bounded one.</summary>
    public bool InBounds(int x, int z)
    {
        return !World.IsBounded || (x >= 0 && x < GridWidth && z >= 0 && z < GridHeight);
    }

    /// <summary>Whether a map cell lies in the world: always in a generated world, inside the rectangle in a bounded one.</summary>
    public bool MapInBounds(int mapX, int mapZ)
    {
        return !World.IsBounded || (mapX >= 0 && mapX < MapWidth && mapZ >= 0 && mapZ < MapHeight);
    }

    /// <summary>World-space center of a fine cell.</summary>
    public static Vector3 CellCenter(int x, int z)
    {
        return new Vector3(x + 0.5f, 0f, z + 0.5f);
    }

    /// <summary>The fine cell containing a world-space point.</summary>
    public static (int X, int Z) CellAt(float worldX, float worldZ)
    {
        return (Mathf.FloorToInt(worldX), Mathf.FloorToInt(worldZ));
    }

    /// <summary>The map-cell coordinate containing a fine-cell coordinate (floor division).</summary>
    public static int MapCellOf(int fine)
    {
        int quotient = fine / FineCellsPerMapCell;
        return fine % FineCellsPerMapCell < 0 ? quotient - 1 : quotient;
    }

    /// <summary>World-space center of a map cell.</summary>
    public static Vector3 MapCellCenter(int mapX, int mapZ)
    {
        float half = FineCellsPerMapCell * 0.5f;
        return new Vector3(mapX * FineCellsPerMapCell + half, 0f, mapZ * FineCellsPerMapCell + half);
    }

    // ---- Placement -------------------------------------------------------

    /// <summary>
    /// The standard placement rule: every footprint cell is in bounds, buildable
    /// (buildable terrain with no blocking feature), unoccupied, and not covered by a resource field, and no unit is inside the
    /// footprint (today, the player's cell). Extractors follow the same rule and
    /// may be placed with no field in range (they idle).
    /// </summary>
    public bool CanPlace(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        BuildingDef def = BuildingDefs.For(type);
        // No building stands on a road or bridge: the paving must be removed first.
        (int footWidth, int footDepth) = def.Footprint(facing);
        for (int x = anchorX; x < anchorX + footWidth; x++)
        {
            for (int z = anchorZ; z < anchorZ + footDepth; z++)
            {
                if (IsPaved(x, z))
                {
                    return false;
                }
            }
        }
        if (def.Placement == Placement.AcrossStraightRiver)
        {
            return CanPlaceAcrossRiver(type, anchorX, anchorZ, facing);
        }
        if (CanPlaceRenewable(type, def, anchorX, anchorZ, facing) is bool special)
        {
            return special;
        }

        // The player's cell, by the same point test movement collision uses.
        (int playerX, int playerZ) = CellAt(Player.Position.X, Player.Position.Z);

        (int width, int depth) = def.Footprint(facing);
        bool besideWater = false;
        for (int x = anchorX; x < anchorX + width; x++)
        {
            for (int z = anchorZ; z < anchorZ + depth; z++)
            {
                if (!InBounds(x, z) || !IsExplored(MapCellOf(x), MapCellOf(z)) || _occupied.Contains((x, z)) || !IsBuildable(x, z) || FieldAt(x, z) is not null)
                {
                    return false;
                }
                if (x == playerX && z == playerZ)
                {
                    return false;
                }
                if (def.Placement is Placement.OnSand or Placement.OnDesertSand && TerrainAt(x, z) != TerrainType.Sand)
                {
                    return false;
                }
                if (def.Placement == Placement.OnDesertSand && BiomeAt(x, z) != BiomeType.Desert)
                {
                    return false;
                }
                besideWater |= def.Placement == Placement.BesideWater && BesideWater(x, z);
            }
        }
        return def.Placement != Placement.BesideWater || besideWater;
    }

    /// <summary>Whether a fine cell is edge-adjacent to ShallowWater, DeepWater, or river channel.</summary>
    private bool BesideWater(int x, int z)
    {
        foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int nx = x + dx;
            int nz = z + dz;
            if (InBounds(nx, nz) && (TerrainDefs.IsWater(TerrainAt(nx, nz)) || IsRiverChannel(nx, nz)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The Water Wheel's rule: its footprint lies within one map cell whose river is Straight, its
    /// long side across the flow (facing North or South on a North-South river, East or West on an
    /// East-West one); each cell is buildable or river channel, explored, free, and not the
    /// player's.
    /// </summary>
    private bool CanPlaceAcrossRiver(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        int mapX = MapCellOf(anchorX);
        int mapZ = MapCellOf(anchorZ);
        if (RiverShapeAt(mapX, mapZ) != RiverShape.Straight)
        {
            return false;
        }
        bool northSouth = RiverConnects(mapX, mapZ, Facing.North);
        bool facingAcross = northSouth ? facing is Facing.North or Facing.South : facing is Facing.East or Facing.West;
        if (!facingAcross)
        {
            return false;
        }
        (int playerX, int playerZ) = CellAt(Player.Position.X, Player.Position.Z);
        (int width, int depth) = BuildingDefs.For(type).Footprint(facing);
        for (int x = anchorX; x < anchorX + width; x++)
        {
            for (int z = anchorZ; z < anchorZ + depth; z++)
            {
                if (MapCellOf(x) != mapX || MapCellOf(z) != mapZ || !InBounds(x, z) || !IsExplored(mapX, mapZ)
                    || _occupied.Contains((x, z)) || !(IsBuildable(x, z) || IsRiverChannel(x, z)) || (x == playerX && z == playerZ))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// A building is unlocked once the stockpile has held its whole cost at once; it
    /// stays unlocked when the stock later runs short.
    /// </summary>
    public bool IsUnlocked(BuildingType type)
    {
        return _unlocked.Contains(type);
    }

    /// <summary>Whether the stockpile holds the building's whole cost.</summary>
    public bool CanAfford(BuildingType type)
    {
        return HasAll(BuildingDefs.For(type).Cost);
    }

    /// <summary>
    /// Places a building as the player does: the spot must pass <see cref="CanPlace"/>,
    /// and the building must be unlocked and affordable. Its cost is deducted; a
    /// refused placement changes nothing.
    /// </summary>
    public bool TryPlaceBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        if (!CanPlace(type, anchorX, anchorZ, facing) || !IsUnlocked(type) || !CanAfford(type))
        {
            return false;
        }

        foreach (ResourceAmount cost in BuildingDefs.For(type).Cost)
        {
            AddToStockpile(cost.Type, -cost.Amount);
        }
        AddBuilding(type, anchorX, anchorZ, facing);
        return true;
    }

    /// <summary>The building with an id, or null.</summary>
    public Building? BuildingById(int id)
    {
        return _buildings.Find(b => b.Id == id);
    }

    /// <summary>The building whose footprint covers a fine cell, or null.</summary>
    public Building? BuildingAt(int x, int z)
    {
        if (!_occupied.Contains((x, z)))
        {
            return null;
        }
        return _buildings.Find(b => x >= b.AnchorX && x < b.AnchorX + b.Width && z >= b.AnchorZ && z < b.AnchorZ + b.Depth);
    }

    /// <summary>The switched-on extractors working a field right now (whose current field it is).</summary>
    public List<Building> ExtractorsWorking(ResourceField field)
    {
        return _buildings.FindAll(b => b.Enabled && b.Def.IsExtractor && ReferenceEquals(b.CurrentField, field));
    }

    /// <summary>
    /// Switches a building on or off (never a permanent one, the HQ). Switching off drops the work in hand: progress
    /// goes back to 0, and a processor's running cycle ends with its inputs returned to
    /// the stockpile (fuel, once buildings burn it, will not be). False when there is
    /// no such building.
    /// </summary>
    public bool SetBuildingEnabled(int id, bool enabled)
    {
        Building? building = BuildingById(id);
        if (building is null || building.Def.Permanent)
        {
            return false;
        }
        if (building.Enabled && !enabled)
        {
            foreach (ResourceAmount input in RunningInputs(building))
            {
                AddToStockpile(input.Type, input.Amount);
            }
            building.Crafting = false;
            building.Progress = 0f;
        }
        building.Enabled = enabled;
        RecomputeNetwork();
        return true;
    }

    /// <summary>The inputs a processor's running cycle has consumed (none when idle or not a processor).</summary>
    private static IReadOnlyList<ResourceAmount> RunningInputs(Building building)
    {
        return building.Crafting ? building.CycleInputs : Array.Empty<ResourceAmount>();
    }

    /// <summary>
    /// What a building would refund if destroyed now: half of each cost
    /// amount, rounded up, plus the inputs of a processor's running cycle and everything in its
    /// buffers, in full.
    /// </summary>
    public IReadOnlyList<ResourceAmount> DestroyRefund(Building building)
    {
        var refund = new Dictionary<ResourceType, float>();
        foreach (ResourceAmount cost in building.Def.Cost)
        {
            Add(refund, cost.Type, Mathf.Ceil(cost.Amount * 0.5f));
        }
        foreach (ResourceAmount input in RunningInputs(building))
        {
            Add(refund, input.Type, input.Amount);
        }
        foreach ((ResourceType type, float amount) in building.Output)
        {
            Add(refund, type, amount);
        }
        foreach ((ResourceType type, float amount) in building.Input)
        {
            Add(refund, type, amount);
        }
        foreach ((ResourceType type, float amount) in building.FuelSlot)
        {
            Add(refund, type, amount);
        }
        foreach (ResourceAmount truck in TruckRefund(building))
        {
            Add(refund, truck.Type, truck.Amount);
        }
        var list = new List<ResourceAmount>();
        foreach ((ResourceType type, float amount) in refund)
        {
            list.Add(new ResourceAmount(type, amount));
        }
        return list;

        static void Add(Dictionary<ResourceType, float> into, ResourceType type, float amount)
        {
            into.TryGetValue(type, out float current);
            into[type] = current + amount;
        }
    }

    /// <summary>
    /// Destroys a building: removes it, frees its cells, and adds its refund
    /// (<see cref="DestroyRefund"/>) to the stockpile, even past a storage limit. False when there
    /// is no such building or it is permanent (the HQ).
    /// </summary>
    public bool TryDestroyBuilding(int id)
    {
        Building? building = BuildingById(id);
        if (building is null || building.Def.Permanent)
        {
            return false;
        }
        foreach (ResourceAmount amount in DestroyRefund(building))
        {
            AddToStockpile(amount.Type, amount.Amount);
        }
        RemoveBuilding(building);
        return true;
    }

    private void RemoveBuilding(Building building)
    {
        _buildings.Remove(building);
        if (ReferenceEquals(InteractTarget, building))
        {
            InteractTarget = null;
            InteractProgress = 0f;
        }
        for (int x = building.AnchorX; x < building.AnchorX + building.Width; x++)
        {
            for (int z = building.AnchorZ; z < building.AnchorZ + building.Depth; z++)
            {
                _occupied.Remove((x, z));
            }
        }
        RecomputeNetwork();
        TrucksOnBuildingsChanged(building, added: false);
        RenewablesOnBuildingRemoved(building);
    }

    /// <summary>
    /// What a building is doing: Off when switched off; an extractor Works while it has a
    /// field (or one in range to take up), else is Idle with "No fields in range"; a
    /// processor Works during a cycle, else is Idle waiting for its missing inputs.
    /// </summary>
    public BuildingStatus BuildingStatusOf(Building building)
    {
        if (!building.Enabled)
        {
            return new BuildingStatus(BuildingState.Off, "Switched off");
        }
        if (CapacityBlock(building) is string blocked)
        {
            return new BuildingStatus(BuildingState.Idle, blocked);
        }
        if (building.Def.StoreCapacity > 0f)
        {
            return building.Online
                ? new BuildingStatus(BuildingState.Working, null)
                : new BuildingStatus(BuildingState.Idle, "Offline: stores nothing");
        }
        if (building.Def.Researches)
        {
            return ActiveResearch is null
                ? new BuildingStatus(BuildingState.Idle, "No research running")
                : new BuildingStatus(BuildingState.Working, null);
        }
        if (building.Def.Solar && building.Online && Daylight <= 0f)
        {
            return new BuildingStatus(BuildingState.Idle, "Night: supplies nothing");
        }
        if (building.Def.TruckSlots > 0 && !building.Online)
        {
            return new BuildingStatus(BuildingState.Idle, "Offline: trucks stay parked");
        }
        if (building.Def.IsProvider)
        {
            if (building.Def.Eats is (ResourceType food, float _) && building.Starved)
            {
                return new BuildingStatus(BuildingState.Idle, building.Online
                    ? $"Waiting for {ResourceDefs.For(food).Name}"
                    : "Offline: supplies nothing");
            }
            if (building.Def.BurnsFuel && !HasFuel(building))
            {
                return new BuildingStatus(BuildingState.Idle, building.Online ? "Waiting for fuel" : "Waiting for fuel (load by hand)");
            }
            return building.Online
                ? new BuildingStatus(BuildingState.Working, null)
                : new BuildingStatus(BuildingState.Idle, "Offline: supplies nothing");
        }
        if (building.Def.ProduceSeconds > 0f)
        {
            return BufferHasRoom(building, 1f)
                ? new BuildingStatus(BuildingState.Working, null)
                : new BuildingStatus(BuildingState.Idle, "Full: waiting for pickup");
        }
        if (building.Def.IsExtractor)
        {
            ResourceField? field = building.CurrentField is { Depleted: false } current ? current : NearestFieldInWorkArea(building);
            if (field is null)
            {
                return new BuildingStatus(BuildingState.Idle, "No fields in range");
            }
            return BufferHasRoom(building, Mathf.Min(field.Amount, 1f))
                ? new BuildingStatus(BuildingState.Working, null)
                : new BuildingStatus(BuildingState.Idle, "Full: waiting for pickup");
        }
        if (building.Recipe is not Recipe recipe)
        {
            return new BuildingStatus(BuildingState.Working, null);
        }
        bool finished = building.Crafting && building.Progress >= recipe.CycleTime;
        if ((finished || !building.Crafting) && !OutputsHaveRoom(building, recipe))
        {
            return new BuildingStatus(BuildingState.Idle, building.Online
                ? StorageFull(FirstOutputWithoutRoom(recipe) ?? recipe.Outputs[0].Type)
                : "Full: collect by hand");
        }
        bool needsFuel = building.Def.BurnsFuel && !HasFuel(building);
        if (building.Crafting && !finished && needsFuel)
        {
            return new BuildingStatus(BuildingState.Idle, building.Online ? "Waiting for fuel" : "Waiting for fuel (load by hand)");
        }
        if (building.Crafting)
        {
            return new BuildingStatus(BuildingState.Working, null);
        }
        var missing = new List<string>();
        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            ResourceType type = ChosenInput(building, recipe, i);
            if (AtHand(building, type) < recipe.Inputs[i].Amount - RoomTolerance)
            {
                // On Auto with none at hand it waits for any of them.
                missing.Add(building.InputChoices.ContainsKey(i) ? ResourceDefs.For(type).Name : recipe.InputName(i));
            }
        }
        if (missing.Count == 0)
        {
            return needsFuel
                ? new BuildingStatus(BuildingState.Idle, building.Online ? "Waiting for fuel" : "Waiting for fuel (load by hand)")
                : new BuildingStatus(BuildingState.Working, null);
        }
        return new BuildingStatus(BuildingState.Idle, "Waiting for " + string.Join(", ", missing)
            + (building.Online ? "" : " (load by hand)"));
    }

    private Building AddBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        var building = new Building(_nextBuildingId++, type, anchorX, anchorZ, facing, Telaelus.Sim.Recipes.For(type));
        _buildings.Add(building);
        for (int x = anchorX; x < anchorX + building.Width; x++)
        {
            for (int z = anchorZ; z < anchorZ + building.Depth; z++)
            {
                _occupied.Add((x, z));
            }
        }
        RecomputeNetwork();
        TrucksOnBuildingsChanged(building, added: true);
        RenewablesOnBuildingAdded(building);
        return building;
    }

    // ---- Ship launch (initial win condition) -----------------------------

    /// <summary>Whether storage holds the whole ship bill.</summary>
    public bool LaunchBillMet => HasAll(LaunchCost);

    /// <summary>The Shipyard a ship launches from: the first switched-on, online one, or null.</summary>
    public Building? LaunchShipyard => _buildings.Find(b => b.Type == BuildingType.Shipyard && b.Enabled && b.Online);

    /// <summary>A launch needs the whole ship bill in storage and a Shipyard to launch from.</summary>
    public bool CanLaunchShip()
    {
        return LaunchBillMet && LaunchShipyard is not null;
    }

    /// <summary>Consume the launch cost and record a launched ship. The initial win.</summary>
    public bool TryLaunchShip()
    {
        if (!CanLaunchShip())
        {
            return false;
        }

        foreach (ResourceAmount cost in LaunchCost)
        {
            AddToStockpile(cost.Type, -cost.Amount);
        }
        ShipsLaunched++;
        Raise(new SimEvent(SimEventKind.ShipLaunched));
        return true;
    }

    // ---- Resource fields -------------------------------------------------

    /// <summary>The field covering a fine cell, or null.</summary>
    public ResourceField? FieldAt(int x, int z)
    {
        _fieldsByMapCell.TryGetValue((MapCellOf(x), MapCellOf(z)), out ResourceField? field);
        return field;
    }

    /// <summary>Distance from a point to the nearest point of a field's square (0 inside).</summary>
    public static float DistanceToField(Vector3 point, ResourceField field)
    {
        float size = field.Width;
        float dx = Mathf.Max(Mathf.Max(field.MinX - point.X, 0f), point.X - (field.MinX + size));
        float dz = Mathf.Max(Mathf.Max(field.MinZ - point.Z, 0f), point.Z - (field.MinZ + size));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Non-depleted fields of a type within a radius of a point.</summary>
    public List<ResourceField> FieldsInRange(Vector3 point, float radius, ResourceType type)
    {
        return FieldsInRange(point, radius, new[] { type });
    }

    /// <summary>Non-depleted fields of any of the given types within a radius of a point.</summary>
    public List<ResourceField> FieldsInRange(Vector3 point, float radius, IReadOnlyList<ResourceType> types)
    {
        var result = new List<ResourceField>();
        foreach (ResourceField field in _fields)
        {
            if (Contains(types, field.Type) && field.Workable && DistanceToField(point, field) <= radius)
            {
                result.Add(field);
            }
        }
        return result;
    }

    /// <summary>Adds a field; without an amount it takes its biome's amount for the type.</summary>
    private ResourceField AddField(ResourceType type, int mapX, int mapZ, float? amount = null)
    {
        float initial = amount ?? BiomeDefs.For(MapBiomeAt(mapX, mapZ)).FieldAmount(type);
        var field = new ResourceField(_nextFieldId++, type, mapX, mapZ, initial);
        RegisterField(field);
        return field;
    }

    /// <summary>Adds a field to the list and indexes it under every map cell it covers; discovers it when any is explored.</summary>
    private void RegisterField(ResourceField field)
    {
        _fields.Add(field);
        bool seen = false;
        for (int x = field.MapX; x < field.MapX + field.Size; x++)
        {
            for (int z = field.MapZ; z < field.MapZ + field.Size; z++)
            {
                _fieldsByMapCell[(x, z)] = field;
                seen |= IsExplored(x, z);
            }
        }
        if (seen)
        {
            DiscoverField(field);
        }
    }

    private void UnregisterField(ResourceField field)
    {
        _fields.Remove(field);
        for (int x = field.MapX; x < field.MapX + field.Size; x++)
        {
            for (int z = field.MapZ; z < field.MapZ + field.Size; z++)
            {
                _fieldsByMapCell.Remove((x, z));
            }
        }
        if (ReferenceEquals(HarvestTarget, field))
        {
            HarvestTarget = null;
        }
    }

    private void RemoveField(ResourceField field)
    {
        UnregisterField(field);
        if (field.Owner is Building owner && ReferenceEquals(owner.Plot, field))
        {
            owner.Plot = null;
            owner.PlotWait = owner.Def.Plot!.Value.RegrowSeconds;
        }
    }

    // ---- Player hand-harvest --------------------------------------------

    private static bool Contains(IReadOnlyList<ResourceType> types, ResourceType type)
    {
        foreach (ResourceType candidate in types)
        {
            if (candidate == type)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The nearest hand-harvestable field within reach (a Crude Oil field needs an Oil
    /// Well). A new target drops any partial progress.
    /// </summary>
    private void UpdateHarvestTarget()
    {
        ResourceField? best = null;
        float bestDistance = HarvestRange;
        foreach (ResourceField field in _fields)
        {
            if (!ResourceDefs.For(field.Type).HandHarvestable || !field.Workable)
            {
                continue;
            }
            float distance = DistanceToField(Player.Position, field);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = field;
            }
        }
        if (!ReferenceEquals(best, HarvestTarget))
        {
            HarvestProgress = 0f;
        }
        HarvestTarget = best;
    }

    /// <summary>Works toward one unit of the target at its gather time, then takes it.</summary>
    private void Harvest(double dt)
    {
        ResourceField? field = HarvestTarget;
        if (field is null || HarvestBlocked)
        {
            return;
        }

        HarvestProgress += (float)dt / GatherSeconds(field.Type);
        if (HarvestProgress >= 1f)
        {
            HarvestProgress = 0f;
            TakeUnit(field);
        }
    }

    private static float GatherSeconds(ResourceType type)
    {
        return ResourceDefs.For(type).GatherSeconds
            ?? throw new InvalidOperationException($"{type} has no gather time");
    }

    /// <summary>Moves one unit (or what is left) from a field to the stockpile, removing the field when it empties.</summary>
    private void TakeUnit(ResourceField field)
    {
        AddToStockpile(field.Type, TakeFromField(field));
    }

    /// <summary>Takes one unit (or what is left) from a field, removing it when it empties; returns the amount.</summary>
    private float TakeFromField(ResourceField field)
    {
        float take = Mathf.Min(field.Amount, 1f);
        field.Amount -= take;
        if (field.Depleted)
        {
            OnFieldDepleted(field);
            RemoveField(field);
        }
        return take;
    }

    // ---- Buildings -------------------------------------------------------

    private void StepBuildings(double dt)
    {
        StepCapacity(dt);
        foreach (Building building in _buildings)
        {
            if (!building.Enabled)
            {
                continue;
            }
            if (building.Def.Plot is not null)
            {
                StepForester(building, dt);
                continue;
            }
            if (building.Def.ProduceSeconds > 0f)
            {
                StepProducer(building, dt);
                continue;
            }
            if (building.Def.IsExtractor)
            {
                StepExtractor(building, dt);
                continue;
            }

            Recipe? recipe = building.Recipe;
            if (recipe is not null)
            {
                StepProcessor(building, recipe, dt);
            }
        }
        StepResearch(dt);
        TransferFromCollectors();
    }

    private void StepExtractor(Building building, double dt)
    {
        ResourceField? field = building.CurrentField;
        if (field is null || field.Depleted)
        {
            field = NearestFieldInWorkArea(building);
            building.CurrentField = field;
            building.Progress = 0f;
        }
        if (field is null)
        {
            return;
        }

        building.Progress += (float)dt * WorkSpeed(building);
        float perUnit = GatherSeconds(field.Type) / building.Def.Speed;
        if (building.Progress < perUnit)
        {
            return;
        }
        if (!BufferHasRoom(building, Mathf.Min(field.Amount, 1f)))
        {
            // A whole unit waits at the field until the buffer has room for it.
            building.Progress = perUnit;
            return;
        }

        building.Progress -= perUnit;
        ResourceType type = field.Type;
        Put(building.Output, type, TakeFromField(field));
        if (field.Depleted)
        {
            building.CurrentField = null;
        }
    }

    private ResourceField? NearestFieldInWorkArea(Building building)
    {
        Vector3 center = building.Position;
        ResourceField? best = null;
        float bestDistance = float.MaxValue;
        foreach (ResourceField field in FieldsInRange(center, building.Def.WorkRadius, building.Def.Works))
        {
            float distance = DistanceToField(center, field);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = field;
            }
        }
        return best;
    }

    private void StepProcessor(Building building, Recipe recipe, double dt)
    {
        if (building.Online)
        {
            DrainOutput(building);
            ReturnStaleInputs(building);
        }
        float speed = WorkSpeed(building);
        if (!building.Crafting)
        {
            if (speed <= 0f || !InputsAvailable(building, recipe) || !OutputsHaveRoom(building, recipe)
                || (building.Def.BurnsFuel && !HasFuel(building)))
            {
                return;
            }
            ConsumeInputs(building, recipe);
            building.Crafting = true;
            building.Progress = 0f;
        }

        if (building.Progress < recipe.CycleTime)
        {
            // A fuel burner's cycle runs only while a unit burns.
            if (building.Def.BurnsFuel && !Burn(building, dt))
            {
                return;
            }
            building.Progress = Mathf.Min(building.Progress + (float)dt * speed, recipe.CycleTime);
        }

        // A finished cycle's output waits in the building until it fits somewhere.
        if (building.Progress >= recipe.CycleTime && Deliver(building, recipe))
        {
            building.Crafting = false;
            building.Progress = 0f;
            building.CycleInputs.Clear();
            TakeUpPendingRecipe(building);
        }
    }

    /// <summary>Whether the inputs are at hand: in the input buffer, plus storage when online.</summary>
    private bool InputsAvailable(Building building, Recipe recipe)
    {
        foreach (ResourceAmount input in ResolvedInputs(building, recipe))
        {
            if (AtHand(building, input.Type) < input.Amount - RoomTolerance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Takes a cycle's inputs, from the input buffer first and then (online) from storage.</summary>
    private void ConsumeInputs(Building building, Recipe recipe)
    {
        building.CycleInputs.Clear();
        foreach (ResourceAmount input in ResolvedInputs(building, recipe))
        {
            building.CycleInputs.Add(input);
            float fromBuffer = Mathf.Min(Held(building.Input, input.Type), input.Amount);
            Put(building.Input, input.Type, -fromBuffer);
            if (input.Amount - fromBuffer > RoomTolerance)
            {
                AddToStockpile(input.Type, -(input.Amount - fromBuffer));
            }
        }
    }

    private static float OutputTotal(Recipe recipe)
    {
        float total = 0f;
        foreach (ResourceAmount output in recipe.Outputs)
        {
            total += output.Amount;
        }
        return total;
    }

    private static bool BufferHasRoom(Building building, float amount)
    {
        return building.OutputCount + amount <= building.Def.OutputBuffer + RoomTolerance;
    }

    /// <summary>Whether a cycle's outputs have somewhere to go: storage when online, else the output buffer.</summary>
    private bool OutputsHaveRoom(Building building, Recipe recipe)
    {
        return (building.Online && OutputsFit(recipe)) || BufferHasRoom(building, OutputTotal(recipe));
    }

    /// <summary>Puts a finished cycle's outputs into storage when online and they fit, else into the output buffer.</summary>
    private bool Deliver(Building building, Recipe recipe)
    {
        if (building.Online && OutputsFit(recipe))
        {
            ProduceOutputs(recipe);
            return true;
        }
        if (!BufferHasRoom(building, OutputTotal(recipe)))
        {
            return false;
        }
        foreach (ResourceAmount output in recipe.Outputs)
        {
            Put(building.Output, output.Type, output.Amount);
        }
        return true;
    }

    private bool HasAll(IReadOnlyList<ResourceAmount> amounts)
    {
        foreach (ResourceAmount amount in amounts)
        {
            _stockpile.TryGetValue(amount.Type, out float have);
            if (have < amount.Amount)
            {
                return false;
            }
        }
        return true;
    }


    private void ProduceOutputs(Recipe recipe)
    {
        foreach (ResourceAmount output in recipe.Outputs)
        {
            AddToStockpile(output.Type, output.Amount);
        }
    }

    private void AddToStockpile(ResourceType type, float amount)
    {
        _stockpile.TryGetValue(type, out float current);
        _stockpile[type] = current + amount;
        if (amount > 0f)
        {
            Discover(type);
            UnlockAffordable();
        }
    }

    private void UnlockAffordable()
    {
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (!_unlocked.Contains(type) && BuildingDefs.For(type).Buildable && CanAfford(type))
            {
                _unlocked.Add(type);
                Raise(new SimEvent(SimEventKind.Unlocked)
                {
                    Building = type
                });
            }
        }
    }
}
