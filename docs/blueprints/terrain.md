# Terrain Blueprint

## Overview

Terrain is a per-cell classification of the grid that decorates the ground and governs where the
player can walk and build.

## Current state

### Types and data

- `TerrainType` has three values: **Grass**, **Water**, **Mountain**. Grass is the default.
- The simulation holds a terrain grid (one `TerrainType` per cell) and exposes `TerrainAt(x, z)`,
  which returns Grass for out-of-bounds cells.
- The layout is a **fixed test layout** built in `Simulation.GenerateTerrain`: a water pond across
  cells x in `[2, 6]`, z in `[-4, 0]`, and a small mountain cluster at `(-6, 2)`, `(-6, 3)`,
  `(-5, 2)`. Everything else is grass. Procedural generation is not yet in.

### Passability

- `IsPassable(x, z)` is true only for grass; it gates building placement terrain (see buildings.md).
- `IsWalkable(x, z)` is `IsPassable` and no resource node and no building on the tile; it gates
  player movement. Water and mountain block movement, as do nodes (until depleted) and buildings
  (see resources.md, buildings.md, player.md).

### Rendering

- `TerrainView` builds one tile per grid cell; `TerrainTiles` renders the current placeholders:
  a flat colored tile per type (grass green, water blue, a grey mountain cone on a grass base).
- Water tiles add green shoreline strips on each land-facing edge (land is any non-water cell),
  previewing the grass-to-water transitions that real CC0 edge and corner tiles will provide.
- One tile is one world unit, centered on its cell. Real CC0 terrain tile models are pending; the
  expected set and conventions are in `src/assets/models/terrain/README.md`.

## Related

- simulation.md (the grid), player.md (movement blocking), presentation.md (rendering).
