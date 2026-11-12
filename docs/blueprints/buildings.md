# Buildings Blueprint

## Overview

Buildings are placed on the fine grid and run the production chain that turns raw resources into
fuel. Accumulating enough refined output lets the player launch a ship, which closes the on-planet
loop.

## Current state

### Building types and definitions

- `BuildingType` has five values: **Mine**, **Lumber Camp**, **Smelter**, **Refinery**, **Oil Well**.
- Each type has a `BuildingDef` (in `BuildingDefs`): a display name, a footprint in fine cells
  (width along X, depth along Z when facing North), and for an extractor, the deposit types it
  works (`Works`; a building is an extractor when the list is not empty) and its work radius.

  | Type        | Footprint | Works                                | Work radius |
  |-------------|-----------|--------------------------------------|-------------|
  | Mine        | 2 x 2     | Iron Ore, Copper Ore, Coal, Stone    | 6 units     |
  | Lumber Camp | 2 x 2     | Wood                                 | 6 units     |
  | Oil Well    | 2 x 2     | Crude Oil                            | 3 units     |
  | Smelter     | 3 x 3     |                                      |             |
  | Refinery    | 2 x 3     |                                      |             |

  The Oil Well's short radius means it sits beside its seep.

- A `Recipe` has inputs, outputs (each a `ResourceAmount`), and a cycle time. Recipes are defined
  centrally:
  - Extractors (Mine, Lumber Camp, Oil Well): no recipe; each pulls from a field at
    `ExtractorRate` (5 per second).
  - Smelter: 1 Iron Ore to 1 Iron, cycle 1 s.
  - Refinery: 1 Crude Oil to 1 Rocket Fuel, cycle 2 s.
  These are placeholders until the production chain toward a ship (Steel Mill, Water into the
  Refinery, ship parts; see docs/game-plan.md) is built.

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
  bounds, buildable (`IsBuildable`: Grass, Sand, Snow, or Rock with no blocking feature, so a
  river bank is buildable but its channel and a mountain are not; see terrain.md), unoccupied,
  and not covered by a resource field, and no unit is inside the
  footprint. Today the only unit is the player, whose cell is the fine cell their position floors
  to (`CellAt`), the same point test movement collision uses, so a building can go exactly where
  the player could not walk into. A building may be placed right next to the player. No building
  may sit on a resource. Placement rules belong to the definition, so a future exception (a water wheel that
  overhangs water) is a new rule on that one definition.
- An extractor may be placed with no matching field in its work area; it idles until one is in
  range. This allows planning ahead and later lets resources be created around a building.
- `TryPlaceBuilding(type, anchorX, anchorZ, facing)` validates via `CanPlace`, then adds the
  building and marks every footprint cell occupied. A building blocks player movement on its cells
  for as long as it stands.

### Production

- Each tick, every building steps.
- An extractor works one field at a time: its current field, or if that is gone, the nearest
  non-depleted field of any type it works within its work area (distance from its footprint center
  to the field's square, at most the work radius). It pulls `ExtractorRate` of that field's type
  into the stockpile and clears
  its current field when that field depletes. Several extractors may work the same field. With no
  field in range, it idles, and it picks up a field as soon as one is in range.
- A processor, when idle, consumes its recipe inputs from the stockpile if available and starts a
  cycle, then adds the outputs when the cycle completes.
- All buildings operate on the shared global stockpile. This is the abstract stand-in for logistics
  until vehicles and belts are added.

### Ship launch (win condition)

- When the stockpile holds at least `LaunchRocketFuelCost` (500 Rocket Fuel) and `LaunchIronCost`
  (100 Iron),
  `CanLaunchShip` is true. `TryLaunchShip` consumes that cost and increments `ShipsLaunched`. This
  is the current win that closes the initial loop.

### Rendering and interaction

- `BuildingViews` renders each building's model (`BuildingModels`, files in
  `src/Game/assets/models/buildings/`), placed at the footprint center and turned to its facing.
  Each model is authored facing North with its front toward -Z, fills its footprint minus a 0.1
  inset per side, and carries its type's accent color: Mine (a headframe over a shaft, ore cart at
  the front, orange), Lumber Camp (a plank shed, log pile and saw bench at the front, brown),
  Smelter (a stone furnace with a chimney and a glowing mouth at the front, red), Refinery (two
  tanks joined by pipes, a control hut at the front, blue), Oil Well (a pumpjack whose horsehead
  hangs over a wellhead at the front, with a crank, a counterweight, and a small tank, yellow and
  black). The front marks the facing, so a
  rotated building reads as rotated.
- `BuildController` drives build mode: keys 1 Mine, 2 Lumber Camp, 3 Smelter, 4 Refinery, 5 Oil Well; 0 or
  Escape cancels; R rotates the facing. The ghost is a translucent copy of the selected building's
  model, turned to the chosen facing, over a thin plate covering the exact footprint; it is placed
  at the anchor whose footprint center is nearest the mouse's ground point, and every surface of
  it is tinted green when placeable, red when not. Left-click issues the placement command.
- The pointer is the viewport mouse position unless `PointerOverride` holds a world ground point
  (set by scenarios); that point is projected to the screen each frame and then goes through the
  same screen-to-ground ray, so aiming stays fixed on the ground while the camera moves.
  `CurrentGhost()` reports the ghost (anchor, size, center, placeability, fields in range) for the
  current pointer, and `ResetBuildMode()` returns to no selection, facing North, and the real mouse
  (used by the `initMap` testing command).
- For an extractor, the ghost also shows its work area as a flat translucent disc of the work
  radius around the footprint center and highlights each field in range of a type it works.
- The HUD shows the resource totals, the launch progress and prompt, the build selection with
  its facing, and for an extractor, the count of fields in range.

## Related

- resources.md (stockpile, fields, range), simulation.md (commands, grid), presentation.md (views,
  HUD, build UI).
