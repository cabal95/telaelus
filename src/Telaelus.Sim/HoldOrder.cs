namespace Telaelus.Sim;

/// <summary>
/// A queueing processor's hold (see Simulation.SetHold): the recipe (by index) it runs, and the
/// stock of that recipe's output it keeps storage at, in items.
/// </summary>
public sealed class HoldOrder
{
    public HoldOrder(int recipeIndex, int target)
    {
        RecipeIndex = recipeIndex;
        Target = target;
    }

    public int RecipeIndex { get; }

    /// <summary>The items of the output storage should hold; it makes more while it holds fewer.</summary>
    public int Target { get; }
}
