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

- **Project:** `src/Telaelus.Sim.Tests/Telaelus.Sim.Tests.csproj`, net8.0, xUnit, with a
  `ProjectReference` to `Telaelus.Sim`. It is listed in `Telaelus.slnx` and sits beside the Godot project,
  so Godot never imports it.
- **Engine-free:** `Telaelus.Sim` uses only Godot's math types from the `GodotSharp` package, which
  are plain managed structs, so the tests need no Godot install, window, or editor.
- **Run:** `dotnet test src/Telaelus.Sim.Tests`. The suite takes well under a second to execute.

### The seam rule

- The sim may use Godot math types only: `Vector2`, `Vector3`, and `Mathf`. Any engine object
  (anything derived from `GodotObject`) needs the native engine.
- `SeamTests` reads `Telaelus.Sim.dll`'s metadata and fails, naming the type, if the sim references
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
- `MovementTests`: walking at the player's speed and stopping on the point; round a building;
  a click on water ending on the nearest ground; no walk with no way out; paths never crossing the
  fog while the walk keeps going into it; walking round a building placed across the path;
  teleporting stopping a walk; and the map's edge.
- `SaltFlatTests`: on generated worlds, about one salt flat per 400 desert map cells (300 to 500
  over four seeds), each 2 to 6 cells across and most at least 3 x 3; every flat cell in the
  Desert, bordered only by salt flat or Sand, and in a 2 x 2 block of flat; no field, motherlode,
  or feature on a flat; the test map's flat at x 14 to 15, z 5 to 6.
- `WorldGenTests`: the same seed gives identical chunks and generation order changes nothing; chunk
  biomes match the generator; rivers flow into rivers or water across chunk borders; the spawn is
  open Grassland at the center of chunk (0, 0) for seed 1; the spawn moves when the center has no
  exit (seed 8); the spawn search takes the first candidate with an exit or settles for the last;
  deposits sit on free walkable cells, apart, from their biome; the biome mix follows the
  planet's weights on average; chunks generate around the player and on demand; a generated world
  has no edge while bounded worlds keep theirs; resets switch the world kind.
- `ResourceTests`: the resource definitions (63 by tier, raw resources at tier 0, the new
  deposits' gather times, an item model file for each, `items/<resource>.glb`), biome deposits, the Mine and Oil Well, and
  hand-harvest rules.
- `WorldGenTests` (added for the field rebalance): the starter set on ten seeds (two Wood and one
  Stone in the ring, on open cells, touching no other field, 1000 each); every newer deposit
  generating on some seed. `ExtractorTests`: a Mine working Limestone and a Drill Rig Crystals.
  `ResourceTests`: the new amounts and densities, and every deposit offered by some biome.
- `RenewablesTests`: a depleted Iron Ore field leaving a worked-out record and Wood none; a
  Forester only on an open Grass cell, its field unworkable while growing, cut by a Lumber Camp,
  regrowing 30 s after, stopping in a Water blackout, gone with the building; a Quarry only beside a
  mountain, Stone every 6 s, Limestone only in the Highlands, served by a truck; the Charcoal
  Kiln's recipe; the Rock Crusher supplying Sand while it has Stone; the deep buildings only exactly
  over a worked-out cell they work, at their rates; a Deep Mine on a Laterite's center (snapping,
  refused off-center or twice), yielding its chosen resource at its share's rate, collected from
  beside the motherlode by the player and a truck; one on walkable Swamp mud; a motherlode not
  worked by a Mine or by hand, and discovered with its yields. `WorldGenTests` adds motherlodes
  being rare, one a chunk at most, and far from the spawn.
- `PathTests`: a straight path on open ground; a path round a building ending beside its target;
  no path to a collector on an island; a path round slow ground when that is cheaper.
- `TruckBalanceTests`: Stone Mines about 40 units from the HQ hauled for ten minutes: one truck
  keeps up with three, falls behind four, and two nearly keep up with six, at least 90% since
  trucks unload on the HQ's pad (each run writes what it delivered).
- `PadTests`: a truck backing onto the HQ's pad (its bed to the entrance), unloading there, and
  driving off home; a second truck waiting in line in front of the pad, facing the HQ, and never
  two on the pad; a truck unloading beside the HQ when the pad's front is blocked.
- `TruckTests`: a new Depot's one truck and buying up to four; selling trucks for half a bought
  one's cost back (the free one for nothing) down to none, and buying again; selling a parked
  truck first, and a loaded one's load coming back; a truck hauling a Mine's
  buffer to the HQ and parking (the tests' map lays Relays and Small Hubs over its south-west, so
  their Mines are online); waiting for 10 units; dispatch taking the fullest, then one no
  other truck is on; loading one resource up to 20 (and parking when what is left is under a
  trip); loading only what storage has room for (no job for less than a trip unless the buffer is
  full); room counting the loads other trucks carry; unloading everything when the room was taken
  on the way; dropping a job when the collector is destroyed or emptied;
  finding a way round a building placed in its path; driving round its own Depot's shed, not through it; skipping a collector no truck can reach; a
  collector reachable only through fog unreachable until the fog lifts, and a path never entering an
  unexplored map cell; a
  switched-off Depot's truck finishing its trip and parking; an offline Depot's truck staying
  parked; a destroyed Depot refunding its bought
  truck and the load.
- `CapacityTests`: Water supply and demand over online, switched-on buildings only; the Refinery
  stopping without Water; a consumer of two types running at the slower (Water at half speed with
  Power plenty); a Pumping Station supplying at its power speed (12.8 at 80%) and nothing in a
  power blackout; Water providers only beside water by an edge (a pond or a river channel) and
  Sand providers only wholly on Sand; the Glassworks in a Sand blackout, waiting for fuel, then
  making Glass every 12 s on Coal; Sand and Water never stored.
- `PowerTests`: brownout speeds and a blackout past a quarter over, demand counting idle
  consumers; offline buildings neither drawing nor supplying; the Burner Generator burning whenever
  on, Best fuel first, stopping when switched off, its burning unit not refunded; a fuel choice
  burning only that fuel; the Smelter's cycle advancing only while fuel burns, and finishing in
  16 s on Wood, 13.9 s on Charcoal, 12.3 s on Coal; a Burner Generator supplying no more on Coal; a Drill Rig's speed
  2; the Water Wheel only across a Straight river cell, supplying 5 online.
- `NetworkTests`: hubs linking within the larger link radius, and a Relay linking a far hub 63
  units out; two Small Hubs at their full 22-unit link range covering the ground between them;
  coverage reaching a footprint's nearest point; switching off or destroying a hub cutting its branch; an extractor filling its
  buffer and waiting for pickup; collecting a whole two-type buffer by hand in one 0.5 s hold, leaving what storage has no room for, and stopping on
  full storage; an offline processor doing nothing and offering nothing to load; a processor
  going offline holding its cycle and finishing it once online; cancelling an offline cycle
  returning its inputs to storage; destroying
  returning buffers past the limit; storage counting only online; `WouldBeOnline`.
- `CollectorNetworkTests`: an offline Mine, Lumber Camp, and Oil Well making nothing and reading
  "Offline: outside the network"; a hub chain bringing a camp online, and cutting it dropping the
  unit in hand; an offline buffer still collected by hand; a truck never sent to an offline
  collector, and one on its way turning home when it goes offline; the HQ's and Small Hub's radii.
  `SimTestHelpers.Connect` chains Small Hubs from the HQ to a spot (`PlaceOnline` places a building
  and connects it, for tests about extraction rather than the network), `Burners` places a row of
  Burner Generators (along the empty map's north edge by default), `Gathered` counts the stockpile
  plus every output buffer, and `CollectByHand` empties a building by holding E on it.
- `StorageTests`: every resource's storage class and amounts; the HQ's base; limits summing the
  HQ and storage buildings; a Silo's step following its resource; Cargo room as the free pool over
  the weight; hand-harvest, extractors, and processors stopping at the limit and resuming; a
  finished cycle waiting for room; refunds passing the limit; destroying storage leaving stock
  over the limit; the HQ permanent, placed in every kind of world, and on clear ground with no
  field under it in generated worlds; every building cost but the Vault's fitting the HQ's base.
  `SimTestHelpers.Placed` lists the buildings other than the HQ and the hubs `Connect` placed, for
  tests that count them.
- `DiscoveryTests`: nothing discovered on an all-grass map; a field discovers its deposit when
  placed in an explored cell or when its cell is revealed; Sand, Water, and rivers discover from
  terrain; a debug edit to an explored cell discovers; holding a resource discovers it; discovery
  outlasts the field and resets with the world; the test map discovers its deposits and Water but
  no new deposit.
- `HarvestTests`: edge-based harvest range, one whole unit at the gather time, partial progress
  dropped on release or a new target, and field removal reopening cells.
- `ExtractorTests`: base speed, one unit per gather time over speed, nearest field, moving on
  after depletion, idling, resource type filtering, and two extractors sharing a field.
- `ProductionTests`: Smelter and Refinery cycles, waiting for inputs, and recipe definitions.
- `BuildingControlTests`: switching off (an extractor drops its unit in hand; a processor returns
  its cycle's inputs), status and idle reasons, destroying (half the cost back, rounded up, plus a
  running cycle's inputs; cells freed), `BuildingAt`, and `ExtractorsWorking` (the switched-on
  extractors on a field).
- `BuildingTableTests`: buildings.md's "At a glance" table matches one rebuilt from
  `BuildingDefs` and `Recipes` (the failure prints the table as it should read).
- `BuildingDescriptionTests`: every buildable building has a description under 80 characters
  with no long dash; the HQ has none.
- `CostTests`: unlocking once the whole cost is held, by seeding and by hand gathering (kept
  when the stock runs out), placement
  deducting the cost, refusal with nothing deducted when locked, unaffordable, or blocked, free
  debug placement, and every building having a cost and every deposit a gather time.
- `EventTests`: `Happened` raised once per unlock (a burst of unlocks one event each), per
  first discovery, per completed project, and per launch; a blackout raised once, and again only
  after the capacity was back to normal; nothing from a new world or a load.
- `LaunchTests`: the launch needing the whole ship bill and an online Shipyard, consuming the bill
  and counting the ship, and an end-to-end run on the empty map from placed fields through the
  refining chain (Refineries, Electrolyzers on each gas, Fuel Plants) to the launch's Rocket Fuel
  and Liquid Oxygen, hauling by hand.
- `PartsTests`: each parts building's recipes, the recipes making two units, a Copper Wire cycle
  putting two wires in storage, and an end-to-end run from seeded tier-1 materials through five
  Fabricators, two Electronics Works, a Tile Kiln, three Assembly Plants, and two Shipyards (on
  Burner Generators, with Warehouses for the parts) to a launch.
- `ResearchTests`: the fee taken once and refused short; Advanced Material Transfer locked until
  Material Transfer is complete, and never carrying Titanium Ore, Rare Earths, Uranium Ore, or
  Crystals; pausing keeping progress and the fee, resuming charging nothing;
  research speed at 0, 1, 2, 3, and 5 labs; one lab finishing Material Transfer in two game days;
  two labs at 1.5x; no lab working leaving research waiting; Material Transfer draining an online
  Mine but not an offline one nor a Drill Rig's Bauxite until Advanced Material Transfer;
  transfer stopping when storage is full.
- `RoadTests`: the road line's L on the tile grid; tile costs on Grass and Mud and laying them;
  a line skipping paved tiles and stopping when Stone runs out; roads refused on water, Lava, a
  mountain, a field, a river channel, and a building; a building refused over a road; the player at
  road speed over Mud; a truck path taking a road that saves time; a bridge over ShallowWater
  letting a path cross and refusing a building; bridges refused when not straight, along the water,
  over dry land, too long, or over DeepWater; a bridge across a straight river only at right angles
  and not over a bend; removing refunding half, and a bridge with the player on it staying.
- `SaveTests`: a save loaded into a fresh sim saving identically; a game saved at a tick and
  loaded continuing identically to the one that ran on; every kind of state (buildings mid-cycle,
  fuel, choices, trucks mid-trip, roads and bridges, research, the clock, Battery charge) surviving;
  a generated fog world round trip that keeps exploring the same; a switched-off extractor whose
  field was removed saving and loading with no current field, and a save naming a missing one
  loading with none; an older save's input buffer and fuel slot going to storage; an older save's
  2 x 2 Burner Generator growing to 4 x 4 around its center, or, with a building in the way,
  refunded in full and told on the next step; and the file
  format's round trip, a newer version refused, and a damaged file refused.
- `DemoTownTests`: the demo town (saving.md) built and run as the demo save is: every building
  online and working, power normal, four trucks, Material Transfer running; every collector on a
  field and the Smelters fed by truck; no truck waiting on storage and every Smelter working 15
  minutes later; and every building it has unlocked.
- `PowerStoreTests`: the clock starting mid-morning, advancing, and wrapping; daylight through the
  day; the Solar Array's supply by daylight and its night status; a Battery Bank charging at its
  rate up to its capacity, covering a shortage at Normal until it runs dry, giving at most its rate
  (a brownout past it), and off neither charging nor discharging; the Fission Reactor burning a
  Fuel Rod every 300 s for 400 Power and half that in a Water brownout; Fuel Rods only in the
  reactor and nothing else in it.
- `LaterTiersTests`: each building's recipe list (the new buildings and the added recipes), every
  refined resource having a maker, a Silicon Wafer cycle making two, the Brine Well's placement (on
  a salt flat, refused on Desert Sand and off the flat) and its Lithium into a Stockyard, the Air
  Separator's Liquid Oxygen with no Water, the Cement Kiln waiting for fuel and then burning it
  with no Power, the Concrete Mixer in a Sand blackout and then making Concrete, and an
  end-to-end run from seeded materials through two Semiconductor Fabs, an Electronics Works, an
  Assembly Plant, and two Enrichment Plants to a Computer and a Fuel Rod.
- `RefiningTests`: each building's recipes; the Steel Mill on Auto taking Coal, on Charcoal only
  Charcoal, waiting for it, and back on Auto; the next order's recipe waiting for the running
  cycle; the Electrolyzer's two gases and the Fuel Plant's Rocket
  Fuel; the Chemical Plant's and Electric Furnace's recipes; Aluminum Smelters' heavy draw.
- `CraftingQueueTests`: a Charcoal Kiln taking only the Wood its order needs, then idle "Queue
  empty"; a later order of the same recipe going after another's, one in a row growing the last;
  four orders at most; orders running in turn, the first waiting for its inputs; a Copper Wire
  order counted in whole cycles; cancelling the running order returning its inputs, and a later one
  leaving the running one alone; an idle Electric Furnace still drawing its 40 Power; an idle
  Smelter taking no new fuel while its burning unit burns out; the Electrolyzer, Brine Well, and
  Air Separator running without a queue and the Glassworks queueing; input choices kept per
  recipe; the queue and choices surviving a save; an older save loading with an empty queue.
- `HoldTests`: entering Hold emptying the queue and stopping a cycle of another recipe (its Ore
  back), or letting one of the held recipe run; a held Charcoal Kiln making up to its target,
  idle "Holding: 2 Charcoal" taking no more Wood, and making more once Charcoal is used; a cycle
  started under the target finishing past it; the target starting at the stock and at least one
  cycle (two for Copper Wire); leaving Hold finishing the running cycle with an empty queue; a hold
  surviving a save.
- Sim tests that need a processor working queue an order large enough for the test
  (`SimTestHelpers.Order`).
- `SeamTests`: the seam rule above.
- `DebugTests`: each `Simulation.Debug` operation and the `MapChanged` event.

### Scenarios

- A scenario is a JSON file: `name`, optional `description`, and an ordered `steps` array, one
  command per step. Any step may also carry a `comment` string, which is ignored. Comments and
  trailing commas in the file are tolerated. `src/Telaelus.Integration.Tests/scenario.schema.json`
  defines the format; scenarios reference it with `$schema`.
- **Commands:**
  - Map setup: `initMap` `{}` (the bounded empty map: all-Grassland grass with no features, player
    at the center, fully explored; `{ fog: true }` gives it fog of war) or `{ generate: <seed> }`
    (a generated world from that seed, player at its spawn, always with fog), either way with build mode reset: nothing selected, facing North, no aim override, and the camera jumped onto the player (`CameraRig.JumpTo`: attached, no slide or easing), and the pins started afresh as a new game's (`GameRoot.PinDiscovered`); `changeTiles`
    `{ type?, biome?, feature?, flow?, cells: [[x, z], ...] }` or the same with `from, to` (map
    cells, inclusive; at least one of `type`, `biome`, `feature`; `flow` goes with a River and
    sets biome, then terrain, then feature),
    `placeResource` `{ type, mapCell, amount? }` (default: the biome's amount), `setResourceAmount`
    `{ mapCell, amount }` (0 removes), `removeResource` `{ mapCell }`, `removeBuilding`
    `{ anchor }`.
  - Other setup: `placeBuilding` `{ type, anchor, facing? }` (the spatial placement rule, free
    and whether or not unlocked), `teleportPlayer` `{ x, z }` (the camera jumps onto the player too), `seedStockpile` `{ type, amount }`, `fillBuffer` `{ anchor, type, amount }` (sets one
    resource in the output buffer of the building at that anchor; 0 removes it), `setRecipe`
    `{ anchor, index }` (chooses the recipe of the running processor at that anchor, as its picker
    does; a queueing processor refuses it), `queueCraft` `{ anchor, recipe, count }` (queues an
    order at the processor at that anchor, as its Craft button does; fails when it does not queue
    or its queue is full),
    `newGame` `{ seed? }` (see above), `saveGame` `{ name }` (saves the game in play, as the save
    dialog does), `loadGame` `{ name }` (loads a save whatever the state; the next command runs
    once the loaded game is in play; fails when the save cannot be loaded), `loadDemo` (`{}`;
    loads the demo save, as the menu's Demo does), `setTimeOfDay` (a number from 0 up to 1, 0.5 midday), `placeRoad` and `placeBridge`
    `{ from: [x, z], to: [x, z] }` (lay a road line or bridge span free, as `placeBuilding` places;
    fail when nothing can be laid), `removeRoad` and `removeBridge` (the same, with the refund), `startResearch` (a project name; fails
    when it cannot start), `pauseResearch` (`{}`), `startTrainer` (`{}`, or `{ step }` 1-based: start or restart the trainer there), `askTrainer` (`{}`: open the first New Game's question, which test runs never ask on their own), `setResearchProgress` `{ project, seconds }` (at
    full work it completes),
    `placeMotherlode`
    `{ kind, mapCell }`, `setWorkedOut` `{ mapCell, deposit }`
    (unlocks any building whose whole cost is then held; scenarios that place through the menu and a click seed first), `cameraZoom` `{ size }` (the camera's zoom: world units across 720 pixels
    of window height, so at the 1280 x 720 scenario resolution it is the visible world height).
  - Input: `press` (key down now, up at the start of the next frame), `keyDown`, `keyUp`, `hold`
    `{ key, frames }` (down, wait, up), `aimDrag` `{ from: [x, z], to: [x, z], button?: left|right,
    shift? }` (world points: aims at `from` and presses now, aims at `to`, releases at the start
    of the next frame; fails if either end is outside the window; drives the line tools),
    `aimAt` `{ cell: [x, z] }` (a fine cell's center) or
    `{ world: [x, z] }`, `click` `{ button?: left|right, ctrl? }` (that button, left unless given, at the aim, up the
    next frame; a right click or Ctrl + left on the world walks; it fails, "click at
    (x, y) is outside the window", when the aim projects outside the window, since a player cannot
    click there), `drag`
    `{ button: right|left, ctrl?, from: [x, y], to: [x, y], frames? }` (screen pixels: pressed at
    `from` now, moved in even steps over `frames` (default 5), released the frame after; the
    scenario waits until the release; pushed through the viewport like real input), `clickUi`
    `{ id }` (moves the pointer over a registered UI control and clicks it, released the next
    frame), `hoverUi` `{ id }` (moves the pointer over one, so its hover state and tooltip
    show), `selectUi` `{ id, item }` (picks a picker's item by its text: a dropdown's, as choosing
    it from the list does, or a segmented picker's in either of its forms, as clicking its cell
    does), `holdUi` `{ id, frames }` (moves the pointer over one and holds the left button down
    for the frames, released on the last; the scenario waits for the release; a control clipped by
    a scroll container must be hovered into view first), and `scrollUi` `{ id, ticks }` (turns the wheel over one: positive ticks down,
    negative up). When a scroll container clips the control, `clickUi` and `hoverUi` first scroll
    it into view, as a player would, and act a frame later, once the layout has settled. Keys are Godot `Key` names (`R`, `E`, `B`, `Escape`). UI ids are a dialog's id and a
    control's name: `buildMenu/<BuildingType>` for a cell (`buildMenu/Road` and `buildMenu/Bridge` for the line tools), `buildingDetail/toggle` (the on/off icon
    toggle) and `buildingDetail/destroy` for the detail panel's actions, `buildingDetail/siloResource` for the
    Silo picker, `buildingDetail/mode`, `buildingDetail/input`, and `buildingDetail/fuel` for its
    segmented pickers (for `selectUi`) and `buildingDetail/mode/Hold` and the like for a picker's
    cell (for `clickUi`), `inventory/<ResourceType>` for an inventory row, `inventory/pin/<ResourceType>`
    for a row's Pin toggle (shown only on discovered rows, and only clickable while the row is
    scrolled into view), `trainer` and `trainer/close` for the step card and its X, `choice/yes` and `choice/no` for the choice dialog's answers, `toast/<n>` for a notification (0 is the newest) and `toast/<n>/close` for its X, `topBar/<ResourceType>` for a top bar entry (top bar ids work while no dialog is
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
  (`kind`, `seed` or null, `chunks` generated, `tiles` of terrain built, `fieldBatches` built,
  `fog` whether the world has fog of war, `explored` the count of explored map cells or null
  without fog);
  `perf` (`headlineMs` (the larger of the CPU and GPU times), `workMs`, `scriptMs`, `renderCpuMs`, `gpuMs` or null, `simMs`, `frameMs` (wall time
  since the last frame), `worstMs`, `fps`, `draws`, `objects`, as of that frame; see
  presentation.md); `player` (`position`
  [x, z], `walking`, `destination` [x, z] (where the walk ends) or null, `harvestTarget` field id or null, `harvestProgress` (0 to 1), `interactTarget` building
  id or null, `interactProgress`, the `terrain`, `biome`, and `feature` under the player,
  and their `chunk` [x, z]); `build` (null when not in build
  mode, else `selected`, `facing`, `fieldsInRange`, and `ghost`: `anchor`, `width`, `depth`,
  `placeable` (the spot), `affordable` (the cost), or null, and `tooltip`: the placement
  tooltip's lines, or null; with a line tool instead, `line` (`Road` or `Bridge`), `drag`
  (`from`, `to`, `removes`) or null, and `tooltip`); `ui` (`open`: the open dialog's id or null, `width`: its width in UI units or null, `debug`: whether the debug overlay shows,
  `network`: `overlay` (whether N has it on), `shownHubs` (the ids of the hubs whose coverage is
  drawn), and `offlineMarkers` (how many offline markers are drawn),
  `topBar`: its `items` (each pinned resource's `type`, `amount`, `limit` (null for a Cargo or
  unlimited resource), `full`, the `text` shown (the amount), and its `tone`: `normal`,
  `danger` when full, or `dim` with no storage), its `power`, `water`, and `sand` readouts ("45 / 100", "Blackout: 140 / 100", or null
  while hidden) and their `powerTone`, `waterTone`, and `sandTone`, its `clock` ("14:00"), and its
  `store` (the Battery Banks' readout, or null with none), `hint`: the hint's `text`, `progress`,
  and `storageFull` or null, `toasts`: the notifications shown, newest first, each `style`
  (`brief` or `lasting`), `title`, `detail` (or null), `remaining` seconds (one decimal), and
  `shown` (false while the stack is hidden under a modal),
  `trainer`: the trainer's `step` (1-based) and `total`, its card's `header`, `text`, and `hint`,
  `done` (the card reads Done), and the pointer ring's `pointer` [x, z] or null; or null with the
  trainer off, `choice`: the choice dialog's question while it is open, or null, `tooltip`: the hover
  tooltip's lines or null, `worldTip`: the truck tooltip's lines outside build mode, or null,
  `playTime` (game seconds played) and `unsaved` (unsaved progress), while the game menu is open
  its `buttons`, `confirm` (the question shown, or null), and `status`, while the save dialog is
  open its `name` and `confirm`, while the load dialog is open its `saves` (each `name` and
  `loadable`) and `confirm`, and while the build menu is open, its `cells` in display order (`Road` and `Bridge`, then the buildings), each
  `type`, `state` (`affordable`, `unaffordable`, or `undiscovered`), and `label`; while the
  research tree is open, its `header`, its `cards` (each `project`, `running`, `progress` 0 to 1,
  `selected`, and `locked`), and its `drawer` (the selected `project`, its `state` as shown,
  `button`, `enabled`, `fee`, `time`, and `progress` 0 to 1); while the
  detail panel is open, `building` (its id), `strip` (the status strip in order, each entry's
  `id` (`switch` for the on/off toggle, then `network`, `power`, `water`, `sand`, `charge`,
  `burning`, `truck`), `value` as shown ("-10", "+35", "53%", "22 s", or null), `tone`
  (`normal`, `dim`, `good`, `warning`, or `danger`), and `tip`, its tooltip), `state` (the state
  line, "Working" or "Idle: " and the reason, or null) and `stateTone`, `rows` (each shown status
  row, keyed in camel case: `research`, `site`, `plot`, `eats`, `inRange`, `workingOn`, `recipe`,
  `buffer`, `links`, `reach`), `trucks` (a Depot's bays in order, each `state`, its truck's state
  in camel case or `empty`, and `tip`, as "Truck 2: Parked" or "Empty bay"; null for other
  buildings),
  `fuel` (the fuel picker's choice, or null), `output` (the Output picker's choice as shown, or
  null), `recipe` (the Recipe picker's choice, or null), `recipeNeeds` (the line under it: its
  `text` and the `short` inputs' names, or null), `input` (the input picker's `choice` and
  `note`, or null), `order` (a queueing processor's `quantity` as shown and `canCraft`, or null; null in Hold),
  `mode` (its Mode picker, "Queue" or "Hold", or null), `hold` (in Hold, the `target` as shown and the
  storage `line`, or null),
  `queue` (its queue rows as shown, "Iron x 5 (crafting)" or "Queue empty", or null), `buyTruck` and `sellTruck` (a Depot's buttons: `text`, `enabled`, and `tip`, its tooltip, or null), `launch` (a
  Shipyard's Ship launch section: its `bill` ("have / need" per entry), the `button` text,
  `enabled`, the `note` saying why not or null, and `ships` ("Ships launched: n") or null; null for
  other buildings), `adds` (what a storage building adds, by
  resource name, as "+300"), `destroy` (the Destroy button's text, or null when hidden), `destroyTip`
  (its tooltip, "Refund: 13 Wood, 8 Stone", or null when hidden), and
  `turnDegrees`; while the field detail panel is open, `field` (its id), `title`, and `rows`
  (`remaining`, `workedBy`); while the inventory is open, `inventory`: its rows in display order,
  each `type`, `amount`, `limit` (null for a Cargo or unlimited resource), `section` (`raw`,
  `tier1` to `tier4`), `discovered`, and `pinned`, and `cargo`, the Cargo row's text); `buildings` (`id`, `type`, `anchor`, `facing`, `width`, `depth`,
  `currentField`, `enabled`, `online`, and `output` (amounts by type),
  `burning` (the fuel burning, or null), `status`
  (`working`, `idle`, or `off`), and `idleReason` or null);
  `trucks` (`id`, `depot`, `bay`, `state` (`parked`, `toCollector`, `loading`, `toHq`, `unloading`,
  `waitingForRoom` (older saves only), `toDepot`), `position` [x, z], `cargo` or null, `amount`, and `target` or null);
  `workedOut` (each worked-out map cell's `mapCell` and `deposit`); each building also has
  `producing` (a collector without a field's output, or null), `recipe` (the name of the
  recipe a processor runs, or null), `queue` (a queueing processor's orders, each `recipe` and
  `remaining`, or null for any other building), `hold` (a held processor's `recipe` and `target`, or
  null), `display` (the item its display stand shows, or null), and
  `charge` (a Battery Bank's stored Power, or null);
  `hoveredBuilding` (the id of the building under the pointer outside build mode, or null);
  `focusedBuilding` (the id of the building the detail panel shows and outlines, or null);
  `hoveredField` and `focusedField` (the same for resource fields);
  `camera` (`zoom`: world units across 720 pixels, `target` [x, z]: the ground point at the view's
  center, `detached`: whether a drag has moved it off the player);
  `fields` (`id`, `type`, `mapCell`, `amount`, `stage`, `visibleModels`, `explored`, `growing`,
  `motherlode` or null);
  `app` (`loading`, `menu`, or `game`; before a game the snapshot is only `app`, `loading` (the
  progress, 0 to 1), and `ui` with `open` (`mainMenu` on the menu) and `menu` (`title`, `buttons`:
  the shown, enabled ones, and `saves`: the main menu's save list (each `name` and `loadable`)
  while it shows, else null));
  `stockpile` (every resource); `capacity` (`power`, `water`, and `sand`, each with `supply`,
  `demand`, `speed`, and `state`: `normal`, `brownout`, or `blackout`, and once there is a Battery
  Bank, `stored`, `storeCapacity`, and `storeFlow`); `timeOfDay` and `daylight`; `research` (its
  `active` project or null, `speed`, and `projects`, each camel-cased with `state`, `progress`, and
  `paid`); `roads` (each road tile's corner cell, by row); `bridges` (each span's `tiles` and
  `alongX`); `discovered` (the discovered resources' type names, in enum
  order); `unlockedBuildings` (type names, in enum order); and
  `shipsLaunched`. New checks are added as snapshot fields.
- **Input injection:** keys and mouse buttons go through `Input.ParseInputEvent` followed by
  `Input.FlushBufferedEvents()`, so they are delivered in the current frame (Godot otherwise
  buffers them until its next flush). Every injected event's C# wrapper is disposed once it is
  delivered (UI pushes too): Godot holds its own reference while it needs one, and a wrapper left
  to the garbage collector could be finalized while the engine shuts down, racing Godot's own
  cleanup of the same object and crashing the process. Aiming uses `BuildController.PointerOverride`, a world
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

- Windowed, with screenshots: `<godot.editor> --path src/Telaelus --fixed-fps 30 --resolution
  1280x720 -- --scenario=<file> [--output=<dir>] [--no-screenshots]`.
- Headless, state only: `<godot.editor> --headless --path src/Telaelus --fixed-fps 30 --
  --scenario=<file> [--output=<dir>]`. Headless implies `--no-screenshots`, which turns each
  `screenshot` into a `snapshot`. A headless window is 64 x 64 and ignores `--resolution`, so
  the scenario runner sizes it to 1280 x 720, and the UI lays out and UI clicks land as they do
  windowed.
- **A scenario starts at the main menu.** Its first command runs once the loading screen is done
  (the models loaded and the pictures drawn, so screenshots show them). `newGame` `{ seed? }` goes
  into a new game whatever the current state, from the menu or replacing the game in play (the seed
  when given, else the scenario seed, 1); the next command runs a frame later. Every game scenario
  begins with it; commands that need a game fail with "no game yet: start one with newGame" before
  one. `wait`, `pause`, `step`, `expect`, `snapshot`, and `screenshot` work on the menu too. Clicks
  on the main menu's buttons come later.
- **A run's saves are its own:** the executor points `SaveStore.Folder` at `<output>/saves`, so a
  scenario or live run never reads or changes the player's saves, and every run's menu starts
  with none.
- **Wait a frame or two after opening a dialog before `clickUi`:** a control's place comes from its
  layout, and a dialog opened the frame before (the game menu on Escape in a freshly loaded game)
  has not been laid out yet, so the click lands where the control is not.
- Relative `--scenario` and `--output` paths resolve against the repo root, because Godot changes
  its working directory to the project folder when given `--path`.
- `--fixed-fps 30` matches the sim's 30 Hz tick, so every frame advances exactly one sim tick:
  frame counts in `wait` and `hold` are tick counts, and runs repeat exactly. It also guarantees
  that input held for a single frame (a `press`) overlaps a sim tick; at 60 fps the sim ticks only
  every other frame, so a one-frame press of a polled key (E) could be missed. If the
  sim tick rate changes, this value changes with it.
- `AppRoot` attaches the `ScenarioRunner` only with `--scenario` and only in debug builds. The
  runner processes with `ProcessMode.Always`, so scenarios can use `pause` and `step`, and with a
  process priority after the game's, as when it was the game's child. Its report's perf summary
  samples whichever game is in play.
- **A fixed start:** a scenario's game is built from `AppRoot.ScenarioSeed` (1) unless `newGame` or
  `--seed` names another, so the world, spawn, and camera before the next command are the same
  every run (interactive runs keep a random world). Together with the camera jump in `initMap` and
  `teleportPlayer`, a scenario's first frames do not depend on chance.
- **Output:** by default `test-output/runs/<yyMMdd-HHmmss>-<name>/` at the repo root
  (git-ignored), or `--output`. It holds the PNGs and `report.json`: scenario name and file,
  result, exit code, message, frame count, a run-level `perf` summary, and one record per
  executed command (step index,
  command, frame, the step's JSON, outcome, message, and detail: expectation results with
  expected and actual values, or the snapshot for a capture). `hold` records as its three parts.
- **Performance summary:** the report's `perf` covers every frame of the run: `mode` (`windowed`
  or `headless`), `uncapped`, `frames`, `renderer`, `budgetMs`, the average and worst
  `headlineMs` (the larger of the CPU and GPU times) and `workMs`, the average
  `scriptMs`, `renderCpuMs`, and `gpuMs`, the average and worst `simMs` and `frameMs`, and the
  average and peak `draws` and `objects`. Headless runs use a dummy renderer, so their timings and
  render counts are not measurements; measure windowed, with `--uncapped` for true frame times.
  Scenarios never assert on timings or draw counts, which vary by machine. To compare a change,
  run `perf-walk` several times before and after (`<godot.editor> --path src/Telaelus --fixed-fps 30
  --resolution 1280x720 -- --uncapped --scenario=src/Telaelus.Integration.Tests/Scenarios/perf-walk.json`)
  and compare the medians: run-to-run noise is about 10%.
- **Exit codes:** 0 passed; 1 an expectation failed; 2 the scenario was invalid (unreadable file,
  bad JSON, unknown command, bad arguments) or a step threw. A run stops at its first failure and
  still writes its report. The last lines of stdout are `SCENARIO PASSED|FAILED|ERROR: ...` and
  `SCENARIO REPORT: <path>`.
- **Shutdown trace:** a run also writes `shutdown.log` in its output folder (`ShutdownTrace`),
  flushed line by line, each line the milliseconds since the trace started and a step: the trace
  started, the report written and quit requested, the game root leaving the tree and finishing
  .NET's finalizers, and the last node's deletion. A run that dies while quitting shows how far it
  got.
- **`--exit-garbage=<n>`** (a stress knob, after `--`): just before quitting, the runner drops n C#
  input events and starts a garbage collection without waiting, so finalizers are still running as
  the engine shuts down. With the shutdown handling in place such runs still exit cleanly; it
  exists to prove that.

### Stress runs

- **`src/Telaelus.Integration.Tests/tools/stress_scenarios.py`** runs scenarios many times to catch
  intermittent failures: `python src/Telaelus.Integration.Tests/tools/stress_scenarios.py <names...>
  [--all] [--runs N] [--jobs J] [--timeout S] [--dumps] [--dump-type 1-4] [--exit-garbage N]
  [--keep-ok]` (Godot from `tools.json`). Each run is the headless launch the integration tests
  use, in its own folder under `test-output/stress/<yyMMdd-HHmmss>/`.
- **Classes:** ok (exit 0, report passed), failed (the scenario failed: its report's exit code),
  shutdown (the report passed, then the process died), crash (died before its report), timeout.
  Problem runs keep stdout, stderr, the report, and `shutdown.log`; ok runs are deleted unless
  `--keep-ok`.
- **Output:** a progress line (a dot per ok run, a letter per problem run, with its exit as a
  named Windows code and its crash signature: the .NET fatal or unhandled-exception lines and top
  stack frames from stderr), a table of classes per scenario, how many runs left C# references to
  Godot objects at exit ("Leaked unsafe reference" in stderr, the shutdown crash's precondition),
  the distinct crash signatures, and `summary.json`.
- **Dumps:** `--dumps` sets `DOTNET_DbgEnableMiniDump` and its settings on the Godot process, so
  the .NET runtime writes `crash-<pid>.dmp` in the run's folder when it dies (type 2, with heap,
  by default: enough for managed stacks in `dotnet-dump`). No system setting changes.

### Integration tests

- **Project:** `src/Telaelus.Integration.Tests/`, xUnit, separate from the engine-free sim tests. A
  `ProjectReference` to `Telaelus.csproj` with `ReferenceOutputAssembly="false"` builds the game first.
- **One test per file:** `ScenarioTests.Scenario` is a `[Theory]` over the file names in
  `Scenarios/*.json`, so each file is its own test in Test Explorer and `dotnet test`.
- Each test runs Godot headless with `--fixed-fps 30`, an output folder under
  `test-output/integration/`, and a 60 s timeout. It passes on exit code 0; a failure message
  gives the exit code, the report's message, the run folder, and the end of stderr.
- **Godot path:** the `GODOT_EDITOR` environment variable, else `godot.editor` in `tools.json` at
  the repo root (found by walking up to `Telaelus.slnx`); with neither, the test fails with setup
  instructions.
- **In CI:** the GitHub workflow's test job runs the sim tests and these on Linux, with
  `GODOT_EDITOR` set to the editor `ci/export.py setup` downloads (builds.md).
- **Schema checks:** `ScenarioSchemaTests`, in the same project but without launching Godot,
  checks that `scenario.schema.json` has no duplicate keys (JSON lets a later key silently replace
  an earlier one) and validates every scenario file against it with JsonSchema.Net, one test per
  file. This keeps the `$schema` that editors use accurate.
- **Run:** `dotnet test src/Telaelus.Integration.Tests`, or `dotnet test Telaelus.slnx` for everything.
- **What belongs here:** what only the running game can show (input reaching the right place,
  presentation state, views mirroring the sim). Sim rules belong in the unit tests. Screenshot
  steps in a kept scenario are for manual runs; the test suite skips them.
- **Current scenarios:** `boot` (the game starts; player at the center, nothing on the map but
  the HQ in the empty map's north-east corner),
  `field-detail` (clicking a field opens its panel with its name, Remaining, and Worked by; the
  field keeps its outline; two Mines show as "2 Mines" and Remaining falls as they dig; a
  building and a Forest Wood field switch the panel; empty ground closes it; it closes when its
  field depletes),
  `free-camera` (a right drag pans the camera and not the player, keeping the grabbed ground under
  the pointer, and a 2-pixel drag does not; Space slides it back; Ctrl + left drags the same; a
  building can be clicked while panned; Space after clicking a button does not press it again;
  with the player under an open panel, Space jumps the camera back; a 2-pixel drag is a click that
  walks; a right click to walk leaves a panned camera where it is, and Space then slides it back; the camera stops at the map's edge; a drag over the build menu does nothing; a Ctrl +
  left drag in build mode places nothing),
  `fog` (on the empty map with fog, a field beyond sight is not drawn and cannot be hovered;
  coming into sight reveals it, and it stays revealed after the player leaves),
  `hud` (the debug overlay starts hidden and F3 toggles it; the top bar starts with no pins on the
  empty map, then shows each discovery pinned in table order as amounts, Crude Oil dim with no storage, with tooltips of name, amount, and limit, and no Power, Water, Sand, or ship bill with none on the network; the hint overlay shows the harvest hint
  and its bar; I opens and closes the inventory, grouped by tier with discovered resources first;
  the inventory and a building's detail replace each other; a panel covers the debug overlay),
  `power` (a Drill Rig with no supply is in a blackout; two Burner Generators on Coal power it; three
  then four Refineries push demand into a brownout at 57% and a blackout, on the top bar; the
  generator's panel shows its supply and Best fuel),
  `crafting-queue` (a Smelter idle "Queue empty"; its panel's "+" held 1.5 s climbing to 17
  cycles, "-" held back to one, and a click stepping one cycle; Craft adding an order; its status strip's burning entry naming Coal's +30% speed; a later Iron order going after
  a Copper one; an order's X removing it;
  the first order counting down and the Smelter idle when the queue is done; the line under the
  picker naming the recipe's needs, and its input short once storage runs out; windowed screenshots
  of the empty queue, the queue, and the end),
  `hold-mode` (a Smelter switched to Hold from its Mode picker holding at the 2 Iron in storage;
  "+" making one more and holding "Holding: 3 Iron"; Iron used elsewhere made back; "-" held down
  to one cycle; back in Queue mode, by clicking the Queue cell, with an empty queue; windowed screenshots making and holding),
  `refining` (a Steel Mill's Auto input picker and its note and recipe text; an Electrolyzer on
  Hydrogen feeding a Fuel Plant to Rocket Fuel; no ship bill on the top bar),
  `menu` (the loading screen done and the menu showing its title and New Game, Demo, and Quit; `newGame`
  going into a game, and a second replacing it with a fresh world; windowed screenshots of the menu
  and the game),
  `save-load` (Escape opening the game menu and standing the game still; Save Game saving "Save
  1" through the dialog; Load Game asking first with unsaved progress and bringing the saved
  stockpile back; `saveGame` and `loadGame`; Main Menu asking first, and the menu then showing
  Continue and Load Game; windowed screenshots of the game menu, the dialogs, and the menu),
  `demo` (the menu offering Demo; `loadDemo` opening the town at the widest zoom, powered, with
  Material Transfer running and four trucks, and its Iron still rising a minute later; windowed
  screenshots),
  `roads` (the menu's Road tool laying an L of tiles by drag and taking their Stone, a Shift drag
  removing three for half back, a right drag panning instead, Escape leaving build mode; a Mine across a strip of ShallowWater out
  of a truck's reach until the Bridge tool spans it, then the truck hauling its ore home over it),
  `research` (a Research Lab; T opening the tree on Material Transfer with Advanced Material
  Transfer locked, selecting its card showing it disabled in the drawer; starting Material
  Transfer from the drawer taking its fee; pausing keeping progress; resuming without a second fee; the
  project completing, Advanced Material Transfer turning available, and an online Mine's Iron Ore
  reaching storage),
  `power-consumers` (at midday a Solar Array's 60 Power charging a Battery Bank at 50, with the
  clock, power, and store readouts; at night the bank carrying a Drill Rig at Normal; a Fission
  Reactor on Fuel Rods and Water adding 400),
  `later-tiers` (a Sawmill's Lumber; a Cement Kiln's Cement turned into Concrete by a Concrete
  Mixer on Sand and Water; a Brine Well on a salt flat pumping Lithium into a Stockyard; a
  Semiconductor Fab's display stand and recipe changing to Processor when its Silicon Wafer order
  is done),
  `first-ship` (a Fabricator making Hull Plate, its display stand showing the part and changing
  when its Hull Plate order is done and the Steel Beam order runs; a Shipyard's panel showing the bill short and Launch ship
  disabled with why; with the bill seeded, the ship on its pad and the button enabled; L doing
  nothing; the button launching it, the bill counting down again, and "Ships launched: 1"),
  `trainer` (a generated world: the question starting it; all 13 steps done by doing them, each
  reading Done before the next; the ring on the nearest Wood and on the camp; a step already done
  completing at once; the "Trainer complete" toast; the card's X asking to stop, Keep going and
  Stop; a save resuming the step; the game menu's Trainer starting it again),
  `notifications` (no toasts in a new world; seeding Wood showing "New building: Lumber Camp"
  above "Discovered: Wood"; the build menu hiding the stack and holding its time, a discovery
  under it joining the hidden stack, all showing again once it closes and gone after 5 s; a finished project's lasting toast held while
  hovered and closed by its X; a Water blackout and a ship launch each a lasting toast; a burst of
  discoveries as one toast without detail; a fifth and sixth toast pushing out the oldest brief
  ones; the lasting ones gone after 15 s),
  `renewables` (a Forester on Water and Power growing its trees, a Lumber Camp idle until they are
  grown and then cutting; a Quarry beside a mountain; a Deep Mine on a Laterite producing Iron Ore,
  its panel's Site row and Output picker; a worked-out Crude Oil marker named on hover; a Rock
  Crusher supplying 4 Sand),
  `trucks` (a new Depot online beside the HQ with one truck, its panel showing a bay per truck
  slot, buying a second from its panel,
  selling it for 8 Iron and 5 Wood back (the first then selling for nothing) and buying it again, a
  Mine linked by Small Hubs
  filled to 12 getting a truck that loads, carries it round to the HQ's pad, backs on, unloads it
  into storage there (hovering it reads Unloading), and parks, the
  Mine's truck entry in its status strip, hovering a parked truck),
  `capacity` (a Glassworks with no Sand is in a blackout, in `Danger` on the Sand readout beside Power; a Sand
  Collector on Sand supplies it and Glass is made; five Glassworks on four Collectors are a Sand
  brownout at half speed, in `Warning`, shown in the Glassworks panel's Sand entry (in `Warning`)
  and its recipe),
  `network` (a Mine out of reach is offline with the warning marker and makes nothing; E still
  collects what its buffer holds; N shows the overlay; one Small Hub in the HQ's link range brings it
  online and working; the placement tooltip's
  online line; a selected hub's panel shows it online and only its own coverage),
  `storage` (Wood at the HQ's 100 shows full, in red, on the top bar and the harvest hint says Storage
  full; a Silo raises the limit to 400 and harvesting resumes; the inventory's Cargo row; a
  discovery pinning itself, unpinning and pinning again from the inventory in table order, and
  the bar filling at 8 so a ninth discovery does not pin itself; the Silo's detail lists what it adds),
  `inventory` (on the empty map nothing is discovered or pinned and every row is darkened, through
  the last tier; a field in sight and a held resource are discovered, pinned, and move to the top
  of their tier),
  `building-detail` (clicking a building opens its panel with the on/off toggle and Network in
  its status strip, the state line in its tone, and the right status rows; the world
  keeps its hover with it open; it turns the building off and on; clicking another building
  switches it; empty ground, B, and Escape close it; the panel is a third of the window wide; Destroy shows
  its refund on hover, asks once, then refunds half the cost),
  `build-menu` (B opens the menu with discovered buildings first, affordable in color and short
  ones gray, then undiscovered ones; the modal menu stops the world; a hover shows the description
  and then the cost, none for an undiscovered cell, which ignores clicks; B, Escape, and Close close it; clicking an
  unaffordable discovered cell selects it with the placement tooltip),
  `build-rotate` (R rotates the Refinery ghost 2 x 3 to 3 x 2 and back), `place-at-mouse` (aim and
  click places a Mine at the expected anchor; the ghost counts an Iron Ore field in range),
  `walk` (a right click walks there with the destination ring, and Ctrl + left the same; a left
  click on ground does not walk; a right click with a building's panel open walks and keeps it
  open; a right click on the building walks beside it, and on a pond to its nearest shore; a right
  drag pans without walking; in build mode a left click places and a right click walks),
  `harvest-input` (holding E fills the progress bar and harvests one Wood at 3 s, which does
  not yet unlock the Lumber Camp), `field-stage-views` (visible models track the stage and
  vanish with the field), `pause-step` (a right click and a key, while paused, wait for `step`, the walk starting
  with the release,
  and steps advance exact ticks), `place-on-player` (a building cannot be placed on the player;
  right beside them it can, and a click still walks the player away), `terrain-types` (every terrain, with a salt flat in a ring of sand
  and a screenshot of it, a mountain on each ground that takes one, and rivers in every shape on several bank terrains render; the ghost is blocked on a
  river channel and allowed on its bank; the snapshot reports the terrain, biome, and feature under
  the player), `building-models` (every building model in every facing, the tinted ghost for each
  type, red over a blocked spot, and placement still works), `resource-fields` (every deposit's
  field model renders; Crude Oil cannot be hand-harvested; a Mine fills from the nearest mineral
  deposit and an Oil Well from its seep), `generated-world` (a generated world from a fixed seed
  starts the player on open Grassland in chunk (0, 0) with the 3 x 3 chunks around them generated
  and tiles drawn; crossing into chunk (1, 0) generates the chunks around it and the tiles
  follow), `field-batches` (every built terrain tile has a field batch, and batches follow the
  tiles into another chunk), `perf-walk` (the standing performance benchmark: seed 2 at the widest
  zoom, nine clicks of walking for about 30 seconds; in the suite it is a smoke test of a long walk
  across chunks that checks the walk ended where it should).

### Live control channel

- **Starting:** `<godot.editor> --path src/Telaelus --fixed-fps 30 --resolution 1280x720 --
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
