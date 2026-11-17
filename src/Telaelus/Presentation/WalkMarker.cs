using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// A thin accent ring on the ground where the player's walk ends (<see cref="Player.Destination"/>),
/// shown while they walk there and fading out over <see cref="FadeSeconds"/> once they stop.
/// </summary>
public partial class WalkMarker : MeshInstance3D
{
    public const float FadeSeconds = 0.3f;
    private const float Opacity = 0.8f;

    private StandardMaterial3D _material = null!;
    private float _alpha;

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
            InnerRadius = 0.32f,
            OuterRadius = 0.42f,
            Rings = 24,
            RingSegments = 6
        };
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        Scale = new Vector3(1f, 0.15f, 1f);
        Visible = false;
    }

    /// <summary>Follows the player's walk: at its end while walking, fading out after.</summary>
    public void Sync(Player player, double delta)
    {
        if (player.Destination is Vector3 destination)
        {
            Position = destination + new Vector3(0f, 0.05f, 0f);
            _alpha = Opacity;
        }
        else
        {
            _alpha = Mathf.Max(0f, _alpha - (float)delta * Opacity / FadeSeconds);
        }
        Visible = _alpha > 0f;
        _material.AlbedoColor = new Color(UiTheme.Accent, _alpha);
    }
}
