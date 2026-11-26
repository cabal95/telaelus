# UI Style Blueprint

## Overview

How every piece of on-screen UI looks and behaves: the three kinds of UI (modals, panels, and
overlays) and how each routes input, the color and size tokens, the shared Godot theme, the UI
scale, the layers, the patterns that say which control or display does which job, and the shared
components (buttons, cells, tooltips, icons). All UI uses these rules and never sets its own colors
or sizes. The code lives in `src/Telaelus/Presentation/Ui/`.

## Current state

### Kinds of UI

- **Modal**: centered over a dimmed backdrop; takes all input until it is used or dismissed. The
  build menu.
- **Panel**: docked against the left edge below the top bar; captures only Escape, so the world
  keeps its input. The building detail, the field detail, and the inventory.
- Over the world (not over any UI), a right drag or a Ctrl + left drag pans the camera, and a
  right click or Ctrl + left click walks (see player.md); over a panel, a modal, or the top bar
  neither does anything.
- **Overlay**: part of the screen rather than something opened: always shown (the top bar),
  shown while it applies (the hint overlay, the notifications), or toggled by the player (the
  debug overlay). An overlay never takes keys and never closes anything.

Modals and panels are **dialogs** in code (`Dialog`, with a `DialogKind` of `Modal` or `Panel`,
owned by the `DialogHost`); overlays are `HudOverlay`s, except the debug overlay, which is its own
layer. ("Popup" was considered for panels and set aside: it usually means a small box by the
cursor that closes on any outside click, and Godot's `Popup` class works that way.)

### Scale

- UI is authored for a 720 px tall window. Each UI layer (`UiLayer`, a `CanvasLayer`; the dialog
  host and the debug overlay are both one) scales its contents by window height / 720, clamped to
  1.0 to 2.0 (`UiTheme.ScaleFor`). The layer's `Root` control covers the window in unscaled units,
  so layout and anchors work as if the window were 720 px tall. This is the same reference height
  the camera's zoom uses, so a larger window shows more of the world at the same scale, and the
  UI grows with it instead of shrinking.

### Layers

From back to front: the debug overlay (its own layer); then, in the dialog host's layer, the
screen overlays (the top bar and the hint overlay), the modal backdrop, the dialogs, and the
tooltips. The debug overlay never moves anything: panels and the hint overlay lay out as if it
were not there, and they may cover it.

### Tokens

`UiTheme` holds the tokens; nothing else names a UI color or size.

| Token | Value (RGB, alpha) | Use |
|---|---|---|
| `Panel` | 0.08, 0.10, 0.09 at 85% | Dialog, top bar, hint, and tooltip background |
| `PanelEdge` | 0.30, 0.36, 0.32 | Panel border (1 px), scroll grabber |
| `Cell` | 0.14, 0.17, 0.15 at 90% | Grid cell and button background |
| `CellHover` / `CellPressed` | 0.19, 0.23, 0.20 / 0.10, 0.12, 0.11 at 95% | Hovered and pressed cells and buttons |
| `Text` | 0.92, 0.94, 0.92 | Body text |
| `TextDim` | 0.60, 0.64, 0.61 | Secondary text, hints, "Undiscovered" |
| `Accent` | 0.98, 0.80, 0.35 | Hover outline, title rule, tooltip headings |
| `Short` | 1.00, 0.42, 0.38 | A cost amount the player lacks |
| `Danger` | 0.85, 0.30, 0.25 | Destructive buttons; a capacity blackout (Power, Water, Sand); a full pin on the top bar |
| `Warning` | 0.98, 0.66, 0.18 | Storage full (inventory, the harvest hint); a capacity brownout; a status that is degraded or cut off |
| `Good` | 0.30, 0.95, 0.70 | On and online (the on/off toggle, the Network entry); the hubs' glowing green |
| `Backdrop` | black at 35% | Behind a modal |
| `Silhouette` | 0.06, 0.07, 0.06 | Undiscovered building pictures |

The accent is a warm amber that reads against both the green ground and the dark panel.

- **Shape and spacing:** corner radius 6 on panels and 4 on cells and buttons; an 8 px spacing
  grid (`Gap` 8 between items, `Padding` 16 inside panels); hover outlines are 2 px.
- **Type:** Godot's default font, at 22 (dialog titles), 16 (body, buttons), and 14 (cell labels,
  tooltips). Any font shipped later must carry a license that allows bundling it in a game (the
  SIL Open Font License does), with the license file kept beside the font.
- `UiTheme.Theme` is the one Godot `Theme` built from these (labels, panels, buttons, the
  vertical scroll bar); the UI layers set it on their root.

### Dialogs

A `Dialog` is a panel-styled frame with a centered title over an `Accent` rule, a vertically
scrolling body, and a Close button at the bottom center. The `DialogHost` (a UI layer that runs
even while the game is paused) owns them:

- One dialog is open at a time; opening one closes the other.
- Escape closes the open dialog (the host takes it before the world sees it).
- The game keeps running while a dialog is open (no pause: the sim is the future co-op server),
  except the game menu and the save and load dialogs (`Dialog.PausesGame`), which stand it still.
- Clicks and the mouse wheel over a dialog never reach the world. A scroll body uses the wheel
  first, and once it is at the end of its range the dialog keeps the event (Godot otherwise
  passes unused wheel events up through a stopping control, `MouseForcePassScrollEvents`), so
  the world never zooms behind a dialog or the modal backdrop.

The two kinds place themselves:

- **Modal:** centered over the dimmed backdrop, 60% of the window wide (at least 420 and at most
  900 UI units) and as tall as its content up to 80% of the window, after which the body
  scrolls. A modal may place itself otherwise (`Dialog.Place` is virtual): the research tree
  fills the window below the top bar, 24 units in from every edge. It takes all input until it is dismissed: the host swallows every key (handing it to
  the modal first, so the build menu can close on B), and the world reads no movement or
  harvest input while one is open (`DialogHost.CapturesWorldInput`). Clicking the backdrop
  closes it.
- **Panel:** against the left edge with a 16 px margin, a third of the window wide (at least
  280 UI units), from just below the top bar (`DialogHost.PanelTop`) to the bottom margin, with
  no backdrop; when its content is taller, the body scrolls and the Close button stays pinned.
  Its content never widens it: pickers and buttons cut long text with an ellipsis, status rows
  wrap, and anything else too wide is clipped. It
  captures only Escape, so movement, harvesting, zoom over the world, and every other key still
  reach the game; its own toggle key (I for the inventory) reaches the game too, which closes it.
  It closes by its Close button, Escape, a click on the world that selects nothing, or another
  dialog opening.

### The game menu and the save and load dialogs

- **Game menu** (`GameMenu`, a modal titled "Paused", id `gameMenu`): Escape opens it when nothing
  else takes Escape (no open dialog, not in build mode), and the frame behind it is kept as the
  thumbnail for a save made from it. Buttons, 40 tall, one centered stack twice as wide as its
  widest button (the button pattern): Resume, Save Game, Load Game,
  Trainer (starts the trainer from its first step and closes the menu), Main Menu, and Quit; Resume takes the place of the Close button. Main Menu and Quit with unsaved
  progress swap the buttons for "Unsaved progress will be lost." in `Warning` with Leave without
  saving and Cancel. A status line under them in `TextDim` says what just happened ("Saved as
  Save 1", or why a load failed).
- **Save dialog** (`SaveDialog`, "Save Game", id `saveGame`): "Existing saves" (the named saves,
  newest first, as left-aligned buttons that fill in the name; "None yet" without any), then "Name"
  with a name field (suggesting the first free "Save n"; Enter saves) and Save. Saving over an
  existing name asks "Overwrite <name>?" with Overwrite and Cancel. Saving returns to the game
  menu with its status.
- **Load dialog** (`LoadDialog`, "Load Game", id `loadGame`): the save list (`SaveList`), a row per
  save, newest first, with its thumbnail (128 x 72), name, "Saved <date time>   Played <time>", and
  "<n> buildings   <n> ships launched" (singular for one); a save that cannot be loaded is dimmed
  with its reason. The selected row has an `Accent` outline; a double-click loads it. Under the
  list, Load and Delete; Delete asks first. With unsaved progress, Load asks "Unsaved progress will
  be lost." with Load anyway and Cancel.

- **Choice dialog** (`ChoiceDialog`, a modal, id `choice`): a question with two answers, 40 tall
  buttons side by side, each its label's width (at least 96), no Close button (the second answer and Escape do what Close would); it
  stands the game still. The trainer's "Learn to play?" and "Stop the trainer?" use it.

### Overlays

- **Loading screen and main menu** (presentation.md): full-window `UiLayer`s outside the dialog
  host. The loading screen is the splash image with "Loading" in `TitleSize` and a progress bar on
  a dark band at the bottom center. The menu blurs and darkens a little the whole live town behind
  it, with the menu at the left third: the title at twice `TitleSize` and the buttons in the shared
  button style, 44 units tall, left aligned. While a save loads, the loading screen shows the
  save's thumbnail in place of the splash.
- **Top bar** (`TopBar`): a full-width strip across the top of the window, 36 UI units tall, in
  the panel color with a 1 px `PanelEdge` bottom border and no rounded corners. Left: every
  pinned resource as a 22-unit icon and its amount in whole numbers (no limit), in `Danger` when
  its storage is full and in `TextDim` while it has no storage at all (a limit of 0, as Crude Oil
  before a Tank Farm), each with a tooltip naming it with its amount and limit ("Wood 45 / 100";
  opening below the entry, so it never covers its neighbors). Right: the clock (a sun or moon icon and "14:00"), then Power, Water,
  and Sand, each only once the network has supply or demand of it (its icon and demand over
  supply, in normal text, `Warning`, or `Danger` by state, with "Blackout:" in `Danger` before the
  icon in a blackout, and a name tooltip), then the Battery Banks' store (the Battery icon and
  "stored / capacity", `Warning` while discharging) once there is a bank. Every entry (each pin,
  and each section on the right) sits two `Gap`s (16 units) apart, and each icon 3 units from its
  text. The ship bill and the launch live in the Shipyard's
  panel, not on the bar. It takes the mouse (so hovering it shows no building outline and
  clicks on it never reach the world) but no keys.
- **Hint overlay** (`HintOverlay`): centered, 8 units below the top bar, a small panel-style box
  shown only while a hint applies, never taking the mouse. Today the one hint is harvesting:
  "[E] Harvest" in `Accent`, the resource's icon and name, over a progress bar toward the next
  unit, or "Storage full" (14 units, `Warning`, centered) in place of the bar; the verb reads
  "[E] Harvest" or "[E] Collect" for the target.
- **Notifications** (`Notifications`): toasts stacked against the right edge, `Gap` in from it
  and `Gap` below the top bar, newest on top, at most 4. Each is a card 280 units wide in the
  panel style (`PanelBox`, `Gap` + 2 padding): a 28-unit icon at the left, a title in `BodySize`
  `Text`, and an optional detail line in `SmallSize` `TextDim`, both wrapping. Two styles:
  - **Brief:** gone after 5 seconds; no close button.
  - **Lasting:** gone after 15 seconds; a 3-unit `Accent` strip down its left edge, an X (two
    drawn lines, 18 units, `TextDim`, `Text` while hovered) at its top right that closes it, and
    a 3-unit `Accent` countdown bar along its bottom edge, shrinking to nothing.
  A toast slides in 24 units from the right while fading in (0.2 s) and fades out (0.3 s); the
  others glide to their new places. Its time counts only while the game runs (not under the game
  menu or the save and load dialogs) and not while the pointer is over it. A new toast past the
  fourth pushes out the oldest brief one, or the oldest of all when every one is lasting. The
  cards take the mouse (so clicks on them never reach the world); the space around them does not.
  While a modal is open (`DialogHost.CapturesWorldInput`) the whole stack, the trainer's step
  card too, fades out over 0.15 s and its toasts wait (`Notifications.Covered`); it fades back in
  when the modal closes, with new toasts raised meanwhile on top. A panel docks left, so they never
  cover it.
  - **Step card** (the trainer's): the lasting card's strip and X, no icon and no countdown bar,
    and three lines: "Trainer 3 / 13" (or "Done" once the step is done) in `SmallSize` `Accent`,
    the instruction in `BodySize` `Text`, and an optional hint in `SmallSize` `TextDim`. It sits
    above every toast, never times out, never counts toward the 4, and is never pushed out; its X
    (tooltip "Stop the trainer") asks to stop the trainer.
- **Pointer ring** (`PointerRing`): the trainer's marker on the ground, an `Accent` ring 2.4
  units across that pulses (its size by 8% and its opacity between 0.45 and 0.85, every 1.2 s).
- **Debug overlay** (`DebugOverlay`): F3 toggles it; it starts hidden. Two lines of outlined text
  (readable over bright ground) in the top-left corner below the top bar: the world line (the
  seed or the bounded map's name, the biome and terrain under the player, their chunk and map
  cell) and the performance line, colored by headroom (see presentation.md). It never takes the
  mouse.

### Patterns

Which control or display does which job, so new UI picks from this list instead of inventing.

1. **Status strip** (`StatusStrip`): small icons, each with a short value beside it (a number, a
   percent, a time) or none, for **states and quantities the player checks at a glance**: on or
   off, online or not, a capacity drawn or supplied, a charge, a fuel burning, a truck.
   - Every icon has a tooltip with the full sentence ("Draws 10 Power, running at 80%").
   - Values are signed where they flow: "-10" for a draw, "+35" for a supply.
   - Color carries the state (`Tone`): `Text` normal, `TextDim` off or not applicable now,
     `Warning` degraded or cut off (a brownout, offline, no truck can reach), `Danger` broken (a
     blackout), `Good` on and online. With a value, the value takes the color (and a dim entry
     dims its icon); without one, the icon does.
   - Icons are 20 units, values `SmallSize`, a `Gap` between entries, 3 units from icon to value.
   - It runs as a row, wrapping to a further line past its width, or as a column, wrapping into a
     further column past its height (its wrap length, such as the picture beside it). The top bar
     works this way as a row; the building detail panel has a column beside its picture.
2. **Text rows** (`StatusRows`): for **values that need words**: lists ("Iron Ore 2 (500), Stone 1
   (250)"), sentences, or names ("Growing: 2:10 left", "Laterite motherlode"). A row stays only
   when an icon and a number cannot say it.
3. **Segmented picker** (`SegmentedPicker`): a row of flat cells, no background, each with a 2
   unit border, `TextDim` when not chosen and `Accent` when chosen, lightening on hover; exactly
   one is chosen. Labels are `SmallSize` with 8 units of side padding, cells 32 tall and 4 apart.
   For **mutually exclusive options with short labels (or icons), where seeing them all helps**:
   Queue / Hold, Best / Coal / Charcoal / Wood, Auto / Coal / Charcoal, and later the tier pickers.
4. **Dropdown** (`OptionButton`): for **choices whose options do not fit on one row**, and for
   **recipes**, which are dropdowns on every building for consistency.
   - **Segmented or dropdown: space decides.** A segmented picker's cells must fit the room it has
     on one row, each label whole (no ellipsis, no wrapping); option counts are only a guide. At
     14 with 8 units of padding Fuel's four options take about 252 units, fitting every side panel
     but the narrowest (248 inside).
   - A control chooses its form when it is built for a building or screen, not as the window
     resizes, so it never changes form under the player. `SegmentedPicker` does this itself: given
     its options and the room, it shows as cells, or as a dropdown when they do not fit.
5. **Icon toggle** (`IconToggle`): one icon button with two looks, for **a binary that has a
   natural icon and acts on the thing itself**: a building's on/off as the power symbol, `Good`
   on and `TextDim` off (off is the player's choice, not a problem), with a tooltip ("On: click
   to turn off", "Off: click to turn on"). A two-word binary like Queue / Hold is a segmented
   picker instead.
6. **Step buttons** (`StepButton`): for **a number the player sets**, between "-" and "+", the
   value in the middle.
7. **Buttons:** for **actions (verbs)** that do something once: Craft, Buy truck, Launch ship,
   Destroy. A button never shows a state; a label that changes with state is a sign it should be
   a toggle.
   - **Sized to their label** (`UiTheme.SizeToLabel`), not stretched: the label plus the theme's 16
     units of side padding, never narrower than `ButtonMinWidth` (96). In a narrow side panel a
     button sits centered; in a wider panel or a modal it sits where the layout reads best.
   - A **stack of buttons** (the game menu) shares one width, twice its widest button's, so a short
     menu still reads as a menu; a button **in a
     row with other controls** (Craft after the quantity, Buy and Sell truck side by side) takes
     its label's width in that row.
   - Small icon-like buttons (the queue's X, "-" and "+") keep their square size.
8. **Notes** (dim `SmallSize` lines): **under a control**, saying what it does now ("Best: burns
   Coal (40 s per unit)"), or why something cannot happen. Never on their own.
9. **Section headings** (`SectionHeading`): a `SmallSize` `TextDim` caps label, letter-spaced (1
   unit), over a 1 unit `PanelEdge` rule, grouping a panel's controls once it has more than one
   kind (Production, Fuel, Trucks).

### Components

- **Buttons:** `Cell` background with 16 px side padding; hover lightens to `CellHover` and draws
  the 2 px `Accent` outline; pressed darkens to `CellPressed`. Destructive buttons use `Danger`.
  Action buttons size to their label, at least 96 units wide (`UiTheme.SizeToLabel`, the button
  pattern above).
- Buttons take no keyboard focus (`FocusMode` None): the UI is driven by the mouse, and Space
  belongs to the camera, so it must never press the last-clicked button again.
- **Destructive buttons** ask once: the first click changes the label to a confirmation ("Confirm
  destroy") for 3 seconds, and only a second click in that time acts. Buttons act on release.
- **Step buttons** (`StepButton`, the "-" and "+" of an up/down value) act on press instead: one
  step as the mouse button goes down, then, still held after 0.5 s, 15 steps a second until
  released. Leaving the button or its turning disabled (the value at a limit) stops the repeat. It
  is timed by the frame, so it runs while the game is paused. Their tooltips end "Hold to repeat."
- **Icon rows:** a 22-unit resource icon, then the name and the amount in parentheses, as
  "Iron (23)"; hovering a row lightens its background (`CellHover`). The inventory lists them.
- **Status rows:** a two-column grid of 14 px label (in `TextDim`) and value (in `Text`) pairs;
  rows that do not apply are hidden.
- **Status strip** (`StatusStrip`, a `FlowContainer`): entries (`StatusEntry`: an id, an icon, an
  optional value, a `Tone`, the tooltip, and whether it is faint) laid out as a row or a column
  with a wrap length; fixed controls added first (an `IconToggle`) stay ahead of them. `Show`
  reuses the entry nodes, so it runs every frame. A faint entry draws its icon at 35% opacity
  (an empty slot). `UiTheme.ColorOf` is the one table from `Tone` to color.
- **Segmented picker** (`SegmentedPicker`): the cells of the pattern above, or its dropdown when
  they do not fit the room given to `SetOptions`. Setting `Selected` from code raises nothing;
  a player's choice raises `Chosen`. It registers itself at its id and each cell at
  "id/label" (`selectUi` picks by label in either form).
- **Icon toggle** (`IconToggle`): a flat 24-unit button with a 20-unit icon, tinted by `On`
  through the button's icon colors; hover draws the `Accent` outline over `CellHover`.
- **Section heading** (`SectionHeading`): the heading of the pattern above.
- **Progress bars:** 8 units tall, a `Cell` track with an `Accent` fill, radius 3.
- **Turntables** (`TurntableView`): a live 180-unit view of a building turning once every 18
  seconds on a `Cell` box, drawn from the same stage as the building pictures and only while
  visible.
- **Grid cells:** a `Cell` box with a picture over a centered 14 px label; hover draws the
  `Accent` outline.
- **Tooltips** (`Tooltip`): a `Panel` box with 8 px padding holding 14 px text lines, each
  optionally colored (`TipLine`). A hover tooltip shows 0.3 s after the pointer enters its
  control, beside the control (to its right, or its left when the right would leave the window),
  or below it for controls in a row (the top bar), and is kept inside the window. Tooltips never
  take the mouse.
- **The picture stage** (`PictureStage`): every UI picture of a model is drawn in its own small
  world lit like the game's (its sun, no environment), from the game camera's angle, with an
  orthographic camera fitted to the model: loosely (room to turn) for pictures and turntables,
  tightly for icons, where the camera centers on and fills the square with the model's outline
  as it sees it (every vertex projected onto the view), so a leaning model such as the water drop
  sits centered like the rest. `RenderOnce` draws a set of stages once into cached
  textures, in batches of at most 16 stages drawing at once across every caller (each batch over
  two frames), because Direct3D 12 runs out of sampler descriptors when every icon and picture
  draws in one frame. Headless runs draw nothing, so there are no pictures or icons there.
- **Building pictures** (`BuildingPictures`): each building is rendered once at startup into a
  256 px texture, in color and as a `Silhouette`, with its front toward the viewer. Grayscale (at
  70% brightness, for "not now") is a canvas shader on the color picture.
- **Resource icons** (`ResourceIcons`): each resource's item model (art-style.md) is rendered
  once at startup into a 64 px texture, shown at 20 to 24 UI units, so it stays sharp up to the
  2x UI scale. UI that shows an icon keeps its size when there is none (headless).
- **Costs** read one line per material, as "25 Wood", and an amount the player lacks in `Short`
  with what they hold, as "15 Stone (have 3)" (`BuildMenu.CostLines`).

## Related

- presentation.md (the screen UI, the build menu, the panels, and the placement tooltip),
  buildings.md (build mode), testing.md (UI ids, `clickUi`, `hoverUi`, and `scrollUi`).
