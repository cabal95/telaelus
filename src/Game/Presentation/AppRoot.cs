using System;
using System.Collections.Generic;
using Game.Presentation.Ui;
using Game.Testing;
using Godot;

namespace Game.Presentation;

/// <summary>
/// The main scene's root (scenes/App.tscn): the app's stages. It starts on the loading screen, which
/// runs the one-time startup work (every model loaded once, then the resource icons, building and
/// field pictures, and UI icons drawn on the picture stage, all shared by every game), then shows the
/// main menu; New Game frees the menu and creates a <see cref="GameRoot"/> with a new world. With
/// <c>--render-splash</c> it renders the town scene to the splash image instead and quits.
/// </summary>
public partial class AppRoot : Node
{
    /// <summary>The seed a scenario's game starts from when it names none, so every run begins the same.</summary>
    public const int ScenarioSeed = 1;

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
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        _loading?.QueueFree();
        _loading = null;
        _menu = new MainMenu
        {
            Name = "MainMenu"
        };
        _menu.NewGameRequested += () => NewGame(null);
        _menu.QuitRequested += () => GetTree().Quit();
        AddChild(_menu);
        Current = Stage.Menu;
    }

    /// <summary>Goes into a new game, from the menu or replacing the game in play: a new world from the seed (or <see cref="ReadSeed"/>).</summary>
    public GameRoot NewGame(int? seed)
    {
        if (_menu is not null)
        {
            RemoveChild(_menu);
            _menu.QueueFree();
            _menu = null;
        }
        if (Game is not null)
        {
            RemoveChild(Game);
            Game.QueueFree();
        }
        var game = new GameRoot
        {
            Name = "GameRoot"
        };
        game.Initialize(seed ?? ReadSeed(), Icons, BuildingPictures, FieldPictures);
        AddChild(game);
        Game = game;
        Current = Stage.Game;
        GameStarted?.Invoke(game);
        return game;
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
