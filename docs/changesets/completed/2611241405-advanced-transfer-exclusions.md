# Advanced Material Transfer Skips the Rare Resources

- Status: Implemented
- Id: 2611241405-advanced-transfer-exclusions

## Summary

Advanced Material Transfer no longer carries Titanium Ore, Rare Earths, Uranium Ore, or Crystals.
Those four reach storage only by hand collection or trucks.

## Motivation

Research should automate the bulk resources, not the rare late-game ones. Leaving the rarest four
to trucks and hand collection keeps them something the player has to go out and bring home.

## Design

- **Advanced Material Transfer** carries Crude Oil, Bauxite, Limestone, Quartz, and Sulfur.
  Titanium Ore, Rare Earths, Uranium Ore, and Crystals drop off its list.
- Material Transfer is unchanged (Wood, Stone, Iron Ore, Copper Ore, Coal).
- Its description changes from "every other raw resource" to "more raw resources": "The network
  carries more raw resources from online collectors straight into storage."
- Saves need nothing: the transfer list comes from the definition.
- Request wording note: the request says "Titanium". The research carries Titanium Ore (the raw
  resource), so that is what is dropped; refined Titanium was never transferred.

## Code changes

- `src/Telaelus.Sim/ResearchDefs.cs`: the transfer list and description.
- `src/Telaelus.Sim.Tests/ResearchTests.cs`: a Drill Rig's Rare Earths are not drained after
  Advanced Material Transfer, while its Bauxite still is. Existing asserts on the list updated.
- The `research` scenario checked for the description or a drained rare resource.

## Blueprint updates

- research.md: the project table and the opening line.
- buildings.md: the Material Transfer note under collectors.
- testing.md: the changed `ResearchTests` line.

## Verification

- Sim and integration tests pass.
