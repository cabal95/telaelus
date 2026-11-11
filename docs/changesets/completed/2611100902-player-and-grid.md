# Player and grid

- Status: Implemented
- Id: 2611100902-player-and-grid
- Depends-on: none

## Summary

Add the tile grid, a drivable player with camera-relative movement, and a camera that follows the
player with mouse-wheel zoom.

## Motivation

Turn the tech demo into a world you can move around in, and set up the grid that terrain, resources,
and buildings all sit on.

## Design

- A 32 x 32 grid, tile size 1, addressed by integer coordinates centered on the origin.
- A `Player` with position and a speed of 6 units per second, moved from `PlayerIntent.Move`.
- WASD and arrows are read as screen-relative input and rotated into world space via the camera, so
  "up" moves away from the camera at the fixed angle.
- The camera follows the player smoothly and zooms with the wheel.

## Code changes

- `Simulation`: grid fields (`GridWidth`, `GridHeight`, `TileSize`), `TileCenter`, `InBounds`,
  `MovePlayer` (with grid clamp), the `PlayerIntent` field.
- `src/Sim/Player.cs`, `src/Sim/PlayerIntent.cs`.
- `GameRoot`: `ReadWorldMoveInput` (camera-relative), a player visual (a capsule at this stage), and
  camera wiring. `CameraRig`: follow and zoom.

## Blueprint updates

- Establishes `player.md` (state, movement, input, camera); updates `simulation.md` (the grid).

## Verification

Drive the player with WASD/arrows; the camera follows; the wheel zooms.
