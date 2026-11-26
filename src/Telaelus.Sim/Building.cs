using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// A placed building. Its anchor (minimum-corner fine cell) and facing are its
/// authoritative placement; the footprint size and center Position are derived
/// from them. An extractor (Mine, Lumber Camp) works resource fields within its
/// work area; a processor (Smelter, Refinery) runs a Recipe against the shared
/// stockpile.
/// </summary>
public sealed class Building
{
    public int Id { get; }
    public BuildingType Type { get; }
    public BuildingDef Def { get; }
    public int AnchorX { get; private set; }
    public int AnchorZ { get; private set; }
    public Facing Facing { get; }
    /// <summary>The recipes it can run (none for a building that is not a processor), the default first.</summary>
    public IReadOnlyList<Recipe> Recipes { get; }

    /// <summary>
    /// Whether it crafts from a queue of orders (see Simulation.QueueCraft): every processor but one
    /// that makes from nothing and burns no fuel (the Electrolyzer, Brine Well, Air Separator), which
    /// runs its recipe until storage is full.
    /// </summary>
    public bool Queues { get; }

    /// <summary>A queueing processor's orders, the first crafting; at most Simulation.MaxQueue.</summary>
    public List<CraftOrder> Queue { get; } = new();

    /// <summary>A queueing processor's hold (Hold mode, with an empty queue), or null in Queue mode.</summary>
    public HoldOrder? Hold { get; set; }

    /// <summary>
    /// The index of the recipe it runs: a queueing processor's first order's (or its last when the
    /// queue is empty), else the one chosen with Simulation.SetRecipe.
    /// </summary>
    public int RecipeIndex { get; set; }

    /// <summary>A recipe chosen for a running processor while a cycle runs, taken up when the cycle finishes; null for none.</summary>
    public int? PendingRecipe { get; set; }

    /// <summary>The recipe it runs, or null for a building that is not a processor.</summary>
    public Recipe? Recipe => Recipes.Count > 0 ? Recipes[RecipeIndex] : null;

    /// <summary>The choice for each input with alternatives, by recipe and input index: a type, or absent for Auto.</summary>
    public Dictionary<(int Recipe, int Input), ResourceType> InputChoices { get; } = new();

    /// <summary>The inputs the running cycle took (refunded if the building is destroyed mid-cycle).</summary>
    public List<ResourceAmount> CycleInputs { get; } = new();

    /// <summary>Whether the building runs; a switched-off building does nothing (see Simulation.SetBuildingEnabled).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether it is on the HQ network (see Simulation.RecomputeNetwork).</summary>
    public bool Online { get; set; }

    /// <summary>Its output buffer: units waiting to be collected (or, online, to drain into storage).</summary>
    public Dictionary<ResourceType, float> Output { get; } = new();

    /// <summary>The units in the output buffer, of every type.</summary>
    public float OutputCount
    {
        get
        {
            float count = 0f;
            foreach (float amount in Output.Values)
            {
                count += amount;
            }
            return count;
        }
    }

    /// <summary>The fuel burning now, or null; it is spent once taken.</summary>
    public ResourceType? Burning { get; set; }

    /// <summary>Seconds left on the unit burning now.</summary>
    public float BurnLeft { get; set; }

    /// <summary>A Battery Bank's stored Power, in Power-seconds.</summary>
    public float Charge { get; set; }

    /// <summary>The fuel it burns: one by name, or null for Best (the longest-burning one at hand).</summary>
    public ResourceType? FuelChoice { get; set; }

    /// <summary>A Silo's chosen Bulk resource (see Simulation.SetSiloResource).</summary>
    public ResourceType SiloResource { get; set; } = ResourceType.Wood;

    // Extractor: the field it is currently working (null when idle).
    public ResourceField? CurrentField { get; set; }

    // Extractor: seconds toward the next unit from its current field.
    // Processor: seconds into the current cycle, and whether a cycle is running.
    public bool Crafting { get; set; }
    public float Progress { get; set; }

    /// <summary>What a collector with a choice yields (the Quarry, a motherlode's Deep Mine or Deep Well); see Simulation.OutputChoices.</summary>
    public ResourceType? OutputChoice { get; set; }

    /// <summary>The motherlode a Deep Mine or Deep Well stands on, or null.</summary>
    public ResourceField? SiteMotherlode { get; set; }

    /// <summary>The worked-out deposit a Deep Mine or Deep Well stands over, or null.</summary>
    public ResourceType? SiteDeposit { get; set; }

    /// <summary>A Forester's field while it has one (growing or grown); null while the plot stands bare.</summary>
    public ResourceField? Plot { get; set; }

    /// <summary>Seconds left before a bare Forester plot plants again.</summary>
    public float PlotWait { get; set; }

    /// <summary>Seconds left on the unit a provider that eats (the Rock Crusher) is working through.</summary>
    public float FeedLeft { get; set; }

    /// <summary>Whether a provider that eats had nothing to eat this tick.</summary>
    public bool Starved { get; set; }

    public Building(int id, BuildingType type, int anchorX, int anchorZ, Facing facing, IReadOnlyList<Recipe> recipes)
    {
        Id = id;
        Type = type;
        Def = BuildingDefs.For(type);
        AnchorX = anchorX;
        AnchorZ = anchorZ;
        Facing = facing;
        Recipes = recipes;
        Queues = recipes.Count > 0 && (Def.BurnsFuel || recipes.Any(recipe => recipe.Inputs.Count > 0));
    }

    /// <summary>Moves the anchor (a loaded building whose footprint grew since it was saved).</summary>
    internal void MoveAnchor(int anchorX, int anchorZ)
    {
        AnchorX = anchorX;
        AnchorZ = anchorZ;
    }

    public int Width => Def.Footprint(Facing).Width;

    public int Depth => Def.Footprint(Facing).Depth;

    /// <summary>World-space center of the footprint (derived, not stored).</summary>
    public Vector3 Position => Def.Center(AnchorX, AnchorZ, Facing);
}
