using System;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class LaterTiersTests
{
    /// <summary>
    /// The empty map (its HQ at fine x 56..61, z 2..11) with six Burner Generators on Coal (210
    /// Power), two Water Pumps on a pond at map cell (13, 4) (8 Water), and a Tank Farm for fluids.
    /// </summary>
    private static Simulation Plant(bool water = true)
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 180f);
        SimTestHelpers.Burners(sim, 6);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 28, 0, Facing.North);
        if (water)
        {
            sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
            sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 14, Facing.North);
            sim.Debug.PlaceBuilding(BuildingType.WaterPump, 54, 14, Facing.North);
        }
        return sim;
    }

    [Theory]
    [InlineData(BuildingType.Sawmill, "Lumber")]
    [InlineData(BuildingType.CementKiln, "Cement")]
    [InlineData(BuildingType.ConcreteMixer, "Concrete")]
    [InlineData(BuildingType.BrineWell, "Lithium")]
    [InlineData(BuildingType.SemiconductorFab, "Silicon Wafer,Processor,Solar Cell")]
    [InlineData(BuildingType.AirSeparator, "Liquid Oxygen")]
    [InlineData(BuildingType.EnrichmentPlant, "Enriched Uranium,Fuel Rod")]
    [InlineData(BuildingType.ChemicalPlant, "Plastic,Rubber,Carbon Fiber,Sulfuric Acid")]
    [InlineData(BuildingType.Fabricator, "Hull Plate,Steel Beam,Aluminum Frame,Machine Parts,Composite Panel,Magnet,Electric Motor,Armor Plate")]
    [InlineData(BuildingType.ElectronicsWorks, "Copper Wire,Circuit,Insulated Cable,Battery")]
    [InlineData(BuildingType.AssemblyPlant, "Control Module,Thruster Assembly,Fuel Tank,Computer,Heat Shield,Life Support Unit,Sensor Array")]
    public void EachBuilding_RunsItsRecipes(BuildingType type, string names)
    {
        Assert.Equal(names, string.Join(",", Recipes.For(type).Select(r => r.Name)));
    }

    [Fact]
    public void EveryCatalogItem_HasASource()
    {
        var made = Enum.GetValues<BuildingType>()
            .SelectMany(Recipes.For)
            .Select(r => r.Outputs[0].Type)
            .ToHashSet();
        var unmade = Enum.GetValues<ResourceType>()
            .Where(t => ResourceDefs.For(t).Kind == ResourceKind.Refined && !made.Contains(t))
            .ToList();
        Assert.Empty(unmade);
    }

    [Fact]
    public void ASiliconWaferCycle_MakesTwoWafers()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Silicon, 1f);
        Building fab = sim.Debug.PlaceBuilding(BuildingType.SemiconductorFab, 47, 4, Facing.North);
        SimTestHelpers.Order(sim, fab);
        Assert.True(fab.Online);

        SimTestHelpers.Seconds(sim, 10.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.SiliconWafer));
    }

    [Fact]
    public void TheBrineWell_StandsOnASaltFlat_AndPumpsLithium()
    {
        Simulation sim = Plant();
        // Map cells (12, 1) and (12, 2) are fine x 48..51, z 4..11. Desert Sand is not enough.
        sim.Debug.SetBiome(12, 1, BiomeType.Desert);
        sim.Debug.SetBiome(12, 2, BiomeType.Desert);
        sim.Debug.SetTerrain(12, 1, TerrainType.Sand);
        sim.Debug.SetTerrain(12, 2, TerrainType.Sand);
        Assert.False(sim.CanPlace(BuildingType.BrineWell, 48, 4, Facing.North));

        sim.Debug.SetTerrain(12, 1, TerrainType.SaltFlat);
        sim.Debug.SetTerrain(12, 2, TerrainType.SaltFlat);
        Assert.True(sim.CanPlace(BuildingType.BrineWell, 48, 4, Facing.North));
        // One column off the flat onto the grass beside it is refused.
        Assert.False(sim.CanPlace(BuildingType.BrineWell, 47, 4, Facing.North));

        // Lithium has no room at the HQ: it keeps in a Stockyard or Silo.
        Assert.True(sim.Debug.PlaceBuilding(BuildingType.Stockyard, 52, 4, Facing.North).Online);
        Building well = sim.Debug.PlaceBuilding(BuildingType.BrineWell, 48, 4, Facing.North);
        Assert.True(well.Online);
        SimTestHelpers.Seconds(sim, 20.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Lithium));
    }

    [Fact]
    public void TheAirSeparator_MakesLiquidOxygen_WithoutWater()
    {
        Simulation sim = Plant(water: false);
        Building separator = sim.Debug.PlaceBuilding(BuildingType.AirSeparator, 48, 4, Facing.North);
        Assert.True(separator.Online);

        SimTestHelpers.Seconds(sim, 10.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.LiquidOxygen));
        Assert.Equal(0f, separator.Def.Draw(CapacityType.Water));
    }

    [Fact]
    public void TheCementKiln_BurnsFuel_AndNeedsNoPower()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Limestone, 6f);
        Building kiln = sim.Debug.PlaceBuilding(BuildingType.CementKiln, 48, 4, Facing.North);
        SimTestHelpers.Order(sim, kiln);
        SimTestHelpers.Ticks(sim, 1);
        Assert.True(kiln.Online);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Waiting for fuel"), sim.BuildingStatusOf(kiln));

        // One Coal burns 40 s: three 12 s cycles finish.
        sim.Debug.SeedStockpile(ResourceType.Coal, 1f);
        SimTestHelpers.Seconds(sim, 37.0);

        Assert.Equal(3f, SimTestHelpers.Stock(sim, ResourceType.Cement));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Limestone));
    }

    [Fact]
    public void TheConcreteMixer_DrawsSandAndWater()
    {
        Simulation sim = Plant();
        sim.Debug.SeedStockpile(ResourceType.Cement, 1f);
        Building mixer = sim.Debug.PlaceBuilding(BuildingType.ConcreteMixer, 48, 4, Facing.North);
        SimTestHelpers.Order(sim, mixer);
        SimTestHelpers.Ticks(sim, 1);
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Blackout: Sand"), sim.BuildingStatusOf(mixer));

        sim.Debug.SetTerrain(12, 4, TerrainType.Sand);
        Building collector = sim.Debug.PlaceBuilding(BuildingType.SandCollector, 48, 16, Facing.North);
        Assert.True(collector.Online);
        SimTestHelpers.Seconds(sim, 10.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.Concrete));
    }

    /// <summary>
    /// From seeded materials to a Computer and a Fuel Rod: two Semiconductor Fabs (wafers, then
    /// processors), an Electronics Works on Insulated Cable, an Assembly Plant on Computers, and two
    /// Enrichment Plants (Enriched Uranium, then Fuel Rods), on Burner Generators and Water Pumps.
    /// </summary>
    [Fact]
    public void EndToEnd_SeededMaterialsReachAComputerAndAFuelRod()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        (ResourceType Type, float Amount)[] seeds =
        {
            (ResourceType.Silicon, 10f), (ResourceType.Circuit, 10f), (ResourceType.SulfuricAcid, 10f),
            (ResourceType.CopperWire, 10f), (ResourceType.Rubber, 5f), (ResourceType.UraniumOre, 10f),
            (ResourceType.Steel, 10f), (ResourceType.Coal, 500f)
        };
        foreach ((ResourceType type, float amount) in seeds)
        {
            sim.Debug.SeedStockpile(type, amount);
        }
        sim.Debug.SetTerrain(12, 6, TerrainType.ShallowWater);

        // 300 Power of makers on nine Burner Generators (315) along the north edge.
        SimTestHelpers.Burners(sim, 9);
        (BuildingType Type, int X, int Z, int Recipe)[] placements =
        {
            (BuildingType.SemiconductorFab, 8, 14, 0),
            (BuildingType.SemiconductorFab, 14, 14, 1),
            (BuildingType.ElectronicsWorks, 20, 14, 2),
            (BuildingType.AssemblyPlant, 8, 24, 3),
            (BuildingType.EnrichmentPlant, 15, 24, 0),
            (BuildingType.EnrichmentPlant, 22, 24, 1),
            (BuildingType.WaterPump, 46, 24, 0),
            (BuildingType.WaterPump, 46, 26, 0),
            (BuildingType.WaterPump, 48, 22, 0),
            (BuildingType.WaterPump, 50, 22, 0),
            (BuildingType.Vault, 34, 24, 0)
        };
        foreach ((BuildingType type, int x, int z, int recipe) in placements)
        {
            Building building = sim.Debug.PlaceBuilding(type, x, z, Facing.North);
            if (building.Queues)
            {
                SimTestHelpers.Order(sim, building, recipe);
            }
        }
        foreach ((int x, int z) in new[] { (47, 10), (37, 10), (27, 10), (17, 10), (30, 21), (30, 32), (20, 32), (12, 32), (47, 20) })
        {
            sim.Debug.PlaceBuilding(BuildingType.SmallHub, x, z, Facing.North);
        }
        Assert.All(SimTestHelpers.Placed(sim).Where(b => !b.Def.IsHub), b => Assert.True(b.Online, $"{b.Type} at ({b.AnchorX}, {b.AnchorZ}) is offline"));

        double seconds = 0;
        while ((SimTestHelpers.Stock(sim, ResourceType.Computer) < 1f || SimTestHelpers.Stock(sim, ResourceType.FuelRod) < 1f) && seconds < 600)
        {
            SimTestHelpers.Seconds(sim, 10.0);
            seconds += 10.0;
        }

        ResourceType[] parts =
        {
            ResourceType.SiliconWafer, ResourceType.Processor, ResourceType.InsulatedCable, ResourceType.Computer,
            ResourceType.EnrichedUranium, ResourceType.FuelRod
        };
        string state = string.Join(", ", parts.Select(p => $"{p} {SimTestHelpers.Stock(sim, p)}"))
            + $"; power {sim.Capacity(CapacityType.Power).State}; water {sim.Capacity(CapacityType.Water).State}; "
            + string.Join("; ", SimTestHelpers.Placed(sim).Where(b => b.Recipe is not null).Select(b => $"{b.Type} {b.Recipe!.Name} {sim.BuildingStatusOf(b).IdleReason}"));
        Assert.True(SimTestHelpers.Stock(sim, ResourceType.Computer) >= 1f, $"no Computer after {seconds} s: {state}");
        Assert.True(SimTestHelpers.Stock(sim, ResourceType.FuelRod) >= 1f, $"no Fuel Rod after {seconds} s: {state}");
    }
}
