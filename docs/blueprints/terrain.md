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

- `IsBuildable(x, z)`: the terrain is buildable and no feature blocks the fine cell. (Placement
  also needs the cell explored; see buildings.md.)
- `IsWalkable(x, z)`: the terrain is walkable, no feature blocks the fine cell, no resource field
  covers it (until depleted), and no building stands on it (see resources.md, buildings.md,
  player.md); or a bridge stands on it (below), whatever the terrain or river under it.

### Roads and bridges

- **Tiles.** Roads and bridges are 2 x 2 tiles on the even grid: a tile is named by its corner fine
  cell, both coordinates even (`Simulation.TileOf`), so tiles never overlap and always line up.
  `RoadTiles`, `Bridges` (each a `Bridge`: its tiles in order and whether it runs along X),
  `IsRoad`, `IsBridge`, and `IsPaved` (either) read them; `RoadVersion` counts every change.
- **A road tile** paves four cells that are each explored walkable land, with no building, field,
  or paving (`CanLayRoadTile`). It costs 1 Stone a cell, 2 on Sand, Snow, and Mud
  (`RoadTileCost`: 4 Stone on Grass, 8 on Mud).
- **A bridge** is a straight span of up to `MaxBridgeTiles` (6) tiles along X or Z. Every cell of
  every tile is explored, unbuilt, with no field or paving, and either walkable land, ShallowWater,
  or the channel of a Straight river running across the span (a North-South river takes a bridge
  along X); every tile has at least one water or channel cell; and the tiles just past both ends
  are walkable land, so a bridge always lands (`BridgeProblem` names what is wrong: "A bridge runs
  straight", "At most 12 cells long", "Not over open water", "It must land at both ends"). Its
  cells become walkable. It costs 3 Wood and 1 Stone a cell (`BridgeTileCost`: 12 Wood and 4 Stone
  a tile).
- **Speed.** On a paved cell the speed is `RoadSpeed` (1.5) in place of the terrain's
  (`CellSpeed`), for the player and for trucks.
- **Lines.** A drag from one cell to another (`RoadLine`): both snap to their tiles, and the line
  runs along X then Z, an L whose longer leg comes first, through every tile between, in order from
  the start; a bridge line (`BridgeLine`) must be straight. `PlanRoad` says for each tile whether
  it is already paved (free, skipped), ready (layable and paid for, in order), short (the stock ran
  out before it), or blocked; `TryLayRoad` lays the ready tiles and pays for them.
  `TryLayBridge` lays a whole span only when it is valid and the stock pays for it.
- **Removing** (`TryRemoveRoad`, `TryRemoveBridge`: every bridge with a tile on the line) refunds
  half the cost, rounded up per resource, past storage limits as building refunds are. A bridge
  with the player or a truck on it stays.
- **Buildings** are refused on any paved cell (`CanPlace`); the paving must be removed first.

### Biomes

`BiomeType`: Grassland (the default), Forest, Highlands, Desert, Swamp, Tundra, Volcanic, Ocean.
`MapBiomeAt` and `BiomeAt` read it (Grassland out of bounds). `BiomeDefs.For(type)` returns a
`BiomeDef` with its `BaseTerrain` and `Fields`, the deposits it offers with each one's field
amount (deposits are resource types of kind Deposit, see resources.md); a biome lists only what it
offers, following the signature resources in docs/game-plan.md:

| Biome | Base terrain | Fields |
|---|---|---|
| Grassland | Grass | Wood 250, Stone 250, Iron Ore 250 |
| Forest | Grass | Wood 500 |
| Highlands | Rock | Iron Ore 400, Copper Ore 300, Coal 400, Stone 400 |
| Desert | Sand | Copper Ore 250, Crude Oil 500 |
| Swamp | Mud | Wood 250 |
| Tundra | Snow | Iron Ore 250 |
| Volcanic | Rock | Iron Ore 400, Stone 400 |
| Ocean | DeepWater | none |

- A field placed without an amount takes its biome's amount for its type (see resources.md).
- Every tree that matters is a Wood field: a forest is Grass dense with large Wood fields, a
  grassland has a few smaller ones. Fields block movement until depleted, so a dense forest is a
  wall that clearing opens, and a cleared field shows the cell's terrain beneath.

### Test map

The bounded test map (see world-generation.md) is a fixed layout built in
`Simulation.GenerateTerrain` (map cells, x east, z south). Unit tests and most scenarios use it; the
game runs a generated world, whose terrain, biomes, mountains, rivers, and deposits come from the
generator described in world-generation.md.

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

- `TerrainView` draws the terrain in render tiles of 16 x 16 map cells, batched into one
  multimesh per model (see presentation.md); `TerrainTiles` lists each map cell's models from the
  generated terrain models (files and conventions in `src/Telaelus/assets/models/terrain/README.md`; the look and
  the ground levels in art-style.md). Only explored cells draw their models; unexplored cells are
  darkness (the fog overlay, presentation.md), so their terrain, mountains, and rivers are unseen.
- A plain cell draws its **ground tile**, `ground_<terrain>_<a|b|c>`: one of three variants at a
  90-degree yaw, both from the map cell's coordinates. Land tiles are faceted surfaces at y = 0
  with small flat details (grass patches, sand ripples, snow drifts, rock slabs and pebbles, mud
  puddles) and a skirt down to -0.1 on every side; water and lava tiles are surfaces at y = -0.1
  (lava has glowing cracks between crust plates). A mountain or river cell draws its mountain or
  river model instead, which covers the whole cell.
- **Transitions** follow draw priority: on each edge whose in-bounds neighbor has a higher
  priority, the cell draws that neighbor's `edge_<terrain>` piece, a strip with a ragged inner
  edge stopping short of the corners. Each corner draws the `corner_<terrain>` piece of the
  highest-priority terrain among the three cells around it (the two edge neighbors and the
  diagonal one), when that terrain outranks the cell, so transitions stay continuous where three
  terrains meet. Where a river flows in across the edge, the cell draws `edge_mouth_<terrain>`
  instead, whose strip leaves a gap so the channel meets it. Pieces are authored for the North edge
  and North-West corner and turned into place; one edge set per terrain (Grass, Snow, Sand, Mud,
  Rock, Lava; water never outranks) keeps transition art at N pieces rather than one per pair.
  Land pieces carry a skirt to -0.1, so a land strip in a water cell is a ragged shore.
- A **mountain** is a generated model from `MountainModels`: the Snow family on Snow ground and
  the Stone family on every other ground, one of three shapes at a deterministic 90-degree
  rotation per map cell. The model stays inside its map cell with every edge flat on the
  ground, so each mountain is a standalone peak and neighbors form a cluster. The six files are required (see
  `src/Telaelus/assets/models/terrain/README.md`).
- A **river** cell draws `river_<shape>_<bank terrain>` (`RiverPieces`): one model per shape and
  bank terrain (Grass, Sand, Snow, Mud, Rock), banks at y = 0 in that terrain's look and the sim's
  channel (the middle 2 x 2 fine cells plus a 2-wide arm per connection) sunk to a water surface at
  y = -0.1 with short sloped sides. Every connected edge has the same channel cross-section, so
  pieces join each other and meet water tiles flush. Each shape is authored with canonical
  connections (End: North; Straight: North and South; Bend: North and East; Junction: North, East,
  and South) and turned in 90-degree steps to the cell's connections.
- Ground pieces (tiles, edges, corners, river pieces) cast no shadows; mountains do.
- Every terrain model is required; a missing file throws (see presentation.md).

## Related

- simulation.md (the grid), player.md (movement), resources.md (fields and their amounts),
  buildings.md (placement), presentation.md (rendering), docs/game-plan.md (planets, signature
  resources, and the infrastructure rules roads, pipes, and bridges will read from terrain).
