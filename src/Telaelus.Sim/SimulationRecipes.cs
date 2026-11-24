using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>
/// The recipe controls. A queueing processor crafts its orders in turn (`QueueCraft`, `CancelCraft`)
/// and idles when they run out; a running one (the makers from nothing) runs the recipe chosen with
/// `SetRecipe`, which takes effect between cycles. An input with alternatives (Coal or Charcoal) is
/// filled by the choice made for that recipe, or on Auto by whichever is more at hand. Inputs no
/// recipe in the queue (or the running recipe) can use go back to storage while online.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The most orders a crafting queue holds.</summary>
    public const int MaxQueue = 4;

    /// <summary>
    /// Adds an order of a recipe to a queueing processor: the count in items, rounded up to whole
    /// cycles. It grows the last order when that is the same recipe, else goes after the others.
    /// False for a building that does not queue, a recipe it lacks, a count under 1, or a full queue.
    /// </summary>
    public bool QueueCraft(int id, int recipeIndex, int count)
    {
        Building? building = BuildingById(id);
        if (building is null || !building.Queues || recipeIndex < 0 || recipeIndex >= building.Recipes.Count || count < 1)
        {
            return false;
        }
        int items = WholeCycles(building.Recipes[recipeIndex], count);
        if (building.Queue.Count > 0 && building.Queue[^1].RecipeIndex == recipeIndex)
        {
            building.Queue[^1].Remaining += items;
            return true;
        }
        if (building.Queue.Count >= MaxQueue)
        {
            return false;
        }
        building.Queue.Add(new CraftOrder(recipeIndex, items));
        FollowQueue(building);
        return true;
    }

    /// <summary>
    /// Removes an order. Removing the first while its cycle runs stops the cycle and returns what it
    /// took to storage (past limits, as a refund). False when there is no such order.
    /// </summary>
    public bool CancelCraft(int id, int index)
    {
        Building? building = BuildingById(id);
        if (building is null || index < 0 || index >= building.Queue.Count)
        {
            return false;
        }
        if (index == 0 && building.Crafting)
        {
            foreach (ResourceAmount input in building.CycleInputs)
            {
                AddToStockpile(input.Type, input.Amount);
            }
            building.CycleInputs.Clear();
            building.Crafting = false;
            building.Progress = 0f;
        }
        building.Queue.RemoveAt(index);
        FollowQueue(building);
        return true;
    }

    /// <summary>The items a recipe makes in the fewest whole cycles that make at least a count.</summary>
    public static int WholeCycles(Recipe recipe, int count)
    {
        int perCycle = Math.Max(1, (int)recipe.Outputs[0].Amount);
        return (count + perCycle - 1) / perCycle * perCycle;
    }

    /// <summary>Points a queueing processor between cycles at its first order's recipe.</summary>
    private static void FollowQueue(Building building)
    {
        if (building.Queues && !building.Crafting && building.Queue.Count > 0)
        {
            building.RecipeIndex = building.Queue[0].RecipeIndex;
        }
    }

    /// <summary>Counts a finished cycle against the first order, removing it when done.</summary>
    private static void CountFinishedCycle(Building building, Recipe recipe)
    {
        if (!building.Queues || building.Queue.Count == 0)
        {
            return;
        }
        CraftOrder order = building.Queue[0];
        order.Remaining -= (int)recipe.Outputs[0].Amount;
        if (order.Remaining <= 0)
        {
            building.Queue.RemoveAt(0);
        }
        FollowQueue(building);
    }

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
        if (building.InputChoices.TryGetValue((IndexOf(building, recipe), index), out ResourceType chosen))
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

    /// <summary>Whether an input of a recipe at a building has a choice made (not Auto).</summary>
    public static bool HasInputChoice(Building building, Recipe recipe, int index)
    {
        return building.InputChoices.ContainsKey((IndexOf(building, recipe), index));
    }

    private static int IndexOf(Building building, Recipe recipe)
    {
        for (int i = 0; i < building.Recipes.Count; i++)
        {
            if (ReferenceEquals(building.Recipes[i], recipe))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>How much of a type a building has at hand: what storage holds when it is online, else none.</summary>
    public float AtHand(Building building, ResourceType type)
    {
        _stockpile.TryGetValue(type, out float stock);
        return building.Online ? stock : 0f;
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
    /// Chooses a running processor's recipe by index: at once between cycles, else when the running
    /// cycle finishes. False for a building without that recipe or one that queues (its orders choose).
    /// </summary>
    public bool SetRecipe(int id, int index)
    {
        Building? building = BuildingById(id);
        if (building is null || building.Queues || index < 0 || index >= building.Recipes.Count)
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

    /// <summary>
    /// Sets an alternative input's choice for one of a building's recipes: one of its types, or null
    /// for Auto. False when the recipe's input has no such alternative.
    /// </summary>
    public bool SetInputChoice(int id, int recipeIndex, int inputIndex, ResourceType? type)
    {
        Building? building = BuildingById(id);
        if (building is null || recipeIndex < 0 || recipeIndex >= building.Recipes.Count)
        {
            return false;
        }
        Recipe recipe = building.Recipes[recipeIndex];
        if (inputIndex < 0 || inputIndex >= recipe.Inputs.Count || recipe.OptionsFor(inputIndex).Count < 2)
        {
            return false;
        }
        if (type is ResourceType chosen)
        {
            if (!((IList<ResourceType>)recipe.OptionsFor(inputIndex)).Contains(chosen))
            {
                return false;
            }
            building.InputChoices[(recipeIndex, inputIndex)] = chosen;
        }
        else
        {
            building.InputChoices.Remove((recipeIndex, inputIndex));
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
    }

    /// <summary>Takes up a recipe chosen during the cycle that just finished.</summary>
    private void TakeUpPendingRecipe(Building building)
    {
        if (building.PendingRecipe is int pending)
        {
            ApplyRecipe(building, pending);
        }
    }
}
