using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>A toast to show: its style, icon, title, and detail line.</summary>
public sealed record Toastable(ToastStyle Style, Texture2D? Icon, string Title, string? Detail);

/// <summary>
/// Turns one frame's sim events into toasts. Discoveries merge into one toast, and so do unlocks (shown above them)
/// ("New buildings: Mine, Smelter"); research, blackouts, and launches get one each, in order.
/// </summary>
public static class NotificationText
{
    public static List<Toastable> For(IReadOnlyList<SimEvent> events, Simulation sim, ResourceIcons icons, BuildingPictures pictures)
    {
        var toasts = new List<Toastable>();
        var discovered = events.Where(e => e.Kind == SimEventKind.Discovered).Select(e => e.Resource).Distinct().ToList();
        if (discovered.Count > 0)
        {
            string names = string.Join(", ", discovered.Select(t => ResourceDefs.For(t).Name));
            toasts.Add(new Toastable(ToastStyle.Brief, icons.For(discovered[0]), "Discovered: " + names,
                discovered.Count == 1 ? WorkedBy(discovered[0]) : null));
        }
        var unlocked = events.Where(e => e.Kind == SimEventKind.Unlocked).Select(e => e.Building).Distinct().ToList();
        if (unlocked.Count > 0)
        {
            string names = string.Join(", ", unlocked.Select(t => BuildingDefs.For(t).Name));
            toasts.Add(new Toastable(ToastStyle.Brief, pictures.Color(unlocked[0]),
                (unlocked.Count == 1 ? "New building: " : "New buildings: ") + names, "[B] to build"));
        }
        foreach (SimEvent e in events)
        {
            switch (e.Kind)
            {
                case SimEventKind.ResearchComplete:
                    ResearchDef def = ResearchDefs.For(e.Project);
                    toasts.Add(new Toastable(ToastStyle.Lasting, icons.Icon(ResearchTree.ResearchIconName(e.Project)),
                        "Research complete: " + def.Name, def.Description));
                    break;
                case SimEventKind.Blackout:
                    CapacityReading reading = sim.Capacity(e.Capacity);
                    toasts.Add(new Toastable(ToastStyle.Lasting, CapacityIcon(e.Capacity, icons), "Blackout: " + e.Capacity,
                        $"Demand {Mathf.CeilToInt(reading.Demand)} over supply {Mathf.FloorToInt(reading.Supply)}"));
                    break;
                case SimEventKind.RefundedOnLoad:
                    BuildingDef refunded = BuildingDefs.For(e.Building);
                    toasts.Add(new Toastable(ToastStyle.Lasting, pictures.Color(e.Building),
                        refunded.Name + " refunded",
                        $"It grew to {refunded.Width} x {refunded.Depth} and no longer fit; its cost is back in storage"));
                    break;
                case SimEventKind.ShipLaunched:
                    toasts.Add(new Toastable(ToastStyle.Lasting, pictures.Color(BuildingType.Shipyard), "Ship launched",
                        $"Ships launched: {sim.ShipsLaunched}"));
                    break;
            }
        }
        return toasts;
    }

    /// <summary>"A Mine can work it", "A Mine or a Drill Rig can work it", or null when nothing extracts it.</summary>
    public static string? WorkedBy(ResourceType type)
    {
        var names = System.Enum.GetValues<BuildingType>()
            .Select(BuildingDefs.For)
            .Where(d => d.Buildable && d.Works.Contains(type))
            .Select(d => Article(d.Name) + " " + d.Name)
            .ToList();
        if (names.Count == 0)
        {
            return null;
        }
        string list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1];
        return char.ToUpperInvariant(list[0]) + list[1..] + " can work it";
    }

    private static string Article(string name)
    {
        return "AEIOU".Contains(char.ToUpperInvariant(name[0])) ? "an" : "a";
    }

    private static Texture2D? CapacityIcon(CapacityType type, ResourceIcons icons)
    {
        return type switch
        {
            CapacityType.Water => icons.For(ResourceType.Water),
            CapacityType.Sand => icons.For(ResourceType.Sand),
            _ => icons.Icon("icon_power")
        };
    }
}
