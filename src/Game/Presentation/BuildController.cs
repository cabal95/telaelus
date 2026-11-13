using System;
using System.Collections.Generic;
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
/// Outside build mode, a placed building under the pointer gets a thin accent
/// outline, and a left click on the world reports the building clicked
/// (<see cref="BuildingClicked"/>) or that nothing was (<see cref="EmptyClicked"/>).
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

    /// <summary>Raised on a left click on a placed building outside build mode.</summary>
    public event Action<Building>? BuildingClicked;

    /// <summary>Raised on a left click on the world that hits no building, outside build mode.</summary>
    public event Action? EmptyClicked;

    /// <summary>The placed building under the pointer outside build mode, or null.</summary>
    public Building? Hovered { get; private set; }

    private MeshInstance3D _hoverOutline = null!;
    private MeshInstance3D _focusOutline = null!;

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
        int FieldsInRange);

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
            fieldsInRange);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            HandleKey(key.PhysicalKeycode);
        }
        else if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
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
                Cancel();
                break;
        }
    }

    /// <summary>The placed building under the pointer's ground point, or null.</summary>
    private Building? BuildingUnderPointer()
    {
        if (!TryGetMouseGround(out Vector3 hit))
        {
            return null;
        }
        (int x, int z) = Simulation.CellAt(hit.X, hit.Z);
        return _sim.BuildingAt(x, z);
    }

    private MeshInstance3D MakeOutline(string name, Material material)
    {
        var outline = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
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
        Hovered = Selected is null && !overUi ? BuildingUnderPointer() : null;
        ShowOutline(_hoverOutline, Hovered != Focused ? Hovered : null);
        ShowOutline(_focusOutline, Focused);
    }

    private static void ShowOutline(MeshInstance3D outline, Building? building)
    {
        outline.Visible = building is not null;
        if (building is null)
        {
            return;
        }
        const float Border = 0.12f;
        ((BoxMesh)outline.Mesh).Size = new Vector3(building.Width + Border * 2, PlateHeight, building.Depth + Border * 2);
        outline.Position = building.Position + new Vector3(0f, PlateHeight * 0.5f, 0f);
    }

    private void ClickWorld()
    {
        if (BuildingUnderPointer() is Building building)
        {
            BuildingClicked?.Invoke(building);
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
        }
    }

    /// <summary>Leaves build mode (the facing is kept).</summary>
    public void Cancel()
    {
        Selected = null;
    }

    /// <summary>The pointer's screen position: the projected override, or the mouse.</summary>
    public Vector2 PointerScreen()
    {
        return PointerOverride is Vector3 world
            ? _camera.UnprojectPosition(world)
            : GetViewport().GetMousePosition();
    }

    /// <summary>
    /// The placement tooltip's lines while a building is selected: its name, its cost
    /// (short amounts marked), fields in range for an extractor, and the key hint.
    /// </summary>
    public List<TipLine> PlacementLines()
    {
        var lines = new List<TipLine>();
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
