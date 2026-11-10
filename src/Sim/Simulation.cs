using System.Collections.Generic;
using Godot;

namespace Game.Sim;

/// <summary>
/// The authoritative game state and logic. Fixed-step, and free of Godot nodes
/// and input devices. In single-player this runs in-process; conceptually it is
/// the future authoritative "server".
/// </summary>
public sealed class Simulation
{
    // Grid definition. Buildings snap to this grid; it is centered on the origin.
    public int GridWidth { get; } = 32;
    public int GridHeight { get; } = 32;
    public float TileSize { get; } = 1f;

    public Player Player { get; } = new();

    /// <summary>Input for the current tick, set by the presentation before Step.</summary>
    public PlayerIntent Intent;

    private readonly List<ResourceNode> _nodes = new();
    public IReadOnlyList<ResourceNode> Nodes => _nodes;

    private readonly List<Building> _buildings = new();
    public IReadOnlyList<Building> Buildings => _buildings;

    private readonly HashSet<(int X, int Z)> _occupied = new();

    private readonly Dictionary<ResourceType, float> _stockpile = new();
    public IReadOnlyDictionary<ResourceType, float> Stockpile => _stockpile;

    private readonly TerrainType[,] _terrain;

    /// <summary>The node currently in range to hand-harvest, or null.</summary>
    public ResourceNode? HarvestTarget { get; private set; }

    /// <summary>How many ships have been launched (the initial win condition).</summary>
    public int ShipsLaunched { get; private set; }

    private int _nextBuildingId;

    private const float HarvestRange = 1.5f;
    private const float HarvestRate = 10f;

    public const float LaunchFuelCost = 500f;
    public const float LaunchMetalCost = 100f;

    public float WorldSizeX => GridWidth * TileSize;
    public float WorldSizeZ => GridHeight * TileSize;

    public Simulation()
    {
        _terrain = new TerrainType[GridWidth + 1, GridHeight + 1];
        GenerateTerrain();
        SpawnInitialNodes();
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

    // ---- Placement -------------------------------------------------------

    public bool InBounds(int gridX, int gridZ)
    {
        int halfX = GridWidth / 2;
        int halfZ = GridHeight / 2;
        return gridX >= -halfX && gridX <= halfX && gridZ >= -halfZ && gridZ <= halfZ;
    }

    public Vector3 TileCenter(int gridX, int gridZ)
    {
        return new Vector3(gridX * TileSize, 0f, gridZ * TileSize);
    }

    public bool CanPlace(BuildingType type, int gridX, int gridZ)
    {
        if (!InBounds(gridX, gridZ))
        {
            return false;
        }
        if (_occupied.Contains((gridX, gridZ)))
        {
            return false;
        }
        if (!IsPassable(gridX, gridZ))
        {
            return false;
        }

        // Extractors need a node to draw from; processors need a clear tile.
        bool hasNode = NodeAt(gridX, gridZ) is not null;
        return type == BuildingType.Extractor ? hasNode : !hasNode;
    }

    public bool TryPlaceBuilding(BuildingType type, int gridX, int gridZ)
    {
        if (!CanPlace(type, gridX, gridZ))
        {
            return false;
        }

        var building = new Building(
            _nextBuildingId++,
            type,
            gridX,
            gridZ,
            TileCenter(gridX, gridZ),
            Recipes.For(type))
        {
            Source = type == BuildingType.Extractor ? NodeAt(gridX, gridZ) : null
        };

        _buildings.Add(building);
        _occupied.Add((gridX, gridZ));
        return true;
    }

    // ---- Ship launch (initial win condition) -----------------------------

    public bool CanLaunchShip()
    {
        _stockpile.TryGetValue(ResourceType.Fuel, out float fuel);
        _stockpile.TryGetValue(ResourceType.Metal, out float metal);
        return fuel >= LaunchFuelCost && metal >= LaunchMetalCost;
    }

    /// <summary>Consume the launch cost and record a launched ship. The initial win.</summary>
    public bool TryLaunchShip()
    {
        if (!CanLaunchShip())
        {
            return false;
        }

        AddToStockpile(ResourceType.Fuel, -LaunchFuelCost);
        AddToStockpile(ResourceType.Metal, -LaunchMetalCost);
        ShipsLaunched++;
        return true;
    }

    private ResourceNode? NodeAt(int gridX, int gridZ)
    {
        foreach (ResourceNode node in _nodes)
        {
            int nx = Mathf.RoundToInt(node.Position.X / TileSize);
            int nz = Mathf.RoundToInt(node.Position.Z / TileSize);
            if (nx == gridX && nz == gridZ)
            {
                return node;
            }
        }
        return null;
    }

    // ---- Player hand-harvest --------------------------------------------

    private void MovePlayer(double dt)
    {
        Vector2 move = Intent.Move;
        if (move.LengthSquared() > 1f)
        {
            move = move.Normalized();
        }

        Vector3 step = new Vector3(move.X, 0f, move.Y) * Player.Speed * (float)dt;
        Vector3 p = Player.Position;

        // Move each axis independently so the player slides along impassable
        // terrain instead of sticking on it.
        float nextX = p.X + step.X;
        if (IsPassableAt(nextX, p.Z))
        {
            p.X = nextX;
        }
        float nextZ = p.Z + step.Z;
        if (IsPassableAt(p.X, nextZ))
        {
            p.Z = nextZ;
        }

        float halfX = WorldSizeX * 0.5f;
        float halfZ = WorldSizeZ * 0.5f;
        p.X = Mathf.Clamp(p.X, -halfX, halfX);
        p.Z = Mathf.Clamp(p.Z, -halfZ, halfZ);
        Player.Position = p;
    }

    private void UpdateHarvestTarget()
    {
        ResourceNode? best = null;
        float bestDistance = HarvestRange * HarvestRange;
        foreach (ResourceNode node in _nodes)
        {
            float distance = Player.Position.DistanceSquaredTo(node.Position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }
        HarvestTarget = best;
    }

    private void Harvest(double dt)
    {
        ResourceNode? node = HarvestTarget;
        if (node is null)
        {
            return;
        }

        float take = Mathf.Min(node.Amount, HarvestRate * (float)dt);
        node.Amount -= take;
        AddToStockpile(node.Type, take);

        if (node.Depleted)
        {
            RemoveNode(node);
        }
    }

    // ---- Buildings -------------------------------------------------------

    private void StepBuildings(double dt)
    {
        foreach (Building building in _buildings)
        {
            if (building.Type == BuildingType.Extractor)
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
        ResourceNode? node = building.Source;
        if (node is null || node.Depleted)
        {
            return;
        }

        float take = Mathf.Min(node.Amount, Recipes.ExtractorRate * (float)dt);
        node.Amount -= take;
        AddToStockpile(node.Type, take);

        if (node.Depleted)
        {
            RemoveNode(node);
            building.Source = null;
        }
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

    private void RemoveNode(ResourceNode node)
    {
        _nodes.Remove(node);
        if (ReferenceEquals(HarvestTarget, node))
        {
            HarvestTarget = null;
        }
    }

    // ---- Terrain ---------------------------------------------------------

    private int HalfX => GridWidth / 2;
    private int HalfZ => GridHeight / 2;

    public TerrainType TerrainAt(int x, int z)
    {
        if (!InBounds(x, z))
        {
            return TerrainType.Grass;
        }
        return _terrain[x + HalfX, z + HalfZ];
    }

    /// <summary>Only grass is walkable and buildable; water and mountain block.</summary>
    public bool IsPassable(int x, int z)
    {
        return TerrainAt(x, z) == TerrainType.Grass;
    }

    private bool IsPassableAt(float worldX, float worldZ)
    {
        int tileX = Mathf.RoundToInt(worldX / TileSize);
        int tileZ = Mathf.RoundToInt(worldZ / TileSize);
        return IsPassable(tileX, tileZ);
    }

    private void SetTerrain(int x, int z, TerrainType type)
    {
        if (InBounds(x, z))
        {
            _terrain[x + HalfX, z + HalfZ] = type;
        }
    }

    private void GenerateTerrain()
    {
        // Fixed test layout: a water pond and a small mountain cluster on grass.
        for (int x = 2; x <= 6; x++)
        {
            for (int z = -4; z <= 0; z++)
            {
                SetTerrain(x, z, TerrainType.Water);
            }
        }

        (int X, int Z)[] mountains =
        {
            (-6, 2),
            (-6, 3),
            (-5, 2)
        };
        foreach ((int mountainX, int mountainZ) in mountains)
        {
            SetTerrain(mountainX, mountainZ, TerrainType.Mountain);
        }
    }

    private void SpawnInitialNodes()
    {
        (ResourceType Type, int X, int Z)[] placements =
        {
            (ResourceType.Ore, 6, 4),
            (ResourceType.Ore, 7, 5),
            (ResourceType.Ore, 5, 6),
            (ResourceType.Wood, -8, -6),
            (ResourceType.Wood, -7, -7),
            (ResourceType.Wood, -9, -5),
            (ResourceType.Ore, -6, 8),
            (ResourceType.Wood, 9, -8)
        };

        int id = 0;
        foreach ((ResourceType type, int x, int z) in placements)
        {
            _nodes.Add(new ResourceNode(id++, type, TileCenter(x, z), 100f));
        }
    }
}
