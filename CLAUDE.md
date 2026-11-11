# Project: Game (working title)

A 2.5D on-planet resource/factory game (Part A) that feeds a space exploration and combat game
(Part B), built in Godot 4.7 with C#. See [docs/game-plan.md](docs/game-plan.md) for the full
design, milestone roadmap, and architecture decisions.

## Development process (spec-driven)

We develop spec-first; the process is described in [docs/development.md](docs/development.md).
There are two kinds of spec:

- **Blueprints** (start at [docs/blueprints/INDEX.md](docs/blueprints/INDEX.md)) are the living,
  present-tense description of how each area works right now, and are authoritative for "what is".
  Consult the relevant blueprint before changing an area, and keep it accurate.
- **Changesets** (see [docs/changesets/INDEX.md](docs/changesets/INDEX.md)) are the units of
  change: a change is drafted as a changeset and approved before code is written; after
  implementing, update the affected blueprint(s) and move the changeset to `completed/` with its
  stamped prefix. One changeset per commit.

## Local tool paths

Machine-specific tool paths (such as the Godot editor executable) live in `tools.json` at the
repo root. This file is git-ignored and differs per machine.

- To find a tool's path, read `tools.json` and use the value there.
- If `tools.json` does not exist, do NOT guess paths or search the filesystem for the tool.
  Tell the user to copy `tools.template.json` to `tools.json` and fill in their paths, then
  wait for them before continuing.

## Building and running (Godot C#)

Use the Godot editor path from `tools.json` wherever `<godot.editor>` appears below.

`src/` holds one folder per C# project. The Godot project root is `src/Game` (that is where
`project.godot` and `Game.csproj` live), so Godot commands take `--path src/Game`. The
simulation is a separate class library in `src/Game.Sim`, referenced by `Game.csproj`.
`game.slnx` at the repo root lists every project; building it needs .NET SDK 9.0.200 or later
(older SDKs cannot read `.slnx`), while each project still targets net8.0.

- Build without the editor: `dotnet build game.slnx -c Debug`
- Run the sim unit tests: `dotnet test src/Game.Sim.Tests` (engine-free; must pass before a
  changeset is stamped Implemented)
- Open the editor: `<godot.editor> --editor --path src/Game`
- Run the game: `<godot.editor> --path src/Game`
- Headless runtime check: `<godot.editor> --headless --path src/Game --quit-after 20` (stderr
  should be empty on success)

Avoid the headless editor `--build-solutions --quit` flow; it can hang after building. Prefer
`dotnet build` for compile checks.

## Architecture (keep this seam intact)

The simulation (`src/Game.Sim`, its own project) is authoritative and free of Godot nodes,
advanced on a fixed-timestep clock. The presentation (`src/Game/Presentation`) reads sim state and
mirrors it onto nodes. The project reference runs one way (the game references the sim), so the
compiler stops the sim from depending on presentation code. Keep game logic in the sim and rendering/input in the presentation. This separation is
what makes the planned co-op multiplayer feasible. See the "Architecture Posture" section of
[docs/game-plan.md](docs/game-plan.md).

## Code style

Follow [docs/code-style.md](docs/code-style.md): Allman braces and braces on every control-flow
body (both enforced via analyzers in `.editorconfig`), and always-multi-line object initializers
(a hand-followed convention the editor cannot enforce).
