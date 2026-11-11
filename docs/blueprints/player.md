# Player Blueprint

## Overview

The player is a character the user drives around the planet surface to explore, harvest by hand, and
place buildings. This blueprint covers the player's simulation state, movement, input, and its
on-screen character and camera.

## Current state

### State and movement

- The simulation holds a `Player` with a world `Position` and a `Speed` (6 units per second). The
  player spawns at the test map's center, `(32, 0, 32)`.
- Movement is applied each tick from `PlayerIntent.Move` (a world-space ground direction). Each axis
  is moved independently and only if the target fine cell is walkable, so the player slides along
  obstacles rather than sticking. The result is clamped to the test map's extent.
- Walkability (see terrain.md) blocks water, mountain, resource fields (all their fine cells,
  until depleted), and buildings.

### Input

- `GameRoot` reads WASD and the arrow keys as screen-relative input and rotates it into world space
  using the camera's orientation, so "up" always moves away from the camera regardless of the fixed
  view angle. The result is written to `PlayerIntent.Move`.
- Holding `E` sets `PlayerIntent.Harvest`, which drives hand-harvesting (see resources.md).

### Character and camera (presentation)

- `PlayerView` renders the player from the assembled character scene
  (`src/assets/models/character/character.tscn`): it loads the scene, finds its `AnimationPlayer`,
  and plays an idle or walk clip based on whether the player is moving (clips are matched by their
  leaf name containing "idle" or "walk"/"run"). It faces the movement direction by rotating its own
  wrapper node, which preserves the character scene's baked orientation and scale. If the scene is
  absent it falls back to a capsule. All model-specific wiring lives in the scene, so swapping the
  character is an asset change, not a code change.
- `CameraRig` is an orthographic camera holding the fixed 2.5D angle (offset `(14, 16, 14)` from its
  target). It smoothly follows the player and zooms with the mouse wheel (size clamped between its
  min and max). Orientation is set once and never changed, so the angle stays locked while it pans.

## Related

- simulation.md (intent, the loop), terrain.md (walkability), resources.md (harvest), presentation.md.
