using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The shared UI style (see docs/blueprints/ui-style.md): color tokens, sizes, and the
/// Godot <see cref="Theme"/> all the UI uses, plus the UI scale. Sizes are authored
/// for a 720 px tall window; each UI layer is scaled by <see cref="Scale"/>.
/// </summary>
public static class UiTheme
{
    // ---- Color tokens ---------------------------------------------------
    public static readonly Color Panel = new(0.08f, 0.10f, 0.09f, 0.85f);
    public static readonly Color PanelEdge = new(0.30f, 0.36f, 0.32f);
    public static readonly Color Cell = new(0.14f, 0.17f, 0.15f, 0.90f);
    public static readonly Color CellHover = new(0.19f, 0.23f, 0.20f, 0.95f);
    public static readonly Color CellPressed = new(0.10f, 0.12f, 0.11f, 0.95f);
    public static readonly Color Text = new(0.92f, 0.94f, 0.92f);
    public static readonly Color TextDim = new(0.60f, 0.64f, 0.61f);
    public static readonly Color Accent = new(0.98f, 0.80f, 0.35f);
    public static readonly Color Short = new(1.00f, 0.42f, 0.38f);
    public static readonly Color Danger = new(0.85f, 0.30f, 0.25f);
    public static readonly Color Warning = new(0.98f, 0.66f, 0.18f);
    public static readonly Color Good = new(0.30f, 0.95f, 0.70f);
    public static readonly Color Backdrop = new(0f, 0f, 0f, 0.35f);
    public static readonly Color Silhouette = new(0.06f, 0.07f, 0.06f);

    // ---- Sizes (at UI scale 1) --------------------------------------------
    public const float ReferenceHeight = 720f;
    public const int Gap = 8;
    public const int Padding = 16;
    public const int PanelRadius = 6;
    public const int CellRadius = 4;
    public const int OutlineWidth = 2;
    public const int TitleSize = 22;
    public const int BodySize = 16;
    public const int SmallSize = 14;
    public const double TooltipDelay = 0.3;
    public const int ButtonMinWidth = 96;

    private static Theme? _theme;

    /// <summary>The UI scale for a window height: height / 720, clamped to 1 to 2.</summary>
    public static float ScaleFor(float windowHeight)
    {
        return Mathf.Clamp(windowHeight / ReferenceHeight, 1f, 2f);
    }

    /// <summary>The shared theme, built once.</summary>
    public static Theme Theme => _theme ??= BuildTheme();

    /// <summary>A flat box in a color, with a corner radius, an optional border, and padding.</summary>
    public static StyleBoxFlat Box(Color background, int radius, Color? border = null, int borderWidth = 0, int padding = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding
        };
        box.SetCornerRadiusAll(radius);
        if (border is Color edge && borderWidth > 0)
        {
            box.BorderColor = edge;
            box.SetBorderWidthAll(borderWidth);
        }
        return box;
    }

    /// <summary>The look of a panel: the dark translucent panel with a thin edge.</summary>
    public static StyleBoxFlat PanelBox(int padding = Padding)
    {
        return Box(Panel, PanelRadius, PanelEdge, 1, padding);
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme
        {
            DefaultFontSize = BodySize
        };

        theme.SetColor("font_color", "Label", Text);
        theme.SetColor("default_color", "RichTextLabel", Text);
        theme.SetStylebox("panel", "PanelContainer", PanelBox());

        StyleBoxFlat normal = Box(Cell, CellRadius, padding: Gap);
        normal.ContentMarginLeft = Padding;
        normal.ContentMarginRight = Padding;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = CellHover;
        hover.BorderColor = Accent;
        hover.SetBorderWidthAll(OutlineWidth);
        StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = CellPressed;
        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", Text);
        theme.SetColor("font_hover_color", "Button", Text);
        theme.SetColor("font_pressed_color", "Button", Text);

        // A thin scroll bar in the panel's edge color.
        StyleBoxFlat grabber = Box(PanelEdge, 3);
        theme.SetStylebox("grabber", "VScrollBar", grabber);
        theme.SetStylebox("grabber_highlight", "VScrollBar", Box(TextDim, 3));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Box(Accent, 3));
        StyleBoxFlat track = Box(new Color(0f, 0f, 0f, 0.2f), 3);
        track.ContentMarginLeft = 3;
        track.ContentMarginRight = 3;
        theme.SetStylebox("scroll", "VScrollBar", track);
        return theme;
    }

    /// <summary>A label in the theme's text color at a font size.</summary>
    public static Label MakeLabel(string text, int size, Color? color = null)
    {
        var label = new Label
        {
            Text = text
        };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is Color c)
        {
            label.AddThemeColorOverride("font_color", c);
        }
        return label;
    }

    /// <summary>The color that carries a tone (ui-style.md, status strip).</summary>
    public static Color ColorOf(Tone tone)
    {
        return tone switch
        {
            Tone.Dim => TextDim,
            Tone.Good => Good,
            Tone.Warning => Warning,
            Tone.Danger => Danger,
            _ => Text
        };
    }

    /// <summary>
    /// Sizes a button to its label (the theme's 16 units of side padding around it), never narrower
    /// than <see cref="ButtonMinWidth"/>, placed in its container instead of stretched across it.
    /// </summary>
    public static T SizeToLabel<T>(T button, Control.SizeFlags placement = Control.SizeFlags.ShrinkCenter) where T : Button
    {
        button.CustomMinimumSize = new Vector2(ButtonMinWidth, button.CustomMinimumSize.Y);
        button.SizeFlagsHorizontal = placement;
        return button;
    }

    /// <summary>The hex form of a color for BBCode.</summary>
    public static string Hex(Color color)
    {
        return color.ToHtml(false);
    }
}

/// <summary>What a status shows as color: normal, off or not applicable now, on, degraded, or broken.</summary>
public enum Tone
{
    Normal,
    Dim,
    Good,
    Warning,
    Danger
}
