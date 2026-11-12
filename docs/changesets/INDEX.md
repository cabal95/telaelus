# Changesets

A changeset is one coherent unit of change: what we are adding or altering, the code it requires,
and the blueprint updates it entails. A changeset in progress lives here as `<slug>.md`; once
implemented it is stamped `YYMMDDHHMM-slug.md` and moved to `completed/`. (The lifecycle and naming
rules are in ../development.md.)

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
