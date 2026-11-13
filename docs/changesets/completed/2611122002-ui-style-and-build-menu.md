# UI Style and Build Menu

- Status: Implemented
- Id: 2611122002-ui-style-and-build-menu
- Depends-on: none

## Summary

Starts a UI styling blueprint (`ui-style.md`) with a shared theme, and replaces the HUD's build
line with a build menu opened by B: a translucent overlay with a scrolling grid of building
cells. Discovered buildings come first, in full color when affordable and grayscale when not;
undiscovered buildings follow as off-black silhouettes labeled "Undiscovered". Hovering a cell
outlines it and shows its cost; clicking it closes the menu with that building selected for
placement.

## Motivation

The build line was a stopgap: it grows with every building, hides what is still to be found,
and gives the player no picture of what they are about to build. Every overlay to come (this
menu, the building detail overlay, later inventories and the launch screen) needs the same
panel, text, button, and hover rules, so the rules are written down once, before the second
overlay exists.

## Design

### The UI style blueprint and theme

A new blueprint, `docs/blueprints/ui-style.md`, holds the rules every overlay follows, and one
Godot `Theme` built in code (`UiTheme`) carries them so no overlay sets its own colors or sizes.
Starting values:

- **Scale.** UI sizes are authored for a 720 px tall window and scaled by window height / 720
  (clamped to 1.0 to 2.0), the same reference the camera uses, so a large window does not shrink
  the UI. The HUD labels follow the same scale.
- **Colors** (tokens, named in the blueprint):

  | Token | Value | Use |
  |---|---|---|
  | `panel` | 0.08, 0.10, 0.09 at 85% opacity | Overlay background |
  | `panel_edge` | 0.30, 0.36, 0.32 | Panel border, 1 px |
  | `cell` | 0.14, 0.17, 0.15 at 90% | Grid cell background |
  | `text` | 0.92, 0.94, 0.92 | Body text (the HUD's text color) |
  | `text_dim` | 0.60, 0.64, 0.61 | Secondary text, "Undiscovered" |
  | `accent` | 0.98, 0.80, 0.35 | Hover outline, focus, title rule |
  | `short` | 1.00, 0.42, 0.38 | A cost amount the player lacks (the HUD's red) |
  | `danger` | 0.85, 0.30, 0.25 | Destructive buttons |

  The accent is a warm amber that reads against both the green ground and the dark panel.
- **Shape.** 6 px corner radius on panels, 4 px on cells and buttons; an 8 px spacing grid
  (gaps of 8, paddings of 16).
- **Type.** Godot's default font for now; sizes 22 (overlay title), 16 (body, buttons), 14
  (cell labels, tooltips). A custom font is a later, separate choice; any font we ship must carry
  a license that allows bundling it in a game (the SIL Open Font License does), with the license
  file kept beside the font.
- **Overlays** come in two positions. In both, one overlay is open at a time (opening one closes
  the other), Escape closes it, the close button sits bottom center, the game keeps running (no
  pause: the sim is the future co-op server, and pausing would not carry over), and clicks and
  the mouse wheel over the panel never reach the world.
  - **Modal**: centered over a dimmed full-screen backdrop (black at 35%). It takes all input
    until it is dismissed: the player must use it or close it. Clicking the backdrop closes it.
    The build menu is modal.
  - **Side panel**: against the left edge (16 px margin), one third of the window width, full
    height less the margins, no backdrop. It captures only Escape: movement, harvesting, zoom
    over the world, and every other key still reach the game, so the player can walk around with
    it open. It closes by its close button, Escape, a click on the world that selects nothing
    (empty terrain, a field), or another overlay opening (B, or clicking another building). The
    building detail overlay is a side panel.
- **Buttons.** `cell` background, `text` label; hover lightens the background and draws a 2 px
  `accent` outline; pressed darkens it. Destructive buttons use `danger`.
- **Tooltips.** A small `panel` box with 8 px padding near the pointer, after a 0.3 s hover.

### The build menu

- **Opening.** B toggles the menu; Escape, the close button, or a click on the backdrop closes
  it. Opening it cancels any building selected for placement. The number keys 1 to 5 go away:
  the menu is the only way into build mode.
- **Panel.** A modal overlay, titled "Buildings" at the top, the close button bottom center below the grid. The
  panel is 60% of the window width (at least 420 px, at most 900, at UI scale 1) and as tall as
  its content up to 80% of the window height; past that the grid scrolls vertically and the title
  and close button stay put.
- **Grid.** Square cells of 112 px: the building's picture filling most of the cell, its label
  centered below. The column count is whatever fits the panel width. Order:
  1. Discovered buildings (`IsUnlocked`), by name.
  2. Undiscovered buildings, by name. (Sorted by name too, so their order gives nothing away.)
- **Cell looks.**
  - Discovered and affordable: the building in full color, its name.
  - Discovered, not affordable now: the same picture in grayscale (a desaturating shader on the
    picture, at 70% brightness so it also reads as "not now"), its name.
  - Undiscovered: the building's shape as a solid off-black silhouette (0.06, 0.07, 0.06) on the
    cell, labeled "Undiscovered" in `text_dim`.
- **Pictures.** Each building is rendered once at startup from a fixed three-quarter view, the
  same angle as the game camera, into a 256 px texture, in its own small world lit like the
  game's (its sun and no environment: under the Compatibility renderer an environment in any world
  carried over into the game's world and brightened it): once in color and once as a silhouette (every
  surface overridden with an unshaded off-black material). Grayscale is a shader on the color
  picture, so no third render. Rendering once keeps the menu cheap however many buildings there
  are; nothing in the grid is a live 3D view.
- **Hover.** The cell gets the 2 px `accent` outline and a tooltip with the cost, one line per
  material, short amounts in `short` with what the player holds ("15 Stone (have 3)"). An
  undiscovered cell gets the outline but no tooltip, so its cost stays a mystery.
- **Clicking.** Clicking a discovered cell, affordable or not, closes the menu and selects that
  building for placement at the current facing, so the player can position it while the
  materials build up; the ghost shows red until it is affordable, as today. Clicking an
  undiscovered cell does nothing.
- **Live updates.** While open, the menu refreshes when the stockpile or the unlocked set
  changes, so a cell turns from gray to color as the Mine fills the last Stone.

### The HUD without the build line

The build line goes, with nothing in its place on the HUD. While placing, what it showed moves
into a **placement tooltip** that follows the pointer, offset below and to the right so it never
covers the ghost: the building's name, its cost (short amounts marked), fields in range for an
extractor, and a hint line in `text_dim`, "R rotate   Esc cancel". The ghost itself already
shows the facing (the model is turned) and whether the spot is open and affordable (green or
red), so neither needs text. With nothing selected, the HUD shows no build text.

### Testing

- The snapshot gains `ui`: `open` (null, or `"buildMenu"`), and for the build menu its `cells`
  in order, each with `type`, `state` (`affordable`, `unaffordable`, `undiscovered`), and
  `label`.
- A scenario command `clickUi { id }` clicks a named control through real input events, for
  example `{ "clickUi": { "id": "buildMenu/LumberCamp" } }` or `buildMenu/close`; `hoverUi`
  moves the pointer over one, so the tooltip can be checked in a screenshot.

## Code changes

- `Presentation/Ui/UiTheme.cs`: the tokens and the `Theme` (panel, button, label, tooltip, and
  scroll styles) and the UI scale.
- `Presentation/Ui/Overlay.cs`: the base overlay in its two positions, modal (backdrop, centered
  panel, all input) and side panel (left edge, a third of the width, Escape only), with the
  title, scrolling body, bottom-center close button, and one open at a time.
- `Presentation/Ui/PlacementTooltip.cs`: the tooltip that follows the pointer while placing.
- `Presentation/Ui/BuildingPictures.cs`: renders each building's color and silhouette pictures
  once through an offscreen `SubViewport` and caches the textures.
- `Presentation/Ui/BuildMenu.cs`: the grid, cell states, hover tooltip, and selection.
- The desaturating canvas shader is built in code on `BuildingPictures` (no shader file).
- `Presentation/Ui/UiLayer.cs` and `Presentation/Ui/Tooltip.cs`: the scaled UI layer the HUD and
  the overlay host share, and the tooltip panel.
- `BuildController`: the number keys go; a `Select(type)` the menu calls; it feeds the
  placement tooltip.
- `Hud`: the build line goes; the labels follow the UI scale.
- `GameRoot`: creates the menu; B toggles it.
- Testing: the `ui` snapshot, `clickUi` and `hoverUi`; scenarios that pressed 1 to 5 select
  through the menu instead (`build-rotate`, `building-models`, `pause-step`, `place-at-mouse`,
  `place-on-player`, `terrain-types`); a new `build-menu` scenario covers ordering, the three
  cell states, the cost tooltip (and none on an undiscovered cell), clicking to place, the
  placement tooltip, and closing by button, backdrop, and Escape. The snapshot's `build` gains
  the placement tooltip's text lines.
- The scenario runner sizes a headless window to 1280 x 720 (it is otherwise 64 x 64), and UI
  pointer events go straight to the viewport, so UI clicks work in headless runs.
- Run the headless import after adding the scripts, and commit their `.uid` files.

## Blueprint updates

- ui-style.md (new): everything under "The UI style blueprint and theme" above.
- presentation.md: the build menu, the building pictures, the placement tooltip, the HUD without
  a build line, and the UI scale; how each overlay position routes input.
- buildings.md: build mode starts from the menu instead of number keys.
- testing.md: the `ui` snapshot and the `clickUi` and `hoverUi` commands; the new scenario.
- INDEX.md: list ui-style.md.

## Verification

- `dotnet build game.slnx`, `dotnet test src/Game.Sim.Tests`, `dotnet test
  src/Game.Integration.Tests`.
- The `build-menu` scenario windowed at 1280x720, and the menu at 2288x1358 and 640x400,
  reading the screenshots: the three cell states, the hover outline and tooltip, the scale on the
  large window, and scrolling on the short one (two rows taller than the panel allows).
