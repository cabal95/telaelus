using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>How long a toast stays and whether it can be closed by hand.</summary>
public enum ToastStyle
{
    /// <summary>Good to know, fine to miss: gone after 5 seconds, no close button.</summary>
    Brief,

    /// <summary>Worth reading: an X to close it and a countdown bar, gone after 15 seconds.</summary>
    Lasting,

    /// <summary>The trainer's step card: kept on top until its step is done; its X asks to stop the trainer.</summary>
    Step
}

/// <summary>A toast as shown, for the snapshot.</summary>
public sealed record ToastInfo(ToastStyle Style, string Title, string? Detail, double Remaining);

/// <summary>The trainer's step card as shown: its header ("Trainer 3 / 13", or "Done"), instruction, and hint.</summary>
public sealed record StepCardInfo(string Header, string Text, string? Hint);

/// <summary>
/// Toast notifications, stacked against the right edge below the top bar, newest on top. A toast
/// slides in, counts down on game time (so it waits while the game is paused), holds while the
/// pointer is over it, and fades out. At most <see cref="MaxShown"/> show: a new one pushes out the
/// oldest brief toast, or the oldest of all when every one is lasting. The trainer's step card
/// (<see cref="ShowStep"/>) sits above them all, never times out, and is never pushed out. Only the toasts take the
/// mouse, so the world around them keeps its clicks; like every overlay, they never take keys.
/// </summary>
public partial class Notifications : HudOverlay
{
    public const double BriefSeconds = 5.0;
    public const double LastingSeconds = 15.0;
    public const int MaxShown = 4;
    public const float Width = 280f;
    private const double FadeSeconds = 0.3;
    private const double SlideSeconds = 0.2;
    private const float SlideDistance = 24f;
    private const float IconSize = 28f;
    private const float StripWidth = 3f;
    private const float BarHeight = 3f;
    private const float CloseSize = 18f;

    private readonly List<Toast> _toasts = new();
    private DialogHost _host = null!;
    private Vector2 _uiSize;

    /// <summary>The toasts on screen, newest first (closing ones and the step card excluded).</summary>
    public IReadOnlyList<ToastInfo> Shown => _toasts
        .Where(t => !t.Closing && t.Style != ToastStyle.Step)
        .Select(t => new ToastInfo(t.Style, t.Title, t.Detail, System.Math.Max(0.0, t.Remaining)))
        .ToList();

    /// <summary>The trainer's step card, or null when none shows.</summary>
    public StepCardInfo? Step => StepToast is Toast step ? new StepCardInfo(step.Header!, step.Title, step.Detail) : null;

    /// <summary>Raised when the step card's X is pressed.</summary>
    public event System.Action? StepCloseRequested;

    private Toast? StepToast => _toasts.FirstOrDefault(t => t.Style == ToastStyle.Step && !t.Closing);

    public void Initialize(DialogHost host)
    {
        _host = host;
    }

    /// <summary>While the game stands still (the game menu, saving, loading), toasts wait.</summary>
    public bool Paused { get; set; }

    /// <summary>
    /// While a modal is open: the whole stack (the trainer's step card too) fades out and its toasts
    /// wait, then fades back in when it closes, so nothing shows half covered beside a modal.
    /// </summary>
    public bool Covered { get; set; }

    /// <summary>How long the stack takes to fade out under a modal, and back in.</summary>
    public const double CoverFadeSeconds = 0.15;

    /// <summary>How much of the stack shows: 1 uncovered, 0 hidden under a modal.</summary>
    private float _shown = 1f;

    /// <summary>Whether the stack shows (false once it has faded out under a modal).</summary>
    public bool StackShown => Visible;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Shows a toast on top of the stack.</summary>
    public void Show(ToastStyle style, Texture2D? icon, string title, string? detail = null)
    {
        var toast = new Toast(style, icon, title, detail);
        toast.CloseRequested += () => toast.Remaining = 0.0;
        AddChild(toast);
        _toasts.Insert(StepToast is null ? 0 : 1, toast);
        List<Toast> open = _toasts.Where(t => !t.Closing && t.Style != ToastStyle.Step).ToList();
        if (open.Count > MaxShown)
        {
            Toast oldest = open.LastOrDefault(t => t.Style == ToastStyle.Brief) ?? open[^1];
            Remove(oldest);
        }
        Layout(snap: false);
        toast.Position = toast.Position with { Y = toast.Target.Y };
    }

    /// <summary>Shows the trainer's step card on top, replacing the one before it.</summary>
    public void ShowStep(string header, string text, string? hint)
    {
        HideStep();
        var card = new Toast(ToastStyle.Step, null, text, hint, header);
        card.CloseRequested += () => StepCloseRequested?.Invoke();
        AddChild(card);
        _toasts.Insert(0, card);
        Layout(snap: false);
        card.Position = card.Position with { Y = card.Target.Y };
        RegisterControls();
    }

    /// <summary>Marks the step card done: its header reads "Done".</summary>
    public void MarkStepDone()
    {
        StepToast?.SetHeader("Done");
    }

    /// <summary>Fades the step card out.</summary>
    public void HideStep()
    {
        if (StepToast is Toast card)
        {
            card.Remaining = 0.0;
        }
    }

    public override void Refresh(Simulation sim, Vector2 uiSize)
    {
        _uiSize = uiSize;
    }

    public override void _Process(double delta)
    {
        Control? hovered = GetViewport().GuiGetHoveredControl();
        float fade = (float)(delta / CoverFadeSeconds);
        _shown = Mathf.Clamp(_shown + (Covered ? -fade : fade), 0f, 1f);
        Modulate = new Color(1f, 1f, 1f, _shown);
        Visible = _shown > 0f;
        if (Paused || Covered)
        {
            delta = 0.0;
        }
        foreach (Toast toast in _toasts.ToList())
        {
            toast.Age += delta;
            bool held = hovered is not null && (hovered == toast || toast.IsAncestorOf(hovered));
            if (!held && !toast.Closing)
            {
                toast.Remaining -= delta;
            }
            if (toast.Remaining <= 0.0)
            {
                toast.Fading += delta;
                if (toast.Fading >= FadeSeconds)
                {
                    Remove(toast);
                    continue;
                }
            }
            toast.Sync();
        }
        Layout(snap: false, Paused || Covered ? 1f : (float)delta);
        RegisterControls();
    }

    private void Remove(Toast toast)
    {
        _toasts.Remove(toast);
        toast.QueueFree();
        RegisterControls();
    }

    /// <summary>Moves each toast toward its place in the stack, sliding in from the right while new.</summary>
    private void Layout(bool snap, float delta = 0f)
    {
        float y = TopBar.Height + UiTheme.Gap;
        float right = (_uiSize.X > 0f ? _uiSize.X : GetViewportRect().Size.X) - UiTheme.Gap;
        foreach (Toast toast in _toasts)
        {
            Vector2 size = toast.Measure();
            float slide = (float)System.Math.Clamp(toast.Age / SlideSeconds, 0.0, 1.0);
            toast.Target = new Vector2(right - size.X + SlideDistance * (1f - slide), y);
            float weight = snap ? 1f : Mathf.Min(1f, delta * 14f);
            toast.Position = new Vector2(toast.Target.X, Mathf.Lerp(toast.Position.Y, toast.Target.Y, weight));
            float fadeIn = slide;
            float fadeOut = toast.Remaining <= 0.0 ? 1f - (float)(toast.Fading / FadeSeconds) : 1f;
            toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(Mathf.Min(fadeIn, fadeOut), 0f, 1f));
            y += size.Y + UiTheme.Gap;
        }
    }

    /// <summary>Registers each shown toast ("toast/0" is the newest) and its X for scenarios.</summary>
    private void RegisterControls()
    {
        for (int i = 0; i <= MaxShown; i++)
        {
            _host.Unregister($"toast/{i}");
            _host.Unregister($"toast/{i}/close");
        }
        _host.Unregister("trainer");
        _host.Unregister("trainer/close");
        if (StepToast is Toast step)
        {
            _host.Register("trainer", step);
            _host.Register("trainer/close", step.Close!);
        }
        List<Toast> open = _toasts.Where(t => !t.Closing && t.Style != ToastStyle.Step).ToList();
        for (int i = 0; i < open.Count; i++)
        {
            _host.Register($"toast/{i}", open[i]);
            if (open[i].Close is Control close)
            {
                _host.Register($"toast/{i}/close", close);
            }
        }
    }

    /// <summary>One toast: a card with an icon, a title, and a detail line; a lasting one adds an accent strip, an X, and a countdown bar.</summary>
    private sealed partial class Toast : Control
    {
        public readonly ToastStyle Style;
        public string? Header { get; private set; }
        public readonly string Title;
        public readonly string? Detail;
        public readonly double Lifetime;
        public double Remaining;
        public double Age;
        public double Fading;
        public Vector2 Target;
        public readonly Control? Close;
        public event System.Action? CloseRequested;

        private readonly PanelContainer _card;
        private readonly ColorRect? _bar;
        private readonly Label? _header;

        public bool Closing => Remaining <= 0.0;

        public Toast(ToastStyle style, Texture2D? icon, string title, string? detail, string? header = null)
        {
            Style = style;
            Header = header;
            Title = title;
            Detail = detail;
            Lifetime = style switch
            {
                ToastStyle.Lasting => LastingSeconds,
                ToastStyle.Step => double.PositiveInfinity,
                _ => BriefSeconds
            };
            Remaining = Lifetime;
            MouseFilter = MouseFilterEnum.Stop;

            _card = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            _card.AddThemeStyleboxOverride("panel", UiTheme.PanelBox(UiTheme.Gap + 2));
            AddChild(_card);

            var row = new HBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddThemeConstantOverride("separation", UiTheme.Gap);
            _card.AddChild(row);

            bool pictured = style != ToastStyle.Step;
            if (pictured)
            {
                TextureRect picture = MakeIcon(IconSize);
                picture.Texture = icon;
                picture.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                row.AddChild(picture);
            }

            bool closable = style != ToastStyle.Brief;
            float textWidth = Width - 2 * (UiTheme.Gap + 2) - (pictured ? IconSize + UiTheme.Gap : StripWidth) - (closable ? CloseSize : 0f);
            var column = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(textWidth, 0f),
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            column.AddThemeConstantOverride("separation", 2);
            row.AddChild(column);
            if (header is not null)
            {
                _header = Wrapped(UiTheme.MakeLabel(header, UiTheme.SmallSize, UiTheme.Accent), textWidth);
                column.AddChild(_header);
            }
            column.AddChild(Wrapped(UiTheme.MakeLabel(title, UiTheme.BodySize, UiTheme.Text), textWidth));
            if (detail is not null)
            {
                column.AddChild(Wrapped(UiTheme.MakeLabel(detail, UiTheme.SmallSize, UiTheme.TextDim), textWidth));
            }

            if (closable)
            {
                AddChild(new ColorRect
                {
                    Color = UiTheme.Accent,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Position = Vector2.Zero,
                    Name = "Strip"
                });
                if (style == ToastStyle.Lasting)
                {
                    _bar = new ColorRect
                    {
                        Color = UiTheme.Accent,
                        MouseFilter = MouseFilterEnum.Ignore
                    };
                    AddChild(_bar);
                }
                var close = new CloseButton
                {
                    CustomMinimumSize = new Vector2(CloseSize, CloseSize),
                    Size = new Vector2(CloseSize, CloseSize),
                    TooltipText = style == ToastStyle.Step ? "Stop the trainer" : "Close"
                };
                close.Pressed += () => CloseRequested?.Invoke();
                AddChild(close);
                Close = close;
            }
        }

        public void SetHeader(string text)
        {
            Header = text;
            if (_header is not null)
            {
                _header.Text = text;
            }
        }

        /// <summary>A label wrapping at a fixed width, sized to it at once: a wrapping label with no width yet measures a word per line.</summary>
        private static Label Wrapped(Label label, float width)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.MouseFilter = MouseFilterEnum.Ignore;
            label.CustomMinimumSize = new Vector2(width, 0f);
            label.Size = new Vector2(width, 0f);
            return label;
        }

        /// <summary>The card's size at the fixed width, laying out the strip, X, and bar to it.</summary>
        public Vector2 Measure()
        {
            _card.Size = new Vector2(Width, 0f);
            Vector2 size = new(Width, _card.GetCombinedMinimumSize().Y);
            _card.Size = size;
            Size = size;
            if (GetNodeOrNull<ColorRect>("Strip") is ColorRect strip)
            {
                strip.Size = new Vector2(StripWidth, size.Y);
            }
            if (Close is Control close)
            {
                close.Position = new Vector2(size.X - CloseSize - 4f, 4f);
            }
            return size;
        }

        public void Sync()
        {
            if (_bar is not null)
            {
                float share = (float)System.Math.Clamp(Remaining / Lifetime, 0.0, 1.0);
                _bar.Position = new Vector2(StripWidth, Size.Y - BarHeight);
                _bar.Size = new Vector2((Size.X - StripWidth) * share, BarHeight);
            }
        }
    }

    /// <summary>A flat button drawn as an X, brighter while hovered.</summary>
    private sealed partial class CloseButton : Button
    {
        public CloseButton()
        {
            Flat = true;
            FocusMode = FocusModeEnum.None;
            MouseFilter = MouseFilterEnum.Stop;
            var empty = new StyleBoxEmpty();
            foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            {
                AddThemeStyleboxOverride(state, empty);
            }
        }

        public override void _Draw()
        {
            Color color = IsHovered() ? UiTheme.Text : UiTheme.TextDim;
            float inset = 5f;
            Vector2 a = new(inset, inset);
            Vector2 b = Size - new Vector2(inset, inset);
            DrawLine(a, b, color, 2f, true);
            DrawLine(new Vector2(b.X, a.Y), new Vector2(a.X, b.Y), color, 2f, true);
        }

        public override void _Notification(int what)
        {
            if (what == NotificationMouseEnter || what == NotificationMouseExit)
            {
                QueueRedraw();
            }
        }
    }
}
