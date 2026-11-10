# Code Style

C# conventions for this project. Some are enforced by analyzers (configured in
[.editorconfig](../.editorconfig) and run during `dotnet build` because the csproj sets
`EnforceCodeStyleInBuild`); others we follow by hand because no analyzer the editor honors can
enforce them.

## Enforced (warnings in the IDE and in `dotnet build`)

- **Allman braces.** The opening brace goes on its own line for every construct: types,
  methods, control blocks, and initializers. Never write `if (x) {` on one line.
  (IDE0055, via `csharp_new_line_before_open_brace = all`.)
- **Braces on every control-flow body.** Even a single-statement `if`, `for`, `while`, or
  similar body gets braces, on their own lines. No single-line bodies.
  (IDE0011, via `csharp_prefer_braces = true`.)

```csharp
// Yes
if (ready)
{
    Start();
}

// No
if (ready) Start();
if (ready) {
    Start();
}
```

## Conventions (followed by hand; not analyzer-enforced)

- **Object, collection, and array initializers are always multi-line**, one member per line,
  even for a single member:

```csharp
// Yes
_sim.Intent = new PlayerIntent
{
    Move = ReadWorldMoveInput()
};

// No
_sim.Intent = new PlayerIntent { Move = ReadWorldMoveInput() };
```

  The Roslyn formatter that VS Code uses leaves inline initializers alone, so this cannot be a
  warning here. (Rider could enforce it with
  `resharper_wrap_object_and_collection_initializer_style = chop_always`.) We keep it by
  convention instead.

## General

- **Match the surrounding code**: naming, comment density, and idioms.
- **Expression-bodied members** are fine for trivial one-liners (for example
  `public void SetTarget(Vector3 t) => _target = t;`).
- **Keep the sim/presentation seam.** Game logic lives in the simulation, rendering and input
  in the presentation. This is architecture rather than formatting; see
  [game-plan.md](game-plan.md) and the project `CLAUDE.md`.

This is a living document. When we settle a new convention, record it here.
