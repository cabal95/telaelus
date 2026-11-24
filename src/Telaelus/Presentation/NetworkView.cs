using System.Collections.Generic;
using System.Linq;
using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// Draws the HQ network on the ground: each hub's coverage as a translucent disc (accent for an
/// online hub, gray for an offline one) and the links between online hubs as thin lines. It shows
/// every hub while the overlay is on (N) or while a building is being placed (with the ghost's own
/// coverage and links when it is a hub), and only the selected hub while the detail panel shows
/// one. Separately, every switched-on offline building carries a small warning ring above it, so
/// outposts read as unconnected without the overlay.
/// Coverage draws as one flat plane under the hubs whose shader tints each point once: the online
/// color where any online hub covers it, else gray where an offline one does, so overlapping
/// discs never darken. The placement ghost's disc draws over it.
/// </summary>
public partial class NetworkView : Node3D
{
    private const float DiscHeight = 0.02f;
    private const float LineWidth = 0.12f;
    private const float MarkerHeight = 2.4f;

    private static readonly Color OnlineColor = new(UiTheme.Accent, 0.16f);
    private static readonly Color OfflineColor = new(0.6f, 0.6f, 0.6f, 0.14f);
    private static readonly Color GhostColor = new(0.3f, 0.9f, 0.4f, 0.2f);
    private static readonly Color LinkColor = new(UiTheme.Accent, 0.7f);

    /// <summary>The most hubs the coverage plane draws (the shader's array size).</summary>
    private const int MaxCoverage = 256;

    private const string CoverageShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform int count = 0;
uniform vec4 hubs[256];
uniform vec4 online_color : source_color;
uniform vec4 offline_color : source_color;

varying vec2 ground;

void vertex() {
    ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    bool online = false;
    bool offline = false;
    for (int i = 0; i < count; i++) {
        vec4 hub = hubs[i];
        if (distance(ground, hub.xy) <= hub.z) {
            online = online || hub.w > 0.5;
            offline = offline || hub.w <= 0.5;
        }
    }
    if (!online && !offline) {
        discard;
    }
    vec4 color = online ? online_color : offline_color;
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
";

    private Simulation _sim = null!;
    private BuildController _builder = null!;
    private BuildingDetail _detail = null!;
    private readonly List<MeshInstance3D> _discs = new();
    private readonly List<MeshInstance3D> _lines = new();
    private readonly List<MeshInstance3D> _markers = new();
    private readonly Dictionary<Color, StandardMaterial3D> _materials = new();
    private readonly List<Vector4> _coverage = new();
    private MeshInstance3D _coveragePlane = null!;
    private ShaderMaterial _coverageMaterial = null!;
    private int _discsUsed;
    private int _linesUsed;

    /// <summary>Whether the overlay (N) is on.</summary>
    public bool OverlayShown { get; set; }

    /// <summary>The hubs whose coverage is drawn this frame, by id (for the snapshot).</summary>
    public List<int> ShownHubs { get; } = new();

    /// <summary>How many offline markers are drawn (for the snapshot).</summary>
    public int OfflineMarkers { get; private set; }

    public void Initialize(Simulation sim, BuildController builder, BuildingDetail detail)
    {
        _sim = sim;
        _builder = builder;
        _detail = detail;
        _coverageMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = CoverageShader
            },
            RenderPriority = -1
        };
        _coverageMaterial.SetShaderParameter("online_color", OnlineColor);
        _coverageMaterial.SetShaderParameter("offline_color", OfflineColor);
        _coveragePlane = new MeshInstance3D
        {
            Mesh = new PlaneMesh(),
            MaterialOverride = _coverageMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        AddChild(_coveragePlane);
    }

    public override void _Process(double delta)
    {
        _discsUsed = 0;
        _linesUsed = 0;
        ShownHubs.Clear();
        _coverage.Clear();

        if (OverlayShown || _builder.Selected is not null)
        {
            foreach (Building hub in _sim.Buildings.Where(b => b.Def.IsHub))
            {
                DrawHub(hub);
            }
            DrawGhost();
        }
        else if (_detail.Building is Building selected && selected.Def.IsHub)
        {
            DrawHub(selected);
        }

        DrawCoverage();
        Hide(_discs, _discsUsed);
        Hide(_lines, _linesUsed);
        DrawMarkers();
    }

    /// <summary>Sizes the coverage plane to this frame's hubs and hands their circles to its shader.</summary>
    private void DrawCoverage()
    {
        if (_coverage.Count == 0)
        {
            _coveragePlane.Visible = false;
            return;
        }
        float minX = _coverage.Min(c => c.X - c.Z);
        float maxX = _coverage.Max(c => c.X + c.Z);
        float minZ = _coverage.Min(c => c.Y - c.Z);
        float maxZ = _coverage.Max(c => c.Y + c.Z);
        ((PlaneMesh)_coveragePlane.Mesh).Size = new Vector2(maxX - minX, maxZ - minZ);
        _coveragePlane.Position = new Vector3((minX + maxX) * 0.5f, 0.06f, (minZ + maxZ) * 0.5f);
        _coveragePlane.Visible = true;
        // Online circles first, so a plane with more hubs than the shader holds drops offline ones.
        Vector4[] circles = _coverage.OrderByDescending(c => c.W).Take(MaxCoverage).ToArray();
        _coverageMaterial.SetShaderParameter("count", circles.Length);
        _coverageMaterial.SetShaderParameter("hubs", circles);
    }

    private void DrawHub(Building hub)
    {
        ShownHubs.Add(hub.Id);
        bool online = hub.Online && hub.Enabled;
        _coverage.Add(new Vector4(hub.Position.X, hub.Position.Z, hub.Def.CoverageRadius, online ? 1f : 0f));
        if (!online)
        {
            return;
        }
        foreach (Building other in _sim.LinkedHubs(hub))
        {
            // Each link once: from the hub with the smaller id, unless only this hub is drawn.
            if (other.Id > hub.Id || !OverlayShown && _builder.Selected is null)
            {
                Line(hub.Position, other.Position);
            }
        }
    }

    private void DrawGhost()
    {
        if (_builder.Selected is not BuildingType type || !BuildingDefs.For(type).IsHub
            || _builder.CurrentGhost() is not BuildController.GhostInfo ghost)
        {
            return;
        }
        BuildingDef def = BuildingDefs.For(type);
        Disc(ghost.Center, def.CoverageRadius, GhostColor);
        foreach (Building hub in _sim.Buildings.Where(b => b.Def.IsHub && b.Online && b.Enabled))
        {
            if (hub.Position.DistanceTo(ghost.Center) <= Mathf.Max(def.LinkRadius, hub.Def.LinkRadius))
            {
                Line(ghost.Center, hub.Position);
            }
        }
    }

    private void Disc(Vector3 center, float radius, Color color)
    {
        MeshInstance3D disc = Take(_discs, ref _discsUsed, () => new CylinderMesh
        {
            Height = DiscHeight,
            RadialSegments = 48
        });
        var mesh = (CylinderMesh)disc.Mesh;
        mesh.TopRadius = radius;
        mesh.BottomRadius = radius;
        disc.MaterialOverride = Material(color);
        disc.Position = center + new Vector3(0f, 0.04f, 0f);
    }

    private void Line(Vector3 from, Vector3 to)
    {
        MeshInstance3D line = Take(_lines, ref _linesUsed, () => new BoxMesh());
        float length = from.DistanceTo(to);
        ((BoxMesh)line.Mesh).Size = new Vector3(LineWidth, 0.03f, length);
        line.MaterialOverride = Material(LinkColor);
        Vector3 mid = (from + to) * 0.5f + new Vector3(0f, 0.08f, 0f);
        line.Position = mid;
        line.Rotation = new Vector3(0f, Mathf.Atan2(to.X - from.X, to.Z - from.Z), 0f);
    }

    private void DrawMarkers()
    {
        int used = 0;
        foreach (Building building in _sim.Buildings.Where(b => b.Enabled && !b.Online))
        {
            MeshInstance3D marker = Take(_markers, ref used, () => new TorusMesh
            {
                InnerRadius = 0.3f,
                OuterRadius = 0.45f,
                Rings = 16,
                RingSegments = 6
            });
            marker.MaterialOverride = Material(UiTheme.Warning);
            marker.Position = building.Position + new Vector3(0f, MarkerHeight, 0f);
            marker.Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f);
        }
        OfflineMarkers = used;
        Hide(_markers, used);
    }

    private MeshInstance3D Take(List<MeshInstance3D> pool, ref int used, System.Func<Mesh> make)
    {
        if (used == pool.Count)
        {
            var instance = new MeshInstance3D
            {
                Mesh = make(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(instance);
            pool.Add(instance);
        }
        MeshInstance3D taken = pool[used++];
        taken.Visible = true;
        return taken;
    }

    private static void Hide(List<MeshInstance3D> pool, int used)
    {
        for (int i = used; i < pool.Count; i++)
        {
            pool[i].Visible = false;
        }
    }

    private StandardMaterial3D Material(Color color)
    {
        if (!_materials.TryGetValue(color, out StandardMaterial3D? material))
        {
            material = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = color
            };
            _materials[color] = material;
        }
        return material;
    }
}
