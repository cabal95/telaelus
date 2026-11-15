using System;
using System.Collections.Generic;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// The renewable bypasses: worked-out records left by depleted fields; collectors that produce
/// without a field (the Quarry, and the Deep Well and Deep Mine over a worked-out cell or on a
/// motherlode); the Forester's plot, which grows and regrows its own Wood field; and the special
/// placements they use. A building's reach (where trucks stop and the player collects) is its
/// footprint, or for one on a motherlode, the motherlode.
/// </summary>
public sealed partial class Simulation
{
    private readonly Dictionary<(int MapX, int MapZ), ResourceType> _workedOut = new();

    /// <summary>The deposit each worked-out map cell held, by map cell.</summary>
    public IReadOnlyDictionary<(int MapX, int MapZ), ResourceType> WorkedOut => _workedOut;

    // ---- Placement -------------------------------------------------------

    /// <summary>
    /// The footprint's cells pass the standard rule (in bounds, explored, free, buildable, not the
    /// player's), with no field on them other than <paramref name="allowed"/>. A building on a
    /// motherlode stands on the motherlode, so its cells need only be walkable ground.
    /// </summary>
    private bool CellsFree(BuildingDef def, int anchorX, int anchorZ, Facing facing, ResourceField? allowed, Func<int, int, bool>? also = null)
    {
        (int playerX, int playerZ) = CellAt(Player.Position.X, Player.Position.Z);
        (int width, int depth) = def.Footprint(facing);
        for (int x = anchorX; x < anchorX + width; x++)
        {
            for (int z = anchorZ; z < anchorZ + depth; z++)
            {
                ResourceField? field = FieldAt(x, z);
                bool ground = allowed is { IsMotherlode: true }
                    ? TerrainDefs.For(TerrainAt(x, z)).Walkable && !FeatureBlocks(x, z)
                    : IsBuildable(x, z);
                if (!InBounds(x, z) || !IsExplored(MapCellOf(x), MapCellOf(z)) || _occupied.Contains((x, z)) || !ground
                    || (field is not null && !ReferenceEquals(field, allowed)) || (x == playerX && z == playerZ)
                    || (also is not null && !also(x, z)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static int FloorMod(int value, int by)
    {
        int r = value % by;
        return r < 0 ? r + by : r;
    }

    /// <summary>
    /// What an <see cref="Placement.OverSite"/> building would work at an anchor: a motherlode of its
    /// kind whose center 4 x 4 the footprint is, or a worked-out map cell of a deposit it lists that
    /// the footprint is exactly; null for neither.
    /// </summary>
    public (ResourceField? Motherlode, ResourceType? Deposit)? SiteAt(BuildingType type, int anchorX, int anchorZ)
    {
        BuildingDef def = BuildingDefs.For(type);
        if (def.Placement != Placement.OverSite)
        {
            return null;
        }
        if (FloorMod(anchorX, FineCellsPerMapCell) == 2 && FloorMod(anchorZ, FineCellsPerMapCell) == 2
            && FieldAt(anchorX, anchorZ) is { IsMotherlode: true } lode
            && lode.MinX + 2 == anchorX && lode.MinZ + 2 == anchorZ
            && MotherlodeDefs.For(lode.Motherlode!.Value).WorkedBy == type)
        {
            return (lode, null);
        }
        if (FloorMod(anchorX, FineCellsPerMapCell) == 0 && FloorMod(anchorZ, FineCellsPerMapCell) == 0
            && _workedOut.TryGetValue((MapCellOf(anchorX), MapCellOf(anchorZ)), out ResourceType deposit)
            && ((IList<ResourceType>)def.Sites).Contains(deposit))
        {
            return (null, deposit);
        }
        return null;
    }

    /// <summary>The placements this file owns; null for one it does not (the caller applies the standard rule).</summary>
    private bool? CanPlaceRenewable(BuildingType type, BuildingDef def, int anchorX, int anchorZ, Facing facing)
    {
        switch (def.Placement)
        {
            case Placement.OverSite:
                return SiteAt(type, anchorX, anchorZ) is ({ } site) && CellsFree(def, anchorX, anchorZ, facing, site.Motherlode);
            case Placement.OpenGrassCell:
                return FloorMod(anchorX, FineCellsPerMapCell) == 0 && FloorMod(anchorZ, FineCellsPerMapCell) == 0
                    && CellsFree(def, anchorX, anchorZ, facing, null, (x, z) => TerrainAt(x, z) == TerrainType.Grass);
            case Placement.BesideMountain:
            {
                if (!CellsFree(def, anchorX, anchorZ, facing, null))
                {
                    return false;
                }
                (int width, int depth) = def.Footprint(facing);
                return MountainBeside(anchorX, anchorZ, width, depth, highlandsOnly: false);
            }
            default:
                return null;
        }
    }

    /// <summary>Whether any cell of a footprint is edge-adjacent to a mountain (in the Highlands only, when asked).</summary>
    private bool MountainBeside(int anchorX, int anchorZ, int width, int depth, bool highlandsOnly)
    {
        for (int x = anchorX - 1; x <= anchorX + width; x++)
        {
            for (int z = anchorZ - 1; z <= anchorZ + depth; z++)
            {
                bool insideX = x >= anchorX && x < anchorX + width;
                bool insideZ = z >= anchorZ && z < anchorZ + depth;
                if (insideX == insideZ || !InBounds(x, z))
                {
                    // The footprint itself and its diagonal corners are not edge neighbors.
                    continue;
                }
                if (FeatureAt(x, z) == MapFeature.Mountain && (!highlandsOnly || BiomeAt(x, z) == BiomeType.Highlands))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Where a building's ghost goes for a pointer at a world point: the map cell's corner for a
    /// Forester or a building over a site, or a motherlode's center for one that works motherlodes;
    /// null for a building that places freely.
    /// </summary>
    public (int X, int Z)? SnapAnchor(BuildingType type, float worldX, float worldZ)
    {
        BuildingDef def = BuildingDefs.For(type);
        if (def.Placement is not (Placement.OpenGrassCell or Placement.OverSite))
        {
            return null;
        }
        (int x, int z) = CellAt(worldX, worldZ);
        if (def.Placement == Placement.OverSite && FieldAt(x, z) is { IsMotherlode: true } lode)
        {
            return ((int)lode.MinX + 2, (int)lode.MinZ + 2);
        }
        return (MapCellOf(x) * FineCellsPerMapCell, MapCellOf(z) * FineCellsPerMapCell);
    }

    // ---- Lifecycle -------------------------------------------------------

    /// <summary>Sets up a new building's site, output choice, and plot.</summary>
    private void RenewablesOnBuildingAdded(Building building)
    {
        if (SiteAt(building.Type, building.AnchorX, building.AnchorZ) is ({ } site))
        {
            building.SiteMotherlode = site.Motherlode;
            building.SiteDeposit = site.Deposit;
        }
        if (building.Def.Plot is not null)
        {
            Plant(building);
        }
    }

    /// <summary>Takes a Forester's field with it.</summary>
    private void RenewablesOnBuildingRemoved(Building building)
    {
        if (building.Plot is ResourceField plot)
        {
            building.Plot = null;
            UnregisterField(plot);
        }
    }

    /// <summary>Records a depleted field's cell as worked out (not Wood) and starts a Forester's wait to replant.</summary>
    private void OnFieldDepleted(ResourceField field)
    {
        if (field.Owner is not null)
        {
            return;
        }
        if (field.Type != ResourceType.Wood && !field.IsMotherlode)
        {
            _workedOut[(field.MapX, field.MapZ)] = field.Type;
        }
    }

    // ---- The Forester ----------------------------------------------------

    private void Plant(Building forester)
    {
        (float amount, float grow, float _) = forester.Def.Plot!.Value;
        var field = new ResourceField(_nextFieldId++, ResourceType.Wood, MapCellOf(forester.AnchorX), MapCellOf(forester.AnchorZ), amount)
        {
            Owner = forester,
            Growing = true,
            GrowLeft = grow
        };
        RegisterField(field);
        forester.Plot = field;
        forester.PlotWait = 0f;
    }

    /// <summary>Grows a Forester's field, or counts down its bare plot, at its work speed while it is switched on.</summary>
    private void StepForester(Building forester, double dt)
    {
        float step = (float)dt * WorkSpeed(forester);
        if (forester.Plot is ResourceField plot)
        {
            if (plot.Growing)
            {
                plot.GrowLeft = Math.Max(0f, plot.GrowLeft - step);
                plot.Growing = plot.GrowLeft > 0f;
            }
            return;
        }
        forester.PlotWait -= step;
        if (forester.PlotWait <= 0f)
        {
            Plant(forester);
        }
    }

    // ---- Collectors without a field ----------------------------------------

    /// <summary>
    /// What a collector with a choice may yield, the default first: the Quarry Stone (and Limestone
    /// beside a Highlands mountain), a building on a motherlode its yields, one over a worked-out
    /// cell that deposit. Empty for anything else.
    /// </summary>
    public IReadOnlyList<ResourceType> OutputChoices(Building building)
    {
        if (building.SiteMotherlode is { Motherlode: MotherlodeType kind })
        {
            var yields = new List<ResourceType>();
            foreach ((ResourceType type, float _) in MotherlodeDefs.For(kind).Yields)
            {
                yields.Add(type);
            }
            return yields;
        }
        if (building.SiteDeposit is ResourceType deposit)
        {
            return new[] { deposit };
        }
        if (building.Def.Placement == Placement.BesideMountain)
        {
            return MountainBeside(building.AnchorX, building.AnchorZ, building.Width, building.Depth, highlandsOnly: true)
                ? new[] { ResourceType.Stone, ResourceType.Limestone }
                : new[] { ResourceType.Stone };
        }
        return Array.Empty<ResourceType>();
    }

    /// <summary>What a collector without a field yields now: its choice, or its first.</summary>
    public ResourceType? OutputOf(Building building)
    {
        IReadOnlyList<ResourceType> choices = OutputChoices(building);
        if (building.OutputChoice is ResourceType chosen && ((IList<ResourceType>)choices).Contains(chosen))
        {
            return chosen;
        }
        return choices.Count > 0 ? choices[0] : null;
    }

    /// <summary>Sets a collector's output; false for a building or resource it does not offer.</summary>
    public bool SetOutputChoice(int id, ResourceType type)
    {
        Building? building = BuildingById(id);
        if (building is null || !((IList<ResourceType>)OutputChoices(building)).Contains(type))
        {
            return false;
        }
        building.OutputChoice = type;
        return true;
    }

    /// <summary>Seconds a collector without a field takes for a unit of its output: by the motherlode's share, or its own rate.</summary>
    public float UnitSeconds(Building building)
    {
        if (building.SiteMotherlode is { Motherlode: MotherlodeType kind } && OutputOf(building) is ResourceType type)
        {
            return MotherlodeDefs.For(kind).UnitSeconds(type);
        }
        return building.Def.ProduceSeconds;
    }

    /// <summary>Works toward the next unit at the collector's speed; a whole unit waits until its buffer has room.</summary>
    private void StepProducer(Building building, double dt)
    {
        if (OutputOf(building) is not ResourceType type)
        {
            return;
        }
        float unit = UnitSeconds(building);
        building.Progress += (float)dt * WorkSpeed(building);
        if (building.Progress < unit)
        {
            return;
        }
        if (!BufferHasRoom(building, 1f))
        {
            building.Progress = unit;
            return;
        }
        building.Progress -= unit;
        Put(building.Output, type, 1f);
    }

    // ---- Eating providers (the Rock Crusher) --------------------------------

    /// <summary>
    /// Works a provider that eats through its current unit, taking the next from storage (online)
    /// when it is used up; false when there is nothing to eat.
    /// </summary>
    private bool Feed(Building building, double dt)
    {
        (ResourceType food, float seconds) = building.Def.Eats!.Value;
        if (building.FeedLeft <= 0f)
        {
            _stockpile.TryGetValue(food, out float stock);
            if (!building.Online || stock < 1f - RoomTolerance)
            {
                building.Starved = true;
                return false;
            }
            AddToStockpile(food, -1f);
            building.FeedLeft = seconds;
        }
        building.Starved = false;
        building.FeedLeft = Math.Max(0f, building.FeedLeft - (float)dt * WorkSpeed(building));
        return true;
    }

    // ---- Reach ---------------------------------------------------------------

    /// <summary>The rectangle trucks stop beside and the player collects from: the motherlode under a building on one, else its footprint.</summary>
    public static (int X, int Z, int Width, int Depth) ReachOf(Building building)
    {
        if (building.SiteMotherlode is ResourceField lode)
        {
            int size = lode.Size * FineCellsPerMapCell;
            return ((int)lode.MinX, (int)lode.MinZ, size, size);
        }
        return (building.AnchorX, building.AnchorZ, building.Width, building.Depth);
    }

    /// <summary>Distance from a point to a building's reach (0 inside).</summary>
    public static float DistanceToReach(Vector3 point, Building building)
    {
        (int x, int z, int width, int depth) = ReachOf(building);
        float dx = Mathf.Max(Mathf.Max(x - point.X, 0f), point.X - (x + width));
        float dz = Mathf.Max(Mathf.Max(z - point.Z, 0f), point.Z - (z + depth));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ---- Motherlodes -----------------------------------------------------------

    private ResourceField AddMotherlode(MotherlodeType kind, int mapX, int mapZ)
    {
        var field = new ResourceField(_nextFieldId++, kind, mapX, mapZ);
        RegisterField(field);
        return field;
    }

    /// <summary>Discovers what a field shows: its deposit, or every yield of a motherlode.</summary>
    private void DiscoverField(ResourceField field)
    {
        if (field.Motherlode is MotherlodeType kind)
        {
            foreach ((ResourceType type, float _) in MotherlodeDefs.For(kind).Yields)
            {
                Discover(type);
            }
            return;
        }
        Discover(field.Type);
    }
}
