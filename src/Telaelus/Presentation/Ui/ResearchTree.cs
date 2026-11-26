using System;
using System.Collections.Generic;
using System.Linq;
using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation.Ui;

/// <summary>
/// The research tree (T): a modal covering the screen below the top bar, a margin of world left round
/// it. The research speed heads it. On the left the tree, one section per tier with projects, side by
/// side: a faint panel headed "TIER 2" ("TIER 3: UNLOCKED BY ELECTRONICS RESEARCH" while locked, a
/// green check once every project is complete), holding each project as a small card (its icon, its
/// name, and while it runs an indicator and a thin progress bar) on a grid by its column and row
/// within the tier, with a line from each prerequisite to the project it opens, across sections. On the right a drawer shows the
/// selected project: its state, what it does and transfers, its fee, its time, its progress, and the
/// button that starts, pauses, or resumes it. Clicking a card selects it.
/// </summary>
public partial class ResearchTree : Dialog
{
    public const float CardWidth = 240f;
    public const float CardHeight = 64f;
    private const float ColumnGap = 80f;
    private const float RowGap = 24f;
    private const float DrawerWidth = 340f;
    private const float Margin = 24f;
    private const float CardIcon = 40f;
    private const float DrawerIcon = 56f;
    private const float SmallIcon = 18f;
    private const float SectionGap = 48f;
    private const float SectionPad = 12f;
    private const float HeadingHeight = 44f;
    private const float CheckSize = 14f;

    // Each tier with projects: its section's left edge and width, left to right.
    private static readonly IReadOnlyList<(int Tier, float X, float Width)> SectionLayout = LayOutSections();

    private Simulation _sim = null!;
    private ResourceIcons _icons = null!;
    private Label _header = null!;
    private HBoxContainer _split = null!;
    private TreeCanvas _canvas = null!;
    private ScrollContainer _treeScroll = null!;
    private readonly Dictionary<int, (Label Heading, TextureRect Check)> _sections = new();
    private readonly Dictionary<ResearchProject, Card> _cards = new();
    private Drawer _drawer = null!;

    public override DialogKind Kind => DialogKind.Modal;

    public override string Id => "researchTree";

    /// <summary>The project the drawer shows.</summary>
    public ResearchProject Selected { get; private set; }

    /// <summary>Each tier's section as shown (tier, heading, locked, checked: every project complete), for the snapshot.</summary>
    public IReadOnlyList<(int Tier, string Heading, bool Locked, bool Checked)> Sections => SectionLayout
        .Select(s => (s.Tier, _sections[s.Tier].Heading.Text, s.Tier > _sim.UnlockedTier, TierComplete(_sim, s.Tier)))
        .ToList();

    /// <summary>"TIER 2", or "TIER 3: UNLOCKED BY ELECTRONICS RESEARCH" while the tier is locked.</summary>
    public static string SectionHeading(Simulation sim, int tier)
    {
        string heading = BuildMenu.TierLabel(tier);
        if (tier > sim.UnlockedTier)
        {
            heading += ": " + char.ToLowerInvariant(BuildMenu.UnlockedBy(tier)[0]) + BuildMenu.UnlockedBy(tier)[1..];
        }
        return heading.ToUpperInvariant();
    }

    /// <summary>Whether every project of a tier is complete (false for a tier with none).</summary>
    public static bool TierComplete(Simulation sim, int tier)
    {
        var projects = ResearchDefs.All.Where(p => ResearchDefs.For(p).Tier == tier).ToList();
        return projects.Count > 0 && projects.All(sim.IsResearched);
    }

    /// <summary>The header as shown, for the snapshot.</summary>
    public string HeaderText => _header.Text;

    /// <summary>Each card as shown (project, whether it shows running, its progress 0 to 1, selected, locked), for the snapshot.</summary>
    public IEnumerable<(ResearchProject Project, bool Running, float Progress, bool Selected, bool Locked)> Cards =>
        ResearchDefs.All.Select(p => (p, _cards[p].Indicator.Visible, (float)_cards[p].Bar.Value, p == Selected,
            _sim.StateOf(p) == ResearchState.Locked));

    /// <summary>The drawer as shown (project, state, button text and whether enabled, fee, time, progress 0 to 1), for the snapshot.</summary>
    public (ResearchProject Project, string State, string Button, bool Enabled, string Fee, string Time, float Progress) DrawerShown =>
        (Selected, _drawer.State.Text, _drawer.Button.Text, _drawer.Button.Visible && !_drawer.Button.Disabled,
            _drawer.FeeText, _drawer.Time.Text, (float)_drawer.Bar.Value);

    public void Initialize(Simulation sim, ResourceIcons icons)
    {
        _sim = sim;
        _icons = icons;
    }

    protected override void Build()
    {
        Scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _header = UiTheme.MakeLabel("", UiTheme.BodySize);
        _header.HorizontalAlignment = HorizontalAlignment.Center;
        Body.AddChild(_header);

        _split = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _split.AddThemeConstantOverride("separation", UiTheme.Gap);
        Body.AddChild(_split);

        _treeScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _split.AddChild(_treeScroll);
        (int lastTier, float lastX, float lastWidth) = SectionLayout[^1];
        _canvas = new TreeCanvas(this)
        {
            CustomMinimumSize = new Vector2(lastX + lastWidth + UiTheme.Padding, 2 * UiTheme.Padding + SectionHeight),
            MouseFilter = MouseFilterEnum.Pass
        };
        _treeScroll.AddChild(_canvas);

        foreach ((int tier, float x, float width) in SectionLayout)
        {
            var heading = new HBoxContainer
            {
                Name = "Tier" + tier,
                Position = new Vector2(x + SectionPad, UiTheme.Padding + 6f),
                Size = new Vector2(width - 2 * SectionPad, HeadingHeight - 8f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            heading.AddThemeConstantOverride("separation", 6);
            Label label = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
            label.AddThemeFontOverride("font", new FontVariation
            {
                BaseFont = ThemeDB.FallbackFont,
                SpacingGlyph = 1
            });
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.MouseFilter = MouseFilterEnum.Ignore;
            heading.AddChild(label);
            TextureRect check = HudOverlay.MakeIcon(CheckSize);
            check.Modulate = UiTheme.Good;
            check.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            heading.AddChild(check);
            _canvas.AddChild(heading);
            _sections[tier] = (label, check);
        }

        foreach (ResearchProject project in ResearchDefs.All)
        {
            var card = new Card(this, project)
            {
                Position = CardPosition(ResearchDefs.For(project))
            };
            _canvas.AddChild(card);
            _cards[project] = card;
            Host.Register(Id + "/card/" + project, card);
        }

        _drawer = new Drawer(this);
        _split.AddChild(_drawer);
        Host.Register(Id + "/action", _drawer.Button);
    }

    private static List<(int Tier, float X, float Width)> LayOutSections()
    {
        var sections = new List<(int Tier, float X, float Width)>();
        float x = UiTheme.Padding;
        foreach (int tier in ResearchDefs.All.Select(p => ResearchDefs.For(p).Tier).Distinct().OrderBy(t => t))
        {
            int columns = ResearchDefs.All.Where(p => ResearchDefs.For(p).Tier == tier).Max(p => ResearchDefs.For(p).Column) + 1;
            float width = 2 * SectionPad + columns * CardWidth + (columns - 1) * ColumnGap;
            sections.Add((tier, x, width));
            x += width + SectionGap;
        }
        return sections;
    }

    // Every section is as tall as the most rows any tier has, under its heading.
    private static float SectionHeight
    {
        get
        {
            int rows = ResearchDefs.All.Max(p => ResearchDefs.For(p).Row) + 1;
            return HeadingHeight + 2 * SectionPad + rows * CardHeight + (rows - 1) * RowGap;
        }
    }

    private static Rect2 SectionRect((int Tier, float X, float Width) section)
    {
        return new Rect2(section.X, UiTheme.Padding, section.Width, SectionHeight);
    }

    private static Vector2 CardPosition(ResearchDef def)
    {
        (int _, float x, float _) = SectionLayout.First(s => s.Tier == def.Tier);
        return new Vector2(x + SectionPad + def.Column * (CardWidth + ColumnGap),
            UiTheme.Padding + HeadingHeight + SectionPad + def.Row * (CardHeight + RowGap));
    }

    public override void OnOpened()
    {
        base.OnOpened();
        // Open on the running project, else the first that can be worked on, else the first.
        Selected = _sim.ActiveResearch
            ?? ResearchDefs.All.FirstOrDefault(p => _sim.StateOf(p) is ResearchState.Available or ResearchState.Paused, ResearchDefs.All[0]);
        // Scrolled so its card shows, once the tree has laid out.
        Card selected = _cards[Selected];
        Callable.From(() => _treeScroll.EnsureControlVisible(selected)).CallDeferred();
    }

    public override void OnKey(Key key)
    {
        if (key == Key.T)
        {
            Host.Close(this);
        }
    }

    /// <summary>Fills the window below the top bar, a margin of world showing round it.</summary>
    public override void Place(Vector2 uiSize, float panelTop)
    {
        float top = panelTop + Margin;
        var size = new Vector2(Mathf.Max(480f, uiSize.X - 2 * Margin), Mathf.Max(320f, uiSize.Y - top - Margin));
        // The body takes what the title, rule, and Close button leave.
        float chrome = Size.Y - Scroll.Size.Y;
        float body = Mathf.Max(160f, size.Y - chrome);
        Scroll.CustomMinimumSize = new Vector2(0f, body);
        Body.CustomMinimumSize = new Vector2(0f, body);
        Size = size;
        Position = new Vector2(Margin, top).Floor();
    }

    public override void Refresh()
    {
        foreach ((int tier, (Label heading, TextureRect check)) in _sections)
        {
            heading.Text = SectionHeading(_sim, tier);
            // As wide as its text (so the check mark follows it), wrapping at the section's width.
            float room = SectionLayout.First(s => s.Tier == tier).Width - 2 * SectionPad - CheckSize - 6f;
            Font font = heading.GetThemeFont("font");
            float text = font.GetStringSize(heading.Text, HorizontalAlignment.Left, -1, UiTheme.SmallSize).X + heading.Text.Length + 1f;
            heading.CustomMinimumSize = new Vector2(Mathf.Min(text, room), 0f);
            check.Texture ??= _icons.Icon("check");
            check.Visible = TierComplete(_sim, tier);
        }

        int labs = _sim.WorkingLabs.Count;
        if (labs == 0)
        {
            _header.Text = "No labs working: research waits";
            _header.AddThemeColorOverride("font_color", UiTheme.Warning);
        }
        else
        {
            _header.Text = $"Speed {_sim.ResearchSpeed:0.##}x: {labs} lab{(labs == 1 ? "" : "s")} working";
            _header.AddThemeColorOverride("font_color", UiTheme.Text);
        }
        foreach (Card card in _cards.Values)
        {
            card.Refresh();
        }
        _drawer.Refresh();
        _canvas.QueueRedraw();
    }

    /// <summary>A span of game time as days and hours of the 24-hour clock ("1 day 6 h", "4 h", "under 1 h").</summary>
    public static string GameTime(float seconds)
    {
        float hours = seconds / Simulation.DayLength * 24f;
        int whole = (int)MathF.Ceiling(hours - 0.001f);
        if (whole < 1)
        {
            return "under 1 h";
        }
        int days = whole / 24;
        int rest = whole % 24;
        string dayText = days == 1 ? "1 day" : $"{days} days";
        return days == 0 ? $"{rest} h" : rest == 0 ? dayText : $"{dayText} {rest} h";
    }

    /// <summary>A project's icon model name (in <c>icons/</c>), as "material_transfer".</summary>
    public static string ResearchIconName(ResearchProject project)
    {
        var text = new System.Text.StringBuilder();
        foreach (char c in project.ToString())
        {
            if (char.IsUpper(c) && text.Length > 0)
            {
                text.Append('_');
            }
            text.Append(char.ToLowerInvariant(c));
        }
        return text.ToString();
    }

    private void Select(ResearchProject project)
    {
        Selected = project;
        Refresh();
    }

    private void Press()
    {
        if (_sim.ActiveResearch == Selected)
        {
            _sim.PauseResearch();
        }
        else
        {
            _sim.StartResearch(Selected);
        }
    }

    private static StyleBoxFlat? _sectionPanel;

    // A section's faint panel: the cell color at low opacity.
    private static StyleBoxFlat SectionPanel => _sectionPanel ??= UiTheme.Box(new Color(UiTheme.Cell, 0.35f), UiTheme.CellRadius);

    /// <summary>The cards' parent: draws the section panels and the prerequisite lines under them.</summary>
    private sealed partial class TreeCanvas : Control
    {
        private readonly ResearchTree _tree;

        public TreeCanvas(ResearchTree tree)
        {
            _tree = tree;
        }

        public override void _Draw()
        {
            // The section panels first, so the lines run over them and the cards over both.
            foreach ((int Tier, float X, float Width) section in SectionLayout)
            {
                DrawStyleBox(SectionPanel, SectionRect(section));
            }
            foreach (ResearchProject project in ResearchDefs.All)
            {
                ResearchDef def = ResearchDefs.For(project);
                Vector2 to = CardPosition(def) + new Vector2(0f, CardHeight / 2f);
                foreach (ResearchProject before in def.Prerequisites)
                {
                    Vector2 from = CardPosition(ResearchDefs.For(before)) + new Vector2(CardWidth, CardHeight / 2f);
                    Color color = _tree._sim.IsResearched(before) ? UiTheme.Accent : UiTheme.PanelEdge;
                    DrawLine(from, to, color, 3f, true);
                    DrawColoredPolygon(new[] { to, to + new Vector2(-10f, -6f), to + new Vector2(-10f, 6f) }, color);
                }
            }
        }
    }

    /// <summary>A project's card: its icon and name, and while it runs a dot and a thin progress bar.</summary>
    private sealed partial class Card : PanelContainer
    {
        private readonly ResearchTree _tree;
        private readonly ResearchProject _project;
        private readonly TextureRect _icon;
        private readonly Label _name;
        private readonly StyleBoxFlat _style = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: UiTheme.Gap);

        public Label Indicator { get; }
        public ProgressBar Bar { get; }

        public Card(ResearchTree tree, ResearchProject project)
        {
            _tree = tree;
            _project = project;
            Name = project.ToString();
            CustomMinimumSize = new Vector2(CardWidth, CardHeight);
            Size = CustomMinimumSize;
            MouseFilter = MouseFilterEnum.Stop;
            AddThemeStyleboxOverride("panel", _style);

            var column = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            column.AddThemeConstantOverride("separation", 2);
            AddChild(column);
            var row = new HBoxContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", UiTheme.Gap);
            column.AddChild(row);
            _icon = HudOverlay.MakeIcon(CardIcon);
            _icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(_icon);
            _name = UiTheme.MakeLabel(ResearchDefs.For(project).Name, UiTheme.BodySize);
            _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _name.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(_name);
            Indicator = UiTheme.MakeLabel("Running", UiTheme.SmallSize, UiTheme.Accent);
            Indicator.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            Indicator.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(Indicator);
            Bar = StatusRows.MakeProgressBar();
            Bar.CustomMinimumSize = new Vector2(0f, 3f);
            Bar.MouseFilter = MouseFilterEnum.Ignore;
            column.AddChild(Bar);

            GuiInput += OnInput;
        }

        public void Refresh()
        {
            Simulation sim = _tree._sim;
            ResearchState state = sim.StateOf(_project);
            _icon.Texture ??= _tree._icons.Icon(ResearchIconName(_project));
            bool running = state == ResearchState.Running;
            Indicator.Visible = running;
            Bar.Visible = running;
            Bar.Value = sim.ResearchProgress(_project) / ResearchDefs.For(_project).Work;
            Modulate = state == ResearchState.Locked ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
            bool selected = _tree.Selected == _project;
            _style.BgColor = selected ? UiTheme.CellHover : UiTheme.Cell;
            _style.BorderColor = running ? UiTheme.Accent : UiTheme.Text;
            _style.SetBorderWidthAll(selected || running ? UiTheme.OutlineWidth : 0);
        }

        private void OnInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                AcceptEvent();
                _tree.Select(_project);
            }
        }
    }

    /// <summary>The drawer at the right edge: everything about the selected project and its button.</summary>
    private sealed partial class Drawer : PanelContainer
    {
        private readonly ResearchTree _tree;
        private readonly TextureRect _icon;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Label _transfersLabel;
        private readonly HBoxContainer _transfers;
        private readonly Label _needs;
        private readonly HBoxContainer _fee;
        private string _feeSignature = "";
        private ResearchProject? _shown;

        public Label State { get; }
        public Label Time { get; }
        public ProgressBar Bar { get; }
        public Button Button { get; }
        public string FeeText { get; private set; } = "";

        public Drawer(ResearchTree tree)
        {
            _tree = tree;
            Name = "Drawer";
            CustomMinimumSize = new Vector2(DrawerWidth, 0f);
            SizeFlagsVertical = SizeFlags.ExpandFill;
            StyleBoxFlat style = UiTheme.Box(UiTheme.Cell, UiTheme.CellRadius, padding: UiTheme.Padding);
            style.BorderColor = UiTheme.PanelEdge;
            style.BorderWidthLeft = 1;
            AddThemeStyleboxOverride("panel", style);

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", UiTheme.Gap);
            AddChild(column);

            var top = new HBoxContainer();
            top.AddThemeConstantOverride("separation", UiTheme.Gap);
            column.AddChild(top);
            _icon = HudOverlay.MakeIcon(DrawerIcon);
            top.AddChild(_icon);
            var names = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            names.AddThemeConstantOverride("separation", 0);
            top.AddChild(names);
            _name = UiTheme.MakeLabel("", UiTheme.TitleSize);
            _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            names.AddChild(_name);
            State = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
            names.AddChild(State);

            _description = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.Text);
            _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(_description);

            _transfersLabel = UiTheme.MakeLabel("Transfers", UiTheme.SmallSize, UiTheme.TextDim);
            column.AddChild(_transfersLabel);
            _transfers = new HBoxContainer();
            _transfers.AddThemeConstantOverride("separation", 3);
            column.AddChild(_transfers);

            _needs = UiTheme.MakeLabel("", UiTheme.SmallSize, UiTheme.TextDim);
            _needs.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(_needs);

            column.AddChild(UiTheme.MakeLabel("Cost to begin", UiTheme.SmallSize, UiTheme.TextDim));
            _fee = new HBoxContainer();
            _fee.AddThemeConstantOverride("separation", 4);
            column.AddChild(_fee);

            column.AddChild(UiTheme.MakeLabel("Time", UiTheme.SmallSize, UiTheme.TextDim));
            Time = UiTheme.MakeLabel("", UiTheme.BodySize);
            column.AddChild(Time);
            Bar = StatusRows.MakeProgressBar();
            column.AddChild(Bar);

            column.AddChild(new Control
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            });
            Button = new Button
            {
                Name = "Action",
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 40f)
            };
            Button.Pressed += () =>
            {
                // The button's text and tip change with the press, so the old tip goes.
                _tree.Host.HideHover(Button);
                _tree.Press();
            };
            Button.MouseEntered += ShowButtonTip;
            Button.MouseExited += () => _tree.Host.HideHover(Button);
            column.AddChild(Button);
        }

        public void Refresh()
        {
            Simulation sim = _tree._sim;
            ResearchProject project = _tree.Selected;
            ResearchDef def = ResearchDefs.For(project);
            ResearchState state = sim.StateOf(project);

            if (_shown != project)
            {
                _shown = project;
                _icon.Texture = null;
                _name.Text = def.Name;
                _description.Text = def.Description;
                foreach (Node child in _transfers.GetChildren())
                {
                    _transfers.RemoveChild(child);
                    child.QueueFree();
                }
                foreach (ResourceType type in def.Transfers)
                {
                    TextureRect icon = HudOverlay.MakeIcon(SmallIcon + 4f);
                    icon.TooltipText = ResourceDefs.For(type).Name;
                    icon.MouseFilter = MouseFilterEnum.Pass;
                    _transfers.AddChild(icon);
                }
                _feeSignature = "";
            }
            _icon.Texture ??= _tree._icons.Icon(ResearchIconName(project));
            for (int i = 0; i < def.Transfers.Count; i++)
            {
                ((TextureRect)_transfers.GetChild(i)).Texture ??= _tree._icons.For(def.Transfers[i]);
            }
            _transfersLabel.Visible = def.Transfers.Count > 0;
            _transfers.Visible = def.Transfers.Count > 0;

            State.Text = state switch
            {
                ResearchState.Locked => "Locked",
                ResearchState.Available => "Available",
                ResearchState.Running => "Running",
                ResearchState.Paused => "Paused",
                _ => "Complete"
            };
            State.AddThemeColorOverride("font_color", state == ResearchState.Running ? UiTheme.Accent : UiTheme.TextDim);
            var missing = def.Prerequisites.Where(p => !sim.IsResearched(p)).Select(p => ResearchDefs.For(p).Name).ToList();
            _needs.Visible = def.Prerequisites.Count > 0;
            _needs.Text = missing.Count > 0
                ? "Needs " + string.Join(", ", missing) + " first"
                : "After " + string.Join(", ", def.Prerequisites.Select(p => ResearchDefs.For(p).Name));
            _needs.AddThemeColorOverride("font_color", missing.Count > 0 ? UiTheme.Warning : UiTheme.TextDim);

            RefreshFee(sim, project, def);

            float done = sim.ResearchProgress(project);
            Bar.Value = def.Work > 0f ? done / def.Work : 0.0;
            string work = $"{GameTime(def.Work)} at 1 lab";
            float speed = sim.ResearchSpeed;
            Time.Text = state switch
            {
                ResearchState.Complete => work,
                _ when speed <= 0f => $"{work}; waits for a lab",
                _ => $"{work}; {GameTime((def.Work - done) / speed)} left"
            };

            Button.Visible = state != ResearchState.Complete;
            Button.Text = state switch
            {
                ResearchState.Running => "Pause Research",
                ResearchState.Paused => "Resume Research",
                _ => "Start Research"
            };
            Button.Disabled = state != ResearchState.Running && !sim.CanStartResearch(project);
        }

        private void RefreshFee(Simulation sim, ResearchProject project, ResearchDef def)
        {
            bool paid = sim.ResearchFeePaid(project);
            string signature = project + (paid ? ":paid" : ":" + string.Join(";", def.Fee.Select(c => $"{c.Type}:{HudOverlay.Whole(sim, c.Type) >= c.Amount}")));
            if (signature == _feeSignature)
            {
                return;
            }
            _feeSignature = signature;
            foreach (Node child in _fee.GetChildren())
            {
                _fee.RemoveChild(child);
                child.QueueFree();
            }
            if (paid || def.Fee.Count == 0)
            {
                FeeText = def.Fee.Count == 0 ? "No fee" : "Fee paid";
                _fee.AddChild(UiTheme.MakeLabel(FeeText, UiTheme.BodySize, UiTheme.TextDim));
                return;
            }
            var parts = new List<string>();
            foreach (ResourceAmount cost in def.Fee)
            {
                TextureRect icon = HudOverlay.MakeIcon(SmallIcon + 4f);
                icon.Texture = _tree._icons.For(cost.Type);
                icon.TooltipText = ResourceDefs.For(cost.Type).Name;
                icon.MouseFilter = MouseFilterEnum.Pass;
                _fee.AddChild(icon);
                bool short_ = HudOverlay.Whole(sim, cost.Type) < cost.Amount;
                _fee.AddChild(UiTheme.MakeLabel($"{cost.Amount:0}", UiTheme.BodySize, short_ ? UiTheme.Short : UiTheme.Text));
                parts.Add($"{cost.Amount:0} {ResourceDefs.For(cost.Type).Name}");
            }
            FeeText = "Fee: " + string.Join(", ", parts);
        }

        private void ShowButtonTip()
        {
            Simulation sim = _tree._sim;
            ResearchProject project = _tree.Selected;
            ResearchDef def = ResearchDefs.For(project);
            var lines = new List<TipLine>();
            if (Button.Disabled)
            {
                if (sim.StateOf(project) == ResearchState.Locked)
                {
                    lines.Add(new TipLine("Needs " + string.Join(", ", def.Prerequisites.Where(p => !sim.IsResearched(p)).Select(p => ResearchDefs.For(p).Name)) + " first", UiTheme.Short));
                }
                foreach (ResourceAmount cost in def.Fee)
                {
                    int have = HudOverlay.Whole(sim, cost.Type);
                    if (have < cost.Amount && !sim.ResearchFeePaid(project))
                    {
                        lines.Add(new TipLine($"Needs {cost.Amount:0} {ResourceDefs.For(cost.Type).Name} (have {have})", UiTheme.Short));
                    }
                }
            }
            else if (sim.ActiveResearch is ResearchProject running && running != project)
            {
                lines.Add(new TipLine("Pauses " + ResearchDefs.For(running).Name));
            }
            else if (!sim.ResearchFeePaid(project) && def.Fee.Count > 0 && sim.ActiveResearch != project)
            {
                lines.Add(new TipLine("Pays the fee once; it is not refunded"));
            }
            if (lines.Count > 0)
            {
                _tree.Host.ShowHover(Button, lines);
            }
        }
    }
}
