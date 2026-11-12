# Camera Zoom Per Pixel

- Status: Implemented
- Id: 2611112139-camera-zoom-per-pixel
- Depends-on: none

## Summary

Makes the camera's zoom a fixed scale on screen rather than a fixed slice of the world: resizing
or maximizing the window shows more or less of the map at the same size, instead of zooming
everything in.

## Motivation

The orthographic camera's size is the height of the visible world in units, and Godot keeps it
when the window changes. So a taller window stretches the same slice of map over more pixels:
the character that is about 80 pixels tall in a small window grows to about 200 when the window is
maximized. A bigger window should simply show more of the world.

## Design

- The camera's zoom is the number of world units visible across a **reference height of 720
  pixels** (the resolution scenarios run at). Each frame the camera's actual orthographic size is
  the zoom scaled by the viewport's height: `size = zoom * viewportHeight / 720`. Pixels per world
  unit therefore depend only on the zoom, and a larger window shows proportionally more map.
- The mouse wheel and `ZoomTo` change the zoom, clamped between the same limits as today (16 to
  40), with the same default (28). At a 720-pixel-tall window nothing changes, so every scenario
  (all run at 1280 x 720) and its `cameraZoom` steps behave exactly as before.
- At the default 648-pixel window the view is slightly closer than before (28 units over 720
  pixels rather than over 648); maximized, it shows proportionally more.
- The terrain view's range already follows what the camera sees, so a larger window builds more
  render tiles.
- **Camera distance** (found in testing): the camera sat a fixed 25 units from its target. Past
  about 1,048 pixels of window height the view grew tall enough that its lower edge dipped below
  the ground, so the ground beyond it fell behind the camera's near limit and a gray band showed at
  the bottom (for example at 2288 x 1358). The camera now sits the offset's length plus the visible
  world height away, along the same direction; an orthographic view looks the same from any
  distance. The light's shadow range follows it, since directional shadows are drawn only up to a
  set distance from the camera.

## Code changes

- `CameraRig`: a `ZoomLevel` property (units per 720 pixels) replaces setting `Size` directly;
  `_Process` sets `Size` from the zoom and the viewport height; `Zoom(steps)` and `ZoomTo(size)`
  change and clamp the zoom; `ViewDistance` (the offset's length plus `Size`) places the camera.
- `GameRoot`: sets the directional light's shadow max distance each frame to `ViewDistance` plus
  the visible height.

## Blueprint updates

- **player.md:** the camera's zoom is world units per 720 pixels of window height, so resizing
  the window shows more or less of the map at the same scale; limits and default unchanged; the
  camera's distance grows with the view, and the shadow range follows.
- **testing.md:** `cameraZoom` `{ size }` sets that zoom (world units across 720 pixels of height).

## Verification

- `dotnet build game.slnx -c Debug`; `dotnet test src/Game.Integration.Tests` passes unchanged.
- Windowed: screenshots of the spawn of seed 1 at 1152 x 648, 1280 x 720, and 1920 x 1080 show the
  same pixel scale (a rock cluster beside the player sits about 507 pixels from them in each), with
  the larger windows showing more map. At 1280 x 720 the orthographic size equals the zoom, as
  before, so every scenario renders as it did.
- The zoom limits (16 to 40) were checked by hand in the game at the default and maximized window
  sizes and kept.
- At 2288 x 1358 and the widest zoom the ground fills the window with no gray band; at 1280 x 720
  the view is the same, with slightly softer shadows (the shadow range now spans a different
  depth).
