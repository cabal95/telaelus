using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// One status strip entry: an icon, an optional short value beside it ("-10", "53%", "22 s"), the
/// tone that colors it, and the tooltip's full sentence. With a value the value carries the tone;
/// without one the icon does. A faint entry draws its icon at a third of its opacity, for an empty
/// slot (a Depot's empty bay). <see cref="Id"/> names it for the snapshot.
/// </summary>
public readonly record struct StatusEntry(string Id, Texture2D? Icon, string? Value, Tone Tone, string Tip, bool Faint = false);

/// <summary>
/// The status strip (ui-style.md): icon-value entries with tooltips, for states and quantities the
/// player checks at a glance. It runs as a row that wraps to a further line past its width, or as a
/// column that wraps into a further column past its height (the wrap length, its minimum height).
/// Fixed controls added first (an <see cref="IconToggle"/>) stay ahead of the entries. It only lays
/// out and recolors: <see cref="Show"/> reuses the entry nodes, so it can run every frame.
/// </summary>
public partial class StatusStrip : FlowContainer
{
    public const float IconSize = 20f;

    private readonly DialogHost _host;
    private readonly float _iconSize;
    private readonly List<(HBoxContainer Box, TextureRect Icon, Label Value)> _nodes = new();
    private readonly List<StatusEntry> _shown = new();

    public StatusStrip(DialogHost host, bool column, float wrapLength, float iconSize = IconSize)
    {
        _host = host;
        _iconSize = iconSize;
        Vertical = column;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = column ? new Vector2(0f, wrapLength) : new Vector2(wrapLength, 0f);
        AddThemeConstantOverride("h_separation", UiTheme.Gap);
        AddThemeConstantOverride("v_separation", UiTheme.Gap);
    }

    /// <summary>The entries as last shown, in order, for the snapshot.</summary>
    public IReadOnlyList<StatusEntry> Shown => _shown;

    /// <summary>Shows these entries in order, reusing the nodes of the last call.</summary>
    public void Show(IReadOnlyList<StatusEntry> entries)
    {
        _shown.Clear();
        _shown.AddRange(entries);
        while (_nodes.Count < entries.Count)
        {
            _nodes.Add(MakeNode(_nodes.Count));
        }
        for (int i = 0; i < _nodes.Count; i++)
        {
            (HBoxContainer box, TextureRect icon, Label value) = _nodes[i];
            box.Visible = i < entries.Count;
            if (!box.Visible)
            {
                continue;
            }
            StatusEntry entry = entries[i];
            Color color = UiTheme.ColorOf(entry.Tone);
            icon.Texture = entry.Icon;
            Color tint = entry.Value is null ? color : entry.Tone == Tone.Dim ? UiTheme.TextDim : Colors.White;
            icon.Modulate = entry.Faint ? tint with { A = 0.35f } : tint;
            value.Visible = entry.Value is not null;
            value.Text = entry.Value ?? "";
            value.AddThemeColorOverride("font_color", color);
        }
    }

    private (HBoxContainer, TextureRect, Label) MakeNode(int index)
    {
        var box = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        box.AddThemeConstantOverride("separation", 3);
        TextureRect icon = HudOverlay.MakeIcon(_iconSize);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        box.AddChild(icon);
        Label value = UiTheme.MakeLabel("", UiTheme.SmallSize);
        value.VerticalAlignment = VerticalAlignment.Center;
        box.AddChild(value);
        box.MouseEntered += () =>
        {
            if (index < _shown.Count)
            {
                _host.ShowHover(box, TipLine.Wrapped(_shown[index].Tip).ToArray());
            }
        };
        box.MouseExited += () => _host.HideHover(box);
        AddChild(box);
        return (box, icon, value);
    }
}
