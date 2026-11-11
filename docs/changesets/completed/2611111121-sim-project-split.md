# Split the Simulation into Its Own Project

- Status: Implemented
- Id: 2611111121-sim-project-split
- Depends-on: none

## Summary

Move the simulation into its own C# project, `Game.Sim`, and move the Godot project into its own
folder, `Game`, so `src/` holds one subfolder per project. The game references the sim; the sim
references nothing of ours. Behavior does not change.

## Motivation

- The sim/presentation seam is currently a convention inside one project. As separate projects,
  the dependency direction is enforced by the compiler: `Game.Sim` cannot see presentation code.
- A test project (changeset sim-unit-tests, implemented after this one) can reference
  `Game.Sim` directly instead of compiling its source files or pulling in the Godot SDK.
- Each project gets its own folder under `src/`, which leaves room for the test project beside
  them.

## Design

### Layout

```
game.slnx                  (all projects)
src/
  Game/                    Godot project root (was src/)
    project.godot
    Game.csproj            Godot.NET.Sdk; references Game.Sim
    Presentation/
    assets/
    scenes/
  Game.Sim/                plain class library (was src/Sim/)
    Game.Sim.csproj
    *.cs
```

- The Godot project root becomes `src/Game`. `res://` paths are relative to the project root, and
  `Presentation/`, `assets/`, and `scenes/` move with it, so scene and asset paths do not change.
- `Game.Sim` sits beside the Godot project, not inside it, so Godot neither imports it nor
  compiles its files into `Game.dll`.
- Namespaces do not change: sim types stay in `Game.Sim`, presentation in `Game.Presentation`.

### Projects

- **`Game.Sim.csproj`:** `Microsoft.NET.Sdk` class library, `net8.0`, `Nullable` enabled,
  `RootNamespace` `Game.Sim`, `EnforceCodeStyleInBuild` on (so the analyzer rules in the root
  `.editorconfig` apply as they do today). It has a `PackageReference` to `GodotSharp` **4.7.2**
  for the math types the sim uses (`Vector2`, `Vector3`, `Mathf`). That version must match the
  Godot.NET.Sdk version in `Game.csproj`; upgrading Godot means bumping both together.
- **`Game.csproj`:** unchanged settings (`assembly_name` stays `Game`), plus a `ProjectReference`
  to `../Game.Sim/Game.Sim.csproj`. Building it builds the sim and copies `Game.Sim.dll` beside
  `Game.dll` in Godot's output folder, where the engine loads it.
- **`game.slnx`:** lists both projects.

### Godot editor and project settings

- Running the game (`--path src/Game`) uses the assemblies `dotnet build` produces, so no
  `project.godot` change is expected for that.
- The editor opens `src/Game`, builds, and runs the game with no setting changes: no solution
  file next to `project.godot` is needed, `project.godot` is unchanged, and the editor creates no
  new files. (Had the editor needed a manual change, the user would make it and we would record
  the resulting diff here.)

### Housekeeping

- The `.cs.uid` files under `src/Sim/` are Godot metadata for scripts inside the project. The sim
  files leave the project, so their `.uid` files are deleted. Presentation `.uid` files move with
  their scripts.
- The git-ignored `.godot/` cache moves to `src/Game/.godot/` so assets don't need re-importing.
  If it is lost, opening the editor once (or a headless `--import`) rebuilds it.
- Completed changesets keep their old paths; they describe the project as it was.

## Code changes

- `git mv` `src/project.godot`, `src/Game.csproj`, `src/Presentation/`, `src/assets/`, and
  `src/scenes/` into `src/Game/`.
- `git mv` the `src/Sim/*.cs` files into `src/Game.Sim/`; delete `src/Sim/*.cs.uid`.
- New `src/Game.Sim/Game.Sim.csproj`.
- `src/Game/Game.csproj`: add the `ProjectReference` to `Game.Sim`.
- `game.slnx`: add `src/Game.Sim/Game.Sim.csproj`, update the `Game.csproj` path.
- No C# source changes are expected. If a sim type needs `public` because the game can no
  longer see an `internal` member across the assembly boundary, that is noted here when found.
- `.editorconfig`: update the comment that names `src/Game.csproj`.
- `src/Game/assets/models/nature/README.md`: the `NodeModels.cs` path becomes
  `src/Game/Presentation/NodeModels.cs`.
- `CLAUDE.md`: the project root is `src/Game`; commands become
  `dotnet build src/Game/Game.csproj -c Debug` and `<godot.editor> --path src/Game` (editor and
  headless variants likewise); the architecture section names `src/Game.Sim` and
  `src/Game/Presentation`, and notes the seam is enforced by the project reference.

## Blueprint updates

- `simulation.md`: the sim is the `Game.Sim` class library in `src/Game.Sim`; it references only
  the `GodotSharp` package (math types), and the game references it, never the reverse.
- `presentation.md`: the Godot project root is `src/Game`; paths for the main scene and assets
  update; `Game.csproj` references `Game.Sim`.
- `player.md`, `resources.md`, `terrain.md`: asset paths become `src/Game/assets/...`.

## Verification

- `dotnet build game.slnx -c Debug` builds both projects with no warnings or errors.
- The headless run (`--headless --path src/Game --quit-after 20`) has empty stderr.
- The game runs from `--path src/Game` and plays as before (spawn, harvest, build, rotate).
- The Godot editor opens `src/Game`, builds, and runs the game with no manual steps.
- `git log --follow` on a moved file still shows its history.
