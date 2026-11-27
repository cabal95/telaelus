using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class RenewablesTests
{
    /// <summary>
    /// The empty map (its HQ at fine x 56..61, z 2..11 covers this corner) with a Burner Generator
    /// on Coal (100 Power) and a pond at map cell (13, 4) with two Water Pumps on its north shore
    /// (8 Water), all online.
    /// </summary>
    private static Simulation Powered()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 60f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 8, Facing.North);
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 12, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 56, 16, Facing.North);
        return sim;
    }

    [Fact]
    public void ADepletedField_LeavesAWorkedOutRecord_ButWoodDoesNot()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.PlaceField(ResourceType.IronOre, 5, 5, 1f);
        sim.Debug.PlaceField(ResourceType.Wood, 8, 5, 1f);
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 24, 20);
        SimTestHelpers.PlaceOnline(sim, BuildingType.LumberCamp, 36, 20);

        SimTestHelpers.Seconds(sim, 12.0);

        Assert.Empty(sim.Fields);
        Assert.Equal(ResourceType.IronOre, sim.WorkedOut[(5, 5)]);
        Assert.False(sim.WorkedOut.ContainsKey((8, 5)));
        // A worked-out cell blocks nothing.
        Assert.True(sim.IsWalkable(21, 21));
        Assert.True(sim.CanPlace(BuildingType.Silo, 20, 20, Facing.North));
    }

    [Fact]
    public void AForester_GrowsItsField_ThenRegrowsItAfterItIsCut()
    {
        Simulation sim = Powered();
        Assert.True(sim.CanPlace(BuildingType.Forester, 48, 12, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Forester, 49, 12, Facing.North));
        Building forester = sim.Debug.PlaceBuilding(BuildingType.Forester, 48, 12, Facing.North);
        SimTestHelpers.PlaceOnline(sim, BuildingType.LumberCamp, 44, 16);
        SimTestHelpers.Ticks(sim, 1);

        ResourceField plot = Assert.IsType<ResourceField>(forester.Plot);
        Assert.Same(plot, sim.FieldAt(50, 14));
        Assert.True(plot.Growing);
        Assert.Equal(150f, plot.InitialAmount);
        Assert.True(forester.Online);

        // Nothing works it while it grows.
        SimTestHelpers.Seconds(sim, 170.0);
        Assert.True(plot.Growing);
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.Wood));

        SimTestHelpers.Seconds(sim, 20.0);
        Assert.False(plot.Growing);
        Assert.True(SimTestHelpers.Gathered(sim, ResourceType.Wood) >= 1f);

        // Cut down, it stands bare for 30 s, then plants again.
        sim.Debug.SetFieldAmount(12, 3, 0f);
        Assert.Null(forester.Plot);
        Assert.False(sim.WorkedOut.ContainsKey((12, 3)));
        SimTestHelpers.Seconds(sim, 29.0);
        Assert.Null(forester.Plot);
        SimTestHelpers.Seconds(sim, 1.5);
        Assert.True(forester.Plot is { Growing: true });

        // Destroyed, it takes its field with it.
        Assert.True(sim.TryDestroyBuilding(forester.Id));
        Assert.Null(sim.FieldAt(50, 14));
    }

    [Fact]
    public void AForester_GrowsOnlyWithItsWater()
    {
        Simulation sim = Powered();
        Building forester = sim.Debug.PlaceBuilding(BuildingType.Forester, 48, 12, Facing.North);
        SimTestHelpers.Seconds(sim, 10.0);
        float left = forester.Plot!.GrowLeft;
        Assert.InRange(left, 169f, 171f);

        // Without its pumps it is in a Water blackout and its trees stop.
        sim.Debug.RemoveBuilding(52, 12);
        sim.Debug.RemoveBuilding(56, 16);
        SimTestHelpers.Seconds(sim, 10.0);
        Assert.Equal(CapacityState.Blackout, sim.Capacity(CapacityType.Water).State);
        Assert.Equal(left, forester.Plot!.GrowLeft, 2);
    }

    [Fact]
    public void AQuarry_GoesBesideAMountain_AndYieldsStoneForever_OrLimestoneInTheHighlands()
    {
        Simulation sim = Powered();
        // A mountain at map cell (11, 2): fine cells x 44..47, z 8..11. The generator is moved off it.
        sim.Debug.RemoveBuilding(44, 8);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 52, 8, Facing.North);
        sim.Debug.SetFeature(11, 2, MapFeature.Mountain);
        Assert.True(sim.CanPlace(BuildingType.Quarry, 48, 8, Facing.North));
        Assert.False(sim.CanPlace(BuildingType.Quarry, 49, 8, Facing.North));

        Building quarry = sim.Debug.PlaceBuilding(BuildingType.Quarry, 48, 8, Facing.North);
        Assert.Equal(new[] { ResourceType.Stone }, sim.OutputChoices(quarry));
        Assert.False(sim.SetOutputChoice(quarry.Id, ResourceType.Limestone));

        // Six seconds a unit.
        SimTestHelpers.Seconds(sim, 12.1);
        Assert.Equal(2f, quarry.Output[ResourceType.Stone]);

        sim.Debug.SetBiome(11, 2, BiomeType.Highlands);
        Assert.Equal(new[] { ResourceType.Stone, ResourceType.Limestone }, sim.OutputChoices(quarry));
        Assert.True(sim.SetOutputChoice(quarry.Id, ResourceType.Limestone));
        SimTestHelpers.Seconds(sim, 6.1);
        Assert.Equal(1f, quarry.Output[ResourceType.Limestone]);

        // Trucks serve it like a Mine.
        quarry.Output[ResourceType.Stone] = 12f;
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, 44, 14, Facing.North);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(quarry, sim.TrucksOf(depot)[0].Target);
    }

    [Fact]
    public void TheCharcoalKiln_TurnsThreeWoodIntoOneCharcoal()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 6f);
        SimTestHelpers.Order(sim, sim.Debug.PlaceBuilding(BuildingType.Kiln, 46, 14, Facing.North));

        SimTestHelpers.Seconds(sim, 30.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Charcoal));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void TheRockCrusher_SuppliesSand_WhileItHasStone()
    {
        Simulation sim = Powered();
        sim.Debug.SeedStockpile(ResourceType.Stone, 2f);
        Building crusher = sim.Debug.PlaceBuilding(BuildingType.RockCrusher, 48, 8, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(4f, sim.Capacity(CapacityType.Sand).Supply);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Stone));

        // One Stone each 10 s: the second goes at 10 s, and at 20 s there is none.
        SimTestHelpers.Seconds(sim, 20.5);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Stone));
        Assert.Equal(0f, sim.Capacity(CapacityType.Sand).Supply);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for Stone"), sim.BuildingStatusOf(crusher));
    }

    [Fact]
    public void DeepLevels_WorkAWorkedOutCellTheyBorder()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        // Worked-out Crude Oil at map cell (5, 5), fine x 20..23, z 20..23; Iron Ore at (7, 5), x 28..31.
        sim.Debug.SetWorkedOut(5, 5, ResourceType.CrudeOil);
        sim.Debug.SetWorkedOut(7, 5, ResourceType.IronOre);

        // Beside the oil cell (its north edge), a Deep Well works it; a Deep Mine there finds nothing it digs.
        Building well = sim.Debug.PlaceBuilding(BuildingType.OilWell, 20, 24, Facing.North, 3);
        Assert.Equal((null, (ResourceType?)ResourceType.CrudeOil), sim.DeepSiteAround(well));
        // East of the Iron Ore cell, a Deep Mine works it; two cells further east it borders nothing.
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 32, 20, Facing.North, 3);
        Assert.Equal((null, (ResourceType?)ResourceType.IronOre), sim.DeepSiteAround(mine));
        Building far = sim.Debug.PlaceBuilding(BuildingType.Mine, 40, 20, Facing.North, 3);
        Assert.Null(sim.DeepSiteAround(far));
        // A Drill Rig is not deep.
        Building rig = sim.Debug.PlaceBuilding(BuildingType.Mine, 28, 24, Facing.North, 2);
        Assert.Null(sim.DeepSiteAround(rig));

        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(ResourceType.CrudeOil, sim.OutputOf(well));
        Assert.Equal(80f / 3f, sim.UnitSeconds(well), 3);
        Assert.Equal(ResourceType.IronOre, sim.OutputOf(mine));
        Assert.Equal(20f, sim.UnitSeconds(mine));
    }

    [Fact]
    public void FourDeepMines_AroundOneWorkedOutCell_AllProduce()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 200f);
        SimTestHelpers.Burners(sim, 4);
        // Worked-out Iron Ore at map cell (7, 5): fine x 28..31, z 20..23; one Deep Mine on each edge.
        sim.Debug.SetWorkedOut(7, 5, ResourceType.IronOre);
        Building[] mines =
        {
            SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 24, 20, Facing.North, 3),
            SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 32, 20, Facing.North, 3),
            SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 28, 16, Facing.North, 3),
            SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 28, 24, Facing.North, 3)
        };

        SimTestHelpers.Seconds(sim, 20.5);

        Assert.All(mines, mine => Assert.Equal(1f, mine.Output.GetValueOrDefault(ResourceType.IronOre)));
    }

    [Fact]
    public void ADeepMine_WorksALiveFieldFirst_ThenTheWorkedOutCellBesideIt()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 200f);
        SimTestHelpers.Burners(sim, 2);
        sim.Debug.SetWorkedOut(7, 5, ResourceType.IronOre);
        ResourceField copper = sim.Debug.PlaceField(ResourceType.CopperOre, 9, 5, 1f);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 32, 20, Facing.North, 3);

        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(copper, mine.CurrentField);
        Assert.Null(mine.SiteDeposit);

        // The one unit of Copper Ore (6 s at speed 1.6), then on to the worked-out Iron Ore.
        SimTestHelpers.Seconds(sim, 4.5);
        Assert.Equal(1f, mine.Output.GetValueOrDefault(ResourceType.CopperOre));
        SimTestHelpers.Seconds(sim, 1.0);
        Assert.Equal(ResourceType.IronOre, mine.SiteDeposit);
        Assert.Equal(new BuildingStatus(BuildingState.Working, null), sim.BuildingStatusOf(mine));
    }

    [Fact]
    public void ADeepMineBesideAMotherlode_YieldsOneChosenResource_AtItsShare()
    {
        Simulation sim = Powered();
        // A Laterite at map cells (12..13, 2..3): fine x 48..55, z 8..15. The Deep Mine stands west of
        // it, beside the Burner Generator at (44, 8).
        sim.Debug.RemoveBuilding(52, 12);
        sim.Debug.RemoveBuilding(56, 16);
        ResourceField lode = sim.Debug.PlaceMotherlode(MotherlodeType.Laterite, 12, 2);
        Assert.False(lode.Workable);
        Assert.False(sim.CanPlace(BuildingType.Mine, 48, 8, Facing.North));
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 44, 12, Facing.North, 3);
        Assert.True(mine.Online);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Same(lode, mine.SiteMotherlode);
        Assert.Equal(new[] { ResourceType.IronOre, ResourceType.Bauxite }, sim.OutputChoices(mine));
        Assert.Equal(5f, sim.UnitSeconds(mine));
        Assert.True(sim.SetOutputChoice(mine.Id, ResourceType.Bauxite));
        Assert.Equal(7.5f, sim.UnitSeconds(mine));

        SimTestHelpers.Seconds(sim, 15.1);
        Assert.Equal(2f, mine.Output[ResourceType.Bauxite]);
        Assert.False(mine.Output.ContainsKey(ResourceType.IronOre));
        Assert.Contains(lode, sim.Fields);

        // The player collects it from beside the building.
        sim.Debug.TeleportPlayer(43.5f, 14f);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Same(mine, sim.InteractTarget);
    }

    [Fact]
    public void ADeepWellBesideASaltDome_PumpsCrudeOil()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        // A Salt Dome at map cells (5..6, 5..6): fine x 20..27, z 20..27; the Deep Well stands east of it.
        ResourceField dome = sim.Debug.PlaceMotherlode(MotherlodeType.SaltDome, 5, 5);
        Building well = sim.Debug.PlaceBuilding(BuildingType.OilWell, 28, 22, Facing.North, 3);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 22, 28, Facing.North, 3);

        Assert.Equal(((ResourceField?)dome, (ResourceType?)null), sim.DeepSiteAround(well));
        // A Salt Dome is the Oil Well family's, not the Mine's.
        Assert.Null(sim.DeepSiteAround(mine));
    }

    [Fact]
    public void AMotherlode_IsNotWorkedByAMineADrillRigOrHand()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.PlaceMotherlode(MotherlodeType.Laterite, 5, 5);
        Building mine = SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 14, 20);
        sim.Debug.TeleportPlayer(19.5f, 28.5f);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Equal(new BuildingStatus(BuildingState.Idle, "No fields in range"), sim.BuildingStatusOf(mine));
        Assert.Null(sim.HarvestTarget);
        Assert.Contains(ResourceType.Bauxite, Enumerable.Where(System.Enum.GetValues<ResourceType>(), sim.IsDiscovered));
    }

    [Fact]
    public void ATruck_HaulsFromADeepMineBesideAMotherlode()
    {
        Simulation sim = Powered();
        sim.Debug.RemoveBuilding(52, 12);
        sim.Debug.RemoveBuilding(56, 16);
        sim.Debug.PlaceMotherlode(MotherlodeType.Laterite, 12, 2);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 44, 12, Facing.North, 3);
        mine.Output[ResourceType.IronOre] = 12f;
        Building depot = sim.Debug.PlaceBuilding(BuildingType.Depot, 38, 12, Facing.North);

        SimTestHelpers.Seconds(sim, 30.0);

        Assert.True(SimTestHelpers.Stock(sim, ResourceType.IronOre) >= 12f);
    }
}
