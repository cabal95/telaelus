# Testing Blueprint

## Overview

Automated tests guard behavior that already works, at two levels. **Sim unit tests** test the
simulation in a plain xUnit project that runs without the engine. **Scenarios** are JSON files
that drive the real game (setup, injected input, waits, screenshots, expectations); kept
scenarios run headless as **integration tests**, one test per file. Both gate a changeset before
it is stamped Implemented (see `docs/development.md`). The **live control channel** accepts the
same commands over local HTTP, so an agent can drive a running game interactively, including
pausing it and stepping frame by frame.

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

### Setup

- Tests drive the sim the way the presentation does: set `Intent`, call commands, call `Step`.
- Where setup would mean playing out a long sequence, tests use `Simulation.Debug` (see
  simulation.md), the same setup surface scenarios use.
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
- `DebugTests`: each `Simulation.Debug` operation and the `MapChanged` event.

### Scenarios

- A scenario is a JSON file: `name`, optional `description`, and an ordered `steps` array, one
  command per step. Any step may also carry a `comment` string, which is ignored. Comments and
  trailing commas in the file are tolerated. `src/Game.Integration.Tests/scenario.schema.json`
  defines the format; scenarios reference it with `$schema`.
- **Commands:**
  - Map setup: `initMap` `{}` (empty all-grass map, player at the center, and build mode reset:
    nothing selected, facing North, no aim override), `changeTiles`
    `{ type, cells: [[x, z], ...] }` or `{ type, from, to }` (map cells, inclusive),
    `placeResource` `{ type, mapCell, amount? }` (default 250), `setResourceAmount`
    `{ mapCell, amount }` (0 removes), `removeResource` `{ mapCell }`, `removeBuilding`
    `{ anchor }`.
  - Other setup: `placeBuilding` `{ type, anchor, facing? }` (normal placement rules),
    `teleportPlayer` `{ x, z }`, `seedStockpile` `{ type, amount }`, `cameraZoom` `{ size }`.
  - Input: `press` (key down now, up at the start of the next frame), `keyDown`, `keyUp`, `hold`
    `{ key, frames }` (down, wait, up), `aimAt` `{ cell: [x, z] }` (a fine cell's center) or
    `{ world: [x, z] }`, and `click` `{}` (left button at the aim, up the next frame). Keys are
    Godot `Key` names (`R`, `E`, `Key1`, `Escape`).
  - Time and capture: `wait` (frames), `screenshot` (label; records a snapshot and saves the next
    drawn frame as `NN-<label>.png`, taking one frame), `snapshot` (label; records state only).
  - Pause: `pause` `{}`, `resume` `{}`, and `step` `{ frames }` (see "Pause and frame stepping").
  - Check: `expect` with a snapshot path and exactly one of `equals`, `notEquals`, `gte`, `lte`,
    `approx` (optional `tolerance`, default 0.001), or `exists`. Paths use dots and indexes
    (`build.ghost.width`, `fields[0].visibleModels`, `player.position[0]`). Numbers compare by
    value. `expect` reads a fresh snapshot.
- Commands that take no frames run back to back in the same frame, so several inputs with no
  `wait` between them arrive together. `wait` and `screenshot` hold the next step until their
  frames pass.
- **The state snapshot** (`StateSnapshot`) is the contract for `expect`, the report, and the live
  channel: `frame` (game frames processed since the executor started; paused frames do not
  count); `paused`; `input` (`heldKeys`: keys the executor pressed that are still down); `player`
  (`position` [x, z], `harvestTarget` field id or null); `build` (null when not in build
  mode, else `selected`, `facing`, `fieldsInRange`, and `ghost`: `anchor`, `width`, `depth`,
  `placeable`, or null); `buildings` (`id`, `type`, `anchor`, `facing`, `width`, `depth`,
  `currentField`); `fields` (`id`, `type`, `mapCell`, `amount`, `stage`, `visibleModels`);
  `stockpile` (all four resources); and `shipsLaunched`. New checks are added as snapshot fields.
- **Input injection:** keys and mouse buttons go through `Input.ParseInputEvent` followed by
  `Input.FlushBufferedEvents()`, so they are delivered in the current frame (Godot otherwise
  buffers them until its next flush). Aiming uses `BuildController.PointerOverride`, a world
  point projected to the screen each frame; injected mouse motion is not used because a windowed
  game reports the real OS cursor instead.

### Pause and frame stepping

- `pause` uses Godot's scene-tree pause: game nodes stop processing (sim clock, camera, views,
  animation). The scenario runner and the control channel run with `ProcessMode.Always`, so they
  keep taking commands; rendering continues, so screenshots show the frozen frame.
- `step` `{ frames: n }` pauses first if running, then runs exactly n game frames and stays
  paused. At `--fixed-fps 30` each frame is one sim tick, so `step 1` is one tick.
- **While paused, input waits for `step`:** `press`, `keyDown`, `keyUp`, and `click` are queued,
  and the next `step` unpauses, injects the queue, flushes it, and runs its frames, so everything
  queued since the last step arrives together in that step's first frame (two keys, or a key and
  a click, in the same frame). Godot drops input flushed while the tree is paused, which is why
  the executor unpauses before injecting.
- A paused `press` or `click` delivers its down event in the next step and its up event in the
  step after, so it reads as held for exactly one frame; until that later step, the snapshot still
  lists the key as held. `wait` and `hold` run as steps while paused (a `hold`'s key-up waits for
  the next step). `resume` delivers any queued input and resumes normal running.

### Running a scenario

- Windowed, with screenshots: `<godot.editor> --path src/Game --fixed-fps 30 --resolution
  1280x720 -- --scenario=<file> [--output=<dir>] [--no-screenshots]`.
- Headless, state only: `<godot.editor> --headless --path src/Game --fixed-fps 30 --
  --scenario=<file> [--output=<dir>]`. Headless implies `--no-screenshots`, which turns each
  `screenshot` into a `snapshot`.
- Relative `--scenario` and `--output` paths resolve against the repo root, because Godot changes
  its working directory to the project folder when given `--path`.
- `--fixed-fps 30` matches the sim's 30 Hz tick, so every frame advances exactly one sim tick:
  frame counts in `wait` and `hold` are tick counts, and runs repeat exactly. It also guarantees
  that input held for a single frame (a `press`) overlaps a sim tick; at 60 fps the sim ticks only
  every other frame, so a one-frame press of a polled key (movement, E) could be missed. If the
  sim tick rate changes, this value changes with it.
- `GameRoot` attaches the `ScenarioRunner` only with `--scenario` and only in debug builds. The
  runner processes with `ProcessMode.Always`, so scenarios can use `pause` and `step`.
- **Output:** by default `test-output/runs/<yyMMdd-HHmmss>-<name>/` at the repo root
  (git-ignored), or `--output`. It holds the PNGs and `report.json`: scenario name and file,
  result, exit code, message, frame count, and one record per executed command (step index,
  command, frame, the step's JSON, outcome, message, and detail: expectation results with
  expected and actual values, or the snapshot for a capture). `hold` records as its three parts.
- **Exit codes:** 0 passed; 1 an expectation failed; 2 the scenario was invalid (unreadable file,
  bad JSON, unknown command, bad arguments) or a step threw. A run stops at its first failure and
  still writes its report. The last lines of stdout are `SCENARIO PASSED|FAILED|ERROR: ...` and
  `SCENARIO REPORT: <path>`.

### Integration tests

- **Project:** `src/Game.Integration.Tests/`, xUnit, separate from the engine-free sim tests. A
  `ProjectReference` to `Game.csproj` with `ReferenceOutputAssembly="false"` builds the game first.
- **One test per file:** `ScenarioTests.Scenario` is a `[Theory]` over the file names in
  `Scenarios/*.json`, so each file is its own test in Test Explorer and `dotnet test`.
- Each test runs Godot headless with `--fixed-fps 30`, an output folder under
  `test-output/integration/`, and a 60 s timeout. It passes on exit code 0; a failure message
  gives the exit code, the report's message, the run folder, and the end of stderr.
- **Godot path:** the `GODOT_EDITOR` environment variable, else `godot.editor` in `tools.json` at
  the repo root (found by walking up to `game.slnx`); with neither, the test fails with setup
  instructions.
- **Schema checks:** `ScenarioSchemaTests`, in the same project but without launching Godot,
  checks that `scenario.schema.json` has no duplicate keys (JSON lets a later key silently replace
  an earlier one) and validates every scenario file against it with JsonSchema.Net, one test per
  file. This keeps the `$schema` that editors use accurate.
- **Run:** `dotnet test src/Game.Integration.Tests`, or `dotnet test game.slnx` for everything.
- **What belongs here:** what only the running game can show (input reaching the right place,
  presentation state, views mirroring the sim). Sim rules belong in the unit tests. Screenshot
  steps in a kept scenario are for manual runs; the test suite skips them.
- **Current scenarios:** `boot` (the game starts; player at the center, nothing on the map),
  `build-rotate` (R rotates the Refinery ghost 2 x 3 to 3 x 2 and back), `place-at-mouse` (aim and
  click places a Mine at the expected anchor; the ghost counts an Ore field in range),
  `harvest-input` (holding E harvests), `field-stage-views` (visible models track the stage and
  vanish with the field), `pause-step` (paused input waits for `step`, and steps advance exact
  ticks), `place-on-player` (a building cannot be placed on the player; right beside them it can,
  and the player can still walk).

### Live control channel

- **Starting:** `<godot.editor> --path src/Game --fixed-fps 30 --resolution 1280x720 --
  --control-port=<n> [--start-paused] [--output=<dir>] [--no-screenshots]`. Debug builds only;
  ignored if `--scenario` is also given. `--start-paused` pauses once the world is built.
  Screenshots and the session log go to `test-output/runs/<yyMMdd-HHmmss>-live/` or `--output`.
- **Transport:** a `TcpListener` bound to `IPAddress.Loopback` (127.0.0.1 only; nothing else can
  connect) with a minimal HTTP/1.1 parser on a background thread. It needs no URL reservation or
  admin rights, unlike `HttpListener` on Windows. Each request is queued and run on the main
  thread by `ControlChannel` (a `ProcessMode.Always` node) with the shared `CommandExecutor`; one
  request runs at a time, in arrival order. Responses close the connection.
- **Endpoints:**
  - `GET /status`: `ready`, `paused`, `frame`, and the run folder `output`.
  - `GET /snapshot`: the current state snapshot.
  - `POST /commands`: one step object or an array of them, in the scenario format. The response
    comes when the last command finishes (so `wait`, `step`, and `hold` hold it) and has
    `passed` (every `expect` passed), `error` (null, or a runtime failure such as an invalid
    placement, which stops the rest of the request), `results` (per command: `index`,
    `command`, `input`, `outcome` ok/failed/error, `message`, `detail`; a screenshot's detail
    includes its absolute `path`), and the final `snapshot`. A failed `expect` is HTTP 200 with
    `passed: false`; the game keeps running.
  - `POST /quit`: responds, then exits the game.
  - An invalid request (bad JSON, unknown command, bad arguments) is HTTP 400 with `error`, and
    nothing in it runs. An unknown path or method is HTTP 404.
- **Session log:** every request and response is written to `session.json` in the run folder, so
  a session can be reviewed or trimmed into a scenario file.

### Agent workflow

- During implementation, write an ad hoc scenario (for example under `test-output/`), run it
  windowed, and read the PNGs and `report.json`. To keep it, add `expect` steps for its state and
  move it into `Scenarios/`.
- To explore interactively, start the game in the background with `--control-port`, poll
  `/status` until it answers, send commands with `curl -s localhost:<n>/commands -d '<json>'`,
  open the returned screenshot paths, and finish with `curl -s -X POST localhost:<n>/quit`. Use
  `--start-paused` and `step` to inspect changes frame by frame.

## Related

- simulation.md (the sim, `Simulation.Debug`), presentation.md (`GameRoot`, testing code),
  buildings.md (the pointer override), `docs/development.md` (the test gates).
