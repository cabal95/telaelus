using System.Linq;
using Xunit.Abstractions;

namespace Telaelus.Sim.Tests;

/// <summary>
/// The truck balance as a measure that can be run again: Stone Mines (one unit every 6 s) in a
/// column on the empty map with a Depot beside the HQ and Stockyards for room, hauled for ten
/// minutes after a minute's warm-up.
/// </summary>
public class TruckBalanceTests
{
    private readonly ITestOutputHelper _output;

    public TruckBalanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>What share of what the Mines made the trucks delivered, and how far out the Mines stand.</summary>
    private (float Share, float Distance) Haul(int mines, int trucks, int mineX)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        foreach (int ax in new[] { 44, 47, 50, 53 })
        {
            sim.Debug.PlaceBuilding(BuildingType.Stockyard, ax, 14, Facing.North);
        }
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, 50, 2, Facing.South);
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);
        sim.Debug.SeedStockpile(ResourceType.Wood, 100f);
        for (int truck = 1; truck < trucks; truck++)
        {
            Assert.True(sim.TryBuyTruck(depot.Id));
        }
        for (int i = 0; i < mines; i++)
        {
            int z = 4 + i * 7;
            sim.Debug.PlaceField(ResourceType.Stone, mineX / 4 - 1, z / 4, 5000f);
            SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, mineX, z);
        }
        SimTestHelpers.Seconds(sim, 60.0);
        float before = SimTestHelpers.Stock(sim, ResourceType.Stone);
        float buffered = sim.Buildings.Where(b => b.Type == BuildingType.Mine).Sum(b => b.OutputCount);
        SimTestHelpers.Seconds(sim, 600.0);
        float delivered = SimTestHelpers.Stock(sim, ResourceType.Stone) - before;
        float made = delivered + sim.Buildings.Where(b => b.Type == BuildingType.Mine).Sum(b => b.OutputCount) - buffered
            + sim.Trucks.Sum(t => t.Amount);
        Building hq = sim.Buildings.First(b => b.Type == BuildingType.Hq);
        float distance = sim.Buildings.First(b => b.Type == BuildingType.Mine).Position.DistanceTo(hq.Position);
        float share = delivered / (mines * 600f / 6f);
        _output.WriteLine($"{mines} Mines ~{distance:0} out, {trucks} truck(s): delivered {delivered / 600f:0.00}/s, {share:P0} of a full rate (made {made / 600f:0.00}/s)");
        return (share, distance);
    }

    [Fact]
    public void OneTruck_KeepsUpWithThreeMinesFortyOut()
    {
        (float share, float distance) = Haul(3, 1, 20);

        Assert.InRange(distance, 35f, 42f);
        Assert.True(share >= 0.95f, $"delivered {share:P0}");
    }

    [Fact]
    public void OneTruck_FallsBehindFourMinesFortyOut()
    {
        (float share, _) = Haul(4, 1, 20);

        Assert.True(share <= 0.9f, $"delivered {share:P0}");
    }

    [Fact]
    public void TwoTrucks_NearlyKeepUpWithSixMinesFortyOut()
    {
        (float share, _) = Haul(6, 2, 20);

        // 92% since trucks unload on the HQ's pad: the HQ in this map's corner faces its pad away
        // from the Mines, so each trip drives round to its front.
        Assert.True(share >= 0.9f, $"delivered {share:P0}");
    }
}
