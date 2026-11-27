# Presentation Blueprint

## Overview

The presentation renders the simulation and routes input to it. It owns the Godot scene tree and
reads simulation state each frame; it holds no game logic. Keeping this seam is what makes the
planned multiplayer feasible.

## Current state

### Entry point and the frame loop

- **The app's stages.** The main scene (`src/Telaelus/scenes/App.tscn`) has a single root node running
  `AppRoot`, which owns the stages (`AppRoot.Stage`: Loading, Menu, Game). Godot's own boot splash
  (project settings) is the same splash image, full size, so the window shows the town from the
  first frame.
  - **Loading** (`LoadingScreen`, a `UiLayer`): the splash image (`assets/ui/splash.png`) covering
    the window, cropped and never stretched, over the panel-dark background; a dark band along the
    bottom 84 units with "Loading" (`TitleSize`) and a progress bar under it, centered. Meanwhile
    `AppRoot` loads every `.glb` under `assets/models/` once into `ModelLibrary`'s cache
    (`ModelLibrary.Preload`, 24 a frame) and creates the shared pictures, `ResourceIcons`,
    `BuildingPictures`, and `FieldPictures`, which draw on the picture stage. Progress is half the
    models loaded and half the three pictures ready (`LoadingProgress`); headless runs draw no
    pictures. At the end the menu replaces it.
  - **Menu** (`MainMenu`): the town scene (`scenes/Town.tscn`) instanced live, through its own
    camera, and over it a `UiLayer` covering the whole window that blurs and darkens a little what is
    behind it (`assets/ui/blur.gdshader`: the screen texture at mip 2.5, mixed 27.5% toward the panel
    tint). At the left third, from 30% down and 48 units in: the title ("Telaelus", twice `TitleSize`) and the
    buttons, 44 units tall, left aligned, `BodySize` + 4: **Continue** (shown when there is a
    loadable save; it loads the newest), **New Game** (Enter does the same), **Load Game**
    (disabled with no saves; it shows the save list (`SaveList`, as in the load dialog) under a
    "Load Game" title in place of the buttons, with Back), **Demo** (shown when the demo save is in
    the game; it loads it, saving.md), and **Quit**. Options is planned before Quit. Escape does nothing on the menu.
  - **Game:** New Game (`AppRoot.NewGame(seed)`) frees the menu, or a game already in play, and
    creates a `GameRoot` with a new world (`--seed=<n>`, else the scenario seed for a scenario run,
    else random; `AppRoot.ReadSeed`), handing it the shared pictures (`GameRoot.Initialize`), and
    raises `GameStarted`. Loading a save (`AppRoot.LoadGame`) shows the loading screen over the
    save's thumbnail for two frames and then creates the `GameRoot` from it (saving.md). Escape in a
    game opens the game menu (ui-style.md), whose Main Menu (`AppRoot.ReturnToMenu`) frees the game
    and shows the menu again.
- **The town scene** (`scenes/Town.tscn`, script `TownLife`): ground cells, a straight river with a
  Water Wheel, a road across it on a bridge, a handful of buildings, trees and ore, the character, a
  truck on a road loop (`TruckRoute`, a `Path3D`, with a `PathFollow3D`), a camera at the game's angle
  (orthographic, size 30), the sun, and an environment as the game's. Its starting layout was
  written by `src/asset-factory/tools/town_layout.py`; it is meant to be arranged by hand in the
  editor from there. While `TownLife.Alive` the truck drives the loop unhurried (4 units a second, a
  third of a truck's road speed) and the light follows a clock at the game's pace from mid-morning (`DayLight`); not
  alive, it holds midday with the truck at the loop's start.
- **The splash image:** `<godot.editor> --path src/Telaelus -- --render-splash` (windowed) renders the
  town, not alive, in a 1920 x 1080 viewport, waits six frames, saves `assets/ui/splash.png`, and
  quits (`SplashRenderer`). Re-run it after changing the town; the image is committed.
- **The demo save:** `<godot.editor> --path src/Telaelus -- --write-demo-save` (windowed) loads as
  usual, then opens the demo town as a game, lets it draw for 30 frames, writes
  `assets/saves/demo.save` and its thumbnail, and quits (`DemoSaveWriter`; saving.md).
- `GameRoot` (one per game) builds the world in code and owns the `Simulation` and `SimClock`.
- Each `_Process(delta)`: read input into `PlayerIntent` (camera-relative move, held `E` harvest),
  advance the clock and step the simulation, then sync the views from read-only simulation state.
  `_UnhandledInput` handles mouse-wheel zoom.
- `GameRoot` exposes the sim, camera, build controller, and field views read-only for the testing
  code. `AppRoot` attaches a `ScenarioRunner` when the command line has `--scenario`, or a
  `ControlChannel` when it has `--control-port` (debug builds only, processed after the game each
  frame; see
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
  removed; `GameRoot` passes only the buildings on explored cells), `TruckViews` (a truck model
  per truck, placed at its sim position each frame and turned smoothly toward its heading, with the
  carried resource's item model on its bed, at 0.7 scale, while loaded), `RoadViews` (rebuilt
  when `RoadVersion` changes: each road tile the piece from `assets/models/infrastructure/` whose
  open edges match the neighbouring road or bridge tiles (patch, end, straight, corner, tee,
  crossing), turned in quarter turns; each bridge tile a deck along its span, its end tiles a ramp
  turned with its low end to the land, and every other tile a pier), `SiteViews` (a marker on
  each explored worked-out cell with no field or building on it, `worked_out_oil` for Crude Oil and
  `worked_out_pit` otherwise; and each explored motherlode as `motherlode_base` with twelve of its
  yields' field models round its ring, shared out by its shares, at 1.3 scale),
  `DisplayStandViews` (a `display_stand` at the middle of the front edge, 0.4 inside the footprint
  and turned with the building, on every building with a choice of output: a processor with more
  than one recipe, a Quarry, or a building on a motherlode; it carries the item model of what the
  building makes (`DisplayedOutput`: the running recipe's output, the waiting one's once a change
  is chosen, or the collector's output; nothing while switched off) at 0.7 scale, turning slowly;
  and on every Shipyard's pad the ship as storage holds its parts: `ship_engine` once there is an
  Engine, a `ship_hull_ring` per Hull Section up to four stacked on it, and `ship_nose` once there
  is a Control Module), `PlayerView` (the character), `CameraRig` (the orthographic follow camera, with
  `ZoomTo` for a direct size; attached it follows the player, a drag detaches it, and
  `ReturnToPlayer` slides or jumps it back; see player.md),
  `BuildController` (build-mode selection and rotation, the footprint ghost, the extractor
  work-area disc and field highlights, and the place command; it ignores buildings and fields in
  unexplored cells under the pointer), and the screen UI (below).

### Screen UI

The kinds of UI (modals, panels, overlays), their look, layers, and input rules are in
ui-style.md. There is no text HUD; the screen holds:

- **Top bar** (`TopBar`, always shown): the pinned resources (`ResourcePins`, saved with the game;
  at most 8, `ResourcePins.Max`, kept in table order; none at start: a new game pins what it
  discovered around the HQ, in table order, and each later first discovery pins itself while the
  bar has room (`AutoPin`, from the sim's `Discovered` events); Sand and Water can never be
  pinned, `ResourcePins.CanPin`, and a loaded save's pins drop them and keep at most 8), each as its icon and
  its amount alone (`TopBar.PinText`), in `Danger` when storage has no room for another unit and
  dim while it has no storage at all (`TopBar.PinColor`), with a tooltip of its name, amount, and
  limit ("Wood 45 / 100", `HudOverlay.StorageText`). Then the clock
  (the `icons/sun` icon while there is daylight, else `icons/moon`, and the hour of a 24-hour day,
  "14:00"), then each capacity in `CapacityType` order (Power, Water, Sand), shown only once the
  network has any supply or demand of it (or, for Power, a Battery Bank): its icon (the
  `icons/power` bolt, or the Water or Sand item's icon) and demand over supply ("45 / 100", or
  "Blackout:" before the icon), in normal text, `Warning` in a brownout, or `Danger` in a blackout,
  with a name tooltip; and once there is a Battery Bank, what the banks hold (the Battery item's
  icon and "3200 / 6000", `Warning` while discharging). Pinning or unpinning rebuilds the pins. The
  ship launch is not on the bar: it is the Shipyard's panel's.
- **Hint overlay** (`HintOverlay`): below the top bar, the E key's action with the resource's
  icon and a progress bar toward the next unit: "[E] Harvest Wood" while a hand-harvestable field is
  the target, "[E] Collect Iron Ore (12)" (units of the first type in the output buffer, with "+1 more" for
  each further type: "[E] Collect Stone (5) +1 more") while a building is; while storage has no room for the
  resource, "Storage full" in `Warning` replaces the bar.
- **Notifications** (`Notifications`, look in ui-style.md): `GameRoot` collects the sim's
  `Happened` events and, once a frame, turns them into toasts (`NotificationText`): a resource
  discovered for the first time (brief, its icon, "Discovered: Coal", and what can work it, "A
  Mine or a Drill Rig can work it", or no detail when nothing extracts it); a research project
  complete (lasting, the project's icon, "Research complete: Material Transfer", its description),
  and for a tier's unlock project a second lasting toast after it (its icon, "Tier 2 buildings and
  research are now available", "[B] to build"); a
  capacity in blackout (lasting, its icon, "Blackout: Water", "Demand 2 over supply 0"); a ship
  launched (lasting, the Shipyard's picture, "Ship launched", "Ships launched: 2"); a building
  refunded on load (lasting, its picture, "Burner Generator refunded", "It grew to 4 x 4 and no
  longer fit; its cost is back in storage"). A frame's
  discoveries merge into one toast ("Discovered: Coal, Sulfur", the first one's icon, no detail). A new
  world and a load notify nothing. `GameRoot` sets `Notifications.Covered` while a modal is open,
  hiding the stack and holding its timers (ui-style.md).
- **Trainer** (`Trainer`): teaches the controls and the opening, one step at a time on the step
  card (ui-style.md). Each step is done by doing it and reads "Done" for a second before the next;
  a step already done when it comes up completes at once. The steps (`TrainerStep`: text, hint,
  when it is done, where the pointer ring goes) are one table:
  1. "Right-click the ground to walk there." (the player has walked 4 units)
  2. "Drag with the right mouse button to look around." (the camera is detached 8 units from the
     player)
  3. "Scroll the mouse wheel to zoom in and out." (the zoom has gone both ways)
  4. "Press [Space] to bring the view back to you." (the camera follows again)
  5. "Walk to the trees and hold [E] to gather Wood." (hint "Gather 15 to build the Lumber Camp.";
     done when the Lumber Camp is affordable; the ring on the nearest explored Wood field)
  6. "Press [I] to see what you have." (the inventory is open)
  7. "Press [B] and choose the Lumber Camp." (build mode with it)
  8. "Press [R] to turn it." (its facing has changed)
  9. "Left-click near the trees to place it." (a Lumber Camp stands; the ring on the Wood)
  10. "Click the Lumber Camp to see what it is doing." (its panel is open; the ring on the camp)
  11. "Its Wood waits in the camp. Stand by it and hold [E] to collect it." (Wood collected from a
      Lumber Camp by hand; the ring on the camp)
  12. "Press [N] to show the network." (the network overlay shows)
  13. "Press [T] to see research, then [Esc] for the game menu." (the game menu opens after the
      research tree has been open)

  After the last it ends with a lasting toast, "Trainer complete". Its counters (distance walked,
  zoom directions, the facing, Wood collected, research seen) start with each step and are not
  saved. It is presentation only: it reads the sim, the camera, the build controller, the network
  overlay, and the open dialog.
- **Starting the trainer:** the first New Game on a machine asks "Learn to play?" (Start trainer /
  Skip) once the game opens (`GameRoot.AskTrainer`, set by `AppRoot` when no save is loading and
  `UserSettings.TrainerAsked` is false; asking sets it, in `user://settings.cfg`). Later, the game
  menu's Trainer starts it from the first step. Test runs (`--scenario=` or `--control-port=`)
  never ask and never read or write the settings file.
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
- **Field panel** additions: a growing field shows a Growing row ("2:10 left") instead of
  Remaining and Worked by; a motherlode is titled by its kind ("Laterite motherlode") with a Yields
  row (each share and rate), Remaining "Endless", and Worked by its one building and what it is set
  to, or what would work it. A growing Forester field's trees stand at a third of full height when
  planted and rise to full (its batch rebuilds each tenth of its growth).
- **Inventory** (`InventoryPanel`, a panel titled "Inventory"): I opens and closes it. First the
  Cargo pool, "Cargo (40 / 150)" (in `Warning` when full); then an arrow picker (`inventory/tier`,
  ui-style.md) over "Resources" (the raw resources), then "Tier 1" to "Tier 4", a tier not yet
  unlocked hidden (its items cannot be made); then the chosen one's resources. The discovered come
  first, then the undiscovered, each group in table order; rows move as resources are discovered.
  The panel reopens on the last choice viewed ("Resources" the first time); the choice is not
  saved and starts again with a new game or a load. A discovered row is its icon, "Wood (45 / 100)" (the amount alone for
  a Cargo or unlimited resource, as "Iron (23)"; demand over supply for Sand and Water, as "Water
  (2 / 4)", colored by its state), including those at 0, in `Warning` when storage has no room for
  another unit, and a Pin toggle at the right ("Pin", or "Pinned" when on the top
  bar) that pins or unpins it, disabled with "The top bar holds 8: unpin one first" while 8 are
  pinned and it is not one (none for Sand and Water, which have their own top bar readouts); an
  undiscovered row is darkened: its icon tinted `Silhouette` and its name in `TextDim`, with no
  amount or pin, and a tooltip, "Not yet found" for a raw resource or "Not yet made" for an item. The
  body scrolls. Like every panel, opening it closes a building's detail panel and the other way
  round.
- **Research tree** (`ResearchTree`, a modal titled "Research"): T opens it; see research.md for
  its cards, link lines, and buttons. The Research Lab's detail panel adds a Research row and a
  "Research tree [T]" button.
- **Build menu** (`BuildMenu`, a modal titled "Buildings"): B opens it (leaving build
  mode) and B, Escape, the Close button, or a click on the backdrop closes it. At its top a
  segmented tier picker (`buildMenu/tier`: "Tier 1" to "Tier 4"); a tier not yet unlocked
  (research.md) is a disabled cell whose tooltip names its research ("Unlocked by Steel Works
  research"). The menu opens on the tier last viewed, the highest unlocked the first time; the
  choice is not saved and starts again with a new game or a load. Below, a grid of 112-unit square
  cells, as many columns as fit, scrolling when taller than the panel allows: the chosen tier's
  buildings (never the HQ), each level of a leveled family its own cell in its level's tier
  ("Drill Rig" in Tier 2, `buildMenu/DrillRig`, a cell's id being its name without spaces), by
  name, and on Tier 1 the Road and Bridge cells sorted in among them
  (pictures drawn from `road_cross` and `bridge_deck` as UI icons, in color when one tile is
  affordable on plain ground, grayscale when not; a hover names the tool and its price; clicking
  one closes the menu and selects that line tool, buildings.md). A building's cell shows its
  picture and name, in full color when affordable and in grayscale when not. A level whose tier is
  open but whose research is not done is a **locked** cell, after the others (by name): its picture
  grayscale and dimmed, its name in `TextDim`. Hovering a cell outlines it and shows its tooltip:
  its description (buildings.md) in `TextDim` broken into lines of about 44 characters
  (`TipLine.Wrapped`), then its cost, or for a locked cell what it needs ("Needs Powered Mining
  research", and "Deep Mining needs Powered Mining" while that project's own prerequisite is not
  done; `BuildMenu.LockLines`). Clicking a cell, affordable or not, closes the menu and selects the
  building at that level for placement (`BuildController.Select(type, level)`), so the player can
  position it while materials build up; clicking a locked cell does nothing. The cells refresh while the menu is open, so a cell turns from gray to color
  as the stock arrives.
- **Building detail panel** (`BuildingDetail`, a panel): opened by clicking a placed building
  outside build mode, and switched to another building by clicking it. It is built from the UI
  patterns (ui-style.md) and refreshes every frame. Top to bottom:
  1. **Title:** the building's name.
  2. **Top block:** a `TurntableView` showing the building turning about its center once every 18
     seconds in a 180-unit square (a live view of the picture stage, drawn only while visible),
     with the **status strip** as a column to its right that wraps into a further column past
     the picture's height. Its entries, in order, each only when it applies: the **on/off icon
     toggle** (`buildingDetail/toggle`, the power symbol, `Good` on and `TextDim` off, tooltip
     "On: click to turn off" or "Off: click to turn on"; clicking calls `SetBuildingEnabled`; not
     on a storage building); **Network** (the hub mast, `Good` "Online: connected to the network"
     or `Warning` "Offline: outside the network"); one entry per capacity type the building draws
     or supplies (**Power** as the bolt, **Water** and **Sand** as their item icons): a consumer's
     draw as "-10", in `Text`, `Warning` in a brownout ("Draws 10 Power, running at 80%") or
     offline ("Draws 10 Power: offline, so it gets none"), `Danger` in a blackout ("Draws 10 Power:
     blackout"); a provider's supply as "+16" ("Supplies 16 Power", "Supplies 12.8 of 16 Power"
     when its own draws or the daylight slow it, a Solar Array), `Warning` "+0" offline
     ("Offline: supplies no Power"); dim while switched off or supplying nothing; a Battery Bank's
     **charge** (the battery icon and "53%"; tooltip "3200 / 6000, charging", or "discharging" in
     `Warning`, "full", "holding"); the **fuel burning** in a burner (the fuel's icon and "22 s";
     tooltip "Coal, 22 s left", and on a building that makes things the fuel's boost, "Coal, 22 s
     left, +30% speed"; no entry while nothing burns); and an extractor's **truck** (the truck
     icon) while one is on its way ("On its way") or loading ("Loading"), or `Warning` "No truck
     can reach".
  3. **State line:** "Working" or "Idle: " and the reason in `BodySize`, in `Text` working,
     `TextDim` while idle by choice or in the normal course of things (switched off, "Queue
     empty", "Holding: 50 Iron", "No research running", "Night: supplies nothing"), and `Warning`
     for any other reason (a problem: no power, offline, storage full, waiting for inputs or fuel).
     Not on storage buildings or hubs.
  4. **Text rows** (`StatusRows`): Research (a Research Lab's running project, "Material Transfer,
     40%, speed 1x", or "No research running"), Site (a Deep Well or Deep Mine working the site
     beside it: "Worked-out Iron Ore", "Laterite motherlode"), Plot (a Forester: "Growing: 2:10 left", "Grown: 112 / 150 Wood",
     or "Replanting in 18 s"), Eats (the Rock Crusher: "1 Stone every 10 s; 230 in storage"), In
     range (extractors: the fields in the work area counted by type, each followed by what those
     fields hold in all, as "Iron Ore 2 (500), Stone 1 (250)", or "none"), Working on (only for a
     building that works more than one type: the current field's type and amount left, as "Stone
     (212 left)"), Recipe (processors: "1 Iron Ore to 1 Iron, 16 s"; "1 Glass, 12 s" with no
     inputs; an alternative input as all of them, "2 Iron + 1 Coal or Charcoal + 1 Limestone to 1
     Steel, 20 s"; "(next: Copper)" while a change waits for the running cycle; on a queueing
     processor only while it has an order or a cycle), Buffer (an extractor's or processor's output
     buffer, "Iron Ore 12 / 20", "Iron 4 / 10"), and a hub's Links (how many hubs it links to) and
     Reach ("covers 8, links 12").
  5. **Progress bar** toward the next unit or through the cycle, with no label: extractors,
     collectors, and processors (a queueing processor only while it has an order or a cycle).
  6. **Sections**, each under a section heading and only when it has something:
     - **Launch** (a Shipyard): a Launch ship button (`buildingDetail/launch`), enabled only while
       this Shipyard is switched on and online and the whole ship bill (`Simulation.LaunchCost`)
       is in storage, calling `TryLaunchShip`; under it why not ("Needs the whole bill in
       storage", "This Shipyard is offline", "This Shipyard is switched off"); "Ships launched: n"
       once one has; then a row per bill entry with its icon, name, and "have / need", in `Short`
       until met. It comes first, so it shows without scrolling. There is no key for it.
     - **Production** (processors): a Recipe dropdown (`buildingDetail/recipe`) when the building
       has several recipes. On a queueing processor (buildings.md) it chooses what to queue,
       starting on the recipe the building runs; on the three that run without a queue it calls
       `SetRecipe`. Under it a dim line spells out the chosen recipe ("1 Iron Ore to 1 Iron, 16
       s"), each input storage holds less than a cycle's worth of in `Danger` (an input with
       alternatives only when every one is short). A queueing processor then has Mode, a
       segmented Queue / Hold (`buildingDetail/mode`): Hold calls `SetHold` with the picked recipe
       and `DefaultHoldTarget`, and Queue calls `ClearHold`. A recipe whose input has
       alternatives has a segmented input picker for the picked recipe (`buildingDetail/input`:
       Auto, then each; choosing calls `SetInputChoice` for that recipe) over a dim note ("Auto:
       uses Coal (Coal 58, Charcoal 4)", or "Uses only Charcoal"). In Queue mode come the order
       controls: a quantity in items between "-" and "+" step buttons (`buildingDetail/less`,
       `buildingDetail/more`; repeating while held), each step a cycle of the chosen recipe, from
       one cycle (where it starts, and again on a new recipe) to 99, then Craft at its label's
       width (`buildingDetail/craft`, calling `QueueCraft`; disabled when the queue is full and its
       last order is another recipe); and under a dim "Queue" label a row per order ("Iron x 5",
       the first "(crafting)" while its cycle runs), each with an X (`buildingDetail/queue/0` and
       on, calling `CancelCraft`), or "Queue empty". In Hold the Recipe dropdown picks the held
       recipe (a new one resets the target), and in place of the order controls and queue there
       is a "Hold at" row: "-", the target, "+" (`buildingDetail/holdLess`,
       `buildingDetail/holdMore`, step buttons stepping a cycle's output, from one cycle's output
       up to what storage could hold of it, its stock plus `RoomFor`, at most 9999), and a dim
       line "In storage: 32 / 50 Iron", the count in `Text` once it reaches the target.
     - **Output** (a collector without a field, such as the Quarry or a building on a
       motherlode): a dropdown of its `OutputChoices` with each rate ("Iron Ore, every 5 s";
       disabled with one choice; choosing calls `SetOutputChoice`), a dropdown because its labels
       are too long for cells.
     - **Fuel** (a building with a fuel slot): a segmented picker (`buildingDetail/fuel`) of Best,
       then each fuel the building takes, longest-burning first (Best / Coal / Charcoal / Wood, or
       Best / Fuel Rod), a dropdown instead when its cells do not fit the panel; choosing calls
       `SetFuelChoice`. Under it a dim note naming what it burns ("Best: burns Coal (40 s per
       unit)", with ", +30% speed" on a building that makes things; "Best: no fuel at hand";
       "Burns only Charcoal").
     - **Storage** (storage buildings): a Silo's "Holds" dropdown of the Bulk resources
       (`buildingDetail/siloResource`, calling `SetSiloResource`), and "Adds to storage": one line
       per resource the building adds to, with the amount ("Wood   +300"), and "Cargo pool" for
       the HQ and a Warehouse, headed "Offline: adds nothing until connected" while it is offline.
     - **Trucks** (a Depot): a row of truck icons, one per bay, each colored by its truck's state
       (`Text` driving, loading, or unloading; `TextDim` parked; `Warning` waiting for the pad, or
       for room in older saves) with the state in its tooltip ("Truck 2: To HQ: 20 Iron Ore", by
       `TruckText`: "Parked", "To Mine: Iron Ore", "Loading", "To HQ: 20 Iron Ore", "Waiting for
       the pad", "Backing onto the pad", "Unloading", "Waiting: storage full (Iron Ore)" (older
       saves only), "Heading home" (also while pulling off the pad)); an empty bay is the icon
       faint ("Empty bay"). Then Buy truck and Sell truck side by side at their labels' widths:
       Buy truck (`buildingDetail/buyTruck`, disabled unless `CanBuyTruck`, tooltip "Costs 15
       Iron, 10 Wood") and Sell truck (`buildingDetail/sellTruck`, selling the next truck,
       `TruckToSell`; disabled with no trucks; tooltip "Gives back 8 Iron, 5 Wood", or "Gives
       nothing back (the truck the Depot came with)"). Under them a note: what is short ("15 Iron
       (have 3)" in `Short`), "Every bay is full" (dim), or else the cost (dim).
     - **Research** (a Research Lab): the Research tree [T] button (`buildingDetail/openResearch`),
       sized to its label, opening the research tree.
  7. **Upgrade** (a leveled building below its top level, `buildingDetail/upgrade`): "Upgrade to
     Drill Rig", sized to its label, above Destroy. Its tooltip is the next level's description,
     then its price (the difference in cost, short amounts marked) or, while that level is
     locked, what it needs (as a locked build menu cell). It is disabled while locked or short;
     pressing it upgrades the building in place (`Simulation.TryUpgrade`), and its model, picture,
     and panel switch to the new level.
  8. **Destroy** (not for the HQ), alone at the bottom, sized to its label and centered, whose
     tooltip shows its refund ("Refund: 13 Wood, 8 Stone", or "Refund: nothing") and which asks
     once: the first click changes it to "Confirm destroy" for 3 seconds, and a second click in
     that time destroys the building.

  Pickers choose their form (segmented cells or a dropdown) when the panel shows a building, by
  the panel's width then, so a picker never changes form under the player. The panel closes
  itself when its building is gone. While it is open, its building keeps the hover outline in
  the world.
- **Network view** (`NetworkView`, in the world): hub coverage as one flat translucent plane whose
  shader tints each point once (accent where any online hub covers it, else gray where an offline
  or switched-off one does), so overlapping coverage never darkens, and the links between online
  hubs as thin accent lines. It draws every hub while the network overlay is on (N toggles it; off at
  start) or while a building is being placed, adding the ghost's own coverage (a green disc drawn over the plane) and lines to
  the hubs it would link to when the ghost is a hub; with neither, it draws only the hub the detail
  panel shows, if it shows one. Separately, every switched-on offline building carries a small
  `Warning` ring floating 2.4 units above its center.
- **Placement tooltip:** while a building is selected (and no modal is open), a tooltip
  follows the pointer 28 units below and to the right of it, clear of the ghost: the building's
  name in the accent color, its cost (short amounts marked), fields in range for an extractor,
  "Online", or offline in `Warning`: "Offline (no hub in reach)" for a hub and "Offline: will not
  work" for anything else, for the spot (`WouldBeOnline`), for each
  capacity type the building draws or supplies, that and the network's figure with it added when
  the spot is online ("Draws 2 Sand: Sand 4 / 2", "Supplies 16 Water: Water 2 / 20", colored by the
  state that would give), and the dim hint "R rotate   Esc cancel". Outside build mode the same tooltip names the truck nearest the
  pointer (within 28 pixels of its middle): "Truck" in the accent color over its state, or, with
  no truck there, an empty worked-out cell under the pointer ("Worked-out Crude Oil"). The ghost of
  a Forester snaps (`SnapAnchor`) to map cells. Field outlines and the hover outline fit a
  motherlode's 8 x 8. The ghost itself shows the facing (the model is
  turned) and whether the spot is open and affordable (green or red).

### Asset and model pipeline

- Models are glTF assets (generated by the asset factory) under
  `src/Telaelus/assets/models/<area>/`, following art-style.md.
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
- The character is `character.tscn`, a scene instancing the generated, animated
  `character.glb` (its `idle` and `run` clips set to loop in its import options), that
  `PlayerView` loads. It is the one exception to required models: absent the scene, the player
  falls back to a capsule.

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
  `docs/changesets/future/graphics-settings.md`. The Godot project root is `src/Telaelus`;
  `Telaelus.csproj` references the `Telaelus.Sim` project (see simulation.md), whose `Telaelus.Sim.dll` is built
  beside `Game.dll` and loaded by the engine.
- Lighting (see art-style.md): one `DirectionalLight3D` (the sun, rotated -50 and -40 degrees,
  shadows on) that never moves, so the hard shadows hold still. Each frame `DayLight.Apply` sets
  only its color and energy by the time of day: white at midday and warm (1, 0.74, 0.52) when the
  sun is low, energy 1, by day; a blue moonlight (0.56, 0.64, 0.88) at energy 0.76 by night;
  blended by the daylight through dawn and dusk.
- **On the way out** `GameRoot._ExitTree` runs .NET's garbage collection and waits for pending
  finalizers, twice, so every leftover C# wrapper of a Godot object is finalized while the engine
  is whole. A wrapper finalized during Godot's own C# cleanup races it over the same object and
  crashes the process (0xC0000005 or 0xC000001D on quit). A `WorldEnvironment` with a clear-color background, ambient light off,
  and linear tone mapping; without one, the Mobile renderer would add ambient fill. Directional
  shadows use the hard filter (`soft_shadow_filter_quality` 0), so they stay crisp and dark. The
  first launch on a new machine compiles shaders once (one frame of about a second); Godot caches
  them.

### Testing code

- `src/Telaelus/Testing/` holds the scenario runner, the live control channel, the command executor,
  the command parser, the state snapshot, and shared path helpers (see testing.md). It is inert
  unless `--scenario` or `--control-port` is given.

## Related

- simulation.md (the authoritative state), player.md, terrain.md, resources.md, buildings.md.
