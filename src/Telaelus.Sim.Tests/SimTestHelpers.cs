using Godot;

namespace Telaelus.Sim.Tests;

/// <summary>
/// Shared helpers and known coordinates from the fixed test map (see
/// Simulation.GenerateTerrain and SpawnInitialFields). Fine-cell coordinates are
/// map-cell coordinates times 4.
/// </summary>
internal static class SimTestHelpers
{
    public const double TickSeconds = 1.0 / 30.0;

    // Clear grass near the spawn (map cells 7 and 8 have no water, mountain, or field).
    public const int ClearX = 28;
    public const int ClearZ = 28;

    // A Water Pump spot on the west shore of the pond (which starts at fine x 36, z 20..27), in
    // the HQ's coverage.
    public const int PondPumpX = 34;
    public const int PondPumpZ = 22;

    /// <summary>Queues an order at a processor large enough to keep it crafting for the whole test.</summary>
    public static Building Order(Simulation sim, Building building, int recipe = 0, int count = 10000)
    {
        Assert.True(sim.QueueCraft(building.Id, recipe, count));
        return building;
    }

    /// <summary>Steps the sim for a number of fixed ticks with the current intent.</summary>
    public static void Ticks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(TickSeconds);
        }
    }

    /// <summary>Steps the sim for roughly the given number of seconds.</summary>
    public static void Seconds(Simulation sim, double seconds)
    {
        Ticks(sim, (int)System.Math.Ceiling(seconds / TickSeconds));
    }

    /// <summary>The field occupying a map cell (by its center fine cell).</summary>
    public static ResourceField FieldAtMapCell(Simulation sim, int mapX, int mapZ)
    {
        ResourceField? field = sim.FieldAt(mapX * Simulation.FineCellsPerMapCell, mapZ * Simulation.FineCellsPerMapCell);
        Assert.NotNull(field);
        return field;
    }

    /// <summary>What has been gathered or made of a resource: the stockpile plus every building's output buffer.</summary>
    public static float Gathered(Simulation sim, ResourceType type)
    {
        float total = Stock(sim, type);
        foreach (Building building in sim.Buildings)
        {
            building.Output.TryGetValue(type, out float buffered);
            total += buffered;
        }
        return total;
    }

    /// <summary>
    /// Brings a spot onto the HQ network: places Small Hubs (free, through the debug tools) in a
    /// chain from the HQ until one stands within 6 units of the point, so a building there is
    /// covered. Each hub goes on the nearest free cell to its step; call it after placing the
    /// buildings it serves.
    /// </summary>
    public static void Connect(Simulation sim, float x, float z)
    {
        Building last = System.Linq.Enumerable.First(sim.Buildings, b => b.Type == BuildingType.Hq);
        var target = new Vector3(x, 0f, z);
        while (last.Position.DistanceTo(target) > 6f)
        {
            Vector3 from = last.Position;
            Vector3 step = from + (target - from).Normalized() * Mathf.Min(8f, from.DistanceTo(target));
            last = PlaceHubNear(sim, step);
            ConnectHubs.GetOrCreateValue(sim).Add(last.Id);
        }
    }

    /// <summary>
    /// Places Burner Generators (4 x 4, 35 Power each) side by side westward from an anchor; by
    /// default along the empty map's north edge from (52, 0), where the HQ covers up to nine (x 20
    /// to 55, z 0 to 3).
    /// </summary>
    public static Building[] Burners(Simulation sim, int count, int x = 52, int z = 0)
    {
        var burners = new Building[count];
        for (int i = 0; i < count; i++)
        {
            burners[i] = sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, x - 4 * i, z, Facing.North);
        }
        return burners;
    }

    /// <summary>Places a building and connects it to the HQ's network (collectors work only online).</summary>
    public static Building PlaceOnline(Simulation sim, BuildingType type, int x, int z, Facing facing = Facing.North)
    {
        Building building = sim.Debug.PlaceBuilding(type, x, z, facing);
        if (!building.Online)
        {
            Connect(sim, building.Position.X, building.Position.Z);
        }
        return building;
    }

    private static Building PlaceHubNear(Simulation sim, Vector3 point)
    {
        (int cx, int cz) = Simulation.CellAt(point.X, point.Z);
        for (int ring = 0; ring <= 2; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (sim.CanPlace(BuildingType.SmallHub, cx + dx, cz + dz, Facing.North))
                    {
                        return sim.Debug.PlaceBuilding(BuildingType.SmallHub, cx + dx, cz + dz, Facing.North);
                    }
                }
            }
        }
        throw new System.InvalidOperationException($"no free cell for a hub near ({point.X}, {point.Z})");
    }

    /// <summary>Stands on a building and holds the harvest input until its output buffer is empty or storage is full.</summary>
    public static void CollectByHand(Simulation sim, Building building)
    {
        Teleport(sim, building.Position.X, building.Position.Z);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };
        // Step once first, so the interaction target is this building before checking it.
        sim.Step(TickSeconds);
        for (int i = 0; i < 30 * 60 && building.OutputCount > 0f && !sim.InteractBlocked; i++)
        {
            sim.Step(TickSeconds);
        }
        sim.Intent = default;
    }

    /// <summary>The HQ, which every world places at start.</summary>
    public static Building Hq(Simulation sim)
    {
        return System.Linq.Enumerable.Single(sim.Buildings, b => b.Type == BuildingType.Hq);
    }

    /// <summary>Every building but the HQ, which every world places at start.</summary>
    /// <summary>The Small Hubs <see cref="Connect"/> placed, per sim, which <see cref="Placed"/> leaves out.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Simulation, System.Collections.Generic.HashSet<int>> ConnectHubs = new();

    /// <summary>The Small Hubs <see cref="Connect"/> placed in a sim.</summary>
    public static System.Collections.Generic.List<Building> ConnectedHubs(Simulation sim)
    {
        System.Collections.Generic.HashSet<int> ids = ConnectHubs.GetOrCreateValue(sim);
        return System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(sim.Buildings, b => ids.Contains(b.Id)));
    }

    /// <summary>Every building but the HQ and the hubs <see cref="Connect"/> placed.</summary>
    public static System.Collections.Generic.List<Building> Placed(Simulation sim)
    {
        System.Collections.Generic.HashSet<int> hubs = ConnectHubs.GetOrCreateValue(sim);
        return System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(sim.Buildings, b => b.Type != BuildingType.Hq && !hubs.Contains(b.Id)));
    }

    public static float Stock(Simulation sim, ResourceType type)
    {
        sim.Stockpile.TryGetValue(type, out float amount);
        return amount;
    }

    public static void Teleport(Simulation sim, float x, float z)
    {
        sim.Player.Position = new Vector3(x, 0f, z);
    }
}
