using System.Collections.Generic;
using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Two kinds of prop on buildings. A display stand at the middle of the front edge of every
/// building with a choice of output, carrying its current output's item model, turning slowly, so
/// a row of Fabricators reads at a glance. And the ship on a Shipyard's pad: an engine, up to four
/// hull rings, and the nose, each shown once storage holds that part of the ship bill.
/// </summary>
public partial class DisplayStandViews : Node3D
{
    private const float ItemScale = 0.7f;
    private const float StandTop = 0.58f;
    private const float TurnSpeed = 0.8f;
    private const float FrontInset = 0.4f;

    private sealed class Stand
    {
        public required Node3D Root { get; init; }
        public Node3D? Item { get; set; }
        public ResourceType? Shown { get; set; }
    }

    private readonly Dictionary<int, Stand> _stands = new();
    private readonly Dictionary<int, (Node3D Root, string Key)> _ships = new();

    /// <summary>
    /// The output a building's stand shows: its recipe's (the waiting one's once a change is chosen)
    /// for a processor with several recipes, its output for a Quarry or a building on a motherlode;
    /// null when switched off or for a building with no stand.
    /// </summary>
    public static ResourceType? DisplayedOutput(Simulation sim, Building building)
    {
        if (!HasStand(building) || !building.Enabled)
        {
            return null;
        }
        if (building.Recipes.Count > 1)
        {
            return building.Recipes[building.PendingRecipe ?? building.RecipeIndex].Outputs[0].Type;
        }
        return sim.OutputOf(building);
    }

    /// <summary>Whether a building has a display stand: a choice of recipe, or a collector with an Output picker of choices.</summary>
    public static bool HasStand(Building building)
    {
        return building.Recipes.Count > 1
            || (building.Def.ProduceSeconds > 0f && (building.SiteMotherlode is not null || building.Def.Placement == Telaelus.Sim.Placement.BesideMountain));
    }

    public void Sync(Simulation sim, double delta)
    {
        var present = new HashSet<int>();
        foreach (Building building in sim.Buildings)
        {
            if (!HasStand(building))
            {
                continue;
            }
            present.Add(building.Id);
            if (!_stands.TryGetValue(building.Id, out Stand? stand))
            {
                Node3D root = ModelLibrary.Instance("buildings/display_stand");
                root.Name = $"Stand{building.Id}";
                root.Position = OnFront(building, 0f, building.Def.Depth * 0.5f - FrontInset);
                AddChild(root);
                stand = new Stand
                {
                    Root = root
                };
                _stands[building.Id] = stand;
            }
            ResourceType? shown = DisplayedOutput(sim, building);
            if (shown != stand.Shown)
            {
                stand.Item?.QueueFree();
                stand.Item = null;
                stand.Shown = shown;
                if (shown is ResourceType type)
                {
                    Node3D item = ModelLibrary.Instance(ResourceIcons.ModelPath(type));
                    item.Name = "Item";
                    item.Position = new Vector3(0f, StandTop, 0f);
                    item.Scale = Vector3.One * ItemScale;
                    stand.Root.AddChild(item);
                    stand.Item = item;
                }
            }
            if (stand.Item is Node3D turning)
            {
                turning.RotateY(TurnSpeed * (float)delta);
            }
        }
        Prune(_stands, present, s => s.Root);
        SyncShips(sim);
    }

    /// <summary>The ship on each Shipyard's pad, by how much of the ship bill storage holds.</summary>
    private void SyncShips(Simulation sim)
    {
        sim.Stockpile.TryGetValue(ResourceType.HullSection, out float hulls);
        sim.Stockpile.TryGetValue(ResourceType.Engine, out float engines);
        sim.Stockpile.TryGetValue(ResourceType.ControlModule, out float controls);
        int rings = Mathf.Clamp(Mathf.FloorToInt(hulls), 0, 4);
        bool engine = engines >= 1f;
        bool nose = controls >= 1f;
        string key = $"{rings}|{engine}|{nose}";

        var present = new HashSet<int>();
        foreach (Building building in sim.Buildings)
        {
            if (building.Type != BuildingType.Shipyard)
            {
                continue;
            }
            present.Add(building.Id);
            if (_ships.TryGetValue(building.Id, out (Node3D Root, string Key) ship) && ship.Key == key)
            {
                continue;
            }
            ship.Root?.QueueFree();
            var root = new Node3D
            {
                Name = $"Ship{building.Id}",
                // The pad is the front half: its center a quarter of the depth in from the front.
                Position = OnFront(building, 0f, building.Def.Depth * 0.25f) + new Vector3(0f, 0.3f, 0f)
            };
            // The engine stands on the pad; the hull rings stack on it (on the pad without it).
            float y = 0f;
            if (engine)
            {
                root.AddChild(Part("buildings/ship_engine", y));
                y += 1f;
            }
            for (int i = 0; i < rings; i++)
            {
                root.AddChild(Part("buildings/ship_hull_ring", y));
                y += 1f;
            }
            if (nose)
            {
                root.AddChild(Part("buildings/ship_nose", y));
            }
            AddChild(root);
            _ships[building.Id] = (root, key);
        }
        var gone = new List<int>();
        foreach (int id in _ships.Keys)
        {
            if (!present.Contains(id))
            {
                gone.Add(id);
            }
        }
        foreach (int id in gone)
        {
            _ships[id].Root.QueueFree();
            _ships.Remove(id);
        }
    }

    private static Node3D Part(string path, float y)
    {
        Node3D part = ModelLibrary.Instance(path);
        part.Position = new Vector3(0f, y, 0f);
        return part;
    }

    /// <summary>
    /// A point on a building given in its North-facing frame (x across, and how far in from the
    /// front edge), turned to its facing.
    /// </summary>
    private static Vector3 OnFront(Building building, float across, float inFromCenter)
    {
        // Authored facing North, the front is toward -Z.
        var local = new Vector3(across, 0f, -inFromCenter);
        float yaw = Mathf.DegToRad(ModelLibrary.EdgeYaw(building.Facing));
        return building.Position + local.Rotated(Vector3.Up, yaw);
    }

    private static void Prune<T>(Dictionary<int, T> views, HashSet<int> present, System.Func<T, Node3D> root)
    {
        var gone = new List<int>();
        foreach (int id in views.Keys)
        {
            if (!present.Contains(id))
            {
                gone.Add(id);
            }
        }
        foreach (int id in gone)
        {
            root(views[id]).QueueFree();
            views.Remove(id);
        }
    }
}
