using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>
/// Handles build mode: selecting a building type with the number keys, showing a
/// ghost at the mouse tile, and issuing a placement command to the simulation on
/// a left click. Placement itself is decided by the sim (CanPlace / TryPlace).
/// </summary>
public partial class BuildController : Node3D
{
    private Simulation _sim = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _ghost = null!;
    private StandardMaterial3D _ghostMaterial = null!;

    public BuildingType? Selected { get; private set; }

    private static readonly Color ValidColor = new(0.3f, 0.9f, 0.4f, 0.5f);
    private static readonly Color InvalidColor = new(0.9f, 0.3f, 0.3f, 0.5f);

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
        _ghost = new MeshInstance3D
        {
            Name = "BuildGhost",
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.9f, 0.9f, 0.9f)
            },
            MaterialOverride = _ghostMaterial,
            Visible = false
        };
        AddChild(_ghost);
    }

    public override void _Process(double delta)
    {
        if (Selected is null || !TryGetMouseTile(out int gridX, out int gridZ))
        {
            _ghost.Visible = false;
            return;
        }

        _ghost.Visible = true;
        _ghost.Position = _sim.TileCenter(gridX, gridZ) + new Vector3(0f, 0.45f, 0f);
        _ghostMaterial.AlbedoColor = _sim.CanPlace(Selected.Value, gridX, gridZ) ? ValidColor : InvalidColor;
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

    private void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Key1:
                Selected = BuildingType.Extractor;
                break;
            case Key.Key2:
                Selected = BuildingType.Smelter;
                break;
            case Key.Key3:
                Selected = BuildingType.Refinery;
                break;
            case Key.Key0:
            case Key.Escape:
                Selected = null;
                break;
        }
    }

    private void TryPlaceAtMouse()
    {
        if (Selected is null || !TryGetMouseTile(out int gridX, out int gridZ))
        {
            return;
        }
        _sim.TryPlaceBuilding(Selected.Value, gridX, gridZ);
    }

    private bool TryGetMouseTile(out int gridX, out int gridZ)
    {
        gridX = 0;
        gridZ = 0;

        Vector2 mouse = GetViewport().GetMousePosition();
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

        Vector3 hit = origin + direction * t;
        gridX = Mathf.RoundToInt(hit.X / _sim.TileSize);
        gridZ = Mathf.RoundToInt(hit.Z / _sim.TileSize);
        return true;
    }
}
