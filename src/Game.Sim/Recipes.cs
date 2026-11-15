using System;
using System.Collections.Generic;

namespace Game.Sim;

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
        var list = new ResourceAmount[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            list[i] = new ResourceAmount(inputs[i].Type, inputs[i].Amount);
        }
        return new Recipe(list, new[] { new ResourceAmount(output, 1f) }, cycle);
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
            BuildingType.ChemicalPlant => new[] { Plastic, Rubber, CarbonFiber },
            BuildingType.AluminumSmelter => new[] { Aluminum },
            BuildingType.ElectricFurnace => new[] { SiliconWithFuel, Titanium },
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
