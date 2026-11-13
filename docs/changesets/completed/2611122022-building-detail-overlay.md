# Building Detail Overlay

- Status: Implemented
- Id: 2611122022-building-detail-overlay
- Depends-on: 2611122002-ui-style-and-build-menu

## Summary

Clicking a placed building opens a detail side panel at the left edge of the screen: its name, a slowly turning picture of it,
its status (on or off, working or idle, the fields in range by type, and what it is working
on), and its actions: turn off or on, destroy (refunding half its cost, rounded up), and close.
The sim gains buildings that can be switched off and destroyed.

## Motivation

Once placed, a building is a black box: the player cannot tell whether a Mine is digging or
idle, which deposit it is on, or why a Smelter has stopped, and a misplaced building is there
for good. This overlay is where a placed building is looked at and managed, and it is the first
user of the styling blueprint after the build menu.

## Design

### Opening

With no building selected for placement, left-clicking a placed building opens its detail
overlay. Hovering a building first shows a thin `accent` outline on its footprint (the ghost's
plate, in the accent color), so the player can tell the click will land. While the panel is
open, its building keeps that outline, so the player can tell which building they are looking
at.

The overlay is a **side panel** (see ui-style.md): against the left edge, a third of the window
wide, starting just below the HUD's text so it never covers the resource totals, no dimmed
backdrop. It captures only Escape, so the player can walk, harvest, and zoom
with it open. It closes by:

- its close button or Escape;
- a click on the world that selects nothing (empty terrain, a field);
- another overlay opening: B opens the build menu, and clicking another building switches the
  panel to that building.

### Layout, top to bottom

The whole panel body scrolls vertically, so it fits on small windows; the close button stays
pinned at the bottom.

1. **Name**, as the overlay title.
2. **Picture**: a live 3D view of the building's model, 180 px square at UI scale 1, turning
   about its center at one revolution per 18 seconds, lit like the world, on the `cell`
   background. It renders in its own small world (a `SubViewport` with its own `World3D`, a
   camera at the game's view angle, and one light), and only while the overlay is open.
3. **Status**, as label and value rows:
   - **Power**: On or Off.
   - **State**: Working, or Idle with the reason, from the sim:
     - Off: "Switched off".
     - Extractor: "No fields in range".
     - Processor: "Waiting for Iron Ore" (each missing input).
   - **In range** (extractors): the non-depleted fields in the work area, counted by type, each
     followed by what those fields hold in all, for example "Iron Ore 2 (500), Stone 1 (250)"
     (two Iron Ore fields of 250 each), or "none".
   - **Working on** (extractors that work more than one type, so the Mine today): the current
     field's type and its remaining amount, for example "Stone (212 left)".
   - **Recipe** (processors): "1 Iron Ore to 1 Iron, 16 s", with the current cycle's progress
     as a bar.
   - **Progress** (extractors): progress toward the next unit, as a bar.
4. **Actions**:
   - **Turn off** / **Turn on**, one button that flips with the state.
   - **Destroy**, a `danger` button showing the refund ("Destroy (refund 13 Wood, 8 Stone)"). It
     asks once: the first click changes the button to "Confirm destroy" for 3 seconds, and a
     second click in that time destroys it (see open questions).
   - **Close**, bottom center, as in every overlay.

The status rows refresh every frame while the overlay is open; the overlay closes itself if its
building is destroyed.

### Sim rules

- `Building.Enabled` (true when placed). `SetBuildingEnabled(id, enabled)` switches it. A
  switched-off building does nothing, and switching off drops the work in hand: progress goes
  back to 0, and a processor's running cycle ends with its inputs returned to the stockpile
  (fuel, once buildings burn it, will not be). Switched back on, it starts a whole unit or cycle
  over.
- `BuildingStatusOf(building)` gives the state and idle reason above, so the rule lives in the
  sim, not the overlay.
- `TryDestroyBuilding(id)` removes the building, frees its cells, and adds half of each cost
  amount, rounded up, to the stockpile: a Lumber Camp (15 Wood) returns 8 Wood; a Mine (25 Wood,
  15 Stone) returns 13 Wood and 8 Stone. A processor destroyed mid-cycle also returns the
  inputs that cycle consumed, in full (1 Iron Ore for a Smelter). Fuel, once buildings burn it
  (a later changeset), is not refunded: what has burned is gone. A building
  placed through `Debug.PlaceBuilding` refunds the same way (the refund follows the type, not
  what was paid).

### Testing

- Snapshot: `buildings[]` gains `enabled`, `status`, and `idleReason`; `hoveredBuilding` is the
  building under the pointer and `focusedBuilding` the one the panel outlines; `camera.zoom`; `ui.open` can be `"buildingDetail"`, with `ui.building` (its id),
  `ui.rows` (the shown status rows, keyed in camel case, as `inRange`, since snapshot paths take
  no spaces), `ui.destroy` (the Destroy button's text), and `ui.turnDegrees`.
- Scenario: `building-detail` places a Mine beside Iron Ore and Stone, clicks it, checks the
  status rows, walks with the panel open, turns it off and on (the stockpile stops and resumes),
  clicks a second building to switch the panel and empty terrain to close it, destroys one, and
  checks the refund and the freed cells; windowed screenshots of the overlay at two moments show the turn.
- Unit tests: off extractors and processors do nothing, drop their progress, and a processor
  returns its cycle's inputs; status and
  idle reasons; destroy refunds half the cost rounded up plus a mid-cycle processor's inputs, and
  frees cells.

## Code changes

- `Building`: `Enabled`.
- `Simulation`: `SetBuildingEnabled`, `TryDestroyBuilding`, `BuildingStatusOf` (a
  `BuildingStatus` record: state and idle reason); `StepBuildings` skips switched-off buildings.
- `Presentation/Ui/BuildingDetail.cs`: the overlay.
- `Presentation/Ui/TurntableView.cs`: the turning model in its own `SubViewport` world.
- `BuildController`: hover outline over a placed building, and with nothing selected, a click
  that opens the panel on a building or closes it elsewhere on the world.
- `GameRoot`: creates the overlay.
- Testing: the snapshot fields and the `building-detail` scenario; the `clickUi` ids
  `buildingDetail/toggle`, `buildingDetail/destroy`, and `buildingDetail/close`.
- `Hud.Bottom` and `OverlayHost.SideTop`: the side panel starts below the HUD's text.
- Overlays and the modal backdrop set `MouseForcePassScrollEvents` off, so a wheel event the
  scroll body leaves unused (at the end of its range) never reaches the world's zoom.
- A `scrollUi { id, ticks }` scenario command turns the wheel over a UI control; the scenario
  checks the camera zoom is unchanged after scrolling past the end of the panel.
- Run the headless import after adding the scripts, and commit their `.uid` files.

## Blueprint updates

- buildings.md: switching off, status and idle reasons, destroying with its refund.
- presentation.md: the detail overlay, the turntable view, and the hover outline.
- ui-style.md: the label and value status rows, progress bars, and the confirm-to-destroy
  pattern, as shared components.
- testing.md: the new snapshot fields, scenario, and `clickUi` ids.

## Verification

- `dotnet build game.slnx`, `dotnet test src/Game.Sim.Tests`, `dotnet test
  src/Game.Integration.Tests`.
- The `building-detail` scenario windowed at 1280x720 and at a short window (1280x600) to see
  the body scroll, reading the screenshots.
