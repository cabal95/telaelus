# Testing Blueprint

## Overview

Automated tests guard behavior that already works. Today that is the simulation, tested in a
plain xUnit project that runs without the engine. Passing it is a gate before any changeset is
stamped Implemented (see `docs/development.md`).

## Current state

### Sim unit tests

- **Project:** `src/Game.Sim.Tests/Game.Sim.Tests.csproj`, net8.0, xUnit, with a
  `ProjectReference` to `Game.Sim`. It is listed in `game.slnx` and sits beside the Godot project,
  so Godot never imports it.
- **Engine-free:** `Game.Sim` uses only Godot's math types from the `GodotSharp` package, which
  are plain managed structs, so the tests need no Godot install, window, or editor.
- **Run:** `dotnet test src/Game.Sim.Tests`. The suite takes well under a second to execute.

### The seam rule

- The sim may use Godot math types only: `Vector2`, `Vector3`, and `Mathf`. Any engine object
  (anything derived from `GodotObject`) needs the native engine.
- `SeamTests` reads `Game.Sim.dll`'s metadata and fails, naming the type, if the sim references
  any Godot type outside that allow-list. This matters because actually *running* engine code
  outside the engine does not fail a test cleanly: it crashes the test host with an access
  violation and the whole run is reported as aborted.

### Setup and test hooks

- Tests drive the sim the way the presentation does: set `Intent`, call commands, call `Step`.
  The player's `Position` is set directly to place the player.
- Where setup would mean playing out a long sequence, the sim exposes a clearly named `internal`
  test hook. `Game.Sim.csproj` declares `InternalsVisibleTo` for `Game.Sim.Tests`, so the hooks
  are visible to the tests but not to the game. Current hook: `TestSeedStockpile(type, amount)`.
- Tests use the fixed test map's known layout (pond, mountains, and the eight fields) for
  coordinates. `SimTestHelpers` holds tick helpers and a known clear grass spot near the spawn.

### Test classes

- `GridTests`: floor-division map cells (including negatives), `CellAt`, `CellCenter`,
  `InBounds`, terrain lookup, and the spawn point.
- `FieldTests`: stages, depletion, field bounds, distance to a field, `FieldAt` coverage,
  `FieldsInRange`, and that the starting fields hold enough for a launch.
- `PlacementTests`: footprints by facing, each `CanPlace` rejection (water, mountain, field,
  occupied, out of bounds, rotated footprint), extractors placeable with no field in range,
  occupancy, and derived size and center.
- `MovementTests`: speed, blocking by water, fields, and buildings, sliding, and map clamping.
- `HarvestTests`: edge-based harvest range, harvest rate, and field removal reopening cells.
- `ExtractorTests`: nearest field, moving on after depletion, idling, resource type filtering,
  and two extractors sharing a field.
- `ProductionTests`: Smelter and Refinery cycles, waiting for inputs, and recipe definitions.
- `LaunchTests`: launch cost and an end-to-end run from the starting fields to a launch.
- `SeamTests`: the seam rule above.

## Related

- simulation.md (the sim and its project), `docs/development.md` (the test gate).
