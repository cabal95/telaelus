using Godot;
using Telaelus.Presentation.Ui;

namespace Telaelus.Presentation;

/// <summary>
/// <c>--render-splash</c>: renders the town scene, held still at midday, in a 1920 x 1080 viewport and
/// saves it as the splash image (assets/ui/splash.png), then quits. Needs a window: headless runs
/// cannot render.
/// </summary>
public partial class SplashRenderer : Node
{
    public static readonly Vector2I Size = new(1920, 1080);

    // Frames to let the scene draw (and its shadows settle) before the capture.
    private const int SettleFrames = 6;

    private SubViewport _view = null!;
    private int _frames;

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.PrintErr("--render-splash needs a window; run without --headless.");
            GetTree().Quit(2);
            return;
        }
        _view = new SubViewport
        {
            Size = Size,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X,
            OwnWorld3D = true
        };
        AddChild(_view);
        if (GD.Load<PackedScene>(MainMenu.TownPath).Instantiate() is TownLife town)
        {
            town.Alive = false;
            _view.AddChild(town);
        }
    }

    public override void _Process(double delta)
    {
        if (_view is null || ++_frames < SettleFrames)
        {
            return;
        }
        SetProcess(false);
        Image image = _view.GetTexture().GetImage();
        string path = ProjectSettings.GlobalizePath(LoadingScreen.SplashPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        Error error = image.SavePng(path);
        GD.Print(error == Error.Ok ? $"SPLASH saved {path}" : $"SPLASH failed: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
