using System.Collections.Generic;
using System.Text;
using Game.Presentation.Ui;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Minimal heads-up display: resource totals (deposits on one line, refined goods on
/// the next), the diagnostics line, the performance line (colored by headroom; F3
/// hides it), launch progress, and a harvest hint with its progress bar. Drawn at the
/// UI scale. Pure presentation; it only reads simulation state and the performance
/// monitor. Building lives in the build menu, not here.
/// </summary>
public partial class Hud : UiLayer
{
    private static readonly Color TextColor = new(0.92f, 0.94f, 0.92f);
    private static readonly Color Good = new(0.55f, 0.92f, 0.55f);
    private static readonly Color Tight = new(0.98f, 0.85f, 0.35f);
    private static readonly Color Over = new(1.0f, 0.42f, 0.38f);

    private Label _label = null!;
    private Label _perfLabel = null!;
    private Label _lowerLabel = null!;

    public bool ShowPerf
    {
        get => _perfLabel.Visible;
        set => _perfLabel.Visible = value;
    }

    public override void _Ready()
    {
        base._Ready();
        var lines = new VBoxContainer
        {
            Position = new Vector2(16f, 12f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        lines.AddThemeConstantOverride("separation", 0);
        Root.AddChild(lines);
        _label = MakeLabel();
        _perfLabel = MakeLabel();
        _lowerLabel = MakeLabel();
        lines.AddChild(_label);
        lines.AddChild(_perfLabel);
        lines.AddChild(_lowerLabel);
    }

    private static Label MakeLabel()
    {
        var label = new Label();
        label.AddThemeColorOverride("font_color", TextColor);
        // A dark outline keeps the text readable over bright ground such as snow and sand.
        label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.06f, 0.05f));
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    /// <summary>
    /// Shows the performance line: the frame's work time against the budget (colored
    /// green under 50%, yellow to 80%, red above), then its parts (scripts, with the
    /// sim's share, and the renderer's CPU time), the approximate GPU time, the worst
    /// frame in the last second, fps, and draw calls.
    /// </summary>
    public void RefreshPerf(PerfMonitor perf)
    {
        PerfMonitor.Sample avg = perf.Average;
        double share = avg.WorkMs / PerfMonitor.BudgetMs;
        // Under vsync Godot's GPU time includes the wait for the refresh, so it is only
        // shown when running uncapped.
        string gpu = !perf.Uncapped ? "(uncapped only)" : avg.GpuMs is double g ? "~" + g.ToString("F1") : "n/a";
        _perfLabel.Text = $"frame {avg.WorkMs:F1} / {PerfMonitor.BudgetMs:F1} ms ({share * 100:F0}%)   " +
            $"scripts {avg.ScriptMs:F1} (sim {avg.SimMs:F1})  render {avg.RenderCpuMs:F1}  gpu {gpu}   " +
            $"worst {perf.WorstMs:F1}   fps {avg.Fps:F0}   draws {avg.Draws}";
        _perfLabel.AddThemeColorOverride("font_color", share < 0.5 ? Good : share < 0.8 ? Tight : Over);
    }

    private const int ProgressBarWidth = 10;

    public void Refresh(Simulation sim)
    {
        IReadOnlyDictionary<ResourceType, float> stockpile = sim.Stockpile;
        ResourceField? harvestTarget = sim.HarvestTarget;
        bool canLaunch = sim.CanLaunchShip();
        int shipsLaunched = sim.ShipsLaunched;
        string diagnostics = Diagnostics(sim);

        var text = new StringBuilder();
        AppendLine(text, stockpile, ResourceKind.Deposit);
        text.Append('\n');
        AppendLine(text, stockpile, ResourceKind.Refined);
        text.Append('\n').Append(diagnostics);
        _label.Text = text.ToString();
        text.Clear();

        text.Append("Launch: Rocket Fuel ").Append(Amount(stockpile, ResourceType.RocketFuel)).Append('/').Append((int)Simulation.LaunchRocketFuelCost);
        text.Append("   Iron ").Append(Amount(stockpile, ResourceType.Iron)).Append('/').Append((int)Simulation.LaunchIronCost);
        if (canLaunch)
        {
            text.Append("   [L] LAUNCH SHIP");
        }
        if (shipsLaunched > 0)
        {
            text.Append("   (launched: ").Append(shipsLaunched).Append(')');
        }

        if (harvestTarget is not null)
        {
            text.Append("\n[E] harvest ").Append(ResourceDefs.For(harvestTarget.Type).Name).Append("  ");
            AppendBar(text, sim.HarvestProgress);
        }

        _lowerLabel.Text = text.ToString();
    }

    /// <summary>A text progress bar, as "[####------]".</summary>
    private static void AppendBar(StringBuilder text, float progress)
    {
        int filled = Mathf.Clamp(Mathf.FloorToInt(progress * ProgressBarWidth), 0, ProgressBarWidth);
        text.Append('[').Append('#', filled).Append('-', ProgressBarWidth - filled).Append(']');
    }

    /// <summary>
    /// One line of totals for every resource of a kind, in table order. Terrain-sourced
    /// resources are left out until a building can collect them.
    /// </summary>
    private static void AppendLine(StringBuilder text, IReadOnlyDictionary<ResourceType, float> stockpile, ResourceKind kind)
    {
        bool first = true;
        foreach (ResourceType type in System.Enum.GetValues<ResourceType>())
        {
            ResourceDef def = ResourceDefs.For(type);
            if (def.Kind != kind)
            {
                continue;
            }
            text.Append(first ? "" : "   ").Append(def.Name).Append(": ").Append(Amount(stockpile, type));
            first = false;
        }
    }

    /// <summary>
    /// The diagnostics line for checking the world while exploring: the world (its seed,
    /// or the bounded map's name), the biome and terrain under the player, and their
    /// chunk and map cell.
    /// </summary>
    public static string Diagnostics(Simulation sim)
    {
        (int x, int z) = Simulation.CellAt(sim.Player.Position.X, sim.Player.Position.Z);
        int mapX = Simulation.MapCellOf(x);
        int mapZ = Simulation.MapCellOf(z);
        (int chunkX, int chunkZ) = Simulation.ChunkAt(sim.Player.Position);
        string world = sim.World.Kind switch
        {
            WorldKind.Generated => "seed " + sim.World.Seed,
            WorldKind.TestMap => "test map",
            _ => "empty map"
        };
        return $"{world}   {sim.MapBiomeAt(mapX, mapZ)} / {sim.MapTerrainAt(mapX, mapZ)}   chunk ({chunkX}, {chunkZ})   cell ({mapX}, {mapZ})";
    }

    private static int Amount(IReadOnlyDictionary<ResourceType, float> stockpile, ResourceType type)
    {
        stockpile.TryGetValue(type, out float value);
        return Mathf.FloorToInt(value);
    }
}
