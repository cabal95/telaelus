# World Map Cells, Resource Fields, and Work Areas

- Status: Implemented
- Id: 2611110949-world-map-cells
- Depends-on: none

## Summary

Split the world into two grid levels: coarse **map cells** (4x4 fine cells) that carry terrain and
resource fields, and the **fine grid** that carries buildings, collision, and movement. Resources
become map-cell-sized fields that deplete in visible stages. Buildings get multi-cell footprints
and a facing. Extractors no longer sit on a resource; they work every matching field within a
**work area** shown during placement.

## Motivation

- One grid doing terrain, resources, and buildings forces a bad compromise: fine cells make
  resources too small to read, and coarse cells make factory layout chunky.
- Map-cell resource fields have room for staged depletion (five trees, then four, and so on),
  which reads far better than a shrinking model.
- Sparse base tiles plus overlays become practical at map-cell size.
- A work area (in the style of Frostpunk's placement overlay) matches how manual harvesting works
  (stand within range of a resource) and frees extractors from having to sit on a resource.

## Design

### Two grid levels

- **Fine cell**: 1 x 1 world unit. Buildings, collision, movement, and placement live here.
- **Map cell**: 4 x 4 fine cells (`FineCellsPerMapCell = 4`). Terrain and resource fields live
  here, and procedural generation (later) works at this level.
- The **fixed test map** is `MapWidth` x `MapHeight` = **16 x 16** map cells, so its fine grid is
  **64 x 64**. This bound belongs to the test map only. Procedural generation (a later changeset)
  will generate map cells on demand, likely in chunks, for a world that is effectively unbounded;
  nothing in the cell model assumes a fixed size.
- Coordinates are signed integers. Fine cell `(x, z)` spans `[x, x+1) x [z, z+1)` with center
  `(x + 0.5, 0, z + 0.5)`. The map cell containing fine cell `(x, z)` is
  `(floor(x / 4), floor(z / 4))`, using floor division so negative coordinates map correctly
  (C# integer division truncates toward zero, which would put fine cells -3 to 3 all in map
  cell 0).
- The fixed test map occupies fine cells `[0, 63]` on each axis, with the world origin at its
  corner. Its terrain is stored in a fixed array; that storage is a test-map detail that
  procedural generation replaces. This also fixes today's inclusive `[-16, 16]` bounds (33 cells,
  not 32).
- The player spawns at the test map's center `(32, 0, 32)`, which the fixed layout keeps clear.

### Terrain

- One `TerrainType` per map cell (Grass, Water, Mountain, unchanged). `TerrainAt(x, z)` for a fine
  cell returns its map cell's terrain. Out-of-bounds reads return Grass, as today.
- The layout stays a **fixed test layout**, re-expressed in map cells: a water pond covering map
  cells x in `[9, 11]`, z in `[5, 6]`, and mountains at `(4, 9)`, `(4, 10)`, `(5, 9)`. Procedural
  generation is a later changeset.

### Resource fields (replace resource nodes)

- A `ResourceField` occupies exactly one map cell. It has an `Id`, a `Type` (Ore or Wood), its map
  cell, `InitialAmount`, and `Amount`. The field owns its placement; map cells do not hold fields.
- Storage: the simulation keeps a list of fields (stable iteration order for stepping and view
  sync) plus an index from map cell to field, so "which field covers this fine cell" is one
  lookup instead of a scan. Both update together when a field is added or removed. Fields are
  sparse (most cells have none), so per-cell storage would mostly hold nothing, and the index
  shape can later map several cells to one field if large deposits span cells.
- `StageCount = 5`. The visible stage is `ceil(RemainingFraction * StageCount)`: 5 models at full,
  1 model just before depletion, 0 when depleted. Stages are presentation only; the sim tracks a
  continuous amount.
- A field blocks movement and placement on all 16 of its fine cells until it depletes and is
  removed. (Unblocking cells as individual trees fall is a possible later refinement.)
- Fields start at **250** each. Fixed placements, four of each type:
  - Ore: map cells `(10, 9)`, `(11, 10)`, `(9, 11)`, `(4, 12)`.
  - Wood: map cells `(3, 4)`, `(4, 3)`, `(2, 5)`, `(12, 3)`.
  - That is 1000 Ore and 1000 Wood, enough for a launch (600 Ore for 600 Metal, 500 Wood). Today's
    eight nodes hold 400 Ore, short of the 600 a launch needs, so this also fixes that shortfall.

### Range to a field

- "Distance to a field" is the distance from a point to the nearest point of the field's map-cell
  square (zero when inside). Center-to-center distance would not work for a 4 x 4 field.
- **Hand-harvest:** `HarvestTarget` is the nearest field within `HarvestRange` (1.5 units) of the
  player by that measure. `HarvestRate` stays 10 per second.

### Buildings: footprint and facing

- Each building type has a definition: footprint width and depth in fine cells, and for an
  extractor, the resource it extracts and its work radius.

  | Type        | Footprint | Extracts | Work radius |
  |-------------|-----------|----------|-------------|
  | Mine        | 2 x 2     | Ore      | 6 units     |
  | Lumber Camp | 2 x 2     | Wood     | 6 units     |
  | Smelter     | 3 x 3     |          |             |
  | Refinery    | 2 x 3     |          |             |

- The generic Extractor splits into **Mine** (Ore) and **Lumber Camp** (Wood), since a work area
  needs to know which resource it is looking for. `ExtractorRate` stays 5 per second.
- A building has an **anchor** (its minimum-corner fine cell) and a **facing** (North, East,
  South, West). East and West swap width and depth, so a 2 x 3 Refinery facing East is 3 x 2.
  Facing is stored now so belts can use input and output sides later.
- The anchor (integer fine cell) and facing are the building's authoritative placement state;
  occupancy, rotation, and future belt connections are all computed from them. `Position` is the
  footprint center as a world point, derived from anchor, facing, and footprint, not stored
  separately. It exists for the float-space work: work-area distances and rendering.

### Placement

- `CanPlace(type, anchorX, anchorZ, facing)` requires every footprint cell to be in bounds, Grass,
  unoccupied, and not covered by a resource field. Extractors follow the same rule: no building
  may sit on a resource.
- An extractor may be placed with **no matching field in its work area**. It idles until one is
  in range, which allows planning ahead, and later lets resources be created around an existing
  building (for example, wheat fields planted around a farm). The build overlay shows how many
  fields are in range, so placing one where it will idle is a visible choice.
- Placement rules belong to the building definition, so a future exception (a water wheel that
  must overhang water) is a new rule on that one definition. Its specifics are out of scope here.

### Extractor work area

- A field is in an extractor's work area when its distance from the extractor's footprint center
  is at most the work radius.
- Each tick an extractor works one field: its current field, or if that is gone, the nearest
  non-depleted matching field in its work area. It pulls `ExtractorRate` into the stockpile.
  Several extractors may work the same field. When no field remains in range, it idles.

### Presentation

- **Terrain:** one placeholder tile per map cell (a 4 x 4 colored quad, mountain cone scaled to
  match), with shoreline strips on land-facing map-cell edges. Overlays and real tile models are
  separate changesets.
- **Fields:** each field has `StageCount` model slots at deterministic, jittered positions inside
  its cell, kept clear of the cell edge. Each slot uses `NodeModels` with the footprint cap set to
  the slot size (about 1.2 units) rather than one cell. The view shows the first
  `stage` slots; the shrink-by-fraction behavior goes away.
- **Buildings:** a colored box sized to the rotated footprint (Mine orange, Lumber Camp brown,
  Smelter red, Refinery blue).
- **Build mode:** keys 1 Mine, 2 Lumber Camp, 3 Smelter, 4 Refinery; 0 or Escape cancels; **R**
  rotates. The ghost is sized to the rotated footprint and centered on the mouse cell. For an
  extractor, the ghost also shows its work area as a flat translucent disc of the work radius
  around the footprint center, and highlights the matching fields in range. The HUD shows the
  selection, its facing, and for an extractor, "fields in range: N".
- **Camera:** unchanged settings; it follows the player across the larger map.

## Code changes

- `Simulation`:
  - Grid: `FineCellsPerMapCell`, the test map's `MapWidth` and `MapHeight`, derived fine
    `GridWidth` and `GridHeight`; `InBounds` over the test map's `[0, 63]`, `CellCenter`
    (replaces `TileCenter`), and floor-based fine-to-map-cell and world-to-cell helpers.
  - Terrain: per-map-cell array; `TerrainAt(x, z)` maps fine to map cell; `GenerateTerrain`
    re-expressed in map cells.
  - Fields: `_fields` list and a `_fieldsByMapCell` dictionary index, `Fields` read-only,
    `FieldAt(x, z)` (field covering a fine cell, via the index), `RemoveField` (updates both),
    `DistanceToField(point, field)`, `FieldsInRange(point, radius, type)`.
  - `IsWalkable` uses `FieldAt`; `UpdateHarvestTarget` uses `DistanceToField`.
  - Placement: `CanPlace(type, anchorX, anchorZ, facing)`, `TryPlaceBuilding(...)` with the same
    parameters; occupies every footprint cell.
  - `StepExtractor` selects and works fields per the work-area rule.
- New `Sim/ResourceField.cs` (replaces `ResourceNode.cs`), with `Stage`.
- New `Sim/Facing.cs` (enum) and `Sim/BuildingDefs.cs` (footprint, extracted resource, work radius,
  and a `Footprint(facing)` helper).
- `BuildingType`: `Mine`, `LumberCamp`, `Smelter`, `Refinery`.
- `Building`: anchor, facing, footprint size, center `Position`, `CurrentField` (replaces
  `Source`).
- `Presentation`:
  - `TerrainView` / `TerrainTiles`: one tile per map cell, sized to the map cell.
  - `ResourceNodeViews` becomes `ResourceFieldViews`: staged slots instead of scaling.
  - `NodeModels`: footprint cap becomes a parameter.
  - `BuildingViews`: footprint-sized boxes.
  - `BuildController`: new keys, R to rotate, mouse cell to anchor, footprint ghost, work-area disc
    and field highlights.
  - `Hud`: new building names, facing, fields-in-range count.
  - `GameRoot`: syncs `ResourceFieldViews` and passes facing and fields-in-range to the HUD. (The
    player spawn at map center is set in the `Simulation` constructor.)
- Asset READMEs: `src/assets/models/terrain/README.md` (tiles are 4 x 4 units per map cell) and
  `src/assets/models/nature/README.md` (models fill stage slots, not a whole cell).

## Blueprint updates

- `simulation.md`: the grid section describes the two levels, zero-based coordinates, map size,
  and that terrain and fields live on map cells while buildings and movement live on fine cells.
- `terrain.md`: terrain per map cell, the re-expressed layout, rendering one tile per map cell.
- `resources.md`: resource fields replace nodes (map-cell size, 250 each, the eight placements,
  stages, blocking), distance-to-field, and the hand-harvest rule.
- `buildings.md`: the four types and definitions table, anchor and facing, placement rules
  (including no building on a resource), the work area and extractor behavior, and the build-mode
  controls and overlay.
- `player.md`: spawn at map center; movement blocking by fields.
- `presentation.md`: terrain per map cell, staged field views, footprint boxes, build ghost and
  work-area overlay, HUD additions.

## Verification

- `dotnet build src/Game.csproj -c Debug` is clean, and the headless run has empty stderr.
- In game:
  - Terrain renders as 4 x 4 tiles; the player spawns in the open at the map center.
  - Fields show five models that drop one at a time as they are harvested, and vanish (reopening
    all 16 cells) when depleted.
  - Hand-harvest works when standing beside a field's edge, not only near its center.
  - Each building's ghost matches its footprint; R rotates the Refinery between 2 x 3 and 3 x 2.
  - No building can be placed on a field, water, or mountain.
  - An extractor ghost shows its work-area disc and highlights fields in range, and the HUD count
    matches them. With no field in range it is still placeable, and the placed extractor idles.
  - A placed Mine drains the nearest Ore field in range, then moves to the next one.
  - A full run reaches a ship launch with the starting fields.
