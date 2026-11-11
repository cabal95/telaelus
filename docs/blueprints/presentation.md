# Presentation Blueprint

## Overview

The presentation renders the simulation and routes input to it. It owns the Godot scene tree and
reads simulation state each frame; it holds no game logic. Keeping this seam is what makes the
planned multiplayer feasible.

## Current state

### Entry point and the frame loop

- The main scene (`src/Game/scenes/Main.tscn`) has a single root node running `GameRoot`. `GameRoot`
  builds the world in code and owns the `Simulation` and `SimClock`.
- Each `_Process(delta)`: read input into `PlayerIntent` (camera-relative move, held `E` harvest),
  advance the clock and step the simulation, then sync the views from read-only simulation state.
  `_UnhandledInput` handles mouse-wheel zoom and the `L` launch command.

### Views

`GameRoot` creates and syncs these presentation nodes, each reading simulation state:

- `TerrainView` (one terrain tile per map cell), `ResourceFieldViews` (staged resource field
  visuals), `BuildingViews` (footprint-sized building boxes), `PlayerView` (the character), `CameraRig` (the orthographic follow camera),
  `Hud` (a `CanvasLayer` label with resource totals, launch status, build selection with facing
  and fields in range, and the harvest hint), and `BuildController` (build-mode selection and
  rotation, the footprint ghost, the extractor work-area disc and field highlights, and the place
  command).

### Asset and model pipeline

- Models are CC0 glTF assets under `src/Game/assets/models/<area>/`, loaded in code with a placeholder
  fallback so the build and game stay green before assets are added.
- `NodeModels` scales a loaded model to a target height from its bounding box, caps its footprint to
  a caller-given size (a resource field's slot), seats it on the ground, and gives it a
  deterministic variant and yaw; absent a model it builds a colored box.
- The character is a hand-assembled Godot scene (`character.tscn`) that `PlayerView` loads; terrain
  tiles are currently colored placeholders awaiting CC0 models (see terrain.md).

### Rendering

- The project uses Godot 4.7 with C# and the **Compatibility (OpenGL)** renderer. The Godot project
  root is `src/Game`; `Game.csproj` references the `Game.Sim` project (see simulation.md), whose
  `Game.Sim.dll` is built beside `Game.dll` and loaded by the engine.

## Related

- simulation.md (the authoritative state), player.md, terrain.md, resources.md, buildings.md.
