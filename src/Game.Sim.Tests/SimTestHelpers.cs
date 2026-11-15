using Godot;

namespace Game.Sim.Tests;

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
        }
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
    public static System.Collections.Generic.List<Building> Placed(Simulation sim)
    {
        return System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(sim.Buildings, b => b.Type != BuildingType.Hq));
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
