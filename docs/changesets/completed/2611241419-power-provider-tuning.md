# Water Wheel and Burner Generator Tuning

- Status: Implemented
- Id: 2611241419-power-provider-tuning

## Summary

The Water Wheel supplies 5 Power instead of 20. The Burner Generator supplies 35 Power instead of
100 and grows from 2 x 2 to 4 x 4, keeping its current model scaled up.

## Motivation

Play showed early power is too plentiful: one Burner Generator runs most of a base, and a Water
Wheel gives free power that never needs tending. Smaller outputs make power something the player
plans and expands, and a bigger generator makes it a real presence in the base.

## Design

| Provider | Footprint | Supplies |
|---|---|---|
| Water Wheel | 4 x 2 (unchanged) | 20 → **5 Power** |
| Burner Generator | 2 x 2 → **4 x 4** | 100 → **35 Power** |

- Costs, fuel, placement, and the "burns whenever switched on" rule are unchanged.
- **The model:** the Burner Generator's existing design is regenerated at 4 x 4 in the asset
  factory (the same parts, scaled to fill the larger footprint), with its kept preview, so the
  model still fills its footprint like every other building.
- **Saved games:** a saved Burner Generator grows around its old center (its anchor moves 1 cell
  back on each axis, so the 2 x 2 sits in the middle of the 4 x 4). If any of the new cells is
  taken or cannot be built on, the generator is removed on load and its full cost goes back to
  storage (past the storage limit if need be, as a refund already does), with a notification
  ("A Burner Generator no longer fit and was refunded").
- **Late game:** the cuts are deliberate. Consumers draw 5 to 80 Power, so a tier 3 base outgrows
  burners and Water Wheels and pushes the player toward other power sources.
- **Balance knock-on:** a base that ran on one generator now needs about three. The demo town and
  scenarios that power a base with one Burner Generator or Water Wheel are checked; any that would
  now brown out get more providers, so they still test what they say.

## Code changes

- `src/Telaelus.Sim/BuildingDefs.cs`: the two supplies and the Burner Generator's footprint.
- `src/Telaelus.Sim/SimulationSave.cs`: the load step that grows a Burner Generator saved at
  2 x 2, or refunds it. `SaveFormat.Version` stays 1 (the step keys off the saved width).
- `src/asset-factory`: the Burner Generator regenerated at 4 x 4, its `.glb`, and its previews.
- Tests: `PowerTests`, `CapacityTests`, `RenewablesTests` and the others that assert supply
  amounts; a `SaveTests` case for each load outcome (grows, refunded).
- Scenarios (`power`, `renewables`, `first-ship`, `later-tiers`, `refining`) and `DemoTown.cs`
  checked for supply; the demo save rewritten if its power changes.

## Blueprint updates

- buildings.md: the provider table and the building table rows.
- saving.md: the load rule for old Burner Generators.
- testing.md: the new tests.

## Verification

- Sim and integration tests pass.
- A windowed run shows the 4 x 4 generator filling its footprint.
- A save made before the change loads with its generator grown or refunded.

## Implementation notes

- Saves had no footprint width, so `BuildingData` gains `Width` (0 in older saves). A Burner
  Generator whose saved width is not its own grows or is refunded; `SaveFormat.Version` stays 1.
- The refund notification is held through the quiet load and raised on the first step after it
  (`SimEventKind.RefundedOnLoad`): lasting, the building's picture, "Burner Generator refunded".
- The model is the same design built at 2 x 2 and scaled by 2 (`Mesh.scaled`), re-exported and
  re-imported; its preview, the demo save, and the splash were re-rendered.
- Tests that powered a base with one or two generators now use `SimTestHelpers.Burners` (a row of
  4 x 4 generators along the empty map's north edge) sized to the demand; the end-to-end tests
  use seven, nine, and thirteen. Brownout tests keep their steps with speeds worked from 105
  Power. The `power` scenario runs on two generators (brownout at 85 / 70, blackout at 110 / 70);
  `refining`, `first-ship`, and `later-tiers` use six, `renewables` two, `notifications` three.
