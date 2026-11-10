# Project: Game (working title)

A 2.5D on-planet resource/factory game (Part A) that feeds a space exploration and combat game
(Part B), built in Godot 4.7 with C#. See [docs/game-plan.md](docs/game-plan.md) for the full
design, milestone roadmap, and architecture decisions.

## Local tool paths

Machine-specific tool paths (such as the Godot editor executable) live in `tools.json` at the
repo root. This file is git-ignored and differs per machine.

- To find a tool's path, read `tools.json` and use the value there.
- If `tools.json` does not exist, do NOT guess paths or search the filesystem for the tool.
  Tell the user to copy `tools.template.json` to `tools.json` and fill in their paths, then
  wait for them before continuing.

## Building and running (Godot C#)

Use the Godot editor path from `tools.json` wherever `<godot.editor>` appears below.

The Godot project root is the `src/` folder (that is where `project.godot` and `Game.csproj`
live), so Godot commands take `--path src`.

- Build without the editor: `dotnet build src/Game.csproj -c Debug`
- Open the editor: `<godot.editor> --editor --path src`
- Run the game: `<godot.editor> --path src`
- Headless runtime check: `<godot.editor> --headless --path src --quit-after 20` (stderr should
  be empty on success)

Avoid the headless editor `--build-solutions --quit` flow; it can hang after building. Prefer
`dotnet build` for compile checks.

## Architecture (keep this seam intact)

The simulation (`src/Sim`) is authoritative and free of Godot nodes, advanced on a
fixed-timestep clock. The presentation (`src/Presentation`) reads sim state and mirrors it onto
nodes. Keep game logic in the sim and rendering/input in the presentation. This separation is
what makes the planned co-op multiplayer feasible. See the "Architecture Posture" section of
[docs/game-plan.md](docs/game-plan.md).
