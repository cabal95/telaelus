using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The in-game menu (Escape when nothing else uses it): a modal titled "Paused" that stops the game,
/// with Resume, Save Game, Load Game, Main Menu, and Quit. Main Menu and Quit ask first when there is
/// unsaved progress. A line under the buttons says what just happened ("Saved as Save 2").
/// </summary>
public partial class GameMenu : Dialog
{
    private readonly List<Button> _buttons = new();
    private VBoxContainer _actions = null!;
    private VBoxContainer _confirm = null!;
    private Label _confirmText = null!;
    private Label _status = null!;
    private Action? _confirmed;

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "gameMenu";

    public override bool PausesGame => true;

    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;

    /// <summary>Whether leaving should ask first (unsaved progress).</summary>
    public Func<bool> HasUnsavedProgress { get; set; } = () => false;

    /// <summary>The buttons as shown, for the snapshot.</summary>
    public IReadOnlyList<string> ButtonTexts => _actions.Visible ? _buttons.Select(b => b.Text).ToList() : new List<string>();

    /// <summary>The question being asked, or null, for the snapshot.</summary>
    public string? ConfirmText => _confirm.Visible ? _confirmText.Text : null;

    public string StatusText => _status.Text;

    protected override void Build()
    {
        // Resume does what Close does.
        CloseButton.Visible = false;
        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_actions);
        AddButton("Resume", () => Host.Close(this));
        AddButton("Save Game", () => SaveRequested?.Invoke());
        AddButton("Load Game", () => LoadRequested?.Invoke());
        AddButton("Main Menu", () => Leave(() => MainMenuRequested?.Invoke()));
        AddButton("Quit", () => Leave(() => QuitRequested?.Invoke()));

        _confirm = new VBoxContainer
        {
            Visible = false
        };
        _confirm.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_confirm);
        _confirmText = UiTheme.MakeLabel("", UiTheme.BodySize, UiTheme.Warning);
        _confirmText.HorizontalAlignment = HorizontalAlignment.Center;
        _confirm.AddChild(_confirmText);
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", UiTheme.Gap);
        _confirm.AddChild(row);
        var yes = new Button
        {
            Name = "Confirm",
            Text = "Leave without saving",
            FocusMode = FocusModeEnum.None
        };
        yes.Pressed += () =>
        {
            Action? then = _confirmed;
            ShowActions();
            then?.Invoke();
        };
        row.AddChild(yes);
        Host.Register(Id + "/Confirm", yes);
        var no = new Button
        {
            Name = "Cancel",
            Text = "Cancel",
            FocusMode = FocusModeEnum.None
        };
        no.Pressed += ShowActions;
        row.AddChild(no);
        Host.Register(Id + "/Cancel", no);

        _status = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Body.AddChild(_status);
    }

    private void AddButton(string text, Action pressed)
    {
        var button = new Button
        {
            Name = text.Replace(" ", ""),
            Text = text,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(240f, 40f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        button.Pressed += pressed;
        _actions.AddChild(button);
        _buttons.Add(button);
        Host.Register(Id + "/" + button.Name, button);
    }

    private void Leave(Action leave)
    {
        if (!HasUnsavedProgress())
        {
            leave();
            return;
        }
        _confirmText.Text = "Unsaved progress will be lost.";
        _confirmed = leave;
        _actions.Visible = false;
        _confirm.Visible = true;
    }

    private void ShowActions()
    {
        _confirmed = null;
        _confirm.Visible = false;
        _actions.Visible = true;
    }

    /// <summary>Opens on its buttons, with an optional status line ("Saved as Save 2").</summary>
    public void SetStatus(string text)
    {
        _status.Text = text;
    }

    public override void OnOpened()
    {
        base.OnOpened();
        ShowActions();
    }

    public override void OnClosed()
    {
        base.OnClosed();
        _status.Text = "";
    }
}
