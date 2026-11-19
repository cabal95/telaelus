using System;
using System.Collections.Generic;
using Telaelus.Presentation.Ui;
using Telaelus.Sim;
using Telaelus.Testing;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// The main scene's root (scenes/App.tscn): the app's stages. It starts on the loading screen, which
/// runs the one-time startup work (every model loaded once, then the resource icons, building and
/// field pictures, and UI icons drawn on the picture stage, all shared by every game), then shows the
/// main menu; New Game frees the menu and creates a <see cref="GameRoot"/> with a new world. With
/// <c>--render-splash</c> it renders the town scene to the splash image instead and quits; with
/// <c>--write-demo-save</c> it builds the demo town (<see cref="DemoTown"/>) once loading is done,
/// writes it as the demo save, and quits.
/// </summary>
public partial class AppRoot : Node
{
    /// <summary>The seed a scenario's game starts from when it names none, so every run begins the same.</summary>
    public const int ScenarioSeed = 1;

    /// <summary>The game's version, written into saves.</summary>
    public const string GameVersion = "0.1.0";

    /// <summary>Frames the loading screen shows (over the save's thumbnail) before a loaded game is built.</summary>
    public const int LoadFrames = 2;

    /// <summary>The demo save and its thumbnail, shipped inside the game (the main menu's Demo).</summary>
    public const string DemoSavePath = "res://assets/saves/demo.save";
    public const string DemoThumbnailPath = "res://assets/saves/demo.png";

    // Set by --write-demo-save: once loading is done, the demo is written instead of the menu shown.
    private bool _writeDemo;

    // A load waiting for its loading screen to be drawn.
    private (SaveFile Save, int Frames)? _pendingLoad;

    public enum Stage
    {
        Loading,
        Menu,
        Game
    }

    // Models loaded per frame while loading, so the bar moves.
    private const int ModelsPerFrame = 24;

    private readonly List<string> _models = new();
    private int _modelsLoaded;
    private LoadingScreen? _loading;
    private MainMenu? _menu;

    public Stage Current { get; private set; } = Stage.Loading;

    public ResourceIcons Icons { get; private set; } = null!;
    public BuildingPictures BuildingPictures { get; private set; } = null!;
    public FieldPictures FieldPictures { get; private set; } = null!;

    /// <summary>The game in play, or null on the loading screen and the menu.</summary>
    public GameRoot? Game { get; private set; }

    /// <summary>The menu while it shows, else null.</summary>
    public MainMenu? Menu => _menu;

    /// <summary>The loading screen's progress, 0 to 1 (1 once loaded).</summary>
    public float LoadingProgress { get; private set; }

    /// <summary>Raised when a game starts (New Game, or a replaced game).</summary>
    public event Action<GameRoot>? GameStarted;

    public override void _Ready()
    {
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--render-splash") >= 0)
        {
            SetProcess(false);
            AddChild(new SplashRenderer
            {
                Name = "SplashRenderer"
            });
            return;
        }

        BuildingPictures = new BuildingPictures
        {
            Name = "BuildingPictures"
        };
        AddChild(BuildingPictures);
        Icons = new ResourceIcons
        {
            Name = "ResourceIcons"
        };
        AddChild(Icons);
        FieldPictures = new FieldPictures
        {
            Name = "FieldPictures"
        };
        AddChild(FieldPictures);

        _writeDemo = Array.IndexOf(OS.GetCmdlineUserArgs(), "--write-demo-save") >= 0;
        CollectModels(ModelLibrary.Root.TrimEnd('/'));
        _loading = new LoadingScreen
        {
            Name = "LoadingScreen"
        };
        AddChild(_loading);

        ScenarioRunner.AttachIfRequested(this);
        ControlChannel.AttachIfRequested(this);
    }

    public override void _Process(double delta)
    {
        if (Current != Stage.Loading)
        {
            return;
        }
        if (_pendingLoad is (SaveFile save, int frames))
        {
            if (frames > 0)
            {
                _pendingLoad = (save, frames - 1);
                return;
            }
            _pendingLoad = null;
            StartGame(null, save);
            return;
        }
        int end = Math.Min(_models.Count, _modelsLoaded + ModelsPerFrame);
        for (; _modelsLoaded < end; _modelsLoaded++)
        {
            ModelLibrary.Preload(_models[_modelsLoaded]);
        }
        // Headless runs draw no pictures.
        bool headless = DisplayServer.GetName() == "headless";
        int pictures = headless ? 3 : (Icons.IsReady ? 1 : 0) + (BuildingPictures.IsReady ? 1 : 0) + (FieldPictures.IsReady ? 1 : 0);
        float models = _models.Count == 0 ? 1f : (float)_modelsLoaded / _models.Count;
        LoadingProgress = 0.5f * models + 0.5f * pictures / 3f;
        _loading?.SetProgress(LoadingProgress);
        if (_modelsLoaded >= _models.Count && pictures == 3)
        {
            LoadingProgress = 1f;
            if (_writeDemo)
            {
                _writeDemo = false;
                AddChild(new DemoSaveWriter
                {
                    Name = "DemoSaveWriter",
                    Game = StartGame(null, DemoSaveWriter.DemoStart())
                });
                return;
            }
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        if (_loading is not null)
        {
            RemoveChild(_loading);
            _loading.QueueFree();
            _loading = null;
        }
        _menu = new MainMenu
        {
            Name = "MainMenu"
        };
        _menu.NewGameRequested += () => NewGame(null);
        _menu.LoadRequested += name =>
        {
            if (LoadGame(name) is string problem)
            {
                _menu?.ShowProblem(problem);
            }
        };
        _menu.DemoRequested += () =>
        {
            if (LoadDemo() is string problem)
            {
                _menu?.ShowProblem(problem);
            }
        };
        _menu.QuitRequested += () => GetTree().Quit();
        AddChild(_menu);
        Current = Stage.Menu;
    }

    /// <summary>Goes into a new game, from the menu or replacing the game in play: a new world from the seed (or <see cref="ReadSeed"/>).</summary>
    public GameRoot NewGame(int? seed)
    {
        return StartGame(seed ?? ReadSeed(), null);
    }

    private GameRoot StartGame(int? seed, SaveFile? save)
    {
        ClearStage();
        var game = new GameRoot
        {
            Name = "GameRoot"
        };
        game.Initialize(this, seed ?? 0, save, Icons, BuildingPictures, FieldPictures);
        game.AskTrainer = save is null && !UserSettings.TestRun && !UserSettings.TrainerAsked;
        AddChild(game);
        Game = game;
        Current = Stage.Game;
        GameStarted?.Invoke(game);
        return game;
    }

    /// <summary>Frees whatever is showing: the menu, the game, or a loading screen.</summary>
    private void ClearStage()
    {
        if (_menu is not null)
        {
            RemoveChild(_menu);
            _menu.QueueFree();
            _menu = null;
        }
        if (_loading is not null)
        {
            RemoveChild(_loading);
            _loading.QueueFree();
            _loading = null;
        }
        if (Game is not null)
        {
            RemoveChild(Game);
            Game.QueueFree();
            Game = null;
        }
    }

    /// <summary>
    /// Loads a save: the loading screen over its thumbnail for a few frames, then a game built from it.
    /// Returns why it cannot be loaded, or null when the load has begun.
    /// </summary>
    public string? LoadGame(string name)
    {
        SaveFile save;
        try
        {
            save = SaveStore.Read(name);
        }
        catch (SaveFormatException ex)
        {
            return ex.Message;
        }
        BeginLoad(save, SaveStore.Thumbnail(name));
        return null;
    }

    /// <summary>
    /// Loads the demo save shipped inside the game, as <see cref="LoadGame"/> loads a save. Saving it
    /// later makes a new save; the demo itself never changes.
    /// </summary>
    public string? LoadDemo()
    {
        if (!Godot.FileAccess.FileExists(DemoSavePath))
        {
            return "No demo save";
        }
        SaveFile save;
        try
        {
            save = SaveFormat.Read(Godot.FileAccess.GetFileAsBytes(DemoSavePath));
        }
        catch (SaveFormatException ex)
        {
            return ex.Message;
        }
        BeginLoad(save, ResourceLoader.Exists(DemoThumbnailPath) ? GD.Load<Texture2D>(DemoThumbnailPath) : null);
        return null;
    }

    private void BeginLoad(SaveFile save, Texture2D? thumbnail)
    {
        ClearStage();
        _loading = new LoadingScreen
        {
            Name = "LoadingScreen",
            Image = thumbnail
        };
        AddChild(_loading);
        _loading.SetProgress(1f);
        Current = Stage.Loading;
        _pendingLoad = (save, LoadFrames);
    }

    /// <summary>Saves the game in play under a name (its thumbnail from when the game menu opened).</summary>
    public void SaveGame(string name)
    {
        if (Game is not GameRoot game)
        {
            throw new InvalidOperationException("no game to save");
        }
        SaveStore.Write(name, game.MakeSave(), game.SaveThumbnail);
        game.MarkSaved();
    }

    /// <summary>Saves the game in play into the older of the two autosaves.</summary>
    public void Autosave()
    {
        if (Game is not GameRoot game)
        {
            return;
        }
        SaveStore.Write(SaveStore.NextAutosave(), game.MakeSave(), game.CaptureThumbnail());
        game.MarkSaved();
    }

    /// <summary>Leaves the game for the main menu.</summary>
    public void ReturnToMenu()
    {
        ClearStage();
        ShowMenu();
    }

    /// <summary>
    /// The world seed from <c>--seed=&lt;n&gt;</c> after the <c>--</c> separator, else
    /// <see cref="ScenarioSeed"/> for a scenario run, else a random one.
    /// </summary>
    public static int ReadSeed()
    {
        bool scenario = false;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--seed=") && int.TryParse(arg["--seed=".Length..], out int seed))
            {
                return seed;
            }
            scenario |= arg.StartsWith("--scenario=");
        }
        return scenario ? ScenarioSeed : (int)(GD.Randi() & 0x7FFFFFFF);
    }

    /// <summary>Every model under a folder (recursively), as ModelLibrary paths ("buildings/hq").</summary>
    private void CollectModels(string folder)
    {
        using DirAccess? dir = DirAccess.Open(folder);
        if (dir is null)
        {
            return;
        }
        foreach (string sub in dir.GetDirectories())
        {
            CollectModels(folder + "/" + sub);
        }
        foreach (string file in dir.GetFiles())
        {
            if (file.EndsWith(".glb"))
            {
                string full = folder + "/" + file;
                _models.Add(full[ModelLibrary.Root.Length..^".glb".Length]);
            }
        }
    }
}
