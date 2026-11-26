using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// A section heading (ui-style.md): a <c>SmallSize</c> <c>TextDim</c> caps label, letter-spaced, over a
/// 1 unit <c>PanelEdge</c> rule, grouping a panel's controls once it has more than one kind.
/// </summary>
public partial class SectionHeading : VBoxContainer
{
    private static FontVariation? _spaced;

    public SectionHeading(string text)
    {
        Name = "Section" + text.Replace(" ", "");
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
        Label label = UiTheme.MakeLabel(text.ToUpperInvariant(), UiTheme.SmallSize, UiTheme.TextDim);
        label.AddThemeFontOverride("font", _spaced ??= new FontVariation
        {
            BaseFont = ThemeDB.FallbackFont,
            SpacingGlyph = 1
        });
        AddChild(label);
        AddChild(new ColorRect
        {
            Color = UiTheme.PanelEdge,
            CustomMinimumSize = new Vector2(0f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        });
    }
}
