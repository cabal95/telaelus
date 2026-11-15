using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>
/// A processing recipe: consume Inputs, produce Outputs (one, by the plan's rule), over CycleTime
/// seconds. An input may have alternatives (Coal or Charcoal): <see cref="Alternatives"/> lists,
/// by input index, every type that can fill it in the same amount, the listed input's type first.
/// </summary>
public sealed class Recipe
{
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<ResourceType>> NoAlternatives =
        new Dictionary<int, IReadOnlyList<ResourceType>>();

    public IReadOnlyList<ResourceAmount> Inputs { get; }
    public IReadOnlyList<ResourceAmount> Outputs { get; }
    public float CycleTime { get; }

    /// <summary>The types that can fill an input, by input index; an input not listed takes only its own type.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<ResourceType>> Alternatives { get; init; } = NoAlternatives;

    public Recipe(IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs, float cycleTime)
    {
        Inputs = inputs;
        Outputs = outputs;
        CycleTime = cycleTime;
    }

    /// <summary>What it is called in a picker: its output's name.</summary>
    public string Name => ResourceDefs.For(Outputs[0].Type).Name;

    /// <summary>The types that can fill an input (its own type alone when it has no alternatives).</summary>
    public IReadOnlyList<ResourceType> OptionsFor(int index)
    {
        return Alternatives.TryGetValue(index, out IReadOnlyList<ResourceType>? options) ? options : new[] { Inputs[index].Type };
    }

    /// <summary>Whether any input can be filled by a type.</summary>
    public bool Uses(ResourceType type)
    {
        for (int i = 0; i < Inputs.Count; i++)
        {
            if (((IList<ResourceType>)OptionsFor(i)).Contains(type))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>An input with its alternatives as one name ("Coal or Charcoal").</summary>
    public string InputName(int index)
    {
        var names = new List<string>();
        foreach (ResourceType type in OptionsFor(index))
        {
            names.Add(ResourceDefs.For(type).Name);
        }
        return string.Join(" or ", names);
    }
}
