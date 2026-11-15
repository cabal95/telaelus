using System.Collections.Generic;

namespace Game.Sim;

/// <summary>
/// The recipe controls: a processor runs one of its recipes at a time (`SetRecipe` takes effect
/// between cycles, the running cycle finishing first), and an input with alternatives (Coal or
/// Charcoal) is filled by the chosen one, or on Auto by whichever is more at hand. Inputs the
/// running recipe cannot use go back to storage while the building is online.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>
    /// The type that fills a recipe input at a building: its own when it has no alternatives, the
    /// chosen one, or on Auto the alternative most at hand (in the input buffer, plus storage when
    /// online), the first on a tie.
    /// </summary>
    public ResourceType ChosenInput(Building building, Recipe recipe, int index)
    {
        IReadOnlyList<ResourceType> options = recipe.OptionsFor(index);
        if (options.Count == 1)
        {
            return options[0];
        }
        if (building.InputChoices.TryGetValue(index, out ResourceType chosen))
        {
            return chosen;
        }
        ResourceType best = options[0];
        float most = float.MinValue;
        foreach (ResourceType option in options)
        {
            float have = AtHand(building, option);
            if (have > most)
            {
                most = have;
                best = option;
            }
        }
        return best;
    }

    /// <summary>How much of a type a building has at hand: its input buffer, plus storage when online.</summary>
    public float AtHand(Building building, ResourceType type)
    {
        _stockpile.TryGetValue(type, out float stock);
        return Held(building.Input, type) + (building.Online ? stock : 0f);
    }

    /// <summary>A recipe's inputs as this building would take them, each alternative resolved.</summary>
    public List<ResourceAmount> ResolvedInputs(Building building, Recipe recipe)
    {
        var inputs = new List<ResourceAmount>(recipe.Inputs.Count);
        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            inputs.Add(new ResourceAmount(ChosenInput(building, recipe, i), recipe.Inputs[i].Amount));
        }
        return inputs;
    }

    /// <summary>
    /// Chooses a processor's recipe by index: at once between cycles, else when the running cycle
    /// finishes. False for a building without that recipe.
    /// </summary>
    public bool SetRecipe(int id, int index)
    {
        Building? building = BuildingById(id);
        if (building is null || index < 0 || index >= building.Recipes.Count)
        {
            return false;
        }
        if (building.Crafting)
        {
            building.PendingRecipe = index == building.RecipeIndex ? null : index;
            return true;
        }
        ApplyRecipe(building, index);
        return true;
    }

    /// <summary>Sets an alternative input's choice: one of its types, or null for Auto. False when the input has no such alternative.</summary>
    public bool SetInputChoice(int id, int inputIndex, ResourceType? type)
    {
        Building? building = BuildingById(id);
        if (building?.Recipe is not Recipe recipe || inputIndex < 0 || inputIndex >= recipe.Inputs.Count
            || recipe.OptionsFor(inputIndex).Count < 2)
        {
            return false;
        }
        if (type is ResourceType chosen)
        {
            if (!((IList<ResourceType>)recipe.OptionsFor(inputIndex)).Contains(chosen))
            {
                return false;
            }
            building.InputChoices[inputIndex] = chosen;
        }
        else
        {
            building.InputChoices.Remove(inputIndex);
        }
        return true;
    }

    private void ApplyRecipe(Building building, int index)
    {
        building.PendingRecipe = null;
        if (index == building.RecipeIndex)
        {
            return;
        }
        building.RecipeIndex = index;
        building.InputChoices.Clear();
        ReturnStaleInputs(building);
    }

    /// <summary>Takes up a recipe chosen during the cycle that just finished.</summary>
    private void TakeUpPendingRecipe(Building building)
    {
        if (building.PendingRecipe is int pending)
        {
            ApplyRecipe(building, pending);
        }
    }

    /// <summary>Sends what the input buffer holds that the running recipe cannot use back to storage, when online.</summary>
    private void ReturnStaleInputs(Building building)
    {
        if (!building.Online || building.Recipe is not Recipe recipe || building.Input.Count == 0)
        {
            return;
        }
        var stale = new List<ResourceType>();
        foreach (ResourceType type in building.Input.Keys)
        {
            if (!recipe.Uses(type))
            {
                stale.Add(type);
            }
        }
        foreach (ResourceType type in stale)
        {
            AddToStockpile(type, building.Input[type]);
            building.Input.Remove(type);
        }
    }
}
