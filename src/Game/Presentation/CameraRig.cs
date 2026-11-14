using System;
using Godot;

namespace Game.Presentation;

/// <summary>
/// An orthographic camera that holds the fixed 2.5D angle, follows the player, and
/// zooms with the mouse wheel. Orientation is set once and never changed, so the angle
/// stays locked while the camera pans.
/// <para>
/// It looks at a ground point (<see cref="GroundCenter"/>). <b>Attached</b>, that point
/// glides after the player. A drag (<see cref="BeginDrag"/>) <b>detaches</b> it: the ground
/// point under the pointer stays under the pointer while it moves, the camera ignores the
/// player, and its center is kept inside <see cref="Bounds"/>. <see cref="ReturnToPlayer"/>
/// brings it back, sliding over <see cref="SlideSeconds"/> or jumping straight there.
/// </para>
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

    /// <summary>How long the camera slides back to the player when they are on screen.</summary>
    public const float SlideSeconds = 0.25f;

    /// <summary>Pointer travel (pixels) before a press becomes a drag; less is a click.</summary>
    public const float DragThreshold = 4f;

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

    private Vector3 _player;
    private Vector3 _center;

    // A drag in progress: where the press was, and the ground point grabbed there.
    private Vector2? _pressAt;
    private Vector3 _grabbed;
    private bool _dragging;

    // A slide back to the player in progress: where it started and how far along it is.
    private Vector3 _slideFrom;
    private float _slideTime = -1f;

    /// <summary>World units visible across <see cref="ReferenceHeight"/> pixels of window height.</summary>
    public float ZoomLevel { get; private set; } = 28f;

    /// <summary>The ground point at the center of the view.</summary>
    public Vector3 GroundCenter => _center;

    /// <summary>Whether the camera has been dragged away and does not follow the player.</summary>
    public bool Detached { get; private set; }

    /// <summary>Whether a press is held that may become a drag (or already is one).</summary>
    public bool Pressing => _pressAt is not null;

    /// <summary>The ground rectangle (x and z) the view's center must stay in while detached, or null for none.</summary>
    public Func<Rect2?> Bounds { get; set; } = () => null;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        ApplyZoom();
        _center = _player;
        Place();
        LookAt(_center, Vector3.Up);
        Current = true;
    }

    /// <summary>The player's position, followed while the camera is attached.</summary>
    public void SetTarget(Vector3 target) => _player = target;

    public override void _Process(double delta)
    {
        ApplyZoom();
        if (_slideTime >= 0f)
        {
            _slideTime += (float)delta;
            float t = Mathf.Min(1f, _slideTime / SlideSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease out
            _center = _slideFrom.Lerp(_player, eased);
            if (t >= 1f)
            {
                _slideTime = -1f;
            }
        }
        else if (!Detached)
        {
            _center = _center.Lerp(_player, (float)Mathf.Min(1.0, FollowSpeed * delta));
        }
        Place();
    }

    /// <summary>
    /// Brings a detached camera back to the player: sliding when they are on screen,
    /// jumping straight there when not. It then follows them again.
    /// </summary>
    public void ReturnToPlayer(bool playerOnScreen)
    {
        CancelDrag();
        if (!Detached && _slideTime < 0f)
        {
            return;
        }
        Detached = false;
        if (playerOnScreen)
        {
            _slideFrom = _center;
            _slideTime = 0f;
        }
        else
        {
            _slideTime = -1f;
            _center = _player;
            Place();
        }
    }

    /// <summary>A press over the world that may become a drag of the map.</summary>
    public void BeginDrag(Vector2 screen)
    {
        _pressAt = screen;
        _dragging = false;
        _grabbed = GroundPoint(screen);
    }

    /// <summary>
    /// The pointer moved while pressed: past <see cref="DragThreshold"/> the camera
    /// detaches, and from then on the grabbed ground point stays under the pointer.
    /// </summary>
    public void DragTo(Vector2 screen)
    {
        if (_pressAt is not Vector2 start)
        {
            return;
        }
        if (!_dragging)
        {
            if (screen.DistanceTo(start) <= DragThreshold)
            {
                return;
            }
            _dragging = true;
            Detached = true;
            _slideTime = -1f;
        }
        Vector3 under = GroundPoint(screen);
        _center += _grabbed - under;
        _center = Clamp(_center);
        Place();
    }

    /// <summary>The press ended (or was cancelled).</summary>
    public void CancelDrag()
    {
        _pressAt = null;
        _dragging = false;
    }

    /// <summary>Whether a world point is inside the visible view.</summary>
    public bool IsOnScreen(Vector3 world, Rect2 visible)
    {
        return visible.HasPoint(UnprojectPosition(world));
    }

    private Vector3 Clamp(Vector3 center)
    {
        if (Bounds() is not Rect2 bounds)
        {
            return center;
        }
        return new Vector3(
            Mathf.Clamp(center.X, bounds.Position.X, bounds.End.X),
            center.Y,
            Mathf.Clamp(center.Z, bounds.Position.Y, bounds.End.Y));
    }

    /// <summary>Puts the camera at its distance from the center along the fixed angle.</summary>
    private void Place()
    {
        Position = _center + Offset.Normalized() * ViewDistance;
    }

    /// <summary>Where the ray through a screen point meets the ground (y = 0).</summary>
    private Vector3 GroundPoint(Vector2 screen)
    {
        Vector3 origin = ProjectRayOrigin(screen);
        Vector3 normal = ProjectRayNormal(screen);
        float t = Mathf.Abs(normal.Y) > 1e-4f ? -origin.Y / normal.Y : 0f;
        Vector3 hit = origin + normal * t;
        return new Vector3(hit.X, 0f, hit.Z);
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
        Place();
    }

    /// <summary>Positive steps zoom out, negative zoom in.</summary>
    public void Zoom(float steps)
    {
        ZoomLevel = Mathf.Clamp(ZoomLevel + steps * ZoomStep, MinSize, MaxSize);
        ApplyZoom();
        Place();
    }
}
