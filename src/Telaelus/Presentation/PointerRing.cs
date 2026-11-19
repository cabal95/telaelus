using Telaelus.Presentation.Ui;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// A pulsing accent ring on the ground that points the trainer's player at a place (the trees to
/// gather, the camp to click). Hidden while <see cref="Target"/> is null.
/// </summary>
public partial class PointerRing : MeshInstance3D
{
    private const float Radius = 2.4f;
    private const float PulseSeconds = 1.2f;

    private StandardMaterial3D _material = null!;
    private double _time;

    /// <summary>Where it points, on the ground, or null for nowhere.</summary>
    public Vector3? Target { get; set; }

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(UiTheme.Accent, 0f)
        };
        Mesh = new TorusMesh
        {
            InnerRadius = Radius - 0.12f,
            OuterRadius = Radius,
            Rings = 48,
            RingSegments = 6
        };
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (Target is not Vector3 target)
        {
            Visible = false;
            return;
        }
        _time += delta;
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)(_time / PulseSeconds * Mathf.Tau));
        Position = target + new Vector3(0f, 0.08f, 0f);
        float grow = 1f + 0.08f * pulse;
        Scale = new Vector3(grow, 0.15f, grow);
        _material.AlbedoColor = new Color(UiTheme.Accent, 0.45f + 0.4f * pulse);
        Visible = true;
    }
}
