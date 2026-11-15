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

    /// <summary>A resource's own limit as a whole number, or null for a Cargo or unlimited resource.</summary>
    public static int? WholeLimit(Simulation sim, ResourceType type)
    {
        return ResourceDefs.For(type).Storage.IsPerType ? Mathf.FloorToInt(sim.StorageLimit(type)) : null;
    }

    /// <summary>"45 / 100" for a resource with a limit of its own, else the amount alone.</summary>
    public static string StorageText(Simulation sim, ResourceType type)
    {
        return WholeLimit(sim, type) is int limit ? $"{Whole(sim, type)} / {limit}" : Whole(sim, type).ToString();
    }

    /// <summary>The network's power as demand over supply ("Power 45 / 100"; "Blackout: Power 140 / 100" in a blackout).</summary>
    public static string PowerText(Simulation sim, float extraDemand = 0f, float extraSupply = 0f)
    {
        string text = $"Power {Mathf.CeilToInt(sim.PowerDemand + extraDemand)} / {Mathf.FloorToInt(sim.PowerSupply + extraSupply)}";
        return sim.PowerState == PowerState.Blackout && extraDemand == 0f && extraSupply == 0f ? "Blackout: " + text : text;
    }

    /// <summary>The color for a power readout of demand over supply: normal, warning in a brownout, danger in a blackout.</summary>
    public static Color PowerColor(float demand, float supply)
    {
        float overload = supply > 0f ? demand / supply - 1f : demand > 0f ? float.PositiveInfinity : 0f;
        return overload <= 0f ? UiTheme.Text : overload <= Simulation.BrownoutLimit ? UiTheme.Warning : UiTheme.Danger;
    }

    /// <summary>Whether storage has no room for another unit of a limited resource (shown in the warning color).</summary>
    public static bool IsFull(Simulation sim, ResourceType type)
    {
        return ResourceDefs.For(type).Storage.Class != StorageClass.Unlimited && sim.RoomFor(type) < 1f;
    }
}
