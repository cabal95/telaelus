# Name the Game Telaelus

- Status: Implemented
- Id: 2611151521-telaelus-rename
- Depends-on: 2611151454-save-and-load

## Summary

The game gets its permanent name, **Telaelus** (tel-AY-lus), and the working title "Game" goes
away everywhere it names this project: the window title, the main menu, the C# namespaces and
assemblies, the project folders and files, the solution, and the docs. A new `README.md` at the
repo root introduces the game: what it is, how to say its name, where the name comes from, and
where to go next.

## Motivation

"Game" was always a placeholder (`CLAUDE.md` calls it the working title, and the main menu shows
it). The name is now chosen, and the longer the placeholder stays in namespaces and paths, the more
code and docs carry it. The repo also has no README: someone opening it today lands on a folder
list with no idea what the project is.

## The name

**Telaelus** blends the Roman pair **Tellus**, the earth, and **Caelus**, the sky. In Roman myth
Caelus is the sky god and the husband of Terra, the earth (identified with Tellus). The game runs
the same way: build up on a planet's surface (Part A), then take to space (Part B).

- Pronunciation: **tel-AY-lus**, stress on the middle syllable; IPA /tɛˈleɪləs/. Three
  syllables: "tel" as in "tell", "ay" as in "day", "lus" as in "Venus".
- Checked on 2026-11-15: no game or product named Telaelus on Steam, itch.io, or a general web
  search. Trademark databases, domains, and social handles are not checked yet (not part of this
  change).

## Design

### What changes name

| Today | After |
|---|---|
| `src/Game/` (Godot project root) | `src/Telaelus/` |
| `src/Game/Game.csproj` | `src/Telaelus/Telaelus.csproj` |
| `src/Game.Sim/`, `Game.Sim.csproj` | `src/Telaelus.Sim/`, `Telaelus.Sim.csproj` |
| `src/Game.Sim.Tests/`, `Game.Sim.Tests.csproj` | `src/Telaelus.Sim.Tests/`, `Telaelus.Sim.Tests.csproj` |
| `src/Game.Integration.Tests/`, `Game.Integration.Tests.csproj` | `src/Telaelus.Integration.Tests/`, `Telaelus.Integration.Tests.csproj` |
| `game.slnx` | `Telaelus.slnx` |
| Namespaces `Game`, `Game.Presentation`, `Game.Presentation.Ui`, `Game.Testing` | `Telaelus`, `Telaelus.Presentation`, `Telaelus.Presentation.Ui`, `Telaelus.Testing` |
| Namespaces `Game.Sim`, `Game.Sim.Tests`, `Game.Integration.Tests` | `Telaelus.Sim`, `Telaelus.Sim.Tests`, `Telaelus.Integration.Tests` |
| `RootNamespace` in each project file | to match the new namespaces |
| `project.godot`: `config/name="Game"` | `config/name="Telaelus"` (the window title, and the name Godot uses for the player's data folder) |
| `project.godot`: `dotnet/project/assembly_name="Game"` | `"Telaelus"` |
| `MainMenu.Title` (`"Game"`) | `"Telaelus"` |
| Scenario schema `"title": "Game scenario"` | `"Telaelus scenario"` |

Folders and files move with `git mv`, so history follows them. Each `.cs` file keeps its name; only
its `namespace` line and `using` lines change. The `.uid` files and every `res://` path stay as
they are, since they are relative to the Godot project root, which moves as a whole.

### What keeps the word "game"

The word stays wherever it means "a game" rather than naming this project:

- Classes: `GameRoot` (the root of a game in play), `GameMenu` (the in-game menu). Renaming them to
  `TelaelusRoot` would read worse and say less.
- Menu text: **New Game**, **Load Game**, **Save Game**, and similar.
- Scenario commands such as `newGame` and `saveGame`.
- Prose such as "the game camera" or "in game".

### Paths in tools and docs

Everything that names a path or project moves with it:

- `CLAUDE.md`: the title ("Project: Telaelus"), every `src/Game...` path, `game.slnx`, and the
  build and test commands.
- `.editorconfig`: the project file paths in its header comment.
- `ScenarioTests.cs` and `Paths.cs`: the Godot project path (`src/Telaelus`) and the scenario
  folder.
- `stress_scenarios.py`: `GAME = REPO / "src" / "Telaelus"` and its usage text.
- Asset factory tools (`building_gen.py`, `item_gen.py`, `resource_gen.py`, `terrain_gen.py`,
  `unit_gen.py`, `town_layout.py`): output paths and doc comments.
- The asset folder READMEs under `src/Telaelus/assets/models/`, and `src/asset-factory/README.md`.
- Docs: `development.md`, `asset-pipeline.md`, `game-plan.md` (the title becomes "Telaelus: Game
  Plan"), and every blueprint that names a path or namespace (`testing`, `presentation`, `saving`,
  `simulation`, `terrain`, `buildings`, `player`, `resources`, `ui-style`).

**Completed changesets stay as written.** They record what was true when they landed, so their
`src/Game` paths are history, not mistakes. The changeset index gets this changeset's line like any
other.

### The player's data folder (saves)

Godot keeps `user://` (saves, and later settings) in a folder named after `config/name`, so on
Windows it moves from `%APPDATA%\Godot\app_userdata\Game\` to
`%APPDATA%\Godot\app_userdata\Telaelus\`. Saves made before the rename are not found by the renamed
game. The game has no players but us yet, so the change does not migrate them; the changeset's
notes say how to copy the `saves` folder across by hand.

Decision 2 covers why it does not get its own top-level folder yet.

### README.md

A new `README.md` at the repo root, for anyone opening the repo:

- **Telaelus**, with the pronunciation right under the title: *tel-AY-lus* (/tɛˈleɪləs/).
- Where the name comes from: Tellus and Caelus, earth and sky, and how that matches the two parts.
- Why it exists: a learning project first, built to learn game development hands-on (rendering,
  the game loop, systems design, and the math of a 2.5D world), and a playable game second.
- What the game is, in a few short paragraphs drawn from `docs/game-plan.md`: a 2.5D factory and
  resource game on a planet (Part A) that builds and supplies a fleet, then space exploration and
  combat with that fleet (Part B); single-player first, built with co-op in mind; low-poly,
  flat-colored art under an angled orthographic camera.
- Where it stands: Part A in progress, with a short list of what plays today (procedural worlds
  with biomes and fog of war, gathering, buildings and production chains, power, trucks and roads,
  research, saving and loading).
- Built with:
  - Godot 4.7 and C# (.NET 8), with a separate engine-free simulation library.
  - **Claude (Anthropic's AI) writing the code, with human review** of every change, so the human
    side learns how the code works rather than only what it does. Changes are specified first as
    changesets and approved before code is written.
  - **UI and graphics designed by AI too**, through a feedback loop: the 3D models come from Python
    generators in the asset factory and are rendered to preview images, and scripted scenarios run
    the game and capture screenshots. The AI reviews those images itself, revises, and repeats, and
    the human reviews the result.
- Getting started: the tools needed (Godot 4.7.2 .NET, the .NET 9 SDK), `tools.json`, and the build
  and run commands, kept short and pointing at `CLAUDE.md` for the full list.
- Where to read more: `docs/game-plan.md` (the vision and roadmap), `docs/blueprints/INDEX.md` (how
  each system works today), `docs/development.md` (how we work).

The README describes the game and points elsewhere for detail, so it stays accurate without much
upkeep. It is updated when a part of the game ships, not with every changeset.

## Verification

- `dotnet build Telaelus.slnx -c Debug` builds with no warnings beyond today's.
- `dotnet test src/Telaelus.Sim.Tests` and `dotnet test src/Telaelus.Integration.Tests` pass.
- A headless import (`--headless --path src/Telaelus --import`) and the headless runtime check
  (`--quit-after 20`) run with empty stderr. The old `.godot/mono` build output names the old
  assembly, so delete `src/Telaelus/.godot` before the first run, and Godot rebuilds it.
- A windowed run shows "Telaelus" in the window title and on the main menu, and New Game, Save, and
  Load work (a save lands in the new data folder).
- `git grep -n "Game\.Sim\|Game\.Presentation\|Game\.Testing\|src/Game\b\|game\.slnx\|namespace Game\b"`
  finds nothing outside `docs/changesets/completed/`.
- The asset factory tools still write to the right place: run `town_layout.py` and confirm it
  writes `src/Telaelus/scenes/Town.tscn` with no other change.

## Not in this change

- Renaming the repository folder (`D:\source\game`) or a remote repository. Both are the owner's to
  do whenever suits; nothing in the repo depends on the folder's name.
- A logo, an app icon, or a title treatment on the splash image.
- Trademark, domain, and handle checks.
- The asset preview project (`asset-lab`): it is a tool, not the game, and keeps its name.

## Decisions

1. **Class names:** `GameRoot` and `GameMenu` keep their names; there "game" means a game in play.
2. **Data folder:** Godot's default location, `%APPDATA%\Godot\app_userdata\Telaelus\`. A
   custom top-level folder can come with a later settings or release change.
3. **Old saves:** copied across by hand; the game does not migrate them.
4. **Solution file:** `Telaelus.slnx`.

## Implementation notes

- The project folders were moved file by file rather than renamed whole: an open VS Code window
  held a handle on `src/Game`, so a folder rename failed with "permission denied". Git still sees
  the moves as renames.
- The old `.godot`, `bin`, and `obj` folders were deleted with the old project folders; the first
  build and import recreated them under the new names.
- `town_layout.py` was not re-run to check its new output path, since that would overwrite
  `Town.tscn`, which is arranged by hand. Its path was checked instead.
- `.vscode/settings.json` (git-ignored) now names `Telaelus.slnx` as the default solution. VS Code
  and Visual Studio need a reload to pick up the moved projects.
