# Biome and Terrain Model

- Status: Implemented
- Id: 2611111426-biome-terrain-model
- Depends-on: none

## Summary

Replaces the three hard-coded terrain types with a data-driven model: eight terrain types whose
rules (walkable, walk speed, buildable, edge draw order) live in a `TerrainDef` table, eight
biomes stored per map cell whose `BiomeDef` sets each biome's base terrain and field sizes, and
map features (Mountain, River) that sit on top of a map cell's terrain rather than replacing it.
The fixed test map is repainted to show every biome, and the long-term world design (planets,
signature resources, infrastructure rules) is recorded in the game plan.

## Motivation

Procedural generation is next, and it needs something to generate. Settling the vocabulary first
(what a biome is, what a terrain type is, what a feature is, which layer owns which rule) lets the
generator be a pure "fill the model" change. It also fixes the shape that roads, pipes and bridges
will build on, so they arrive as lookups rather than a redesign.

## Design

### Layers

| Layer | Grain | Answers | Set by |
|---|---|---|---|
| Biome | map cell | What kind of place is this? Base terrain, field sizes, later the generator's terrain mix and resource table | generator (test layout for now) |
| Terrain | map cell | What is the ground? Can I walk or build on it, and how fast do I move? | generator |
| Feature | map cell, on top of terrain | Is there a mountain or a river here? | generator |
| Resource fields | map cell | Finite deposits for extractors (unchanged) | generator |
| Infrastructure | fine cell | Roads, pipes, bridges (future, see Game plan below) | player |

Rules come from terrain and features only. A biome carries no gameplay rule of its own: it decides
what the generator puts in a map cell (terrain, features, fields, field sizes), and every walk or
build rule is then read from the terrain and the feature on it. This keeps the rules in two small
tables and makes a new biome mostly data.

### Decisions recorded

- **Flat world.** No elevation and no cliffs. A mountain range is the only hard land barrier.
- **Per-planet biomes.** Each planet weights the biomes differently (a frozen planet is mostly
  tundra, so it is the long-term uranium choice). Part A starts on one planet. The planet
  definition and its biome weights arrive with procedural generation, which is their first
  consumer.
- **Terrain is permanent.** After generation the player never changes terrain or features: no
  landfill, no converting cleared ground to dirt. Paving exists only as roads (future), which sit
  on the infrastructure layer, not in terrain.
- **Mountains and rivers are features, not terrain.** The ground under them is still Grass, Sand,
  Snow and so on, so a tundra has snow mountains, a desert river runs between sand banks, and a
  river can cross a biome edge (grass into snow) without changing what it is.
- **Trees.** Every tree that matters is a Wood field. A forest is Grass terrain dense with large
  Wood fields; a grassland has a few smaller ones. Because fields block movement until depleted,
  a dense forest is a natural wall that clearing opens up, and a cleared field shows the map
  cell's own terrain beneath (today's behavior). Decorative trees, when the decor overlays come,
  are small, sparse, never block and are never harvested, so there is no third kind of "tree you
  can't walk through or collect".

### Terrain types

`TerrainType` becomes: **Grass, Sand, Snow, Rock, Mud, ShallowWater, DeepWater, Lava** (Grass
stays value 0 and the default; Water becomes ShallowWater; Mountain moves to features).

`TerrainDefs.For(type)` returns a `TerrainDef` record:

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

- **Speed** multiplies `Player.Speed` while the player stands on a cell of that terrain. It is 1
  on unwalkable terrain, so a player teleported there can walk off.
- **Buildable** gates building placement. Fields are not gated by it (a swamp holds Wood fields
  on Mud); a field needs only a featureless map cell. Mud is unbuildable until a foundation
  mechanic exists; water buildings (pump) will add their own rule when they exist.
- **Takes features**: a mountain or river may sit only on walkable land.
- **Draw priority** decides transitions: on each edge where a neighbor has a higher priority, the
  cell draws an edge strip in the neighbor's color. Today's shoreline strips are the special case
  "land draws over water". Strips stop short of the corners; each corner gets a square in the
  color of the highest-priority terrain among the three cells around it (the two edge neighbors
  and the diagonal one) when it outranks the cell, so transitions stay continuous where three
  terrains meet. Where a river flows in across an edge, the strip's middle is left open so the
  channel meets the water. One edge set per terrain keeps future transition art at N assets, not
  N squared.

### Features

`MapFeature` is **None, Mountain, River** (None is value 0). A map cell holds at most one, and only
on terrain that takes features. A feature changes no terrain: `TerrainAt` and `MapTerrainAt`
still report the ground beneath. It adds its own blocking on top:

- **Mountain** blocks walking and building on all 16 fine cells of its map cell. It is drawn as a
  peak whose color follows the terrain beneath (grey on Rock, white-capped on Snow, green-grey on
  Grass, tan on Sand).
- **River** carves a **channel** of fine cells that block walking and building; the other fine
  cells are banks and behave like the map cell's terrain. The channel is drawn as water over the
  terrain, so a desert river has sand banks.
- `IsRiverChannel(x, z)` marks flowing water, for the future water wheel; a 2-wide channel keeps a
  future bridge short.

### River flow and shape

A river cell stores one value besides the feature: its **flow**, the edge its water leaves by (a
`Facing`). Everything else is derived, never stored, so it cannot contradict itself.

- **Connections.** A river cell connects on its flow edge when the neighbor there is a river
  cell, water (ShallowWater or DeepWater), or off the map; it also connects on every edge whose
  neighbor is a river cell flowing into it. A flow edge facing anything else (dry land, a
  mountain, lava) does not connect, so a river can be painted cell by cell in any order and a
  dead end simply stops.
- **Parallel rivers stay separate.** Two river cells side by side connect only if one flows into
  the other, so neighboring rivers never merge into a block.
- **Shape.** `RiverShapeAt(mapX, mapZ)` reads the connections: **End** (one), **Straight** (two
  opposite), **Bend** (two adjacent), **Junction** (three), **Cross** (four), or **Pool** (none).
  A source, a mouth into water and a mouth off the map edge are all End or Straight by this
  count; flow says which way the water goes.
- **Channel mask.** The middle 2 x 2 fine cells are always channel, and an arm 2 fine cells wide
  runs from the center to each connected edge.
- **Bridges** (future) are allowed only on a Straight river cell, crossing at right angles to its
  flow; bends, junctions, crossings and ends reject them. The rule lives in the game plan until
  bridges exist; the shape it reads exists now.

### Biomes

`BiomeType`: **Grassland, Forest, Highlands, Desert, Swamp, Tundra, Volcanic, Ocean** (Grassland
is value 0 and the default). The simulation stores one per map cell. `BiomeDefs.For(type)` returns
a `BiomeDef` with its `BaseTerrain` and its `Fields`: the resource types the biome offers, each
with its field amount. A biome lists only what it actually offers:

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

A field placed without an explicit amount takes its biome's amount for that type, or the global
default of 250 when the biome does not list the type (only Debug placement can do that; the
generator will place only listed types, so an empty list means no fields). The generator's terrain
mix, feature frequency, field density and the signature resources (oil, uranium and so on) are
added to `BiomeDef` by the changesets that first use them.

Larger fields look larger: a Wood field's tree height scales with its initial amount, 2.5 units at
250 up to 3.5 units at 500 or more (still capped to the slot footprint).

### Simulation API

- `MapBiomeAt(mapX, mapZ)`, `BiomeAt(x, z)`; out-of-bounds reads return Grassland.
- `MapFeatureAt(mapX, mapZ)`, `FeatureAt(x, z)`; out-of-bounds reads return None.
  `RiverFlowAt(mapX, mapZ)` (null when not a river), `RiverConnects(mapX, mapZ, edge)`,
  `RiverShapeAt(mapX, mapZ)`, `IsRiverChannel(x, z)`, and `EdgeOffset(facing)` (North is -Z,
  East +X, South +Z, West -X).
- `IsPassable(x, z)` is renamed `IsBuildable(x, z)`: the terrain is buildable and no feature blocks
  the fine cell.
- `IsWalkable(x, z)`: the terrain is walkable, no feature blocks the fine cell, and the field and
  building checks as now.
- A field may not be placed on a map cell with a feature.
- Player movement multiplies speed by the current cell's `TerrainDef.Speed` (a river bank is the
  bank's terrain speed).
- `Debug.SetTerrain` keeps its shape and throws if the cell holds a feature the new terrain cannot
  take. `Debug.SetBiome(mapX, mapZ, biome)` and `Debug.SetFeature(mapX, mapZ, feature, flow)` are
  added: a River requires a flow and other features reject one, and a feature on terrain that
  cannot take one, or on a field, throws. `Debug.ResetToEmptyMap` also
  resets biomes to Grassland and features to None. `Debug.PlaceField`'s amount becomes optional
  (biome default) and it throws on a feature cell.

### Test map

Repainted to show every biome while keeping every feature the current tests rely on (the pond, the
mountain cluster, all eight fields, the clear spawn area). Map cells, x east and z south:

| Region | Cells | Biome / terrain / feature |
|---|---|---|
| Default | everything else | Grassland / Grass |
| Forest | x 0 to 5, z 0 to 6 | Forest / Grass (Wood fields at (3, 4), (4, 3), (2, 5) become 500) |
| Swamp | x 6 to 10, z 0 to 2 | Swamp / Mud, ShallowWater at (8, 1), (9, 1) |
| River | (7, 0) to (7, 3) flow South; (7, 4), (8, 4) flow East; (9, 4) flows South | river through Mud in the swamp, then Grass, into the pond (a source, a straight run, two bends and a mouth) |
| Pond | x 9 to 11, z 5 to 6 | Grassland / ShallowWater (as now) |
| Tundra | x 13 to 15, z 0 to 3 | Tundra / Snow, Mountain at (14, 1), (15, 1) |
| Desert | x 13 to 15, z 5 to 12 | Desert / Sand, Rock at (14, 8), river at (13, 10), (14, 10), (15, 10) flowing East off the map edge |
| Highlands | x 0 to 5, z 8 to 12 | Highlands / Rock, Mountain at (4, 9), (4, 10), (5, 9) as now; Ore field (4, 12) becomes 400 |
| Grass mountain | (11, 12) | Grassland / Grass, Mountain |
| Ocean | x 0 to 9, z 13 | Ocean / ShallowWater (shore) |
| Ocean | x 0 to 9, z 14 to 15 | Ocean / DeepWater |
| Volcanic | x 12 to 15, z 13 to 15 | Volcanic / Rock, Lava at (13, 14), (14, 14) |

Totals become 1150 Ore and 1750 Wood, still enough for a launch.

### Game plan

`docs/game-plan.md` gains a **World: planets, biomes and terrain** section holding the long-term
intent that is not yet code: planet biome weights; each biome's signature resources (Grassland
wood, stone, iron; Forest wood; Highlands iron, copper, coal; Desert oil, sand, copper; Swamp
biomass, sulfur; Tundra rare metals, uranium; Volcanic sulfur, crystals, geothermal; Ocean water,
later offshore oil); finite fields versus terrain-sourced resources (water, sand, geothermal from
pumps and taps on the right terrain); and the infrastructure rules roads, pipes and bridges will
read from terrain and features (roads on any walkable land, costlier on Sand, Snow and Mud; pipes
on land, ShallowWater and river channels; bridges over ShallowWater and over Straight river cells
only, at right angles to the flow; DeepWater, Lava and Mountain block all three).

## Code changes

- `src/Game.Sim/TerrainType.cs`: the eight values.
- `src/Game.Sim/TerrainDefs.cs` (new): `TerrainDef` record (`Walkable`, `Speed`, `Buildable`,
  `TakesFeatures`, `DrawPriority`) and the table.
- `src/Game.Sim/MapFeature.cs`, `src/Game.Sim/RiverShape.cs` (new): the enums.
- `src/Game.Sim/BiomeType.cs`, `src/Game.Sim/BiomeDefs.cs` (new): the enum, `BiomeDef` record
  (`BaseTerrain`, `Fields`, `FieldAmount(ResourceType)` with the global fallback) and the table.
- `src/Game.Sim/SimulationTerrain.cs` (new partial of `Simulation`, taking over its terrain
  section): terrain, biome, feature and river flow arrays and reads; derived river connections,
  shape and channel mask; `IsBuildable` and `IsWalkable` with feature blocking; the terrain speed
  multiplier; the repainted `GenerateTerrain` and `SpawnInitialFields`.
- `src/Game.Sim/Simulation.cs`: allocates the layers, `CanPlace` reads `IsBuildable`, movement
  applies the terrain speed, and fields default to their biome's amount.
- `src/Game.Sim/SimulationDebug.cs`: `SetBiome`, `SetFeature`, the `SetTerrain` check, reset
  changes, optional field amount.
- `src/Game/Presentation/TerrainTiles.cs`: a color per terrain, generalized priority edge strips
  (replacing the shoreline-only code), mountain peaks tinted by the terrain beneath, river channel
  quads over the terrain.
- `src/Game/Presentation/NodeModels.cs` and `ResourceFieldViews.cs`: Wood height from initial
  amount.
- `src/Game/Presentation/TerrainView.cs` and `GameRoot.cs`: `MapChanged` marks the terrain dirty
  and it rebuilds once on its next frame (processing while paused), instead of rebuilding on every
  change. A scenario's setup makes many map edits in one frame; rebuilding the whole map for each
  one left enough discarded meshes and materials that Godot crashed while quitting after the
  headless `terrain-types` run.
- `src/Game/Testing/ScenarioCommand.cs`, `CommandExecutor.cs`: `changeTiles` takes optional
  `type`, `biome`, `feature` and `flow` (at least one of the first three; `flow` required with
  River; applied biome, then terrain, then feature, so one step can paint a cell and put a
  feature on it); `placeResource`'s `amount` is optional.
- `src/Game/Testing/StateSnapshot.cs`: `player.terrain`, `player.biome` and `player.feature`.
- `src/Game.Integration.Tests/scenario.schema.json`: the terrain, biome and feature enums and the
  new `changeTiles` and `placeResource` shapes.
- Existing scenarios and sim tests that name `Water` or `Mountain` terrain or assume 250 everywhere
  are updated.

## Blueprint updates

- **terrain.md**: rewritten around the layers: the terrain table and its rules, features (Mountain,
  River, flow, derived connections and shape, the channel mask), biomes and `BiomeDef`, the repainted test layout, and priority edge
  rendering with tinted peaks and river channels. Overview names biomes and features.
- **resources.md**: fields take their biome's amount by default and may not sit on a feature cell;
  the test map's field amounts and totals; Wood height scales with initial amount.
- **player.md**: movement speed multiplied by the terrain under the player; water, lava, mountains
  and river channels block.
- **buildings.md**: placement reads `IsBuildable` (Grass, Sand, Snow, Rock with no blocking
  feature; river banks are buildable).
- **presentation.md**: the terrain view rebuilds once on the frame after `MapChanged`.
- **simulation.md**: the per-map-cell layers (terrain, biome, feature).
- **testing.md**: `changeTiles` biome and feature arguments, optional field amount, and the new
  snapshot fields.
- **INDEX.md**: the terrain line names biomes and features.

## Verification

- Sim unit tests (new or changed):
  - Every `TerrainType` and `BiomeType` has a def (table completeness).
  - Walk and build rules per terrain, read through `IsWalkable` and `IsBuildable`.
  - Player speed on Sand and Mud is the multiplied speed; on Grass unchanged.
  - Mountain on Grass and on Snow: all 16 fine cells block, and `TerrainAt` still reports the
    terrain beneath.
  - River connections and shape: Pool, End (source, mouth into water, mouth off the map), Straight,
    Bend, Junction (two rivers flowing into one), a flow edge facing dry land not connecting, and
    two parallel rivers in adjacent columns staying separate (both Straight, no sideways arms).
  - River channel mask follows the connections; channel cells block, bank cells allow walking and
    building and report their terrain, on Grass, Sand and Snow.
  - The test map's river shapes (source, straight, bends, mouths) are as laid out.
  - `SetFeature` River without a flow, or another feature with one, throws; on water or lava throws; `SetTerrain` to water under a feature throws; fields on
    feature cells throw; `ResetToEmptyMap` clears biomes and features.
  - A field placed without an amount takes its biome's amount, or 250 for a type the biome does
    not list; Ocean lists no fields; the test map totals (1150 Ore,
    1750 Wood).
  - Existing placement tests still pass against the repainted map.
- Scenario `terrain-types` (new): paints a strip of each terrain, a mountain on snow and a river
  crossing from grass into sand, screenshots it, aims the ghost at a channel cell (`placeable`
  false) and a bank cell (`placeable` true), and checks `player.terrain`, `player.biome` and
  `player.feature` after teleports.
- `dotnet test src/Game.Sim.Tests` and `dotnet test src/Game.Integration.Tests` pass.
- In game: the boot map shows every biome, transitions draw the higher-priority terrain over the
  lower, snow mountains read as snowy, the swamp river and the desert river show their own banks,
  and walking onto sand feels slower.
