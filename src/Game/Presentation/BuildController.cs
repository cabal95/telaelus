using System.Collections.Generic;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Handles build mode: selecting a building type with the number keys, rotating
/// with R, showing a footprint-sized ghost centered on the mouse, and issuing a
/// placement command to the simulation on a left click. For an extractor, it also
/// shows the work area as a translucent disc and highlights the matching fields
/// in range. Placement itself is decided by the sim (CanPlace / TryPlace).
/// The pointer is the viewport mouse position, unless a scenario sets
/// <see cref="PointerOverride"/> to a world point; that point is projected to the
/// screen each frame and then goes through the same screen-to-ground ray.
/// </summary>
public partial class BuildController : Node3D
{
    private Simulation _sim = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _ghost = null!;
    private BoxMesh _ghostMesh = null!;
    private StandardMaterial3D _ghostMaterial = null!;
    private MeshInstance3D _workArea = null!;
    private CylinderMesh _workAreaMesh = null!;
    private StandardMaterial3D _highlightMaterial = null!;
    private readonly List<MeshInstance3D> _highlights = new();

    public BuildingType? Selected { get; private set; }

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
        int FieldsInRange);

    private static readonly Color ValidColor = new(0.3f, 0.9f, 0.4f, 0.5f);
    private static readonly Color InvalidColor = new(0.9f, 0.3f, 0.3f, 0.5f);
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
        _ghostMesh = new BoxMesh();
        _ghost = new MeshInstance3D
        {
            Name = "BuildGhost",
            Mesh = _ghostMesh,
            MaterialOverride = _ghostMaterial,
            Visible = false
        };
        AddChild(_ghost);

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
        if (Selected is null || ghost is null)
        {
            HideGhost();
            return;
        }

        GhostInfo g = ghost.Value;
        BuildingDef def = BuildingDefs.For(Selected.Value);

        _ghost.Visible = true;
        _ghostMesh.Size = BuildingViews.BoxSize(g.Width, g.Depth);
        _ghost.Position = g.Center + new Vector3(0f, BuildingViews.BoxHeight * 0.5f, 0f);
        _ghostMaterial.AlbedoColor = g.Placeable ? ValidColor : InvalidColor;

        if (def.Extracts is ResourceType extracts)
        {
            ShowWorkArea(g.Center, def.WorkRadius, extracts);
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
        int fieldsInRange = def.Extracts is ResourceType extracts
            ? _sim.FieldsInRange(center, def.WorkRadius, extracts).Count
            : 0;
        return new GhostInfo(
            anchorX,
            anchorZ,
            width,
            depth,
            center,
            _sim.CanPlace(Selected.Value, anchorX, anchorZ, Facing),
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
            TryPlaceAtMouse();
        }
    }

    private void ShowWorkArea(Vector3 center, float radius, ResourceType type)
    {
        _workArea.Visible = true;
        _workAreaMesh.TopRadius = radius;
        _workAreaMesh.BottomRadius = radius;
        _workArea.Position = center + new Vector3(0f, 0.03f, 0f);

        ShowHighlights(_sim.FieldsInRange(center, radius, type));
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
            case Key.Key1:
                Selected = BuildingType.Mine;
                break;
            case Key.Key2:
                Selected = BuildingType.LumberCamp;
                break;
            case Key.Key3:
                Selected = BuildingType.Smelter;
                break;
            case Key.Key4:
                Selected = BuildingType.Refinery;
                break;
            case Key.R:
                Facing = (Facing)(((int)Facing + 1) % 4);
                break;
            case Key.Key0:
            case Key.Escape:
                Selected = null;
                break;
        }
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

        Vector2 mouse = PointerOverride is Vector3 world
            ? _camera.UnprojectPosition(world)
            : GetViewport().GetMousePosition();
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
