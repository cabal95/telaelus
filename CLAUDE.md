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

`src/` holds one folder per C# project, plus `src/asset-factory` (asset source and generators, not
a C# project; see [docs/asset-pipeline.md](docs/asset-pipeline.md)). The Godot project root is `src/Game` (that is where
`project.godot` and `Game.csproj` live), so Godot commands take `--path src/Game`. The
simulation is a separate class library in `src/Game.Sim`, referenced by `Game.csproj`.
`game.slnx` at the repo root lists every project; building it needs .NET SDK 9.0.200 or later
(older SDKs cannot read `.slnx`), while each project still targets net8.0.

- Build without the editor: `dotnet build game.slnx -c Debug`
- After adding a C# script or asset under `src/Game`, run `<godot.editor> --headless --path
  src/Game --import` (a headless project scan and import that quits in seconds) and commit the
  `.uid` files it creates with the script, so they do not appear later when the editor opens.
- Asset factory tools (generator, `.glb` exporter, preview renderer) run in a private Python
  environment: `python src/asset-factory/setup_env.py` once per machine, then
  `src/asset-factory/.venv/Scripts/python src/asset-factory/tools/<tool>.py`. See
  [docs/asset-pipeline.md](docs/asset-pipeline.md).
- Run the sim unit tests: `dotnet test src/Game.Sim.Tests` (engine-free; must pass before a
  changeset is stamped Implemented)
- Run the integration tests: `dotnet test src/Game.Integration.Tests` (each JSON scenario in
  `src/Game.Integration.Tests/Scenarios/` runs headless as its own test; must pass when a change
  touches presentation or input)
- Run one scenario windowed with screenshots: `<godot.editor> --path src/Game --fixed-fps 30
  --resolution 1280x720 -- --scenario=<file> [--output=<dir>]` (relative paths resolve against
  the repo root; output defaults to `test-output/runs/<timestamp>-<name>/`; read the PNGs and
  `report.json` there). Add `--headless` for a state-only run. See
  [docs/blueprints/testing.md](docs/blueprints/testing.md) for the command list.
  Windowed runs (and the live control channel) always open a Godot window for a second or two and
  take focus. We found no way to hide it: `--headless` cannot render screenshots, `--no-window`,
  `--minimized` and off-screen `--position` are ignored or clamped, a hidden process launch still
  takes keyboard focus briefly, and the `no_focus` window setting leaves the window visible. Use
  `--headless` (state only) when you do not need pixels.
- Drive a running game live: start it in the background with `<godot.editor> --path src/Game
  --fixed-fps 30 --resolution 1280x720 -- --control-port=7777 [--start-paused]`, poll
  `curl -s localhost:7777/status` until it answers, send scenario commands with
  `curl -s localhost:7777/commands -d '[{"press":"Key4"},{"wait":2},{"screenshot":"ghost"}]'`
  (open the returned screenshot paths to see the frame; use `pause` and `step` to go frame by
  frame), and finish with `curl -s -X POST localhost:7777/quit`.
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
