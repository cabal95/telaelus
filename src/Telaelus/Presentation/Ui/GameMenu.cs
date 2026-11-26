using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

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
    public event Action? TrainerRequested;

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
        // The stack shares one width, twice its widest label's (StackWidth), centered.
        _actions = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _actions.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_actions);
        AddButton("Resume", () => Host.Close(this));
        AddButton("Save Game", () => SaveRequested?.Invoke());
        AddButton("Load Game", () => LoadRequested?.Invoke());
        AddButton("Trainer", () => TrainerRequested?.Invoke());
        AddButton("Main Menu", () => Leave(() => MainMenuRequested?.Invoke()));
        AddButton("Quit", () => Leave(() => QuitRequested?.Invoke()));
        _actions.CustomMinimumSize = new Vector2(StackWidth(_buttons.Select(b => b.Text)), 0f);

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
        row.AddChild(UiTheme.SizeToLabel(yes));
        Host.Register(Id + "/Confirm", yes);
        var no = new Button
        {
            Name = "Cancel",
            Text = "Cancel",
            FocusMode = FocusModeEnum.None
        };
        no.Pressed += ShowActions;
        row.AddChild(UiTheme.SizeToLabel(no));
        Host.Register(Id + "/Cancel", no);

        _status = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Body.AddChild(_status);
    }

    /// <summary>
    /// A menu stack's width: twice its widest button (label plus the theme's side padding, at least
    /// <see cref="UiTheme.ButtonMinWidth"/>), so a short menu still reads as a menu.
    /// </summary>
    private static float StackWidth(IEnumerable<string> labels)
    {
        Font font = ThemeDB.FallbackFont;
        float widest = labels.Max(label => font.GetStringSize(label, HorizontalAlignment.Left, -1, UiTheme.BodySize).X + 2 * UiTheme.Padding);
        return 2f * Mathf.Max(widest, UiTheme.ButtonMinWidth);
    }

    private void AddButton(string text, Action pressed)
    {
        var button = new Button
        {
            Name = text.Replace(" ", ""),
            Text = text,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(UiTheme.ButtonMinWidth, 40f)
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
