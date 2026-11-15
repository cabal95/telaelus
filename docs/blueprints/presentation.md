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
  dirty, and every tile is rebuilt on the next frame; the sim's `CellsRevealed` rebuilds only the
  tiles holding the newly explored cells (and the tiles beside them whose fog border includes
  them); it processes while paused. A tile draws only its explored cells. In a world with fog,
  where a tile or its one-cell border has unexplored cells, it also gets a `FogOverlay`: a quad 0.2 above the ground
  over the whole tile, with a mask texture of one texel per map cell (plus the border) sampled
  with linear filtering through a small shader, in near-black (the UI's panel color). It is
  opaque over unexplored cells and fades out across the outer half of the explored cells beside
  them, for a soft edge; a solid plane of the same color 3 units below the ground covers the tile
  too, so where the blend thins around an unexplored cell's corner, darkness shows rather than the
  background. A fully explored tile has neither. `TileCount` and
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
  stage while its tile is built; fields in unexplored cells are left out),
  `BuildingViews` (footprint-sized building boxes, created and freed as buildings appear and are
  removed; `GameRoot` passes only the buildings on explored cells), `PlayerView` (the character), `CameraRig` (the orthographic follow camera, with
  `ZoomTo` for a direct size; attached it follows the player, a drag detaches it, and
  `ReturnToPlayer` slides or jumps it back; see player.md),
  `BuildController` (build-mode selection and rotation, the footprint ghost, the extractor
  work-area disc and field highlights, and the place command; it ignores buildings and fields in
  unexplored cells under the pointer), and the screen UI (below).

### Screen UI

The kinds of UI (modals, panels, overlays), their look, layers, and input rules are in
ui-style.md. There is no text HUD; the screen holds:

- **Top bar** (`TopBar`, always shown): the pinned resources (`ResourcePins`, presentation state
  for the session; the six starting deposits and Sand and Water at start, kept in table order),
  each as its icon and "amount / limit" (the amount alone for a Cargo or unlimited resource), in
  `Warning` when storage has no room for another unit, with name tooltips; a capacity resource
  (Sand, Water) shows demand over supply instead ("2 / 4", or "Blackout: 6 / 4"), in normal text,
  `Warning` in a brownout, or `Danger` in a blackout (`HudOverlay.StorageText` and `AmountColor`).
  Then the launch progress with "[L] Launch ship" once it can launch, preceded by the network's
  power ("Power 45 / 100", or "Blackout: Power 140 / 100", colored the same way). Pinning or unpinning rebuilds the entries.
- **Hint overlay** (`HintOverlay`): below the top bar, the E key's action with the resource's
  icon and a progress bar toward the next unit: "[E] Harvest Wood" while a hand-harvestable field is
  the target, "[E] Collect Iron Ore (12)" (units left in the output buffer) or "[E] Load Iron Ore
  (3 / 5)" (input buffer held and capacity) while a building is; while storage has no room for the
  resource, "Storage full" in `Warning` replaces the bar.
- **Debug overlay** (`DebugOverlay`, F3, hidden at start): the world line for exploring (for
  example `seed 1234   Highlands / Rock   chunk (1, -2)   cell (87, -101)`) and the performance
  line (see Performance below), in the top-left corner below the top bar, behind everything else.
- **Field detail panel** (`FieldDetail`, a panel): opened by clicking a resource field outside
  build mode (see resources.md). Its title is the resource's name ("Iron Ore"). Below it, a still
  180-unit picture of a full field of that deposit from `FieldPictures`, which draws one at
  startup per deposit (all five slots of a stand-in field with a fixed id and its biome's starting
  amount, placed by the map's own slot rules) and per tree family for Wood, so the picture shows
  the trees the field's biome grows; the biome only picks the picture. Then the status rows,
  refreshed every frame: Remaining ("212 / 250", the amount left and the starting amount, with a
  bar of the fraction left) and Worked by (the switched-on extractors working it now, as
  "2 Mines", or "nothing"). The UI shows no biome and no gather speeds. The panel closes itself
  when its field depletes. Its field keeps the hover outline while it is open.
- **Inventory** (`InventoryPanel`, a panel titled "Inventory"): I opens and closes it. First the
  Cargo pool, "Cargo (40 / 150)" (in `Warning` when full); then every resource, under tier headings
  in `TextDim` (Raw, then Tier 1 to Tier 4), each over a thin rule. Within a heading the
  discovered resources come first, then the undiscovered, each group in table order; rows move as
  resources are discovered. A discovered row is its icon, "Wood (45 / 100)" (the amount alone for
  a Cargo or unlimited resource, as "Iron (23)"; demand over supply for Sand and Water, as "Water
  (2 / 4)", colored by its state), including those at 0, in `Warning` when storage has no room for
  another unit, and a Pin toggle at the right ("Pin", or "Pinned" when on the top
  bar) that pins or unpins it; an undiscovered row is darkened like an undiscovered building in the build
  menu: its icon tinted `Silhouette` and "Undiscovered" in `TextDim`, with no name, amount, or
  pin. The
  body scrolls. Like every panel, opening it closes a building's detail panel and the other way
  round.
- **Build menu** (`BuildMenu`, a modal titled "Buildings"): B opens it (leaving build
  mode) and B, Escape, the Close button, or a click on the backdrop closes it. Its body is a grid
  of 112-unit square cells, as many columns as fit, scrolling when taller than the panel allows.
  It lists every buildable type (not the HQ). Discovered buildings (`IsUnlocked`) come first, then undiscovered ones, each group by name.
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
  through the cycle). The first row is Network ("Online" or "Offline"), then Switch ("On" or "Off",
  as Power was before it meant electricity), then one row per capacity type the building draws or
  supplies (Power, Water, Sand): "Draws 10", "Draws 10 (running at 80%)", or "Draws 10: blackout"
  for a consumer, "No Water: offline" offline; "Supplies 16", "Supplies 12.8 of 16" for a provider
  slowed by its own draws, or "Offline: supplies nothing". A recipe with no inputs reads "1 Glass,
  12 s". A building with a fuel slot has a Burning row ("Coal,
  22 s left; 4 in slot") and, below the rows, a Fuel picker (Best, then each fuel longest-burning
  first; choosing calls `SetFuelChoice`) with a dim note naming what it burns ("Best: burns Coal
  (40 s per unit)"). The progress bar shows only for extractors and processors. An extractor or
  processor a Buffer row ("Iron Ore 12 / 20", or "Iron 4 / 10; in: Iron Ore 3 / 5"); a hub Links
  (how many hubs it links to) and Reach ("covers 8, links 12") and no State or progress. A storage
  building shows no Power, State, or progress; instead a Silo shows
  "Holds" and a picker of the Bulk resources (choosing one calls `SetSiloResource`), and every
  storage building lists "Adds to storage": one line per resource it adds to, with the amount
  ("Wood   +300"), and "Cargo pool" for the HQ and a Warehouse, headed "Offline: adds nothing until
  connected" while it is offline. Then its actions: Turn off / Turn
  on (not for a storage building), and Destroy (not for the HQ), which shows its refund
  ("Destroy (refund 13 Wood, 8 Stone)") and asks once: the first click changes it to "Confirm
  destroy" for 3 seconds, and a second click in that time destroys the building. The panel closes
  itself when its building is gone. While it is open, its building keeps the hover outline in
  the world.
- **Network view** (`NetworkView`, in the world): each hub's coverage as a flat translucent disc
  (accent for an online hub, gray for an offline or switched-off one) and the links between online
  hubs as thin accent lines. It draws every hub while the network overlay is on (N toggles it; off at
  start) or while a building is being placed, adding the ghost's own coverage (green) and lines to
  the hubs it would link to when the ghost is a hub; with neither, it draws only the hub the detail
  panel shows, if it shows one. Separately, every switched-on offline building carries a small
  `Warning` ring floating 2.4 units above its center.
- **Placement tooltip:** while a building is selected (and no modal is open), a tooltip
  follows the pointer 28 units below and to the right of it, clear of the ghost: the building's
  name in the accent color, its cost (short amounts marked), fields in range for an extractor,
  "Online" or "Offline (no hub in reach)" in `Warning` for the spot (`WouldBeOnline`), for each
  capacity type the building draws or supplies, that and the network's figure with it added when
  the spot is online ("Draws 2 Sand: Sand 4 / 2", "Supplies 16 Water: Water 2 / 20", colored by the
  state that would give), and the dim hint "R rotate   Esc cancel". The ghost itself shows the facing (the model is
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
  gives the frame's CPU **work** time. `GameRoot` times the sim steps (**sim**). The **GPU** time is
  Godot's viewport measurement. On the Mobile renderer (and Forward+) it is taken with GPU
  timestamps, so it never includes the wait for the display; under vsync it reads higher than
  uncapped (on this machine about 3 ms against 1 ms) because the GPU clocks down when it has time
  to spare, which is still the time the frame took. The **headline** is the larger of the CPU and
  GPU times: how much of the budget the frame used, whichever side is busier. (On the Compatibility
  renderer, should Godot fall back to it, the GPU time is coarse and can include waiting, so the
  headline is the CPU time and the GPU time shows only uncapped; there the CPU also waits on the
  GPU inside the render step, so the CPU time already covers most of the frame.) `Renderer` names
  the renderer and driver. The monitor also records the wall time between frames, fps, draw calls,
  and objects drawn, and keeps one second of samples for an average and the worst frame.
- The debug overlay's performance line reads, for example,
  `frame 3.1 / 16.7 ms (18%)   cpu 0.9 (scripts 0.6, sim 0.2, render 0.2)   gpu 3.0   worst 4.5   fps 60   draws 58   Mobile D3D12`.
  The frame figure is the average headline over the last second, green under 50% of the budget,
  yellow to 80%, red above; then the CPU time with its parts, the GPU time, the worst headline in
  that second (one-off spikes such as a chunk generating), fps, draw calls, and the renderer. F3
  shows and hides the debug overlay, which starts hidden.
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
  | With fog of war (three runs) | 4.5 (runs 4.2 to 4.9) | 347 | 356 |

  The rows above ran on Compatibility at 1280x720, where the frame figure was the CPU time (which
  then included waiting on the GPU). On the Mobile renderer, `perf-walk` (two runs each):

  | Window | Capped | Headline (ms) | CPU (ms) | GPU (ms) | Wall (ms) | Draw calls |
  |---|---|---|---|---|---|---|
  | 1280x720 | uncapped | 1.2 | 0.7 | 0.9 | 1.8 | 71 |
  | 1280x720 | 60 fps | 3.2 to 4.1 | 1.1 | 3.0 to 3.9 | 16.7 | 71 |
  | 2304x1536 (fullscreen) | uncapped | 1.9 | 0.8 | 1.7 | 2.4 | 105 |
  | 2304x1536 (fullscreen) | 60 fps | 5.9 | 1.0 | 5.7 | 16.8 | 105 |

  Fullscreen at 60 fps uses about 35% of the budget (Compatibility used about 52% to 58% of it
  fullscreen); capped, the GPU clocks down and takes longer per frame than uncapped. The worst
  frame, about 130 ms, is still the first one. Mobile draws the same scene in fewer, larger draw
  calls. Compatibility, Forward+, and the measurements that led to Mobile are in
  `docs/changesets/future/graphics-settings.md`.

  With fog of war, only the cells the walk has seen are drawn, so the same walk draws about 40%
  fewer calls and is about 2 ms faster; its worst frame (the first one, which builds every tile at
  once) fell from about 170 ms to about 130 ms, and revealing as the player walks adds no
  noticeable spikes. Batching cut draw calls by 42% and objects by 28%, but the frame time barely moved (within the
  run-to-run noise): on this machine (Windows on ARM, the Compatibility renderer through a D3D12
  translation layer) the renderer's CPU time has a base of about 3 ms even for an empty map, the
  remaining draw calls are mostly terrain multimeshes (several per tile, and casters again in each
  shadow cascade), and the GPU time is about the same as the frame. An idle empty map takes about
  3.7 ms a frame, an idle generated world at the widest zoom about 7.2 ms.

### Rendering

- The project uses Godot 4.7 with C# and the **Mobile** renderer, on Direct3D 12 on Windows (the
  `driver.windows` project setting; Godot's defaults elsewhere: Metal on macOS, Vulkan on Linux).
  Where neither is available Godot falls back to the Compatibility (OpenGL) renderer
  (`fallback_to_opengl3`), which shades differently. Mobile suits the integrated, tile-based GPUs
  of laptops; a player setting for Forward+ (for the space mode's features) is parked in
  `docs/changesets/future/graphics-settings.md`. The Godot project root is `src/Game`;
  `Game.csproj` references the `Game.Sim` project (see simulation.md), whose `Game.Sim.dll` is built
  beside `Game.dll` and loaded by the engine.
- Lighting (see art-style.md): one `DirectionalLight3D` (the sun, rotated -50 and -40 degrees,
  energy 1, shadows on) and a `WorldEnvironment` with a clear-color background, ambient light off,
  and linear tone mapping; without one, the Mobile renderer would add ambient fill. Directional
  shadows use the hard filter (`soft_shadow_filter_quality` 0), so they stay crisp and dark. The
  first launch on a new machine compiles shaders once (one frame of about a second); Godot caches
  them.

### Testing code

- `src/Game/Testing/` holds the scenario runner, the live control channel, the command executor,
  the command parser, the state snapshot, and shared path helpers (see testing.md). It is inert
  unless `--scenario` or `--control-port` is given.

## Related

- simulation.md (the authoritative state), player.md, terrain.md, resources.md, buildings.md.
