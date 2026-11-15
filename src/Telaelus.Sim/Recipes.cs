using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>
/// The recipes each processing building can run, the first its default (a building runs one at a
/// time; see Simulation.SetRecipe). Every recipe has one output.
/// </summary>
public static class Recipes
{
    private static readonly IReadOnlyList<ResourceType> CoalOrCharcoal = new[]
    {
        ResourceType.Coal,
        ResourceType.Charcoal
    };

    private static Recipe Make(float cycle, ResourceType output, params (ResourceType Type, float Amount)[] inputs)
    {
        return MakeMany(cycle, output, 1f, inputs);
    }

    /// <summary>A recipe making more than one unit of its output a cycle.</summary>
    private static Recipe MakeMany(float cycle, ResourceType output, float makes, params (ResourceType Type, float Amount)[] inputs)
    {
        var list = new ResourceAmount[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            list[i] = new ResourceAmount(inputs[i].Type, inputs[i].Amount);
        }
        return new Recipe(list, new[] { new ResourceAmount(output, makes) }, cycle);
    }

    private static readonly Recipe Iron = Make(16f, ResourceType.Iron, (ResourceType.IronOre, 1f));
    private static readonly Recipe Copper = Make(16f, ResourceType.Copper, (ResourceType.CopperOre, 1f));
    private static readonly Recipe Kerosene = Make(8f, ResourceType.Kerosene, (ResourceType.CrudeOil, 1f));
    private static readonly Recipe Glass = Make(12f, ResourceType.Glass);
    private static readonly Recipe Charcoal = Make(15f, ResourceType.Charcoal, (ResourceType.Wood, 3f));

    private static readonly Recipe Steel = Make(20f, ResourceType.Steel,
        (ResourceType.Iron, 2f), (ResourceType.Coal, 1f), (ResourceType.Limestone, 1f));

    private static readonly Recipe Hydrogen = Make(6f, ResourceType.Hydrogen);
    private static readonly Recipe LiquidOxygen = Make(6f, ResourceType.LiquidOxygen);
    private static readonly Recipe RocketFuel = Make(8f, ResourceType.RocketFuel, (ResourceType.Kerosene, 1f), (ResourceType.Hydrogen, 1f));
    private static readonly Recipe Plastic = Make(10f, ResourceType.Plastic, (ResourceType.CrudeOil, 2f));
    private static readonly Recipe Rubber = Make(12f, ResourceType.Rubber, (ResourceType.CrudeOil, 1f), (ResourceType.Sulfur, 1f));
    private static readonly Recipe CarbonFiber = Make(15f, ResourceType.CarbonFiber, (ResourceType.Plastic, 2f));
    private static readonly Recipe Aluminum = Make(20f, ResourceType.Aluminum, (ResourceType.Bauxite, 2f));
    private static readonly Recipe Silicon = Make(15f, ResourceType.Silicon, (ResourceType.Quartz, 1f), (ResourceType.Coal, 1f));
    private static readonly Recipe Titanium = Make(20f, ResourceType.Titanium, (ResourceType.TitaniumOre, 2f));
    private static readonly Recipe Lumber = Make(8f, ResourceType.Lumber, (ResourceType.Wood, 2f));
    private static readonly Recipe Cement = Make(12f, ResourceType.Cement, (ResourceType.Limestone, 2f));
    private static readonly Recipe Concrete = MakeMany(10f, ResourceType.Concrete, 2f, (ResourceType.Cement, 1f));
    private static readonly Recipe SulfuricAcid = MakeMany(8f, ResourceType.SulfuricAcid, 2f, (ResourceType.Sulfur, 1f));
    private static readonly Recipe EnrichedUranium = Make(30f, ResourceType.EnrichedUranium,
        (ResourceType.UraniumOre, 2f), (ResourceType.SulfuricAcid, 1f));
    private static readonly Recipe Lithium = Make(10f, ResourceType.Lithium);
    private static readonly Recipe SeparatedOxygen = Make(5f, ResourceType.LiquidOxygen);

    // Tier 2: components.
    private static readonly Recipe HullPlate = Make(12f, ResourceType.HullPlate, (ResourceType.Steel, 2f));
    private static readonly Recipe SteelBeam = Make(15f, ResourceType.SteelBeam, (ResourceType.Steel, 3f));
    private static readonly Recipe AluminumFrame = Make(12f, ResourceType.AluminumFrame, (ResourceType.Aluminum, 2f));
    private static readonly Recipe MachineParts = Make(15f, ResourceType.MachineParts, (ResourceType.Steel, 2f), (ResourceType.Rubber, 1f));
    private static readonly Recipe CompositePanel = Make(20f, ResourceType.CompositePanel, (ResourceType.CarbonFiber, 2f), (ResourceType.Aluminum, 1f));
    private static readonly Recipe CopperWire = MakeMany(6f, ResourceType.CopperWire, 2f, (ResourceType.Copper, 1f));
    private static readonly Recipe Circuit = Make(10f, ResourceType.Circuit, (ResourceType.CopperWire, 2f), (ResourceType.Plastic, 1f));
    private static readonly Recipe CeramicTile = MakeMany(15f, ResourceType.CeramicTile, 2f, (ResourceType.Silicon, 1f), (ResourceType.Aluminum, 1f));
    private static readonly Recipe InsulatedCable = MakeMany(8f, ResourceType.InsulatedCable, 2f, (ResourceType.CopperWire, 2f), (ResourceType.Rubber, 1f));
    private static readonly Recipe Battery = Make(15f, ResourceType.Battery,
        (ResourceType.Lithium, 1f), (ResourceType.Copper, 1f), (ResourceType.Plastic, 1f));
    private static readonly Recipe SiliconWafer = MakeMany(10f, ResourceType.SiliconWafer, 2f, (ResourceType.Silicon, 1f));
    private static readonly Recipe Processor = Make(20f, ResourceType.Processor,
        (ResourceType.SiliconWafer, 2f), (ResourceType.Circuit, 1f), (ResourceType.SulfuricAcid, 1f));
    private static readonly Recipe SolarCell = Make(15f, ResourceType.SolarCell,
        (ResourceType.SiliconWafer, 1f), (ResourceType.Glass, 1f), (ResourceType.CopperWire, 2f));
    private static readonly Recipe Magnet = MakeMany(12f, ResourceType.Magnet, 2f, (ResourceType.RareEarths, 1f), (ResourceType.Iron, 1f));
    private static readonly Recipe ElectricMotor = Make(15f, ResourceType.ElectricMotor,
        (ResourceType.Steel, 1f), (ResourceType.CopperWire, 2f), (ResourceType.Magnet, 1f));
    private static readonly Recipe ArmorPlate = Make(15f, ResourceType.ArmorPlate, (ResourceType.Steel, 2f), (ResourceType.Titanium, 1f));

    // Tier 3: assemblies.
    private static readonly Recipe ControlModule = Make(40f, ResourceType.ControlModule,
        (ResourceType.Circuit, 4f), (ResourceType.HullPlate, 2f), (ResourceType.Glass, 2f));
    private static readonly Recipe ThrusterAssembly = Make(45f, ResourceType.ThrusterAssembly,
        (ResourceType.Titanium, 4f), (ResourceType.MachineParts, 2f), (ResourceType.CeramicTile, 6f));
    private static readonly Recipe FuelTank = Make(40f, ResourceType.FuelTank,
        (ResourceType.AluminumFrame, 4f), (ResourceType.CompositePanel, 4f));
    private static readonly Recipe Computer = Make(40f, ResourceType.Computer,
        (ResourceType.Processor, 2f), (ResourceType.Circuit, 2f), (ResourceType.InsulatedCable, 2f));
    private static readonly Recipe HeatShield = Make(40f, ResourceType.HeatShield,
        (ResourceType.CeramicTile, 6f), (ResourceType.CompositePanel, 2f));
    private static readonly Recipe LifeSupportUnit = Make(45f, ResourceType.LifeSupportUnit,
        (ResourceType.ElectricMotor, 2f), (ResourceType.MachineParts, 2f), (ResourceType.Glass, 2f));
    private static readonly Recipe SensorArray = Make(40f, ResourceType.SensorArray,
        (ResourceType.Processor, 1f), (ResourceType.Magnet, 2f), (ResourceType.Glass, 2f));
    private static readonly Recipe FuelRod = Make(40f, ResourceType.FuelRod, (ResourceType.EnrichedUranium, 2f), (ResourceType.Steel, 2f));

    // Tier 4: the ship.
    private static readonly Recipe HullSection = Make(60f, ResourceType.HullSection, (ResourceType.HullPlate, 4f), (ResourceType.SteelBeam, 2f));
    private static readonly Recipe Engine = Make(90f, ResourceType.Engine,
        (ResourceType.ThrusterAssembly, 2f), (ResourceType.FuelTank, 2f), (ResourceType.MachineParts, 4f));

    /// <summary>The recipes a building type runs, default first; empty for one that is not a processor.</summary>
    public static IReadOnlyList<Recipe> For(BuildingType type)
    {
        return type switch
        {
            BuildingType.Smelter => new[] { Iron, Copper },
            BuildingType.Refinery => new[] { Kerosene },
            BuildingType.Glassworks => new[] { Glass },
            BuildingType.CharcoalKiln => new[] { Charcoal },
            BuildingType.SteelMill => new[] { SteelWithFuel },
            BuildingType.Electrolyzer => new[] { Hydrogen, LiquidOxygen },
            BuildingType.FuelPlant => new[] { RocketFuel },
            BuildingType.ChemicalPlant => new[] { Plastic, Rubber, CarbonFiber, SulfuricAcid },
            BuildingType.AluminumSmelter => new[] { Aluminum },
            BuildingType.ElectricFurnace => new[] { SiliconWithFuel, Titanium },
            BuildingType.Fabricator => new[] { HullPlate, SteelBeam, AluminumFrame, MachineParts, CompositePanel, Magnet, ElectricMotor, ArmorPlate },
            BuildingType.ElectronicsWorks => new[] { CopperWire, Circuit, InsulatedCable, Battery },
            BuildingType.TileKiln => new[] { CeramicTile },
            BuildingType.AssemblyPlant => new[] { ControlModule, ThrusterAssembly, FuelTank, Computer, HeatShield, LifeSupportUnit, SensorArray },
            BuildingType.Shipyard => new[] { HullSection, Engine },
            BuildingType.Sawmill => new[] { Lumber },
            BuildingType.CementKiln => new[] { Cement },
            BuildingType.ConcreteMixer => new[] { Concrete },
            BuildingType.BrineWell => new[] { Lithium },
            BuildingType.SemiconductorFab => new[] { SiliconWafer, Processor, SolarCell },
            BuildingType.AirSeparator => new[] { SeparatedOxygen },
            BuildingType.EnrichmentPlant => new[] { EnrichedUranium, FuelRod },
            _ => Array.Empty<Recipe>()
        };
    }

    // Steel and Silicon take Coal or Charcoal for their carbon (input 1).
    private static readonly Recipe SteelWithFuel = new(Steel.Inputs, Steel.Outputs, Steel.CycleTime)
    {
        Alternatives = new Dictionary<int, IReadOnlyList<ResourceType>>
        {
            [1] = CoalOrCharcoal
        }
    };

    private static readonly Recipe SiliconWithFuel = new(Silicon.Inputs, Silicon.Outputs, Silicon.CycleTime)
    {
        Alternatives = new Dictionary<int, IReadOnlyList<ResourceType>>
        {
            [1] = CoalOrCharcoal
        }
    };
}
