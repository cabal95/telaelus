# Mobile Renderer

- Status: Implemented
- Id: 2611141406-mobile-renderer
- Depends-on: none

## Summary

Switches the game from the Compatibility (OpenGL) renderer to the Mobile renderer on Direct3D 12,
tunes the lighting so the game keeps today's look, and cleans up the debug overlay's performance
line so it measures the frame correctly under the new renderer (adding the GPU time and using the
larger of CPU and GPU as the headline).

## Motivation

Measured on the development laptop (Snapdragon X Elite, Adreno X1-85, Windows on ARM; full
numbers in `future/graphics-settings.md`): with no native OpenGL driver, Compatibility runs
through a translation layer to Direct3D 12, costing about 35 µs of CPU per draw call. Fullscreen
`perf-walk` took 8.7 ms a frame on Compatibility, 8.9 ms on Forward+ (almost all GPU), and 2.6 ms
on Mobile (1.8 ms GPU, 0.2 ms renderer CPU). The Adreno is a mobile-style, tile-based GPU, which
is what the Mobile renderer is built for. In real play, Forward+ needed 12 ms of GPU time
fullscreen, leaving little headroom at 60 fps.

The planet view uses nothing Mobile lacks (it has directional shadows, glow, MSAA, and custom
shaders). A player setting to choose Forward+ on desktop GPUs, for the space mode's sake, is
parked in `future/graphics-settings.md`.

The debug overlay's "frame" figure is CPU time only. Under Compatibility the CPU waited on the GPU
inside the render step, so it looked like the whole frame; under Direct3D 12 it no longer waits
there, so the same figure read 0.8 ms while the GPU took 12 ms. The headline must include the GPU.

## Design

### Renderer

- Project settings: the rendering method is `mobile` (also for the `.mobile` feature override,
  replacing `gl_compatibility` there), and the Windows driver is `d3d12`. Fallback to OpenGL
  stays on, so a machine without Direct3D 12 still starts (on Compatibility, with its own look).
- macOS and Linux keep Godot's defaults for the Mobile renderer (Metal and Vulkan).
- The first launch compiles shaders once (one frame of about a second here); Godot caches them, so
  later launches are smooth. Nothing to do about it now.
- Headless runs use the dummy renderer either way, so the integration tests are unaffected.

### Keeping the look

The art style was tuned under Compatibility: one sun, a dark ambient, faces turned from the sun
reading near black (art-style.md). Mobile and Forward+ came out brighter and flatter, with lighter
shadows. Before switching, reference screenshots are taken under Compatibility at fixed views (a
generated world near spawn, a forest and ore fields, buildings, the fog edge, the build menu and
panels with their pictures). After switching, the light, the ambient, the shadow settings, and
tone mapping are tuned until the screenshots match side by side, and the values are recorded in
art-style.md. If matching needs a `WorldEnvironment` in the main world (the picture stage avoids
one today because under Compatibility an environment leaked into the main world), that is checked
under Mobile and used if it no longer leaks.

- The picture stage (`BuildingPictures`, `ResourceIcons`, `FieldPictures`, `TurntableView`) is
  checked the same way: pictures, icons, and the turntable match their current look, including
  the gray silhouettes of undiscovered buildings.
- The fog overlay and its floor plane are checked at the edge and across tile seams.
- The asset factory's Godot preview renderer (`tools/preview/project.godot`, on Forward+ today)
  switches to Mobile too, so its "engine-accurate" check matches the game.

### The debug overlay's performance line

```
frame 2.6 / 16.7 ms (16%)   cpu 0.8 (scripts 0.6, sim 0.1, render 0.2)   gpu 1.8   worst 9.8   fps 60   draws 105   Mobile D3D12
```

- **frame** is the larger of the CPU time and the GPU time, averaged over the last second, against
  the 16.7 ms budget; the color (green under 50%, yellow to 80%, red above) follows it. It answers
  "how much of the budget did a frame use", whichever side is the bottleneck.
- **cpu** is today's work time (scripts plus the renderer's CPU time), with its parts.
- **gpu** is Godot's GPU time for the viewport. On Direct3D 12 it is measured with GPU timestamps,
  so it does not include the wait for the display, and it is shown always. Checked: the same
  `perf-walk` read 3.2 ms of GPU time capped and 1.0 ms uncapped. That is not the wait (which would
  be about 16 ms) but the GPU clocking down when it has time to spare, so each frame takes longer at
  the lower clock: still the real time it took. On Compatibility (should Godot fall back to it) the
  GPU time stays "uncapped only" and the headline is the CPU time.
- **worst** is the slowest frame by the same headline.
- The renderer and driver are named at the end of the line, so a number always says what it
  measured.
- `PerfMonitor` keeps recording each part, and adds the headline: the snapshot's `perf` gains
  `headlineMs`, and the scenario report's perf summary gains `headlineMsAvg` beside its existing
  fields.

### What matching the look took

- Under Mobile with no environment, the world was brighter and the shaded faces lighter: Mobile
  adds ambient fill where Compatibility added none. The main world now has a `WorldEnvironment`
  with a clear-color background, ambient light off, and linear tone mapping; the picture stage's
  worlds need none (their pictures already matched).
- Shadows were faint and blurry: Mobile's default soft shadow filter dithers them. The directional
  shadow filter is set to hard (`soft_shadow_filter_quality` 0, for the Mobile override too), which
  matches Compatibility's crisp, dark shadows.
- With those two, the reference screenshots match side by side (mean brightness within 0.2 of 255
  on every view with no fog in it; grass and the top bar identical pixel for pixel). The fog's
  near-black reads a few levels different ((22, 25, 22) against (17, 23, 20)), from Mobile's
  10-bit color buffer having coarse steps in very dark colors: not visible, left as it is.
- Trees in the reference shots sit slightly differently between runs; that is not the renderer
  (field ids, which seed the tree placement, depend on the random world built before a scenario
  resets), so it was ignored.

### Testing

- Both test suites (the headless tests are unaffected by the renderer, but the overlay change
  touches the snapshot).
- The reference screenshots before and after, compared side by side.
- `perf-walk` windowed, capped and uncapped, at 1280x720 and fullscreen, two runs each, recorded
  in presentation.md's benchmark.

## Code changes

- `src/Game/project.godot`: the rendering method (and its `.mobile` override), the Windows driver,
  the OpenGL fallback, and the hard directional shadow filter.
- `GameRoot`: the main world's `WorldEnvironment` (ambient off). The sun is unchanged.
- `PictureStage`: only its comment (the pictures matched without changes).
- `PerfMonitor`: the headline (the larger of CPU and GPU where the GPU time is measured), the
  worst frame by the headline, `GpuMeasured`, and `Renderer` (from
  `RenderingServer.GetCurrentRenderingMethod` and `GetCurrentRenderingDriverName`).
- `DebugOverlay`: the new line.
- `StateSnapshot` (`perf.headlineMs`) and `ScenarioRunner`'s report (`renderer`,
  `headlineMsAvg`, `headlineMsWorst`).
- `src/asset-factory/tools/preview/project.godot`: Mobile on Direct3D 12 with hard shadows (its own
  lighting unchanged).

## Blueprint updates

- presentation.md: the renderer (Rendering), the performance line and what each figure means
  (Performance), and a new benchmark row for Mobile; the note that on this machine
  Compatibility ran through a translation layer stays as history in the benchmark's text.
- art-style.md: the lighting values under Mobile.
- testing.md: the snapshot's and the report's headline field.
- asset-pipeline.md: the Godot preview renderer uses Mobile.

## Verification

- `dotnet build game.slnx`, both test suites: 167 sim tests and 39 integration tests pass.
- Side-by-side screenshots, Compatibility before and Mobile after, matching (above): a board of
  every terrain, deposit, and three buildings at two zooms, the build menu, the building panel with
  its turntable, the field panel, the inventory, and a generated world with its fog edge. The
  preview tool renders a model under Mobile.
- `perf-walk` on Mobile, two runs each (presentation.md): headline 1.2 ms uncapped and 3.2 to 4.1 ms
  capped at 1280x720; 1.9 ms uncapped and 5.9 ms capped fullscreen (about 35% of the budget, against
  about 52% to 58% on Compatibility). The overlay's line was checked on screen.
