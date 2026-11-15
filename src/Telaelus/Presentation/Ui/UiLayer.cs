using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// A canvas layer drawn at the UI scale: its children are authored for a 720 px tall
/// window, and the layer scales them by window height / 720 (clamped to 1 to 2). Its
/// <see cref="Root"/> control covers the window in those unscaled units, so anchors
/// and layout work as if the window were the reference size.
/// </summary>
public partial class UiLayer : CanvasLayer
{
    /// <summary>The full-window control children are added to.</summary>
    public Control Root { get; private set; } = null!;

    public float UiScale { get; private set; } = 1f;

    public override void _Ready()
    {
        Root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = UiTheme.Theme
        };
        AddChild(Root);
        GetViewport().SizeChanged += ApplyScale;
        ApplyScale();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= ApplyScale;
    }

    private void ApplyScale()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        UiScale = UiTheme.ScaleFor(size.Y);
        Scale = new Vector2(UiScale, UiScale);
        Root.Position = Vector2.Zero;
        Root.Size = size / UiScale;
    }

    /// <summary>The window size in UI units (the root's size).</summary>
    public Vector2 UiSize => Root.Size;

    /// <summary>Converts a screen (viewport pixel) point to UI units.</summary>
    public Vector2 ToUi(Vector2 screen) => screen / UiScale;

    /// <summary>Converts a point in UI units to a screen (viewport pixel) point.</summary>
    public Vector2 ToScreen(Vector2 ui) => ui * UiScale;
}
