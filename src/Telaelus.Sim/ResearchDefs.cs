using System;
using System.Collections.Generic;

namespace Telaelus.Sim;

/// <summary>A research project. One runs at a time for the whole base (see Simulation.StartResearch).</summary>
public enum ResearchProject
{
    MaterialTransfer,
    AdvancedMaterialTransfer,
    SteelWorks,
    Electronics,
    Computers
}

/// <summary>Where a project stands for the player.</summary>
public enum ResearchState
{
    /// <summary>Its tier is not unlocked, or a prerequisite is not complete.</summary>
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
/// row), its tier (it can start once that tier is unlocked), the tier it unlocks if it is a tier's
/// unlock project, and for a transfer project the raw resources the network then carries from
/// collectors.
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

    /// <summary>The tier it belongs to, 1 to 4; it is locked while that tier is.</summary>
    public int Tier { get; init; } = 1;

    /// <summary>The building tier completing it unlocks, or null for an ordinary project.</summary>
    public int? Unlocks { get; init; }
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
            Tier = 2,
            Transfers = new[]
            {
                ResourceType.CrudeOil,
                ResourceType.Bauxite,
                ResourceType.Limestone,
                ResourceType.Quartz,
                ResourceType.Sulfur
            }
        },
        [ResearchProject.SteelWorks] = new ResearchDef(
            "Steel Works",
            "Unlocks Tier 2 buildings: steel, oil, and the chemistry they open up.",
            1200f,
            new ResourceAmount[]
            {
                new(ResourceType.Iron, 60f),
                new(ResourceType.Copper, 60f),
                new(ResourceType.Glass, 40f),
                new(ResourceType.Wood, 80f)
            },
            Array.Empty<ResearchProject>(),
            0,
            1)
        {
            Unlocks = 2
        },
        [ResearchProject.Electronics] = new ResearchDef(
            "Electronics",
            "Unlocks Tier 3 buildings: components, circuits, and motors.",
            1800f,
            new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Plastic, 60f),
                new(ResourceType.Aluminum, 40f),
                new(ResourceType.Wood, 120f)
            },
            new[]
            {
                ResearchProject.SteelWorks
            },
            1,
            1)
        {
            Tier = 2,
            Unlocks = 3
        },
        [ResearchProject.Computers] = new ResearchDef(
            "Computers",
            "Unlocks Tier 4 buildings: assemblies, reactors, and the Shipyard.",
            2700f,
            new ResourceAmount[]
            {
                new(ResourceType.Circuit, 60f),
                new(ResourceType.ElectricMotor, 20f),
                new(ResourceType.Steel, 40f),
                new(ResourceType.Wood, 200f)
            },
            new[]
            {
                ResearchProject.Electronics
            },
            2,
            1)
        {
            Tier = 3,
            Unlocks = 4
        }
    };

    public static ResearchDef For(ResearchProject project)
    {
        return Table[project];
    }
}
