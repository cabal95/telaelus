using System;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// The player's own settings, kept across games in <c>user://settings.cfg</c>. Test runs (a scenario
/// or the live control channel) neither read nor write it, so they start the same every time.
/// </summary>
public static class UserSettings
{
    private const string Path = "user://settings.cfg";

    /// <summary>Whether this is a test run (<c>--scenario=</c> or <c>--control-port=</c>).</summary>
    public static bool TestRun { get; } = Array.Exists(OS.GetCmdlineUserArgs(),
        a => a.StartsWith("--scenario=") || a.StartsWith("--control-port="));

    /// <summary>Whether a New Game has already asked to start the trainer.</summary>
    public static bool TrainerAsked
    {
        get => !TestRun && Read().GetValue("trainer", "asked", false).AsBool();
        set
        {
            if (TestRun)
            {
                return;
            }
            ConfigFile config = Read();
            config.SetValue("trainer", "asked", value);
            config.Save(Path);
        }
    }

    private static ConfigFile Read()
    {
        var config = new ConfigFile();
        config.Load(Path);
        return config;
    }
}
