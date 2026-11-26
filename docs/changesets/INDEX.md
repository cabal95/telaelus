# Changesets

A changeset is one coherent unit of change: what we are adding or altering, the code it requires,
and the blueprint updates it entails. A changeset in progress lives here as `<slug>.md`; once
implemented it is stamped `YYMMDDHHMM-slug.md` and moved to `completed/`. Plans we expect to want
but are not building yet are parked in `future/` until their time comes. (The lifecycle and naming
rules are in ../development.md.)

## Future

Parked plans, not approved; see `future/`.

- **graphics-settings** - A player graphics settings panel: the renderer (Forward+ or Mobile, on
  restart, with a first-launch default by GPU), display mode, render scale with FSR upscaling,
  vsync, and a frame cap, kept in a settings file in the player's data folder.

## Completed

Newest last. One line each; open the file for the full spec.

- **2611100901-project-foundation** - Stands up the Godot C# project, the sim/presentation
  architecture, the fixed-step loop, and the orthographic camera.
- **2611100902-player-and-grid** - Adds the tile grid, a drivable player with camera-relative
  movement, and a follow camera with zoom.
- **2611100903-resources-and-harvest** - Adds resource nodes, the stockpile, hand-harvesting, and
  the HUD.
- **2611100904-buildings-and-production** - Adds grid building placement and the Ore to Metal to
  Fuel production chain.
- **2611100905-ship-launch** - Adds the ship launch win condition (500 Fuel, 100 Metal).
- **2611100906-model-asset-pipeline** - Renders resource nodes with footprint-capped CC0 models and
  a colored box fallback.
- **2611100907-animated-character** - Renders the player from an animated character scene with a
  capsule fallback.
- **2611100908-terrain** - Adds tile-based terrain (grass, water, mountain) with a fixed layout and
  colored tiles with shoreline previews.
- **2611100909-impassability** - Blocks movement and placement on water, mountain, resource nodes,
  and buildings.
- **2611110949-world-map-cells** - Splits the world into map cells (terrain, resource fields) and
  fine cells (buildings, movement); adds staged resource fields, building footprints and facing,
  and extractor work areas.
- **2611111121-sim-project-split** - Moves the Godot project to `src/Game` and the simulation into its
  own `Game.Sim` class library, which the game references.
- **2611111128-sim-unit-tests** - Adds the engine-free xUnit sim test project, a seam check that the
  sim references only Godot math types, and the test gate before Implemented.
- **2611111204-scenario-runner** - Adds JSON scenarios that drive the real game (setup, input,
  screenshots, expectations), `Simulation.Debug`, and headless integration tests, one per scenario.
- **2611111216-live-control** - Adds a loopback HTTP control channel that drives a running game with the
  scenario commands, plus `pause`, `resume`, and frame-exact `step`.
- **2611111234-block-placement-on-player** - Blocks placing a building with the player inside its
  footprint, and records when a change needs a changeset.
- **2611111238-scenario-schema-checks** - Fixes the scenario schema's duplicate `step` definition and
  adds tests that check the schema and validate every scenario against it.
- **2611111426-biome-terrain-model** - Adds eight terrain types with rules in a `TerrainDef` table, eight
  biomes per map cell, mountain and river features (rivers with stored flow and derived shape and
  channel), and a test map repainted to show every biome.
- **2611111615-mountain-models** - Replaces the mountain cone with six required generated models
  (stone and snow families of three shapes, chosen by the ground beneath), each a standalone peak
  inside its map cell with flat edges.
- **2611111733-initial-world-assets** - Replaces every remaining placeholder with generated models (ground tiles,
  transition pieces, river pieces, buildings, and model-based build ghosts), makes all models
  required, and starts the art-style blueprint.
- **2611111844-raw-resources** - Adds the eight raw resources the first ship needs (six deposits, two
  terrain-sourced) with a definitions table, biome deposits, a multi-deposit Mine, the Oil Well,
  placeholder Smelter and Refinery recipes, and generated deposit models.
- **2611112100-procedural-world** - Runs the game on a generated world: 64 x 64 map-cell chunks made on demand from
  a seed and the starter planet (climate-driven biome regions, terrain patches, mountain ranges,
  rivers, deposits, and a spawn with an exit), drawn in 16 x 16 render tiles, with HUD
  diagnostics; the test map stays as a bounded world for tests.
- **2611112121-field-batching-and-perf-hud** - Batches resource field models into one multimesh per model per render tile,
  and adds frame-budget performance numbers to the HUD (F3), snapshots, and scenario reports, with
  the `perf-walk` benchmark and `--uncapped` for measurement runs.
- **2611112139-camera-zoom-per-pixel** - Makes the camera zoom world units per 720 pixels of window height, so resizing
  the window shows more or less map at the same scale, and backs the camera off as the view grows
  so tall windows no longer clip the ground.
- **2611112146-generated-trees** - Replaces the CC0 trees with generated tree families by biome (conifers in
  Forest, broadleaf in Grassland, swamp trees in Swamp), so every world model is generated.
- **2611121827-building-costs-and-gather-rates** - Gathering takes one unit at a time over a gather time per
  deposit, base extractors work at 0.75x hand speed, processors at half an extractor's pace, and
  buildings cost resources and unlock once their materials have been held.
- **2611122002-ui-style-and-build-menu** - Starts the UI style blueprint (tokens, theme, UI scale, modal and side-panel
  overlays) and replaces the HUD's build line with a build menu on B (discovered buildings in
  color or gray, undiscovered silhouettes, cost tooltips) and a placement tooltip.
- **2611122022-building-detail-overlay** - Clicking a placed building opens a detail side panel: a turning picture, status
  rows (power, state, fields in range, what it works on, recipe, progress), turn off or on, and
  destroy (asks once) for half the cost back; the sim gains switching off, status, and destroying.
- **2611122105-resource-icons** - Adds a generated item model per resource (`item_gen.py`) and renders each once at
  startup into a resource icon through a shared picture stage.
- **2611122116-hud-overlays-and-inventory** - Replaces the text HUD with a top bar (raw resources as icons, launch
  progress), a hint overlay, an F3 debug overlay (off by default), and an I inventory panel, and
  names the kinds of UI modal, panel, and overlay.
- **2611122136-field-detail-panel** - Clicking a resource field opens a field detail panel: the resource's name, a
  picture of a full field drawn once at startup (per tree family for Wood), Remaining, and Worked
  by.
- **2611141051-fog-of-war** - Hides the world until the player sees it (reveal once, 24 units of
  sight): unexplored cells are soft-edged darkness, their contents are not drawn or pickable, and
  buildings need explored ground.
- **2611141241-free-camera** - Right drag or Ctrl + left drag pans the camera without moving the
  character, within the explored area; moving or Space brings it back (sliding when the character
  is on screen, jumping when not). UI buttons no longer take keyboard focus, and maps without fog
  no longer draw a dark rim at their edge.
- **2611141406-mobile-renderer** - Switches to the Mobile renderer on Direct3D 12 (about 3 times
  faster fullscreen on the laptop's integrated GPU), keeping the look with no ambient light and
  hard shadows; the debug line's frame figure becomes the larger of CPU and GPU time.
- **2611141757-resource-tree** - Plans the resource tree (raw to first ship, with a Mermaid
  chart) and the economy around it: Stored and Capacity supply, fuel and input pickers, storage
  limits, brownout, the HQ hub network, Depot trucks, motherlodes, and renewable sources; a
  planning changeset that updates game-plan.md and orders the follow-on changesets.
- **2611141930-resource-catalog** - Adds every resource in the catalog (63, by tier) with item models
  and icons, discovery (a deposit when first revealed on the map, anything when first held), and an
  inventory grouped by tier with undiscovered resources darkened; startup pictures draw in batches.
- **2611142016-storage-limits** - Adds storage limits: a per-type limit for raw materials, fluids, and
  secure resources and a shared Cargo pool for made goods; the HQ placed by every world, and the
  Silo, Stockyard, Tank Farm, Warehouse, and Vault; gathering and production stop at the limit
  ("Storage full"); top bar pins with amount over limit.
- **2611142047-hq-network** - Adds the HQ network: the HQ, Small Hubs, and Relays link and cover
  buildings, which are online or offline; collectors fill local buffers the player collects by
  hand (E), online processors use storage, offline ones run from buffers loaded by hand; storage
  counts only online; the network overlay (N), a selected hub's coverage, and offline markers.
- **2611142117-power** - Adds power on the HQ network: online generators supply, online powered
  buildings draw, a brownout slows consumers by twice the overload and past 25% over a blackout
  stops them; fuel slots with burn times and a Best fuel picker (the Smelter now burns fuel); the
  Water Wheel, Burner Generator, Lumber Mill, and Drill Rig; the Refinery draws power.
- **2611142141-terrain-capacity** - Makes Power, Water, and Sand one Capacity model on the HQ network:
  the Water Pump and Pumping Station beside water, the Sand Collector and Sand Dredge on Sand; the
  Glassworks makes Glass from fuel and 2 Sand; the Refinery draws 2 Water; Sand and Water readouts
  on the top bar, inventory, panel, and placement tooltip.
- **2611142204-depot-trucks** - Adds the Depot and its trucks: an online Depot comes with one truck and
  buys up to four; trucks dispatch themselves to the fullest collector buffer (10 or more), load one
  resource, drive it to the HQ, unload into storage, and park; A* paths in the sim over walkable
  cells; truck models with their cargo, the Depot panel, and a collector's Truck row.
- **2611142231-field-rebalance** - Makes fields fewer and larger (four times the amount, a third the
  density, the Forest half); a starter set of two Wood and one Stone in sight of the spawn; fields
  and field models for Bauxite, Quartz, Limestone, Sulfur, Titanium Ore, Rare Earths, Uranium Ore,
  and Crystals, worked by the Mine and Drill Rig.
- **2611150937-renewables** - Adds the renewable bypasses: the Forester (a tree plot that regrows its
  own Wood field), the Quarry (endless Stone or Limestone beside a mountain), the Charcoal Kiln, the
  Rock Crusher (Sand from Stone), and the Deep Well and Deep Mine over worked-out cells; depleted
  fields leave worked-out markers; rare 8 x 8 motherlodes, endless, each worked by one centered Deep
  Mine or Deep Well yielding one chosen resource at its share's rate.
- **2611151006-production-refining** - Adds tier-1 refining: processors with several recipes (a Recipe
  picker) and Coal-or-Charcoal inputs (an Auto picker); the Smelter's Copper, the Refinery's
  Kerosene, and the Steel Mill, Electrolyzer, Fuel Plant, Chemical Plant, Aluminum Smelter, and
  Electric Furnace with their models; Rocket Fuel from Kerosene and Hydrogen; the launch adds 200
  Liquid Oxygen.
- **2611151033-first-ship-parts** - Adds the first ship's parts: the Fabricator, Electronics Works,
  and Tile Kiln (tier 2), the Assembly Plant (tier 3), and the Shipyard (tier 4) with 13 recipes; the
  launch needs 4 Hull Sections, an Engine, a Control Module, 500 Rocket Fuel, and 200 Liquid Oxygen
  and an online Shipyard; display stands show what a building makes, and the ship grows on the
  Shipyard's pad as its parts arrive.
- **2611151049-later-tiers** - Gives every catalog item a maker: Sulfuric Acid, Magnet, Electric
  Motor, Armor Plate, Insulated Cable, Battery, Computer, Heat Shield, Life Support Unit, and Sensor
  Array as new recipes; the Sawmill, Cement Kiln, Concrete Mixer, Brine Well (Lithium on Desert
  Sand), Semiconductor Fab, Air Separator, and Enrichment Plant with their models; the new
  buildings cost Lumber, Concrete, and parts.
- **2611151108-power-consumers** - Adds a ten-minute day and night (the light warming at dawn and
  dusk, a dim moonlight at night, its angle fixed), the Solar Array (Power by daylight), the Battery Bank (stored Power that
  charges from a surplus and covers a shortage), and the Fission Reactor (Fuel Rods for 400 Power,
  cooled by Water); fuels by building; the top bar's clock, power bolt, and battery readout.
- **2611151236-research** - Adds global research: one project at a time, progress in game time that
  is never lost, one-time fees, prerequisites, and research speed from Research Labs (each less than
  the last, down to a quarter); Material Transfer (Wood, Stone, Iron Ore, Copper Ore, Coal) and
  Advanced Material Transfer (every other raw resource) drain online collectors into storage; the
  research tree (T) and the Research Lab.
- **2611151313-shutdown-crash** - Fixes the intermittent crash on quit (C# wrappers of injected input
  events finalized while Godot freed them): injected events are disposed once delivered, and the
  game root finishes .NET's finalizers on exit; adds the scenario stress script with crash dumps,
  shutdown breadcrumbs, and a forced-race knob.
- **2611151337-scenario-camera** - Fixes the `trucks` click flake: scenario runs start from seed 1,
  `initMap` and `teleportPlayer` jump the camera onto the player, and `click` fails when the aim is
  outside the window instead of clicking there.
- **2611151401-roads-and-bridges** - Adds roads (2 x 2 gravel tiles on walkable land, 1.5x speed for
  trucks and the player, laid as an L by drag) and bridges (straight spans over ShallowWater or across
  a straight river), with costs, half refunds, buildings kept off them, truck paths using them, the
  build menu's Infrastructure cells, and the joining road and bridge pieces.
- **2611151428-loading-and-menu** - Adds a loading screen (the town splash image, "Loading", and a
  progress bar while every model loads and the pictures draw, once per run) and a main menu over the
  live town with a blurred panel (New Game, Quit); an editable town scene and `--render-splash`; games
  created on New Game; scenarios start at the menu with a `newGame` step.
- **2611151454-save-and-load** - Saves and loads a game: the sim's whole state as versioned gzip
  JSON that continues exactly, named saves and two rotating autosaves with thumbnails in the
  player's data folder, an in-game menu on Escape (Resume, Save Game, Load Game, Main Menu, Quit)
  that pauses the game and asks before throwing away unsaved play, and Continue and Load Game on
  the main menu; `saveGame` and `loadGame` scenario commands.
- **2611151521-telaelus-rename** - Names the game Telaelus: the window title, main menu, C#
  namespaces and assemblies, project folders (`src/Telaelus*`), solution (`Telaelus.slnx`), and
  docs, plus a root README that introduces the game, its name and pronunciation, and how it is built.
- **2611151728-ci-builds** - Exports Windows (x64, arm64) and macOS (universal) builds with one
  script, `ci/export.py`, that sets up Godot and runs the same locally and on GitHub; a GitHub
  Actions workflow that tests every push and pull request and uploads the builds as artifacts;
  `act` support for running the workflow in Docker; Godot pointed at the repo's solution.
- **2611161735-export-on-tags** - Runs the workflow's export job only for `v*` tags and manual
  runs; pushes to `main` and pull requests only test.
- **2611161741-etc2-astc-import** - Turns on ETC2/ASTC texture import explicitly so the macOS export
  works on GitHub's x64 runner (Godot's default is on only for ARM64 editors).
- **2611161830-ci-cleanup** - Clears the C# build warnings (formatting, a null recipe list, an
  unused `SaveDialog.SetName`, and `end_of_line = lf` for C#), moves the workflow's actions to their
  Node.js 24 versions, and uploads each build's zip as is (`archive: false`), skipped under `act`.
- **2611161851-demo-save** - Adds a demo save shipped with the game and loaded from the main menu's
  Demo: a working mining town (`DemoTown`, built in code and run 5 game minutes, balanced so its
  trucks keep running) written with its thumbnail by `--write-demo-save`; the `loadDemo` command
  and the `demo` scenario.
- **2611161940-click-to-move** - The character walks where the player right-clicks (or Ctrl +
  left-clicks), in place of WASD and the arrows; the same press dragged still pans. `WalkTo` paths
  over explored cells round what is in the way (to the nearest reachable cell when the target is
  blocked, on into the fog as it clears), and a ring marks the destination. The road and bridge
  tools remove with Shift alone.
- **2611162030-truck-balance** - Trucks drive at 3 (from 8) and load and unload at 0.25 s a unit, so
  one keeps up with about three collectors 40 units from the HQ (from eight); they load only what
  storage has room for once other trucks' loads are counted, and unload everything (past the limit
  if room was taken on the way), so a truck never waits at the HQ. A Depot sells trucks for half a
  bought one's cost back. The demo save is rewritten.
- **2611162105-top-bar-cleanup** - The ship bill and launch leave the top bar for a Ship launch
  section in the Shipyard's panel with a Launch ship button (no L key); Water and Sand move beside
  Power as capacity readouts, and all three show only once the network has any; Sand and Water can
  no longer be pinned. Pins show their amount alone (red when full, dim with no storage, the limit
  in the tooltip), and the bar's entries sit twice as far apart.
- **2611162133-low-poly-character** - The player character is a generated low-poly colonist in an
  orange work suit: rigid parts on joints, with looping idle and run clips, replacing the textured
  CC0 import. The asset factory's exporter writes animated models.
- **2611191008-notifications** - Toast notifications on the right below the top bar: brief ones (5 s)
  for a building unlocked and a resource first discovered, lasting ones (15 s, an X and a
  countdown bar) for research complete, a blackout, and a ship launched. Bursts merge, hover holds
  a toast, and a new world or load notifies nothing. The sim raises `Happened` events.
- **2611191051-trainer** - An optional trainer teaches the controls and the opening in 13 steps on a
  step card above the notifications (walk, pan, zoom, recenter, gather, the inventory, choose,
  turn, and place a Lumber Camp, its panel, collect from it, the network, research and the game
  menu), each done by doing it, with a pulsing ring on the place it means. The first New Game asks;
  the game menu's Trainer starts it again; a save keeps the step.
- **2611191129-collectors-need-network** - Collectors work only online: a Mine, Lumber Camp, or Oil
  Well off the network idles "Offline: outside the network", going offline drops the unit in
  hand, and trucks serve only online collectors (hand collection still works). The HQ reaches
  further (coverage 19, link 22) and so does the Small Hub (9, 14); placing off the network warns
  "Offline: will not work".
- **2611191223-pin-and-truck-fixes** - Bug fixes: the top bar starts with no pins, a new game pins what
  it discovered and each later discovery pins itself, up to 8; trucks drive round their own Depot's
  shed instead of through it (only the apron is open to them).
- **2611191242-hq-unloading-pad** - Trucks unload on the HQ's landing pad: one at a time, backing on
  with the bed to the entrance and driving off nose first, the others waiting in a line in front of
  it. Unloading beside the HQ remains only when the pad's front cannot be reached.
- **2611191244-building-descriptions** - Every buildable building has a one-line description of what
  it makes or gives and what it needs, shown first in the build menu's tooltip (hidden for an
  undiscovered building).
- **2611191433-toasts-under-modals** - While a modal is open the notification stack (the trainer's
  card too) fades out and its timers hold, then fades back in when it closes, so no toast shows half
  covered beside the build menu or runs out unread behind it.
- **2611191454-building-table** - The buildings blueprint gains an "At a glance" table of every
  building's cost, draws, supplies, fuel, and recipes; `BuildingTableTests` rebuilds it from the
  code and fails when the blueprint's copy differs.
- **2611191751-crafting-queue** - Processors craft from a queue of up to four orders set in the
  building panel (recipe, quantity, Craft, and a removable row per order) and idle when it is
  empty, still drawing their capacity; the Electrolyzer, Brine Well, and Air Separator run without
  one.
- **2611241218-trucks-explored-paths** - Trucks path only through explored map cells, like the player;
  a collector reachable only through fog is unreachable until a map cell is newly explored.
- **2611241302-network-only-processing** - Every building works only while online: hand loading goes,
  with the input buffer and fuel slot; offline processors hold their cycle; the panel's Buffer and
  Burning rows lose their empty parts.
- **2611241306-destroy-refund-tooltip** - The Destroy button reads "Destroy" with its refund in a hover
  tooltip, and side panels hold their width whatever their content.
- **2611241311-collect-all-by-hand** - Holding E at a building collects its whole output buffer, every
  type, in one 0.5 s hold, as far as storage has room.
- **2611241315-recipe-requirements-line** - A dim line under a processor's Recipe picker spells out
  the chosen recipe, with inputs storage is short of in the danger color.
- **2611241331-salt-flats** - Deserts gain salt flats, a new terrain placed about one per 400 desert
  map cells, and the Brine Well stands only on them.
- **2611241358-hub-ranges** - The HQ's reach doubles, a Small Hub covers 11 and links 22 so two at full
  range leave no gap, and a Relay links 90 for 5 Wood, 10 Stone, 5 Iron.
- **2611241405-advanced-transfer-exclusions** - Advanced Material Transfer no longer carries Titanium
  Ore, Rare Earths, Uranium Ore, or Crystals; those come by hand or truck.
- **2611241419-power-provider-tuning** - The Water Wheel supplies 5 Power and the Burner Generator 35 at
  4 x 4; an old save's generator grows around its center or is refunded.
- **2611241427-fuel-speed** - Buildings that make things work 15% faster on Charcoal and 30% faster on
  Coal than on Wood; generators supply the same on any fuel.
- **2611241432-flat-network-overlay** - Network coverage draws as one even tint however many hubs
  overlap, online over offline, with the placement ghost on top.
- **2611241441-hold-to-repeat-steppers** - Up/down buttons step on press and repeat 15 a second after a
  0.5 s hold; the crafting quantity uses them and loses its Shift step.
- **2611242051-hold-mode** - A queueing processor can hold its output's stock at a target instead of
  running orders, chosen from a Mode picker in its panel.

## Adding a changeset

Copy the template below to `docs/changesets/<slug>.md`. When it is implemented, stamp its prefix,
move it to `completed/`, and add a one-line entry above.

```markdown
# <Title>

- Status: Proposed | Accepted | Implemented
- Id: <YYMMDDHHMM-slug> (assigned when Implemented)
- Depends-on: <id(s), or none>

## Summary

One or two sentences: what this changeset does.

## Motivation

Why we are making this change.

## Design

How it works: usage, construction, features, and key values (sizes, rates, costs).

## Code changes

The C# and asset or scene changes required: files, types, methods, and behavior.

## Blueprint updates

Which blueprint(s) change and how, so each reflects the new present after this lands.

## Verification

How we confirm it works: build, headless run, and the in-game check.
```
