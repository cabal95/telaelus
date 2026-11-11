using System;
using System.Text.Json.Nodes;
using Game.Presentation;
using Game.Sim;
using Godot;

namespace Game.Testing;

/// <summary>
/// Builds the JSON state snapshot: the single contract for scenario expectations,
/// run reports, and the live channel. New checks are added as new fields here.
/// </summary>
public static class StateSnapshot
{
    public static JsonObject Build(GameRoot root, CommandExecutor executor)
    {
        Simulation sim = root.Sim;
        var heldKeys = new JsonArray();
        foreach (Key key in executor.HeldKeys)
        {
            heldKeys.Add(key.ToString());
        }

        return new JsonObject
        {
            ["frame"] = executor.Frame,
            ["paused"] = executor.Paused,
            ["input"] = new JsonObject
            {
                ["heldKeys"] = heldKeys
            },
            ["player"] = Player(sim),
            ["build"] = Build(root.Builder),
            ["buildings"] = Buildings(sim),
            ["fields"] = Fields(sim, root.FieldViews),
            ["stockpile"] = Stockpile(sim),
            ["shipsLaunched"] = sim.ShipsLaunched
        };
    }

    private static JsonObject Player(Simulation sim)
    {
        (int x, int z) = Simulation.CellAt(sim.Player.Position.X, sim.Player.Position.Z);
        return new JsonObject
        {
            ["position"] = Pair(sim.Player.Position.X, sim.Player.Position.Z),
            ["harvestTarget"] = sim.HarvestTarget?.Id,
            ["terrain"] = sim.TerrainAt(x, z).ToString(),
            ["biome"] = sim.BiomeAt(x, z).ToString(),
            ["feature"] = sim.FeatureAt(x, z).ToString()
        };
    }

    private static JsonNode? Build(BuildController builder)
    {
        if (builder.Selected is not BuildingType selected)
        {
            return null;
        }

        BuildController.GhostInfo? ghost = builder.CurrentGhost();
        return new JsonObject
        {
            ["selected"] = selected.ToString(),
            ["facing"] = builder.Facing.ToString(),
            ["fieldsInRange"] = ghost?.FieldsInRange ?? 0,
            ["ghost"] = ghost is BuildController.GhostInfo g
                ? new JsonObject
                {
                    ["anchor"] = Pair(g.AnchorX, g.AnchorZ),
                    ["width"] = g.Width,
                    ["depth"] = g.Depth,
                    ["placeable"] = g.Placeable
                }
                : null
        };
    }

    private static JsonArray Buildings(Simulation sim)
    {
        var list = new JsonArray();
        foreach (Building b in sim.Buildings)
        {
            list.Add(new JsonObject
            {
                ["id"] = b.Id,
                ["type"] = b.Type.ToString(),
                ["anchor"] = Pair(b.AnchorX, b.AnchorZ),
                ["facing"] = b.Facing.ToString(),
                ["width"] = b.Width,
                ["depth"] = b.Depth,
                ["currentField"] = b.CurrentField?.Id
            });
        }
        return list;
    }

    private static JsonArray Fields(Simulation sim, ResourceFieldViews views)
    {
        var list = new JsonArray();
        foreach (ResourceField f in sim.Fields)
        {
            list.Add(new JsonObject
            {
                ["id"] = f.Id,
                ["type"] = f.Type.ToString(),
                ["mapCell"] = Pair(f.MapX, f.MapZ),
                ["amount"] = Round(f.Amount),
                ["stage"] = f.Stage,
                ["visibleModels"] = views.VisibleModels(f.Id)
            });
        }
        return list;
    }

    private static JsonObject Stockpile(Simulation sim)
    {
        var stock = new JsonObject();
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            sim.Stockpile.TryGetValue(type, out float amount);
            stock[type.ToString()] = Round(amount);
        }
        return stock;
    }

    private static JsonArray Pair(float x, float z)
    {
        return new JsonArray(Round(x), Round(z));
    }

    private static JsonArray Pair(int x, int z)
    {
        return new JsonArray(x, z);
    }

    private static double Round(float value)
    {
        return Math.Round(value, 3);
    }
}
