using System;
using System.Linq;
using System.Text.Json.Nodes;
using Game.Presentation;
using Game.Presentation.Ui;
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
            ["world"] = World(sim, root.Terrain, root.FieldViews),
            ["perf"] = Perf(root.Perf),
            ["player"] = Player(sim),
            ["build"] = Build(root.Builder, root.Dialogs),
            ["hoveredBuilding"] = root.Builder.Hovered?.Id,
            ["focusedBuilding"] = root.Builder.Focused?.Id,
            ["hoveredField"] = root.Builder.HoveredField?.Id,
            ["focusedField"] = root.Builder.FocusedField?.Id,
            ["camera"] = new JsonObject
            {
                ["zoom"] = Math.Round(root.Camera.ZoomLevel, 2),
                ["target"] = Pair(root.Camera.GroundCenter.X, root.Camera.GroundCenter.Z),
                ["detached"] = root.Camera.Detached
            },
            ["ui"] = Ui(root),
            ["buildings"] = Buildings(sim),
            ["trucks"] = Trucks(sim),
            ["workedOut"] = WorkedOut(sim),
            ["fields"] = Fields(sim, root.FieldViews),
            ["stockpile"] = Stockpile(sim),
            ["capacity"] = Capacity(sim),
            ["discovered"] = Discovered(sim),
            ["unlockedBuildings"] = Unlocked(sim),
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
            ["harvestProgress"] = Math.Round(sim.HarvestProgress, 3),
            ["interactTarget"] = sim.InteractTarget?.Id,
            ["interactAction"] = sim.InteractTarget is null ? null : Camel(sim.InteractAction.ToString()),
            ["interactProgress"] = Math.Round(sim.InteractProgress, 3),
            ["terrain"] = sim.TerrainAt(x, z).ToString(),
            ["biome"] = sim.BiomeAt(x, z).ToString(),
            ["feature"] = sim.FeatureAt(x, z).ToString(),
            ["chunk"] = Pair(Simulation.ChunkAt(sim.Player.Position).ChunkX, Simulation.ChunkAt(sim.Player.Position).ChunkZ)
        };
    }

    private static JsonObject Perf(PerfMonitor perf)
    {
        PerfMonitor.Sample s = perf.Latest;
        return new JsonObject
        {
            ["headlineMs"] = Math.Round(s.HeadlineMs, 2),
            ["workMs"] = Math.Round(s.WorkMs, 2),
            ["scriptMs"] = Math.Round(s.ScriptMs, 2),
            ["renderCpuMs"] = Math.Round(s.RenderCpuMs, 2),
            ["gpuMs"] = s.GpuMs is double g ? Math.Round(g, 2) : null,
            ["simMs"] = Math.Round(s.SimMs, 2),
            ["frameMs"] = Math.Round(s.FrameMs, 2),
            ["worstMs"] = Math.Round(perf.WorstMs, 2),
            ["fps"] = Math.Round(s.Fps, 1),
            ["draws"] = s.Draws,
            ["objects"] = s.Objects
        };
    }

    private static JsonObject World(Simulation sim, TerrainView terrain, ResourceFieldViews fieldViews)
    {
        return new JsonObject
        {
            ["kind"] = sim.World.Kind.ToString(),
            ["seed"] = sim.World.Kind == WorldKind.Generated ? sim.World.Seed : null,
            ["chunks"] = sim.ChunkCount,
            ["tiles"] = terrain.TileCount,
            ["fieldBatches"] = fieldViews.BatchCount,
            ["fog"] = sim.World.Fog,
            ["explored"] = sim.World.Fog ? sim.ExploredCount : null
        };
    }

    private static JsonObject Ui(GameRoot root)
    {
        var ui = new JsonObject
        {
            ["open"] = root.Dialogs.Current?.Id,
            ["tooltip"] = Lines(root.Dialogs.HoverTip),
            ["worldTip"] = root.Builder.Selected is null ? Lines(root.Dialogs.PlacementTip) : null,
            ["debug"] = root.Debug.Shown,
            ["network"] = new JsonObject
            {
                ["overlay"] = root.Network.OverlayShown,
                ["shownHubs"] = new JsonArray(root.Network.ShownHubs.Select(id => (JsonNode?)id).ToArray()),
                ["offlineMarkers"] = root.Network.OfflineMarkers
            },
            ["topBar"] = TopBar(root.TopBar),
            ["hint"] = root.Hint.Text is string hint
                ? new JsonObject
                {
                    ["text"] = hint,
                    ["progress"] = Math.Round(root.Hint.Progress, 3),
                    ["storageFull"] = root.Hint.StorageFull
                }
                : null
        };
        if (root.Dialogs.Current == root.FieldDetail && root.FieldDetail.Field is ResourceField field)
        {
            var fieldRows = new JsonObject();
            foreach ((string name, string value) in root.FieldDetail.Rows)
            {
                fieldRows[RowKey(name)] = value;
            }
            ui["field"] = field.Id;
            ui["title"] = ResourceDefs.For(field.Type).Name;
            ui["rows"] = fieldRows;
        }
        if (root.Dialogs.Current == root.Inventory)
        {
            var rows = new JsonArray();
            foreach ((ResourceType type, int amount, string section, bool discovered, int? limit, bool pinned) in root.Inventory.Rows)
            {
                rows.Add(new JsonObject
                {
                    ["type"] = type.ToString(),
                    ["amount"] = amount,
                    ["limit"] = limit,
                    ["section"] = section,
                    ["discovered"] = discovered,
                    ["pinned"] = pinned
                });
            }
            ui["inventory"] = rows;
            ui["cargo"] = root.Inventory.CargoText;
        }
        if (root.Dialogs.Current == root.BuildMenu)
        {
            var cells = new JsonArray();
            foreach ((BuildingType type, BuildCellState state, string label) in root.BuildMenu.Cells)
            {
                cells.Add(new JsonObject
                {
                    ["type"] = type.ToString(),
                    ["state"] = Camel(state.ToString()),
                    ["label"] = label
                });
            }
            ui["cells"] = cells;
        }
        if (root.Dialogs.Current == root.BuildingDetail && root.BuildingDetail.Building is Building building)
        {
            var rows = new JsonObject();
            foreach ((string name, string value) in root.BuildingDetail.Rows)
            {
                rows[RowKey(name)] = value;
            }
            ui["building"] = building.Id;
            ui["rows"] = rows;
            ui["destroy"] = root.BuildingDetail.DestroyText;
            ui["fuel"] = root.BuildingDetail.FuelText;
            ui["output"] = root.BuildingDetail.OutputText;
            ui["recipe"] = root.BuildingDetail.RecipeChoiceText;
            ui["input"] = root.BuildingDetail.InputChoiceText is (string choice, string note)
                ? new JsonObject
                {
                    ["choice"] = choice,
                    ["note"] = note
                }
                : null;
            ui["buyTruck"] = root.BuildingDetail.BuyTruck is (string text, bool enabled)
                ? new JsonObject
                {
                    ["text"] = text,
                    ["enabled"] = enabled
                }
                : null;
            var adds = new JsonObject();
            foreach ((string resource, string amount) in root.BuildingDetail.Adds)
            {
                adds[resource] = amount;
            }
            ui["adds"] = adds;
            ui["turnDegrees"] = Math.Round(root.BuildingDetail.TurnDegrees, 1);
        }
        return ui;
    }

    /// <summary>Each capacity type's supply, demand, speed, and state, keyed by camel-cased type.</summary>
    private static JsonObject Capacity(Simulation sim)
    {
        var capacity = new JsonObject();
        foreach (CapacityType type in Simulation.CapacityTypes)
        {
            CapacityReading reading = sim.Capacity(type);
            capacity[Camel(type.ToString())] = new JsonObject
            {
                ["supply"] = Round(reading.Supply),
                ["demand"] = Round(reading.Demand),
                ["speed"] = Round(reading.Speed),
                ["state"] = Camel(reading.State.ToString())
            };
        }
        return capacity;
    }

    private static JsonObject TopBar(TopBar bar)
    {
        var items = new JsonArray();
        foreach ((ResourceType type, int amount, int? limit, bool full, string text, string tone) in bar.Entries)
        {
            items.Add(new JsonObject
            {
                ["type"] = type.ToString(),
                ["amount"] = amount,
                ["limit"] = limit,
                ["full"] = full,
                ["text"] = text,
                ["tone"] = tone
            });
        }
        return new JsonObject
        {
            ["items"] = items,
            ["power"] = bar.PowerText,
            ["launch"] = bar.LaunchText
        };
    }

    /// <summary>A tooltip's lines while it shows, else null.</summary>
    private static JsonArray? Lines(Tooltip tip)
    {
        if (!tip.Visible)
        {
            return null;
        }
        var lines = new JsonArray();
        foreach (string line in tip.Lines)
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>A status row's label as a snapshot key: "In range" becomes "inRange".</summary>
    private static string RowKey(string label)
    {
        string[] words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = new System.Text.StringBuilder(words[0].ToLowerInvariant());
        foreach (string word in words[1..])
        {
            key.Append(char.ToUpperInvariant(word[0])).Append(word[1..]);
        }
        return key.ToString();
    }

    private static string Camel(string name)
    {
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static JsonNode? Build(BuildController builder, DialogHost dialogs)
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
            ["tooltip"] = Lines(dialogs.PlacementTip),
            ["ghost"] = ghost is BuildController.GhostInfo g
                ? new JsonObject
                {
                    ["anchor"] = Pair(g.AnchorX, g.AnchorZ),
                    ["width"] = g.Width,
                    ["depth"] = g.Depth,
                    ["placeable"] = g.Placeable,
                    ["affordable"] = g.Affordable
                }
                : null
        };
    }

    private static JsonArray Unlocked(Simulation sim)
    {
        var list = new JsonArray();
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (sim.IsUnlocked(type))
            {
                list.Add(type.ToString());
            }
        }
        return list;
    }

    /// <summary>The worked-out map cells in cell order, each with its deposit.</summary>
    private static JsonArray WorkedOut(Simulation sim)
    {
        var list = new JsonArray();
        foreach (((int x, int z), ResourceType deposit) in sim.WorkedOut.OrderBy(e => e.Key.MapX).ThenBy(e => e.Key.MapZ))
        {
            list.Add(new JsonObject
            {
                ["mapCell"] = Pair(x, z),
                ["deposit"] = deposit.ToString()
            });
        }
        return list;
    }

    private static JsonArray Trucks(Simulation sim)
    {
        var list = new JsonArray();
        foreach (Truck t in sim.Trucks)
        {
            list.Add(new JsonObject
            {
                ["id"] = t.Id,
                ["depot"] = t.Depot.Id,
                ["bay"] = t.Bay,
                ["state"] = Camel(t.State.ToString()),
                ["position"] = Pair(t.Position.X, t.Position.Z),
                ["cargo"] = t.Cargo?.ToString(),
                ["amount"] = Round(t.Amount),
                ["target"] = t.Target?.Id
            });
        }
        return list;
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
                ["currentField"] = b.CurrentField?.Id,
                ["enabled"] = b.Enabled,
                ["online"] = b.Online,
                ["output"] = Amounts(b.Output),
                ["input"] = Amounts(b.Input),
                ["fuelSlot"] = Amounts(b.FuelSlot),
                ["burning"] = b.BurnLeft > 0f ? b.Burning?.ToString() : null,
                ["producing"] = b.Def.ProduceSeconds > 0f ? sim.OutputOf(b)?.ToString() : null,
                ["recipe"] = b.Recipe?.Name,
                ["status"] = Camel(sim.BuildingStatusOf(b).State.ToString()),
                ["idleReason"] = sim.BuildingStatusOf(b).IdleReason
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
                ["visibleModels"] = views.VisibleModels(f.Id),
                ["explored"] = sim.IsExplored(f.MapX, f.MapZ),
                ["growing"] = f.Growing,
                ["motherlode"] = f.Motherlode?.ToString()
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

    private static JsonObject Amounts(System.Collections.Generic.Dictionary<ResourceType, float> buffer)
    {
        var amounts = new JsonObject();
        foreach ((ResourceType type, float amount) in buffer.OrderBy(entry => entry.Key))
        {
            amounts[type.ToString()] = Round(amount);
        }
        return amounts;
    }

    private static JsonArray Discovered(Simulation sim)
    {
        var list = new JsonArray();
        foreach (ResourceType type in Enum.GetValues<ResourceType>().Where(sim.IsDiscovered))
        {
            list.Add(type.ToString());
        }
        return list;
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
