# Terrain Blueprint

## Overview

Terrain is a per-map-cell classification of the world that decorates the ground and governs where
the player can walk and build.

## Current state

### Types and data

- `TerrainType` has three values: **Grass**, **Water**, **Mountain**. Grass is the default.
- The simulation holds one `TerrainType` per map cell (see simulation.md for the two grid levels).
  `MapTerrainAt(mapX, mapZ)` reads a map cell; `TerrainAt(x, z)` reads a fine cell as its map
  cell's terrain. Out-of-bounds reads return Grass.
- The layout is a **fixed test layout** built in `Simulation.GenerateTerrain`, in map cells: a
  water pond across map cells x in `[9, 11]`, z in `[5, 6]`, and a small mountain cluster at
  `(4, 9)`, `(4, 10)`, `(5, 9)`. Everything else is grass, including the center map cells where the
  player spawns. Procedural generation is not yet in.

### Passability

- `IsPassable(x, z)` (fine cell) is true only for grass; it gates building placement terrain (see
  buildings.md).
- `IsWalkable(x, z)` is `IsPassable`, no resource field covering the cell, and no building on it;
  it gates player movement. Water and mountain block movement, as do fields (until depleted) and
  buildings (see resources.md, buildings.md, player.md).

### Rendering

- `TerrainView` builds one tile per map cell, centered on it; `TerrainTiles` renders the current
  placeholders, each 4 x 4 world units: a flat colored tile per type (grass green, water blue, a
  grey mountain cone on a grass base).
- Water tiles add green shoreline strips on each land-facing map-cell edge (land is any non-water
  cell), previewing the grass-to-water transitions that real CC0 edge and corner tiles will
  provide.
- Real CC0 terrain tile models are pending; the expected set and conventions are in
  `src/assets/models/terrain/README.md`.

## Related

- simulation.md (the grid), player.md (movement blocking), presentation.md (rendering).
