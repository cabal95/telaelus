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

    /// <summary>The placements this file owns; null for one it does not (the caller applies the standard rule).</summary>
    private bool? CanPlaceRenewable(BuildingType type, BuildingDef def, int anchorX, int anchorZ, Facing facing)
    {
        switch (def.Placement)
        {
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
    /// Forester; null for a building that places freely.
    /// </summary>
    public (int X, int Z)? SnapAnchor(BuildingType type, float worldX, float worldZ)
    {
        BuildingDef def = BuildingDefs.For(type);
        if (def.Placement != Placement.OpenGrassCell)
        {
            return null;
        }
        (int x, int z) = CellAt(worldX, worldZ);
        return (MapCellOf(x) * FineCellsPerMapCell, MapCellOf(z) * FineCellsPerMapCell);
    }

    // ---- Lifecycle -------------------------------------------------------

    /// <summary>Sets up a new building's plot.</summary>
    private void RenewablesOnBuildingAdded(Building building)
    {
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
        (ResourceField? lode, ResourceType? siteDeposit) = SiteOf(building);
        if (lode is { Motherlode: MotherlodeType kind })
        {
            var yields = new List<ResourceType>();
            foreach ((ResourceType type, float _) in MotherlodeDefs.For(kind).Yields)
            {
                yields.Add(type);
            }
            return yields;
        }
        if (siteDeposit is ResourceType deposit)
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
        if (SiteOf(building).Motherlode is { Motherlode: MotherlodeType kind } && OutputOf(building) is ResourceType type)
        {
            return MotherlodeDefs.For(kind).UnitSeconds(type);
        }
        return building.Def.ProduceSeconds;
    }

    /// <summary>
    /// The site a deep level works while no live field is in range: a motherlode of a kind its family
    /// works, or else a worked-out cell of one of its <see cref="BuildingDef.DeepSites"/>, among the map
    /// cells its footprint touches or borders (a motherlode first, then cells by row); null for none.
    /// Any number of deep levels may work the same site.
    /// </summary>
    public (ResourceField? Motherlode, ResourceType? Deposit)? DeepSiteAround(Building building)
    {
        if (!building.Def.IsDeep)
        {
            return null;
        }
        // The fine cells the footprint covers, grown by one on every side.
        int x0 = building.AnchorX - 1;
        int z0 = building.AnchorZ - 1;
        int x1 = building.AnchorX + building.Width;
        int z1 = building.AnchorZ + building.Depth;
        foreach (ResourceField field in _fields)
        {
            if (field.Motherlode is MotherlodeType kind && MotherlodeDefs.For(kind).WorkedBy == building.Type)
            {
                int size = field.Size * FineCellsPerMapCell;
                if (field.MinX <= x1 && field.MinX + size - 1 >= x0 && field.MinZ <= z1 && field.MinZ + size - 1 >= z0)
                {
                    return (field, null);
                }
            }
        }
        for (int mz = MapCellOf(z0); mz <= MapCellOf(z1); mz++)
        {
            for (int mx = MapCellOf(x0); mx <= MapCellOf(x1); mx++)
            {
                if (_workedOut.TryGetValue((mx, mz), out ResourceType deposit) && ((IList<ResourceType>)building.Def.DeepSites).Contains(deposit))
                {
                    return (null, deposit);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The site a collector works without a field: a deep level's current one, or (before it has
    /// stepped, or while offline) the one around it, so its output choice can be set at any time.
    /// </summary>
    private (ResourceField? Motherlode, ResourceType? Deposit) SiteOf(Building building)
    {
        if (building.SiteMotherlode is not null || building.SiteDeposit is not null || !building.Def.IsDeep)
        {
            return (building.SiteMotherlode, building.SiteDeposit);
        }
        return DeepSiteAround(building) ?? (null, null);
    }

    /// <summary>
    /// A deep level's step: a live field in range is worked as by any extractor, at its speed; with
    /// none, the site around it (<see cref="DeepSiteAround"/>) at the deep rate. Switching between them
    /// starts the unit over.
    /// </summary>
    private void StepDeep(Building building, double dt)
    {
        ResourceField? live = building.CurrentField is { Depleted: false } current ? current : NearestFieldInWorkArea(building);
        if (live is not null)
        {
            if (building.SiteMotherlode is not null || building.SiteDeposit is not null)
            {
                building.SiteMotherlode = null;
                building.SiteDeposit = null;
                building.Progress = 0f;
            }
            StepExtractor(building, dt);
            return;
        }
        (ResourceField? lode, ResourceType? deposit) = DeepSiteAround(building) ?? (null, null);
        if (lode != building.SiteMotherlode || deposit != building.SiteDeposit)
        {
            building.SiteMotherlode = lode;
            building.SiteDeposit = deposit;
            building.CurrentField = null;
            building.Progress = 0f;
        }
        if (lode is not null || deposit is not null)
        {
            StepProducer(building, dt);
        }
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

    /// <summary>The rectangle trucks stop beside and the player collects from: its footprint.</summary>
    public static (int X, int Z, int Width, int Depth) ReachOf(Building building)
    {
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
