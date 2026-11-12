# Player Blueprint

## Overview

The player is a character the user drives around the planet surface to explore, harvest by hand, and
place buildings. This blueprint covers the player's simulation state, movement, input, and its
on-screen character and camera.

## Current state

### State and movement

- The simulation holds a `Player` with a world `Position` and a `Speed` (6 units per second). The
  player spawns at the bounded map's center, `(32, 0, 32)`, or at a generated world's spawn point
  (see world-generation.md).
- Movement is applied each tick from `PlayerIntent.Move` (a world-space ground direction). Each axis
  is moved independently and only if the target fine cell is walkable, so the player slides along
  obstacles rather than sticking. In a bounded world the result is clamped to its extent; a
  generated world has no edge.
- The speed each tick is `Speed` times the speed multiplier of the terrain under the player (Sand
  and Snow 0.75, Mud 0.6, others 1; see terrain.md). A river bank moves at its terrain's speed.
- Walkability (see terrain.md) blocks water, lava, mountains, river channels, resource fields (all
  their fine cells, until depleted), and buildings.

### Input

- `GameRoot` reads WASD and the arrow keys as screen-relative input and rotates it into world space
  using the camera's orientation, so "up" always moves away from the camera regardless of the fixed
  view angle. The result is written to `PlayerIntent.Move`.
- Holding `E` sets `PlayerIntent.Harvest`, which drives hand-harvesting (see resources.md).

### Character and camera (presentation)

- `PlayerView` renders the player from the assembled character scene
  (`src/Game/assets/models/character/character.tscn`): it loads the scene, finds its `AnimationPlayer`,
  and plays an idle or walk clip based on whether the player is moving (clips are matched by their
  leaf name containing "idle" or "walk"/"run"). It faces the movement direction by rotating its own
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

## Related

- simulation.md (intent, the loop), terrain.md (walkability), resources.md (harvest), presentation.md.
