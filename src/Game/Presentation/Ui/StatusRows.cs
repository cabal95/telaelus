using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// Status rows (ui-style.md): a two-column grid of a 14-unit label in the dim text color
/// and its value. A row set to null is hidden, so rows that do not apply disappear.
/// </summary>
public partial class StatusRows : GridContainer
{
    private readonly Dictionary<string, (Label Name, Label Value)> _rows = new();

    public StatusRows(params string[] rows)
    {
        Columns = 2;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("h_separation", UiTheme.Padding);
        AddThemeConstantOverride("v_separation", 2);
        foreach (string row in rows)
        {
            Label name = UiTheme.MakeLabel(row, UiTheme.SmallSize, UiTheme.TextDim);
            Label value = UiTheme.MakeLabel("", UiTheme.SmallSize);
            value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(name);
            AddChild(value);
            _rows[row] = (name, value);
        }
    }

    /// <summary>Sets a row's value, or hides the row for null.</summary>
    public void Set(string row, string? value)
    {
        (Label name, Label label) = _rows[row];
        name.Visible = value is not null;
        label.Visible = value is not null;
        label.Text = value ?? "";
    }

    /// <summary>The shown rows (label to value), for the snapshot.</summary>
    public IReadOnlyDictionary<string, string> Shown => _rows
        .Where(entry => entry.Value.Value.Visible)
        .ToDictionary(entry => entry.Key, entry => entry.Value.Value.Text);

    /// <summary>A progress bar (ui-style.md): 8 units tall, a cell track with an accent fill.</summary>
    public static ProgressBar MakeProgressBar(float minWidth = 0f)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(minWidth, 8f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.Box(UiTheme.Cell, 3));
        bar.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTheme.Accent, 3));
        return bar;
    }
}
