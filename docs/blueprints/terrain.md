# Terrain Blueprint

## Overview

The world's ground is described per map cell in three layers: **terrain** (the ground itself),
**biome** (what kind of place the cell is), and **feature** (a mountain or river on top of the
terrain). Terrain and features govern where the player can walk and build; biomes decide what a
cell holds and carry no rules of their own.

## Current state

### Layers

- The simulation holds one `TerrainType`, one `BiomeType`, and one `MapFeature` per map cell (see
  simulation.md for the two grid levels), plus a river flow for river cells.
- Rules come from terrain and features only. A biome decides what goes in a map cell (its base
  terrain and its fields); every walk or build rule is read from the terrain and the feature on it.
- The world is flat: no elevation and no cliffs. A mountain range is the only hard land barrier.
- Terrain and features are permanent once the map is made: the player never changes them (no
  landfill, no converting cleared ground). Only `Simulation.Debug` edits them.

### Terrain types

`TerrainType`: Grass (the default), Sand, Snow, Rock, Mud, ShallowWater, DeepWater, Lava.
`TerrainDefs.For(type)` returns a `TerrainDef`:

| Terrain | Walkable | Speed | Buildable | Takes features | Draw priority |
|---|---|---|---|---|---|
| Grass | yes | 1.0 | yes | yes | 6 |
| Snow | yes | 0.75 | yes | yes | 5 |
| Sand | yes | 0.75 | yes | yes | 4 |
| Mud | yes | 0.6 | no | yes | 3 |
| Rock | yes | 1.0 | yes | yes | 2 |
| Lava | no | 1.0 | no | no | 1 |
| ShallowWater | no | 1.0 | no | no | 0 |
| DeepWater | no | 1.0 | no | no | 0 |

- **Speed** multiplies the player's speed while they stand on the terrain (see player.md). It is 1
  on unwalkable terrain so a player teleported there can walk off.
- **Buildable** gates building placement (see buildings.md).
- **Takes features**: a mountain or river may sit only on walkable land.
- **Draw priority** orders transitions (see Rendering).
- `MapTerrainAt(mapX, mapZ)` and `TerrainAt(x, z)` (a fine cell's map cell) read terrain;
  out-of-bounds reads return Grass. A feature never changes what they report.

### Features

`MapFeature`: None (the default), Mountain, River. A map cell holds at most one feature, only on
terrain that takes features, and never with a resource field. `MapFeatureAt` and `FeatureAt` read
it (None out of bounds). A feature adds its own blocking on top of the terrain's rules:

- A **Mountain** blocks all 16 fine cells of its map cell.
- A **River** blocks only its **channel**; the other fine cells are banks and behave like the map
  cell's terrain.

### Rivers

- A river cell stores its **flow** (`RiverFlowAt`, a `Facing`: the edge its water leaves by;
  North is -Z, East +X, South +Z, West -X, per `Simulation.EdgeOffset`). Everything else is derived.
- **Connections** (`RiverConnects(mapX, mapZ, edge)`): a river cell connects on its flow edge when
  the neighbor there is a river cell, water (ShallowWater or DeepWater), or off the map; and on
  every edge whose neighbor is a river cell flowing into it. A flow edge facing dry land, a
  mountain, or lava does not connect, so a river can be painted in any order and a dead end
  simply stops. Two rivers side by side connect only if one flows into the other, so parallel
  rivers stay separate.
- **Shape** (`RiverShapeAt`): Pool (no connections), End (one), Straight (two opposite), Bend (two
  adjacent), Junction (three), Cross (four).
- **Channel** (`IsRiverChannel(x, z)`): the middle 2 x 2 fine cells of a river cell, plus a 2-wide
  arm from the center to each connected edge. It marks flowing water, as distinct from still
  ShallowWater.

### Walk and build rules

- `IsBuildable(x, z)`: the terrain is buildable and no feature blocks the fine cell.
- `IsWalkable(x, z)`: the terrain is walkable, no feature blocks the fine cell, no resource field
  covers it (until depleted), and no building stands on it (see resources.md, buildings.md,
  player.md).

### Biomes

`BiomeType`: Grassland (the default), Forest, Highlands, Desert, Swamp, Tundra, Volcanic, Ocean.
`MapBiomeAt` and `BiomeAt` read it (Grassland out of bounds). `BiomeDefs.For(type)` returns a
`BiomeDef` with its `BaseTerrain` and `Fields`, the resource types it offers with each one's field
amount; a biome lists only what it offers:

| Biome | Base terrain | Fields |
|---|---|---|
| Grassland | Grass | Ore 250, Wood 250 |
| Forest | Grass | Wood 500 |
| Highlands | Rock | Ore 400 |
| Desert | Sand | Ore 250 |
| Swamp | Mud | Wood 250 |
| Tundra | Snow | Ore 250 |
| Volcanic | Rock | Ore 400 |
| Ocean | DeepWater | none |

- A field placed without an amount takes its biome's amount for its type (see resources.md).
- Every tree that matters is a Wood field: a forest is Grass dense with large Wood fields, a
  grassland has a few smaller ones. Fields block movement until depleted, so a dense forest is a
  wall that clearing opens, and a cleared field shows the cell's terrain beneath.

### Test map

The world is a fixed test layout built in `Simulation.GenerateTerrain` (map cells, x east, z
south). Procedural generation is not yet in.

| Region | Cells | Biome / terrain / feature |
|---|---|---|
| Default | everything else | Grassland / Grass |
| Forest | x 0 to 5, z 0 to 6 | Forest / Grass |
| Swamp | x 6 to 10, z 0 to 2 | Swamp / Mud, ShallowWater at (8, 1), (9, 1) |
| River | (7, 0) to (7, 3) flow South; (7, 4), (8, 4) flow East; (9, 4) flows South | through the swamp's mud, then grass, into the pond |
| Pond | x 9 to 11, z 5 to 6 | Grassland / ShallowWater |
| Tundra | x 13 to 15, z 0 to 3 | Tundra / Snow, Mountain at (14, 1), (15, 1) |
| Desert | x 13 to 15, z 5 to 12 | Desert / Sand, Rock at (14, 8), river at (13, 10) to (15, 10) flowing East off the map edge |
| Highlands | x 0 to 5, z 8 to 12 | Highlands / Rock, Mountain at (4, 9), (4, 10), (5, 9) |
| Grass mountain | (11, 12) | Grassland / Grass, Mountain |
| Ocean | x 0 to 9, z 13 | Ocean / ShallowWater (shore) |
| Ocean | x 0 to 9, z 14 to 15 | Ocean / DeepWater |
| Volcanic | x 12 to 15, z 13 to 15 | Volcanic / Rock, Lava at (13, 14), (14, 14) |

The center map cells around the spawn are clear Grassland grass.

### Rendering

- `TerrainView` builds one tile per map cell; `TerrainTiles` renders the current placeholders, each
  4 x 4 world units: a flat tile in its terrain's color (Lava glows).
- **Transitions** follow draw priority: on each edge whose in-bounds neighbor has a higher
  priority, the cell draws a thin strip in the neighbor's color, stopping short of the corners.
  Each corner gets a square in the color of the highest-priority terrain among the three cells
  around it (the two edge neighbors and the diagonal one), when that terrain outranks the cell,
  so transitions stay continuous where three terrains meet. Where a river flows in across the
  edge, the strip's middle is left open so the channel meets the water. One edge set per terrain keeps
  future transition art at N assets rather than one per pair.
- A **mountain** is a generated model from `MountainModels`: the Snow family on Snow ground and
  the Stone family on every other ground, one of three shapes at a deterministic 90-degree
  rotation per map cell. The model stays inside its map cell with every edge flat on the
  ground, so each mountain is a standalone peak and neighbors form a cluster. The six files are required (see
  `src/Game/assets/models/terrain/README.md`).
- A **river** is water laid over its channel fine cells, above the edge strips, so its banks show
  the cell's own terrain.
- Ground tiles are still colored placeholders; mountains are not. The model conventions are in
  `src/Game/assets/models/terrain/README.md`.

## Related

- simulation.md (the grid), player.md (movement), resources.md (fields and their amounts),
  buildings.md (placement), presentation.md (rendering), docs/game-plan.md (planets, signature
  resources, and the infrastructure rules roads, pipes, and bridges will read from terrain).
