using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// A segmented picker (ui-style.md): a row of flat cells, one chosen, for mutually exclusive options
/// with short labels where seeing them all helps. Each cell has a 2 unit border, <c>TextDim</c> when
/// not chosen and <c>Accent</c> when chosen, lightening on hover; labels are <c>SmallSize</c> with 8
/// units of side padding, cells 32 tall and 4 apart. Space decides its form: when
/// <see cref="SetOptions"/> finds the cells wider than the room given, it is a dropdown instead, and
/// it keeps that form until the options or the room change. Registered for scenarios at its id
/// (where <c>selectUi</c> picks by label in either form), and each cell at "&lt;id&gt;/&lt;label&gt;".
/// A cell can be disabled (<see cref="SetEnabled"/>: its label dim, clicks ignored, its tooltip still
/// shown on hover) and can carry a small icon after its label (<see cref="SetIcon"/>).
/// </summary>
public partial class SegmentedPicker : HBoxContainer
{
    public const int CellHeight = 32;
    public const int CellPadding = 8;
    public const int CellGap = 4;
    public const int IconSize = 14;
    private const int IconGap = 4;

    private readonly DialogHost _host;
    private readonly string _id;
    private readonly OptionButton _dropdown;
    private readonly List<Button> _cells = new();
    private List<string> _labels = new();
    private int _icons;
    private readonly List<bool> _enabled = new();
    private readonly List<string?> _tips = new();
    private int _selected = -1;

    public SegmentedPicker(DialogHost host, string id)
    {
        _host = host;
        _id = id;
        Name = id[(id.LastIndexOf('/') + 1)..];
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", CellGap);
        _dropdown = new OptionButton
        {
            FocusMode = FocusModeEnum.None,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false
        };
        _dropdown.ItemSelected += index => Pick((int)index);
        AddChild(_dropdown);
        host.Register(id, this);
    }

    /// <summary>Raised with the option's index when the player chooses one (never when code sets <see cref="Selected"/>).</summary>
    public event Action<int>? Chosen;

    /// <summary>Whether it shows as a dropdown, its cells not fitting.</summary>
    public bool IsDropdown => _dropdown.Visible;

    /// <summary>The chosen option's index (-1 for none). Setting it never raises <see cref="Chosen"/>.</summary>
    public int Selected
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                _selected = value;
                ShowSelected();
            }
        }
    }

    /// <summary>The chosen option's label, or null for none.</summary>
    public string? SelectedText => _selected >= 0 && _selected < _labels.Count ? _labels[_selected] : null;

    /// <summary>
    /// The width the cells take in a row: each label at <c>SmallSize</c> plus its padding, the gaps,
    /// and room for a number of trailing icons.
    /// </summary>
    public static float CellsWidth(IReadOnlyList<string> labels, int icons = 0)
    {
        Font font = ThemeDB.FallbackFont;
        return labels.Sum(label => Mathf.Ceil(font.GetStringSize(label, HorizontalAlignment.Left, -1, UiTheme.SmallSize).X) + 2 * CellPadding)
            + CellGap * Math.Max(0, labels.Count - 1) + icons * (IconSize + IconGap);
    }

    /// <summary>
    /// Sets the options, as cells when they fit the width (each label whole, with room for
    /// <paramref name="icons"/> trailing icons) or else as a dropdown. Does nothing when neither the
    /// options nor the form change, so it can run every frame. Rebuilt cells start enabled with no icon.
    /// </summary>
    public void SetOptions(IReadOnlyList<string> labels, float width, int icons = 0)
    {
        bool dropdown = CellsWidth(labels, icons) > width;
        if (labels.SequenceEqual(_labels) && dropdown == IsDropdown && icons == _icons)
        {
            return;
        }
        _icons = icons;
        foreach (Button cell in _cells)
        {
            _host.Unregister(_id + "/" + cell.Text);
            RemoveChild(cell);
            cell.QueueFree();
        }
        _cells.Clear();
        _labels = labels.ToList();
        _enabled.Clear();
        _enabled.AddRange(_labels.Select(_ => true));
        _tips.Clear();
        _tips.AddRange(_labels.Select(_ => (string?)null));
        _dropdown.Clear();
        _dropdown.Visible = dropdown;
        if (dropdown)
        {
            foreach (string label in _labels)
            {
                _dropdown.AddItem(label);
            }
        }
        else
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                int index = i;
                var cell = new Button
                {
                    Name = _labels[i].Replace(" ", ""),
                    Text = _labels[i],
                    FocusMode = FocusModeEnum.None,
                    CustomMinimumSize = new Vector2(0f, CellHeight)
                };
                cell.AddThemeFontSizeOverride("font_size", UiTheme.SmallSize);
                cell.AddThemeColorOverride("font_disabled_color", UiTheme.TextDim);
                cell.AddThemeConstantOverride("icon_max_width", IconSize);
                cell.AddThemeConstantOverride("h_separation", IconGap);
                cell.IconAlignment = HorizontalAlignment.Right;
                cell.Pressed += () => Pick(index);
                cell.MouseEntered += () =>
                {
                    if (_tips[index] is string tip)
                    {
                        _host.ShowHover(cell, new[] { new TipLine(tip) });
                    }
                };
                cell.MouseExited += () => _host.HideHover(cell);
                AddChild(cell);
                _cells.Add(cell);
                _host.Register(_id + "/" + _labels[i], cell);
            }
        }
        _selected = _selected < _labels.Count ? _selected : -1;
        ShowSelected();
    }

    /// <summary>Whether an option can be chosen.</summary>
    public bool IsEnabled(int index) => index >= 0 && index < _enabled.Count && _enabled[index];

    /// <summary>
    /// Enables or disables an option, with a tooltip shown on hover (null for none). A disabled cell's
    /// label is dim and it ignores clicks; a disabled dropdown item cannot be picked.
    /// </summary>
    public void SetEnabled(int index, bool enabled, string? tooltip = null)
    {
        if (index < 0 || index >= _labels.Count)
        {
            return;
        }
        _enabled[index] = enabled;
        _tips[index] = tooltip;
        if (IsDropdown)
        {
            _dropdown.SetItemDisabled(index, !enabled);
            _dropdown.SetItemTooltip(index, tooltip ?? "");
        }
        else
        {
            _cells[index].Disabled = !enabled;
        }
    }

    /// <summary>Shows a small icon after an option's label, tinted (null removes it).</summary>
    public void SetIcon(int index, Texture2D? icon, Color tint)
    {
        if (IsDropdown || index < 0 || index >= _cells.Count)
        {
            return;
        }
        Button cell = _cells[index];
        cell.Icon = icon;
        foreach (string state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_disabled_color" })
        {
            cell.AddThemeColorOverride(state, tint);
        }
    }

    /// <summary>Chooses an option by its label as the player does (raising <see cref="Chosen"/>); false without one or when it is disabled.</summary>
    public bool Choose(string label)
    {
        int index = _labels.IndexOf(label);
        if (index < 0 || !_enabled[index])
        {
            return false;
        }
        Pick(index);
        return true;
    }

    private void Pick(int index)
    {
        Selected = index;
        Chosen?.Invoke(index);
    }

    private void ShowSelected()
    {
        if (IsDropdown)
        {
            _dropdown.Selected = _selected;
            return;
        }
        for (int i = 0; i < _cells.Count; i++)
        {
            (StyleBoxFlat normal, StyleBoxFlat hover) = i == _selected ? ChosenStyle : NotChosenStyle;
            _cells[i].AddThemeStyleboxOverride("normal", normal);
            _cells[i].AddThemeStyleboxOverride("hover", hover);
            _cells[i].AddThemeStyleboxOverride("pressed", normal);
            _cells[i].AddThemeStyleboxOverride("hover_pressed", hover);
            _cells[i].AddThemeStyleboxOverride("disabled", DisabledStyle);
        }
    }

    private static (StyleBoxFlat Normal, StyleBoxFlat Hover)? _chosenStyle;
    private static (StyleBoxFlat Normal, StyleBoxFlat Hover)? _notChosenStyle;

    private static (StyleBoxFlat, StyleBoxFlat) ChosenStyle => _chosenStyle ??= (CellBox(UiTheme.Accent), CellBox(UiTheme.Accent.Lightened(0.35f)));

    private static StyleBoxFlat? _disabledStyle;

    /// <summary>A disabled cell: its border darker than a cell not chosen.</summary>
    public static StyleBoxFlat DisabledStyle => _disabledStyle ??= CellBox(UiTheme.TextDim.Darkened(0.45f));

    private static (StyleBoxFlat, StyleBoxFlat) NotChosenStyle => _notChosenStyle ??= (CellBox(UiTheme.TextDim), CellBox(UiTheme.TextDim.Lightened(0.35f)));

    private static StyleBoxFlat CellBox(Color edge)
    {
        StyleBoxFlat box = UiTheme.Box(Colors.Transparent, UiTheme.CellRadius, edge, UiTheme.OutlineWidth);
        box.ContentMarginLeft = CellPadding;
        box.ContentMarginRight = CellPadding;
        return box;
    }
}
