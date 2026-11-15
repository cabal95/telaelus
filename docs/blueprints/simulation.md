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
- Map cells are stored in **chunks** of 64 x 64 map cells, and a world is either **bounded** (the
  16 x 16 test map or empty map, occupying fine cells `[0, 63]` on each axis with the world origin
  at its corner; `MapWidth`, `MapHeight`, `GridWidth`, `GridHeight`, `InBounds`, and `MapInBounds`
  describe it) or **generated** (no edge; every cell in bounds). See world-generation.md.
- The player spawns at the bounded map's center, `(32, 0, 32)`, or at a generated world's spawn
  point.
- Each step first generates any missing chunk within one chunk of the player (a generated world
  only), then moves the player, reveals what the player can see (fog of war, below), harvests,
  and steps the buildings.
- **Fog of war (reveal once):** in a world with fog (`WorldSettings.Fog`: always for a generated
  world; the bounded maps only when asked), each map cell has an explored flag, stored in its
  chunk. When the player enters a map cell (and when the world is built), every map cell whose
  center lies within `SightRadius` (24 units, 6 map cells; one constant) of the player becomes
  explored, for good: nothing is ever hidden again, and only the player reveals. `IsExplored(mapX,
  mapZ)` reads it (out of bounds: false; in a world without fog every in-bounds cell is explored),
  `ExploredCount` counts the explored cells, `ExploredBounds` is the smallest rectangle of map
  cells holding them all (display only; the free camera stays inside it), and `CellsRevealed`
  reports each batch of newly explored cells. Explored is world state, shared by everyone. The one rule it changes is
  placement: a building's footprint must lie on explored cells (buildings.md). Exploring a cell
  also discovers the resources it shows (resources.md).
- **Discovery** (`IsDiscovered`) records which resources the player has found. Like explored
  cells it is world state, never clears during play, and resets with the world (resources.md).
- The walk and build rules (`IsWalkable`, `IsBuildable`) are defined in terrain.md.

### Architecture posture (single-player now, multiplayer-ready)

- The simulation is authoritative and Godot-node-free, advanced on the fixed tick.
- Player actions are explicit intent and commands, the natural network message unit later.
- Planet and space are intended to be contexts within one shared session.
- Target networking model is server-authoritative state replication; we do not target deterministic
  lockstep, so no strict-determinism tax is paid.

## Related

- presentation.md (the loop driver and view sync), player.md, terrain.md, resources.md, buildings.md.
