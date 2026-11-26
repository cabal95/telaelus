using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// An icon toggle (ui-style.md): one flat icon button with two looks, for a binary that acts on the
/// thing itself, as a building's on/off power symbol. The icon is <c>Good</c> while on and
/// <c>TextDim</c> while off; hover draws the <c>Accent</c> outline, and the tooltip says the state
/// and what a click does. <see cref="On"/> only shows the state; the owner acts on
/// <see cref="BaseButton.Pressed"/>.
/// </summary>
public partial class IconToggle : Button
{
    public const float IconSize = 20f;

    private readonly DialogHost _host;
    private readonly string _tipOn;
    private readonly string _tipOff;
    private bool _on;
    private bool _hovered;

    public IconToggle(DialogHost host, string tipOn, string tipOff)
    {
        _host = host;
        _tipOn = tipOn;
        _tipOff = tipOff;
        FocusMode = FocusModeEnum.None;
        ExpandIcon = true;
        IconAlignment = HorizontalAlignment.Center;
        CustomMinimumSize = new Vector2(IconSize + 4f, IconSize + 4f);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        StyleBoxFlat flat = UiTheme.Box(Colors.Transparent, UiTheme.CellRadius, padding: 2);
        StyleBoxFlat hover = UiTheme.Box(UiTheme.CellHover, UiTheme.CellRadius, UiTheme.Accent, UiTheme.OutlineWidth, 2);
        AddThemeStyleboxOverride("normal", flat);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", flat);
        AddThemeStyleboxOverride("hover_pressed", hover);
        MouseEntered += () =>
        {
            _hovered = true;
            ShowTip();
        };
        MouseExited += () =>
        {
            _hovered = false;
            _host.HideHover(this);
        };
        Apply();
    }

    /// <summary>Whether it shows on. Setting it never raises <see cref="BaseButton.Pressed"/>.</summary>
    public bool On
    {
        get => _on;
        set
        {
            if (_on == value)
            {
                return;
            }
            _on = value;
            Apply();
            if (_hovered)
            {
                ShowTip();
            }
        }
    }

    /// <summary>The tooltip as shown for the current state.</summary>
    public string Tip => _on ? _tipOn : _tipOff;

    private void Apply()
    {
        Color color = _on ? UiTheme.Good : UiTheme.TextDim;
        foreach (string state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color" })
        {
            AddThemeColorOverride(state, color);
        }
    }

    private void ShowTip()
    {
        _host.ShowHover(this, new[] { new TipLine(Tip) });
    }
}
