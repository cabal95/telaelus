using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The Save Game dialog: the existing saves to pick a name from (newest first) and a name field
/// (suggesting the first free "Save n"), with Save. Saving over an existing name asks first.
/// The game stands still while it is open.
/// </summary>
public partial class SaveDialog : Dialog
{
    private LineEdit _name = null!;
    private VBoxContainer _existing = null!;
    private HBoxContainer _confirm = null!;
    private Label _confirmText = null!;

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "saveGame";

    public override bool PausesGame => true;

    /// <summary>Raised with the name to save under (after any overwrite question).</summary>
    public event Action<string>? SaveRequested;

    public string NameText => _name.Text;

    public string? ConfirmText => _confirm.Visible ? _confirmText.Text : null;

    protected override void Build()
    {
        Body.AddChild(UiTheme.MakeLabel("Existing saves", UiTheme.SmallSize, UiTheme.TextDim));
        _existing = new VBoxContainer();
        _existing.AddThemeConstantOverride("separation", 2);
        Body.AddChild(_existing);

        Body.AddChild(UiTheme.MakeLabel("Name", UiTheme.SmallSize, UiTheme.TextDim));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(row);
        _name = new LineEdit
        {
            Name = "Name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _name.TextSubmitted += _ => TrySave();
        row.AddChild(_name);
        Host.Register(Id + "/Name", _name);
        var save = new Button
        {
            Name = "Save",
            Text = "Save",
            FocusMode = FocusModeEnum.None
        };
        save.Pressed += TrySave;
        row.AddChild(UiTheme.SizeToLabel(save));
        Host.Register(Id + "/Save", save);

        _confirm = new HBoxContainer
        {
            Visible = false
        };
        _confirm.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_confirm);
        _confirmText = UiTheme.MakeLabel("", UiTheme.BodySize, UiTheme.Warning);
        _confirm.AddChild(_confirmText);
        var yes = new Button
        {
            Name = "Overwrite",
            Text = "Overwrite",
            FocusMode = FocusModeEnum.None
        };
        yes.Pressed += () =>
        {
            _confirm.Visible = false;
            SaveRequested?.Invoke(_name.Text.Trim());
        };
        _confirm.AddChild(UiTheme.SizeToLabel(yes));
        Host.Register(Id + "/Overwrite", yes);
        var no = new Button
        {
            Name = "Cancel",
            Text = "Cancel",
            FocusMode = FocusModeEnum.None
        };
        no.Pressed += () => _confirm.Visible = false;
        _confirm.AddChild(UiTheme.SizeToLabel(no));
    }

    public override void OnOpened()
    {
        base.OnOpened();
        _confirm.Visible = false;
        _name.Text = SaveStore.NextName();
        foreach (Node child in _existing.GetChildren())
        {
            _existing.RemoveChild(child);
            child.QueueFree();
        }
        List<SaveEntry> saves = SaveStore.List().Where(e => !e.Name.StartsWith(SaveStore.AutosavePrefix)).ToList();
        if (saves.Count == 0)
        {
            _existing.AddChild(UiTheme.MakeLabel("None yet", UiTheme.BodySize, UiTheme.TextDim));
        }
        foreach (SaveEntry entry in saves)
        {
            var pick = new Button
            {
                Text = entry.Name,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left
            };
            pick.Pressed += () => _name.Text = entry.Name;
            _existing.AddChild(pick);
        }
    }

    private void TrySave()
    {
        string name = _name.Text.Trim();
        if (name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            return;
        }
        if (SaveStore.Exists(name))
        {
            _confirmText.Text = $"Overwrite {name}?";
            _confirm.Visible = true;
            return;
        }
        SaveRequested?.Invoke(name);
    }
}

/// <summary>
/// The Load Game dialog: the saves to load (<see cref="SaveList"/>). Loading with unsaved progress
/// asks first. The game stands still while it is open.
/// </summary>
public partial class LoadDialog : Dialog
{
    private SaveList _list = null!;
    private HBoxContainer _confirm = null!;
    private string? _pending;

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "loadGame";

    public override bool PausesGame => true;

    public event Action<string>? LoadRequested;

    public Func<bool> HasUnsavedProgress { get; set; } = () => false;

    public SaveList List => _list;

    public bool Confirming => _confirm.Visible;

    protected override void Build()
    {
        _list = new SaveList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _list.RegisterWith(Id, Host.Register);
        _list.LoadRequested += name =>
        {
            if (HasUnsavedProgress())
            {
                _pending = name;
                _confirm.Visible = true;
                return;
            }
            LoadRequested?.Invoke(name);
        };
        Body.AddChild(_list);

        _confirm = new HBoxContainer
        {
            Visible = false
        };
        _confirm.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_confirm);
        _confirm.AddChild(UiTheme.MakeLabel("Unsaved progress will be lost.", UiTheme.BodySize, UiTheme.Warning));
        var yes = new Button
        {
            Name = "Confirm",
            Text = "Load anyway",
            FocusMode = FocusModeEnum.None
        };
        yes.Pressed += () =>
        {
            _confirm.Visible = false;
            if (_pending is string name)
            {
                LoadRequested?.Invoke(name);
            }
        };
        _confirm.AddChild(UiTheme.SizeToLabel(yes));
        Host.Register(Id + "/Confirm", yes);
        var no = new Button
        {
            Name = "Cancel",
            Text = "Cancel",
            FocusMode = FocusModeEnum.None
        };
        no.Pressed += () => _confirm.Visible = false;
        _confirm.AddChild(UiTheme.SizeToLabel(no));
    }

    public override void OnOpened()
    {
        base.OnOpened();
        _confirm.Visible = false;
        _list.Open();
    }
}
