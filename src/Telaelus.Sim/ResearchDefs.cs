using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>A research project. One runs at a time for the whole base (see Simulation.StartResearch).</summary>
public enum ResearchProject
{
    MaterialTransfer,
    AdvancedMaterialTransfer
}

/// <summary>Where a project stands for the player.</summary>
public enum ResearchState
{
    /// <summary>A prerequisite is not complete.</summary>
    Locked,

    /// <summary>Never started: its fee (if any) is still to pay.</summary>
    Available,

    /// <summary>The active project.</summary>
    Running,

    /// <summary>Started before (its fee paid or some work done) and not running now.</summary>
    Paused,

    Complete
}

/// <summary>
/// A project's definition: its name and what it does, its work in game seconds at one lab's speed,
/// its fee (paid once, to begin), the projects it needs first, its place in the tree (a column and
/// row), and for a transfer project the raw resources the network then carries from collectors.
/// </summary>
public sealed record ResearchDef(
    string Name,
    string Description,
    float Work,
    IReadOnlyList<ResourceAmount> Fee,
    IReadOnlyList<ResearchProject> Prerequisites,
    int Column,
    int Row)
{
    public IReadOnlyList<ResourceType> Transfers { get; init; } = Array.Empty<ResourceType>();
}

public static class ResearchDefs
{
    public static readonly IReadOnlyList<ResearchProject> All = Enum.GetValues<ResearchProject>();

    private static readonly Dictionary<ResearchProject, ResearchDef> Table = new()
    {
        [ResearchProject.MaterialTransfer] = new ResearchDef(
            "Material Transfer",
            "The network carries Wood, Stone, Iron Ore, Copper Ore, and Coal from online collectors straight into storage.",
            1200f,
            new ResourceAmount[]
            {
                new(ResourceType.Iron, 40f),
                new(ResourceType.Copper, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            0)
        {
            Transfers = new[]
            {
                ResourceType.Wood,
                ResourceType.Stone,
                ResourceType.IronOre,
                ResourceType.CopperOre,
                ResourceType.Coal
            }
        },
        [ResearchProject.AdvancedMaterialTransfer] = new ResearchDef(
            "Advanced Material Transfer",
            "The network carries more raw resources from online collectors straight into storage.",
            1800f,
            new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Glass, 20f)
            },
            new[]
            {
                ResearchProject.MaterialTransfer
            },
            1,
            0)
        {
            Transfers = new[]
            {
                ResourceType.CrudeOil,
                ResourceType.Bauxite,
                ResourceType.Limestone,
                ResourceType.Quartz,
                ResourceType.Sulfur
            }
        }
    };

    public static ResearchDef For(ResearchProject project)
    {
        return Table[project];
    }
}
