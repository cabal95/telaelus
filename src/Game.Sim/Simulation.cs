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
/// fields live on map cells. Coordinates are signed integers, and fine-to-map
/// conversion uses floor division so negative coordinates map correctly.
/// </summary>
public sealed partial class Simulation
{
    public const int FineCellsPerMapCell = 4;

    // The fixed test map's size in map cells. Procedural generation will replace
    // this bound with on-demand cells; nothing else assumes a fixed size.
    public int MapWidth { get; } = 16;
    public int MapHeight { get; } = 16;

    /// <summary>The test map's size in fine cells; it occupies [0, GridWidth) on X.</summary>
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

    /// <summary>The field currently in range to hand-harvest, or null.</summary>
    public ResourceField? HarvestTarget { get; private set; }

    /// <summary>How many ships have been launched (the initial win condition).</summary>
    public int ShipsLaunched { get; private set; }

    private int _nextBuildingId;
    private int _nextFieldId;

    private const float HarvestRange = 1.5f;
    private const float HarvestRate = 10f;

    public const float LaunchRocketFuelCost = 500f;
    public const float LaunchIronCost = 100f;

    public Simulation()
    {
        Debug = new DebugTools(this);
        _terrain = new TerrainType[MapWidth, MapHeight];
        _biomes = new BiomeType[MapWidth, MapHeight];
        _features = new MapFeature[MapWidth, MapHeight];
        _riverFlow = new Facing?[MapWidth, MapHeight];
        GenerateTerrain();
        SpawnInitialFields();
        Player.Position = new Vector3(GridWidth * 0.5f, 0f, GridHeight * 0.5f);
    }

    public void Step(double dt)
    {
        MovePlayer(dt);
        UpdateHarvestTarget();
        if (Intent.Harvest)
        {
            Harvest(dt);
        }
        StepBuildings(dt);
    }

    // ---- Grid -----------------------------------------------------------

    /// <summary>Whether a fine cell lies within the test map.</summary>
    public bool InBounds(int x, int z)
    {
        return x >= 0 && x < GridWidth && z >= 0 && z < GridHeight;
    }

    /// <summary>Whether a map cell lies within the test map.</summary>
    public bool MapInBounds(int mapX, int mapZ)
    {
        return mapX >= 0 && mapX < MapWidth && mapZ >= 0 && mapZ < MapHeight;
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
                if (!InBounds(x, z) || _occupied.Contains((x, z)) || !IsBuildable(x, z) || FieldAt(x, z) is not null)
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

    public bool TryPlaceBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        if (!CanPlace(type, anchorX, anchorZ, facing))
        {
            return false;
        }

        var building = new Building(_nextBuildingId++, type, anchorX, anchorZ, facing, Recipes.For(type));
        _buildings.Add(building);
        for (int x = anchorX; x < anchorX + building.Width; x++)
        {
            for (int z = anchorZ; z < anchorZ + building.Depth; z++)
            {
                _occupied.Add((x, z));
            }
        }
        return true;
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

        p.X = Mathf.Clamp(p.X, 0f, GridWidth);
        p.Z = Mathf.Clamp(p.Z, 0f, GridHeight);
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

    /// <summary>The nearest hand-harvestable field within reach (a Crude Oil field needs an Oil Well).</summary>
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
        HarvestTarget = best;
    }

    private void Harvest(double dt)
    {
        ResourceField? field = HarvestTarget;
        if (field is null)
        {
            return;
        }

        float take = Mathf.Min(field.Amount, HarvestRate * (float)dt);
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
        }
        if (field is null)
        {
            return;
        }

        float take = Mathf.Min(field.Amount, Recipes.ExtractorRate * (float)dt);
        field.Amount -= take;
        AddToStockpile(field.Type, take);

        if (field.Depleted)
        {
            RemoveField(field);
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
            if (!HasInputs(recipe))
            {
                return;
            }
            ConsumeInputs(recipe);
            building.Crafting = true;
            building.Progress = 0f;
        }

        building.Progress += (float)dt;
        if (building.Progress >= recipe.CycleTime)
        {
            ProduceOutputs(recipe);
            building.Crafting = false;
            building.Progress = 0f;
        }
    }

    private bool HasInputs(Recipe recipe)
    {
        foreach (ResourceAmount input in recipe.Inputs)
        {
            _stockpile.TryGetValue(input.Type, out float have);
            if (have < input.Amount)
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
    }
}
