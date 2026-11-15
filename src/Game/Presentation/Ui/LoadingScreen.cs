using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The loading screen: the town's splash image covering the window (cropped, never stretched),
/// "Loading" in the bottom center over a soft dark band, and a thin progress bar under it.
/// </summary>
public partial class LoadingScreen : UiLayer
{
    public const string SplashPath = "res://assets/ui/splash.png";

    private ProgressBar _bar = null!;

    public override void _Ready()
    {
        base._Ready();
        Layer = 10;
        var background = new ColorRect
        {
            Color = new Color(0.1f, 0.12f, 0.11f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(background);
        if (ResourceLoader.Exists(SplashPath))
        {
            var image = new TextureRect
            {
                Texture = GD.Load<Texture2D>(SplashPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            Root.AddChild(image);
        }

        var band = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        band.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        band.OffsetTop = -84f;
        Root.AddChild(band);

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        column.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        column.OffsetLeft = -160f;
        column.OffsetRight = 160f;
        column.OffsetTop = -70f;
        column.OffsetBottom = -24f;
        column.AddThemeConstantOverride("separation", UiTheme.Gap);
        Root.AddChild(column);
        Label label = UiTheme.MakeLabel("Loading", UiTheme.TitleSize);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(label);
        _bar = StatusRows.MakeProgressBar();
        column.AddChild(_bar);
    }

    public void SetProgress(float progress)
    {
        _bar.Value = progress;
    }
}
