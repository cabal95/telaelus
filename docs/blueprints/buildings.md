# Buildings Blueprint

## Overview

Buildings are placed on the grid and run the production chain that turns raw resources into fuel.
Accumulating enough refined output lets the player launch a ship, which closes the on-planet loop.

## Current state

### Building types and recipes

- `BuildingType` has three values: **Extractor**, **Smelter**, **Refinery**.
- A `Recipe` has inputs, outputs (each a `ResourceAmount`), and a cycle time. Recipes are defined
  centrally:
  - Extractor: no recipe; it pulls from a bound node at `ExtractorRate` (5 per second).
  - Smelter: 1 Ore to 1 Metal, cycle 1 s.
  - Refinery: 1 Metal plus 1 Wood to 1 Fuel, cycle 2 s.
- A `Building` has an id, type, grid position, optional recipe, an optional bound source node (for
  extractors), and cycle state (`Crafting`, `Progress`).

### Placement

- Tiles are tracked as occupied in a set. `CanPlace(type, x, z)` requires the tile to be in bounds,
  not occupied, and passable terrain (grass). An Extractor additionally requires a resource node on
  the tile; a processor (Smelter, Refinery) requires no node on the tile.
- `TryPlaceBuilding(type, x, z)` validates via `CanPlace`, then adds the building, binds the source
  node for an extractor, and marks the tile occupied. A building blocks player movement for as long
  as it stands (its tile is not walkable).

### Production

- Each tick, every building steps: an extractor pulls from its source node into the stockpile and
  nulls its source when the node depletes; a processor, when idle, consumes its recipe inputs from
  the stockpile if available and starts a cycle, then adds the outputs when the cycle completes.
- All buildings operate on the shared global stockpile. This is the abstract stand-in for logistics
  until vehicles and belts are added.

### Ship launch (win condition)

- When the stockpile holds at least `LaunchFuelCost` (500 Fuel) and `LaunchMetalCost` (100 Metal),
  `CanLaunchShip` is true. `TryLaunchShip` consumes that cost and increments `ShipsLaunched`. This
  is the current win that closes the initial loop.

### Rendering and interaction

- `BuildingViews` renders one colored box per building (Extractor orange, Smelter red, Refinery
  blue). `BuildController` drives build mode: number keys 1/2/3 select a type, Escape or 0 cancels;
  a ghost box tracks the mouse tile (via a camera ray to the ground) and is green when placeable,
  red when not; left-click issues the placement command. The HUD shows the four resource totals,
  the launch progress and prompt, and the current build selection.

## Related

- resources.md (stockpile, nodes), simulation.md (commands), presentation.md (views, HUD, build UI).
