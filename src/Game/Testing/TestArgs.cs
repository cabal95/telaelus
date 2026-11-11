using Godot;

namespace Game.Testing;

/// <summary>
/// Testing options from the user arguments after "--" on the Godot command line:
/// --scenario=&lt;file&gt;, --output=&lt;dir&gt;, --no-screenshots, --control-port=&lt;n&gt;,
/// and --start-paused.
/// </summary>
public sealed class TestArgs
{
    public string? ScenarioPath { get; private init; }
    public string? OutputDir { get; private init; }
    public bool NoScreenshots { get; private init; }
    public int? ControlPort { get; private init; }
    public bool StartPaused { get; private init; }

    public static TestArgs Parse()
    {
        string? scenario = null;
        string? output = null;
        bool noScreenshots = false;
        int? controlPort = null;
        bool startPaused = false;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--scenario="))
            {
                scenario = arg["--scenario=".Length..];
            }
            else if (arg.StartsWith("--output="))
            {
                output = arg["--output=".Length..];
            }
            else if (arg == "--no-screenshots")
            {
                noScreenshots = true;
            }
            else if (arg.StartsWith("--control-port="))
            {
                if (int.TryParse(arg["--control-port=".Length..], out int port) && port is > 0 and < 65536)
                {
                    controlPort = port;
                }
                else
                {
                    GD.PushError($"Invalid {arg}; expected a port number.");
                }
            }
            else if (arg == "--start-paused")
            {
                startPaused = true;
            }
        }

        return new TestArgs
        {
            ScenarioPath = scenario,
            OutputDir = output,
            NoScreenshots = noScreenshots,
            ControlPort = controlPort,
            StartPaused = startPaused
        };
    }

    /// <summary>True when running without a renderer (screenshots impossible).</summary>
    public static bool IsHeadless => DisplayServer.GetName() == "headless";
}
