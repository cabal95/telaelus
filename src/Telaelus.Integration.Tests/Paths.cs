using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Telaelus.Integration.Tests;

/// <summary>Locates the repo, the scenario folder, and the Godot editor.</summary>
internal static class Paths
{
    /// <summary>The repo root: the nearest folder above the test binaries holding Telaelus.slnx.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ScenarioDir => Path.Combine(RepoRoot, "src", "Telaelus.Integration.Tests", "Scenarios");

    /// <summary>
    /// The Godot editor path: the GODOT_EDITOR environment variable if set, otherwise
    /// godot.editor from tools.json at the repo root.
    /// </summary>
    public static string GodotEditor()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("GODOT_EDITOR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        string tools = Path.Combine(RepoRoot, "tools.json");
        if (File.Exists(tools))
        {
            string? editor = JsonNode.Parse(File.ReadAllText(tools))?["godot"]?["editor"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(editor))
            {
                return editor;
            }
        }

        throw new InvalidOperationException(
            "No Godot editor path. Copy tools.template.json to tools.json at the repo root and set godot.editor, "
            + "or set the GODOT_EDITOR environment variable.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telaelus.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root (Telaelus.slnx).");
    }
}
