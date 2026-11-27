using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The main menu: the town scene, gently alive, seen through its own camera, blurred and darkened a
/// little over the whole window, with the menu at the left: the title and the buttons, Continue (when
/// there is a save; it loads the newest), New Game, Load Game (disabled with no saves; it shows the
/// saves to load in place of the buttons), Demo (the demo save shipped with the game, when it is
/// there), and Quit (Options is planned before it). Enter starts a new game.
/// </summary>
public partial class MainMenu : Node
{
    public const string TownPath = "res://scenes/Town.tscn";
    public const string Title = "Telaelus";

    private readonly List<Button> _buttons = new();
    private Label? _version;

    /// <summary>The version label's text (for the snapshot).</summary>
    public string? VersionText => _version?.Text;

    public event Action? NewGameRequested;
    public event Action? DemoRequested;
    public event Action? QuitRequested;

    /// <summary>Raised with a save's name to load (Continue loads the newest).</summary>
    public event Action<string>? LoadRequested;

    private VBoxContainer _column = null!;
    private VBoxContainer _loadPanel = null!;
    private SaveList _saves = null!;
    private Label _problem = null!;

    /// <summary>The buttons as shown, top to bottom, for the snapshot (the enabled ones, while the buttons show).</summary>
    public IReadOnlyList<string> ButtonTexts => _column.Visible
        ? _buttons.Where(b => b.Visible && !b.Disabled).Select(b => b.Text).ToList()
        : new List<string>();

    /// <summary>The saves listed while Load Game shows them, else null, for the snapshot.</summary>
    public IReadOnlyList<(string Name, bool Loadable, bool Outdated)>? SaveRows => _loadPanel.Visible ? _saves.Rows : null;

    /// <summary>The save list's question while Load Game shows it, else null, for the snapshot.</summary>
    public string? SavesPrompt => _loadPanel.Visible ? _saves.Prompt : null;

    public override void _Ready()
    {
        if (GD.Load<PackedScene>(TownPath)?.Instantiate() is Node town)
        {
            AddChild(town);
        }

        var layer = new UiLayer
        {
            Name = "MenuLayer"
        };
        AddChild(layer);

        var strip = new ColorRect
        {
            Material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://assets/ui/blur.gdshader")
            },
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        // The whole window: the town behind the menu, blurred and darkened a little.
        strip.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.Root.AddChild(strip);

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        column.AnchorLeft = 0f;
        column.AnchorRight = 1f / 3f;
        column.AnchorTop = 0.3f;
        column.AnchorBottom = 1f;
        column.OffsetLeft = 48f;
        column.OffsetRight = -48f;
        column.AddThemeConstantOverride("separation", UiTheme.Padding);
        layer.Root.AddChild(column);

        // The version in the bottom-left corner.
        _version = UiTheme.MakeLabel(AppRoot.VersionLabel, UiTheme.BodySize);
        _version.AddThemeColorOverride("font_color", UiTheme.TextDim);
        _version.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _version.GrowVertical = Control.GrowDirection.Begin;
        _version.OffsetLeft = UiTheme.Padding;
        _version.OffsetBottom = -UiTheme.Padding;
        _version.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.Root.AddChild(_version);

        Label title = UiTheme.MakeLabel(Title, UiTheme.TitleSize * 2);
        column.AddChild(title);
        column.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, UiTheme.Padding),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _column = column;
        List<SaveEntry> saves = SaveStore.List();
        SaveEntry? newest = saves.FirstOrDefault(s => s.Loadable);
        Button continueButton = AddButton(column, "Continue", () =>
        {
            if (newest is not null)
            {
                LoadRequested?.Invoke(newest.Name);
            }
        });
        continueButton.Visible = newest is not null;
        AddButton(column, "New Game", () => NewGameRequested?.Invoke());
        Button load = AddButton(column, "Load Game", ShowLoad);
        load.Disabled = saves.Count == 0;
        Button demo = AddButton(column, "Demo", () => DemoRequested?.Invoke());
        demo.Visible = Godot.FileAccess.FileExists(AppRoot.DemoSavePath);
        // Options will sit here.
        AddButton(column, "Quit", () => QuitRequested?.Invoke());

        _loadPanel = new VBoxContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _loadPanel.AnchorLeft = 0f;
        _loadPanel.AnchorRight = 0.5f;
        _loadPanel.AnchorTop = 0.12f;
        _loadPanel.AnchorBottom = 0.92f;
        _loadPanel.OffsetLeft = 48f;
        _loadPanel.AddThemeConstantOverride("separation", UiTheme.Gap);
        layer.Root.AddChild(_loadPanel);
        _loadPanel.AddChild(UiTheme.MakeLabel("Load Game", UiTheme.TitleSize));
        _saves = new SaveList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _saves.LoadRequested += name => LoadRequested?.Invoke(name);
        _loadPanel.AddChild(_saves);
        _problem = UiTheme.MakeLabel("", UiTheme.BodySize, UiTheme.Warning);
        _loadPanel.AddChild(_problem);
        var back = new Button
        {
            Name = "Back",
            Text = "Back",
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        back.Pressed += () =>
        {
            _loadPanel.Visible = false;
            _column.Visible = true;
        };
        _loadPanel.AddChild(back);
    }

    /// <summary>Shows why a save could not be loaded, under the list.</summary>
    public void ShowProblem(string problem)
    {
        _problem.Text = problem;
    }

    private void ShowLoad()
    {
        _problem.Text = "";
        _saves.Open();
        _column.Visible = false;
        _loadPanel.Visible = true;
    }

    private Button AddButton(VBoxContainer column, string text, Action pressed)
    {
        var button = new Button
        {
            Name = text.Replace(" ", ""),
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0f, 44f),
            Alignment = HorizontalAlignment.Left
        };
        button.AddThemeFontSizeOverride("font_size", UiTheme.BodySize + 4);
        button.Pressed += pressed;
        column.AddChild(button);
        _buttons.Add(button);
        return button;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.PhysicalKeycode is Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();
            NewGameRequested?.Invoke();
        }
    }
}
