# Fix the Scenario Schema and Check It in Tests

- Status: Implemented
- Id: 2611111238-scenario-schema-checks
- Depends-on: none

## Summary

Fix `scenario.schema.json`, which rejected every scenario, and add fast tests that check the
schema has no duplicate keys and that every scenario file validates against it.

The code below was written before this changeset (while reproducing and fixing the bug, before
we settled that a blueprint update makes it a changeset). This spec describes that existing work;
approving it approves the implementation as well.

## Motivation

- Editors validating a scenario against its `$schema` reported errors on valid files, for example
  in `boot.json`: "Missing property 'step'" and "Property initMap is not allowed".
- Cause: changeset 2611111216-live-control added a definition named `step` for the step command,
  but `step` was already the name of the wrapper definition that every entry in `steps` refers
  to. JSON lets a later duplicate key replace an earlier one silently, so the schema required
  every step to be the step command. Parsing the file (the only check at the time) does not catch
  duplicate keys.
- `testing.md` says the schema defines the format and scenarios reference it for validation, so
  the schema should be checked by tests, not only by parsing.

## Design

- **Schema fix:** rename the wrapper definition from `step` to `scenarioStep` (with a
  description), and point `steps.items` at it. `step` remains the step command's definition.
- **Tests:** a new `ScenarioSchemaTests` class in `Game.Integration.Tests`. It does not launch
  Godot, so it runs in milliseconds alongside the scenario runs.
  - `Schema_HasNoDuplicateKeys`: walks the schema with `JsonDocument` and fails, naming the path,
    if any object repeats a key.
  - `Scenario_MatchesTheSchema`: one test per scenario file (the same file list as
    `ScenarioTests`), validating it against the schema with **JsonSchema.Net** and listing the
    errors on failure. The schema is loaded once and shared, because JsonSchema.Net registers a
    schema globally by its `$id` and refuses to register the same `$id` twice.
- The integration project gains the `JsonSchema.Net` package (9.6.0), its first dependency
  beyond xUnit.

## Code changes

- `src/Game.Integration.Tests/scenario.schema.json`: `step` wrapper renamed to `scenarioStep`.
- New `src/Game.Integration.Tests/ScenarioSchemaTests.cs`.
- `src/Game.Integration.Tests/Game.Integration.Tests.csproj`: `PackageReference` to
  `JsonSchema.Net` 9.6.0.

## Blueprint updates

- `testing.md`, Integration tests: describe the schema checks (duplicate keys, every scenario
  validated against the schema, one test per file, no Godot launch).

## Verification

- Before the fix, the new tests fail: `Schema_HasNoDuplicateKeys` reports `$.definitions.step`,
  and `Scenario_MatchesTheSchema` fails for every scenario file.
- After the fix, all of them pass.
- A temporary invalid scenario (an unknown `teleport` command and a negative `wait`) fails
  `Scenario_MatchesTheSchema` and names its failing step indexes, so the check is not passing
  vacuously.
- `dotnet test game.slnx` passes: 85 sim tests and 15 integration tests (7 scenario runs, 7
  schema validations, and the duplicate-key check), with no build warnings.
- An editor validating `boot.json` against its `$schema` no longer reports errors.
