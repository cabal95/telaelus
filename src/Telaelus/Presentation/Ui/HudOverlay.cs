using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

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

    /// <summary>
    /// "45 / 100" for a resource with a limit of its own, the amount alone for a Cargo or unlimited
    /// resource, and demand over supply for a capacity resource ("2 / 4", "Blackout: 6 / 4").
    /// </summary>
    public static string StorageText(Simulation sim, ResourceType type)
    {
        if (CapacityResources.CapacityOf(type) is CapacityType capacity)
        {
            return CapacityText(sim, capacity, named: false);
        }
        return WholeLimit(sim, type) is int limit ? $"{Whole(sim, type)} / {limit}" : Whole(sim, type).ToString();
    }

    /// <summary>A resource's amount color: a capacity resource's state color, else the warning color when storage is full.</summary>
    public static Color AmountColor(Simulation sim, ResourceType type)
    {
        if (CapacityResources.CapacityOf(type) is CapacityType capacity)
        {
            CapacityReading reading = sim.Capacity(capacity);
            return CapacityColor(reading.Demand, reading.Supply);
        }
        return IsFull(sim, type) ? UiTheme.Warning : UiTheme.Text;
    }

    /// <summary>
    /// A capacity type as demand over supply ("Water 2 / 4"; "Blackout: Water 6 / 4" in a blackout),
    /// optionally with a building's draw or supply added (a placement preview, never marked blackout).
    /// </summary>
    public static string CapacityText(Simulation sim, CapacityType type, float extraDemand = 0f, float extraSupply = 0f, bool named = true)
    {
        CapacityReading reading = sim.Capacity(type);
        string figures = $"{Mathf.CeilToInt(reading.Demand + extraDemand)} / {Mathf.FloorToInt(reading.Supply + extraSupply)}";
        string text = named ? $"{type} {figures}" : figures;
        return reading.State == CapacityState.Blackout && extraDemand == 0f && extraSupply == 0f ? "Blackout: " + text : text;
    }

    /// <summary>The color for a capacity readout of demand over supply: normal, warning in a brownout, danger in a blackout.</summary>
    public static Color CapacityColor(float demand, float supply)
    {
        return Simulation.StateFor(demand, supply) switch
        {
            CapacityState.Normal => UiTheme.Text,
            CapacityState.Brownout => UiTheme.Warning,
            _ => UiTheme.Danger
        };
    }

    /// <summary>The snapshot's name for a color: "normal", "warning", or "danger".</summary>
    public static string ToneOf(Color color)
    {
        return color == UiTheme.Danger ? "danger" : color == UiTheme.Warning ? "warning" : "normal";
    }

    /// <summary>Whether storage has no room for another unit of a limited resource (shown in the warning color).</summary>
    public static bool IsFull(Simulation sim, ResourceType type)
    {
        return ResourceDefs.For(type).Storage.Class != StorageClass.Unlimited && sim.RoomFor(type) < 1f;
    }
}
