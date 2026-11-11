# Resources Blueprint

## Overview

Resources are the materials the factory loop produces and consumes. Raw resources come from
resource fields on the map (by hand or by extractor); refined resources come from buildings (see
buildings.md). A single global stockpile holds all amounts.

## Current state

### Resource types

- `ResourceType` has four values: **Ore**, **Wood** (raw, from fields), **Metal**, **Fuel**
  (refined, from buildings).

### Resource fields

- A `ResourceField` occupies exactly one map cell (all 16 of its fine cells). It has an `Id`, a
  `Type` (Ore or Wood), its map cell (`MapX`, `MapZ`), an `InitialAmount`, and a remaining
  `Amount`. It is `Depleted` when empty, `RemainingFraction` reports how full it is, and `MinX`,
  `MinZ`, and `Center` give its world-space square.
- `StageCount` is 5. `Stage` is `ceil(RemainingFraction * StageCount)`: 5 when full, 1 just before
  depletion, 0 when depleted. Stages are presentation only; the amount is continuous.
- The field owns its placement. The simulation keeps a list of fields (stable order for stepping
  and view sync) and an index from map cell to field; `FieldAt(x, z)` finds the field covering a
  fine cell with one lookup. Adding or removing a field updates both.
- The simulation spawns a fixed set of eight fields of 250 each: Ore at map cells `(10, 9)`,
  `(11, 10)`, `(9, 11)`, `(4, 12)`; Wood at `(3, 4)`, `(4, 3)`, `(2, 5)`, `(12, 3)`. That is 1000
  of each, enough for a ship launch.
- A field blocks movement and placement on all its fine cells until it depletes and is removed,
  which reopens them.

### Range to a field

- `DistanceToField(point, field)` is the distance from a point to the nearest point of the field's
  map-cell square (0 inside). `FieldsInRange(point, radius, type)` returns the non-depleted fields
  of a type within a radius by that measure.

### Stockpile and hand-harvesting

- The stockpile is a map from `ResourceType` to amount, exposed read-only.
- Each tick the simulation finds the nearest field within `HarvestRange` (1.5 units) of the player,
  by distance to the field's square, as the `HarvestTarget`. While the player holds the harvest
  intent, it pulls `HarvestRate` (10 per second) from that field into the stockpile; a depleted
  field is removed.

### Rendering

- `ResourceFieldViews` gives each field `StageCount` model slots at deterministic, jittered
  positions inside its map cell (a quincunx around the center, kept clear of the cell edge) and
  shows the first `Stage` slots, so a field loses one model per stage. Visuals are freed when the
  field is removed.
- `NodeModels` builds each slot's model from CC0 glTF models when present (Ore rocks, Wood trees),
  scaling to a target height (Ore ~0.8, Wood ~2.5 units) but capping the footprint to the slot size
  (1.2 units); it seats the model on the ground with a deterministic yaw and variant per slot. When
  a model file is absent it falls back to a colored box. Assets and conventions live in
  `src/assets/models/nature/README.md`.
- The HUD shows resource totals and an `[E] harvest` hint when a field is in range.

## Related

- buildings.md (refined resources, extractors), player.md (harvest input, field blocking),
  presentation.md (rendering, HUD).
