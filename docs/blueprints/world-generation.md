# World Generation Blueprint

## Overview

How the world is stored and generated: chunks of map cells, the two kinds of world (bounded and
generated), and the generator that turns a seed and a planet into terrain, biomes, mountains,
rivers, and deposits, chunk by chunk as the player explores. The terrain model it fills is in
terrain.md; the deposits it places are in resources.md.

## Current state

### Chunks

- A `Chunk` is `Chunk.Size` x `Chunk.Size` = 64 x 64 map cells (256 x 256 world units) and holds
  each cell's terrain, biome, feature, and river flow. `Chunk.Of` converts a map cell coordinate to
  its chunk coordinate by floor division (so chunks run negative), and `Chunk.Origin` gives a
  chunk's first map cell. Chunk (0, 0) holds map cells 0 to 63 on each axis.
- The simulation stores chunks in a dictionary by chunk coordinate. Every terrain, biome,
  feature, and river read goes through it (`MapTerrainAt` and the others in terrain.md), and so do
  the debug edits.

### Worlds

`WorldSettings` picks the kind of world; `Simulation.World` reports it.

| Kind | Bounded | Built | Fog |
|---|---|---|---|
| TestMap | 16 x 16 map cells in chunk (0, 0) | the fixed test layout and its fields (terrain.md, resources.md) | no |
| Empty | 16 x 16 map cells in chunk (0, 0) | all Grassland grass, no features or fields | only if asked |
| Generated | no edge | chunk by chunk from a seed and a planet | always |

- A bounded world behaves as a fixed rectangle: `MapWidth`, `MapHeight`, `GridWidth`, and
  `GridHeight` give its size, `InBounds` and `MapInBounds` test against it, reads outside return
  the defaults (Grass, Grassland, no feature, no river), a river flowing off its edge connects, and
  the player is kept inside it. Its player spawns at the center, `(32, 0, 32)`.
- In a generated world every cell is in bounds and `MapWidth` and the other sizes throw. A chunk
  is generated the first time anything needs it: each step generates the player's chunk and the
  chunks within `LoadRadius` (1) of it, a 3 x 3 block, and any read or edit elsewhere generates
  that chunk on demand. Generated chunks are kept (their fields deplete, edits stick); nothing is
  unloaded yet. `ChunkCount` and `IsChunkGenerated` report what exists, and `ChunkGenerated`
  reports each new chunk.
- A generated chunk's deposits are added to the field list as it is generated.
- `new Simulation()` builds the test map; `new Simulation(WorldSettings.Generated(seed))` a
  generated world on the starter planet. `Debug.ResetToEmptyMap(fog)` and
  `Debug.ResetToGeneratedWorld(seed)` replace the whole world (clearing fields, buildings, the
  stockpile, launches, what is explored, and which resources are discovered) and put the player
  at the new spawn.
  `Debug.RevealAll()` explores every cell of every existing chunk.
- A world with fog starts with only the cells in sight of the spawn explored (simulation.md).
- The game runs a generated world from `--seed=<n>` (after the `--` separator), or a random seed.

### The generator

`WorldGenerator(seed, planet)` generates one chunk at a time. Everything is a pure function of the
seed, the planet, and the map cell's position, using the simulation's own seeded noise (`Noise`:
an integer hash, smooth value noise, fractal octaves, and ridged noise; engine-free and
deterministic), so chunks can be generated in any order and agree along their borders. A chunk is
generated with a 4-cell margin of its neighbors' cells so that every lookup across its border sees
what the neighbor chunk itself holds. Within a chunk, region site positions are computed once into
a small grid and each cell's river noise sign at most once, rather than per lookup. A chunk takes
about 1.5 ms in a Release build (about 6 ms in Debug, as the tests run); building a generator (its
climate bands and the spawn search, which generates the chunks around the spawn) about 25 ms in
Release (about 125 ms in Debug). Profiling shows no single hot spot: the time is the noise itself
and the nearest-site lookups, with the climate bands' 9,216-site sample about a fifth of building
a generator.

**Planet.** `PlanetDef` gives each biome a weight; a biome's share of the planet is its weight over
the total. Part A has one planet, `PlanetDefs.Starter`:

| Biome | Weight |
|---|---|
| Grassland | 25 |
| Forest | 20 |
| Highlands | 15 |
| Ocean | 12 |
| Desert | 10 |
| Swamp | 8 |
| Tundra | 6 |
| Volcanic | 4 |

**Biome regions.** Region sites sit on a grid every `RegionSpacing` (24) map cells, each nudged by a
hash; every map cell belongs to its nearest site, measured after a noise warp of up to about 7
cells, so region edges wander. A site's biome comes from five climate fields sampled at the site:
continentalness, temperature, moisture, ruggedness, and volcanic hot spots (fractal noise with
features 60 to 140 map cells across). Climate bands are carved in order, each taking the sites
highest on its key among those not yet taken: Ocean (low continentalness), Volcanic (hot spots),
Highlands (ruggedness), Tundra (cold), Desert (hot and dry), Swamp (wet), Forest (moist), and the
rest is Grassland. Each band's threshold is set from a fixed sample of 9,216 sites so the band takes
the planet's share. Neighbors follow the climate (tundra and desert sit at opposite ends of
temperature), regions are typically 15 to 40 cells across, and a biome often spans chunk borders.
Averaged over seeds, each biome's share is within a few points of its weight; any one area of a
few chunks varies, as large biomes do.

**Terrain.** Each cell takes its biome's base terrain, plus patches from a fine noise field:

| Biome | Base | Patches |
|---|---|---|
| Grassland | Grass | ShallowWater ponds |
| Forest | Grass | none |
| Highlands | Rock | Grass in the lower parts |
| Desert | Sand | Rock outcrops |
| Swamp | Mud | ShallowWater pockets |
| Tundra | Snow | Rock outcrops |
| Volcanic | Rock | Lava pools |
| Ocean | DeepWater | ShallowWater within 2 cells of land |

**Mountains.** In Highlands and Tundra, ridged noise above a threshold makes ranges, and a second
noise field breaks them into segments, so ranges always have passes. Grassland, Forest, and
Volcanic cells get a rare single peak (a hash below 0.003). Mountains only go on terrain that takes
features.

**Rivers.** River cells lie along the zero line of a river noise field (crossings between a cell
and its east or south neighbor), on land that takes features and is not a mountain, only where a
mask field allows, so rivers run as streams tens of cells long rather than map-wide walls. Flow uses
a hidden elevation field (fractal noise raised along the mountain ridges; the world stays flat):
each river cell flows to its lowest neighbor that is a river cell or water (water counting as
lowest) when that is lower than the cell; otherwise to its lowest river neighbor; and with no river
or water beside it, toward its lowest neighbor, which leaves it a Pool. The connection rules in
terrain.md give every shape, and rivers run on into lakes and the sea across chunk borders.

**Deposits.** Each biome has a field density and a weight per deposit type (`BiomeDef.FieldDensity`
and `FieldWeights`):

| Biome | Density | Deposits (weight) |
|---|---|---|
| Grassland | 0.04 | Wood (2), Stone (1), Iron Ore (1) |
| Forest | 0.30 | Wood (1) |
| Highlands | 0.07 | Iron Ore (3), Copper Ore (2), Coal (2), Stone (2) |
| Desert | 0.04 | Copper Ore (1), Crude Oil (2) |
| Swamp | 0.06 | Wood (1) |
| Tundra | 0.03 | Iron Ore (1) |
| Volcanic | 0.04 | Iron Ore (1), Stone (1) |
| Ocean | 0 | none |

A cell is a candidate when a hash falls below its biome's density, its terrain is walkable, it has
no feature, and it is not within 2 cells of the spawn; a candidate becomes a field only if none of
its 8 neighbors is a candidate with a lower hash, so fields never touch and paths stay open. A
second hash picks the type by weight, and the field takes the biome's amount for it.

### Spawn

- The spawn is chosen when the generator is built. The first candidate is the center of chunk
  (0, 0), map cell (32, 32); later candidates are cells within `SpawnSearchRadius` (96) map cells of
  it, picked by a hash of the seed and the attempt number, so the same seed always gets the same
  spawn.
- A candidate is accepted when it appears to have an exit: with the spawn placed there, a walk over
  open map cells (walkable terrain, no mountain, river, or field) reaches a cell at least
  `SpawnExitDistance` (40) cells away on either axis. The chunks around the candidate are generated
  for the check; the chosen candidate's chunks are handed to the world instead of being generated
  again. After `SpawnAttempts` (100) candidates without an exit, the last one is used.
  `SpawnAttemptsUsed` reports how many were tried (1 for nearly every seed: one seed in 200 needed
  a second candidate, none more). The search runs once, at world creation.
- The region whose site is nearest the spawn is Grassland, and every cell within
  `SpawnClearRadius` (10) map cells of it is Grassland grass with no mountain, river, or water. No
  field lies within `SpawnFieldGap` (2) cells of it.
- Resources are not placed for the player: the deposits around the spawn come from ordinary
  generation, so finding them is part of exploring.

## Related

- terrain.md (the terrain, biome, and feature model; the test map), resources.md (deposits and
  fields), simulation.md (the grid and the step), presentation.md (render tiles),
  docs/game-plan.md (planets and signature resources).
