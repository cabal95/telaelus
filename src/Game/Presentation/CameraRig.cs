using Godot;

namespace Game.Presentation;

/// <summary>
/// An orthographic camera that holds the fixed 2.5D angle, smoothly follows a
/// ground target, and zooms with the mouse wheel. Orientation is set once and
/// never changed, so the angle stays locked while the camera pans.
///
/// The zoom is the world units visible across <see cref="ReferenceHeight"/> pixels of
/// window height. Godot's orthographic size is the visible world height, which it
/// keeps when the window changes; so each frame the size is the zoom scaled by the
/// viewport height, keeping pixels per world unit fixed: a bigger window shows more
/// of the map at the same scale.
/// </summary>
public partial class CameraRig : Camera3D
{
    /// <summary>
    /// The direction from the target to the camera, and its base distance. Its ground
    /// projection sets the view yaw.
    /// </summary>
    public static readonly Vector3 Offset = new(14, 16, 14);

    /// <summary>
    /// How far the camera sits from its target: the offset's length plus the visible
    /// world height. An orthographic view looks the same from any distance, but the
    /// lower edge of a tall view would otherwise dip below the ground and clip it.
    /// </summary>
    public float ViewDistance => Offset.Length() + Size;

    [Export] public float FollowSpeed = 10f;
    [Export] public float ZoomStep = 1.5f;
    [Export] public float MinSize = 16f;
    [Export] public float MaxSize = 40f;

    /// <summary>The window height (pixels) at which the orthographic size equals the zoom.</summary>
    public const float ReferenceHeight = 720f;

    private Vector3 _target;

    /// <summary>World units visible across <see cref="ReferenceHeight"/> pixels of window height.</summary>
    public float ZoomLevel { get; private set; } = 28f;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        ApplyZoom();
        Position = _target + Offset.Normalized() * ViewDistance;
        LookAt(_target, Vector3.Up);
        Current = true;
    }

    public void SetTarget(Vector3 target) => _target = target;

    public override void _Process(double delta)
    {
        ApplyZoom();
        Vector3 desired = _target + Offset.Normalized() * ViewDistance;
        Position = Position.Lerp(desired, (float)Mathf.Min(1.0, FollowSpeed * delta));
    }

    private void ApplyZoom()
    {
        float height = GetViewport().GetVisibleRect().Size.Y;
        Size = ZoomLevel * (height > 0f ? height / ReferenceHeight : 1f);
    }

    /// <summary>Sets the zoom (world units across the reference height), within the zoom limits.</summary>
    public void ZoomTo(float size)
    {
        ZoomLevel = Mathf.Clamp(size, MinSize, MaxSize);
        ApplyZoom();
    }

    /// <summary>Positive steps zoom out, negative zoom in.</summary>
    public void Zoom(float steps)
    {
        ZoomLevel = Mathf.Clamp(ZoomLevel + steps * ZoomStep, MinSize, MaxSize);
        ApplyZoom();
    }
}
