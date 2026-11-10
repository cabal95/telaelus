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
    // Grid definition. Buildings will later snap to this; for now it defines the
    // walkable extent and drives the rendered ground. The grid is centered on
    // the world origin.
    public int GridWidth { get; } = 32;
    public int GridHeight { get; } = 32;
    public float TileSize { get; } = 1f;

    public Player Player { get; } = new();

    /// <summary>Input for the current tick, set by the presentation before Step.</summary>
    public PlayerIntent Intent;

    private readonly List<ResourceNode> _nodes = new();
    public IReadOnlyList<ResourceNode> Nodes => _nodes;

    private readonly Dictionary<ResourceType, float> _stockpile = new();
    public IReadOnlyDictionary<ResourceType, float> Stockpile => _stockpile;

    /// <summary>The node currently in range to harvest, or null.</summary>
    public ResourceNode? HarvestTarget { get; private set; }

    private const float HarvestRange = 1.5f;
    private const float HarvestRate = 10f; // units per second

    public float WorldSizeX => GridWidth * TileSize;
    public float WorldSizeZ => GridHeight * TileSize;

    public Simulation()
    {
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
    }

    private void MovePlayer(double dt)
    {
        Vector2 move = Intent.Move;
        if (move.LengthSquared() > 1f)
        {
            move = move.Normalized();
        }

        Vector3 p = Player.Position + new Vector3(move.X, 0f, move.Y) * Player.Speed * (float)dt;

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
            _nodes.Remove(node);
            HarvestTarget = null;
        }
    }

    private void AddToStockpile(ResourceType type, float amount)
    {
        _stockpile.TryGetValue(type, out float current);
        _stockpile[type] = current + amount;
    }

    private void SpawnInitialNodes()
    {
        // A few deterministic clusters; procedural generation comes later.
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
            var position = new Vector3(x * TileSize, 0f, z * TileSize);
            _nodes.Add(new ResourceNode(id++, type, position, 100f));
        }
    }
}
