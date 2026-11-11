# Sim Unit Tests

- Status: Implemented
- Id: 2611111128-sim-unit-tests
- Depends-on: none (requires 2611111121-sim-project-split, already implemented)

## Summary

Add an xUnit test project that tests the simulation without the engine, and make passing it a
gate before any changeset is stamped Implemented.

## Motivation

- Nothing checks today that a change didn't break earlier behavior. The sim is the authoritative
  game logic and is free of Godot nodes, so it can be tested quickly and thoroughly.
- Testing the sim outside the engine adds a runtime check to the seam: the project split already
  stops the sim from referencing presentation code, and the tests fail if sim code starts using
  engine objects.

## Design

- **Project:** `src/Game.Sim.Tests/Game.Sim.Tests.csproj`, targeting net8.0, using **xUnit**, with
  a `ProjectReference` to `../Game.Sim/Game.Sim.csproj`. Added to `game.slnx`. It sits beside the
  Godot project (`src/Game`), not inside it, so Godot never imports it.
- **Engine-free:** `Game.Sim` uses only Godot's math types (`Vector2`, `Vector3`, `Mathf`) from the
  `GodotSharp` package. These are plain managed structs and run without the engine, so the tests
  need no Godot install, window, or editor.
- **The seam rule, made checkable:** the sim may use Godot math types only (`Vector2`, `Vector3`,
  `Mathf`). Any engine object (anything derived from `GodotObject`, such as
  `RandomNumberGenerator`, `Node`, or `Resource`) needs the native engine. Running engine code
  outside the engine does not fail a test cleanly: it crashes the test host with an access
  violation and the whole run is reported as aborted, without saying what caused it. So a
  `SeamTests` class reads `Game.Sim.dll`'s metadata and fails a named test, listing the offending
  type, if the sim references any Godot type outside that allow-list.
- **Setup through the public API:** tests drive the sim the way the presentation does: set
  `Intent`, call commands, call `Step`. Where setup would mean playing out a long sequence, the sim
  may expose a clearly named `internal` test hook (for example, seeding the stockpile).
  `Game.Sim.csproj` declares `InternalsVisibleTo` for `Game.Sim.Tests`, so those hooks are visible
  to the tests but not to the game.
- **Initial coverage** (current behavior):
  - Grid: `MapCellOf` floor division, including negative coordinates; `CellAt`; `InBounds`.
  - Fields: `Stage` at full, mid, near-empty, and depleted; `DistanceToField` inside, at an edge,
    and at a corner; `FieldAt` covers all 16 fine cells of a field and nothing outside it.
  - Placement: footprint size by facing (a Refinery is 2 x 3 facing North, 3 x 2 facing East);
    `CanPlace` rejects water, mountain, fields, occupied cells, and out of bounds; placement
    occupies every footprint cell; an extractor with no field in range is placeable.
  - Movement: the player is blocked by water, fields, and buildings, and slides along them.
  - Harvest: within range of a field edge the player harvests, a depleted field is removed, and its
    cells become walkable.
  - Extractors: work the nearest matching field in range, move to the next when one depletes, idle
    with none, and ignore the other resource type.
  - Production: Smelter and Refinery recipes and cycle times; launch cost.
  - **End to end:** with the starting fields, a sequence of placements and steps reaches a ship
    launch.
- **Run:** `dotnet test src/Game.Sim.Tests` (a few seconds; no engine or window).

### Process changes

- A changeset's **Verification** section names the unit tests it adds or changes.
- New sim behavior gets unit tests in the same changeset.
- Before a changeset is stamped Implemented, `dotnet test src/Game.Sim.Tests` must pass.

## Code changes

- New `src/Game.Sim.Tests/`: `Game.Sim.Tests.csproj` (xUnit, `ProjectReference` to `Game.Sim`) and
  test classes per area (`GridTests`, `FieldTests`, `PlacementTests`, `MovementTests`,
  `HarvestTests`, `ExtractorTests`, `ProductionTests`, `LaunchTests`, `SeamTests`), plus
  `SimTestHelpers` (tick helpers and known coordinates on the fixed test map).
- `game.slnx`: add the test project.
- `Game.Sim.csproj`: `InternalsVisibleTo` for `Game.Sim.Tests`.
- `Simulation`: `internal` test hooks only where setup needs them (expected: seeding the
  stockpile).

## Blueprint updates

- New `docs/blueprints/testing.md`: the sim test project, its layout, the seam rule, how to run it,
  and the current test classes. Add it to `docs/blueprints/INDEX.md`.
- `simulation.md`: the sim may use Godot math types only, checked by the unit test project.

Process docs (not blueprints) also change in this commit: `docs/development.md` (Verification
expectations, the test gate before Implemented, and `testing` in the area list) and `CLAUDE.md`
(the test command, and that building `game.slnx` needs .NET SDK 9.0.200 or later).

## Verification

- `dotnet build game.slnx` and `dotnet test src/Game.Sim.Tests` pass; the headless run still has
  empty stderr.
- Breaking a rule on purpose fails a test: for example, temporarily changing the Refinery footprint
  makes the placement tests fail.
- Temporarily referencing `RandomNumberGenerator` from sim code fails `SeamTests` with a message
  naming `Godot.RandomNumberGenerator`. (Actually executing it, for example in the `Simulation`
  constructor, crashes the test host and aborts the run, which is why `SeamTests` exists.)
