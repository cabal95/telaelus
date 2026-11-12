# Blueprints

A blueprint is a living description of the current state of one area of the project: how it works
and the decisions behind it, right now. Blueprints are present tense with no history, and they
change only through changesets. (The process is described in ../development.md.)

Read this index to find the right area, then open that blueprint for the detail.

## Areas

- **[simulation.md](simulation.md)** - The authoritative simulation framework: the fixed-timestep
  loop and clock, the intent-and-command input seam, the two-level grid (fine cells and map
  cells), and the multiplayer-ready architecture posture.
- **[world-generation.md](world-generation.md)** - Chunks, bounded and generated worlds, and the
  generator: planet weights, climate-driven biome regions, terrain patches, mountain ranges,
  rivers, deposits, the spawn, and determinism.
- **[terrain.md](terrain.md)** - The per-map-cell layers (terrain types and their rules, biomes,
  and mountain and river features with river flow and channels), the walk and build rules, the
  fixed test layout, and tile rendering with priority transitions.
- **[resources.md](resources.md)** - Resource types, the map-cell resource fields and their
  depletion stages, the global stockpile, hand-harvesting, and field rendering.
- **[buildings.md](buildings.md)** - Building types, footprints, and recipes, anchor and facing
  placement, extractor work areas, the production chain, the ship launch win, and the build UI.
- **[player.md](player.md)** - The player's simulation state and movement, camera-relative input,
  and the on-screen character and follow camera.
- **[presentation.md](presentation.md)** - The presentation layer: the frame loop and view-sync
  pattern, the views, the asset and model pipeline, and the renderer.
- **[art-style.md](art-style.md)** - How every asset looks and is built: the low-poly flat-color
  look, the view assets are judged from, the shared palette, scale, ground levels and cell-model
  rules, variants, sources, and naming.
- **[testing.md](testing.md)** - The engine-free sim unit tests and the seam check, JSON
  scenarios (commands, snapshot, running, output), the headless integration tests, and the live
  control channel with pause and frame stepping.

## Adding a blueprint

Copy the template below to `docs/blueprints/<area>.md`, fill it in, and add a line for it under
Areas.

```markdown
# <Area> Blueprint

## Overview

What this area is and its role in the game.

## Current state

The authoritative description of how it works today: types, data, behavior, and key values.

## Related

Links to related blueprints.
```
