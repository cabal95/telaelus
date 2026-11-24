# Processors Work Only on the Network

- Status: Implemented
- Id: 2611241302-network-only-processing

## Summary

Every building works only while online. Loading an offline processor or fuel burner by hand goes
away, and with it the input buffer and the fuel slot, which nothing else fills. The detail panel
drops the Buffer row's "in:" part and the Burning row's "N in slot".

## Motivation

The panel's Buffer row shows "in: Iron Ore 0 / 5" and the Burning row "0 in slot" on every online
processor, because online buildings take their inputs and fuel straight from storage; only hand
loading an offline building ever fills them. The intended rule is that buildings need the network
to operate. Collectors already follow it; offline processors are the exception, running on
hand-loaded inputs (buildings.md, Production; resources.md, Collecting and loading by hand).

## Design

- **Offline processors do nothing.** A processor (fuel burner or not) starts no cycle and its cycle
  holds while offline, as a powered consumer's does today; it resumes where it was when online.
  Its output buffer keeps what it holds, which the player can still collect by hand.
- **No hand loading.** The E interaction only collects. `InteractAction` goes (Collect is all that
  is left), and the hint for an offline building with nothing to collect is gone (nothing to do
  there).
- **No input buffer.** A cycle takes its inputs from storage when it starts (online, so always).
  A cycle cancelled or switched away from returns what it took to storage (past limits, as a
  destroy refund does), online or not.
- **No fuel slot.** A fuel burner takes each unit from storage when its fire is out, online only.
  The unit burning burns out wherever the building is, as now.
- **Statuses:** "Waiting for Iron Ore (load by hand)" and "Waiting for fuel (load by hand)" go; an
  offline processor or burner reads "Offline: outside the network" like an offline extractor (a
  powered one still reads "No Power: offline" first).
- **Panel:** the Buffer row shows only the output ("Iron 4 / 10"); the Burning row shows only the
  fire ("Coal, 22 s left" or "nothing").
- **Old saves:** whatever a building's input buffer and fuel slot held goes to storage on load,
  past limits.
- `BuildingDef.InputCycles` and `BuildingDef.FuelSlotSize` go; the table and building descriptions
  stop mentioning "inputs 5 cycles".

## Code changes

- `src/Telaelus.Sim/Building.cs`: remove `Input` and `FuelSlot`.
- `src/Telaelus.Sim/BuildingDefs.cs`: remove `InputCycles` and `FuelSlotSize`.
- `src/Telaelus.Sim/Simulation.cs`, `SimulationRecipes.cs`, `SimulationCapacity.cs`: processor
  start, cancel and switch returns, and fuel take without the buffer or slot; offline processors
  hold; remove `ReturnStaleInputs`.
- `src/Telaelus.Sim/SimulationNetwork.cs`: remove `InteractAction`, `NextLoad`, `NextInputLoad`,
  `LoadsFuel`, `InputCapacity`; `InteractAmounts` becomes `InteractHeld` (the units left).
  `SimulationCapacity.cs`: remove `NextFuelLoad`.
- `src/Telaelus.Sim/SaveData.cs`, `SimulationSave.cs`: `Input` and `FuelSlot` read only, moved to
  storage on load; no longer written.
- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: Buffer and Burning rows; status texts.
- `src/Telaelus/Presentation` hint and trainer: no Load wording or action check.
- `src/Telaelus/Testing/StateSnapshot.cs`: drop `player.interactAction` and each building's `input`
  and `fuelSlot`.
- Tests: `NetworkTests` replaces the hand-loaded Smelter with an offline Smelter doing nothing and
  offering nothing to load, a Smelter going offline holding its cycle and finishing it online, and
  a cancelled offline cycle returning its inputs to storage; `PowerTests` expects an offline Burner
  Generator to read "Offline: supplies nothing"; `RefiningTests` drops the stale-input return;
  `SaveTests` adds an older save's input buffer and fuel slot going to storage. The `network`
  scenario drops its `interactAction` check (no scenario loaded by hand).

## Blueprint updates

- buildings.md: Production (processors online only), Fuel (no slot), Recipes and refining (no
  input buffer; returns to storage), statuses, the table's buffer column notes.
- resources.md: Collecting by hand only; the Load bullet goes.
- presentation.md: the Buffer and Burning rows; the hint.
- saving.md: old saves' input and slot go to storage.
- testing.md: any scenario steps that change.

## Verification

- Sim and integration tests pass.
- A windowed screenshot of an online Smelter's panel: Buffer and Burning rows without the empty
  parts (the `crafting-queue` scenario's "queue" frame: "empty / 10", "Coal, 40 s left").

## Implementation notes

- The save load moves an older save's input and slot into storage after the stockpile is restored,
  so the restore does not overwrite them.
- testing.md's `SaveTests` entry also gains the two tests from the earlier fix for a switched-off
  extractor's removed field, which that commit left out of the list.
