# Procedural World

- Status: Implemented
- Id: 2611112100-procedural-world
- Depends-on: none

## Summary

Adds a generated world made of chunks of 64 x 64 map cells, created on demand as the player
explores, from one seed and a planet definition. Biomes form large regions that cross chunk
borders freely; terrain, mountain ranges, rivers, and deposits are generated within them; the
player spawns on open Grassland and explores to find deposits. The game runs a generated world, while the fixed test map
stays for tests. The terrain view draws in render tiles of 16 x 16 map cells near the camera,
batched and culled per tile, and the HUD shows the player's biome, chunk, and map cell for
checking seams while exploring.

## Motivation

The fixed 16 x 16 test layout was the scaffold for the world model. With terrain, biomes,
features, and deposits in place, generation turns it into a game you can replay and explore, and
gives the biomes' signature resources a reason to go looking for them. Chunks keep generation and
rendering proportional to where the player is rather than to the size of the world.

## Design

### Chunks and worlds

- A **chunk** is 64 x 64 map cells (256 x 256 world units). Chunk coordinates are the map cell
  coordinates divided by 64, rounded down, so they run negative as well as positive. Chunk (0, 0)
  holds map cells 0 to 63 on each axis.
- The world stores terrain, biome, feature, and river flow per chunk (a dictionary from chunk
  coordinates to the chunk's arrays) instead of one fixed array.
- There are two kinds of world:
  - **Bounded:** a fixed rectangle of map cells. The test map (16 x 16, inside chunk (0, 0)) and
    the empty debug map are bounded, and behave exactly as today: outside the rectangle is out of
    bounds, reads return the defaults, the player is kept inside, and a river's flow off the edge
    connects.
  - **Generated:** unbounded in every direction. Every cell is in bounds; a chunk is generated the
    first time anything needs it.
- Generation is a pure function of the seed, the planet, and the chunk coordinates, so the order in
  which chunks are generated never changes what they hold. Once generated, a chunk is world state
  (its fields deplete, its terrain can be edited by debug commands) and is kept; unloading chunks is
  later work.
- Each step the simulation makes sure the player's chunk and the chunks within one chunk of it (a
  3 x 3 block) are generated, so nothing the player can reach is missing. A query anywhere else
  (a far teleport, a debug edit) generates that chunk on demand.
- A chunk's deposits are added to the field list when the chunk is generated.
- `Simulation.ChunkGenerated` reports each new chunk so views can draw it; `MapChanged` keeps
  reporting edits.

### The generator

`WorldGenerator(seed, PlanetDef)` builds one chunk at a time from noise functions of world map
cell coordinates. All noise is the simulation's own seeded value noise with fractal octaves
(engine-free and deterministic), so it can run in the unit tests.

**Planet.** A new `PlanetDef` gives each biome a weight. Part A has one, the starter planet:

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

**Biome regions.** Region sites sit on a grid every 24 map cells, each nudged by a hash, and every
map cell belongs to its nearest site, measured after a gentle noise warp so region edges wander
instead of running straight. A region's biome comes from climate fields sampled at its site:
continentalness (low means Ocean), temperature, moisture, ruggedness, and volcanic hot spots, with
features 60 to 140 map cells across. Climate bands are carved in order, each taking the sites
highest on its key among those not yet taken (Ocean, Volcanic, Highlands, Tundra cold, Desert hot
and dry, Swamp wet, Forest moist, and Grassland for the rest), with each threshold set from a fixed
sample of 9,216 sites so the band takes the planet's share. Neighbors follow the climate (tundra does
not border desert), and the mix matches the planet on average over seeds; any one area of a few
chunks varies, as large biomes do (with larger climate features the variation was much wider, so
they were shrunk during tuning). Regions are typically 15 to 40 map cells across, so a biome often
spans chunk borders.

**Terrain.** Each cell takes its biome's base terrain, plus patches from a fine noise field:

| Biome | Base | Patches |
|---|---|---|
| Grassland | Grass | rare ShallowWater ponds |
| Forest | Grass | none |
| Highlands | Rock | Grass in the lower parts |
| Desert | Sand | Rock outcrops |
| Swamp | Mud | ShallowWater pockets |
| Tundra | Snow | Rock outcrops |
| Volcanic | Rock | Lava pools |
| Ocean | DeepWater | ShallowWater within 2 cells of land |

**Mountains.** A ridged noise field above a threshold makes mountain ranges in Highlands and Tundra
regions; a second field breaks each range into segments so ranges always have passes. Elsewhere on
land (not Swamp or Desert), a rare hash places a single peak. Mountains only go on terrain that
takes features.

**Rivers.** A hidden elevation field (fractal noise, raised along the mountain ridges) exists only
to route water; the world stays flat. River cells lie along the zero line of a separate river noise
field (a cell is a river where the field changes sign between it and its east or south neighbor),
only on land that takes features and never on mountains, and only where a mask field allows, so
rivers are streams tens of cells long rather than map-wide walls (rivers cannot be crossed until
bridges exist, and long ones cut the starting area apart during tuning). Each river cell flows to
its lowest neighbor that is a river cell or water (water counting as lowest) when that is lower
than the cell; otherwise to its lowest river neighbor; and with neither beside it, toward its
lowest neighbor, which leaves it a Pool. The existing connection rules then give every shape, and
rivers run on into the sea or a lake across chunk borders.

**Deposits.** `BiomeDef` gains a field density (the chance a free map cell holds a field) and a
weight per deposit type; a hash per map cell decides whether a field goes there and which type.
Fields go only on cells without a feature, on terrain that is walkable, and never next to another
field (a gap of at least one cell keeps paths open):

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

Amounts stay the biome's existing per-type amounts.

### Spawn

The player starts on open ground with a way out; resources are not placed for them. Finding
deposits is part of exploring, so a start may be rich or sparse.

- The spawn is chosen when the generator is built: first the center of chunk (0, 0), then cells
  within 96 map cells of it picked by a hash of the seed and the attempt, so it stays a pure
  function of the seed.
- A candidate is accepted when it appears to have an exit: with the spawn placed there, a walk over
  open map cells (walkable terrain, no mountain, river, or field) gets at least 40 cells away on
  either axis. The chunks around a candidate are generated for the check, and the chosen one's are
  handed to the world rather than generated twice. After 100 candidates without an exit, the last
  one is used. This replaces a test that sampled 20 seeds for reachability: every world now checks
  its own spawn. Measured over 200 seeds, one needed a second candidate and none more; building a
  generator takes about 125 ms in Debug (about 25 ms in Release). A long search would be a short freeze at world creation, which a
  loading screen will cover later.
- The region whose site is nearest the spawn is Grassland, and every cell within 10 map cells of it
  is Grassland grass with no mountain, river, or water. No field is placed within 2 cells of it.
- Deposits near the spawn come from ordinary generation, like anywhere else.

### The game, tests, and scenarios

- `new Simulation()` still builds the bounded test map, so every existing test keeps its exact
  layout. `new Simulation(WorldSettings.Generated(seed))` builds a generated world on the starter
  planet; `Debug.ResetToEmptyMap()` still gives the bounded empty map.
- The game runs a generated world. Its seed comes from `--seed=<n>` on the command line, or a random
  one; the HUD's diagnostics line shows the seed so a world can be reproduced.
- `initMap` takes an optional `generate` seed (`{ "initMap": { "generate": 1234 } }`) for a
  generated world; without it, it builds the bounded empty map as today.
- The state snapshot adds `world` (kind, seed, and the generated chunk count) and the player's
  chunk.

### Rendering

Generation and storage work in chunks; drawing works in smaller **render tiles** of 16 x 16 map
cells (64 x 64 world units, 16 tiles per chunk), so what is built and drawn follows the camera
closely without rebuilding anything per frame.

- `TerrainView` keeps one child per render tile. Each frame it works out the tiles that overlap
  the camera's view on the ground plus a one-tile margin, builds the ones that are missing (whose
  chunk is generated), and frees tiles that have fallen two tiles outside it. At the default zoom
  that is about 6 to 12 tiles. A freed tile's chunk stays generated in the sim and is drawn again
  on return.
- A tile is batched: `TerrainTiles` produces a list of placements (model path and transform) per
  cell with the same rules as today, and the tile groups them by model into one
  `MultiMeshInstance3D` per model it uses, with the mesh taken from that model's glTF. Godot skips
  a tile's batches whenever the tile is outside the camera's view, so only tiles on screen are
  drawn. Ground pieces still cast no shadows; mountains do.
- An edit (a debug edit or a world reset; normal play never edits terrain) marks the view dirty,
  and every built tile is rebuilt on the next frame: simpler than tracking each edit's tile and its
  neighbors, and only the dozen or so tiles in range are built. A bounded world is drawn the same
  way, as the tiles covering its rectangle.
- The range also covers the area within 48 units of the player, so after a reset or a teleport,
  while the camera glides over, the player's surroundings are drawn.
- The tile size is a constant to tune. Measured walking a generated world at the widest zoom: 16
  builds about 25 to 30 tiles, draws about 330,000 triangles in about 870 draw calls (most of them
  resource field models), and spends under 0.2 ms a frame on terrain, with about 60 ms on the first
  frame, which builds every tile at once; 8 drew half the triangles with twice the tiles, and 32
  drew twice the triangles and doubled the first frame. 16 is kept.
- Resource fields keep their per-node views, but only for fields inside the terrain view's range
  (a generated world holds thousands of fields; their visuals are freed when they leave the range
  and built again on return). Buildings keep their current views.

### HUD diagnostics

While the world is being explored and tuned, the HUD shows a diagnostics line under the
resources: the world (`seed 1234`, or `test map`), the player's biome and terrain, their chunk
`(cx, cz)`, and their map cell `(x, z)`, for example
`seed 1234   Highlands / Rock   chunk (1, -2)   cell (87, -101)`. It reads the sim only and is
meant for spotting seams; it can be hidden or moved to a debug overlay later.

### Out of scope

Fog of war, unloading chunks, saving and loading, choosing or travelling between planets, and
generating chunks on a background thread.

## Code changes

### Simulation (`src/Game.Sim`)

- New `Chunk.cs` (the per-chunk arrays and its coordinates), `WorldSettings.cs` (Bounded with a
  size, or Generated with a seed and planet), `PlanetDef.cs` (`PlanetDefs.Starter`), `Noise.cs`
  (seeded value noise, fractal octaves, ridged noise, cell hash), and `WorldGenerator.cs` (regions,
  climate, terrain, mountains, rivers, deposits, the spawn area).
- `Simulation`: chunked storage behind the existing read methods (`MapTerrainAt` and the rest);
  `MapInBounds` and `InBounds` consult the world kind; `MapWidth` and `MapHeight` become the bounded
  size (`Bounds`, null for a generated world); the player clamp applies only to bounded worlds;
  chunk generation around the player each step and on demand; `ChunkGenerated`; the spawn point
  from the generator; a `Simulation(WorldSettings)` constructor.
- `BiomeDefs`: the field density and per-type weights above.
- Profiling (stage stopwatches and a `dotnet-trace` sample) found no single hot spot; two caches
  were added that leave the output unchanged (checked by hashing generated chunks before and
  after): each chunk computes its region site positions once into a grid instead of 9 per cell,
  and each cell's river noise sign at most once instead of up to three times. They make
  generation about 22% faster in Debug and about 6% in Release, where a chunk takes about 1.5 ms.
- `SimulationDebug`: edits address cells in any generated chunk; `ResetToEmptyMap` resets to the
  bounded empty map and the new `ResetToGeneratedWorld(seed)` to a generated world, each replacing
  the whole world.
- `Chunk` exposes read-only cell accessors (`TerrainAt`, `BiomeAt`, `FeatureAt`, `RiverFlowAt` by
  index within the chunk) for tests and tools.

### Presentation and game

- `TerrainView`: render tiles near the camera, built and freed as the view moves, rebuilt on
  edits.
- `TerrainTiles`: returns placements instead of building nodes; new `TerrainTileView` batches a
  render tile's placements into multimeshes. `ModelLibrary` gains `Mesh(path)` (the model's mesh
  for a multimesh).
- `GameRoot`: starts a generated world from `--seed` or a random seed.
- `Hud`: the diagnostics line (world and seed, biome and terrain, chunk, map cell), and a dark
  outline on the HUD text so it reads over snow and sand.
- `MountainModels` and `RiverPieces` return a model path and yaw instead of a node;
  `ModelLibrary.WithoutShadows` is removed (placements carry whether they cast shadows).
- `ResourceFieldViews.Sync` takes the terrain view's range and keeps visuals only for fields in it.
- `GameRoot` exposes the `TerrainView` for the testing code.
- `CameraRig` and views: no assumption that the world starts at 0 or ends at the test map's edge.

### Testing

- `ScenarioCommand`, `CommandExecutor`, `scenario.schema.json`: `initMap.generate`.
- `StateSnapshot`: `world` (kind, seed, generated chunk count, built render tile count) and
  `player.chunk`.
- New scenario `generated-world.json`: a generated world from a fixed seed; overview screenshots of
  the spawn; expects open Grassland under the player and the 3 x 3 chunks around them;
  teleports across a chunk border and expects the new chunks to generate and draw; the snapshot
  exposes the number of built render tiles so the scenario can check tiles follow the camera.

## Blueprint updates

- **New world-generation.md:** chunks and world kinds, the generator (noise, planet, regions,
  climate bands, terrain patches, mountains, rivers, deposits), the spawn area, and
  determinism. Added to `docs/blueprints/INDEX.md` and the area list in `docs/development.md`.
- **simulation.md:** the grid is chunked; bounded and generated worlds; chunk generation around
  the player; `ChunkGenerated`.
- **terrain.md:** the test map is the bounded world; generated worlds point to world-generation.md;
  rendering is per render tile and batched.
- **resources.md:** fields come from generation per chunk (density and weights per biome); the
  test map's fixed fields remain for the bounded world.
- **player.md:** the player is kept inside only a bounded world; the spawn point in a generated
  world.
- **presentation.md:** `TerrainView` render tiles (size, view range, rebuilds), `TerrainTileView`
  multimeshes, `ModelLibrary.Mesh`, and the HUD diagnostics line.
- **testing.md:** `initMap.generate`, the snapshot's `world` and `player.chunk`, and the
  `generated-world` scenario.
- **docs/game-plan.md** (not a blueprint): the planet and generation intent points to
  world-generation.md.

## Verification

- Unit tests (`src/Game.Sim.Tests`):
  - Determinism: the same seed gives identical chunks; generating chunk (1, 0) before (0, 0) gives
    the same contents as the reverse order.
  - Seams: every chunk cell's biome matches the generator's biome for that position, and rivers
    flow into rivers or water across chunk borders, read through a simulation that holds the
    neighboring chunks as generated on their own.
  - The spawn is open Grassland at the center of chunk (0, 0) for one seed, and the player stands
    on it; for a seed whose center has no exit (seed 8), the spawn moves to the next candidate and
    is open Grassland there; the search takes the first candidate with an exit or settles for the
    last (with stand-in candidates and checks).
  - Rivers: every river cell's flow points to a river cell or water, or the cell is a Pool; no river
    sits on water, lava, or a mountain.
  - Deposits: no field on a feature, on unwalkable terrain, or beside another field; types match
    their biome.
  - Planet mix: averaged over 10 seeds' 6 x 6 chunk samples, each biome's share is within 4
    points of its weight (a single 6 x 6 sample varies too much to test, as large biomes do).
  - Resets switch between bounded and generated worlds.
  - Chunk generation follows the player; bounded worlds are unchanged (all existing tests pass).
- `dotnet test src/Game.Integration.Tests` passes, including `generated-world`.
- Windowed runs of a few seeds: review the spawn area, biome regions crossing chunk borders (with
  the HUD diagnostics showing the chunk change), ranges with passes, rivers reaching water, and
  deposits per biome. Measure a chunk's generation time and a render tile's build time, and the
  frame time while walking, for tile sizes 8, 16, and 32; record them in the blueprint.
