using System;
using System.Collections.Generic;
using Godot;

namespace Game.Sim;

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

    private int _nextBuildingId;
    private int _nextFieldId;

    private const float HarvestRange = 1.5f;

    public const float LaunchRocketFuelCost = 500f;
    public const float LaunchIronCost = 100f;

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
        MovePlayer(dt);
        RevealAroundPlayer();
        UpdateHarvestTarget();
        if (Intent.Harvest)
        {
            Harvest(dt);
        }
        else
        {
            HarvestProgress = 0f;
        }
        StepBuildings(dt);
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
        // The player's cell, by the same point test movement collision uses.
        (int playerX, int playerZ) = CellAt(Player.Position.X, Player.Position.Z);

        (int width, int depth) = BuildingDefs.For(type).Footprint(facing);
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
        return true;
    }

    /// <summary>The inputs a processor's running cycle has consumed (none when idle or not a processor).</summary>
    private static IReadOnlyList<ResourceAmount> RunningInputs(Building building)
    {
        return building.Crafting && building.Recipe is Recipe recipe
            ? recipe.Inputs
            : Array.Empty<ResourceAmount>();
    }

    /// <summary>
    /// What a building would refund if destroyed now: half of each cost
    /// amount, rounded up, plus the inputs of a processor's running cycle in full.
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
        for (int x = building.AnchorX; x < building.AnchorX + building.Width; x++)
        {
            for (int z = building.AnchorZ; z < building.AnchorZ + building.Depth; z++)
            {
                _occupied.Remove((x, z));
            }
        }
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
        if (building.Def.IsExtractor)
        {
            ResourceField? field = building.CurrentField is { Depleted: false } current ? current : NearestFieldInWorkArea(building);
            if (field is null)
            {
                return new BuildingStatus(BuildingState.Idle, "No fields in range");
            }
            return HasRoom(field.Type, Mathf.Min(field.Amount, 1f))
                ? new BuildingStatus(BuildingState.Working, null)
                : new BuildingStatus(BuildingState.Idle, StorageFull(field.Type));
        }
        if (building.Recipe is not Recipe recipe)
        {
            return new BuildingStatus(BuildingState.Working, null);
        }
        if (FirstOutputWithoutRoom(recipe) is ResourceType full && (!building.Crafting || building.Progress >= recipe.CycleTime))
        {
            return new BuildingStatus(BuildingState.Idle, StorageFull(full));
        }
        if (building.Crafting)
        {
            return new BuildingStatus(BuildingState.Working, null);
        }
        var missing = new List<string>();
        foreach (ResourceAmount input in recipe.Inputs)
        {
            _stockpile.TryGetValue(input.Type, out float have);
            if (have < input.Amount)
            {
                missing.Add(ResourceDefs.For(input.Type).Name);
            }
        }
        return missing.Count == 0
            ? new BuildingStatus(BuildingState.Working, null)
            : new BuildingStatus(BuildingState.Idle, "Waiting for " + string.Join(", ", missing));
    }

    private Building AddBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        var building = new Building(_nextBuildingId++, type, anchorX, anchorZ, facing, Recipes.For(type));
        _buildings.Add(building);
        for (int x = anchorX; x < anchorX + building.Width; x++)
        {
            for (int z = anchorZ; z < anchorZ + building.Depth; z++)
            {
                _occupied.Add((x, z));
            }
        }
        return building;
    }

    // ---- Ship launch (initial win condition) -----------------------------

    public bool CanLaunchShip()
    {
        _stockpile.TryGetValue(ResourceType.RocketFuel, out float fuel);
        _stockpile.TryGetValue(ResourceType.Iron, out float iron);
        return fuel >= LaunchRocketFuelCost && iron >= LaunchIronCost;
    }

    /// <summary>Consume the launch cost and record a launched ship. The initial win.</summary>
    public bool TryLaunchShip()
    {
        if (!CanLaunchShip())
        {
            return false;
        }

        AddToStockpile(ResourceType.RocketFuel, -LaunchRocketFuelCost);
        AddToStockpile(ResourceType.Iron, -LaunchIronCost);
        ShipsLaunched++;
        return true;
    }

    // ---- Resource fields -------------------------------------------------

    /// <summary>The field covering a fine cell, or null.</summary>
    public ResourceField? FieldAt(int x, int z)
    {
        _fieldsByMapCell.TryGetValue((MapCellOf(x), MapCellOf(z)), out ResourceField? field);
        return field;
    }

    /// <summary>Distance from a point to the nearest point of a field's map-cell square (0 inside).</summary>
    public static float DistanceToField(Vector3 point, ResourceField field)
    {
        float size = FineCellsPerMapCell;
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
            if (Contains(types, field.Type) && !field.Depleted && DistanceToField(point, field) <= radius)
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
        _fields.Add(field);
        _fieldsByMapCell[(mapX, mapZ)] = field;
        if (IsExplored(mapX, mapZ))
        {
            Discover(type);
        }
        return field;
    }

    private void RemoveField(ResourceField field)
    {
        _fields.Remove(field);
        _fieldsByMapCell.Remove((field.MapX, field.MapZ));
        if (ReferenceEquals(HarvestTarget, field))
        {
            HarvestTarget = null;
        }
    }

    // ---- Player hand-harvest --------------------------------------------

    private void MovePlayer(double dt)
    {
        Vector2 move = Intent.Move;
        if (move.LengthSquared() > 1f)
        {
            move = move.Normalized();
        }

        Vector3 p = Player.Position;
        Vector3 step = new Vector3(move.X, 0f, move.Y) * Player.Speed * TerrainSpeedAt(p.X, p.Z) * (float)dt;

        // Move each axis independently so the player slides along impassable
        // terrain instead of sticking on it.
        float nextX = p.X + step.X;
        if (IsWalkableAt(nextX, p.Z))
        {
            p.X = nextX;
        }
        float nextZ = p.Z + step.Z;
        if (IsWalkableAt(p.X, nextZ))
        {
            p.Z = nextZ;
        }

        if (World.IsBounded)
        {
            p.X = Mathf.Clamp(p.X, 0f, GridWidth);
            p.Z = Mathf.Clamp(p.Z, 0f, GridHeight);
        }
        Player.Position = p;
    }

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
            if (!ResourceDefs.For(field.Type).HandHarvestable)
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
        float take = Mathf.Min(field.Amount, 1f);
        field.Amount -= take;
        AddToStockpile(field.Type, take);

        if (field.Depleted)
        {
            RemoveField(field);
        }
    }

    // ---- Buildings -------------------------------------------------------

    private void StepBuildings(double dt)
    {
        foreach (Building building in _buildings)
        {
            if (!building.Enabled)
            {
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

        building.Progress += (float)dt;
        float perUnit = GatherSeconds(field.Type) / building.Def.Speed;
        if (building.Progress < perUnit)
        {
            return;
        }
        if (!HasRoom(field.Type, Mathf.Min(field.Amount, 1f)))
        {
            // A whole unit waits at the field until storage has room for it.
            building.Progress = perUnit;
            return;
        }

        building.Progress -= perUnit;
        TakeUnit(field);
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
        if (!building.Crafting)
        {
            if (!HasInputs(recipe) || !OutputsFit(recipe))
            {
                return;
            }
            ConsumeInputs(recipe);
            building.Crafting = true;
            building.Progress = 0f;
        }

        // A finished cycle's output waits in the building until it fits.
        building.Progress = Mathf.Min(building.Progress + (float)dt, recipe.CycleTime);
        if (building.Progress >= recipe.CycleTime && OutputsFit(recipe))
        {
            ProduceOutputs(recipe);
            building.Crafting = false;
            building.Progress = 0f;
        }
    }

    private bool HasInputs(Recipe recipe)
    {
        return HasAll(recipe.Inputs);
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

    private void ConsumeInputs(Recipe recipe)
    {
        foreach (ResourceAmount input in recipe.Inputs)
        {
            AddToStockpile(input.Type, -input.Amount);
        }
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
            }
        }
    }
}
