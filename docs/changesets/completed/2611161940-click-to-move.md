# Click to Move

- Status: Implemented
- Id: 2611161940-click-to-move
- Depends-on: 2611161851-demo-save

## Summary

The character walks where the player right-clicks (or Ctrl + left-clicks) on the world, along a
path round whatever is in the way, in place of WASD and the arrow keys. The same press dragged
still pans the camera. The road and bridge tools remove with Shift alone.

## Motivation

The game is played with the mouse: building, selecting, panning, and the panels. Walking is the one
thing on the keyboard, so a player keeps a hand on each and moves it back and forth.

## Design

### Clicks

The press that pans the camera also walks: a right press, or a left one with Ctrl, over the world.

| The press | Before | After |
|---|---|---|
| Right (or Ctrl + left), released within 4 pixels | Nothing | Walks to the ground under the pointer |
| Right (or Ctrl + left), dragged past 4 pixels | Pans the camera | Unchanged |
| Left on a building or field | Opens its panel | Unchanged |
| Left on open ground | Closes an open panel | Unchanged (never walks) |
| Left in build mode | Places | Unchanged |
| Right with a Road or Bridge tool | Removes paving | Pans or walks, as anywhere |
| Shift + left with a Road or Bridge tool | Removes paving | Unchanged (the only way to remove) |

- A walk click on a building, a field, or water walks to the ground beside it.
- It walks in build mode too, and leaves an open panel open.
- A press over a panel, a modal, or the top bar never walks.
- `E` still harvests a field and collects from or loads a building beside the character (OQ3).

### The walk (sim)

- A new command, `Simulation.WalkTo(x, z)` (a world point), gives the player a destination and a
  path; `StopWalking()` clears it. The sim stays the authority: the presentation only sends the
  command, as it does for placing a building.
- **The path** uses the trucks' A* over fine cells (8 directions, no corner cutting, the cost of
  each step its length over the terrain speed, roads preferred where they save time), with the
  player's walkability: buildings, fields, water, mountains, and river channels block. It runs over
  **explored** cells only, so a path never shows what lies under the fog.
- **A blocked target** (a building, a field, water, a mountain, or the fog) walks to the reachable
  cell nearest the click instead (OQ4). With nothing reachable, nothing happens.
- **Each tick** the player moves along the path at `Speed` times the terrain speed under them, as
  before, and stops on arriving. A building placed across the path makes the player search again
  for the same destination.
- **Not saved:** like the held actions, a walk in progress stops on load.
- `PlayerIntent.Move` and the key reading in `GameRoot` are removed (OQ1). `PlayerIntent.Harvest`
  stays.

### What the player sees

- A small accent ring marks the destination while the character walks there, and fades on arrival.
- The character plays its walk clip and faces along the path, as it did for key movement.
- **The camera** (OQ2): a click to walk leaves a panned (detached) camera where it is, since the
  player is looking at where they clicked; Space still brings it back. While attached it follows
  the character, as before.
- The road and bridge placement tooltip reads "Drag to lay   Shift-drag to remove   Esc cancel".

## Code changes

- `src/Telaelus.Sim/SimulationWalking.cs` (new): `WalkTo`, `StopWalking`, the player's rule, and
  following the path each tick (planning again when a building blocks it, and going on into the
  fog as it clears).
- `src/Telaelus.Sim/Player.cs`: `Target`, `Path`, `Destination`, `Heading`, and `Walking`.
- `src/Telaelus.Sim/SimulationPaths.cs`: `FindPath` takes a passability rule and can return the
  path to the nearest cell it reached.
- `src/Telaelus.Sim/PlayerIntent.cs`: `Move` removed; `Simulation.MovePlayer` replaced.
- Teleporting, a world reset, and loading stop a walk.
- `src/Telaelus/Presentation/GameRoot.cs`: the key reading removed; a camera press released
  without dragging walks; the view faces the sim's `Heading`; the marker.
- `src/Telaelus/Presentation/CameraRig.cs`: `Dragging` (whether the press has become a drag).
- `src/Telaelus/Presentation/BuildController.cs`: `PointerGround`; the line tools take only the
  left button, with Shift to remove.
- `src/Telaelus/Presentation/WalkMarker.cs` (new): the destination ring.
- `src/Telaelus/Testing`: the snapshot's player gains `walking` and `destination`; `click` takes
  `button` and `ctrl`.
- Sim tests: walking round a building, along a road when that is quicker, to the nearest reachable
  cell, never through the fog, re-pathing when a building is placed, stopping on arrival.
- Scenarios: a new `walk` scenario. The scenarios that walked with movement keys (`build-menu`,
  `building-detail`, `free-camera`, `hud`, `pause-step`, `place-on-player`, `perf-walk`) walk by
  right click instead, and `roads` removes with a Shift drag.

## Blueprint updates

- player.md: movement by destination and path, the clicks, the marker, and the camera.
- simulation.md: the `WalkTo` and `StopWalking` commands, and `PlayerIntent` without `Move`.
- resources.md: a right click on a field walks beside it; a left click still only selects.
- buildings.md: paths (the player's rule beside the trucks'); the line tools' buttons and tooltip.
- ui-style.md: a right or Ctrl + left click over the world walks.
- testing.md: the snapshot's new player fields, `click`'s options, the sim tests, and the
  scenarios.

## Verification

- Sim and integration tests pass.
- In the game: a right click walks round buildings and fields; a right drag pans; a left click on
  a building or field opens its panel without walking; build mode places with the left and walks
  with the right; a Shift drag removes road; a click in the fog walks to its edge.

## Decisions

1. **Keys:** removed. Walking is by mouse only.
2. **Camera:** a click to walk leaves a panned camera where it is; Space brings it back.
3. **Clicking a field or building:** a left click still only opens its panel; walking up to act is
   a later change.
4. **A blocked target:** walk to the reachable cell nearest the click.
5. **Which click walks** (after trying it): the first version walked on a left click on open
   ground, with holding the button steering. It did not feel right, so walking moved to the right
   click (or Ctrl + left), the press that already pans: a click walks and a drag pans. Steering by
   holding went with it, since holding that press pans. The line tools gave up the right button so
   it walks and pans there too; Shift + left is the only remove.

## Implementation notes

- **Click or drag.** `CameraRig` already told a click from a drag by the 4-pixel threshold; it now
  reports `Dragging`, and `GameRoot` walks on the release of a press that never dragged, to the
  ground under the pointer then. A walk starts on the release, so in a paused scenario it comes
  one step after the press.
- **Into the fog.** Paths stay on explored cells, so a click into the fog first walks to the
  nearest explored ground. A walk that ends short of its target plans again once the walk has
  explored more, so the player keeps walking into the fog as it clears instead of stopping at its
  first edge.
- **Nearest reachable cell.** The pathfinder keeps the expanded cell nearest the goal (by its
  distance estimate) and, for the player, returns the path to it when the goal cannot be reached
  or the search hits its 60,000-cell limit. No separate search near the click is needed.
- **Scenarios.** `click` gained `button` and `ctrl`. `perf-walk` checks that its walk ends where
  it should, so the benchmark cannot quietly measure a player standing still. `building-detail`
  and `hud` checked that a side panel leaves the world its input by walking with keys;
  `building-detail` checks hover with the panel open instead, and `hud` drops its copy. In
  `free-camera` a 2-pixel right drag is now a click that walks, so the player is put back after it.
- **Not done:** paths follow cell centers in 8 directions, so a long diagonal can zigzag a little;
  smoothing the path is left for later.

## Open questions (answered)

1. **Remove the keys, or keep them too?** Removing WASD and the arrows makes the mouse the one way
   to walk, as asked; keeping them costs little and suits players who like keys (Don't Starve keeps
   both). Proposed: remove them.
2. **Should walking bring a panned camera back?** Any movement key used to bring it back.
   Proposed: no; the player is looking at where they clicked, and Space brings it back.
3. **Should clicking a field or building walk there and act?** In action RPGs clicking a thing
   walks up to it and uses it: here that would walk beside a field and harvest, or beside a
   building and collect or load, replacing `E`. Proposed: not in this changeset; clicking still
   only opens the panel, and walking to act comes in its own change once walking settles.
4. **A click on something that blocks walking:** walk to the reachable cell nearest the click
   (proposed), or do nothing and show the marker as refused.
