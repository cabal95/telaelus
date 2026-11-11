# Block Building Placement on the Player

- Status: Implemented
- Id: 2611111234-block-placement-on-player
- Depends-on: none

## Summary

A building can no longer be placed with the player inside its footprint. This also writes down
when a change needs a changeset: when it alters what the blueprints describe, which includes this
fix because the placement rule itself changes.

## Motivation

- Movement collision stops the player from walking into a building, but placement does not check
  the player, so a building can be placed on top of them. The player is then trapped: every cell
  around them belongs to the building, so no direction is walkable.
- Reproduced with an ad hoc scenario (`test-output/adhoc/place-on-player.json`, not kept): on an
  empty map with the player at (32, 32), select the Smelter and aim at cell (32, 32). The ghost
  reads as placeable. Clicking places the Smelter at anchor (31, 31) with the player inside (the
  screenshot shows the character's head above the roof). Holding A, D, W, and S for 30 frames each
  then leaves the player at exactly (32, 32).
- The buildings blueprint lists the placement rule as in bounds, grass, unoccupied, and not on a
  field, with no mention of the player. The blueprint describes the bug as the rule, so fixing it
  changes the rule.

## Design

### The placement rule

- `CanPlace` additionally rejects a footprint that contains the **player's cell**: the fine cell
  the player's position falls in (`CellAt(player.X, player.Z)`), the same point-based test
  movement collision uses. Placement and walking now agree: a building can go exactly where the
  player could not walk into, and nowhere the player is standing.
- A building may still be placed right next to the player (their cell just outside the
  footprint). As with walking up to a building today, the character model can visually overlap
  the building's edge slightly, because collision treats the player as a point.
- The blueprint words the rule as "no unit inside the footprint", with the player as the only
  unit today. Worker vehicles and a second co-op player are planned; they block placement the same
  way when they arrive, without another rule change.
- `TryPlaceBuilding` and `Simulation.Debug.PlaceBuilding` go through `CanPlace`, so both reject
  it. The build ghost turns red over the player with no presentation change, because it asks the
  sim whether placement is legal. `Simulation.Debug.TeleportPlayer` can still move the player into
  a building; it is a test tool and bypasses rules by design.

### When a change needs a changeset (process)

Add to `docs/development.md`:

- A changeset is required when a change alters what the blueprints describe: new behavior, a
  changed or new rule, or a design change.
- A fix that brings the code in line with what a blueprint already says needs no changeset. It is
  a normal commit with a regression test (a unit test, or a scenario for input or presentation),
  and the commit message names the bug and the blueprint it restores.
- If the blueprint is silent on the behavior, or describes the buggy behavior as the rule, the fix
  changes the rule, so it is a changeset (like this one). Reproducing a bug first (a test, a
  scenario, or the live channel) changes no code and can happen before the changeset is drafted.

## Code changes

- `Simulation.CanPlace`: also return false when the footprint contains `CellAt` of the player's
  position.
- `PlacementTests`: new tests (see Verification).
- New permanent scenario `src/Game.Integration.Tests/Scenarios/place-on-player.json`.

## Blueprint updates

- `buildings.md`, Placement: the standard rule gains "and no unit inside the footprint (today, the
  player's cell, the fine cell their position falls in)", with the note that placement and
  movement collision use the same point-based test.
- `testing.md`: add `place-on-player` to the current scenarios.

Process docs (not blueprints) also change in this commit: `docs/development.md` (when a change
needs a changeset, as above).

## Verification

- Unit tests (`PlacementTests`), each failing before the fix and passing after where noted:
  - `CanPlace` is false when the footprint covers the player's cell, including when the player
    stands on a footprint edge or corner cell (fails before the fix).
  - `CanPlace` is true when the player's cell is directly adjacent to the footprint.
  - `TryPlaceBuilding` on the player fails and adds no building (fails before the fix).
  - A player standing exactly on a cell boundary counts as the cell their position floors to.
- Scenario `place-on-player`: on an empty map, select the Smelter, aim at the player's cell
  (32, 32), expect the ghost not placeable; click, expect no buildings; aim at cell (35, 32),
  which puts the Smelter at anchor (34, 31) just clear of the player, expect placeable, click,
  expect the Smelter at (34, 31); hold A, expect the player to have walked away.
- The ad hoc reproduction, rerun after the fix, fails at its first "BUG" expectation (the ghost is
  no longer placeable).
- `dotnet test game.slnx` passes: all existing sim tests and scenarios still pass.
