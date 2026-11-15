using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// A live picture of one building turning about its center: a small offscreen world
/// (the same stage the build menu's pictures use) drawn into a square on a cell
/// background, one revolution every <see cref="SecondsPerTurn"/>. It renders only
/// while visible.
/// </summary>
public partial class TurntableView : PanelContainer
{
    public const float PictureSize = 180f;
    public const float SecondsPerTurn = 18f;

    private readonly SubViewport _view = new()
    {
        Size = new Vector2I(256, 256),
        TransparentBg = true,
        World3D = PictureStage.MakeWorld(),
        RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
        Msaa3D = Viewport.Msaa.Msaa4X
    };

    private Node3D? _pivot;
    private BuildingType? _type;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius));
        var container = new SubViewportContainer
        {
            Stretch = true,
            CustomMinimumSize = new Vector2(PictureSize, PictureSize),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(container);
        container.AddChild(_view);
    }

    /// <summary>Shows a building type (rebuilding the stage only when it changes).</summary>
    public void Show(BuildingType type)
    {
        if (_type == type)
        {
            return;
        }
        _type = type;
        foreach (Node child in _view.GetChildren())
        {
            child.QueueFree();
        }
        _pivot = PictureStage.Fill(_view, type, false);
    }

    public override void _Process(double delta)
    {
        if (_pivot is not null && IsVisibleInTree())
        {
            _pivot.RotateY(Mathf.Tau * (float)delta / SecondsPerTurn);
        }
    }

    /// <summary>The pivot's current turn in degrees (for checks that it turns).</summary>
    public float TurnDegrees => _pivot is null ? 0f : Mathf.RadToDeg(_pivot.Rotation.Y);
}
