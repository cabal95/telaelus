using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Ui;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Handles build mode: a building type selected from the build menu
/// (<see cref="Select"/>), rotating with R, cancelling with Escape, showing a ghost
/// of the selected building centered on the mouse (a
/// translucent copy of its model, tinted green or red, over a plate covering the
/// exact footprint), and issuing a
/// placement command to the simulation on a left click. For an extractor, it also
/// shows the work area as a translucent disc and highlights the matching fields
/// in range. Placement itself is decided by the sim (CanPlace / TryPlace).
/// Outside build mode, a placed building or resource field under the pointer gets a
/// thin accent outline, and a left click on the world reports the building
/// (<see cref="BuildingClicked"/>) or field (<see cref="FieldClicked"/>) clicked, or
/// that nothing was (<see cref="EmptyClicked"/>).
/// The pointer is the viewport mouse position, unless a scenario sets
/// <see cref="PointerOverride"/> to a world point; that point is projected to the
/// screen each frame and then goes through the same screen-to-ground ray.
/// </summary>
public partial class BuildController : Node3D
{
    private Simulation _sim = null!;
    private Camera3D _camera = null!;
    private Node3D _ghost = null!;
    private Node3D? _ghostModel;
    private (BuildingType Type, Facing Facing)? _ghostModelKey;
    private MeshInstance3D _ghostPlate = null!;
    private BoxMesh _ghostPlateMesh = null!;
    private StandardMaterial3D _ghostMaterial = null!;
    private MeshInstance3D _workArea = null!;
    private CylinderMesh _workAreaMesh = null!;
    private StandardMaterial3D _highlightMaterial = null!;
    private readonly List<MeshInstance3D> _highlights = new();

    public BuildingType? Selected { get; private set; }

    /// <summary>The line tool selected instead of a building (Road or Bridge), or null.</summary>
    public LineTool? Line { get; private set; }

    /// <summary>Whether build mode is on: a building or a line tool selected.</summary>
    public bool InBuildMode => Selected is not null || Line is not null;

    // A line drag in progress: its start cell and whether it removes.
    private (int X, int Z)? _lineStart;
    private bool _lineRemoves;
    private MouseButton _lineButton;
    private readonly List<MeshInstance3D> _linePlates = new();
    private StandardMaterial3D _lineReady = null!;
    private StandardMaterial3D _lineShort = null!;
    private StandardMaterial3D _lineBlocked = null!;
    private StandardMaterial3D _lineRemove = null!;
    private Node3D _lineGhost = null!;

    /// <summary>The line drag in progress (start cell, current end cell, whether it removes), or null.</summary>
    public (int FromX, int FromZ, int ToX, int ToZ, bool Removes)? LineDrag =>
        _lineStart is (int sx, int sz) && TryGetPointerCell(out int x, out int z) ? (sx, sz, x, z, _lineRemoves) : null;

    /// <summary>Raised on a left click on a placed building outside build mode.</summary>
    public event Action<Building>? BuildingClicked;

    /// <summary>Raised on a left click on a resource field outside build mode.</summary>
    public event Action<ResourceField>? FieldClicked;

    /// <summary>Raised on a left click on the world that hits no building or field, outside build mode.</summary>
    public event Action? EmptyClicked;

    /// <summary>The placed building under the pointer outside build mode, or null.</summary>
    public Building? Hovered { get; private set; }

    /// <summary>The resource field under the pointer outside build mode (when no building is), or null.</summary>
    public ResourceField? HoveredField { get; private set; }

    /// <summary>The field a detail panel shows; it keeps its outline while set.</summary>
    public ResourceField? FocusedField { get; set; }

    private Node3D _hoverOutline = null!;
    private Node3D _focusOutline = null!;

    /// <summary>The building a detail panel shows; it keeps its outline while set.</summary>
    public Building? Focused { get; set; }

    public Facing Facing { get; private set; } = Facing.North;

    /// <summary>Matching fields in the ghost's work area (extractors only; 0 otherwise).</summary>
    public int FieldsInRange { get; private set; }

    /// <summary>
    /// A world point on the ground to aim at instead of the mouse (set by scenarios
    /// and the live channel). Null means use the real mouse.
    /// </summary>
    public Vector3? PointerOverride { get; set; }

    /// <summary>The placement ghost as it stands for the current pointer.</summary>
    public readonly record struct GhostInfo(
        int AnchorX,
        int AnchorZ,
        int Width,
        int Depth,
        Vector3 Center,
        bool Placeable,
        bool Affordable,
        int FieldsInRange,
        bool Online);

    private static readonly Color ValidColor = new(0.3f, 0.9f, 0.4f, 0.45f);
    private static readonly Color InvalidColor = new(0.9f, 0.3f, 0.3f, 0.45f);
    private const float PlateHeight = 0.04f;
    private static readonly Color WorkAreaColor = new(0.95f, 0.85f, 0.35f, 0.18f);
    private static readonly Color HighlightColor = new(0.95f, 0.85f, 0.35f, 0.35f);

    public void Initialize(Simulation sim, Camera3D camera)
    {
        _sim = sim;
        _camera = camera;
    }

    public override void _Ready()
    {
        _ghostMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = ValidColor
        };
        _ghost = new Node3D
        {
            Name = "BuildGhost",
            Visible = false
        };
        AddChild(_ghost);
        _ghostPlateMesh = new BoxMesh();
        _ghostPlate = new MeshInstance3D
        {
            Name = "FootprintPlate",
            Mesh = _ghostPlateMesh,
            MaterialOverride = _ghostMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _ghost.AddChild(_ghostPlate);

        _workAreaMesh = new CylinderMesh
        {
            Height = 0.02f,
            RadialSegments = 48
        };
        _workArea = new MeshInstance3D
        {
            Name = "WorkArea",
            Mesh = _workAreaMesh,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = WorkAreaColor
            },
            Visible = false
        };
        AddChild(_workArea);

        var outlineMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(UiTheme.Accent, 0.55f)
        };
        _hoverOutline = MakeOutline("HoverOutline", outlineMaterial);
        _focusOutline = MakeOutline("FocusOutline", outlineMaterial);

        _lineReady = PlateMaterial(ValidColor);
        _lineShort = PlateMaterial(new Color(UiTheme.Warning, 0.45f));
        _lineBlocked = PlateMaterial(InvalidColor);
        _lineRemove = PlateMaterial(new Color(UiTheme.Danger, 0.5f));
        _lineGhost = new Node3D
        {
            Name = "LineGhost"
        };
        AddChild(_lineGhost);

        _highlightMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = HighlightColor
        };
    }

    public override void _Process(double delta)
    {
        GhostInfo? ghost = CurrentGhost();
        FieldsInRange = ghost?.FieldsInRange ?? 0;
        UpdateHover();
        ShowLineGhost();
        if (Selected is null || ghost is null)
        {
            HideGhost();
            return;
        }

        GhostInfo g = ghost.Value;
        BuildingDef def = BuildingDefs.For(Selected.Value);

        _ghost.Visible = true;
        _ghost.Position = g.Center;
        ShowGhostModel(Selected.Value, Facing);
        _ghostPlateMesh.Size = new Vector3(g.Width, PlateHeight, g.Depth);
        _ghostPlate.Position = new Vector3(0f, PlateHeight * 0.5f, 0f);
        _ghostMaterial.AlbedoColor = g.Placeable && g.Affordable ? ValidColor : InvalidColor;

        if (def.IsExtractor)
        {
            ShowWorkArea(g.Center, def.WorkRadius, def.Works);
        }
        else
        {
            _workArea.Visible = false;
            ShowHighlights(new List<ResourceField>());
        }
    }

    /// <summary>Returns build mode to its starting state: nothing selected, facing North, real mouse.</summary>
    public void ResetBuildMode()
    {
        Selected = null;
        Line = null;
        _lineStart = null;
        Facing = Facing.North;
        PointerOverride = null;
    }

    /// <summary>
    /// Computes the ghost for the current selection, facing, and pointer, or null
    /// when nothing is selected or the pointer misses the ground.
    /// </summary>
    public GhostInfo? CurrentGhost()
    {
        if (Selected is null || !TryGetMouseAnchor(Selected.Value, out int anchorX, out int anchorZ))
        {
            return null;
        }

        BuildingDef def = BuildingDefs.For(Selected.Value);
        (int width, int depth) = def.Footprint(Facing);
        Vector3 center = def.Center(anchorX, anchorZ, Facing);
        int fieldsInRange = def.IsExtractor
            ? _sim.FieldsInRange(center, def.WorkRadius, def.Works).Count
            : 0;
        return new GhostInfo(
            anchorX,
            anchorZ,
            width,
            depth,
            center,
            _sim.CanPlace(Selected.Value, anchorX, anchorZ, Facing),
            _sim.CanAfford(Selected.Value),
            fieldsInRange,
            _sim.WouldBeOnline(Selected.Value, anchorX, anchorZ, Facing));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Line is not null && @event is InputEventMouseButton line && HandleLineInput(line))
        {
            // The line tool owns the left and right buttons, so a right drag removes paving
            // instead of panning the camera.
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            HandleKey(key.PhysicalKeycode);
        }
        else if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && !mb.CtrlPressed)
        {
            // (Ctrl + left belongs to dragging the camera, in GameRoot.)
            if (Selected is null)
            {
                ClickWorld();
            }
            else
            {
                TryPlaceAtMouse();
            }
        }
    }

    private void ShowWorkArea(Vector3 center, float radius, IReadOnlyList<ResourceType> types)
    {
        _workArea.Visible = true;
        _workAreaMesh.TopRadius = radius;
        _workAreaMesh.BottomRadius = radius;
        _workArea.Position = center + new Vector3(0f, 0.03f, 0f);

        ShowHighlights(_sim.FieldsInRange(center, radius, types));
    }

    private void ShowHighlights(List<ResourceField> fields)
    {
        while (_highlights.Count < fields.Count)
        {
            var highlight = new MeshInstance3D
            {
                Name = "FieldHighlight",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(Simulation.FineCellsPerMapCell, 0.04f, Simulation.FineCellsPerMapCell)
                },
                MaterialOverride = _highlightMaterial
            };
            AddChild(highlight);
            _highlights.Add(highlight);
        }

        for (int i = 0; i < _highlights.Count; i++)
        {
            bool used = i < fields.Count;
            _highlights[i].Visible = used;
            if (used)
            {
                _highlights[i].Position = fields[i].Center + new Vector3(0f, 0.05f, 0f);
            }
        }
    }

    /// <summary>
    /// Shows a copy of the building's model for the ghost, rebuilt only when the
    /// type or facing changes, with every surface drawn in the shared tint material.
    /// </summary>
    private void ShowGhostModel(BuildingType type, Facing facing)
    {
        if (_ghostModelKey == (type, facing))
        {
            return;
        }
        _ghostModel?.QueueFree();
        _ghostModel = BuildingModels.Create(type, facing);
        _ghostModelKey = (type, facing);
        TintAll(_ghostModel);
        _ghost.AddChild(_ghostModel);
    }

    private void TintAll(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            geometry.MaterialOverride = _ghostMaterial;
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        foreach (Node child in node.GetChildren())
        {
            TintAll(child);
        }
    }

    private void HideGhost()
    {
        _ghost.Visible = false;
        _workArea.Visible = false;
        ShowHighlights(new List<ResourceField>());
    }

    private void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.R:
                Facing = (Facing)(((int)Facing + 1) % 4);
                break;
            case Key.Escape:
                if (_lineStart is not null)
                {
                    _lineStart = null;
                }
                else
                {
                    Cancel();
                }
                break;
        }
    }

    /// <summary>The placed building under the pointer's ground point, or null (also in unexplored cells).</summary>
    private Building? BuildingUnderPointer()
    {
        if (!TryGetExploredPointerCell(out int x, out int z))
        {
            return null;
        }
        return _sim.BuildingAt(x, z);
    }

    /// <summary>The resource field covering the pointer's ground point, or null (also in unexplored cells).</summary>
    private ResourceField? FieldUnderPointer()
    {
        if (!TryGetExploredPointerCell(out int x, out int z))
        {
            return null;
        }
        return _sim.FieldAt(x, z);
    }

    /// <summary>The explored fine cell under the pointer outside build mode and off the UI, or null.</summary>
    public (int X, int Z)? PointedCell()
    {
        return PointingAtWorld && TryGetExploredPointerCell(out int x, out int z) ? (x, z) : null;
    }

    /// <summary>The fine cell under the pointer, when its map cell is explored.</summary>
    private bool TryGetExploredPointerCell(out int x, out int z)
    {
        x = 0;
        z = 0;
        if (!TryGetMouseGround(out Vector3 hit))
        {
            return false;
        }
        (x, z) = Simulation.CellAt(hit.X, hit.Z);
        return _sim.IsExplored(Simulation.MapCellOf(x), Simulation.MapCellOf(z));
    }

    /// <summary>An outline: four thin accent bars, laid around a footprint by <see cref="ShowOutline"/>.</summary>
    private Node3D MakeOutline(string name, Material material)
    {
        var outline = new Node3D
        {
            Name = name,
            Visible = false
        };
        for (int i = 0; i < 4; i++)
        {
            outline.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh(),
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        AddChild(outline);
        return outline;
    }

    /// <summary>
    /// Outlines the footprint of the building under the pointer outside build mode
    /// (unless the pointer is over a UI control), and of the <see cref="Focused"/>
    /// building, with a thin accent plate each.
    /// </summary>
    private void UpdateHover()
    {
        bool overUi = PointerOverride is null && GetViewport().GuiGetHoveredControl() is not null;
        bool pointing = Selected is null && !overUi;
        Hovered = pointing ? BuildingUnderPointer() : null;
        HoveredField = pointing && Hovered is null ? FieldUnderPointer() : null;

        ShowOutline(_focusOutline, Focused is not null ? Footprint(Focused) : FocusedField is not null ? Footprint(FocusedField) : null);
        bool hoverIsFocused = (Hovered is not null && Hovered == Focused) || (HoveredField is not null && HoveredField == FocusedField);
        ShowOutline(_hoverOutline, hoverIsFocused ? null
            : Hovered is not null ? Footprint(Hovered)
            : HoveredField is not null ? Footprint(HoveredField)
            : null);
    }

    private static (Vector3 Center, float Width, float Depth) Footprint(Building building)
    {
        return (building.Position, building.Width, building.Depth);
    }

    private static (Vector3 Center, float Width, float Depth) Footprint(ResourceField field)
    {
        return (field.Center, field.Width, field.Width);
    }

    /// <summary>
    /// A thin frame just outside a footprint (its bars <see cref="OutlineWidth"/> wide,
    /// starting <see cref="OutlineGap"/> outside the edge), or hidden for none.
    /// </summary>
    private static void ShowOutline(Node3D outline, (Vector3 Center, float Width, float Depth)? footprint)
    {
        outline.Visible = footprint is not null;
        if (footprint is not (Vector3 center, float width, float depth))
        {
            return;
        }
        outline.Position = center + new Vector3(0f, PlateHeight * 0.5f, 0f);
        float halfX = width * 0.5f + OutlineGap + OutlineWidth * 0.5f;
        float halfZ = depth * 0.5f + OutlineGap + OutlineWidth * 0.5f;
        float spanX = halfX * 2f + OutlineWidth;
        float spanZ = halfZ * 2f;
        // North and South bars run the full width (covering the corners); East and West fill between.
        Place(outline.GetChild<MeshInstance3D>(0), new Vector3(0f, 0f, -halfZ), new Vector3(spanX, PlateHeight, OutlineWidth));
        Place(outline.GetChild<MeshInstance3D>(1), new Vector3(0f, 0f, halfZ), new Vector3(spanX, PlateHeight, OutlineWidth));
        Place(outline.GetChild<MeshInstance3D>(2), new Vector3(-halfX, 0f, 0f), new Vector3(OutlineWidth, PlateHeight, spanZ));
        Place(outline.GetChild<MeshInstance3D>(3), new Vector3(halfX, 0f, 0f), new Vector3(OutlineWidth, PlateHeight, spanZ));

        static void Place(MeshInstance3D bar, Vector3 position, Vector3 size)
        {
            bar.Position = position;
            ((BoxMesh)bar.Mesh).Size = size;
        }
    }

    private const float OutlineWidth = 0.14f;
    private const float OutlineGap = 0.04f;

    private void ClickWorld()
    {
        if (BuildingUnderPointer() is Building building)
        {
            BuildingClicked?.Invoke(building);
        }
        else if (FieldUnderPointer() is ResourceField field)
        {
            FieldClicked?.Invoke(field);
        }
        else
        {
            EmptyClicked?.Invoke();
        }
    }

    /// <summary>Selects a building for placement; a locked building cannot be selected.</summary>
    public void Select(BuildingType type)
    {
        if (_sim.IsUnlocked(type))
        {
            Selected = type;
            Line = null;
            _lineStart = null;
        }
    }

    /// <summary>Selects the road or bridge line tool.</summary>
    public void SelectLine(LineTool tool)
    {
        Selected = null;
        Line = tool;
        _lineStart = null;
    }

    /// <summary>Leaves build mode (the facing is kept).</summary>
    public void Cancel()
    {
        Selected = null;
        Line = null;
        _lineStart = null;
    }

    /// <summary>
    /// A mouse button with a line tool: the left button (or the right, or Shift with the left, to
    /// remove) starts a drag at the cell under the pointer; its release lays or removes the line.
    /// The other button cancels a drag. True when the event was the line tool's.
    /// </summary>
    private bool HandleLineInput(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex is not (MouseButton.Left or MouseButton.Right) || mb.CtrlPressed)
        {
            return false;
        }
        if (mb.Pressed)
        {
            if (_lineStart is not null)
            {
                _lineStart = null;
                return true;
            }
            if (!TryGetPointerCell(out int x, out int z))
            {
                return true;
            }
            _lineStart = (x, z);
            _lineButton = mb.ButtonIndex;
            _lineRemoves = mb.ButtonIndex == MouseButton.Right || mb.ShiftPressed;
            return true;
        }
        if (_lineStart is not (int sx, int sz) || mb.ButtonIndex != _lineButton)
        {
            return true;
        }
        _lineStart = null;
        if (!TryGetPointerCell(out int ex, out int ez))
        {
            return true;
        }
        if (Line == LineTool.Road)
        {
            _ = _lineRemoves ? _sim.TryRemoveRoad(sx, sz, ex, ez) : _sim.TryLayRoad(sx, sz, ex, ez);
        }
        else
        {
            _ = _lineRemoves ? _sim.TryRemoveBridge(sx, sz, ex, ez) : (_sim.TryLayBridge(sx, sz, ex, ez) ? 1 : 0);
        }
        return true;
    }

    /// <summary>The fine cell under the pointer's ground point (explored or not).</summary>
    private bool TryGetPointerCell(out int x, out int z)
    {
        x = 0;
        z = 0;
        if (!TryGetMouseGround(out Vector3 hit))
        {
            return false;
        }
        (x, z) = Simulation.CellAt(hit.X, hit.Z);
        return true;
    }

    /// <summary>
    /// The line ghost: a plate on each tile of the drag (or the tile under the pointer before one
    /// starts): green to lay, amber where the stock runs out, red where it cannot go, and a remove
    /// tint on what a removing drag would take away.
    /// </summary>
    private void ShowLineGhost()
    {
        var plates = new List<((int X, int Z) Tile, Material Material)>();
        if (Line is LineTool tool && TryGetPointerCell(out int px, out int pz))
        {
            (int sx, int sz) = _lineStart ?? (px, pz);
            if (_lineStart is not null && _lineRemoves)
            {
                foreach ((int X, int Z) tile in Simulation.RoadLine(sx, sz, px, pz))
                {
                    bool there = tool == LineTool.Road ? _sim.RoadTiles.Contains(tile) : _sim.IsBridge(tile.X, tile.Z);
                    if (there)
                    {
                        plates.Add((tile, _lineRemove));
                    }
                }
            }
            else if (tool == LineTool.Road)
            {
                foreach (RoadPlanTile planned in _sim.PlanRoad(sx, sz, px, pz).Tiles)
                {
                    Material? material = planned.State switch
                    {
                        RoadTileState.Ready => _lineReady,
                        RoadTileState.Short => _lineShort,
                        RoadTileState.Blocked => _lineBlocked,
                        _ => null
                    };
                    if (material is not null)
                    {
                        plates.Add((planned.Tile, material));
                    }
                }
            }
            else
            {
                List<(int X, int Z)>? tiles = Simulation.BridgeLine(sx, sz, px, pz);
                bool ok = _lineStart is null || (_sim.BridgeProblem(tiles) is null);
                Material material = !ok ? _lineBlocked
                    : _lineStart is not null && !_sim.CanAffordBridge(tiles!.Count) ? _lineShort
                    : _lineReady;
                foreach ((int X, int Z) tile in tiles ?? new List<(int X, int Z)> { Simulation.TileOf(px, pz) })
                {
                    plates.Add((tile, material));
                }
            }
        }
        while (_linePlates.Count < plates.Count)
        {
            var plate = new MeshInstance3D
            {
                Mesh = new BoxMesh
                {
                    Size = new Vector3(1.9f, PlateHeight, 1.9f)
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _lineGhost.AddChild(plate);
            _linePlates.Add(plate);
        }
        for (int i = 0; i < _linePlates.Count; i++)
        {
            MeshInstance3D plate = _linePlates[i];
            plate.Visible = i < plates.Count;
            if (i < plates.Count)
            {
                ((int x, int z), Material material) = plates[i];
                plate.Position = new Vector3(x + 1f, PlateHeight * 0.5f + 0.05f, z + 1f);
                plate.MaterialOverride = material;
            }
        }
    }

    private static StandardMaterial3D PlateMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color
        };
    }

    /// <summary>The placement tooltip's lines for a line tool: its name, the drag's length and cost (or what is wrong), and the keys.</summary>
    private List<TipLine> LineLines(LineTool tool)
    {
        var lines = new List<TipLine>
        {
            new(tool == LineTool.Road ? "Road" : "Bridge", UiTheme.Accent)
        };
        if (LineDrag is (int fx, int fz, int tx, int tz, bool removes))
        {
            if (removes)
            {
                lines.Add(new TipLine("Remove: half the cost back"));
            }
            else if (tool == LineTool.Road)
            {
                RoadPlan plan = _sim.PlanRoad(fx, fz, tx, tz);
                int layable = plan.Tiles.Count(t => t.State is RoadTileState.Ready or RoadTileState.Short);
                lines.Add(new TipLine($"Road: {layable} tile{(layable == 1 ? "" : "s")}, {CostText(plan.Cost)}"));
                lines.AddRange(ShortLines(plan.Cost));
            }
            else
            {
                List<(int X, int Z)>? tiles = Simulation.BridgeLine(fx, fz, tx, tz);
                if (_sim.BridgeProblem(tiles) is string problem)
                {
                    lines.Add(new TipLine(problem, UiTheme.Warning));
                }
                else
                {
                    IReadOnlyList<ResourceAmount> cost = Simulation.BridgeCost(tiles!.Count);
                    lines.Add(new TipLine($"Bridge: {tiles.Count} tile{(tiles.Count == 1 ? "" : "s")}, {CostText(cost)}"));
                    lines.AddRange(ShortLines(cost));
                }
            }
        }
        else
        {
            lines.Add(tool == LineTool.Road
                ? new TipLine("1 Stone a cell, 2 on Sand, Snow, and Mud")
                : new TipLine("3 Wood and 1 Stone a cell; straight over water"));
        }
        lines.Add(new TipLine("Drag to lay   Shift or right-drag to remove   Esc cancel", UiTheme.TextDim));
        return lines;
    }

    private static string CostText(IReadOnlyList<ResourceAmount> cost)
    {
        return cost.Count == 0 ? "nothing to lay" : string.Join(", ", cost.Select(c => $"{c.Amount:0} {ResourceDefs.For(c.Type).Name}"));
    }

    private IEnumerable<TipLine> ShortLines(IReadOnlyList<ResourceAmount> cost)
    {
        foreach (ResourceAmount c in cost)
        {
            int have = HudOverlay.Whole(_sim, c.Type);
            if (have < c.Amount)
            {
                yield return new TipLine($"{c.Amount:0} {ResourceDefs.For(c.Type).Name} (have {have})", UiTheme.Short);
            }
        }
    }

    /// <summary>The pointer's screen position: the projected override, or the mouse.</summary>
    /// <summary>Whether the pointer is over the world outside build mode (not over a UI control).</summary>
    public bool PointingAtWorld => !InBuildMode && !(PointerOverride is null && GetViewport().GuiGetHoveredControl() is not null);

    public Vector2 PointerScreen()
    {
        return PointerOverride is Vector3 world
            ? _camera.UnprojectPosition(world)
            : GetViewport().GetMousePosition();
    }

    /// <summary>
    /// The placement tooltip's lines while a building is selected: its name, its cost
    /// (short amounts marked), fields in range for an extractor, whether the spot is online,
    /// and the key hint.
    /// </summary>
    public List<TipLine> PlacementLines()
    {
        var lines = new List<TipLine>();
        if (Line is LineTool tool)
        {
            return LineLines(tool);
        }
        if (Selected is not BuildingType type)
        {
            return lines;
        }
        BuildingDef def = BuildingDefs.For(type);
        lines.Add(new TipLine(def.Name, UiTheme.Accent));
        lines.AddRange(BuildMenu.CostLines(_sim, type));
        if (def.IsExtractor)
        {
            lines.Add(new TipLine($"Fields in range: {FieldsInRange}"));
        }
        if (CurrentGhost() is GhostInfo ghost)
        {
            lines.Add(ghost.Online ? new TipLine("Online") : new TipLine("Offline (no hub in reach)", UiTheme.Warning));
            foreach (CapacityType capacity in Simulation.CapacityTypes)
            {
                float draw = def.Draw(capacity);
                float give = def.Supply(capacity);
                if (draw <= 0f && give <= 0f)
                {
                    continue;
                }
                // The network's figure with this building added, if it would be online.
                float demand = ghost.Online ? draw : 0f;
                float supply = ghost.Online ? give : 0f;
                CapacityReading reading = _sim.Capacity(capacity);
                string role = draw > 0f ? $"Draws {draw:0} {capacity}" : $"Supplies {give:0} {capacity}";
                lines.Add(new TipLine($"{role}: {HudOverlay.CapacityText(_sim, capacity, demand, supply)}",
                    HudOverlay.CapacityColor(reading.Demand + demand, reading.Supply + supply)));
            }
        }
        lines.Add(new TipLine("R rotate   Esc cancel", UiTheme.TextDim));
        return lines;
    }

    private void TryPlaceAtMouse()
    {
        if (Selected is null || !TryGetMouseAnchor(Selected.Value, out int anchorX, out int anchorZ))
        {
            return;
        }
        _sim.TryPlaceBuilding(Selected.Value, anchorX, anchorZ, Facing);
    }

    // The anchor whose footprint center is nearest the mouse's ground point.
    private bool TryGetMouseAnchor(BuildingType type, out int anchorX, out int anchorZ)
    {
        anchorX = 0;
        anchorZ = 0;
        if (!TryGetMouseGround(out Vector3 hit))
        {
            return false;
        }

        // A Forester or a building over a site snaps to map cells (or a motherlode's center).
        if (_sim.SnapAnchor(type, hit.X, hit.Z) is (int snapX, int snapZ))
        {
            anchorX = snapX;
            anchorZ = snapZ;
            return true;
        }
        (int width, int depth) = BuildingDefs.For(type).Footprint(Facing);
        anchorX = Mathf.RoundToInt(hit.X - width * 0.5f);
        anchorZ = Mathf.RoundToInt(hit.Z - depth * 0.5f);
        return true;
    }

    private bool TryGetMouseGround(out Vector3 hit)
    {
        hit = Vector3.Zero;

        Vector2 mouse = PointerScreen();
        Vector3 origin = _camera.ProjectRayOrigin(mouse);
        Vector3 direction = _camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return false;
        }

        float t = -origin.Y / direction.Y;
        if (t < 0f)
        {
            return false;
        }

        hit = origin + direction * t;
        return true;
    }
}

/// <summary>The line tools of build mode.</summary>
public enum LineTool
{
    Road,
    Bridge
}
