using System;
using System.IO;
using Godot;

namespace Game.Testing;

/// <summary>Repo-relative paths shared by the scenario runner and the control channel.</summary>
public static class TestPaths
{
    /// <summary>The repo root: two levels above the Godot project (src/Game).</summary>
    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", ".."));

    /// <summary>
    /// Resolves a path given on the command line. Godot switches its working directory
    /// to the project folder when given --path, so relative paths resolve against the
    /// repo root instead.
    /// </summary>
    public static string FromRepoRoot(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(RepoRoot, path));
    }

    /// <summary>A run folder: the --output path if given, else test-output/runs/&lt;timestamp&gt;-&lt;name&gt;.</summary>
    public static string RunFolder(string? outputArg, string name)
    {
        if (outputArg is string output)
        {
            return FromRepoRoot(output);
        }
        return Path.Combine(RepoRoot, "test-output", "runs", $"{DateTime.Now:yyMMdd-HHmmss}-{name}");
    }
}
