# Buildings Blueprint

## Overview

Buildings are placed on the fine grid and run the production chain that turns raw resources into
fuel. Accumulating enough refined output lets the player launch a ship, which closes the on-planet
loop.

## Current state

### Building types and definitions

- `BuildingType` has four values: **Mine**, **Lumber Camp**, **Smelter**, **Refinery**.
- Each type has a `BuildingDef` (in `BuildingDefs`): a display name, a footprint in fine cells
  (width along X, depth along Z when facing North), and for an extractor, the resource it extracts
  and its work radius.

  | Type        | Footprint | Extracts | Work radius |
  |-------------|-----------|----------|-------------|
  | Mine        | 2 x 2     | Ore      | 6 units     |
  | Lumber Camp | 2 x 2     | Wood     | 6 units     |
  | Smelter     | 3 x 3     |          |             |
  | Refinery    | 2 x 3     |          |             |

- A `Recipe` has inputs, outputs (each a `ResourceAmount`), and a cycle time. Recipes are defined
  centrally:
  - Mine and Lumber Camp: no recipe; each pulls from a field at `ExtractorRate` (5 per second).
  - Smelter: 1 Ore to 1 Metal, cycle 1 s.
  - Refinery: 1 Metal plus 1 Wood to 1 Fuel, cycle 2 s.

### Placement state: anchor and facing

- A `Building` has an id, type, definition, an **anchor** (its minimum-corner fine cell), a
  **facing** (North, East, South, West), an optional recipe, its current field (extractors), and
  cycle state (`Crafting`, `Progress`).
- Anchor and facing are the authoritative placement. East and West swap the footprint's width and
  depth, so a 2 x 3 Refinery facing East is 3 x 2. `Width`, `Depth`, and `Position` (the footprint
  center as a world point) are derived from them, not stored. Facing is kept for belt input and
  output sides later.

### Placement

- `CanPlace(type, anchorX, anchorZ, facing)` applies the standard rule: every footprint cell is in
  bounds, grass, unoccupied, and not covered by a resource field. No building may sit on a
  resource. Placement rules belong to the definition, so a future exception (a water wheel that
  overhangs water) is a new rule on that one definition.
- An extractor may be placed with no matching field in its work area; it idles until one is in
  range. This allows planning ahead and later lets resources be created around a building.
- `TryPlaceBuilding(type, anchorX, anchorZ, facing)` validates via `CanPlace`, then adds the
  building and marks every footprint cell occupied. A building blocks player movement on its cells
  for as long as it stands.

### Production

- Each tick, every building steps.
- An extractor works one field at a time: its current field, or if that is gone, the nearest
  non-depleted matching field within its work area (distance from its footprint center to the
  field's square, at most the work radius). It pulls `ExtractorRate` into the stockpile and clears
  its current field when that field depletes. Several extractors may work the same field. With no
  field in range, it idles, and it picks up a field as soon as one is in range.
- A processor, when idle, consumes its recipe inputs from the stockpile if available and starts a
  cycle, then adds the outputs when the cycle completes.
- All buildings operate on the shared global stockpile. This is the abstract stand-in for logistics
  until vehicles and belts are added.

### Ship launch (win condition)

- When the stockpile holds at least `LaunchFuelCost` (500 Fuel) and `LaunchMetalCost` (100 Metal),
  `CanLaunchShip` is true. `TryLaunchShip` consumes that cost and increments `ShipsLaunched`. This
  is the current win that closes the initial loop.

### Rendering and interaction

- `BuildingViews` renders one colored box per building, sized to its rotated footprint (inset 0.1
  per side, 1 unit tall): Mine orange, Lumber Camp brown, Smelter red, Refinery blue.
- `BuildController` drives build mode: keys 1 Mine, 2 Lumber Camp, 3 Smelter, 4 Refinery; 0 or
  Escape cancels; R rotates the facing. The ghost is sized to the rotated footprint and placed at
  the anchor whose footprint center is nearest the mouse's ground point; it is green when placeable,
  red when not. Left-click issues the placement command.
- For an extractor, the ghost also shows its work area as a flat translucent disc of the work
  radius around the footprint center and highlights each matching field in range.
- The HUD shows the four resource totals, the launch progress and prompt, the build selection with
  its facing, and for an extractor, the count of fields in range.

## Related

- resources.md (stockpile, fields, range), simulation.md (commands, grid), presentation.md (views,
  HUD, build UI).
