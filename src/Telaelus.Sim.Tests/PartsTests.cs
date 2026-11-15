using System.Linq;

namespace Telaelus.Sim.Tests;

public class PartsTests
{
    [Theory]
    [InlineData(BuildingType.Fabricator, "Hull Plate,Steel Beam,Aluminum Frame,Machine Parts,Composite Panel,Magnet,Electric Motor,Armor Plate")]
    [InlineData(BuildingType.ElectronicsWorks, "Copper Wire,Circuit,Insulated Cable,Battery")]
    [InlineData(BuildingType.TileKiln, "Ceramic Tile")]
    [InlineData(BuildingType.AssemblyPlant, "Control Module,Thruster Assembly,Fuel Tank,Computer,Heat Shield,Life Support Unit,Sensor Array")]
    [InlineData(BuildingType.Shipyard, "Hull Section,Engine")]
    public void EachPartsBuilding_RunsItsRecipes(BuildingType type, string names)
    {
        Assert.Equal(names, string.Join(",", Recipes.For(type).Select(r => r.Name)));
    }

    [Fact]
    public void SomeRecipes_MakeTwoUnits()
    {
        Assert.Equal(2f, Recipes.For(BuildingType.ElectronicsWorks)[0].Outputs[0].Amount);
        Assert.Equal(2f, Recipes.For(BuildingType.TileKiln)[0].Outputs[0].Amount);
        Assert.Equal(1f, Recipes.For(BuildingType.Fabricator)[0].Outputs[0].Amount);
    }

    [Fact]
    public void ACopperWireCycle_PutsTwoWiresInStorage()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 20f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        sim.Debug.SetTerrain(13, 4, TerrainType.ShallowWater);
        sim.Debug.PlaceBuilding(BuildingType.WaterPump, 52, 14, Facing.North);
        sim.Debug.SeedStockpile(ResourceType.Copper, 1f);
        Building works = sim.Debug.PlaceBuilding(BuildingType.ElectronicsWorks, 48, 4, Facing.North);
        Assert.True(works.Online);

        SimTestHelpers.Seconds(sim, 6.2);

        Assert.Equal(2f, SimTestHelpers.Stock(sim, ResourceType.CopperWire));
    }

    /// <summary>
    /// From tier-1 materials to a launch through every part: five Fabricators (one per part), two
    /// Electronics Works (wire, then circuits), a Tile Kiln, three Assembly Plants, and two
    /// Shipyards (hull sections and the engine), on Burner Generators, with Warehouses for the parts.
    /// The fuel and oxygen are seeded; refining has its own end-to-end test.
    /// </summary>
    [Fact]
    public void EndToEnd_TierOneMaterialsReachALaunch_ThroughTheParts()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        (ResourceType Type, float Amount)[] seeds =
        {
            (ResourceType.Steel, 250f), (ResourceType.Aluminum, 150f), (ResourceType.CarbonFiber, 50f),
            (ResourceType.Rubber, 25f), (ResourceType.Copper, 15f), (ResourceType.Plastic, 15f),
            (ResourceType.Glass, 10f), (ResourceType.Silicon, 50f), (ResourceType.Titanium, 15f),
            (ResourceType.RocketFuel, 500f), (ResourceType.LiquidOxygen, 200f), (ResourceType.Coal, 500f)
        };
        foreach ((ResourceType type, float amount) in seeds)
        {
            sim.Debug.SeedStockpile(type, amount);
        }
        sim.Debug.SetTerrain(12, 6, TerrainType.ShallowWater);

        (BuildingType Type, int X, int Z, int Recipe)[] placements =
        {
            (BuildingType.Fabricator, 8, 14, 0),
            (BuildingType.Fabricator, 13, 14, 1),
            (BuildingType.Fabricator, 18, 14, 2),
            (BuildingType.Fabricator, 23, 14, 3),
            (BuildingType.Fabricator, 28, 14, 4),
            (BuildingType.ElectronicsWorks, 33, 14, 0),
            (BuildingType.ElectronicsWorks, 33, 19, 1),
            (BuildingType.TileKiln, 38, 14, 0),
            (BuildingType.AssemblyPlant, 8, 24, 0),
            (BuildingType.AssemblyPlant, 15, 24, 1),
            (BuildingType.AssemblyPlant, 22, 24, 2),
            (BuildingType.Shipyard, 8, 34, 0),
            (BuildingType.Shipyard, 17, 34, 1),
            (BuildingType.BurnerGenerator, 42, 14, 0),
            (BuildingType.BurnerGenerator, 44, 14, 0),
            (BuildingType.BurnerGenerator, 46, 14, 0),
            (BuildingType.BurnerGenerator, 42, 17, 0),
            (BuildingType.BurnerGenerator, 44, 17, 0),
            (BuildingType.WaterPump, 46, 24, 0),
            (BuildingType.Warehouse, 30, 24, 0),
            (BuildingType.Warehouse, 34, 24, 0),
            (BuildingType.Warehouse, 38, 24, 0),
            (BuildingType.Warehouse, 30, 28, 0),
            (BuildingType.Warehouse, 34, 28, 0)
        };
        foreach ((BuildingType type, int x, int z, int recipe) in placements)
        {
            Building building = sim.Debug.PlaceBuilding(type, x, z, Facing.North);
            if (recipe > 0)
            {
                Assert.True(sim.SetRecipe(building.Id, recipe));
            }
        }
        // Small Hubs from the HQ along the open ground between the rows.
        foreach ((int x, int z) in new[] { (47, 10), (37, 10), (27, 10), (17, 10), (30, 21), (30, 32), (20, 32), (12, 32), (47, 20) })
        {
            sim.Debug.PlaceBuilding(BuildingType.SmallHub, x, z, Facing.North);
        }
        Assert.All(SimTestHelpers.Placed(sim).Where(b => !b.Def.IsHub), b => Assert.True(b.Online, $"{b.Type} at ({b.AnchorX}, {b.AnchorZ}) is offline"));

        double seconds = 0;
        while (!sim.CanLaunchShip() && seconds < 1200)
        {
            SimTestHelpers.Seconds(sim, 10.0);
            seconds += 10.0;
        }

        ResourceType[] parts =
        {
            ResourceType.HullPlate, ResourceType.SteelBeam, ResourceType.AluminumFrame, ResourceType.MachineParts,
            ResourceType.CompositePanel, ResourceType.CopperWire, ResourceType.Circuit, ResourceType.CeramicTile,
            ResourceType.ControlModule, ResourceType.ThrusterAssembly, ResourceType.FuelTank, ResourceType.HullSection,
            ResourceType.Engine, ResourceType.Steel, ResourceType.Aluminum
        };
        Assert.True(sim.TryLaunchShip(), $"no launch after {seconds} s: "
            + string.Join(", ", parts.Select(p => $"{p} {SimTestHelpers.Stock(sim, p)}"))
            + $"; cargo {sim.CargoUsed} / {sim.CargoPool}; power {sim.Capacity(CapacityType.Power).State}; "
            + string.Join("; ", SimTestHelpers.Placed(sim).Where(b => b.Recipe is not null).Select(b => $"{b.Type} {b.Recipe!.Name} {sim.BuildingStatusOf(b).IdleReason}")));
        Assert.Equal(1, sim.ShipsLaunched);
    }
}
