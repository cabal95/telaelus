using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The hint overlay: a small panel-style box centered just below the top bar, shown only
/// while a hint applies. Today the one hint is harvesting: "[E] Harvest" with the
/// resource's icon and name over a progress bar toward the next unit, or "Storage full" in
/// the warning color in place of the bar while there is no room for the resource. It never
/// takes the mouse. Later hints and notifications stack in the same place.
/// </summary>
public partial class HintOverlay : HudOverlay
{
    private ResourceIcons _icons = null!;
    private PanelContainer _box = null!;
    private TextureRect _icon = null!;
    private Label _name = null!;
    private ProgressBar _progress = null!;
    private Label _full = null!;

    /// <summary>The hint as shown, or null when none applies (for the snapshot).</summary>
    public string? Text => _box.Visible ? "[E] Harvest " + _name.Text : null;

    public float Progress => (float)_progress.Value;

    /// <summary>Whether the hint shows "Storage full" in place of its bar.</summary>
    public bool StorageFull => _box.Visible && _full.Visible;

    public void Initialize(ResourceIcons icons)
    {
        _icons = icons;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _box = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _box.AddThemeStyleboxOverride("panel", UiTheme.PanelBox(UiTheme.Gap));
        AddChild(_box);

        var column = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddThemeConstantOverride("separation", 6);
        _box.AddChild(column);

        var line = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        line.AddThemeConstantOverride("separation", 6);
        column.AddChild(line);
        line.AddChild(UiTheme.MakeLabel("[E] Harvest", UiTheme.BodySize, UiTheme.Accent));
        _icon = MakeIcon(20f);
        _icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        line.AddChild(_icon);
        _name = UiTheme.MakeLabel("", UiTheme.BodySize);
        line.AddChild(_name);

        _progress = StatusRows.MakeProgressBar(180f);
        column.AddChild(_progress);
        _full = UiTheme.MakeLabel("Storage full", UiTheme.SmallSize, UiTheme.Warning);
        _full.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_full);
    }

    public override void Refresh(Simulation sim, Vector2 uiSize)
    {
        ResourceField? target = sim.HarvestTarget;
        _box.Visible = target is not null;
        if (target is null)
        {
            return;
        }
        _icon.Texture = _icons.For(target.Type);
        _name.Text = ResourceDefs.For(target.Type).Name;
        _progress.Value = sim.HarvestProgress;
        _full.Visible = sim.HarvestBlocked;
        _progress.Visible = !sim.HarvestBlocked;

        _box.Size = Vector2.Zero;
        Vector2 size = _box.GetCombinedMinimumSize();
        _box.Position = new Vector2(Mathf.Floor((uiSize.X - size.X) * 0.5f), TopBar.Height + UiTheme.Gap);
    }
}
