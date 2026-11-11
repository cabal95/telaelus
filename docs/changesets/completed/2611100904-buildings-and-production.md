# Buildings and production

- Status: Implemented
- Id: 2611100904-buildings-and-production
- Depends-on: none

## Summary

Add grid-based building placement and the Ore to Metal to Fuel production chain, operating on the
shared stockpile.

## Motivation

Turn gathered raw resources into a real production loop with placement decisions.

## Design

- `BuildingType`: Extractor, Smelter, Refinery. Add Metal and Fuel to `ResourceType`.
- Recipes: Extractor pulls from a bound node at 5 per second; Smelter turns 1 Ore into 1 Metal
  (cycle 1 s); Refinery turns 1 Metal plus 1 Wood into 1 Fuel (cycle 2 s). Processors operate on the
  shared stockpile (the logistics stand-in).
- Placement snaps to the grid: a tile must be in bounds and unoccupied; an Extractor needs a node on
  the tile, a processor needs a clear tile. Build mode uses number keys and a ghost at the mouse
  tile; left-click places.

## Code changes

- `src/Sim/BuildingType.cs`, `src/Sim/ResourceAmount.cs`, `src/Sim/Recipe.cs`, `src/Sim/Recipes.cs`,
  `src/Sim/Building.cs`; add Metal and Fuel to `ResourceType`.
- `Simulation`: buildings list, occupancy set, `CanPlace`, `TryPlaceBuilding`, `NodeAt`,
  `StepBuildings` (extractor and processor stepping), input/consume/produce helpers.
- `src/Presentation/BuildingViews.cs`, `src/Presentation/BuildController.cs` (select, ghost, place);
  HUD shows all four resources and the build selection; `GameRoot` wiring.

## Blueprint updates

- Establishes `buildings.md`; updates `resources.md` (Metal, Fuel, extractors) and
  `presentation.md` (build UI, building views).

## Verification

Place an extractor on an ore node, a smelter, and a refinery; Metal then Fuel rise on the HUD.
