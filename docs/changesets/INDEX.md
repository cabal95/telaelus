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
