# UI Style Blueprint

## Overview

How every piece of on-screen UI looks and behaves: the three kinds of UI (modals, panels, and
overlays) and how each routes input, the color and size tokens, the shared Godot theme, the UI
scale, the layers, and the shared components (buttons, cells, tooltips, icons). All UI uses these
rules and never sets its own colors or sizes. The code lives in `src/Game/Presentation/Ui/`.

## Current state

### Kinds of UI

- **Modal**: centered over a dimmed backdrop; takes all input until it is used or dismissed. The
  build menu.
- **Panel**: docked against the left edge below the top bar; captures only Escape, so the world
  keeps its input. The building detail, the field detail, and the inventory.
- Over the world (not over any UI), a right drag or a Ctrl + left drag pans the camera (see
  player.md); over a panel, a modal, or the top bar it does nothing.
- **Overlay**: part of the screen rather than something opened: always shown (the top bar),
  shown while it applies (the hint overlay, later notifications), or toggled by the player (the
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
| `Danger` | 0.85, 0.30, 0.25 | Destructive buttons; a power blackout |
| `Warning` | 0.98, 0.66, 0.18 | Storage full (top bar, inventory, the harvest hint); a power brownout |
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
- The game keeps running while a dialog is open (no pause: the sim is the future co-op server).
- Clicks and the mouse wheel over a dialog never reach the world. A scroll body uses the wheel
  first, and once it is at the end of its range the dialog keeps the event (Godot otherwise
  passes unused wheel events up through a stopping control, `MouseForcePassScrollEvents`), so
  the world never zooms behind a dialog or the modal backdrop.

The two kinds place themselves:

- **Modal:** centered over the dimmed backdrop, 60% of the window wide (at least 420 and at most
  900 UI units) and as tall as its content up to 80% of the window, after which the body
  scrolls. It takes all input until it is dismissed: the host swallows every key (handing it to
  the modal first, so the build menu can close on B), and the world reads no movement or
  harvest input while one is open (`DialogHost.CapturesWorldInput`). Clicking the backdrop
  closes it.
- **Panel:** against the left edge with a 16 px margin, a third of the window wide (at least
  280 UI units), from just below the top bar (`DialogHost.PanelTop`) to the bottom margin, with
  no backdrop; when its content is taller, the body scrolls and the Close button stays pinned. It
  captures only Escape, so movement, harvesting, zoom over the world, and every other key still
  reach the game; its own toggle key (I for the inventory) reaches the game too, which closes it.
  It closes by its Close button, Escape, a click on the world that selects nothing, or another
  dialog opening.

### Overlays

- **Top bar** (`TopBar`): a full-width strip across the top of the window, 36 UI units tall, in
  the panel color with a 1 px `PanelEdge` bottom border and no rounded corners. Left: every
  pinned resource as a 22-unit icon and "amount / limit" in whole numbers (the amount alone for a
  Cargo or unlimited resource), in `Warning` when full, 16 units apart, each with a tooltip naming it (opening below the entry, so it never
  covers its neighbors). Right: the power readout (demand over supply, in normal text, `Warning`,
  or `Danger` by state), then the launch progress, the Rocket Fuel and Iron icons with "have /
  need" in `Short` until met, then "[L] Launch ship" in `Accent` once both are, and "Ships: n"
  once a ship has launched. It takes the mouse (so hovering it shows no building outline and
  clicks on it never reach the world) but no keys.
- **Hint overlay** (`HintOverlay`): centered, 8 units below the top bar, a small panel-style box
  shown only while a hint applies, never taking the mouse. Today the one hint is harvesting:
  "[E] Harvest" in `Accent`, the resource's icon and name, over a progress bar toward the next
  unit, or "Storage full" (14 units, `Warning`, centered) in place of the bar; the verb reads
  "[E] Harvest", "[E] Collect", or "[E] Load" for the target. Later hints and notifications stack in the same place.
- **Debug overlay** (`DebugOverlay`): F3 toggles it; it starts hidden. Two lines of outlined text
  (readable over bright ground) in the top-left corner below the top bar: the world line (the
  seed or the bounded map's name, the biome and terrain under the player, their chunk and map
  cell) and the performance line, colored by headroom (see presentation.md). It never takes the
  mouse.

### Components

- **Buttons:** `Cell` background with 16 px side padding; hover lightens to `CellHover` and draws
  the 2 px `Accent` outline; pressed darkens to `CellPressed`. Destructive buttons use `Danger`.
- Buttons take no keyboard focus (`FocusMode` None): the UI is driven by the mouse, and Space
  belongs to the camera, so it must never press the last-clicked button again.
- **Destructive buttons** ask once: the first click changes the label to a confirmation ("Confirm
  destroy") for 3 seconds, and only a second click in that time acts. Buttons act on release.
- **Icon rows:** a 22-unit resource icon, then the name and the amount in parentheses, as
  "Iron (23)"; hovering a row lightens its background (`CellHover`). The inventory lists them.
- **Status rows:** a two-column grid of 14 px label (in `TextDim`) and value (in `Text`) pairs;
  rows that do not apply are hidden.
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
