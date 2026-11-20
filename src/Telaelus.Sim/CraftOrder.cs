namespace Telaelus.Sim;

/// <summary>An entry in a processor's crafting queue: a recipe (by index) and the items still to make.</summary>
public sealed class CraftOrder
{
    public CraftOrder(int recipeIndex, int remaining)
    {
        RecipeIndex = recipeIndex;
        Remaining = remaining;
    }

    public int RecipeIndex { get; }

    /// <summary>Items still to make; always a whole number of the recipe's cycles.</summary>
    public int Remaining { get; set; }
}
