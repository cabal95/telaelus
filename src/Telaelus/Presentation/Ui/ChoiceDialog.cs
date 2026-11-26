using System;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// A small modal question with two answers ("Learn to play?": Start trainer or Skip). It stands the
/// game still while open; Escape or its Close gives the second answer.
/// </summary>
public partial class ChoiceDialog : Dialog
{
    private Label _text = null!;
    private Button _yes = null!;
    private Button _no = null!;
    private Action? _onYes;
    private Action? _onNo;

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "choice";

    public override bool PausesGame => true;

    /// <summary>The question shown, for the snapshot.</summary>
    public string Text => _text.Text;

    protected override void Build()
    {
        // The second answer does what Close would.
        CloseButton.Visible = false;
        _text = UiTheme.MakeLabel("", UiTheme.BodySize);
        _text.HorizontalAlignment = HorizontalAlignment.Center;
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(320f, 0f);
        Body.AddChild(_text);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(row);
        _yes = new Button
        {
            Name = "Yes",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(UiTheme.ButtonMinWidth, 40f)
        };
        _yes.Pressed += () => Answer(_onYes);
        row.AddChild(_yes);
        Host.Register(Id + "/yes", _yes);
        _no = new Button
        {
            Name = "No",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(UiTheme.ButtonMinWidth, 40f)
        };
        _no.Pressed += () => Answer(_onNo);
        row.AddChild(_no);
        Host.Register(Id + "/no", _no);
    }

    /// <summary>Opens with a title, a question, and two answers, each with what it does.</summary>
    public void Ask(string title, string text, string yes, Action onYes, string no, Action onNo)
    {
        TitleLabel.Text = title;
        _text.Text = text;
        _yes.Text = yes;
        _no.Text = no;
        _onYes = onYes;
        _onNo = onNo;
        Host.Open(this);
    }

    private void Answer(Action? then)
    {
        _onYes = null;
        _onNo = null;
        Host.Close(this);
        then?.Invoke();
    }

    public override void OnClosed()
    {
        base.OnClosed();
        Action? no = _onNo;
        _onYes = null;
        _onNo = null;
        no?.Invoke();
    }
}
