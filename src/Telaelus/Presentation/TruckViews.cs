using System.Collections.Generic;
using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Mirrors the simulation's trucks onto truck models: placed at each truck's position every frame,
/// turned smoothly toward its heading, with its cargo's item model on the bed while it carries
/// one. Creates and frees views as trucks are bought or go with their Depot.
/// </summary>
public partial class TruckViews : Node3D
{
    // The bed's top and center, from the model (src/Telaelus/assets/models/units/README.md).
    private static readonly Vector3 BedCenter = new(0f, 0.36f, 0.29f);
    private const float CargoScale = 0.7f;
    private const float TurnRate = 10f;

    private readonly Dictionary<int, View> _views = new();

    private sealed class View
    {
        public required Node3D Root { get; init; }
        public Node3D? Cargo { get; set; }
        public ResourceType? CargoType { get; set; }
    }

    public void Sync(Simulation sim, double delta)
    {
        var present = new HashSet<int>();
        foreach (Truck truck in sim.Trucks)
        {
            present.Add(truck.Id);
            if (!_views.TryGetValue(truck.Id, out View? view))
            {
                Node3D root = ModelLibrary.Instance("units/truck");
                root.Name = $"Truck{truck.Id}";
                root.Rotation = new Vector3(0f, YawOf(truck.Heading), 0f);
                AddChild(root);
                view = new View
                {
                    Root = root
                };
                _views[truck.Id] = view;
            }
            view.Root.Position = truck.Position;
            float weight = 1f - Mathf.Exp(-TurnRate * (float)delta);
            view.Root.Rotation = new Vector3(0f, Mathf.LerpAngle(view.Root.Rotation.Y, YawOf(truck.Heading), weight), 0f);
            SyncCargo(view, truck.Cargo);
        }

        var gone = new List<int>();
        foreach (int id in _views.Keys)
        {
            if (!present.Contains(id))
            {
                gone.Add(id);
            }
        }
        foreach (int id in gone)
        {
            _views[id].Root.QueueFree();
            _views.Remove(id);
        }
    }

    /// <summary>The truck whose model is nearest the screen point, within a small reach, or null.</summary>
    public Truck? TruckAt(Simulation sim, Camera3D camera, Vector2 screen, float reachPixels)
    {
        Truck? best = null;
        float bestDistance = reachPixels;
        foreach (Truck truck in sim.Trucks)
        {
            Vector3 middle = truck.Position + new Vector3(0f, 0.4f, 0f);
            if (camera.IsPositionBehind(middle))
            {
                continue;
            }
            float distance = camera.UnprojectPosition(middle).DistanceTo(screen);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = truck;
            }
        }
        return best;
    }

    /// <summary>The yaw that turns a model authored facing -Z toward a direction.</summary>
    private static float YawOf(Vector3 heading)
    {
        return Mathf.Atan2(-heading.X, -heading.Z);
    }

    private static void SyncCargo(View view, ResourceType? cargo)
    {
        if (view.CargoType == cargo)
        {
            return;
        }
        view.Cargo?.QueueFree();
        view.Cargo = null;
        view.CargoType = cargo;
        if (cargo is ResourceType type)
        {
            Node3D item = ModelLibrary.Instance(ResourceIcons.ModelPath(type));
            item.Name = "Cargo";
            item.Position = BedCenter;
            item.Scale = Vector3.One * CargoScale;
            view.Root.AddChild(item);
            view.Cargo = item;
        }
    }
}
