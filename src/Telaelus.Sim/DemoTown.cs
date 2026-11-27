using System;
using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim;

/// <summary>
/// The demo save's town: a working mining town on a generated world, laid out around the HQ and run
/// a few game minutes so it is busy when loaded. Its fields are placed where the layout needs them
/// (the world's own fields near the spawn are cleared first), and its buildings are paid for and
/// placed the way a player places them, so they unlock as they would in play.
/// </summary>
public static class DemoTown
{
    /// <summary>The world the town is built on.</summary>
    public const int Seed = 20261116;

    /// <summary>Game seconds the town runs before it is saved.</summary>
    public const double RunSeconds = 300.0;

    /// <summary>The fixed step the run uses (the game's own).</summary>
    public const double StepSeconds = 1.0 / 30.0;

    // The time of day the run starts at: early morning, so the save lands near midday.
    private const float StartTimeOfDay = 0.95f;

    // How far from the spawn (in map cells) the world's own fields are cleared.
    private const int ClearRadius = 12;

    // The square (in map cells from the spawn) the player walks to explore, and the walk's spacing.
    private const int ExploreRadius = 16;
    private const int ExploreStep = 4;

    /// <summary>Each field: its type and map cell relative to the spawn's.</summary>
    private static readonly (ResourceType Type, int Dx, int Dz)[] FieldLayout =
    {
        (ResourceType.CopperOre, -8, -4),
        (ResourceType.Coal, -8, 4),
        (ResourceType.Wood, 7, -3),
        (ResourceType.Wood, 7, -1),
        (ResourceType.Wood, 7, 2),
        (ResourceType.Stone, 7, 4),
        (ResourceType.IronOre, 2, 8)
    };

    private const float FieldAmount = 2000f;

    // Declared before BuildingLayout, which reads it: static fields initialize in order.
    /// <summary>The map cell (relative to the spawn's) of the Mine the demo upgrades to a Drill Rig.</summary>
    private static readonly (int Dx, int Dz) DrillRigCell = (1, 7);

    /// <summary>
    /// Each building in placing order: its type, the map cell relative to the spawn's, the fine-cell
    /// offset of its anchor inside that cell, and its facing.
    /// </summary>
    private static readonly (BuildingType Type, int Dx, int Dz, int Ox, int Oz, Facing Facing)[] BuildingLayout =
    {
        // The network first, so everything after it is placed online.
        // West to the Iron Ore Mines.
        (BuildingType.SmallHub, -3, -1, 2, 2, Facing.North),
        (BuildingType.SmallHub, -6, -2, 3, 2, Facing.North),
        (BuildingType.SmallHub, -8, -3, 2, 2, Facing.North),
        // South-west to the Copper Ore and Coal Mines.
        (BuildingType.SmallHub, -5, 1, 2, 0, Facing.North),
        (BuildingType.SmallHub, -7, 3, 3, 0, Facing.North),
        // South to the Smelters and the Drill Rig.
        (BuildingType.SmallHub, -1, 1, 2, 2, Facing.North),
        (BuildingType.SmallHub, -2, 3, 2, 2, Facing.North),
        (BuildingType.SmallHub, 0, 5, 2, 2, Facing.North),
        // East to the Lumber Camps, the Stone Mine, and the Charcoal Kiln.
        (BuildingType.SmallHub, 4, -1, 0, 2, Facing.North),
        (BuildingType.SmallHub, 5, -1, 2, 2, Facing.North),
        (BuildingType.SmallHub, 5, 2, 2, 1, Facing.North),
        // Storage: room for the Smelters' Iron and Copper (the Cargo pool) for well over half an hour.
        (BuildingType.Stockyard, 3, -3, 0, 0, Facing.North),
        (BuildingType.Warehouse, 3, -1, 0, 0, Facing.North),
        (BuildingType.Warehouse, 3, 1, 0, 0, Facing.North),
        (BuildingType.Warehouse, -5, -1, 0, 0, Facing.North),
        // Mining, matched to what the town uses so the trucks never wait on full storage: the Drill
        // Rig's and a Mine's Iron Ore feed four Smelters, the Copper Ore Mine one, the Coal Mine and
        // the Charcoal Kiln fuel them and the generator, and the Lumber Camp feeds the Kiln.
        (BuildingType.LumberCamp, 6, -2, 0, 0, Facing.North),
        (BuildingType.Mine, -7, -3, 0, 0, Facing.North),
        (BuildingType.Mine, -7, 4, 0, 0, Facing.North),
        // Hauling.
        (BuildingType.Depot, -4, -3, 0, 0, Facing.South),
        // Refining: the last Smelter makes Copper.
        (BuildingType.Smelter, -5, 2, 0, 0, Facing.North),
        (BuildingType.Smelter, -3, 2, 0, 0, Facing.North),
        (BuildingType.Smelter, -1, 2, 0, 0, Facing.North),
        (BuildingType.Smelter, -3, 4, 0, 0, Facing.North),
        (BuildingType.Smelter, -1, 4, 0, 0, Facing.North),
        (BuildingType.Kiln, 3, 2, 0, 0, Facing.North),
        // Power, the Mine upgraded to a Drill Rig (DrillRigCell), and research.
        (BuildingType.BurnerGenerator, 1, -5, 1, 0, Facing.North),
        (BuildingType.Mine, DrillRigCell.Dx, DrillRigCell.Dz, 0, 0, Facing.North),
        (BuildingType.Mine, DrillRigCell.Dx + 1, DrillRigCell.Dz, 0, 0, Facing.North),
        (BuildingType.ResearchLab, -1, -6, 0, 0, Facing.North)
    };

    /// <summary>Each road line, as map cells relative to the spawn's (from and to, at the cell's corner).</summary>
    private static readonly (int FromDx, int FromDz, int ToDx, int ToDz)[] RoadLayout =
    {
        (-9, 0, 6, 0)
    };

    /// <summary>The stockpile the town is left with once it is built (before the run).</summary>
    private static readonly ResourceAmount[] StartingStock =
    {
        new(ResourceType.Wood, 120f),
        new(ResourceType.Stone, 120f),
        new(ResourceType.IronOre, 20f),
        new(ResourceType.CopperOre, 10f),
        new(ResourceType.Coal, 40f),
        new(ResourceType.Iron, 30f),
        new(ResourceType.Copper, 15f)
    };

    /// <summary>The demo town, built and run: what the demo save holds.</summary>
    public static Simulation Create()
    {
        var sim = new Simulation(WorldSettings.Generated(Seed));
        Build(sim);
        Run(sim, RunSeconds);
        return sim;
    }

    /// <summary>Lays the town out on a new generated world (before any run).</summary>
    public static void Build(Simulation sim)
    {
        (int sx, int sz) = SpawnCell(sim);

        // Explore the town's ground the way a player would, by walking over it.
        Godot.Vector3 spawn = sim.Player.Position;
        for (int dx = -ExploreRadius; dx <= ExploreRadius; dx += ExploreStep)
        {
            for (int dz = -ExploreRadius; dz <= ExploreRadius; dz += ExploreStep)
            {
                sim.Debug.TeleportPlayer(spawn.X + dx * Simulation.FineCellsPerMapCell, spawn.Z + dz * Simulation.FineCellsPerMapCell);
                sim.Step(StepSeconds);
            }
        }
        sim.Debug.TeleportPlayer(spawn.X, spawn.Z);
        sim.Step(StepSeconds);

        foreach (ResourceField field in sim.Fields
            .Where(f => Math.Abs(f.MapX - sx) <= ClearRadius && Math.Abs(f.MapZ - sz) <= ClearRadius)
            .ToList())
        {
            sim.Debug.RemoveField(field.MapX, field.MapZ);
        }
        foreach ((ResourceType type, int dx, int dz) in FieldLayout)
        {
            sim.Debug.PlaceField(type, sx + dx, sz + dz, FieldAmount);
        }

        // Its Drill Rig is a tier 2 level, so Steel Works and Powered Mining are researched first, their
        // fees paid as a player's would be.
        foreach (ResearchProject project in new[] { ResearchProject.SteelWorks, ResearchProject.PoweredMining })
        {
            ResearchDef def = ResearchDefs.For(project);
            Pay(sim, def.Fee);
            if (!sim.StartResearch(project))
            {
                throw new InvalidOperationException($"the demo could not start {def.Name}");
            }
            sim.Debug.SetResearchProgress(project, def.Work);
        }

        foreach ((BuildingType type, int dx, int dz, int ox, int oz, Facing facing) in BuildingLayout)
        {
            Place(sim, type, (sx + dx) * Simulation.FineCellsPerMapCell + ox, (sz + dz) * Simulation.FineCellsPerMapCell + oz, facing);
        }

        // The Mine on the Iron Ore goes up to a Drill Rig in place, its price paid as a player's would be.
        Building drillRig = sim.Buildings.First(b => b.Type == BuildingType.Mine
            && b.AnchorX == (sx + DrillRigCell.Dx) * Simulation.FineCellsPerMapCell
            && b.AnchorZ == (sz + DrillRigCell.Dz) * Simulation.FineCellsPerMapCell);
        Pay(sim, sim.UpgradePrice(drillRig));
        if (!sim.TryUpgrade(drillRig.Id))
        {
            throw new InvalidOperationException("the demo could not upgrade its Mine to a Drill Rig");
        }

        foreach ((int fromDx, int fromDz, int toDx, int toDz) in RoadLayout)
        {
            int fromX = (sx + fromDx) * Simulation.FineCellsPerMapCell;
            int fromZ = (sz + fromDz) * Simulation.FineCellsPerMapCell;
            int toX = (sx + toDx) * Simulation.FineCellsPerMapCell;
            int toZ = (sz + toDz) * Simulation.FineCellsPerMapCell;
            RoadPlan plan = sim.PlanRoad(fromX, fromZ, toX, toZ);
            Pay(sim, plan.Cost);
            sim.TryLayRoad(fromX, fromZ, toX, toZ);
        }

        // Orders: the Smelters 99 Iron, but the last 99 Copper, and the Charcoal Kiln 99 Charcoal.
        List<Building> smelters = sim.Buildings.Where(b => b.Type == BuildingType.Smelter).ToList();
        foreach (Building smelter in smelters)
        {
            Order(sim, smelter, smelter == smelters[^1] ? 1 : 0);
        }
        Order(sim, sim.Buildings.First(b => b.Type == BuildingType.Kiln), 0);

        Building depot = sim.Buildings.First(b => b.Type == BuildingType.Depot);
        for (int truck = 1; truck < depot.Def.TruckSlots; truck++)
        {
            Pay(sim, TruckDefs.Cost);
            if (!sim.TryBuyTruck(depot.Id))
            {
                throw new InvalidOperationException("the demo Depot could not buy a truck");
            }
        }

        Pay(sim, ResearchDefs.For(ResearchProject.MaterialTransfer).Fee);
        if (!sim.StartResearch(ResearchProject.MaterialTransfer))
        {
            throw new InvalidOperationException("the demo could not start Material Transfer");
        }

        foreach (ResourceAmount amount in StartingStock)
        {
            float have = sim.Stockpile.TryGetValue(amount.Type, out float held) ? held : 0f;
            if (amount.Amount > have)
            {
                sim.Debug.SeedStockpile(amount.Type, amount.Amount - have);
            }
        }
        sim.Debug.SetTimeOfDay(StartTimeOfDay);
    }

    /// <summary>Runs the sim for a span of game time at the game's fixed step.</summary>
    public static void Run(Simulation sim, double seconds)
    {
        int steps = (int)Math.Round(seconds / StepSeconds);
        for (int step = 0; step < steps; step++)
        {
            sim.Step(StepSeconds);
        }
    }

    /// <summary>The spawn's map cell.</summary>
    public static (int X, int Z) SpawnCell(Simulation sim)
    {
        return (Simulation.MapCellOf((int)MathF.Floor(sim.SpawnPoint.X)), Simulation.MapCellOf((int)MathF.Floor(sim.SpawnPoint.Z)));
    }

    // Places a building as the player does: its cost is added to the stockpile first, then it is
    // placed and paid for.
    private static void Place(Simulation sim, BuildingType type, int anchorX, int anchorZ, Facing facing)
    {
        if (!sim.CanPlace(type, anchorX, anchorZ, facing))
        {
            throw new InvalidOperationException($"the demo cannot place a {type} at ({anchorX}, {anchorZ}) facing {facing}");
        }
        Pay(sim, BuildingDefs.For(type).Cost);
        if (!sim.TryPlaceBuilding(type, anchorX, anchorZ, facing))
        {
            throw new InvalidOperationException($"the demo could not place a {type} at ({anchorX}, {anchorZ})");
        }
    }

    // Tops the stockpile up to a cost, so the next purchase can pay for it.
    private static void Order(Simulation sim, Building building, int recipe)
    {
        if (!sim.QueueCraft(building.Id, recipe, 99))
        {
            throw new InvalidOperationException($"the demo could not queue work at the {building.Def.Name}");
        }
    }

    private static void Pay(Simulation sim, IEnumerable<ResourceAmount> cost)
    {
        foreach (ResourceAmount amount in cost)
        {
            float have = sim.Stockpile.TryGetValue(amount.Type, out float held) ? held : 0f;
            if (amount.Amount > have)
            {
                sim.Debug.SeedStockpile(amount.Type, amount.Amount - have);
            }
        }
    }
}
