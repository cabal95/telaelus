using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Integration.Tests;

/// <summary>
/// Runs every scenario in Scenarios/ as its own test, headless, against the built
/// game. Each row is a plain string (the file name), so xUnit lists one test per
/// file in Test Explorer and in dotnet test output.
/// </summary>
public class ScenarioTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static IEnumerable<object[]> ScenarioFiles()
    {
        return Directory
            .EnumerateFiles(Paths.ScenarioDir, "*.json")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[]
            {
                name!
            });
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public async Task Scenario(string file)
    {
        string godot = Paths.GodotEditor();
        string name = Path.GetFileNameWithoutExtension(file);
        string output = Path.Combine(
            Paths.RepoRoot,
            "test-output",
            "integration",
            $"{DateTime.Now:yyMMdd-HHmmss}-{name}");

        var start = new ProcessStartInfo(godot)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string arg in new[]
        {
            "--headless",
            "--path",
            Path.Combine(Paths.RepoRoot, "src", "Game"),
            // Matches the sim's 30 Hz tick: one frame is one sim tick.
            "--fixed-fps",
            "30",
            "--",
            $"--scenario={Path.Combine(Paths.ScenarioDir, file)}",
            $"--output={output}"
        })
        {
            start.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{file} timed out after {Timeout.TotalSeconds:0} s. Run folder: {output}");
        }

        await stdout;
        string errors = await stderr;
        if (process.ExitCode != 0)
        {
            Assert.Fail($"{file} exited {process.ExitCode}: {ReportMessage(output)}\nRun folder: {output}\n{Tail(errors)}");
        }
    }

    private static string ReportMessage(string output)
    {
        string report = Path.Combine(output, "report.json");
        if (!File.Exists(report))
        {
            return "no report.json was written";
        }
        JsonNode? node = JsonNode.Parse(File.ReadAllText(report));
        return node?["message"]?.GetValue<string>() ?? "(no message)";
    }

    private static string Tail(string text)
    {
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\n', lines.TakeLast(15));
    }
}
