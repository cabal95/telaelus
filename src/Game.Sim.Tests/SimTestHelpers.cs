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
