using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// An arrow picker (ui-style.md): a left arrow, the current choice, and a right arrow, for stepping
/// through a short ordered list in little space. The current choice is drawn like a segmented
/// picker's chosen cell and is as wide as its widest choice, shorter ones centered, so the arrows
/// never move. The arrows step to the previous or next visible choice; at either end that arrow is
/// disabled (dim), and nothing wraps. Registered for scenarios at its id, with "&lt;id&gt;/prev",
/// "&lt;id&gt;/next", and "&lt;id&gt;/current".
/// </summary>
public partial class ArrowPicker : HBoxContainer
{
    private readonly Button _prev;
    private readonly Button _next;
    private readonly PanelContainer _current;
    private readonly Label _label;
    private List<string> _labels = new();
    private readonly List<bool> _visible = new();
    private int _selected = -1;

    public ArrowPicker(DialogHost host, string id)
    {
        Name = id[(id.LastIndexOf('/') + 1)..];
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", SegmentedPicker.CellGap);
        _prev = MakeArrow("Prev", "<", -1);
        AddChild(_prev);
        _current = new PanelContainer
        {
            Name = "Current",
            CustomMinimumSize = new Vector2(0f, SegmentedPicker.CellHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        StyleBoxFlat box = UiTheme.Box(Colors.Transparent, UiTheme.CellRadius, UiTheme.Accent, UiTheme.OutlineWidth);
        box.ContentMarginLeft = SegmentedPicker.CellPadding;
        box.ContentMarginRight = SegmentedPicker.CellPadding;
        _current.AddThemeStyleboxOverride("panel", box);
        AddChild(_current);
        _label = UiTheme.MakeLabel("", UiTheme.SmallSize);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _current.AddChild(_label);
        _next = MakeArrow("Next", ">", 1);
        AddChild(_next);
        host.Register(id, this);
        host.Register(id + "/prev", _prev);
        host.Register(id + "/next", _next);
        host.Register(id + "/current", _current);
    }

    /// <summary>Raised with the choice's index when the player steps to it (never when code sets <see cref="Selected"/>).</summary>
    public event Action<int>? Chosen;

    /// <summary>The current choice's index (-1 for none). Setting it never raises <see cref="Chosen"/>.</summary>
    public int Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            ShowChoice();
        }
    }

    /// <summary>The current choice's label, or null for none.</summary>
    public string? SelectedText => _selected >= 0 && _selected < _labels.Count ? _labels[_selected] : null;

    /// <summary>The current choice's cell width and the right arrow's x within the picker, for the snapshot (both fixed across choices).</summary>
    public (float CurrentWidth, float NextX) Layout => (_current.Size.X, _next.Position.X);

    /// <summary>Whether an arrow can step that way (-1 back, 1 forward).</summary>
    public bool CanStep(int direction) => Neighbor(direction) >= 0;

    /// <summary>The width of the current choice's cell: the widest label at <c>SmallSize</c> plus its padding.</summary>
    public static float ChoiceWidth(IReadOnlyList<string> labels)
    {
        Font font = ThemeDB.FallbackFont;
        return labels.Max(label => Mathf.Ceil(font.GetStringSize(label, HorizontalAlignment.Left, -1, UiTheme.SmallSize).X))
            + 2 * SegmentedPicker.CellPadding;
    }

    /// <summary>Sets the choices, all visible, and sizes the current cell to the widest. Does nothing when they are unchanged.</summary>
    public void SetOptions(IReadOnlyList<string> labels)
    {
        if (labels.SequenceEqual(_labels))
        {
            return;
        }
        _labels = labels.ToList();
        _visible.Clear();
        _visible.AddRange(_labels.Select(_ => true));
        _current.CustomMinimumSize = new Vector2(ChoiceWidth(_labels), SegmentedPicker.CellHeight);
        _selected = _selected < _labels.Count ? _selected : -1;
        ShowChoice();
    }

    /// <summary>Shows or hides a choice; the arrows skip hidden ones. Hiding the current choice keeps it current.</summary>
    public void SetVisible(int index, bool visible)
    {
        if (index >= 0 && index < _visible.Count && _visible[index] != visible)
        {
            _visible[index] = visible;
            ShowChoice();
        }
    }

    /// <summary>Steps as an arrow does (raising <see cref="Chosen"/>); false when there is nothing that way.</summary>
    public bool Step(int direction)
    {
        int index = Neighbor(direction);
        if (index < 0)
        {
            return false;
        }
        Selected = index;
        Chosen?.Invoke(index);
        return true;
    }

    private int Neighbor(int direction)
    {
        for (int i = _selected + direction; i >= 0 && i < _labels.Count; i += direction)
        {
            if (_visible[i])
            {
                return i;
            }
        }
        return -1;
    }

    private Button MakeArrow(string name, string text, int direction)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(SegmentedPicker.CellHeight, SegmentedPicker.CellHeight)
        };
        button.AddThemeFontSizeOverride("font_size", UiTheme.SmallSize);
        button.AddThemeColorOverride("font_disabled_color", UiTheme.TextDim);
        StyleBoxFlat normal = UiTheme.Box(Colors.Transparent, UiTheme.CellRadius, UiTheme.TextDim, UiTheme.OutlineWidth);
        StyleBoxFlat hover = UiTheme.Box(Colors.Transparent, UiTheme.CellRadius, UiTheme.TextDim.Lightened(0.35f), UiTheme.OutlineWidth);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("hover_pressed", hover);
        button.AddThemeStyleboxOverride("disabled", SegmentedPicker.DisabledStyle);
        button.Pressed += () => Step(direction);
        return button;
    }

    private void ShowChoice()
    {
        _label.Text = SelectedText ?? "";
        _prev.Disabled = !CanStep(-1);
        _next.Disabled = !CanStep(1);
    }
}
