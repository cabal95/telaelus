# Resources and harvest

- Status: Implemented
- Id: 2611100903-resources-and-harvest
- Depends-on: none

## Summary

Add resource nodes on the grid, a global stockpile, hand-harvesting, and a HUD.

## Motivation

Give the world raw materials and the first gather loop: walk to a node, harvest, watch a count rise.

## Design

- `ResourceType` starts with Ore and Wood. A `ResourceNode` has an id, type, position, and a
  depleting amount. A fixed set of eight nodes is spawned.
- The stockpile maps resource type to amount. Each tick the nearest node within 1.5 units of the
  player is the harvest target; holding `E` pulls 10 per second into the stockpile; a depleted node
  is removed.
- A HUD shows totals and an `[E] harvest` hint when in range.

## Code changes

- `src/Sim/ResourceType.cs`, `src/Sim/ResourceNode.cs`; `PlayerIntent.Harvest`.
- `Simulation`: nodes list, stockpile, `HarvestTarget`, `UpdateHarvestTarget`, `Harvest`,
  `AddToStockpile`, `RemoveNode`, `SpawnInitialNodes`.
- `src/Presentation/ResourceNodeViews.cs` (a box per node, shrinking by remaining fraction),
  `src/Presentation/Hud.cs`; `GameRoot` wiring and the harvest input.

## Blueprint updates

- Establishes `resources.md`; updates `player.md` (harvest input) and `presentation.md` (HUD, node
  view).

## Verification

Harvest a node: the total rises, the node shrinks and disappears when empty.
