# UI Style Blueprint

## Overview

How every piece of on-screen UI looks and behaves: the color and size tokens, the shared Godot
theme, the UI scale, the two overlay positions and how each routes input, and the shared
components (buttons, cells, tooltips). Overlays use these rules and never set their own colors or
sizes. The code lives in `src/Game/Presentation/Ui/`.

## Current state

### Scale

- UI is authored for a 720 px tall window. Each UI layer (`UiLayer`, a `CanvasLayer`; the HUD and
  the overlay host are both one) scales its contents by window height / 720, clamped to 1.0 to
  2.0 (`UiTheme.ScaleFor`). The layer's `Root` control covers the window in unscaled units, so
  layout and anchors work as if the window were 720 px tall. This is the same reference height
  the camera's zoom uses, so a larger window shows more of the world at the same scale, and the
  UI grows with it instead of shrinking.

### Tokens

`UiTheme` holds the tokens; nothing else names a UI color or size.

| Token | Value (RGB, alpha) | Use |
|---|---|---|
| `Panel` | 0.08, 0.10, 0.09 at 85% | Overlay and tooltip background |
| `PanelEdge` | 0.30, 0.36, 0.32 | Panel border (1 px), scroll grabber |
| `Cell` | 0.14, 0.17, 0.15 at 90% | Grid cell and button background |
| `CellHover` / `CellPressed` | 0.19, 0.23, 0.20 / 0.10, 0.12, 0.11 at 95% | Hovered and pressed cells and buttons |
| `Text` | 0.92, 0.94, 0.92 | Body text (the HUD's text color) |
| `TextDim` | 0.60, 0.64, 0.61 | Secondary text, hints, "Undiscovered" |
| `Accent` | 0.98, 0.80, 0.35 | Hover outline, title rule, tooltip headings |
| `Short` | 1.00, 0.42, 0.38 | A cost amount the player lacks |
| `Danger` | 0.85, 0.30, 0.25 | Destructive buttons |
| `Backdrop` | black at 35% | Behind a modal overlay |
| `Silhouette` | 0.06, 0.07, 0.06 | Undiscovered building pictures |

The accent is a warm amber that reads against both the green ground and the dark panel.

- **Shape and spacing:** corner radius 6 on panels and 4 on cells and buttons; an 8 px spacing
  grid (`Gap` 8 between items, `Padding` 16 inside panels); hover outlines are 2 px.
- **Type:** Godot's default font, at 22 (overlay titles), 16 (body, buttons), and 14 (cell labels,
  tooltips). Any font shipped later must carry a license that allows bundling it in a game (the
  SIL Open Font License does), with the license file kept beside the font.
- `UiTheme.Theme` is the one Godot `Theme` built from these (labels, panels, buttons, the
  vertical scroll bar); the UI layers set it on their root.

### Overlays

An `Overlay` is a panel with a centered title over an `Accent` rule, a vertically scrolling body,
and a Close button at the bottom center. The `OverlayHost` (a UI layer above the HUD, running
even while the game is paused) owns them:

- One overlay is open at a time; opening one closes the other.
- Escape closes the open overlay (the host takes it before the world sees it).
- The game keeps running while an overlay is open (no pause: the sim is the future co-op server).
- Clicks and the mouse wheel over a panel never reach the world.

There are two positions:

- **Modal:** centered over the dimmed backdrop, 60% of the window wide (at least 420 and at most
  900 UI units) and as tall as its content up to 80% of the window, after which the body
  scrolls. It takes all input until it is dismissed: the host swallows every key (handing it to
  the overlay first, so the build menu can close on B), and the world reads no movement or
  harvest input while one is open (`OverlayHost.CapturesWorldInput`). Clicking the backdrop
  closes it. The build menu is modal.
- **Side panel:** against the left edge with a 16 px margin, a third of the window wide (at least
  280 UI units), the full height less the margins, with no backdrop. It captures only Escape, so
  movement, harvesting, zoom over the world, and every other key still reach the game. It closes
  by its Close button, Escape, a click on the world that selects nothing, or another overlay
  opening.

### Components

- **Buttons:** `Cell` background with 16 px side padding; hover lightens to `CellHover` and draws
  the 2 px `Accent` outline; pressed darkens to `CellPressed`. Destructive buttons use `Danger`.
- **Grid cells:** a `Cell` box with a picture over a centered 14 px label; hover draws the
  `Accent` outline.
- **Tooltips** (`Tooltip`): a `Panel` box with 8 px padding holding 14 px text lines, each
  optionally colored (`TipLine`). A hover tooltip shows 0.3 s after the pointer enters its
  control, beside the control (to its right, or its left when the right would leave the window),
  and is kept inside the window. Tooltips never take the mouse.
- **Building pictures** (`BuildingPictures`): each building is rendered once at startup into a
  256 px texture, in color and as a `Silhouette`, from the game camera's angle in its own small
  world lit like the game's (its sun, no environment), with its front toward the viewer.
  Grayscale (at 70% brightness, for "not now") is a canvas shader on the color picture. Headless
  runs have no pictures.
- **Costs** read one line per material, as "25 Wood", and an amount the player lacks in `Short`
  with what they hold, as "15 Stone (have 3)" (`BuildMenu.CostLines`).

## Related

- presentation.md (the HUD, the build menu, and the placement tooltip), buildings.md (build
  mode), testing.md (UI ids, `clickUi`, and `hoverUi`).
