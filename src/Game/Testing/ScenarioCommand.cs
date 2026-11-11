using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Sim;
using Godot;

namespace Game.Testing;

/// <summary>The result of running one command: pass or fail, and detail for the report.</summary>
public sealed record StepOutcome(bool Passed, string? Message = null, JsonNode? Detail = null)
{
    public static readonly StepOutcome Ok = new(true);
}

/// <summary>
/// One parsed scenario step. Parsing validates the whole command up front (so an
/// invalid scenario fails before anything runs) and captures typed arguments in
/// <see cref="Run"/>. Commands are shared by scenarios and the live channel.
/// </summary>
public sealed class ScenarioCommand
{
    public string Name { get; }
    public JsonNode Raw { get; }
    public Func<CommandExecutor, StepOutcome> Run { get; }

    private ScenarioCommand(string name, JsonNode raw, Func<CommandExecutor, StepOutcome> run)
    {
        Name = name;
        Raw = raw;
        Run = run;
    }

    private static readonly string[] ExpectOperators =
    {
        "equals",
        "notEquals",
        "gte",
        "lte",
        "approx",
        "exists"
    };

    /// <summary>Parses a step object. Throws <see cref="FormatException"/> when invalid.</summary>
    public static IEnumerable<ScenarioCommand> Parse(JsonElement step)
    {
        if (step.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("a step must be a JSON object");
        }

        var keys = step.EnumerateObject().Select(p => p.Name).Where(k => k != "comment").ToList();
        JsonNode raw = JsonNode.Parse(step.GetRawText())!;
        if (keys.Contains("expect"))
        {
            return new[]
            {
                ParseExpect(step, keys, raw)
            };
        }
        if (keys.Count != 1)
        {
            throw new FormatException($"a step must have exactly one command, found: {string.Join(", ", keys)}");
        }

        string name = keys[0];
        JsonElement arg = step.GetProperty(name);
        if (name == "hold")
        {
            // hold expands to keyDown, wait, keyUp.
            Key key = ParseKey(Required(arg, "key"));
            int frames = Int(Required(arg, "frames"), "frames");
            return new[]
            {
                new ScenarioCommand(name, raw, e => e.KeyDown(key)),
                new ScenarioCommand(name, raw, e => e.Wait(frames)),
                new ScenarioCommand(name, raw, e => e.KeyUp(key))
            };
        }
        return new[]
        {
            new ScenarioCommand(name, raw, ParseSingle(name, arg))
        };
    }

    private static Func<CommandExecutor, StepOutcome> ParseSingle(string name, JsonElement arg)
    {
        switch (name)
        {
            case "initMap":
                {
                    RequireObject(arg, name);
                    return e => e.InitMap();
                }
            case "changeTiles":
                {
                    TerrainType type = ParseEnum<TerrainType>(Required(arg, "type"));
                    List<(int X, int Z)> cells = arg.TryGetProperty("cells", out JsonElement list)
                        ? list.EnumerateArray().Select(c => Pair(c, "cells")).ToList()
                        : Rectangle(Pair(Required(arg, "from"), "from"), Pair(Required(arg, "to"), "to"));
                    return e => e.ChangeTiles(type, cells);
                }
            case "placeResource":
                {
                    ResourceType type = ParseEnum<ResourceType>(Required(arg, "type"));
                    (int x, int z) = Pair(Required(arg, "mapCell"), "mapCell");
                    float? amount = arg.TryGetProperty("amount", out JsonElement a) ? Number(a, "amount") : null;
                    return e => e.PlaceResource(type, x, z, amount);
                }
            case "setResourceAmount":
                {
                    (int x, int z) = Pair(Required(arg, "mapCell"), "mapCell");
                    float amount = Number(Required(arg, "amount"), "amount");
                    return e => e.SetResourceAmount(x, z, amount);
                }
            case "removeResource":
                {
                    (int x, int z) = Pair(Required(arg, "mapCell"), "mapCell");
                    return e => e.RemoveResource(x, z);
                }
            case "removeBuilding":
                {
                    (int x, int z) = Pair(Required(arg, "anchor"), "anchor");
                    return e => e.RemoveBuilding(x, z);
                }
            case "placeBuilding":
                {
                    BuildingType type = ParseEnum<BuildingType>(Required(arg, "type"));
                    (int x, int z) = Pair(Required(arg, "anchor"), "anchor");
                    Facing facing = arg.TryGetProperty("facing", out JsonElement f) ? ParseEnum<Facing>(f) : Facing.North;
                    return e => e.PlaceBuilding(type, x, z, facing);
                }
            case "teleportPlayer":
                {
                    float x = Number(Required(arg, "x"), "x");
                    float z = Number(Required(arg, "z"), "z");
                    return e => e.TeleportPlayer(x, z);
                }
            case "seedStockpile":
                {
                    ResourceType type = ParseEnum<ResourceType>(Required(arg, "type"));
                    float amount = Number(Required(arg, "amount"), "amount");
                    return e => e.SeedStockpile(type, amount);
                }
            case "cameraZoom":
                {
                    float size = Number(Required(arg, "size"), "size");
                    return e => e.CameraZoom(size);
                }
            case "press":
                {
                    Key key = ParseKey(arg);
                    return e => e.Press(key);
                }
            case "keyDown":
                {
                    Key key = ParseKey(arg);
                    return e => e.KeyDown(key);
                }
            case "keyUp":
                {
                    Key key = ParseKey(arg);
                    return e => e.KeyUp(key);
                }
            case "aimAt":
                {
                    Vector3 world;
                    if (arg.TryGetProperty("cell", out JsonElement cell))
                    {
                        (int x, int z) = Pair(cell, "cell");
                        world = Simulation.CellCenter(x, z);
                    }
                    else
                    {
                        JsonElement point = Required(arg, "world");
                        float[] xz = Floats(point, "world");
                        world = new Vector3(xz[0], 0f, xz[1]);
                    }
                    return e => e.AimAt(world);
                }
            case "click":
                {
                    RequireObject(arg, name);
                    return e => e.Click();
                }
            case "wait":
                {
                    int frames = Int(arg, "wait");
                    if (frames < 0)
                    {
                        throw new FormatException("wait must be zero or more frames");
                    }
                    return e => e.Wait(frames);
                }
            case "screenshot":
                {
                    string label = Label(arg, name);
                    return e => e.Screenshot(label);
                }
            case "snapshot":
                {
                    string label = Label(arg, name);
                    return e => e.Snapshot(label);
                }
            default:
                throw new FormatException($"unknown command '{name}'");
        }
    }

    private static ScenarioCommand ParseExpect(JsonElement step, List<string> keys, JsonNode raw)
    {
        string path = step.GetProperty("expect").ValueKind == JsonValueKind.String
            ? step.GetProperty("expect").GetString()!
            : throw new FormatException("expect must be a snapshot path string");
        SnapshotPath.Validate(path);

        var ops = keys.Where(k => k != "expect" && k != "tolerance").ToList();
        if (ops.Count != 1 || !ExpectOperators.Contains(ops[0]))
        {
            throw new FormatException($"expect needs exactly one of {string.Join(", ", ExpectOperators)}");
        }

        string op = ops[0];
        JsonElement expected = step.GetProperty(op);
        if (op is "gte" or "lte" or "approx")
        {
            Number(expected, op);
        }
        if (op == "exists" && expected.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new FormatException("exists takes true or false");
        }
        double tolerance = step.TryGetProperty("tolerance", out JsonElement t) ? Number(t, "tolerance") : 0.001;
        JsonNode? expectedNode = JsonNode.Parse(expected.GetRawText());
        return new ScenarioCommand("expect", raw, e => e.Expect(path, op, expectedNode, tolerance));
    }

    private static JsonElement Required(JsonElement obj, string property)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(property, out JsonElement value))
        {
            throw new FormatException($"missing '{property}'");
        }
        return value;
    }

    private static void RequireObject(JsonElement arg, string name)
    {
        if (arg.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"{name} takes an object, e.g. {{}}");
        }
    }

    private static Key ParseKey(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !Enum.TryParse(value.GetString(), false, out Key key))
        {
            throw new FormatException($"unknown key {value.GetRawText()} (use Godot Key names such as R, Key4, Escape)");
        }
        return key;
    }

    private static T ParseEnum<T>(JsonElement value)
        where T : struct, Enum
    {
        if (value.ValueKind != JsonValueKind.String || !Enum.TryParse(value.GetString(), false, out T result))
        {
            throw new FormatException($"unknown {typeof(T).Name} {value.GetRawText()} (one of {string.Join(", ", Enum.GetNames<T>())})");
        }
        return result;
    }

    private static string Label(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new FormatException($"{name} takes a label string");
        }
        return value.GetString()!;
    }

    private static float Number(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException($"'{name}' must be a number");
        }
        return (float)value.GetDouble();
    }

    private static int Int(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
        {
            throw new FormatException($"'{name}' must be a whole number");
        }
        return result;
    }

    private static (int X, int Z) Pair(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
        {
            throw new FormatException($"'{name}' must be [x, z]");
        }
        return (Int(value[0], name), Int(value[1], name));
    }

    private static float[] Floats(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
        {
            throw new FormatException($"'{name}' must be [x, z]");
        }
        return new[]
        {
            Number(value[0], name),
            Number(value[1], name)
        };
    }

    private static List<(int X, int Z)> Rectangle((int X, int Z) from, (int X, int Z) to)
    {
        var cells = new List<(int X, int Z)>();
        for (int x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
        {
            for (int z = Math.Min(from.Z, to.Z); z <= Math.Max(from.Z, to.Z); z++)
            {
                cells.Add((x, z));
            }
        }
        return cells;
    }

    /// <summary>Formats a number for messages without trailing noise.</summary>
    public static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
