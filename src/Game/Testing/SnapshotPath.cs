using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Game.Testing;

/// <summary>
/// Resolves dotted snapshot paths such as "build.ghost.width" or
/// "fields[0].visibleModels", and compares JSON values for expectations.
/// </summary>
public static partial class SnapshotPath
{
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*)((?:\[\d+\])*)$")]
    private static partial Regex SegmentPattern();

    [GeneratedRegex(@"\[(\d+)\]")]
    private static partial Regex IndexPattern();

    /// <summary>Throws <see cref="FormatException"/> if the path is malformed.</summary>
    public static void Validate(string path)
    {
        foreach (string segment in path.Split('.'))
        {
            if (!SegmentPattern().IsMatch(segment))
            {
                throw new FormatException($"invalid snapshot path '{path}'");
            }
        }
    }

    /// <summary>Resolves a path. Returns false if any part of it does not exist.</summary>
    public static bool TryResolve(JsonNode root, string path, out JsonNode? value)
    {
        JsonNode? current = root;
        foreach (string segment in path.Split('.'))
        {
            Match match = SegmentPattern().Match(segment);
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(match.Groups[1].Value, out current))
            {
                value = null;
                return false;
            }

            foreach (Match index in IndexPattern().Matches(match.Groups[2].Value))
            {
                int i = int.Parse(index.Groups[1].Value, CultureInfo.InvariantCulture);
                if (current is not JsonArray array || i >= array.Count)
                {
                    value = null;
                    return false;
                }
                current = array[i];
            }
        }

        value = current;
        return true;
    }

    /// <summary>Deep equality; numbers compare by value, so 3 equals 3.0.</summary>
    public static bool JsonEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        if (a is JsonArray arrayA && b is JsonArray arrayB)
        {
            if (arrayA.Count != arrayB.Count)
            {
                return false;
            }
            for (int i = 0; i < arrayA.Count; i++)
            {
                if (!JsonEquals(arrayA[i], arrayB[i]))
                {
                    return false;
                }
            }
            return true;
        }
        if (a is JsonObject objectA && b is JsonObject objectB)
        {
            if (objectA.Count != objectB.Count)
            {
                return false;
            }
            foreach ((string key, JsonNode? valueA) in objectA)
            {
                if (!objectB.TryGetPropertyValue(key, out JsonNode? valueB) || !JsonEquals(valueA, valueB))
                {
                    return false;
                }
            }
            return true;
        }
        if (TryNumber(a, out double numberA) && TryNumber(b, out double numberB))
        {
            return Math.Abs(numberA - numberB) < 1e-9;
        }
        if (a is JsonValue && b is JsonValue)
        {
            return a.ToJsonString() == b.ToJsonString();
        }
        return false;
    }

    public static bool TryNumber(JsonNode? node, out double number)
    {
        number = 0;
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number)
        {
            // Parse the JSON text: GetValue<double> fails on values built from ints.
            number = double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
            return true;
        }
        return false;
    }
}
