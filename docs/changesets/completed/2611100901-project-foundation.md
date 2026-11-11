# Project foundation

- Status: Implemented
- Id: 2611100901-project-foundation
- Depends-on: none

## Summary

Stand up the Godot 4.7 C# project with the authoritative-simulation / presentation architecture, a
fixed-timestep loop, and an orthographic 2.5D camera over a bootstrap scene.

## Motivation

Establish the sim/presentation seam and a running window before any gameplay, so every later system
has a place to live and a loop to run on.

## Design

- Godot project root is `src/`; the renderer is Compatibility (OpenGL); target framework net8.0.
- The `Simulation` is a plain C# object (no Godot nodes), advanced by a `SimClock` at a fixed 30 Hz
  from `GameRoot._Process`.
- `CameraRig` is an orthographic camera at a fixed 2.5D angle. The main scene's root runs `GameRoot`.

## Code changes

- `src/project.godot` (gl_compatibility renderer, main scene), `src/Game.csproj`
  (`Godot.NET.Sdk/4.7.2`, net8.0), `src/scenes/Main.tscn` (root node runs `GameRoot`).
- `src/Sim/SimClock.cs` (fixed-step accumulator with backlog clamp), `src/Sim/Simulation.cs`
  (initial shell).
- `src/Presentation/GameRoot.cs` (owns the simulation and clock, builds the world, runs the loop),
  `src/Presentation/CameraRig.cs` (orthographic, fixed angle).

## Blueprint updates

- Establishes `simulation.md` (ownership, the fixed loop, the sim/presentation seam, the posture)
  and `presentation.md` (the frame loop, entry point, renderer, camera).

## Verification

Clean `dotnet build`; headless run with no errors; the window shows the scene under the orthographic
camera.
