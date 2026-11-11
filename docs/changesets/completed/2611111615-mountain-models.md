# Mountain Models

- Status: Implemented
- Id: 2611111615-mountain-models
- Depends-on: none

## Summary

Replaces the procedural mountain cone with generated low-poly mountain models: two families
(stone and snow) of three shape variants each, chosen by the terrain beneath the mountain. The
models are required assets with no placeholder fallback. The existing mountain generator is
reworked so a model's footprint stays inside its map cell.

## Motivation

Mountains are the only hard land barrier and the most visible feature on the map, and they still
render as a two-cone placeholder. The asset factory already has a mountain generator
(`mountain_a` to `mountain_c`), but those models were never shipped, every one has a snow cap
regardless of the ground beneath it, and their scree skirt reaches 1.3 units past the map cell
onto walkable neighbors.

## Design

### Variants

A mountain may sit on any terrain that takes features: Grass, Sand, Snow, Mud, or Rock. The model
family depends only on that terrain:

| Terrain beneath | Family | Files |
|---|---|---|
| Snow | Snow | `mountain_snow_a.glb`, `mountain_snow_b.glb`, `mountain_snow_c.glb` |
| Grass, Sand, Mud, Rock | Stone | `mountain_a.glb`, `mountain_b.glb`, `mountain_c.glb` |

All six live in `src/Game/assets/models/terrain/` (`res://assets/models/terrain/<name>.glb`).

- **Stone** is bare rock: dark scree at the foot, mid stone on the slopes, light stone at the top,
  and no snow. It reads on every non-snow ground (green grass, yellow sand, brown mud, grey rock).
- **Snow** uses the same three shapes with snow from mid-slope up and a white-dusted skirt that
  blends into Snow ground, with darker rock left showing on the steepest faces. Tundra mountains
  are snowy all the way down, which is the look the biome needs.
- The two families share their three shapes (the same seeds and peak functions), so a group of
  mountains that crosses from grass into snow keeps its silhouettes and changes only its coloring.
- The family lookup is a table keyed by terrain, so a later family (for example sandstone for
  Sand, or basalt for Volcanic) is a new row plus three files, not a code change. Those are left
  out for now: grey stone on sand and on volcanic rock reads well enough.

### Footprint and edges

- Each model is a full 4 x 4 world-unit map cell, origin at the center of its base, resting on
  y = 0, Y up (the asset-factory conventions). It is placed at that scale, not normalized like
  resource models, so neighbors line up.
- **The whole model stays inside its map cell.** The current generator's skirt spreads 1.3 units
  onto neighboring cells, which would cover walkable ground, buildings, and river channels next
  to a mountain while the sim says those cells are open. The reworked generator keeps the whole
  model inside the cell, and all four edges lie flat on the ground (height 0), with a low scree
  apron rising into the peaks.
- Each mountain is a standalone peak: adjacent mountains sit side by side as a cluster of peaks
  rather than flowing into each other, and a lone mountain looks finished on every side. No
  connection-aware variants (end, corner, interior) are needed. Models designed to flow from one
  cell into the next may come later as a separate change.
- Peaks reach about 3.3 to 3.5 units.

### Choosing a variant

- The shape variant (a, b, c) and a yaw in 90-degree steps are chosen deterministically from the
  map cell's coordinates, so a mountain looks the same every run and after every terrain rebuild.
  The 90-degree rotations are safe because every edge sits flat on the ground.

### No fallback

- Mountain models are required. They load once on first use; a missing or unloadable file throws
  with its path. The game then fails at startup on the test map (which has mountains on Grass,
  Rock, and Snow), and so does every integration scenario that renders it, so a missing model
  cannot ship unnoticed.
- The procedural cone and snow cap are removed.

## Code changes

### Asset factory

- `src/asset-factory/tools/gen.py`:
  - Rework `mountain_tile` so all geometry stays within the 4 x 4 cell: drop the outer scree rings
    and the shared `border_h` and `base_h` edge profile, pin every edge vertex to the ground, and
    blend from the ground into the peaks.
  - Split the geometry (`mountain_shape`, which returns the surface triangles) from the coloring
    (`paint`), so both families use identical shapes.
  - Material banding by family: `STONE_MATS` (scree, rock_dk, rock, rock_lt by height, no snow)
    and `SNOW_MATS` (snow and shaded snow by height and face slope, rock on the steepest faces).
  - `build_mountains` writes `mountain_a` to `mountain_c` and `mountain_snow_a` to
    `mountain_snow_c` from the same three shapes.
- Regenerated sources in `src/asset-factory/models/` (six `.obj` and `.mtl` pairs) and kept preview
  sheets in `src/asset-factory/previews/` (six PNGs plus an arrangement check in
  `test-output/`).

### Game assets

- Six `.glb` files and their `.import` files in `src/Game/assets/models/terrain/`, produced with
  `obj_to_glb.py` and imported with `--headless --import`.
- `src/Game/assets/models/terrain/README.md`: rewritten for the current terrain model (eight
  terrain types, mountains and rivers as features). It lists the six mountain files, the family
  table, the footprint and edge-profile rules, and states that the mountain files are required.

### Presentation

- New `src/Game/Presentation/MountainModels.cs`:
  - A terrain-to-family table and each family's three paths.
  - `Create(TerrainType terrain, int mapX, int mapZ)`: picks the family (throws for terrain that
    cannot hold a mountain), picks the variant and a 0, 90, 180, or 270 degree yaw from a hash of
    the coordinates, and instances the cached `PackedScene` unscaled at the tile origin.
  - A `PackedScene` cache keyed by path; a missing file or a scene that is not a `Node3D` throws
    `InvalidOperationException` naming the path.
- `src/Game/Presentation/TerrainTiles.cs`: `AddMountain` calls `MountainModels.Create`; the cone,
  the snow cap, and `SnowCapColor` are removed; the class summary is updated.

### Tests

- `src/Game.Integration.Tests/Scenarios/terrain-types.json`: add mountains on Sand, Mud, Rock, and
  Grass beside the existing one on Snow, as a 3 x 2 block, so one overview screenshot shows both
  families on every ground that takes a mountain and a mixed cluster.

## Blueprint updates

- **terrain.md**, Rendering: replace the mountain bullet with: a mountain is a generated model
  from `MountainModels`, the Snow family on Snow ground and the Stone family on every other
  ground, one of three shapes at a deterministic 90-degree rotation per map cell; the model stays
  inside its map cell with every edge flat on the ground, so each mountain is a standalone peak.
  Update the Real CC0 line: terrain tiles are still placeholders, mountains are not.
- **presentation.md**, Asset and model pipeline: models load with a placeholder fallback except
  mountain models, which are required and throw when missing; add `MountainModels` (family by
  terrain, variant and yaw by map cell, placed unscaled); terrain tiles remain colored
  placeholders.
- `docs/asset-pipeline.md` (a how-to, not a blueprint): the tileable-assets bullet points to the
  generic shared-edge rule without naming the mountain generator, and adds that a cell-filling
  model that is not tileable (a mountain) stays inside its cell and meets the ground at every
  edge. The Microsoft Store Blender note adds that the launcher sometimes drops runs started
  from Git Bash, and that PowerShell `Start-Process -Wait` runs one at a time are reliable.

## Verification

- `python src/asset-factory/tools/gen.py`, then Blender previews of all six models and an
  arrangement render (a 3 x 2 block mixing variants and rotations, plus a lone mountain) to check
  that every edge sits on the ground and nothing crosses the cell edge.
- `dotnet build game.slnx -c Debug`; `dotnet test src/Game.Sim.Tests` (unchanged, must pass);
  `dotnet test src/Game.Integration.Tests` (must pass, which also proves every mountain file
  loads).
- Windowed `terrain-types` run: review the overview screenshot for both families on all five
  grounds. A windowed run of the test map: review the Highlands group (three adjacent cells), the
  Tundra pair, and the lone grass mountain, including the player and a building beside a
  mountain to confirm nothing overlaps neighboring cells.
- Temporarily renaming one `.glb` makes the game fail at startup with an error naming that file
  (checked once, not kept as a test).
