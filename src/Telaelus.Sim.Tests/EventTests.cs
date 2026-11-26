using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class EventTests
{
    private static (Simulation Sim, List<SimEvent> Events) Recorded()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        var events = new List<SimEvent>();
        sim.Happened += events.Add;
        return (sim, events);
    }

    private static List<T> Of<T>(List<SimEvent> events, SimEventKind kind, System.Func<SimEvent, T> pick)
    {
        return events.Where(e => e.Kind == kind).Select(pick).ToList();
    }

    [Fact]
    public void Discovering_RaisesOncePerResource()
    {
        (Simulation sim, List<SimEvent> events) = Recorded();

        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);

        Assert.Equal(new[] { ResourceType.Coal }, Of(events, SimEventKind.Discovered, e => e.Resource));
    }

    [Fact]
    public void CompletingResearch_RaisesOnce()
    {
        (Simulation sim, List<SimEvent> events) = Recorded();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 48, 4, Facing.North);
        foreach (ResourceAmount cost in ResearchDefs.For(ResearchProject.MaterialTransfer).Fee)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        Assert.True(sim.StartResearch(ResearchProject.MaterialTransfer));

        SimTestHelpers.Seconds(sim, 2 * Simulation.DayLength - 1.0);
        Assert.Empty(Of(events, SimEventKind.ResearchComplete, e => e.Project));
        SimTestHelpers.Seconds(sim, 10.0);

        Assert.Equal(new[] { ResearchProject.MaterialTransfer }, Of(events, SimEventKind.ResearchComplete, e => e.Project));
    }

    [Fact]
    public void ABlackout_RaisesOnce_AndAgainOnlyAfterItIsBackToNormal()
    {
        (Simulation sim, List<SimEvent> events) = Recorded();
        sim.Debug.SetTerrain(12, 4, TerrainType.ShallowWater);
        Building pump = sim.Debug.PlaceBuilding(BuildingType.WaterPump, 48, 14, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Refinery, 46, 8, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
        // The Refinery's Power has no generator, so only Water is watched here.
        List<CapacityType> Water() => Of(events, SimEventKind.Blackout, e => e.Capacity).Where(c => c == CapacityType.Water).ToList();
        Assert.Empty(Water());

        sim.SetBuildingEnabled(pump.Id, false);
        SimTestHelpers.Ticks(sim, 10);
        Assert.Single(Water());

        sim.SetBuildingEnabled(pump.Id, true);
        SimTestHelpers.Ticks(sim, 1);
        sim.SetBuildingEnabled(pump.Id, false);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(2, Water().Count);
        Assert.Single(events, e => e.Kind == SimEventKind.Blackout && e.Capacity == CapacityType.Power);
    }

    [Fact]
    public void LaunchingAShip_Raises()
    {
        (Simulation sim, List<SimEvent> events) = Recorded();
        foreach (ResourceAmount cost in Simulation.LaunchCost)
        {
            sim.Debug.SeedStockpile(cost.Type, cost.Amount);
        }
        sim.Debug.PlaceBuilding(BuildingType.Shipyard, 44, 12, Facing.North);

        Assert.True(sim.TryLaunchShip());

        Assert.Single(events, e => e.Kind == SimEventKind.ShipLaunched);
    }

    [Fact]
    public void ANewWorldOrALoad_RaisesNothing()
    {
        (Simulation sim, List<SimEvent> events) = Recorded();
        sim.Debug.SeedStockpile(ResourceType.Wood, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);
        SaveData save = sim.Save();
        events.Clear();

        sim.Debug.ResetToEmptyMap();
        sim.Debug.ResetToGeneratedWorld(1234);
        sim.Load(save);

        Assert.Empty(events);
        Assert.True(sim.IsUnlocked(BuildingType.Mine));
    }
}
