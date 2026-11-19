using System.Collections.Generic;
using System.Text;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>One tooltip line: its text, and an optional color (the body text color when null).</summary>
public readonly record struct TipLine(string Text, Color? Color = null)
{
    /// <summary>A long text as lines of at most about a width in characters, broken between words.</summary>
    public static IEnumerable<TipLine> Wrapped(string text, Color? color = null, int width = 44)
    {
        var line = new StringBuilder();
        foreach (string word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return new TipLine(line.ToString(), color);
                line.Clear();
            }
            if (line.Length > 0)
            {
                line.Append(' ');
            }
            line.Append(word);
        }
        if (line.Length > 0)
        {
            yield return new TipLine(line.ToString(), color);
        }
    }
}

/// <summary>
/// A small panel of text lines that never takes the mouse. Used for hover tooltips and
/// the placement tooltip. Placed by its owner in UI units, kept inside the window.
/// </summary>
public partial class Tooltip : PanelContainer
{
    private readonly RichTextLabel _text = new()
    {
        BbcodeEnabled = true,
        FitContent = true,
        AutowrapMode = TextServer.AutowrapMode.Off,
        ScrollActive = false,
        MouseFilter = MouseFilterEnum.Ignore
    };

    /// <summary>The lines last shown, as plain text (for the snapshot).</summary>
    public IReadOnlyList<string> Lines => _lines;
    private readonly List<string> _lines = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        AddThemeStyleboxOverride("panel", UiTheme.PanelBox(UiTheme.Gap));
        _text.AddThemeFontSizeOverride("normal_font_size", UiTheme.SmallSize);
        AddChild(_text);
    }

    /// <summary>Shows the lines with the panel's top-left at a point, nudged to stay in the window.</summary>
    public void ShowAt(Vector2 point, IReadOnlyList<TipLine> lines, Vector2 uiSize)
    {
        SetLines(lines);
        Visible = true;
        Size = Vector2.Zero;
        Vector2 size = GetCombinedMinimumSize();
        Vector2 position = point;
        position.X = Mathf.Min(position.X, uiSize.X - size.X - UiTheme.Gap);
        position.Y = Mathf.Min(position.Y, uiSize.Y - size.Y - UiTheme.Gap);
        Position = position.Max(new Vector2(UiTheme.Gap, UiTheme.Gap)).Floor();
    }

    public void HideTip()
    {
        Visible = false;
        _lines.Clear();
    }

    private void SetLines(IReadOnlyList<TipLine> lines)
    {
        _lines.Clear();
        var text = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            TipLine line = lines[i];
            _lines.Add(line.Text);
            if (i > 0)
            {
                text.Append('\n');
            }
            string escaped = line.Text.Replace("[", "[lb]");
            if (line.Color is Color color)
            {
                text.Append("[color=#").Append(UiTheme.Hex(color)).Append(']').Append(escaped).Append("[/color]");
            }
            else
            {
                text.Append(escaped);
            }
        }
        _text.Text = text.ToString();
    }
}
