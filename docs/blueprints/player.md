# Player Blueprint

## Overview

The player is a character the user walks around the planet surface, by clicking where to go, to
explore, harvest by hand, and place buildings. This blueprint covers the player's simulation state, movement, input, and its
on-screen character and camera.

## Current state

### State and movement

- The simulation holds a `Player` with a world `Position` and a `Speed` (6 units per second). The
  player spawns at the bounded map's center, `(32, 0, 32)`, or at a generated world's spawn point
  (see world-generation.md).
- **Walking** is a command: `Simulation.WalkTo(x, z)` sends the player to a world point and
  `StopWalking()` stops them (`SimulationWalking.cs`). The player holds the `Target` asked for, the
  `Path` still to walk (the last point is the `Destination`), `Walking`, and the `Heading` moved this
  tick (zero when standing).
- **The path** is the trucks' A* (buildings.md, Paths) with the player's rule: a cell is passable
  when it is in bounds, its chunk exists, it is **explored**, and it is walkable. So a path never
  crosses the fog and never shows what lies under it. A target in a reachable cell ends on the
  point itself; a blocked one (water, a mountain, a building, a field, beyond the map's edge, or
  under the fog) ends on the reachable cell nearest it (by the search's distance estimate). With
  no step to take, `WalkTo` returns false and the player stays.
- **Each tick** (before revealing, see simulation.md) the player moves along the path at `Speed`
  times the speed multiplier of the cell under them at the tick's start: the road speed 1.5 on a
  road or bridge, else the terrain's (Sand and Snow 0.75, Mud 0.6, others 1; see terrain.md). A
  river bank moves at its terrain's speed. When the next point's cell is no longer walkable (a
  building placed across the path), the path is planned again once that tick.
- **On arriving** the walk ends, unless it ended short of a target under the fog and the walk has
  explored more since it was planned: then it is planned again, so a click into the fog keeps
  the player walking into it as it clears.
- Teleporting (`Debug.TeleportPlayer`), a world reset, and loading a save stop a walk; a walk is
  not saved.
- Walkability (see terrain.md) blocks water, lava, mountains, river channels, resource fields (all
  their fine cells, until depleted), and buildings.

### Input

- **A right click, or a left click with Ctrl, over the world walks** the player to the ground
  under the pointer. It is the same press that pans the camera: released before the pointer has
  moved more than 4 pixels it is a click and walks (`GameRoot._Input`, through
  `BuildController.PointerGround`); past that it is a drag and pans. On a building, a field, or
  water it walks to the nearest reachable ground. It walks in build mode too, and leaves an open
  panel open. A press over a panel, a modal, or the top bar never walks.
- A left click (no Ctrl) never walks: on a building or field it opens its panel, on open ground
  it closes an open side panel, and in build mode it places.
- There are no movement keys.
- Holding `E` sets `PlayerIntent.Harvest`, which drives hand-harvesting a field, or collecting from
  or loading a building when one is the interaction target (see resources.md).
- Dragging the map with the right mouse button, or the left with Ctrl held, pans the camera
  without moving the character (a click with either walks, above); Space brings it back (see the
  camera below). A Ctrl + left press
  never selects, clicks, or places.

### Character and camera (presentation)

- `PlayerView` renders the player from the character scene
  (`src/Telaelus/assets/models/character/character.tscn`, the generated low-poly colonist; see
  art-style.md): it loads the scene, finds its `AnimationPlayer`,
  and plays an idle or walk clip based on whether the player is moving (clips are matched by their
  leaf name containing "idle" or "walk"/"run"), walking while the sim's `Player.Heading` is not
  zero. It faces the movement direction by rotating its own
  wrapper node, which preserves the character scene's baked orientation and scale. If the scene is
  absent it falls back to a capsule. All model-specific wiring lives in the scene, so swapping the
  character is an asset change, not a code change.
- `CameraRig` is an orthographic camera holding the fixed 2.5D angle (looking at its target from
  the direction of `(14, 16, 14)`). It sits the offset's length plus the visible world height away
  from the target (`ViewDistance`): an orthographic view looks the same from any distance, but at a
  fixed distance the lower edge of a tall view would dip below the ground and clip it. The
  directional light's shadow range follows (`ViewDistance` plus the visible height), since shadows
  are drawn only up to a set distance from the camera. It smoothly follows the player and zooms with the mouse wheel. The zoom is the world
  units visible across 720 pixels of window height (default 28, clamped between 16 and 40); each
  frame the camera's orthographic size is the zoom scaled by the viewport's height, so pixels per
  world unit depend only on the zoom and resizing or maximizing the window shows more or less of
  the map at the same scale. Orientation is set once and never changed, so the angle stays locked
  while it pans.

- **Free camera.** The camera looks at a ground point (`GroundCenter`). While **attached** that
  point glides after the player. A drag over the world (right button, or left with Ctrl; a press
  over a panel, a modal, or the top bar never starts one) **detaches** it once the pointer has
  moved more than 4 pixels (less is a click, which walks): the ground point grabbed at the press stays under
  the pointer at any zoom, the character does not move, and the view's center is kept inside the
  explored area's bounding box (`Simulation.ExploredBounds`: the whole map on a bounded map
  without fog), so panning never shows ground beyond the fog's edge. The drag's motion and release
  are followed before the UI, so it keeps going over a panel. A detached camera stays put.
- **The destination ring** (`WalkMarker`): a thin accent ring on the ground at the walk's
  `Destination` while the player walks, fading out over 0.3 s once they stop.
- **Coming back.** Space brings a detached camera back (a click to walk leaves it where it is: the
  player is looking at where they clicked): if the character is on screen (inside the view and not under an
  open panel), it slides back over 0.25 s with an ease-out and then follows; if not, it jumps
  straight to them. Harvesting, building, and opening dialogs leave it where it is. Hover, clicks,
  the panels, and placement all work wherever the camera looks; harvesting still needs the
  character beside a field.

## Related

- simulation.md (intent and commands, the loop), terrain.md (walkability), buildings.md (paths),
  resources.md (harvest), presentation.md.
