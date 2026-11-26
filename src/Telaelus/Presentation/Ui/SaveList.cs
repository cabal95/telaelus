using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The saves, newest first, as rows to pick from: each its thumbnail, its name, when it was saved and
/// how long it was played, and a summary. A save that cannot be loaded is dimmed with the reason.
/// Under the rows, Load and Delete act on the selected one (Delete asks first). Used by the in-game
/// Load dialog and the main menu.
/// </summary>
public partial class SaveList : VBoxContainer
{
    private const float ThumbWidth = 128f;
    private const float ThumbHeight = 72f;

    private readonly VBoxContainer _rows = new();
    private readonly Label _empty = UiTheme.MakeLabel("No saves yet", UiTheme.BodySize, UiTheme.TextDim);
    private readonly Button _load = new();
    private readonly Button _delete = new();
    private readonly HBoxContainer _confirm = new();
    private readonly Label _confirmText = UiTheme.MakeLabel("", UiTheme.BodySize, UiTheme.Warning);
    private readonly List<(SaveEntry Entry, PanelContainer Row, StyleBoxFlat Style)> _entries = new();
    private Action<string, Control>? _register;

    /// <summary>Raised with a save's name when Load is pressed on it (or a row double-clicked).</summary>
    public event Action<string>? LoadRequested;

    public string? Selected { get; private set; }

    /// <summary>The rows as shown (name, whether loadable), for the snapshot.</summary>
    public IReadOnlyList<(string Name, bool Loadable)> Rows => _entries.Select(e => (e.Entry.Name, e.Entry.Loadable)).ToList();

    public SaveList()
    {
        AddThemeConstantOverride("separation", UiTheme.Gap);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        AddChild(scroll);
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_rows);
        AddChild(_empty);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", UiTheme.Gap);
        AddChild(buttons);
        _load.Name = "Load";
        _load.Text = "Load";
        _load.FocusMode = FocusModeEnum.None;
        _load.Pressed += () =>
        {
            if (Selected is string name)
            {
                LoadRequested?.Invoke(name);
            }
        };
        buttons.AddChild(UiTheme.SizeToLabel(_load));
        _delete.Name = "Delete";
        _delete.Text = "Delete";
        _delete.FocusMode = FocusModeEnum.None;
        _delete.Pressed += () =>
        {
            _confirmText.Text = $"Delete {Selected}? It cannot be undone.";
            _confirm.Visible = true;
        };
        buttons.AddChild(UiTheme.SizeToLabel(_delete));

        _confirm.AddThemeConstantOverride("separation", UiTheme.Gap);
        _confirm.Visible = false;
        AddChild(_confirm);
        _confirm.AddChild(_confirmText);
        var yes = new Button
        {
            Name = "ConfirmDelete",
            Text = "Delete",
            FocusMode = FocusModeEnum.None
        };
        yes.Pressed += () =>
        {
            if (Selected is string name)
            {
                SaveStore.Delete(name);
            }
            _confirm.Visible = false;
            Refresh();
        };
        _confirm.AddChild(UiTheme.SizeToLabel(yes));
        var no = new Button
        {
            Name = "CancelDelete",
            Text = "Keep",
            FocusMode = FocusModeEnum.None
        };
        no.Pressed += () => _confirm.Visible = false;
        _confirm.AddChild(UiTheme.SizeToLabel(no));
        _deleteYes = yes;
        _deleteNo = no;
    }

    private readonly Button _deleteYes;
    private readonly Button _deleteNo;

    private static string Count(int n, string noun)
    {
        return $"{n} {noun}{(n == 1 ? "" : "s")}";
    }

    /// <summary>Registers the buttons under an id prefix (for scenarios' clickUi), as "loadGame/Load".</summary>
    public void RegisterWith(string prefix, Action<string, Control> register)
    {
        _register = (id, control) => register(prefix + "/" + id, control);
        _register("Load", _load);
        _register("Delete", _delete);
        _register("ConfirmDelete", _deleteYes);
        _register("CancelDelete", _deleteNo);
    }

    /// <summary>Lists the saves again; keeps the selection when it is still there, else selects the newest.</summary>
    public void Refresh()
    {
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        _entries.Clear();
        List<SaveEntry> saves = SaveStore.List();
        foreach (SaveEntry entry in saves)
        {
            (PanelContainer row, StyleBoxFlat style) = MakeRow(entry);
            _rows.AddChild(row);
            _entries.Add((entry, row, style));
            _register?.Invoke("row/" + entry.Name, row);
        }
        if (Selected is null || saves.All(s => s.Name != Selected))
        {
            Selected = saves.FirstOrDefault()?.Name;
        }
        _empty.Visible = saves.Count == 0;
        _confirm.Visible = false;
        UpdateSelection();
    }

    private (PanelContainer, StyleBoxFlat) MakeRow(SaveEntry entry)
    {
        StyleBoxFlat style = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: 6);
        var row = new PanelContainer
        {
            Name = entry.Name,
            MouseFilter = MouseFilterEnum.Stop
        };
        row.AddThemeStyleboxOverride("panel", style);
        var line = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        line.AddThemeConstantOverride("separation", UiTheme.Gap);
        row.AddChild(line);
        line.AddChild(new TextureRect
        {
            Texture = SaveStore.Thumbnail(entry.Name),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            CustomMinimumSize = new Vector2(ThumbWidth, ThumbHeight),
            MouseFilter = MouseFilterEnum.Ignore
        });
        var text = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        text.AddThemeConstantOverride("separation", 2);
        line.AddChild(text);
        text.AddChild(UiTheme.MakeLabel(entry.Name, UiTheme.BodySize));
        if (entry.Header is { } header && entry.Problem is null)
        {
            text.AddChild(UiTheme.MakeLabel($"Saved {When(header.SavedAt)}   Played {SaveStore.PlayTime(header.PlayTimeSeconds)}",
                UiTheme.SmallSize, UiTheme.TextDim));
            text.AddChild(UiTheme.MakeLabel($"{Count(header.Buildings, "building")}   {Count(header.ShipsLaunched, "ship")} launched",
                UiTheme.SmallSize, UiTheme.TextDim));
        }
        else
        {
            text.AddChild(UiTheme.MakeLabel(entry.Problem ?? "Cannot be loaded", UiTheme.SmallSize, UiTheme.Warning));
            row.Modulate = new Color(1f, 1f, 1f, 0.6f);
        }
        row.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
            {
                row.AcceptEvent();
                Selected = entry.Name;
                UpdateSelection();
                if (click.DoubleClick && entry.Loadable)
                {
                    LoadRequested?.Invoke(entry.Name);
                }
            }
        };
        return (row, style);
    }

    private void UpdateSelection()
    {
        SaveEntry? chosen = null;
        foreach ((SaveEntry entry, PanelContainer _, StyleBoxFlat style) in _entries)
        {
            bool selected = entry.Name == Selected;
            style.BorderColor = UiTheme.Accent;
            style.SetBorderWidthAll(selected ? UiTheme.OutlineWidth : 0);
            style.BgColor = selected ? UiTheme.CellHover : UiTheme.Cell;
            if (selected)
            {
                chosen = entry;
            }
        }
        _load.Disabled = chosen is not { Loadable: true };
        _delete.Disabled = chosen is null;
    }

    private static string When(string savedAt)
    {
        return DateTime.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)
            ? time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : savedAt;
    }
}
