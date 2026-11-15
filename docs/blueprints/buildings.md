# Buildings Blueprint

## Overview

Buildings are placed on the fine grid and run the production chain that turns raw resources into
fuel. Accumulating enough refined output lets the player launch a ship, which closes the on-planet
loop.

## Current state

### Building types and definitions

- `BuildingType` has eleven values: **Mine**, **Lumber Camp**, **Smelter**, **Refinery**, **Oil
  Well**, the **HQ**, and the storage buildings **Silo**, **Stockyard**, **Tank Farm**,
  **Warehouse**, and **Vault**.
- Each type has a `BuildingDef` (in `BuildingDefs`): a display name, a footprint in fine cells
  (width along X, depth along Z when facing North), a cost, a speed, and for an extractor, the
  deposit types it works (`Works`; a building is an extractor when the list is not empty) and its
  work radius. It also has its `StorageRole` (None, Hq, Silo, ClassStore with the class it
  `StoresClass`, or Warehouse), the `CargoPool` it adds, whether it is `Permanent` (cannot be
  switched off or destroyed), and whether it is `Buildable` (placed by the player; the world places
  the HQ).

  | Type        | Footprint | Cost                       | Works                             | Work radius |
  |-------------|-----------|----------------------------|-----------------------------------|-------------|
  | Lumber Camp | 2 x 2     | 15 Wood                    | Wood                              | 6 units     |
  | Mine        | 2 x 2     | 25 Wood, 15 Stone          | Iron Ore, Copper Ore, Coal, Stone | 6 units     |
  | Smelter     | 3 x 3     | 40 Stone, 15 Wood          |                                   |             |
  | Oil Well    | 2 x 2     | 25 Iron, 20 Stone, 10 Wood | Crude Oil                         | 3 units     |
  | Refinery    | 2 x 3     | 40 Iron, 30 Stone, 20 Wood |                                   |             |

  The storage buildings (what each adds is in resources.md):

  | Type | Footprint | Cost | Adds |
  |---|---|---|---|
  | HQ | 6 x 10 | free, placed by the world | every per-type resource's HQ base, and 150 Cargo |
  | Silo | 2 x 2 | 30 Wood | its chosen Bulk resource's Silo step |
  | Stockyard | 3 x 3 | 40 Wood, 20 Iron | every Bulk resource's class step |
  | Tank Farm | 3 x 3 | 30 Iron, 20 Stone | every Fluid resource's class step |
  | Warehouse | 3 x 3 | 40 Wood, 30 Stone | 200 Cargo |
  | Vault | 3 x 3 | 40 Steel, 40 Stone | every Secure resource's class step |

  The Oil Well's short radius means it sits beside its seep.
- **Costs** lean lightly on the real world: a building costs what most of it is made of (a Lumber
  Camp is wood, a Smelter is a stone furnace), not every material it would really need. The
  intended path is a Lumber Camp from hand-gathered Wood, a Mine from hand-gathered Stone, a
  Smelter from the Mine's Stone, then the oil chain from the Smelter's Iron. Copper Ore and Coal
  are in no cost yet.
- **Speed**: every building is base level today, `BuildingDef.BaseSpeed` 0.75, so a base extractor
  is slower than the player by hand. Its benefit is that it works unattended; two together
  out-gather the player. Higher levels would raise the speed.

- A `Recipe` has inputs, outputs (each a `ResourceAmount`), and a cycle time. Recipes are defined
  centrally:
  - Extractors (Mine, Lumber Camp, Oil Well): no recipe; each takes one unit at a time at the
    deposit's gather time divided by its speed (at base speed: Wood 4 s, Stone and Coal 6 s, Iron
    Ore and Copper Ore 8 s, Crude Oil 4 s).
  - Smelter: 1 Iron Ore to 1 Iron, cycle 16 s.
  - Refinery: 1 Crude Oil to 1 Rocket Fuel, cycle 8 s.
  Processors run at half the pace of one base extractor, so one Mine keeps two Smelters busy and
  one Oil Well two Refineries.
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
  bounds, explored (fog of war, simulation.md), buildable (`IsBuildable`: Grass, Sand, Snow, or Rock with no blocking feature, so a
  river bank is buildable but its channel and a mountain are not; see terrain.md), unoccupied,
  and not covered by a resource field, and no unit is inside the
  footprint. Today the only unit is the player, whose cell is the fine cell their position floors
  to (`CellAt`), the same point test movement collision uses, so a building can go exactly where
  the player could not walk into. A building may be placed right next to the player. No building
  may sit on a resource. Placement rules belong to the definition, so a future exception (a water wheel that
  overhangs water) is a new rule on that one definition.
- An extractor may be placed with no matching field in its work area; it idles until one is in
  range. This allows planning ahead and later lets resources be created around a building.
- **Unlocking**: a building is unlocked (`IsUnlocked`) once the stockpile has held its whole cost
  at once (checked whenever an amount rises); it stays unlocked when the stock later runs short,
  so it can still be selected while unaffordable. Only a buildable type unlocks (never the HQ).
  At the start nothing is unlocked. `CanAfford` is whether the stockpile holds the whole cost now.
- `TryPlaceBuilding(type, anchorX, anchorZ, facing)` is placement as the player does it: the spot
  must pass `CanPlace` and the building must be unlocked and affordable. It deducts the cost, adds
  the building, and marks every footprint cell occupied; a refused placement changes nothing.
  There is no demolish yet, so no refund. `Debug.PlaceBuilding` places by `CanPlace` alone, free
  and whether or not unlocked. A building blocks player movement on its cells
  for as long as it stands.

### The HQ and the Silo

- **The HQ** is placed when the world is made (`ResetWorld`), facing South, so its front faces a
  player south of it: on a generated world at the generator's `HqAnchor`, centered on the spawn
  point's X and ending 3 fine cells north of it, inside the spawn's clear Grassland with no field
  under it (see world-generation.md); on the test map at `TestMapHqAnchor` (23, 16), clear of the
  tests' clear spot; on the empty map at `EmptyMapHqAnchor` (56, 2), its north-east corner, clear
  of the center where tests and scenarios build. It cannot be switched off or destroyed, is never
  unlocked, and is not in the build menu.
- **A Silo** holds one Bulk resource (`Building.SiloResource`, Wood when placed).
  `SetSiloResource(id, type)` changes it, moving its step to the new resource at once; it refuses
  anything but a Silo and a Bulk resource.

### Production

- Each tick, every building steps.
- An extractor works one field at a time: its current field, or if that is gone, the nearest
  non-depleted field of any type it works within its work area (distance from its footprint center
  to the field's square, at most the work radius). It works toward one unit of that field's type
  (`Progress`, in seconds, reset when it takes up a new field), adds the unit to the stockpile at
  the gather time over its speed, and clears its current field when that field depletes. When
  storage has no room for the unit, the unit waits at the field: progress holds at the gather time
  and the unit is taken as soon as there is room. Several extractors may work the same field. With no
  field in range, it idles, and it picks up a field as soon as one is in range.
- A processor, when idle, starts a cycle only when its recipe inputs are in the stockpile and its
  outputs would fit (Cargo outputs together in the pool), consuming the inputs. When the cycle
  completes it adds the outputs; if room ran out during the cycle, the finished output waits in
  the building (progress holds at the cycle time) until it fits.
- Storage buildings do no work each tick; they only raise limits while they stand.
- All buildings operate on the shared global stockpile. This is the abstract stand-in for logistics
  until vehicles and belts are added.

### Switching off, status, and destroying

- `Building.Enabled` is true when placed. `SetBuildingEnabled(id, enabled)` switches a building.
  A switched-off building does nothing, and switching off drops the work in hand: progress goes
  back to 0, and a processor's running cycle ends with its inputs returned to the stockpile
  (fuel, once buildings burn it, will not be). Switched back on, it starts a whole unit or cycle
  over.
- `BuildingStatusOf(building)` gives a `BuildingStatus`: a state (`Working`, `Idle`, `Off`) and,
  when not working, a reason. Off reads "Switched off". An extractor works while it has a field
  or one in range to take up and room for its unit; with no field it is idle with "No fields in
  range", and with no room "Storage full: Crude Oil". A processor whose output does not fit, before
  a cycle or with a finished one waiting, is idle "Storage full: Iron"; otherwise it works during a
  cycle, else is idle "Waiting for" its missing inputs ("Waiting for Crude Oil").
- A permanent building (the HQ) cannot be switched off or destroyed: `SetBuildingEnabled` and
  `TryDestroyBuilding` return false for it.
- `TryDestroyBuilding(id)` removes a building, frees its cells, and adds its refund
  (`DestroyRefund`) to the stockpile, even past a storage limit: half of each cost amount, rounded up (a Mine's 25 Wood and
  15 Stone return 13 Wood and 8 Stone), plus the inputs of a processor's running cycle in full.
  Fuel, once buildings burn it, will not be refunded. The refund follows the type, so a building
  placed for free through the debug tools refunds the same.
- `BuildingAt(x, z)` finds the building covering a fine cell, and `BuildingById(id)` finds one by
  id.

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
  black), HQ (a hall with a teal band and an entrance at the front, a landing pad before it, and a
  comms tower with a dish at the back, teal), Silo (a tall round silo with a conical cap and a
  ladder at the front, green), Stockyard (a fenced yard of stone, ore, and log heaps, open at the
  front, purple rails), Tank Farm (three banded tanks joined by pipes, a valve box at the front,
  cyan), Warehouse (a gabled shed with a roller door at the front and crates beside it, pink roof),
  Vault (a squat armored bunker with a round door at the front and gold hazard bands). The front
  marks the facing, so a
  rotated building reads as rotated.
- `BuildController` drives build mode: a building is selected from the build menu (B; see
  presentation.md), and only an unlocked one can be (`Select`); Escape cancels; R rotates the
  facing. The ghost is a translucent copy of the selected building's
  model, turned to the chosen facing, over a thin plate covering the exact footprint; it is placed
  at the anchor whose footprint center is nearest the mouse's ground point, and every surface of
  it is tinted green when placeable and affordable, red when not. Left-click issues the placement command.
- The pointer is the viewport mouse position unless `PointerOverride` holds a world ground point
  (set by scenarios); that point is projected to the screen each frame and then goes through the
  same screen-to-ground ray, so aiming stays fixed on the ground while the camera moves.
  `CurrentGhost()` reports the ghost (anchor, size, center, placeability, fields in range) for the
  current pointer, and `ResetBuildMode()` returns to no selection, facing North, and the real mouse
  (used by the `initMap` testing command).
- Outside build mode, the placed building under the pointer gets a thin accent outline (a
  frame of four bars 0.14 units wide just outside its footprint, unless the pointer is over UI), and a left click on it opens its detail
  panel (see presentation.md); a left click on the world that hits neither a building nor a
  resource field (which opens its own panel, see resources.md) closes a panel. The building the detail panel shows (`Focused`) keeps its outline until the panel
  closes, so the player can tell which one they are looking at.
- For an extractor, the ghost also shows its work area as a flat translucent disc of the work
  radius around the footprint center and highlights each field in range of a type it works.
- The build menu lists every building (discovered first, then undiscovered silhouettes), and
  while placing, a tooltip beside the pointer shows the building's name, its cost, and for an
  extractor, the count of fields in range. A cost amount the stockpile is short of shows what it
  holds, as "15 Stone (have 3)". See presentation.md and ui-style.md.

## Related

- resources.md (stockpile, fields, range), simulation.md (commands, grid), presentation.md (views,
  screen UI, build UI).
