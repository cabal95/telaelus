# HUD Overlays and Inventory

- Status: Implemented
- Id: 2611122116-hud-overlays-and-inventory
- Depends-on: 2611122105-resource-icons

## Summary

Replaces the text HUD with styled UI. A top bar always shows the raw resources as icons and
amounts (with name tooltips) and the launch progress. A hint overlay just below it shows the
harvest hint with a real progress bar. The debug text (seed, location, performance) moves into
one F3 debug overlay, off by default, that other UI may cover. I opens an inventory panel listing
every resource held, raw first. The UI vocabulary is renamed to three kinds: modal, panel, and
overlay.

## Motivation

The HUD is still a stack of outlined text lines from the first prototype: names instead of
pictures, debug numbers mixed in with play information, and the side panel has to dodge it.
Separating what the player always needs (the top bar), what appears for a moment (hints), what
they ask for (the inventory), and what only developers need (the debug overlay) gives each its
own place and style.

## Design

### Terminology

The UI style blueprint names three kinds of UI:

- **Modal**: centered over a dimmed backdrop; takes all input until used or dismissed. The build
  menu.
- **Panel**: docked against the left edge below the top bar; captures only Escape, so the world
  keeps its input; closes by its Close button, Escape, a click on empty ground, or another modal
  or panel opening. The building detail and the inventory.
- **Overlay**: part of the screen rather than something opened: always shown (the top bar),
  shown while it applies (the hint overlay, later notifications), or toggled by the player (the
  debug overlay). Never takes keys and never closes anything.

("Panel" replaces the old "side panel"; "popup" was considered, but it usually means a small box
by the cursor that closes on any outside click, and Godot's `Popup` class works that way.) Modals and panels share one base, `Dialog` (one open at a time, Escape,
Close); overlays share another, `HudOverlay`. In code, today's `Overlay` becomes `Dialog`,
`OverlayKind.Side` becomes `DialogKind.Panel`, and `OverlayHost` becomes `DialogHost`.

### Layers

From back to front: the debug overlay; the top bar and hint overlay; dialogs; tooltips. The
debug overlay never moves anything: panels and the hint overlay lay out as if it were not there,
and they may cover it.

### Top bar

- A full-width strip across the top of the window, in the panel style (`Panel` background,
  `PanelEdge` bottom border, no rounded top corners), 36 UI units tall.
- **Left:** all eight raw resources (the six deposits, then Sand and Water, which nothing
  collects yet but are shown so the bar's sizing and spacing are settled), in table order, each
  as a 22-unit icon and its amount ("[log] 23"), 16 units apart. Hovering an entry shows a tooltip with its name ("Wood"). Amounts are whole
  numbers.
- **Right:** the launch progress, "[fuel icon] 0 / 500   [ingot icon] 0 / 100", with each
  amount in `Short` until it is met, and "[L] Launch ship" in `Accent` once both are; and the
  ships launched count once there is one ("Ships: 1"). The top bar's tooltips open below their
  entry (the hover tooltip gains a "below" placement), since one opening to the right covered
  the next entry.
- The bar takes the mouse (so hovering it never shows a building outline and clicks on it never
  reach the world), but no keys.

### Hint overlay

- Centered horizontally, 8 units below the top bar: a small panel-style box shown only while a
  hint applies. Today the one hint is harvesting: "[E] Harvest [wood icon] Wood" over an 8-unit
  progress bar (the same bar as the detail panel) toward the next unit.
- It never takes the mouse. Later hints and notifications (a building finished, a field ran out)
  stack in the same place, newest at the top.

### Debug overlay

- F3 toggles it; it starts hidden. It holds the two debug lines, in today's outlined HUD text:
  the world line (seed or map name, biome and terrain, chunk, map cell) and the performance line
  (with its budget coloring).
- It sits in the top-left corner below the top bar. It is drawn behind everything else, never
  takes the mouse, and is ignored when laying out panels, so an open panel covers it.

### Inventory panel

- I toggles it; it is a panel (above), titled "Inventory". Like every panel, opening it closes
  a building's detail panel and the other way round: one shows at a time.
- Rows: a 22-unit icon, then "Iron (23)" (the name, then the amount in parentheses).
- Order: raw resources (deposits, then terrain-sourced) first in table order, then a thin
  `PanelEdge` rule, then the other resources (refined) in table order.
- Every resource is listed, including those at 0. (Hiding undiscovered ones is a later choice,
  once there are enough items to crowd the panel.)
- Each row's tooltip is unnecessary (the name is shown); hovering a row only highlights it.
- The rows refresh every frame while it is open.

### The HUD afterwards

`Hud` (the text labels) goes away: its world and performance lines move into the debug overlay,
the resource totals and launch line into the top bar, and the harvest hint into the hint
overlay. Panels start below the top bar (`DialogHost.PanelTop`, the bar's height; it was
`SideTop`, which followed the HUD text). Panels are 85% opaque, so the debug overlay's text
shows faintly through a panel that covers it.

In the top bar the water drop sat high: icons were framed by the model's bounding box, and the
drop leans back, so its shape was off-center in the box. Icons are now framed by the model's
outline as the icon camera sees it (`PictureStage` projects every vertex onto the view and
centers and sizes the camera on that), which also evens out how much of its square each icon
fills.

### Testing

- Snapshot: `ui.debug` (shown or hidden); `ui.topBar` (each entry's `type` and `amount`, and the
  launch text); `ui.hint` (the hint's text and progress, or null); while the inventory is open,
  `ui.inventory` (its rows in order, each `type`, `amount`, and `section`: `raw` or `other`).
  `ui.open` reports dialog ids as today (`buildMenu`, `buildingDetail`, `inventory`).
- `hoverUi` ids for the top bar entries (`topBar/<ResourceType>`) so their tooltips can be
  checked, and `inventory/<ResourceType>` for inventory rows.
- A new `hud` scenario: debug hidden at start and toggled by F3; top bar amounts follow the
  stockpile; the tooltip names a resource; the hint appears beside a field and its bar fills;
  I opens and closes the inventory with the right order and sections; the building detail and
  the inventory replace each other; a panel opens below the top bar while the debug overlay is
  shown.
- Scenarios that read HUD text or rely on the debug lines being visible are updated.

## Code changes

- Rename `Overlay` to `Dialog`, `OverlayKind` to `DialogKind` (`Modal`, `Panel`), and
  `OverlayHost` to `DialogHost`, and update their users.
- `Presentation/Ui/HudOverlay.cs` (base), `TopBar.cs`, `HintOverlay.cs`, `DebugOverlay.cs`,
  `InventoryPanel.cs` (new).
- `Hud.cs` goes; `GameRoot` creates the overlays and the inventory, binds I and F3, and sets
  `SideTop` from the top bar.
- Testing: the snapshot fields above, the `hud` scenario, and updated scenarios.
- Run the headless import after adding the scripts, and commit their `.uid` files.

## Blueprint updates

- ui-style.md: the three kinds and their rules; layers; the top bar, hint overlay, debug
  overlay, and icon rows as components.
- presentation.md: the HUD section becomes the overlays and the inventory; the debug overlay and
  F3.
- player.md and resources.md: where the harvest hint and resource totals now show.
- testing.md: the snapshot fields, `hoverUi` ids, and the `hud` scenario.

## Verification

- `dotnet build game.slnx` and both test suites.
- The `hud` scenario windowed at 1280x720 and 2288x1358, reading the screenshots: the top bar
  with icons and a tooltip, the hint with its bar, the inventory, and the debug overlay shown and
  covered by a panel.
