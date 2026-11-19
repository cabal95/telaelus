# Building Table

- Status: Implemented
- Id: 2611191454-building-table
- Depends-on: 2611191433-toasts-under-modals

## Summary

The buildings blueprint gains an "At a glance" table: every building's tier, size, build cost,
the capacity it draws and supplies, what it burns or eats, and what it makes, in one place. A sim
test rebuilds the table from the code and fails when the blueprint's copy differs, so a balance
change updates the table in the same change.

## Motivation

The numbers a balancing decision needs are spread across the blueprint's many tables (costs in
one, draws in another, recipes in a third, renewables and power in their own sections). Comparing
buildings means reading them all. One table makes the comparison at a glance; a copy of numbers
that live in code drifts unless something checks it.

## Design

### The table

- A new section, "At a glance", after "Building types and definitions" in buildings.md, with one
  row per building in `BuildingType` order and the columns Building, Tier, Size, Build cost,
  Draws, Supplies, Burns / eats, and Makes.
- **Draws** and **Supplies** are capacity per second (Power, Water, Sand); a Solar Array's supply
  is marked "(daylight)" and a Battery Bank's store is listed. **Burns / eats** is a fuel burner's
  fuels and an eating provider's food and rate.
- **Makes** lists each recipe as inputs → outputs (cycle time), with alternative inputs as "Coal or
  Charcoal" and recipes that use only drawn capacity marked "(capacity only)"; an extractor's
  deposits, work radius, and speed; a collector's rate; a Forester's plot; and other roles (hub
  reach, truck bays, research, storage).
- The rules stay in their sections; the table only gathers their numbers.

### The rule it brings

- `BuildingTableTests` builds the table from `BuildingDefs` and `Recipes` and compares it with the
  blueprint's, line for line. On a difference it fails and prints the whole table as it should
  read, ready to paste.
- So any change to a building's cost, size, tier, draw, supply, fuel, or recipe updates its row in
  buildings.md in the same change, or the sim tests fail.
- Three collectors whose output the definitions cannot state plainly (the Quarry, Deep Well, and
  Deep Mine) have their wording in the test, beside the generator.

## Code changes

- `src/Telaelus.Sim.Tests/BuildingTableTests.cs` (new): the table generator and the comparison.

## Blueprint updates

- buildings.md: the "At a glance" section and its note that the test keeps it in step.
- testing.md: `BuildingTableTests`.

## Verification

- Sim tests pass (383).
- The test caught a real slip on its first run (the HQ's reach missing "units"), and changing one
  cost in the blueprint's copy (the Sawmill's 30 Wood to 31) failed it until restored.

## Decisions

- Recorded as a changeset because the test adds a rule for later work, though the table itself
  changes no behavior.
