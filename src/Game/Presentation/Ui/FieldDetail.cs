using System.Collections.Generic;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The detail panel for a resource field, patterned on the building detail panel: the
/// resource's name as the title, a still picture of a full field of that deposit
/// (<see cref="FieldPictures"/>), and its status rows: Remaining ("212 / 250", with a
/// bar of the fraction left) and Worked by (the switched-on extractors working it now).
/// The rows refresh every frame; the panel closes itself when its field is gone.
/// </summary>
public partial class FieldDetail : Dialog
{
    public const float PictureSize = 180f;

    private Simulation _sim = null!;
    private FieldPictures _pictures = null!;
    private TextureRect _picture = null!;
    private StatusRows _rows = null!;
    private ProgressBar _remaining = null!;

    public override DialogKind Kind => DialogKind.Panel;

    public override string Id => "fieldDetail";

    /// <summary>The field shown, or null.</summary>
    public ResourceField? Field { get; private set; }

    /// <summary>The status rows as shown (label to value), for the snapshot.</summary>
    public IReadOnlyDictionary<string, string> Rows => _rows.Shown;

    public void Initialize(Simulation sim, FieldPictures pictures)
    {
        _sim = sim;
        _pictures = pictures;
    }

    protected override void Build()
    {
        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        Body.AddChild(center);
        var frame = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius));
        center.AddChild(frame);
        _picture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(PictureSize, PictureSize),
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddChild(_picture);

        _rows = new StatusRows("Remaining", "Worked by");
        Body.AddChild(_rows);
        _remaining = StatusRows.MakeProgressBar();
        Body.AddChild(_remaining);
    }

    /// <summary>Shows a field (opening the panel if needed).</summary>
    public void ShowField(ResourceField field)
    {
        Field = field;
        TitleLabel.Text = ResourceDefs.For(field.Type).Name;
        _picture.Texture = null;
        Host.Open(this);
        Refresh();
    }

    public override void OnClosed()
    {
        Field = null;
        base.OnClosed();
    }

    public override void Refresh()
    {
        if (Field is not ResourceField field || !_sim.Fields.Contains(field))
        {
            Host.Close(this);
            return;
        }

        // The picture may arrive after the panel opens (it is drawn once at startup).
        _picture.Texture ??= _pictures.For(field.Type, _sim.MapBiomeAt(field.MapX, field.MapZ));
        _rows.Set("Remaining", $"{Mathf.CeilToInt(field.Amount)} / {Mathf.CeilToInt(field.InitialAmount)}");
        _rows.Set("Worked by", WorkedBy(_sim.ExtractorsWorking(field)));
        _remaining.Value = field.RemainingFraction;
    }

    /// <summary>The extractors working a field, counted by type, as "2 Mines", or "nothing".</summary>
    private static string WorkedBy(List<Building> extractors)
    {
        if (extractors.Count == 0)
        {
            return "nothing";
        }
        return string.Join(", ", extractors
            .GroupBy(b => b.Type)
            .OrderBy(group => group.Key)
            .Select(group => group.Count() == 1 ? "1 " + group.First().Def.Name : $"{group.Count()} {group.First().Def.Name}s"));
    }
}
