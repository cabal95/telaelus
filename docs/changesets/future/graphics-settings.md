# Graphics Settings

- Status: Future (parked 2026-11-14; not approved)
- Depends-on: the switch to the Mobile renderer (mobile-renderer)

## Why it waits

Nothing needs it yet. The game runs on the Mobile renderer, which is fast on everything we have
tried, and the planet view uses none of Forward+'s extra features. It becomes worth building when
either the space mode (Part B) wants Forward+'s features, or the game reaches players with
hardware we cannot test.

## Summary

A graphics settings panel, saved in the player's data folder: which renderer to use (Forward+ or
Mobile, taking effect on restart, with a first-launch default chosen from the GPU), the display
mode, a render scale with upscaling, vsync, and a frame cap.

## What we learned (2026-11-14)

### The renderers on this machine

The development laptop: Snapdragon X Elite, Adreno X1-85 integrated GPU (a mobile-style,
tile-based GPU), Windows on ARM. `perf-walk` (seed 2, widest zoom, about 30 s), uncapped, two
runs each. "Wall" is the time between frames, GPU included; "work" is the CPU part the debug
overlay showed as "frame" at the time.

| Renderer | Window | Shadows | Wall (ms) | Work, CPU (ms) | GPU (ms) | Draw calls |
|---|---|---|---|---|---|---|
| Compatibility (OpenGL) | 1280x720 | on | 4.8 | 4.5 | n/a | 347 |
| Compatibility (OpenGL) | 2304x1536 | on | 8.7 | 8.2 | n/a | 492 |
| Compatibility (OpenGL) | 1280x720 | off | 2.8 | 2.5 | n/a | 234 |
| Compatibility (OpenGL) | 2304x1536 | off | 5.5 | 5.1 | n/a | 368 |
| Mobile, Direct3D 12 | 1280x720 | on | 2.1 | 0.7 | 1.2 | 71 |
| Mobile, Direct3D 12 | 2304x1536 | on | 2.6 | 0.8 | 1.8 | 105 |
| Forward+, Direct3D 12 | 1280x720 | on | 4.8 | 0.9 | 3.8 | 347 |
| Forward+, Direct3D 12 | 2304x1536 | on | 8.9 | 1.2 | 8.2 | 492 |

- Compatibility runs OpenGL through Microsoft's translation layer to Direct3D 12 on this machine
  (there is no native OpenGL driver), which made each draw call cost about 35 µs of CPU and made
  the CPU wait on the GPU inside the render step.
- In real play, fullscreen with a mostly explored view at the default zoom, Forward+ measured
  12 ms of GPU time (80 to 85 fps uncapped; about 65% GPU use capped at 60 fps): it holds 60 fps
  with little headroom. Mobile suits this GPU; Forward+ targets desktop GPUs.
- The first launch of a Direct3D 12 renderer had one frame of about 1 s while shaders compiled
  (cached after that).
- The two renderers shade differently (Mobile and Forward+ looked brighter and flatter than
  Compatibility before tuning), so each needs its own lighting tuning.

### Why keep Forward+ at all

The space mode (Part B) may want what only Forward+ has: dynamic global illumination (SDFGI,
VoxelGI), volumetric fog, screen-space reflections and ambient occlusion, and many lights. A
desktop GPU runs Forward+ well, so players with one should be able to choose it, while laptops
and integrated GPUs stay on Mobile.

### What Godot offers

- **Renderer from a setting:** the renderer is fixed at startup. `application/config/project_settings_override`
  can point at a file such as `user://graphics.cfg`, read at launch, holding
  `rendering/renderer/rendering_method` (`forward_plus` or `mobile`) and the driver
  (`rendering/rendering_device/driver.windows`: `d3d12` or `vulkan`). The settings panel writes it
  and says the change applies after a restart.
- **A first-launch default:** `RenderingServer.GetVideoAdapterType()` tells an integrated GPU
  from a discrete one, and the adapter's name and vendor are available too.
- **A fallback:** when Direct3D 12 or Vulkan is missing, Godot can fall back to Compatibility on
  its own (`rendering/rendering_device/fallback_to_opengl3`).
- **Fullscreen:** windowed, borderless fullscreen, and exclusive fullscreen (on Windows, a
  lower-latency presentation path). Godot never changes the monitor's resolution.
- **Resolution, the modern way:** a render scale (`Viewport.Scaling3DScale`, for example 50% to
  100%) draws the 3D world smaller and upscales it, with AMD FSR on Forward+ and Mobile, while the
  UI stays at native resolution and sharp. At 67%, under half the pixels are drawn. In windowed
  mode, a list of window sizes still makes sense.
- The camera already sizes the view by window height, so any window size or render scale shows the
  same view of the world.

## Plan (rough)

- A settings file in the player's data folder, and a Graphics section in a settings panel (the
  game has no settings panel yet: that may be a changeset of its own first).
- Settings: renderer (Forward+ or Mobile, on restart), display mode (windowed, borderless,
  fullscreen), window size (windowed), render scale and upscaler, vsync, frame cap.
- First launch: Mobile on integrated GPUs, Forward+ on discrete ones (or Mobile everywhere until
  something needs Forward+; to decide then).
- A lighting pass for Forward+ against the Mobile look, and a rule that new art is checked on both.
- The debug overlay names the renderer and render scale, so measurements say what they measured.
- A `perf-walk` run per renderer and scale on each machine we have.

## Open questions for when it is picked up

- Whether the space mode needs Forward+ at all (decide once its look is designed).
- Vulkan or Direct3D 12 as the Windows default for Forward+ on desktop GPUs (measure both).
- Whether settings live in their own panel or a modal, and how they reach the panel (a key, a
  menu).
