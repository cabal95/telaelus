using System.Collections.Generic;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The game's UI layer: the screen overlays (the top bar and the hint overlay, behind
/// everything else here), the dialogs, and the tooltips (on top). One dialog is open at
/// a time: opening one closes the other. Escape closes the open dialog. A modal sits
/// over a dimmed backdrop and takes all input (keys are swallowed here; the world reads
/// <see cref="CapturesWorldInput"/> before polling keys); a panel captures only Escape.
/// Controls register ids so scenarios can click and hover them by name.
/// </summary>
public partial class DialogHost : UiLayer
{
    private ColorRect _backdrop = null!;
    private readonly List<Dialog> _dialogs = new();
    private readonly Dictionary<string, Control> _controls = new();

    private Control? _hoverAnchor;
    private IReadOnlyList<TipLine>? _hoverLines;
    private double _hoverTime;
    private bool _hoverBelow;

    public Tooltip HoverTip { get; private set; } = null!;
    public Tooltip PlacementTip { get; private set; } = null!;

    /// <summary>Where a panel's top may start, in UI units (the bottom of the top bar).</summary>
    public float PanelTop { get; set; }

    /// <summary>The open dialog, or null.</summary>
    public Dialog? Current { get; private set; }

    /// <summary>True while a modal is open: the world gets no input.</summary>
    public bool CapturesWorldInput => Current is { Kind: DialogKind.Modal };

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

    /// <summary>
    /// Adds a screen overlay, drawn behind the backdrop and every dialog. Call after the
    /// host is in the tree.
    /// </summary>
    public void AddOverlay(Control overlay)
    {
        Root.AddChild(overlay);
        Root.MoveChild(overlay, _backdrop.GetIndex());
    }

    /// <summary>Adds a dialog to the host, closed. Call after the host is in the tree.</summary>
    public void Add(Dialog dialog, string title)
    {
        _dialogs.Add(dialog);
        Root.AddChild(dialog);
        dialog.Attach(this, title);
        // Tooltips stay on top of every dialog.
        if (HoverTip.GetParent() is null)
        {
            Root.AddChild(PlacementTip);
            Root.AddChild(HoverTip);
        }
        Root.MoveChild(PlacementTip, -1);
        Root.MoveChild(HoverTip, -1);
    }

    public void Open(Dialog dialog)
    {
        if (Current == dialog)
        {
            return;
        }
        if (Current is not null)
        {
            Close(Current);
        }
        Current = dialog;
        dialog.Visible = true;
        _backdrop.Visible = dialog.Kind == DialogKind.Modal;
        dialog.OnOpened();
        dialog.Refresh();
        dialog.Place(UiSize, PanelTop);
    }

    public void Close(Dialog dialog)
    {
        if (Current != dialog)
        {
            return;
        }
        Current = null;
        dialog.Visible = false;
        _backdrop.Visible = false;
        HideHover(null);
        dialog.OnClosed();
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

    /// <summary>The screen rectangle (pixels) the open panel covers, or null when no panel is open.</summary>
    public Rect2? PanelScreenRect()
    {
        if (Current is not { Kind: DialogKind.Panel } panel)
        {
            return null;
        }
        Rect2 rect = panel.GetGlobalRect();
        return new Rect2(ToScreen(rect.Position), ToScreen(rect.Size));
    }

    /// <summary>
    /// Starts the hover tooltip for an anchor control; it shows after the tooltip delay,
    /// beside the anchor, or below it (for controls in a row, such as the top bar).
    /// </summary>
    public void ShowHover(Control anchor, IReadOnlyList<TipLine> lines, bool below = false)
    {
        _hoverBelow = below;
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
                Current.Place(UiSize, PanelTop);
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
                Rect2 rect = _hoverAnchor.GetGlobalRect();
                if (_hoverBelow)
                {
                    HoverTip.ShowAt(new Vector2(rect.Position.X, rect.End.Y + UiTheme.Gap), _hoverLines, UiSize);
                    return;
                }
                // Beside the anchor: to its right, or to its left when that would leave the window.
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

        if (Current.Kind == DialogKind.Modal)
        {
            if (key.Pressed && !key.Echo)
            {
                Current.OnKey(key.PhysicalKeycode);
            }
            // A modal takes every key.
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button
            && button.ButtonIndex is MouseButton.Left or MouseButton.Right
            && Current is { Kind: DialogKind.Modal })
        {
            Close(Current);
            _backdrop.AcceptEvent();
        }
    }
}
