using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// <c>--write-demo-save</c>: the demo town (<see cref="DemoTown"/>) is opened as a loaded game with its
/// pins and the camera zoomed out; once its views have drawn for a moment, the game is saved with its
/// thumbnail as <see cref="AppRoot.DemoSavePath"/> in the project, and the app quits. It needs a
/// window for the thumbnail.
/// </summary>
public partial class DemoSaveWriter : Node
{
    // Frames the game runs and draws before the save is taken, so every view has settled.
    private const int SettleFrames = 30;

    // The camera zoom the demo opens at: its widest, so the town fits.
    private const float Zoom = 40f;

    // The resources pinned to the top bar: what the town mines and makes.
    private static readonly ResourceType[] Pins =
    {
        ResourceType.Wood,
        ResourceType.Stone,
        ResourceType.IronOre,
        ResourceType.CopperOre,
        ResourceType.Coal,
        ResourceType.Charcoal,
        ResourceType.Iron,
        ResourceType.Copper
    };

    private int _frames;

    /// <summary>The game showing the demo town.</summary>
    public GameRoot Game { get; init; } = null!;

    /// <summary>The demo town as a save to open: built and run, with the demo's pins and zoom.</summary>
    public static SaveFile DemoStart()
    {
        Simulation sim = DemoTown.Create();
        return new SaveFile
        {
            Header = new SaveHeader
            {
                SaveVersion = SaveFormat.Version,
                GameVersion = AppRoot.GameVersion,
                PlayTimeSeconds = DemoTown.RunSeconds,
                Seed = sim.World.Seed,
                Buildings = sim.Buildings.Count
            },
            Sim = sim.Save(),
            Presentation = new JsonObject
            {
                ["pins"] = new JsonArray(Pins.Select(t => (JsonNode)t.ToString()).ToArray()),
                ["zoom"] = Zoom
            }
        };
    }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.PrintErr("--write-demo-save needs a window for the thumbnail; run without --headless.");
            GetTree().Quit(1);
        }
    }

    public override void _Process(double delta)
    {
        if (++_frames < SettleFrames)
        {
            return;
        }
        SetProcess(false);

        Image? thumbnail = Game.CaptureThumbnail();
        string save = ProjectSettings.GlobalizePath(AppRoot.DemoSavePath);
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        File.WriteAllBytes(save, SaveFormat.Write(Game.MakeSave()));
        thumbnail?.SavePng(ProjectSettings.GlobalizePath(AppRoot.DemoThumbnailPath));
        GD.Print($"Wrote the demo save to {save}");
        GetTree().Quit();
    }
}
