using Godot;

namespace Game.Testing;

/// <summary>
/// Testing options from the user arguments after "--" on the Godot command line:
/// --scenario=&lt;file&gt;, --output=&lt;dir&gt;, and --no-screenshots.
/// </summary>
public sealed class TestArgs
{
    public string? ScenarioPath { get; private init; }
    public string? OutputDir { get; private init; }
    public bool NoScreenshots { get; private init; }

    public static TestArgs Parse()
    {
        string? scenario = null;
        string? output = null;
        bool noScreenshots = false;
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
        }

        return new TestArgs
        {
            ScenarioPath = scenario,
            OutputDir = output,
            NoScreenshots = noScreenshots
        };
    }

    /// <summary>True when running without a renderer (screenshots impossible).</summary>
    public static bool IsHeadless => DisplayServer.GetName() == "headless";
}
