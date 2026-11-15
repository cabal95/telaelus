using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The hint overlay: a small panel-style box centered just below the top bar, shown only
/// while a hint applies. Today the hint is the E key's action: "[E] Harvest" a field,
/// "[E] Collect" from a building's output buffer ("Wood (12)": what is left), or "[E] Load" an
/// offline processor's input ("Iron Ore (3 / 5)"), with the resource's icon and a progress bar
/// toward the next unit, or "Storage full" in the warning color in place of the bar while there
/// is no room for it. It never takes the mouse. Later hints and notifications stack in the same
/// place.
/// </summary>
public partial class HintOverlay : HudOverlay
{
    private ResourceIcons _icons = null!;
    private PanelContainer _box = null!;
    private TextureRect _icon = null!;
    private Label _name = null!;
    private Label _verb = null!;
    private ProgressBar _progress = null!;
    private Label _full = null!;

    /// <summary>The hint as shown, or null when none applies (for the snapshot).</summary>
    public string? Text => _box.Visible ? _verb.Text + " " + _name.Text : null;

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
        _verb = UiTheme.MakeLabel("[E] Harvest", UiTheme.BodySize, UiTheme.Accent);
        line.AddChild(_verb);
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
        ResourceType? type;
        bool blocked;
        if (sim.InteractTarget is not null && sim.InteractResource is ResourceType moving)
        {
            type = moving;
            blocked = sim.InteractBlocked;
            (float held, float? capacity) = sim.InteractAmounts();
            string amount = capacity is float most ? $"{Mathf.FloorToInt(held)} / {Mathf.FloorToInt(most)}" : Mathf.CeilToInt(held).ToString();
            _verb.Text = sim.InteractAction == InteractAction.Collect ? "[E] Collect" : "[E] Load";
            _name.Text = $"{ResourceDefs.For(moving).Name} ({amount})";
            _progress.Value = sim.InteractProgress;
        }
        else if (sim.HarvestTarget is ResourceField field)
        {
            type = field.Type;
            blocked = sim.HarvestBlocked;
            _verb.Text = "[E] Harvest";
            _name.Text = ResourceDefs.For(field.Type).Name;
            _progress.Value = sim.HarvestProgress;
        }
        else
        {
            _box.Visible = false;
            return;
        }
        _box.Visible = true;
        _icon.Texture = _icons.For(type.Value);
        _full.Visible = blocked;
        _progress.Visible = !blocked;

        _box.Size = Vector2.Zero;
        Vector2 size = _box.GetCombinedMinimumSize();
        _box.Position = new Vector2(Mathf.Floor((uiSize.X - size.X) * 0.5f), TopBar.Height + UiTheme.Gap);
    }
}
