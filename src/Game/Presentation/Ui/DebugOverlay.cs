using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The debug overlay (F3, hidden at start): the world line (the seed or the bounded map's
/// name, the biome and terrain under the player, their chunk, and their map cell) and the
/// performance line (colored by headroom), in outlined text in the top-left corner below
/// the top bar. It is its own UI layer behind everything else, never takes the mouse, and
/// is ignored when laying out other UI, which may cover it.
/// </summary>
public partial class DebugOverlay : UiLayer
{
    private static readonly Color Good = new(0.55f, 0.92f, 0.55f);
    private static readonly Color Tight = new(0.98f, 0.85f, 0.35f);
    private static readonly Color Over = new(1.0f, 0.42f, 0.38f);

    private Label _world = null!;
    private Label _perf = null!;
    private VBoxContainer _lines = null!;

    /// <summary>Whether the overlay is shown (F3 toggles it).</summary>
    public bool Shown
    {
        get => _lines.Visible;
        set => _lines.Visible = value;
    }

    public override void _Ready()
    {
        Layer = 1;
        base._Ready();
        _lines = new VBoxContainer
        {
            Position = new Vector2(UiTheme.Padding, TopBar.Height + UiTheme.Gap),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _lines.AddThemeConstantOverride("separation", 0);
        Root.AddChild(_lines);
        _world = MakeLabel();
        _perf = MakeLabel();
        _lines.AddChild(_world);
        _lines.AddChild(_perf);
    }

    private static Label MakeLabel()
    {
        var label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", UiTheme.Text);
        // A dark outline keeps the text readable over bright ground such as snow and sand.
        label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.06f, 0.05f));
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    public void Refresh(Simulation sim, PerfMonitor perf)
    {
        if (!Shown)
        {
            return;
        }
        _world.Text = WorldLine(sim);

        // The frame's headline time against the budget (green under 50%, yellow to 80%, red
        // above): the larger of the CPU and GPU times (see PerfMonitor). Then the CPU time and
        // its parts (scripts, the sim's share of them, and the renderer's CPU time), the GPU
        // time, the worst frame in the last second, fps, draw calls, and the renderer. On the
        // Compatibility renderer the GPU time is only meaningful uncapped.
        PerfMonitor.Sample avg = perf.Average;
        double share = avg.HeadlineMs / PerfMonitor.BudgetMs;
        string gpu = !perf.GpuMeasured && !perf.Uncapped ? "(uncapped only)"
            : avg.GpuMs is double g ? g.ToString("F1") : "n/a";
        _perf.Text = $"frame {avg.HeadlineMs:F1} / {PerfMonitor.BudgetMs:F1} ms ({share * 100:F0}%)   " +
            $"cpu {avg.WorkMs:F1} (scripts {avg.ScriptMs:F1}, sim {avg.SimMs:F1}, render {avg.RenderCpuMs:F1})   gpu {gpu}   " +
            $"worst {perf.WorstMs:F1}   fps {avg.Fps:F0}   draws {avg.Draws}   {perf.Renderer}";
        _perf.AddThemeColorOverride("font_color", share < 0.5 ? Good : share < 0.8 ? Tight : Over);
    }

    /// <summary>
    /// The world line for checking the world while exploring: the world (its seed, or the
    /// bounded map's name), the biome and terrain under the player, and their chunk and
    /// map cell.
    /// </summary>
    public static string WorldLine(Simulation sim)
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
}
