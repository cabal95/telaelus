using Godot;

namespace Game.Presentation;

/// <summary>
/// An orthographic camera that holds the fixed 2.5D angle, smoothly follows a
/// ground target, and zooms with the mouse wheel. Orientation is set once and
/// never changed, so the angle stays locked while the camera pans.
/// </summary>
public partial class CameraRig : Camera3D
{
    /// <summary>Camera position relative to its target. Its ground projection sets the view yaw.</summary>
    public static readonly Vector3 Offset = new(14, 16, 14);

    [Export] public float FollowSpeed = 10f;
    [Export] public float ZoomStep = 1.5f;
    [Export] public float MinSize = 6f;
    [Export] public float MaxSize = 40f;

    private Vector3 _target;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = 16f;
        Position = _target + Offset;
        LookAt(_target, Vector3.Up);
        Current = true;
    }

    public void SetTarget(Vector3 target) => _target = target;

    public override void _Process(double delta)
    {
        Vector3 desired = _target + Offset;
        Position = Position.Lerp(desired, (float)Mathf.Min(1.0, FollowSpeed * delta));
    }

    /// <summary>Positive steps zoom out, negative zoom in.</summary>
    public void Zoom(float steps)
    {
        Size = Mathf.Clamp(Size + steps * ZoomStep, MinSize, MaxSize);
    }
}
