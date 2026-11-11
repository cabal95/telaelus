# Resources Blueprint

## Overview

Resources are the materials the factory loop produces and consumes. Raw resources come from nodes
on the map (by hand or by extractor); refined resources come from buildings (see buildings.md). A
single global stockpile holds all amounts.

## Current state

### Resource types

- `ResourceType` has four values: **Ore**, **Wood** (raw, from nodes), **Metal**, **Fuel**
  (refined, from buildings).

### Resource nodes

- A `ResourceNode` has an `Id`, a `Type` (Ore or Wood), a world `Position` on a tile, an
  `InitialAmount`, and a remaining `Amount`. It is `Depleted` when empty, and `RemainingFraction`
  reports how full it is.
- The simulation spawns a fixed set of eight nodes (Ore and Wood clusters) at integer tile
  coordinates and exposes them read-only.
- A node blocks player movement (its tile is not walkable) until it depletes and is removed, which
  reopens the tile.

### Stockpile and hand-harvesting

- The stockpile is a map from `ResourceType` to amount, exposed read-only.
- Each tick the simulation finds the nearest node within `HarvestRange` (1.5 units) of the player
  as the `HarvestTarget`. While the player holds the harvest intent, it pulls `HarvestRate`
  (10 per second) from that node into the stockpile; a depleted node is removed.

### Rendering

- `ResourceNodeViews` mirrors nodes to visuals and shrinks each by its remaining fraction, freeing
  the visual when the node is gone.
- `NodeModels` builds each visual from CC0 glTF models when present (Ore rocks, Wood trees),
  scaling to a target height (Ore ~0.8, Wood ~2.5 units) but capping the footprint to ~0.85 of a
  cell so the model stays within its tile and matches the per-tile collision; it seats the model on
  the ground with a deterministic per-node yaw. When a model file is absent it falls back to a
  colored box at the target height. Assets and conventions live in
  `src/assets/models/nature/README.md`.
- The HUD shows resource totals and an `[E] harvest` hint when a node is in range.

## Related

- buildings.md (refined resources, extractors), player.md (harvest input, node blocking),
  presentation.md (rendering, HUD).
