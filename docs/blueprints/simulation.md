# Simulation Blueprint

## Overview

The simulation is the authoritative game state and logic. It is free of Godot nodes and input
devices, and advances on a fixed-timestep clock. In single-player it runs in-process; it is the
future authoritative "server". The presentation layer (see presentation.md) reads simulation state
and mirrors it onto nodes.

## Current state

### Ownership and the loop

- `GameRoot` (presentation) owns a single `Simulation` and a `SimClock`. Each rendered frame it
  reads input into intent, advances the clock, steps the simulation zero or more times, then syncs
  the views.
- `SimClock` runs a fixed tick of **30 Hz**. It accumulates real frame time and releases it in
  fixed `1/30 s` steps, clamping the backlog to 8 steps so a stall cannot trigger a runaway
  catch-up.
- `Simulation.Step(dt)` runs in order: move the player, update the harvest target, harvest (if
  intended), then step buildings.

### Input: intent and commands

- Continuous, per-tick input is a `PlayerIntent` struct the presentation sets before each step:
  `Move` (a world-space ground direction, X to world X and Y to world Z) and `Harvest` (held).
- Discrete actions are command methods on the simulation that the presentation calls on an event:
  `TryPlaceBuilding`, `TryLaunchShip`. They validate and apply, returning success.
- The simulation never reads devices; it only reads intent and receives commands. This seam is what
  makes the planned co-op multiplayer feasible.

### The grid

- A `GridWidth` x `GridHeight` grid of `32` x `32` tiles, `TileSize` 1 world unit, centered on the
  origin. Tiles are addressed by integer coordinates; `TileCenter(x, z)` is `(x, 0, z)` and
  `InBounds` covers `[-16, 16]` on each axis.
- Terrain, resources, and buildings all live on this grid. Terrain-derived passability
  (`IsPassable`, `IsWalkable`) is defined in terrain.md.

### Architecture posture (single-player now, multiplayer-ready)

- The simulation is authoritative and Godot-node-free, advanced on the fixed tick.
- Player actions are explicit intent and commands, the natural network message unit later.
- Planet and space are intended to be contexts within one shared session.
- Target networking model is server-authoritative state replication; we do not target deterministic
  lockstep, so no strict-determinism tax is paid.

## Related

- presentation.md (the loop driver and view sync), player.md, terrain.md, resources.md, buildings.md.
