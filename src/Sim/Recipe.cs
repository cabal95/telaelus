using System.Collections.Generic;

namespace Game.Sim;

/// <summary>A processing recipe: consume Inputs, produce Outputs, over CycleTime seconds.</summary>
public sealed class Recipe
{
    public IReadOnlyList<ResourceAmount> Inputs { get; }
    public IReadOnlyList<ResourceAmount> Outputs { get; }
    public float CycleTime { get; }

    public Recipe(IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs, float cycleTime)
    {
        Inputs = inputs;
        Outputs = outputs;
        CycleTime = cycleTime;
    }
}
