# Simulation Blueprint

## Overview

The simulation is the authoritative game state and logic. It is free of Godot nodes and input
devices, and advances on a fixed-timestep clock. In single-player it runs in-process; it is the
future authoritative "server". The presentation layer (see presentation.md) reads simulation state
and mirrors it onto nodes.

## Current state

### Project and dependencies

- The simulation is the `Game.Sim` class library in `src/Game.Sim` (namespace `Game.Sim`). It
  references only the `GodotSharp` package, for Godot's math types (`Vector2`, `Vector3`,
  `Mathf`); that package version matches the Godot.NET.Sdk version of the game project.
- The game project (`src/Game/Game.csproj`) references `Game.Sim`; `Game.Sim` never references
  the game, so the compiler keeps the sim free of presentation code.
- The sim may use Godot math types only (`Vector2`, `Vector3`, `Mathf`), never engine objects.
  The unit test project checks this (see testing.md).

### Debug surface and map changes

- `Simulation.Debug` (a `DebugTools` instance) holds setup operations the normal game never
  performs: `ResetToEmptyMap` (all Grassland grass with no features, no fields, buildings,
  stockpile, or launches, player at the map center), `SetTerrain` (throws if the cell's feature
  cannot sit on the new terrain), `SetBiome`, `SetFeature` (a River needs a flow, other features
  take none; throws on terrain that cannot take a feature or on a field), `PlaceField` (amount
  optional, defaulting to the biome's; throws on a feature cell), `SetFieldAmount` (0 or less removes the field),
  `RemoveField`, `PlaceBuilding` (through the normal placement rules), `RemoveBuilding` (freeing
  its cells), `TeleportPlayer`, and `SeedStockpile`. Invalid requests throw
  `InvalidOperationException`. It is used by the unit tests, scenarios, and (later) the live
  control channel.
- The `MapChanged` event is raised by the debug operations that change terrain, biomes, features,
  or fields, so views
  built once at startup (the terrain) can rebuild.
- Field and building ids keep counting up across `ResetToEmptyMap`, so a view never mistakes a new
  field for an old one.

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

### The grid (two levels)

- A **fine cell** is 1 x 1 world unit. Buildings, collision, movement, and placement live on fine
  cells.
- A **map cell** is `FineCellsPerMapCell` x `FineCellsPerMapCell` (4 x 4) fine cells. Terrain,
  biomes, features (mountains and rivers), and resource fields live on map cells; a river's
  channel is the one rule that resolves to individual fine cells.
- Coordinates are signed integers. Fine cell `(x, z)` spans `[x, x+1) x [z, z+1)`;
  `CellCenter(x, z)` is `(x + 0.5, 0, z + 0.5)` and `CellAt` maps a world point to its fine cell
  by floor. `MapCellOf` converts a fine coordinate to its map coordinate by floor division, so
  negative coordinates map correctly; `MapCellCenter` gives a map cell's world center.
- The world is a **fixed test map** of `MapWidth` x `MapHeight` = 16 x 16 map cells (64 x 64 fine
  cells, `GridWidth` x `GridHeight`), occupying fine cells `[0, 63]` on each axis with the world
  origin at its corner. `InBounds` and `MapInBounds` test against it. Nothing else assumes a fixed
  size; procedural generation will replace this bound.
- The player spawns at the test map's center, `(32, 0, 32)`.
- The walk and build rules (`IsWalkable`, `IsBuildable`) are defined in terrain.md.

### Architecture posture (single-player now, multiplayer-ready)

- The simulation is authoritative and Godot-node-free, advanced on the fixed tick.
- Player actions are explicit intent and commands, the natural network message unit later.
- Planet and space are intended to be contexts within one shared session.
- Target networking model is server-authoritative state replication; we do not target deterministic
  lockstep, so no strict-determinism tax is paid.

## Related

- presentation.md (the loop driver and view sync), player.md, terrain.md, resources.md, buildings.md.
