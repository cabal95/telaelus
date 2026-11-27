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
    Computers,
    Cement,
    Steelmaking,
    ElectricSmelting,
    PoweredMining,
    PoweredLogging,
    PoweredPumping,
    SandSifting,
    OilRigs,
    DeepMining,
    AdvancedLogging,
    AdvancedPumping,
    Dredging,
    DeepDrilling,
    TileFiring,
    BetterMining1,
    BetterLogging1,
    BetterSmelting1,
    BetterMining2,
    BetterLogging2,
    BetterSmelting2,
    BetterDrilling1,
    BetterDrilling2
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
/// its fee (paid once, to begin), the projects it needs first, its place in its tier's tree (a
/// column and row within the tier), its tier (it can start once that tier is unlocked), the tier it unlocks if it is a tier's
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

    /// <summary>The building level completing it unlocks (a leveled family above level 1), or null.</summary>
    public (BuildingType Type, int Level)? UnlocksLevel { get; init; }

    /// <summary>
    /// The family it speeds up and its research part (0.25 or 0.5), or null. A family's part is its
    /// largest complete project's, never a sum.
    /// </summary>
    public (BuildingType Family, float Bonus)? Speeds { get; init; }
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
            0,
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
            0,
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
            0,
            1)
        {
            Tier = 3,
            Unlocks = 4
        },
        [ResearchProject.Cement] = LevelProject("Cement", BuildingType.Kiln, 2, 600f, new ResourceAmount[]
            {
                new(ResourceType.Iron, 30f),
                new(ResourceType.Stone, 40f),
                new(ResourceType.Wood, 40f)
            },
            Array.Empty<ResearchProject>(),
            0,
            8),
        [ResearchProject.Steelmaking] = LevelProject("Steelmaking", BuildingType.Smelter, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Iron, 60f),
                new(ResourceType.Copper, 30f),
                new(ResourceType.Stone, 40f)
            },
            Array.Empty<ResearchProject>(),
            1,
            7),
        [ResearchProject.ElectricSmelting] = LevelProject("Electric Smelting", BuildingType.Smelter, 3, 1200f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Copper, 30f),
                new(ResourceType.Glass, 20f)
            },
            new[]
            {
                ResearchProject.Steelmaking
            },
            2,
            7),
        [ResearchProject.PoweredMining] = LevelProject("Powered Mining", BuildingType.Mine, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.Copper, 30f)
            },
            Array.Empty<ResearchProject>(),
            1,
            2),
        [ResearchProject.PoweredLogging] = LevelProject("Powered Logging", BuildingType.LumberCamp, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 30f),
                new(ResourceType.Wood, 40f)
            },
            Array.Empty<ResearchProject>(),
            1,
            3),
        [ResearchProject.PoweredPumping] = LevelProject("Powered Pumping", BuildingType.WaterPump, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 30f),
                new(ResourceType.Copper, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            4),
        [ResearchProject.SandSifting] = LevelProject("Sand Sifting", BuildingType.SandCollector, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 30f),
                new(ResourceType.Glass, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            5),
        [ResearchProject.OilRigs] = LevelProject("Oil Rigs", BuildingType.OilWell, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.Copper, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            6),
        [ResearchProject.DeepMining] = LevelProject("Deep Mining", BuildingType.Mine, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Concrete, 30f),
                new(ResourceType.ElectricMotor, 10f)
            },
            new[]
            {
                ResearchProject.PoweredMining
            },
            0,
            2),
        [ResearchProject.AdvancedLogging] = LevelProject("Advanced Logging", BuildingType.LumberCamp, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.ElectricMotor, 20f)
            },
            new[]
            {
                ResearchProject.PoweredLogging
            },
            0,
            3),
        [ResearchProject.AdvancedPumping] = LevelProject("Advanced Pumping", BuildingType.WaterPump, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.Concrete, 20f),
                new(ResourceType.ElectricMotor, 10f)
            },
            new[]
            {
                ResearchProject.PoweredPumping
            },
            0,
            4),
        [ResearchProject.Dredging] = LevelProject("Dredging", BuildingType.SandCollector, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.ElectricMotor, 10f)
            },
            new[]
            {
                ResearchProject.SandSifting
            },
            0,
            5),
        [ResearchProject.DeepDrilling] = LevelProject("Deep Drilling", BuildingType.OilWell, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Concrete, 20f),
                new(ResourceType.ElectricMotor, 10f)
            },
            new[]
            {
                ResearchProject.OilRigs
            },
            0,
            6),
        [ResearchProject.TileFiring] = LevelProject("Tile Firing", BuildingType.Kiln, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Concrete, 40f),
                new(ResourceType.Silicon, 20f),
                new(ResourceType.Steel, 20f)
            },
            new[]
            {
                ResearchProject.Cement
            },
            0,
            8),
        [ResearchProject.BetterMining1] = SpeedProject("Better Mining I", BuildingType.Mine, "Mines", 0.25f, 1, 600f, new ResourceAmount[]
            {
                new(ResourceType.Iron, 40f),
                new(ResourceType.Copper, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            2),
        [ResearchProject.BetterLogging1] = SpeedProject("Better Logging I", BuildingType.LumberCamp, "Lumber Camps", 0.25f, 1, 600f, new ResourceAmount[]
            {
                new(ResourceType.Iron, 30f),
                new(ResourceType.Wood, 40f)
            },
            Array.Empty<ResearchProject>(),
            0,
            3),
        [ResearchProject.BetterSmelting1] = SpeedProject("Better Smelting I", BuildingType.Smelter, "Smelters", 0.25f, 1, 600f, new ResourceAmount[]
            {
                new(ResourceType.Iron, 40f),
                new(ResourceType.Glass, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            7),
        [ResearchProject.BetterMining2] = SpeedProject("Better Mining II", BuildingType.Mine, "Mines", 0.5f, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Concrete, 20f)
            },
            new[]
            {
                ResearchProject.BetterMining1
            },
            0,
            2),
        [ResearchProject.BetterLogging2] = SpeedProject("Better Logging II", BuildingType.LumberCamp, "Lumber Camps", 0.5f, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.Lumber, 40f)
            },
            new[]
            {
                ResearchProject.BetterLogging1
            },
            0,
            3),
        [ResearchProject.BetterSmelting2] = SpeedProject("Better Smelting II", BuildingType.Smelter, "Smelters", 0.5f, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Copper, 30f)
            },
            new[]
            {
                ResearchProject.BetterSmelting1
            },
            0,
            7),
        [ResearchProject.BetterDrilling1] = SpeedProject("Better Drilling I", BuildingType.OilWell, "Oil Wells", 0.25f, 2, 900f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 40f),
                new(ResourceType.Copper, 20f)
            },
            Array.Empty<ResearchProject>(),
            0,
            9),
        [ResearchProject.BetterDrilling2] = SpeedProject("Better Drilling II", BuildingType.OilWell, "Oil Wells", 0.5f, 3, 1500f, new ResourceAmount[]
            {
                new(ResourceType.Steel, 60f),
                new(ResourceType.Concrete, 20f),
                new(ResourceType.ElectricMotor, 10f)
            },
            new[]
            {
                ResearchProject.BetterDrilling1
            },
            0,
            9)
    };

    // A project that unlocks a level: in that level's tier, described by what the level is.
    private static ResearchDef LevelProject(string name, BuildingType type, int level, float work, IReadOnlyList<ResourceAmount> fee,
        IReadOnlyList<ResearchProject> needs, int column, int row)
    {
        BuildingDef def = BuildingDefs.For(type, level);
        string description = $"The {BuildingDefs.For(type).Name} can be built or upgraded to a {def.Name}. {BuildingDefs.DescriptionOf(type, level)}";
        return new ResearchDef(name, description, work, fee, needs, column, row)
        {
            Tier = def.Tier,
            UnlocksLevel = (type, level)
        };
    }

    // A project that speeds up a family at every level; a second step replaces the first's part.
    private static ResearchDef SpeedProject(string name, BuildingType family, string plural, float bonus, int tier, float work,
        IReadOnlyList<ResourceAmount> fee, IReadOnlyList<ResearchProject> needs, int column, int row)
    {
        string percent = $"{bonus * 100f:0}%";
        string description = needs.Count == 0
            ? $"{plural} work {percent} faster, at every level{(family == BuildingType.Smelter ? " and on every recipe" : "")}."
            : $"{plural} work {percent} faster, at every level{(family == BuildingType.Smelter ? " and on every recipe" : "")}, in place of {name[..^1]}'s 25%.";
        return new ResearchDef(name, description, work, fee, needs, column, row)
        {
            Tier = tier,
            Speeds = (family, bonus)
        };
    }

    public static ResearchDef For(ResearchProject project)
    {
        return Table[project];
    }

    /// <summary>The project that unlocks a type's level, or null (level 1, or a type without levels).</summary>
    public static ResearchProject? UnlockingLevel(BuildingType type, int level)
    {
        foreach (ResearchProject project in All)
        {
            if (Table[project].UnlocksLevel == (type, level))
            {
                return project;
            }
        }
        return null;
    }
}
