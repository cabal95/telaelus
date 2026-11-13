using System;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>The two overlay positions (see docs/blueprints/ui-style.md).</summary>
public enum OverlayKind
{
    /// <summary>Centered over a dimmed backdrop; takes all input until dismissed.</summary>
    Modal,

    /// <summary>Against the left edge, a third of the window wide; captures only Escape.</summary>
    Side
}

/// <summary>
/// The shared overlay frame: a panel with a title, a vertically scrolling body, and a
/// close button at the bottom center. Subclasses fill <see cref="Body"/>. The host
/// (<see cref="OverlayHost"/>) opens and closes overlays, one at a time, and places
/// them each frame by their kind.
/// </summary>
public abstract partial class Overlay : PanelContainer
{
    public abstract OverlayKind Kind { get; }

    /// <summary>The overlay's id for the snapshot and the <c>clickUi</c> command, as "buildMenu".</summary>
    public abstract string Id { get; }

    protected OverlayHost Host { get; private set; } = null!;
    protected Label TitleLabel { get; private set; } = null!;
    protected VBoxContainer Body { get; private set; } = null!;
    protected ScrollContainer Scroll { get; private set; } = null!;
    public Button CloseButton { get; private set; } = null!;

    /// <summary>Raised after the overlay closes, by any means.</summary>
    public event Action? Closed;

    public void Attach(OverlayHost host, string title)
    {
        Host = host;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        // Godot hands a wheel event the scroll body did not use (at the end of its range)
        // up to the parent even through Stop; the panel keeps it from the world's zoom.
        MouseForcePassScrollEvents = false;

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", UiTheme.Gap);
        AddChild(column);

        TitleLabel = UiTheme.MakeLabel(title, UiTheme.TitleSize);
        TitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(TitleLabel);
        var rule = new ColorRect
        {
            Color = UiTheme.Accent,
            CustomMinimumSize = new Vector2(0f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddChild(rule);

        Scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(Scroll);
        Body = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        Body.AddThemeConstantOverride("separation", UiTheme.Gap);
        Scroll.AddChild(Body);

        CloseButton = new Button
        {
            Text = "Close",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(120f, 0f)
        };
        CloseButton.Pressed += () => Host.Close(this);
        column.AddChild(CloseButton);
        host.Register(Id + "/close", CloseButton);

        Build();
    }

    /// <summary>Fills <see cref="Body"/> once, when attached.</summary>
    protected abstract void Build();

    /// <summary>Called every frame while open, before the overlay is placed.</summary>
    public virtual void Refresh()
    {
    }

    /// <summary>A key pressed while this modal overlay is open (every key is swallowed either way).</summary>
    public virtual void OnKey(Key key)
    {
    }

    /// <summary>Called by the host when the overlay opens.</summary>
    public virtual void OnOpened()
    {
        Scroll.ScrollVertical = 0;
    }

    /// <summary>Called by the host when the overlay closes.</summary>
    public virtual void OnClosed()
    {
        Closed?.Invoke();
    }

    /// <summary>
    /// The width a modal overlay takes: 60% of the window, at least 420 and at most 900
    /// UI units (never wider than the window less the margins).
    /// </summary>
    public static float ModalWidth(Vector2 uiSize)
    {
        float width = Mathf.Clamp(uiSize.X * 0.6f, 420f, 900f);
        return Mathf.Min(width, uiSize.X - 2 * UiTheme.Padding);
    }

    /// <summary>
    /// Sizes and positions the panel for its kind in a window of the given UI size. A
    /// side panel starts below <paramref name="sideTop"/> (the HUD's text) and runs to the
    /// bottom margin.
    /// </summary>
    public void Place(Vector2 uiSize, float sideTop)
    {
        float margin = UiTheme.Padding;
        if (Kind == OverlayKind.Modal)
        {
            float width = ModalWidth(uiSize);
            // As tall as the content, up to 80% of the window; past that the body scrolls.
            float chrome = Size.Y - Scroll.Size.Y;
            float content = Body.GetCombinedMinimumSize().Y;
            float height = Mathf.Min(chrome + content, uiSize.Y * 0.8f);
            Scroll.CustomMinimumSize = new Vector2(0f, Mathf.Max(0f, height - chrome));
            Size = new Vector2(width, 0f);
            Position = ((uiSize - Size) * 0.5f).Floor();
        }
        else
        {
            float width = Mathf.Max(uiSize.X / 3f, 280f);
            float top = Mathf.Max(margin, sideTop + UiTheme.Gap);
            Size = new Vector2(width, Mathf.Max(160f, uiSize.Y - top - margin));
            Position = new Vector2(margin, top);
        }
    }
}
