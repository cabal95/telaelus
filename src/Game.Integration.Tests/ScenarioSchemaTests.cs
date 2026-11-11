using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Json.Schema;

namespace Game.Integration.Tests;

/// <summary>
/// Checks the scenario schema itself and every scenario file against it, without
/// launching Godot. Keeps the $schema that editors use for validation accurate.
/// </summary>
public class ScenarioSchemaTests
{
    private static string SchemaPath => Path.Combine(Paths.RepoRoot, "src", "Game.Integration.Tests", "scenario.schema.json");

    // Loaded once: the validator registers a schema globally by its $id, and
    // registering the same $id twice throws.
    private static readonly System.Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(File.ReadAllText(SchemaPath)));

    [Fact]
    public void Schema_HasNoDuplicateKeys()
    {
        // JSON allows duplicate keys and the last one wins silently, which once made
        // a command definition replace the step wrapper and reject every scenario.
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        var duplicates = new List<string>();
        FindDuplicates(doc.RootElement, "$", duplicates);
        Assert.True(duplicates.Count == 0, $"duplicate keys in scenario.schema.json: {string.Join(", ", duplicates)}");
    }

    [Theory]
    [MemberData(nameof(ScenarioTests.ScenarioFiles), MemberType = typeof(ScenarioTests))]
    public void Scenario_MatchesTheSchema(string file)
    {
        JsonSchema schema = Schema.Value;
        using JsonDocument scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(Paths.ScenarioDir, file)));

        EvaluationResults results = schema.Evaluate(scenario.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        string errors = string.Join(
            "\n",
            (results.Details ?? new List<EvaluationResults>())
                .Where(d => d.Errors is not null)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")));
        Assert.True(results.IsValid, $"{file} does not match scenario.schema.json:\n{errors}");
    }

    private static void FindDuplicates(JsonElement element, string path, List<string> duplicates)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>();
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    duplicates.Add($"{path}.{property.Name}");
                }
                FindDuplicates(property.Value, $"{path}.{property.Name}", duplicates);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                FindDuplicates(item, $"{path}[{index++}]", duplicates);
            }
        }
    }
}
