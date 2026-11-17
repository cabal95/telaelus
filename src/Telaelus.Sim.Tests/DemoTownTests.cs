using System.Linq;

namespace Telaelus.Sim.Tests;

public class DemoTownTests
{
    private static Simulation Town()
    {
        var sim = new Simulation(WorldSettings.Generated(DemoTown.Seed));
        DemoTown.Build(sim);
        DemoTown.Run(sim, DemoTown.RunSeconds);
        return sim;
    }

    [Fact]
    public void EveryBuildingIsOnlineAndWorkingWhenSaved()
    {
        Simulation sim = Town();

        Assert.All(sim.Buildings, b => Assert.True(b.Online, $"{b.Type} at ({b.AnchorX}, {b.AnchorZ}) is offline"));
        Assert.All(sim.Buildings.Where(b => b.Type != BuildingType.Depot), b =>
            Assert.Equal(BuildingState.Working, sim.BuildingStatusOf(b).State));
        Assert.Equal(CapacityState.Normal, sim.Capacity(CapacityType.Power).State);
        Assert.Equal(4, sim.Trucks.Count);
        Assert.Equal(ResearchProject.MaterialTransfer, sim.ActiveResearch);
    }

    [Fact]
    public void ItsCollectorsHaveFieldsAndItsSmeltersAreFed()
    {
        Simulation sim = Town();

        Assert.All(sim.Buildings.Where(b => b.Def.Works.Count > 0), b => Assert.NotNull(b.CurrentField));
        // The Smelters' ore came by truck: the 20 Iron Ore it started with lasts four Smelters 80 s.
        Assert.True(sim.Stockpile[ResourceType.Iron] > 60f, $"Iron {sim.Stockpile[ResourceType.Iron]}");
        Assert.True(sim.Stockpile[ResourceType.Copper] > 25f, $"Copper {sim.Stockpile[ResourceType.Copper]}");
        Assert.True(sim.Stockpile[ResourceType.Charcoal] > 10f, $"Charcoal {sim.Stockpile[ResourceType.Charcoal]}");
    }

    [Fact]
    public void ItKeepsRunningForAQuarterHourAfterLoading()
    {
        Simulation sim = Town();

        DemoTown.Run(sim, 900.0);

        Assert.DoesNotContain(sim.Trucks, t => t.State == TruckState.WaitingForRoom);
        Assert.All(sim.Buildings.Where(b => b.Type == BuildingType.Smelter), b =>
            Assert.Equal(BuildingState.Working, sim.BuildingStatusOf(b).State));
    }

    [Fact]
    public void ItsBuildingsAreUnlockedAsAPlayerWouldHaveThem()
    {
        Simulation sim = Town();

        Assert.All(sim.Buildings.Where(b => b.Def.Buildable).Select(b => b.Type).Distinct(), type =>
            Assert.True(sim.IsUnlocked(type), $"{type} is locked"));
    }
}
