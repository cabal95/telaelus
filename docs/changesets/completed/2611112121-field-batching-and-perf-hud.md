# Field Batching and Performance HUD

- Status: Implemented
- Id: 2611112121-field-batching-and-perf-hud
- Depends-on: none

## Summary

Draws resource field models in batches, one multimesh per model per render tile, instead of one
node per model, and adds a performance line to the HUD that shows how much of the frame budget
the game is using, so it is obvious at a glance whether there is headroom ("2.1 ms of 16.7") or
not ("14.8 ms of 16.7").

## Motivation

Measured on a generated world, terrain is cheap but resource fields are not: a frame drew about
870 draw calls and 1,250 objects, most of them tree and rock models, because every visible field
slot is its own glTF node (a forest chunk holds hundreds of fields, five models each). Batching
them the way terrain already is removes most of those draw calls and nodes. And as the world and
the factory grow, we need a standing answer to "are we fast enough?" that does not need a
profiler: a number on screen measured against the frame budget.

## Design

### Batched field models

- Field visuals follow the terrain's render tiles (16 x 16 map cells, see presentation.md): for
  every tile the terrain view has built, the field view builds that tile's field batch, and frees
  it when the terrain tile is freed. A field belongs to the tile containing its map cell.
- A tile's field batch places every visible slot of every field in the tile, with today's rules:
  five slots per field at the same deterministic, jittered positions, the first `Stage` slots
  shown, each slot's model variant and yaw chosen as today, scaled to its deposit's target height
  (Wood by the field's size) and capped to the 1.2-unit slot. The slots are grouped by model into
  one `MultiMeshInstance3D` per model, casting shadows.
- The scale for each model comes from its mesh's bounds, computed once per model (today
  `NodeModels` measures every instance's bounds as it is created).
- A tile's field batch is rebuilt when anything it shows changes: a field in it is added or
  removed, or a field's stage changes. Stages change at most five times in a field's life, so
  rebuilds are rare; the view tracks each tile's (field, stage) list and compares it each frame.
- `ResourceFieldViews.VisibleModels(fieldId)` keeps reporting how many models a field shows (its
  stage when its tile is built, null otherwise), so the `field-stage-views` scenario keeps working.
- `NodeModels` becomes the shared rules (model paths, target heights, Wood height, slot layout,
  variant and yaw) used to compute placements, without creating nodes.

### Performance line on the HUD

A new line under the diagnostics, for example:

```
frame 6.6 / 16.7 ms (40%)   scripts 0.7 (sim 0.1)  render 5.9  gpu (uncapped only)   worst 9.8   fps 60   draws 580
```

- **Budget:** 16.7 ms, one frame at 60 fps. This is the game's target, not the display's refresh
  rate; on a 120 Hz display frames stay smooth at that rate only below 8.3 ms.
- **frame:** the frame's **work** time averaged over the last second, colored green under 50% of
  the budget, yellow to 80%, red above. With vsync the frame rate stays at the refresh rate until
  the work no longer fits, so fps alone hides how close we are; the work time shows it.
- How work is measured (found during implementation): Godot's own process time includes the wait
  for the next display refresh, so it read about 16.7 ms under vsync even on an empty map and could
  not show headroom. Instead the game times its own frame: a `PerfFrameStart` node processes first
  each frame and `PerfMonitor` last, so the time between them is every node's update (the
  **scripts** time, the simulation included); adding the renderer's CPU time for the viewport gives
  the **work** time. Checked against runs with vsync off, where the time between frames is the true
  cost: an idle empty map measured 3.6 ms work with vsync and 3.7 ms per frame uncapped; an idle
  generated world 7.8 ms and 7.2 ms.
- **scripts** and **sim:** the scripts time, and the part of it spent in simulation steps (timed in
  `GameRoot`), so a slow sim is told apart from slow presentation. **render:** the renderer's CPU
  time.
- **gpu:** Godot's viewport GPU time. On the Compatibility renderer it is coarse (about 2 ms
  steps) and under vsync includes the wait (it read about 16.5 ms at any load), so it is shown only
  when running uncapped.
- **worst:** the slowest frame's work time in the last second, so one-off spikes (a new chunk
  generating, a batch rebuilding) show even when the average is low.
- **fps** and **draws** (draw calls in the frame), for context.
- F3 hides and shows the line; it starts shown.
- `--uncapped` (a game argument) turns off vsync and the frame cap for measurement runs.

### Performance in the automation output

The same numbers go into what scenarios and the live channel report, so a fixed seed and walk can
be measured before and after a change without reading the screen:

- Every snapshot carries `perf`: `workMs`, `scriptMs`, `renderCpuMs`, `gpuMs` (null when not
  reported), `simMs`, `frameMs` (wall time since the last frame), `worstMs`, `fps`, `draws`, and
  `objects`, as of that frame.
- `report.json` gains a run-level `perf` summary over every frame of the run: `mode` (`windowed` or
  `headless`), `uncapped`, the frame count, the budget, the average and worst work time, the average
  scripts, render CPU, and GPU times, the average and worst sim and frame times, and the average and
  peak draw calls and objects.
- Headless runs use a dummy renderer, so their timings and render counts are not measurements; the
  mode field makes that plain. Measure windowed, uncapped.
- Scenarios never assert on timings or draw counts: they vary by machine.

### Measuring this change

A fixed-seed walk scenario, `perf-walk.json` (seed 2, widest zoom, walking a set path across
grassland and forest for about 30 seconds), is the standing benchmark, run windowed with
`--uncapped` (frames as fast as they can go, one sim tick per frame). Run-to-run noise turned out
to be about 10%, so each side was run five times:

| | Frame (ms, median of 5) | Draw calls (avg) | Objects (avg) |
|---|---|---|---|
| One node per field model (baseline) | 6.9 (runs 6.4 to 7.0) | 1,012 | 1,547 |
| Field models batched per tile | 6.6 (runs 6.4 to 8.3) | 583 | 1,117 |

Batching cut draw calls by 42% and objects by 28% (less than the "low hundreds" expected), and the
frame time barely moved, within the noise. On this machine (Windows on ARM, the Compatibility
renderer through a D3D12 translation layer) the renderer's CPU time has a base of about 3 ms even
for an empty map, the remaining draw calls are mostly terrain multimeshes (several per tile, and
shadow casters again in each shadow cascade), and the GPU time is about the same as the frame.
Batching still removes thousands of nodes and keeps field cost flat as fields get denser.

## Code changes

### Presentation

- `ResourceFieldViews`: per-tile field batches keyed like the terrain tiles, built and freed with
  them, rebuilt when a tile's (field, stage) list changes; `VisibleModels` from the stage; a count
  of built batches for the snapshot.
- New `FieldTileView` (like `TerrainTileView`): builds one tile's field multimeshes from
  placements.
- `NodeModels`: placement rules only (path, variant, yaw, target height and footprint scale from
  cached mesh bounds); no node creation.
- `TerrainView`: exposes its built tile keys (`TileKeys`, `HasTile`) so the field view follows
  the same tiles.
- `ModelLibrary`: mesh bounds per model, cached alongside `Mesh(path)`.
- New `PerfMonitor` and `PerfFrameStart`: time the frame's scripts between the first and last
  process callbacks, enable the viewport's render time measurement, sample renderer CPU and GPU
  time, sim time, wall time between frames, fps, draw calls, and objects each frame, and keep one
  second of history for the average and the worst frame; `--uncapped` turns off vsync and the frame
  cap.
- `GameRoot`: times the sim steps for `PerfMonitor`; F3 toggles the line.
- `Hud`: the performance line in its own label between the diagnostics and the launch line,
  colored by headroom.

### Testing

- `StateSnapshot`: `perf` (see above) for reports and the live channel; scenarios do not assert on
  timings, which vary by machine. `world` adds `fieldBatches` (tiles with a built field batch).
- The scenario runner: accumulates `PerfMonitor` samples every frame of a run and writes the
  run-level `perf` summary (with the mode) into `report.json`.
- New scenario `perf-walk.json` (the benchmark above). It is kept in `Scenarios/`, so it also runs
  headless in the suite as a smoke test of a long walk across chunks, with no timing assertions.
- `field-stage-views` keeps passing unchanged (visible models track the stage and vanish with the
  field).
- New scenario `field-batches.json`: a generated world; every built terrain tile has a field batch,
  and the batches follow the tiles into another chunk; a screenshot shows a forest drawn from
  batches. A field showing one model fewer per stage and none once gone is covered by
  `field-stage-views`, which now runs through the batches.

## Blueprint updates

- **presentation.md:** `ResourceFieldViews` batches per render tile (`FieldTileView`), `NodeModels`
  as placement rules, mesh bounds in `ModelLibrary`, `PerfMonitor` and the HUD performance line
  (budget, each number's meaning, colors, F3), and the measured draw calls and objects before and
  after batching.
- **resources.md:** field rendering as batched slots per tile (same slots, stages, sizes).
- **testing.md:** the snapshot's `perf` and `world.fieldBatches`, the report's run-level `perf`
  summary and mode, why headless timings are not measurements, and the `field-batches` and
  `perf-walk` scenarios (the latter as the standing performance benchmark, run windowed).

## Verification

- `dotnet build game.slnx -c Debug`; `dotnet test src/Game.Sim.Tests` (unchanged) and
  `dotnet test src/Game.Integration.Tests` pass.
- Run `perf-walk` windowed and uncapped, five times before and five after batching (see Measuring
  this change), and record the results in the presentation blueprint.
- Windowed check that fields look the same as before (positions, sizes, stages), that depleting a
  field updates its models, and that the performance line reads sensibly and turns yellow or red
  under load (for example at the widest zoom over a dense forest, or by temporarily lowering the
  budget).
