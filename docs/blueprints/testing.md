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
- Tests use the fixed test map's known layout (pond, mountains, rivers, biomes, and the eight fields) for
  coordinates. `SimTestHelpers` holds tick helpers and a known clear grass spot near the spawn.

### Test classes

- `GridTests`: floor-division map cells (including negatives), `CellAt`, `CellCenter`,
  `InBounds`, terrain lookup, and the spawn point.
- `FieldTests`: stages, depletion, field bounds, distance to a field, `FieldAt` coverage,
  `FieldsInRange`, and that the starting fields hold enough for a launch.
- `TerrainModelTests`: every terrain and biome has a definition, walk and build rules per
  terrain, player speed per terrain, mountains blocking their map cell over Grass and Snow,
  biome field amounts and the default, and the test map's biomes.
- `RiverTests`: river connections and shapes (pool, end, straight, bend, junction, mouths into
  water and off the map, a flow edge facing dry land, parallel rivers staying separate), the
  channel blocking while banks keep their terrain on Grass, Sand, and Snow, the test map's river
  shapes, `SetFeature` rejections, and reset clearing biomes and features.
- `PlacementTests`: footprints by facing, each `CanPlace` rejection (water, mountain, field,
  occupied, out of bounds, rotated footprint), extractors placeable with no field in range,
  occupancy, and derived size and center.
- `MovementTests`: speed, blocking by water, fields, and buildings, sliding, and map clamping.
- `WorldGenTests`: the same seed gives identical chunks and generation order changes nothing; chunk
  biomes match the generator; rivers flow into rivers or water across chunk borders; the spawn is
  open Grassland at the center of chunk (0, 0) for seed 1; the spawn moves when the center has no
  exit (seed 8); the spawn search takes the first candidate with an exit or settles for the last;
  deposits sit on free walkable cells, apart, from their biome; the biome mix follows the
  planet's weights on average; chunks generate around the player and on demand; a generated world
  has no edge while bounded worlds keep theirs; resets switch the world kind.
- `ResourceTests`: the resource definitions, biome deposits, the Mine and Oil Well, and
  hand-harvest rules.
- `HarvestTests`: edge-based harvest range, one whole unit at the gather time, partial progress
  dropped on release or a new target, and field removal reopening cells.
- `ExtractorTests`: base speed, one unit per gather time over speed, nearest field, moving on
  after depletion, idling, resource type filtering, and two extractors sharing a field.
- `ProductionTests`: Smelter and Refinery cycles, waiting for inputs, and recipe definitions.
- `CostTests`: unlocking once the whole cost is held, by seeding and by hand gathering (kept
  when the stock runs out), placement
  deducting the cost, refusal with nothing deducted when locked, unaffordable, or blocked, free
  debug placement, and every building having a cost and every deposit a gather time.
- `LaunchTests`: launch cost and an end-to-end run from the starting fields to a launch.
- `SeamTests`: the seam rule above.
- `DebugTests`: each `Simulation.Debug` operation and the `MapChanged` event.

### Scenarios

- A scenario is a JSON file: `name`, optional `description`, and an ordered `steps` array, one
  command per step. Any step may also carry a `comment` string, which is ignored. Comments and
  trailing commas in the file are tolerated. `src/Game.Integration.Tests/scenario.schema.json`
  defines the format; scenarios reference it with `$schema`.
- **Commands:**
  - Map setup: `initMap` `{}` (the bounded empty map: all-Grassland grass with no features, player
    at the center) or `{ generate: <seed> }` (a generated world from that seed, player at its
    spawn), either way with build mode reset: nothing selected, facing North, no aim override; `changeTiles`
    `{ type?, biome?, feature?, flow?, cells: [[x, z], ...] }` or the same with `from, to` (map
    cells, inclusive; at least one of `type`, `biome`, `feature`; `flow` goes with a River and
    sets biome, then terrain, then feature),
    `placeResource` `{ type, mapCell, amount? }` (default: the biome's amount), `setResourceAmount`
    `{ mapCell, amount }` (0 removes), `removeResource` `{ mapCell }`, `removeBuilding`
    `{ anchor }`.
  - Other setup: `placeBuilding` `{ type, anchor, facing? }` (the spatial placement rule, free
    and whether or not unlocked), `teleportPlayer` `{ x, z }`, `seedStockpile` `{ type, amount }`
    (unlocks any building whose whole cost is then held; scenarios that place through the menu and a click seed first), `cameraZoom` `{ size }` (the camera's zoom: world units across 720 pixels
    of window height, so at the 1280 x 720 scenario resolution it is the visible world height).
  - Input: `press` (key down now, up at the start of the next frame), `keyDown`, `keyUp`, `hold`
    `{ key, frames }` (down, wait, up), `aimAt` `{ cell: [x, z] }` (a fine cell's center) or
    `{ world: [x, z] }`, `click` `{}` (left button at the aim, up the next frame), `clickUi`
    `{ id }` (moves the pointer over a registered UI control and clicks it, released the next
    frame), `hoverUi` `{ id }` (moves the pointer over one, so its hover state and tooltip
    show), and `scrollUi` `{ id, ticks }` (turns the wheel over one: positive ticks down,
    negative up). Keys are Godot `Key` names (`R`, `E`, `B`, `Escape`). UI ids are a dialog's id and a
    control's name: `buildMenu/<BuildingType>` for a cell, `buildingDetail/toggle` and
    `buildingDetail/destroy` for the detail panel's actions, `inventory/<ResourceType>` for an
    inventory row, `topBar/<ResourceType>` for a top bar entry (top bar ids work while no dialog is
    open), and `<dialog>/close` for the Close button. Buttons act on release, the frame after a `clickUi`, so check their effect after
    `wait` 2. UI pointer events go straight to the viewport (`PushInput`), so they reach the GUI in
    headless runs too.
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
  count); `paused`; `input` (`heldKeys`: keys the executor pressed that are still down); `world`
  (`kind`, `seed` or null, `chunks` generated, `tiles` of terrain built, `fieldBatches` built);
  `perf` (`workMs`, `scriptMs`, `renderCpuMs`, `gpuMs` or null, `simMs`, `frameMs` (wall time
  since the last frame), `worstMs`, `fps`, `draws`, `objects`, as of that frame; see
  presentation.md); `player` (`position`
  [x, z], `harvestTarget` field id or null, `harvestProgress` (0 to 1), the `terrain`, `biome`, and `feature` under the player,
  and their `chunk` [x, z]); `build` (null when not in build
  mode, else `selected`, `facing`, `fieldsInRange`, and `ghost`: `anchor`, `width`, `depth`,
  `placeable` (the spot), `affordable` (the cost), or null, and `tooltip`: the placement
  tooltip's lines, or null); `ui` (`open`: the open dialog's id or null, `debug`: whether the debug overlay shows,
  `topBar`: its `items` (each raw resource's `type` and `amount`) and its `launch` text, `hint`:
  the hint's `text` and `progress` or null, `tooltip`: the hover
  tooltip's lines or null, and while the build menu is open, its `cells` in display order, each
  `type`, `state` (`affordable`, `unaffordable`, or `undiscovered`), and `label`; while the
  detail panel is open, `building` (its id), `rows` (each shown status row, keyed in camel case:
  `power`, `state`, `inRange`, `workingOn`, `recipe`), `destroy` (the Destroy button's text), and
  `turnDegrees`; while the inventory is open, `inventory`: its rows in order, each `type`,
  `amount`, and `section` (`raw` or `other`)); `buildings` (`id`, `type`, `anchor`, `facing`, `width`, `depth`,
  `currentField`, `enabled`, `status` (`working`, `idle`, or `off`), and `idleReason` or null);
  `hoveredBuilding` (the id of the building under the pointer outside build mode, or null);
  `focusedBuilding` (the id of the building the detail panel shows and outlines, or null);
  `camera` (`zoom`: world units across 720 pixels);
  `fields` (`id`, `type`, `mapCell`, `amount`, `stage`, `visibleModels`);
  `stockpile` (every resource); `unlockedBuildings` (type names, in enum order); and
  `shipsLaunched`. New checks are added as snapshot fields.
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
  `screenshot` into a `snapshot`. A headless window is 64 x 64 and ignores `--resolution`, so
  the scenario runner sizes it to 1280 x 720, and the UI lays out and UI clicks land as they do
  windowed.
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
  result, exit code, message, frame count, a run-level `perf` summary, and one record per
  executed command (step index,
  command, frame, the step's JSON, outcome, message, and detail: expectation results with
  expected and actual values, or the snapshot for a capture). `hold` records as its three parts.
- **Performance summary:** the report's `perf` covers every frame of the run: `mode` (`windowed`
  or `headless`), `uncapped`, `frames`, `budgetMs`, the average and worst `workMs`, the average
  `scriptMs`, `renderCpuMs`, and `gpuMs`, the average and worst `simMs` and `frameMs`, and the
  average and peak `draws` and `objects`. Headless runs use a dummy renderer, so their timings and
  render counts are not measurements; measure windowed, with `--uncapped` for true frame times.
  Scenarios never assert on timings or draw counts, which vary by machine. To compare a change,
  run `perf-walk` several times before and after (`<godot.editor> --path src/Game --fixed-fps 30
  --resolution 1280x720 -- --uncapped --scenario=src/Game.Integration.Tests/Scenarios/perf-walk.json`)
  and compare the medians: run-to-run noise is about 10%.
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
  `hud` (the debug overlay starts hidden and F3 toggles it; the top bar shows all eight raw
  resources with name tooltips and the launch progress; the hint overlay shows the harvest hint
  and its bar; I opens and closes the inventory with raw resources first; the inventory and a
  building's detail replace each other; a panel covers the debug overlay),
  `building-detail` (clicking a building opens its panel with the right status rows; the
  player walks with it open; it turns the building off and on; clicking another building
  switches it; empty ground, B, and Escape close it; Destroy asks once, then refunds half the
  cost),
  `build-menu` (B opens the menu with discovered buildings first, affordable in color and short
  ones gray, then undiscovered ones; the modal menu stops the world; a hover shows the cost, none
  for an undiscovered cell, which ignores clicks; B, Escape, and Close close it; clicking an
  unaffordable discovered cell selects it with the placement tooltip),
  `build-rotate` (R rotates the Refinery ghost 2 x 3 to 3 x 2 and back), `place-at-mouse` (aim and
  click places a Mine at the expected anchor; the ghost counts an Iron Ore field in range),
  `harvest-input` (holding E fills the progress bar and harvests one Wood at 3 s, which does
  not yet unlock the Lumber Camp), `field-stage-views` (visible models track the stage and
  vanish with the field), `pause-step` (paused input waits for `step`, and steps advance exact
  ticks), `place-on-player` (a building cannot be placed on the player; right beside them it can,
  and the player can still walk), `terrain-types` (every terrain, a mountain on each ground that
  takes one, and rivers in every shape on several bank terrains render; the ghost is blocked on a
  river channel and allowed on its bank; the snapshot reports the terrain, biome, and feature under
  the player), `building-models` (every building model in every facing, the tinted ghost for each
  type, red over a blocked spot, and placement still works), `resource-fields` (every deposit's
  field model renders; Crude Oil cannot be hand-harvested; a Mine fills from the nearest mineral
  deposit and an Oil Well from its seep), `generated-world` (a generated world from a fixed seed
  starts the player on open Grassland in chunk (0, 0) with the 3 x 3 chunks around them generated
  and tiles drawn; crossing into chunk (1, 0) generates the chunks around it and the tiles
  follow), `field-batches` (every built terrain tile has a field batch, and batches follow the
  tiles into another chunk), `perf-walk` (the standing performance benchmark: seed 2 at the widest
  zoom, about 30 seconds of walking; in the suite it is a smoke test of a long walk across chunks).

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
