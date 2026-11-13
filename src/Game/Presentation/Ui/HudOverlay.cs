using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// UI that is part of the screen rather than opened: always shown (the top bar), shown
/// while it applies (the hint overlay), or toggled by the player (the debug overlay).
/// An overlay never takes keys and never closes anything; it refreshes from the
/// simulation every frame and lays itself out in a window of the given UI size.
/// </summary>
public abstract partial class HudOverlay : Control
{
    /// <summary>Updates the overlay's content and layout for this frame.</summary>
    public abstract void Refresh(Simulation sim, Vector2 uiSize);

    /// <summary>An icon box of a fixed size that keeps its size when it has no texture (headless).</summary>
    public static TextureRect MakeIcon(float size)
    {
        return new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    /// <summary>A whole-number amount for display.</summary>
    public static int Whole(Simulation sim, ResourceType type)
    {
        sim.Stockpile.TryGetValue(type, out float amount);
        return Mathf.FloorToInt(amount);
    }
}
