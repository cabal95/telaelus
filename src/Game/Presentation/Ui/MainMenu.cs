using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Presentation.Ui;

/// <summary>
/// The main menu: the town scene, gently alive, seen through its own camera, blurred and darkened a
/// little over the whole window, with the menu at the left. The panel holds the title and the buttons: New Game and
/// Quit (Load Game and Options are planned between them). Enter starts a new game.
/// </summary>
public partial class MainMenu : Node
{
    public const string TownPath = "res://scenes/Town.tscn";
    public const string Title = "Game";

    private readonly List<Button> _buttons = new();

    public event Action? NewGameRequested;
    public event Action? QuitRequested;

    /// <summary>The buttons as shown, top to bottom, for the snapshot.</summary>
    public IReadOnlyList<string> ButtonTexts => _buttons.Select(b => b.Text).ToList();

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

        Label title = UiTheme.MakeLabel(Title, UiTheme.TitleSize * 2);
        column.AddChild(title);
        column.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, UiTheme.Padding),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        // Load Game and Options will sit between these two.
        AddButton(column, "New Game", () => NewGameRequested?.Invoke());
        AddButton(column, "Quit", () => QuitRequested?.Invoke());
    }

    private void AddButton(VBoxContainer column, string text, Action pressed)
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
