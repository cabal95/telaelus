# Scenario Runner and Integration Tests

- Status: Implemented
- Id: 2611111204-scenario-runner
- Depends-on: none (requires 2611111128-sim-unit-tests, already implemented)

## Summary

Add JSON **scenarios**: data files that drive the real game with setup commands, injected input,
waits, screenshots, and expectations against a state snapshot. A runner built into the game executes
one when given `--scenario=<file>`, writes screenshots and a JSON report to a per-run folder, and
exits with a pass or fail code. Permanent scenarios run as xUnit integration tests, one test per
scenario file.

## Motivation

- Some behavior can only be confirmed in the running game: input handling, the build ghost,
  overlays, staged models. During world-map-cells, R-to-rotate could only be checked by a person
  playing. A scenario that presses R, screenshots before and after, and checks the facing lets the
  implementer confirm it directly.
- Scenarios as data, not C#, keep test code out of the game assembly and make writing or changing
  one a file edit with no rebuild.
- Kept scenarios become regression tests that show up one per file in Test Explorer and in
  `dotnet test` output.
- The command executor is the foundation for a live control channel (changeset live-control),
  which reuses the same commands.

## Design

### Scenario files

- A scenario is a JSON file with a `name`, an optional `description`, and an ordered `steps`
  array. Each step is one command. A JSON schema, `scenario.schema.json`, defines the format; each
  scenario references it with `$schema` so editors validate it and an agent has an exact
  reference.
- Example (`build-rotate.json`):

  ```json
  {
    "$schema": "../scenario.schema.json",
    "name": "build-rotate",
    "description": "R rotates the Refinery ghost between 2x3 and 3x2.",
    "steps": [
      { "initMap": {} },
      { "press": "Key4" },
      { "aimAt": { "cell": [36, 30] } },
      { "wait": 2 },
      { "screenshot": "facing-north" },
      { "expect": "build.facing", "equals": "North" },
      { "expect": "build.ghost.width", "equals": 2 },
      { "press": "R" },
      { "wait": 2 },
      { "screenshot": "facing-east" },
      { "expect": "build.facing", "equals": "East" },
      { "expect": "build.ghost.width", "equals": 3 }
    ]
  }
  ```

### Commands

- **Map setup** (acts on the sim directly; the terrain and field views rebuild to match):
  - `initMap`: resets the world to an empty, all-grass map with no fields or buildings, an empty
    stockpile, and the player at the map center. A permanent scenario starts with `initMap` so it
    does not depend on the fixed test layout, which procedural generation will replace. When
    procedural generation lands, `initMap` swaps in a generator that produces only empty grass.
  - `changeTiles` `{ "type", "cells": [[x, z], ...] }` or `{ "type", "from": [x, z], "to": [x, z] }`
    (an inclusive rectangle): sets the terrain of map cells. Types are the current `TerrainType`
    names (Grass, Water, Mountain) and grow as terrain types are added.
  - `placeResource` `{ "type", "mapCell": [x, z], "amount"? }`: adds a field (default amount 250).
  - `setResourceAmount` `{ "mapCell": [x, z], "amount" }`: sets a field's remaining amount, so
    stages can be reached without harvesting them down. An amount of 0 removes the field.
  - `removeResource` `{ "mapCell": [x, z] }` and `removeBuilding` `{ "anchor": [x, z] }`.
- **Other setup:** `teleportPlayer` `{ "x", "z" }`, `seedStockpile` `{ "type", "amount" }`,
  `placeBuilding` `{ "type", "anchor": [x, z], "facing" }`.
- **View:** `cameraZoom` `{ "size" }`: sets the orthographic size, so a screenshot can frame what
  it needs.
- **Input:** `press` (key down, released on the following frame), `keyDown`, `keyUp`, `hold`
  `{ "key", "frames" }`,
  `aimAt` (`{ "cell": [x, z] }` for a fine cell's center, or `{ "world": [x, z] }`), and `click`
  (left button at the current aim, released on the following frame). Key names are Godot `Key`
  names (`R`, `Key4`, `Escape`). Input commands with no `wait` or `hold` between them land in the
  same frame, so simultaneous input can be tested.
- **Time:** `wait` (a number of frames).
- **Capture:** `screenshot` (a label; records a state snapshot, then saves the next drawn frame as
  a PNG, so it takes one frame), and `snapshot` (a label; records a state snapshot only).
- Any step may carry a `comment` string, which is ignored. Scenario files may contain JSON
  comments and trailing commas.
- **Check:** `expect` takes a snapshot path and one operator: `equals`, `notEquals`, `gte`, `lte`,
  `approx` (with `tolerance`), or `exists`. It reads a fresh snapshot when it runs.

### The state snapshot

- One JSON shape, produced by the presentation from sim and view state, is the contract for
  `expect`, for the report, and later for the live channel:
  - `player`: `position`, `harvestTarget` (field id or null).
  - `build`: `selected`, `facing`, `fieldsInRange`, and `ghost` (`anchor`, `width`, `depth`,
    `placeable`), or null when not in build mode.
  - `buildings`: `id`, `type`, `anchor`, `facing`, `width`, `depth`, `currentField`.
  - `fields`: `id`, `type`, `mapCell`, `amount`, `stage`, and `visibleModels` (how many models
    the field's view shows, so a test can check the view mirrors the stage without a screenshot).
  - `stockpile`, `shipsLaunched`, and `frame`.
- New checks are added by adding fields to the snapshot, not new expression syntax.

### Sim setup surface

- Setup commands need sim operations the normal game never calls (resetting the map, changing
  terrain, adding and removing fields and buildings, moving the player, seeding the stockpile).
  The `internal` test hooks from sim-unit-tests move to one public, clearly named surface,
  `Simulation.Debug`, used by unit tests, scenarios, and the live channel alike.
- The sim raises a `MapChanged` notification when terrain or fields change outside normal play,
  so `TerrainView`, which builds once at startup, rebuilds. `ResourceFieldViews` needs no rebuild:
  its per-frame sync already creates and frees field visuals by id, and field ids keep counting up
  across `initMap`, so a new field never reuses an old one's view. `BuildingViews` gains the same
  free-on-removal sync so `removeBuilding` and `initMap` clear building boxes.
- The `internal` hook and `InternalsVisibleTo` from sim-unit-tests are removed; nothing else needs
  them.

### Running a scenario

- Windowed, with screenshots (development):
  `<godot.editor> --path src/Game --fixed-fps 30 --resolution 1280x720 -- --scenario=<file>
  [--output=<dir>] [--no-screenshots]`
- Headless, state only (integration tests): `<godot.editor> --headless --path src/Game
  --fixed-fps 30 -- --scenario=<file> [--output=<dir>]`. No window opens and no GPU is needed.
- `--no-screenshots` turns each `screenshot` step into a `snapshot` step (state recorded, no PNG).
  Headless mode implies it, since there is no renderer to capture. A scenario promoted from
  development keeps its screenshot steps for manual runs without paying for them in the test
  suite.
- `GameRoot` attaches a `ScenarioRunner` node only when `--scenario` is present and only in debug
  builds; normal play is unaffected. The `--scenario` and `--output` paths may be absolute or
  relative to the **repo root**. (Relative to the working directory does not work: Godot changes
  its working directory to the project folder when given `--path`.)
- `--fixed-fps 30` matches the sim's 30 Hz tick, so every frame advances exactly one sim tick:
  frame counts in `wait` and `hold` are tick counts, and runs repeat exactly. It also guarantees
  that input held for a single frame (a `press`) overlaps a sim tick; at 60 fps the sim ticks only
  every other frame, so a one-frame press of a polled key (movement, E) could be missed. If the
  sim tick rate changes, this value changes with it. Fixed fps also runs as fast as the machine allows. (The first draft
  used 60 and wrongly described it as two ticks per frame; it is one tick every two frames, which
  is what exposed the missed-press risk.)
- **Key input:** keys go through `Input.ParseInputEvent`, which drives both `_UnhandledInput`
  handlers and `Input.IsPhysicalKeyPressed` polling, windowed or headless. Godot buffers injected
  events until its next input flush, so the executor calls `Input.FlushBufferedEvents()` right
  after injecting, and input always lands in a known frame.
- **Pointer input:** `BuildController` reads the pointer through a small pointer source: the real
  viewport mouse position in normal play, or a world ground point the runner sets
  (`PointerOverride`). Each frame, `BuildController` projects that world point to the screen with
  the camera and runs its normal screen-to-ground ray, so the ray code is still exercised. A world
  point rather than a fixed screen point matters because the camera follows the player with
  smoothing: after `initMap` or `teleportPlayer` the camera moves for many frames, and a fixed
  screen point would drift across the ground. `click` then acts at the aim. The OS cursor is never
  moved, and the same mechanism works windowed and headless.
- **Why an override, not injected mouse events** (from a probe run on Godot 4.7.2, Windows ARM64):
  - Headless: the root viewport is 64 x 64 (`--resolution` is ignored), but positions are floats,
    so projecting a world point to the screen and back recovered the exact point. An injected
    mouse-motion event did update the viewport mouse position, and an injected key was seen by
    `IsPhysicalKeyPressed`.
  - Windowed: the viewport had its real size and projection round-tripped exactly, but the viewport
    mouse position reported the real OS cursor and ignored the injected motion event. Windowed runs
    (manual scenarios with screenshots, and the live channel) could not aim by injection.

### Output

- Each run writes to its own folder: by default `test-output/runs/<yyMMdd-HHmmss>-<name>/` at the
  repo root (git-ignored), or the folder given by `--output`.
- Contents: `NN-<label>.png` per screenshot in step order, and `report.json` with the scenario
  name and file, each step with its outcome, each expectation's result (path, operator, expected,
  actual), the snapshot at each `screenshot` or `snapshot`, and the final result.
- **Exit codes:** 0 when every expectation passes; 1 when an expectation fails; 2 when the
  scenario is invalid (schema or unknown command) or a step throws. A run stops at its first
  failure but still writes its report and screenshots so far.

### Integration tests

- **Project:** `src/Game.Integration.Tests/`, xUnit. It launches Godot as a subprocess, so it
  stays separate from the fast, engine-free `Game.Sim.Tests`.
- **Scenarios live in** `src/Game.Integration.Tests/Scenarios/*.json`, with
  `scenario.schema.json` beside the folder.
- **One test per file:** a `[Theory]` whose `[MemberData]` lists the scenario files by relative
  path. Because each row is a plain string, xUnit enumerates the rows at discovery, so each file is
  its own item in Test Explorer and in `dotnet test` output, and the test count grows as files are
  added. Each test is named for its file.
- Each test runs the scenario **headless** with `--output` set to a run folder it chooses, waits
  with a timeout (60 s by default), and passes on exit code 0. No window opens during
  `dotnet test`. On failure, the message names the failed
  expectation from `report.json` and gives the run folder path.
- **Godot path:** read from `tools.json` at the repo root (found by walking up from the test
  directory), overridable with a `GODOT_EDITOR` environment variable. If neither exists, the tests
  fail with a message saying how to set it up.
- **Build order:** a `ProjectReference` to `Game.csproj` with `ReferenceOutputAssembly="false"`
  makes `dotnet test` build the game first without linking against it.
- **Run:** `dotnet test src/Game.Integration.Tests` for these alone, or `dotnet test game.slnx`
  for everything.

### What belongs in a permanent scenario

- Sim rules (stages, placement validity, extraction, recipes) are tested in `Game.Sim.Tests`,
  which is faster and more precise. A permanent scenario covers what only the running game can
  show: input reaching the right place, presentation state (the build ghost, facing, work-area
  counts), and views mirroring the sim. Screenshots in permanent scenarios are for people and
  agents running them by hand; the test suite skips them.

### Initial scenarios (permanent)

Each starts with `initMap` and builds only what it needs.

- `boot`: a smoke test that the main scene loads, the world builds, and the game runs a few frames
  without an error; expect the player at the map center.
- `build-rotate`: select the Refinery with its key, aim at grass, expect facing North and a 2 x 3
  ghost; press R, expect East and 3 x 2; press R three more times, expect North again. (Facing
  lives in `BuildController`, not the sim, so this is the only place it is tested.)
- `place-at-mouse`: select a Mine, aim at a cell, click; expect a Mine whose footprint is centered
  on the aimed cell. Then place an Ore field in range with `placeResource` and expect the ghost's
  `fieldsInRange` to count it. (Mouse-to-anchor mapping and the range count are presentation.)
- `harvest-input`: place a Wood field beside the player, hold E for some frames, expect the
  field's amount to drop and Wood in the stockpile. (Checks that the E key reaches the sim's
  harvest intent; harvest rules themselves are unit tested.)
- `field-stage-views`: place a field, set its amount to full, 120, 1, and 0 in turn, and expect
  `visibleModels` of 5, 3, 1, and the field gone. (Checks the view mirrors the stage.)

One-shot development checks, such as screenshotting the work-area disc or the rotated ghost to
judge how they look, are written as ad hoc scenarios or done over the live channel, and are not
kept unless they also check state.

### Agent workflow

- During implementation, write an ad hoc scenario (anywhere, for example under `test-output/`),
  run it with screenshots, and read the PNGs and `report.json` to judge both how it looks and
  what the state is. The live channel (live-control) serves the same purpose interactively.
- When the behavior should stay covered, add `expect` steps for its state, move the file into
  `Scenarios/`, and it becomes an integration test. Its screenshot steps can stay; the test suite
  runs it without them.

### Process changes

- A changeset's **Verification** section names the scenarios it adds or relies on.
- New input, rendering, or UI behavior gets a permanent scenario when it can be checked that way.
- Before a changeset is stamped Implemented, the sim tests must pass, and the integration tests
  must pass when the changeset touches presentation or input. Running all integration tests is
  recommended.

### Out of scope

- Golden-image comparison of screenshots. Pass or fail comes from `expect`; screenshots are for
  viewing.
- CI. Headless runs need no GPU or desktop session, so CI is possible later; setting it up
  (installing the Godot .NET build on a runner) is its own changeset.

## Code changes

- `Game.Sim`: `Simulation.Debug` (reset to an empty grass map, change terrain, add, remove, and
  set fields, place and remove buildings, teleport the player, seed the stockpile), replacing the
  `internal` hook from sim-unit-tests; update those tests to use it, add `DebugTests` for each
  operation, and remove `InternalsVisibleTo`. A `MapChanged` event raised by the map-changing
  operations. `Simulation` becomes `partial`, with the debug surface in `SimulationDebug.cs`.
- New `src/Game/Testing/`: scenario model and JSON loading (`System.Text.Json`), schema validation
  of command shapes, `CommandExecutor` (runs one command over as many frames as it needs; shared
  with live-control), `StateSnapshot` (builds the snapshot), `ScenarioRunner` (node: runs steps,
  writes output, quits with the exit code), and command-line parsing.
- `GameRoot`: parse `--scenario` and `--output`, attach the runner in debug builds, and expose the
  sim, camera, and build controller read-only to the testing code.
- `BuildController`: read the pointer through a pointer source the runner can override (normal
  play uses the viewport mouse position); expose the ghost's anchor, footprint, and placeability
  read-only for the snapshot.
- `TerrainView`: rebuild on `MapChanged`. `ResourceFieldViews`: expose each field's visible model
  count for the snapshot. `BuildingViews`: free boxes for removed buildings.
- `CameraRig`: `ZoomTo(size)`, which sets the size within the existing limits (`SetSize` is
  already a `Camera3D` method).
- New `src/Game.Integration.Tests/`: project, `ScenarioTests` (the theory), Godot path resolution,
  `scenario.schema.json`, and `Scenarios/` with the five initial files.
- `game.slnx`: add the integration test project. `.gitignore`: add `test-output/`.

## Blueprint updates

- `testing.md`: scenarios (format, commands, snapshot, running, output, exit codes), integration
  tests, and the current scenario list.
- `simulation.md`: `Simulation.Debug` and what it is for.
- `presentation.md`: `GameRoot` attaches the scenario runner when given `--scenario`; the testing
  code under `src/Game/Testing/`.

Process docs (not blueprints) also change in this commit: `docs/development.md` (scenario
expectations and the integration-test gate) and `CLAUDE.md` (the scenario and integration test
commands).

## Verification

- `dotnet test game.slnx` passes, showing one integration test per scenario file; adding a sixth
  file raises the count by one. Integration runs produce no PNGs.
- `dotnet test src/Game.Sim.Tests` passes, including `DebugTests`.
- Each initial scenario passes; run by hand with screenshots, `build-rotate` (from the example)
  shows the ghost rotated between its two PNGs.
- After `initMap` and `changeTiles`, a screenshot shows the new terrain, confirming the views
  rebuild.
- `dotnet test` runs the integration tests without opening a window.
- A scenario using `aimAt` gives the same result headless and windowed, and in a windowed run the
  real mouse position does not affect it.
- A scenario with a deliberately false `expect` exits 1, its test fails, and the message names the
  expectation; a scenario with an unknown command exits 2.
- Launching the game without `--scenario` behaves exactly as before.
