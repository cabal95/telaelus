using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Presentation;
using Godot;

namespace Game.Testing;

/// <summary>
/// Runs one JSON scenario against the live game, then writes report.json (and any
/// screenshots) to the run folder and quits with an exit code: 0 passed, 1 an
/// expectation failed, 2 the scenario was invalid or a step threw. Attached by
/// GameRoot only when --scenario is given, and only in debug builds.
/// </summary>
public partial class ScenarioRunner : Node
{
    private static readonly string[] TopLevelKeys =
    {
        "$schema",
        "name",
        "description",
        "steps"
    };

    private GameRoot _root = null!;
    private TestArgs _args = null!;
    private string _scenarioFile = "";
    private string _name = "";
    private string _outputDir = "";
    private readonly List<(int Step, ScenarioCommand Command)> _commands = new();
    private readonly JsonArray _records = new();
    private CommandExecutor _executor = null!;
    private int _next;
    private bool _finished;
    private readonly List<PerfMonitor.Sample> _perfSamples = new();

    /// <summary>Attaches a runner to the game root if the command line asks for a scenario.</summary>
    public static void AttachIfRequested(GameRoot root)
    {
        TestArgs args = TestArgs.Parse();
        if (args.ScenarioPath is null)
        {
            return;
        }
        if (!OS.IsDebugBuild())
        {
            GD.PushWarning("--scenario is ignored outside debug builds.");
            return;
        }

        var runner = new ScenarioRunner
        {
            Name = "ScenarioRunner",
            ProcessMode = ProcessModeEnum.Always,
            _root = root,
            _args = args
        };
        root.AddChild(runner);
    }

    public override void _Ready()
    {
        _scenarioFile = TestPaths.FromRepoRoot(_args.ScenarioPath!);
        _name = Path.GetFileNameWithoutExtension(_scenarioFile);
        try
        {
            Load();
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException)
        {
            _outputDir = ResolveOutputDir();
            Finish(2, $"invalid scenario: {ex.Message}");
            return;
        }

        _outputDir = ResolveOutputDir();
        bool screenshots = !_args.NoScreenshots && !TestArgs.IsHeadless;
        _executor = new CommandExecutor(_root, _outputDir, screenshots);
        _root.Perf.Sampled += _perfSamples.Add;
        GD.Print($"SCENARIO {_name}: {_commands.Count} commands, output {_outputDir}");
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _executor.BeginFrame();
        while (!_executor.Busy && _next < _commands.Count)
        {
            (int step, ScenarioCommand command) = _commands[_next++];
            StepOutcome outcome;
            try
            {
                outcome = command.Run(_executor);
            }
            catch (Exception ex)
            {
                Record(step, command, "error", ex.Message, null);
                Finish(2, $"step {step} ({command.Name}) threw: {ex.Message}");
                return;
            }

            Record(step, command, outcome.Passed ? "ok" : "failed", outcome.Message, outcome.Detail);
            if (!outcome.Passed)
            {
                Finish(1, $"step {step}: {outcome.Message}");
                return;
            }
        }

        if (!_executor.Busy && _next >= _commands.Count)
        {
            Finish(0, null);
        }
    }

    private void Load()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_scenarioFile), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        JsonElement rootElement = doc.RootElement;
        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("a scenario must be a JSON object");
        }
        foreach (JsonProperty property in rootElement.EnumerateObject())
        {
            if (!TopLevelKeys.Contains(property.Name))
            {
                throw new FormatException($"unknown top-level key '{property.Name}'");
            }
        }
        if (!rootElement.TryGetProperty("name", out JsonElement name) || name.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("missing 'name'");
        }
        _name = name.GetString()!;
        if (!rootElement.TryGetProperty("steps", out JsonElement steps) || steps.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("missing 'steps' array");
        }

        int index = 0;
        foreach (JsonElement step in steps.EnumerateArray())
        {
            try
            {
                foreach (ScenarioCommand command in ScenarioCommand.Parse(step))
                {
                    _commands.Add((index, command));
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException($"step {index}: {ex.Message}");
            }
            index++;
        }
    }

    private string ResolveOutputDir()
    {
        return TestPaths.RunFolder(_args.OutputDir, _name);
    }

    private void Record(int step, ScenarioCommand command, string outcome, string? message, JsonNode? detail)
    {
        var record = new JsonObject
        {
            ["step"] = step,
            ["command"] = command.Name,
            ["frame"] = _executor?.Frame,
            ["input"] = command.Raw.DeepClone(),
            ["outcome"] = outcome
        };
        if (message is not null)
        {
            record["message"] = message;
        }
        if (detail is not null)
        {
            record["detail"] = detail;
        }
        _records.Add(record);
    }

    private void Finish(int exitCode, string? message)
    {
        _finished = true;
        if (_root?.Perf is PerfMonitor perf)
        {
            perf.Sampled -= _perfSamples.Add;
        }
        string result = exitCode switch
        {
            0 => "passed",
            1 => "failed",
            _ => "error"
        };

        var report = new JsonObject
        {
            ["scenario"] = _name,
            ["file"] = _scenarioFile,
            ["output"] = _outputDir,
            ["result"] = result,
            ["exitCode"] = exitCode,
            ["message"] = message,
            ["frames"] = _executor?.Frame,
            ["perf"] = PerfSummary(),
            ["steps"] = _records
        };

        Directory.CreateDirectory(_outputDir);
        string reportPath = Path.Combine(_outputDir, "report.json");
        File.WriteAllText(reportPath, report.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        GD.Print($"SCENARIO {result.ToUpperInvariant()}: {_name}{(message is null ? "" : $" - {message}")}");
        GD.Print($"SCENARIO REPORT: {reportPath}");
        GetTree().Quit(exitCode);
    }

    /// <summary>
    /// The run's performance over every sampled frame: average and worst work time (scripts
    /// plus the renderer's CPU time), its parts, the approximate GPU time, the sim time, the
    /// wall time between frames (the true frame cost when uncapped), average and peak draw
    /// calls and objects, whether it ran uncapped, and whether it ran windowed or headless
    /// (headless has no real renderer, so its numbers are not measurements).
    /// </summary>
    private JsonObject PerfSummary()
    {
        int n = _perfSamples.Count;
        double Avg(System.Func<PerfMonitor.Sample, double> f) => n == 0 ? 0 : Math.Round(_perfSamples.Average(f), 2);
        double Max(System.Func<PerfMonitor.Sample, double> f) => n == 0 ? 0 : Math.Round(_perfSamples.Max(f), 2);
        var gpu = _perfSamples.Where(s => s.GpuMs is not null).Select(s => s.GpuMs!.Value).ToList();
        return new JsonObject
        {
            ["mode"] = TestArgs.IsHeadless ? "headless" : "windowed",
            ["uncapped"] = _root.Perf.Uncapped,
            ["frames"] = n,
            ["budgetMs"] = Math.Round(PerfMonitor.BudgetMs, 2),
            ["workMsAvg"] = Avg(s => s.WorkMs),
            ["workMsWorst"] = Max(s => s.WorkMs),
            ["scriptMsAvg"] = Avg(s => s.ScriptMs),
            ["renderCpuMsAvg"] = Avg(s => s.RenderCpuMs),
            ["gpuMsAvg"] = gpu.Count == 0 ? null : Math.Round(gpu.Average(), 2),
            ["simMsAvg"] = Avg(s => s.SimMs),
            ["simMsWorst"] = Max(s => s.SimMs),
            ["frameMsAvg"] = Avg(s => s.FrameMs),
            ["frameMsWorst"] = Max(s => s.FrameMs),
            ["drawsAvg"] = Avg(s => s.Draws),
            ["drawsPeak"] = Max(s => s.Draws),
            ["objectsAvg"] = Avg(s => s.Objects),
            ["objectsPeak"] = Max(s => s.Objects)
        };
    }
}
