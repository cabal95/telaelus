# Presentation Blueprint

## Overview

The presentation renders the simulation and routes input to it. It owns the Godot scene tree and
reads simulation state each frame; it holds no game logic. Keeping this seam is what makes the
planned multiplayer feasible.

## Current state

### Entry point and the frame loop

- The main scene (`src/Game/scenes/Main.tscn`) has a single root node running `GameRoot`. `GameRoot`
  builds the world in code and owns the `Simulation` and `SimClock`.
- Each `_Process(delta)`: read input into `PlayerIntent` (camera-relative move, held `E` harvest),
  advance the clock and step the simulation, then sync the views from read-only simulation state.
  `_UnhandledInput` handles mouse-wheel zoom and the `L` launch command.
- `GameRoot` exposes the sim, camera, build controller, and field views read-only for the testing
  code, and after building the world attaches a `ScenarioRunner` when the command line has
  `--scenario`, or a `ControlChannel` when it has `--control-port` (debug builds only; see
  testing.md). Both run with `ProcessMode.Always` so they keep working while the scene tree is
  paused; every other game node pauses.

### Views

`GameRoot` creates and syncs these presentation nodes, each reading simulation state:

- `TerrainView` (the terrain in **render tiles** of 16 x 16 map cells: each frame it builds the
  missing tiles over the camera's view and the area within 48 units of the player, plus a one-tile
  margin, and frees tiles two tiles outside that range; in a generated world a tile is built only
  once every chunk it reads is generated, so drawing never generates. A tile is
  `TerrainTileView`: every cell's terrain placements grouped into one `MultiMeshInstance3D` per
  model, which Godot skips when the tile is off screen. The sim's `MapChanged` marks the view
  dirty, and every tile is rebuilt on the next frame; it processes while paused. `TileCount` and
  `ViewArea` report what is built and the ground area in range. Measured walking a generated
  world at the widest zoom: about 25 to 30 tiles built, about 330,000 triangles and 870 draw calls
  a frame (most draw calls are resource field models), terrain work under 0.2 ms a frame, and
  about 60 ms on the first frame, which builds every tile at once; tiles of 8 cells drew half the
  triangles with twice the tiles, and tiles of 32 cells drew twice the triangles and doubled the
  first frame),
  `ResourceFieldViews` (resource field models batched per render tile: for every terrain tile
  built, a `FieldTileView` draws the visible slots of the fields in that tile as one multimesh per
  model, casting shadows; a tile's batch is rebuilt when a field in it is added, removed, or
  changes stage, and freed with the terrain tile; it reports each field's visible model count, its
  stage while its tile is built),
  `BuildingViews` (footprint-sized building boxes, created and freed as buildings appear and are
  removed), `PlayerView` (the character), `CameraRig` (the orthographic follow camera, with
  `ZoomTo` for a direct size),
  `BuildController` (build-mode selection and rotation, the footprint ghost, the extractor
  work-area disc and field highlights, and the place command), and the screen UI (below).

### Screen UI

The kinds of UI (modals, panels, overlays), their look, layers, and input rules are in
ui-style.md. There is no text HUD; the screen holds:

- **Top bar** (`TopBar`, always shown): every raw resource's icon and amount, with name tooltips,
  and the launch progress with "[L] Launch ship" once it can launch.
- **Hint overlay** (`HintOverlay`): below the top bar, "[E] Harvest" with the resource's icon,
  name, and a progress bar toward the next unit, while a hand-harvestable field is in range.
- **Debug overlay** (`DebugOverlay`, F3, hidden at start): the world line for exploring (for
  example `seed 1234   Highlands / Rock   chunk (1, -2)   cell (87, -101)`) and the performance
  line (see Performance below), in the top-left corner below the top bar, behind everything else.
- **Inventory** (`InventoryPanel`, a panel titled "Inventory"): I opens and closes it. Every
  resource, including those at 0, as its icon and "Iron (23)": raw resources first (deposits, then
  terrain-sourced) in table order, a thin rule, then the refined ones. Like every panel, opening it
  closes a building's detail panel and the other way round.
- **Build menu** (`BuildMenu`, a modal titled "Buildings"): B opens it (leaving build
  mode) and B, Escape, the Close button, or a click on the backdrop closes it. Its body is a grid
  of 112-unit square cells, as many columns as fit, scrolling when taller than the panel allows.
  Discovered buildings (`IsUnlocked`) come first, then undiscovered ones, each group by name.
  A discovered cell shows the building's picture and name, in full color when affordable and in
  grayscale when not; an undiscovered cell shows its silhouette labeled "Undiscovered" in the dim
  text color. Hovering a cell outlines it; a discovered cell also shows its cost tooltip, while
  an undiscovered one shows none, so its cost stays a mystery. Clicking a discovered cell,
  affordable or not, closes the menu and selects the building for placement, so the player can
  position it while materials build up; clicking an undiscovered cell does nothing. The cells
  refresh while the menu is open, so a cell turns from gray to color as the stock arrives.
- **Building detail panel** (`BuildingDetail`, a panel): opened by clicking a placed building
  outside build mode, and switched to another building by clicking it. Its title is the
  building's name. Below it, a `TurntableView` shows the building turning about its center once
  every 18 seconds in a 180-unit square: a live view of the picture stage (ui-style.md), drawn
  only while visible. Then the status rows, refreshed every frame: Power (On or Off), State
  ("Working" or "Idle: " and the reason), In range (extractors: the fields in the work area
  counted by type, each followed by what those fields hold in all, as "Iron Ore 2 (500), Stone 1
  (250)", or "none"), Working on (only for a building that works
  more than one type: the current field's type and amount left, as "Stone (212 left)"), Recipe
  (processors: "1 Iron Ore to 1 Iron, 16 s"), and a progress bar (toward the next unit, or
  through the cycle). Then its actions: Turn off / Turn on, and Destroy, which shows its refund
  ("Destroy (refund 13 Wood, 8 Stone)") and asks once: the first click changes it to "Confirm
  destroy" for 3 seconds, and a second click in that time destroys the building. The panel closes
  itself when its building is gone. While it is open, its building keeps the hover outline in
  the world.
- **Placement tooltip:** while a building is selected (and no modal is open), a tooltip
  follows the pointer 28 units below and to the right of it, clear of the ghost: the building's
  name in the accent color, its cost (short amounts marked), fields in range for an extractor,
  and the dim hint "R rotate   Esc cancel". The ghost itself shows the facing (the model is
  turned) and whether the spot is open and affordable (green or red).

### Asset and model pipeline

- Models are glTF assets (generated by the asset factory, or CC0) under
  `src/Game/assets/models/<area>/`, following art-style.md.
- `ModelLibrary` loads them: every model is required, and a missing file (or one that is not a
  `Node3D` scene) throws with its path, so a missing asset fails at startup rather than rendering a
  placeholder. It caches loaded scenes, picks a map cell's variant and 90-degree yaw from a hash of
  its coordinates, turns North-authored pieces to an edge or facing, and can switch off shadow
  casting for flat ground pieces.
- `NodeModels` holds the rules for a field slot's model: the model family (a Wood slot's tree
  family follows the field's biome, read from the sim when its tile's batch is built), its
  variant and yaw (deterministic per slot), its scale to the deposit's target height from the mesh's bounds, capped to the slot's
  footprint, and its seat on the ground, as one transform for a multimesh instance.
  `ModelLibrary.MeshBounds(path)` gives a model's mesh bounds.
- `TerrainTiles` lists each map cell's terrain placements (model path, position, yaw, and whether
  it casts shadows), with `MountainModels` and `RiverPieces` choosing and turning those models (see
  terrain.md). `ModelLibrary.Mesh(path)` gives a model's mesh for a multimesh.
- `BuildingModels` maps a building type to its model and turns it to a facing; placed buildings
  and the build ghost both use it (see buildings.md).
- `MountainModels` picks a mountain's model family from the terrain beneath it and its variant and
  90-degree yaw from its map cell, and places the model unscaled (see terrain.md).
- The character is a hand-assembled Godot scene (`character.tscn`) that `PlayerView` loads. It is
  the one exception to required models: absent the scene, the player falls back to a capsule.

### Performance

- `PerfMonitor` measures each frame against a 16.7 ms budget (60 fps). Godot's own process time
  includes the wait for the next display refresh, so it reads about 16.7 ms under vsync at any
  load and cannot show headroom. Instead the game times its own work: `PerfFrameStart` processes
  first each frame and `PerfMonitor` last, and the time between them is every node's update (the
  simulation steps included), the **scripts** time. Adding the renderer's CPU time for the viewport
  gives the frame's **work** time, the headline number. `GameRoot` times the sim steps (**sim**). The
  GPU time is Godot's viewport measurement; on the Compatibility renderer it is coarse (about 2 ms
  steps) and under vsync includes the wait, so it is shown only when running uncapped. The monitor
  also records the wall time between frames, fps, draw calls, and objects drawn, and keeps one
  second of samples for an average and the worst frame.
- The debug overlay's performance line reads, for example,
  `frame 6.6 / 16.7 ms (40%)   scripts 0.7 (sim 0.1)  render 5.9  gpu (uncapped only)   worst 9.8   fps 60   draws 580`.
  The frame figure is the average work time over the last second, green under 50% of the budget,
  yellow to 80%, red above; worst is the slowest frame in that second (one-off spikes such as a
  chunk generating). F3 shows and hides the debug overlay, which starts hidden.
- `--uncapped` (a game argument after `--`) turns off vsync and the frame cap, so frames run as fast
  as they can; the wall time between frames is then the true cost of a frame, GPU included. With
  `--fixed-fps 30`, the game still advances one 30 Hz tick per frame, so a scenario plays out the
  same at any frame rate.
- The budget is a fixed 60 fps target, not the display's refresh rate: on a 120 Hz display, frames
  stay smooth at the display's rate only below 8.3 ms.
- **Benchmark** (`perf-walk`, seed 2 at the widest zoom, about 30 seconds walking across grassland
  and forest; windowed, `--uncapped`, five runs each), before and after batching field models:

  | | Frame (ms, median of 5) | Draw calls (avg) | Objects (avg) |
  |---|---|---|---|
  | One node per field model | 6.9 (runs 6.4 to 7.0) | 1,012 | 1,547 |
  | Field models batched per tile | 6.6 (runs 6.4 to 8.3) | 583 | 1,117 |

  Batching cut draw calls by 42% and objects by 28%, but the frame time barely moved (within the
  run-to-run noise): on this machine (Windows on ARM, the Compatibility renderer through a D3D12
  translation layer) the renderer's CPU time has a base of about 3 ms even for an empty map, the
  remaining draw calls are mostly terrain multimeshes (several per tile, and casters again in each
  shadow cascade), and the GPU time is about the same as the frame. An idle empty map takes about
  3.7 ms a frame, an idle generated world at the widest zoom about 7.2 ms.

### Rendering

- The project uses Godot 4.7 with C# and the **Compatibility (OpenGL)** renderer. The Godot project
  root is `src/Game`; `Game.csproj` references the `Game.Sim` project (see simulation.md), whose
  `Game.Sim.dll` is built beside `Game.dll` and loaded by the engine.

### Testing code

- `src/Game/Testing/` holds the scenario runner, the live control channel, the command executor,
  the command parser, the state snapshot, and shared path helpers (see testing.md). It is inert
  unless `--scenario` or `--control-port` is given.

## Related

- simulation.md (the authoritative state), player.md, terrain.md, resources.md, buildings.md.
