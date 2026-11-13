using System.Collections.Generic;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// Owns the overlays and the tooltips on one UI layer above the HUD. One overlay is
/// open at a time: opening one closes the other. Escape closes the open overlay. A
/// modal overlay sits over a dimmed backdrop and takes all input (keys are swallowed
/// here; the world reads <see cref="CapturesWorldInput"/> before polling keys); a
/// side panel captures only Escape. Controls register ids so scenarios can click and
/// hover them by name.
/// </summary>
public partial class OverlayHost : UiLayer
{
    private ColorRect _backdrop = null!;
    private readonly List<Overlay> _overlays = new();
    private readonly Dictionary<string, Control> _controls = new();

    private Control? _hoverAnchor;
    private IReadOnlyList<TipLine>? _hoverLines;
    private double _hoverTime;

    public Tooltip HoverTip { get; private set; } = null!;
    public Tooltip PlacementTip { get; private set; } = null!;

    /// <summary>Where a side panel's top may start, in UI units (below the HUD's text).</summary>
    public float SideTop { get; set; }

    /// <summary>The open overlay, or null.</summary>
    public Overlay? Current { get; private set; }

    /// <summary>True while a modal overlay is open: the world gets no input.</summary>
    public bool CapturesWorldInput => Current is { Kind: OverlayKind.Modal };

    public override void _Ready()
    {
        Layer = 10;
        ProcessMode = ProcessModeEnum.Always;
        base._Ready();

        _backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = UiTheme.Backdrop,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseForcePassScrollEvents = false,
            Visible = false
        };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _backdrop.GuiInput += OnBackdropInput;
        Root.AddChild(_backdrop);

        HoverTip = new Tooltip
        {
            Name = "HoverTip"
        };
        PlacementTip = new Tooltip
        {
            Name = "PlacementTip"
        };
    }

    /// <summary>Adds an overlay to the host, closed. Call after the host is in the tree.</summary>
    public void Add(Overlay overlay, string title)
    {
        _overlays.Add(overlay);
        Root.AddChild(overlay);
        overlay.Attach(this, title);
        // Tooltips stay on top of every overlay.
        if (HoverTip.GetParent() is null)
        {
            Root.AddChild(PlacementTip);
            Root.AddChild(HoverTip);
        }
        Root.MoveChild(PlacementTip, -1);
        Root.MoveChild(HoverTip, -1);
    }

    public void Open(Overlay overlay)
    {
        if (Current == overlay)
        {
            return;
        }
        if (Current is not null)
        {
            Close(Current);
        }
        Current = overlay;
        overlay.Visible = true;
        _backdrop.Visible = overlay.Kind == OverlayKind.Modal;
        overlay.OnOpened();
        overlay.Refresh();
        overlay.Place(UiSize, SideTop);
    }

    public void Close(Overlay overlay)
    {
        if (Current != overlay)
        {
            return;
        }
        Current = null;
        overlay.Visible = false;
        _backdrop.Visible = false;
        HideHover(null);
        overlay.OnClosed();
    }

    public void CloseCurrent()
    {
        if (Current is not null)
        {
            Close(Current);
        }
    }

    /// <summary>Registers a control under an id, as "buildMenu/Mine", for <c>clickUi</c> and <c>hoverUi</c>.</summary>
    public void Register(string id, Control control)
    {
        _controls[id] = control;
    }

    public void Unregister(string id)
    {
        _controls.Remove(id);
    }

    /// <summary>The registered control with an id, if it is visible on screen.</summary>
    public Control? Find(string id)
    {
        return _controls.TryGetValue(id, out Control? control) && control.IsVisibleInTree() ? control : null;
    }

    /// <summary>The screen (viewport pixel) center of a control.</summary>
    public Vector2 ScreenCenter(Control control)
    {
        return ToScreen(control.GetGlobalRect().GetCenter());
    }

    /// <summary>Starts the hover tooltip for an anchor control; it shows after the tooltip delay.</summary>
    public void ShowHover(Control anchor, IReadOnlyList<TipLine> lines)
    {
        if (_hoverAnchor != anchor)
        {
            _hoverTime = 0;
            HoverTip.HideTip();
        }
        _hoverAnchor = anchor;
        _hoverLines = lines;
    }

    /// <summary>Hides the hover tooltip if it belongs to the anchor (or any, for null).</summary>
    public void HideHover(Control? anchor)
    {
        if (anchor is null || _hoverAnchor == anchor)
        {
            _hoverAnchor = null;
            _hoverLines = null;
            HoverTip.HideTip();
        }
    }

    public override void _Process(double delta)
    {
        if (Current is not null)
        {
            Current.Refresh();
            if (Current is not null)
            {
                Current.Place(UiSize, SideTop);
            }
        }

        if (_hoverAnchor is not null && _hoverLines is not null)
        {
            if (!_hoverAnchor.IsVisibleInTree())
            {
                HideHover(null);
                return;
            }
            _hoverTime += delta;
            if (_hoverTime >= UiTheme.TooltipDelay)
            {
                // Beside the anchor: to its right, or to its left when that would leave the window.
                Rect2 rect = _hoverAnchor.GetGlobalRect();
                var point = new Vector2(rect.End.X + UiTheme.Gap, rect.Position.Y);
                HoverTip.ShowAt(point, _hoverLines, UiSize);
                if (HoverTip.Position.X < rect.End.X)
                {
                    float left = rect.Position.X - UiTheme.Gap - HoverTip.Size.X;
                    HoverTip.Position = new Vector2(Mathf.Max(UiTheme.Gap, left), HoverTip.Position.Y);
                }
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (Current is null || @event is not InputEventKey key)
        {
            return;
        }

        if (key.Pressed && !key.Echo && key.PhysicalKeycode == Key.Escape)
        {
            Close(Current);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Current.Kind == OverlayKind.Modal)
        {
            if (key.Pressed && !key.Echo)
            {
                Current.OnKey(key.PhysicalKeycode);
            }
            // A modal overlay takes every key.
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button
            && button.ButtonIndex is MouseButton.Left or MouseButton.Right
            && Current is { Kind: OverlayKind.Modal })
        {
            Close(Current);
            _backdrop.AcceptEvent();
        }
    }
}
