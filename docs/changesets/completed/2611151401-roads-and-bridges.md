# Roads and Bridges

- Status: Implemented
- Id: 2611151401-roads-and-bridges
- Depends-on: 2611151337-scenario-camera

## Summary

Paving for the ground and crossings over water. A **road**, two cells wide, is laid on walkable land
and makes trucks (and the player) faster on it, most on the slow ground (Mud, Sand, Snow) where it
costs more to lay. A **bridge** spans ShallowWater, or a straight river at right angles to its
flow, opening a crossing for trucks and the player where there was none. Both are laid by dragging
a line in build mode, priced per cell, and removable. Both are two fine cells wide, built from
2 x 2 tiles on an even grid, so a truck has room and the road reads beside 4 x 4 buildings.

## Motivation

The game plan's Infrastructure bullet (game-plan.md): "Roads go on any walkable land (costlier on
Sand, Snow, and Mud) and are the only paving; they speed up trucks. Bridges cross ShallowWater, and
river cells only where the river is Straight, at right angles to its flow. DeepWater, Lava, and
mountains block both." The resource tree (2611141757-resource-tree, section 7) kept roads (a truck
speed bonus) and bridges (a truck crossing over water) for after trucks. Trucks now carry the
mid-game (Advanced Material Transfer is research away), path round slow ground and water today,
and can find a collector unreachable across a river.

## Design

### Scope

- **In:** roads and bridges in the sim (cells, rules, costs, speed); their effect on walking and
  truck paths; laying and removing them by drag in build mode; their models (road pieces that join
  their neighbors, bridge decks and piers); snapshot fields and scenario commands.
- **Out:** road tiers (gravel now; asphalt or concrete later); bridges over DeepWater or Lava;
  trucks keeping to roads beyond what path cost already does; vehicles other than trucks;
  saving roads (they go into the save-and-load changeset with everything else).

### Roads

- **A road is a set of 2 x 2 road tiles** (`Simulation.Roads`, each tile named by its corner
  cell, both coordinates even, so tiles never overlap and always line up). A tile paves its four
  fine cells. It is not a building: it blocks nothing, occupies no building cell, and walking and
  trucks cross it.
- **Where:** a tile whose four fine cells are each walkable land (terrain walkable, no feature
  blocking it, no resource field, no building) and explored. Not on water, Lava, a mountain, or a
  river channel.
- **Cost per cell (placeholders):** 1 Stone on Grass and Rock; 2 Stone on Sand, Snow, and Mud (the
  plan's "costlier" ground). A tile costs its four cells (4 Stone on Grass, 8 on Mud), so a road
  costs 2 Stone per unit of length on Grass. A line's cost is the sum over the tiles it lays (tiles
  already paved are free and skipped).
- **Speed:** on a road the terrain speed is replaced by the **road speed**, 1.5, for trucks (8
  units/s becomes 12) and for the player (6 becomes 9). On Mud that is 2.5 times faster.
- **Truck paths** cost each step by the speed of the cell entered, as today, so A* prefers roads
  where they save time and nothing else changes in dispatch.
- **Buildings and roads:** a building cannot be placed over a road cell (the road must be removed
  first), so roads are never paved over by accident.
- **Removing** refunds half the cost, rounded up per line, as destroying a building does.

### Bridges

- **A bridge is a straight span** of 2 x 2 tiles on the same even grid as roads
  (`Simulation.Bridges`, the tiles and their span's direction). Its cells are **walkable** and
  count as road for speed.
- **Where:** a line of tiles along X or Z where every tile holds water and nothing that blocks:
  each of its cells is ShallowWater, or a cell of a map cell whose river is Straight and runs across
  the line (its channel or banks; a North-South river takes an East-West bridge and the other way
  round), and at least one cell of each tile is water or channel. Both ends must touch a tile of
  walkable land (just past the line's ends), so a bridge always lands. (The river channel is the
  middle two cells of its map cell, so an even-grid tile over it also covers a strip of bank.) DeepWater, Lava, and mountains block it, as does any
  bending river cell or another bridge or a Water Wheel in the way.
- **Length:** at most 12 cells (six tiles, three map cells of water) per span (placeholder).
- **Cost per cell (placeholders):** 3 Wood and 1 Stone, so 12 Wood and 4 Stone a tile. Removing
  refunds half, as roads.
- A bridge's cells are not buildable: no building stands on a bridge.

### Laying them (build mode)

- **The build menu** gains Road and Bridge cells (discovered from the start, under their own
  heading "Infrastructure" at the end of the menu; they are affordable with Stone, and Wood and
  Stone).
- **Dragging:** with Road or Bridge selected, pressing the left button on a cell and dragging
  lays out a line of tiles to the tile under the pointer (both snapped to the even grid): a road
  goes along X then Z (an L, the long leg first) through every tile between; a bridge only straight
  along X or Z. The ghost shows each tile (green where it can be laid, red where not) and a tooltip
  the line's length and cost ("Road: 7 tiles, 28 Stone"; short amounts marked). Releasing lays
  every valid tile the stockpile can pay for, in order from the start; a bridge lays only when the
  whole span is valid and affordable. Escape or a right click cancels the drag.
- **Removing:** with Road or Bridge selected, the same drag with the right button (or Shift with
  the left) removes the road or bridge tiles on the line, refunding half.
- R does nothing for roads and bridges (they have no facing).

### Presentation

- **Road pieces** join their neighbors: each road tile picks one of six 2 x 2 models by which of
  its four edges have road (or bridge) tiles beside them: an end, a straight, a corner, a tee, a
  crossing, or a lone patch, turned to fit. Gravel grey-brown with darker edges, flat on the ground (about
  0.03 high), so they read under buildings' shadows and beside fields.
- **Bridge pieces:** a 2 x 2 deck (planks across the span with rails on both sides) for each
  tile, a pier under every other tile, the deck at the bank's height; the two end tiles get a
  short ramp.
- The network overlay (N) and the build ghost draw above roads.

### Testing support

- Snapshot: `roads` (the count of road cells, and with `"list"` in a scenario's snapshot request
  the cells), `bridges` (each span's cells and direction), and each truck's current speed.
- Scenario commands `placeRoad` `{ from: [x, z], to: [x, z] }`, `removeRoad` (the same),
  `placeBridge` `{ from, to }`, `removeBridge`; the drag in build mode is also exercised through
  the existing `drag` command.

### Assets

- Road pieces: `road_end`, `road_straight`, `road_corner`, `road_tee`, `road_cross`, `road_patch`
  (each 2 x 2, authored with its open edges toward North and East as the convention for turning).
- Bridge pieces: `bridge_deck`, `bridge_ramp`, `bridge_pier`.
- Previews and README rows (a new `assets/models/infrastructure/` folder), a road-family overview
  sheet, and the palette entries (`gravel`, `gravel_edge`, `bridge_wood`, `bridge_rail`).

## Code changes

- `Game.Sim`: road and bridge state (`Roads`, `Bridges`), `CanLayRoad`, `TryLayRoad(from, to)`,
  `TryRemoveRoad`, `CanLayBridge`, `TryLayBridge`, `TryRemoveBridge`, costs and refunds; road
  speed in the player's movement and the trucks' driving; bridge cells walkable in `IsWalkable`
  and in paths; placement refusing road and bridge cells; debug operations.
- Presentation: a `RoadViews` node (pieces chosen and turned per tile, rebuilt where roads change);
  build mode's line drag, ghost, and tooltip; the build menu's Infrastructure cells.
- Testing: unit tests, a `roads` scenario, the commands above, snapshot fields, the schema.
- Assets: the nine pieces, previews, README, palette.

## Blueprint updates

- **terrain.md:** roads and bridges in the walk and build rules.
- **buildings.md:** placement refusing roads; build mode's line drag; the menu's Infrastructure
  cells.
- **player.md:** road speed.
- **presentation.md, ui-style.md:** road views, the line ghost and tooltip.
- **art-style.md:** the pieces and palette.
- **testing.md:** tests, commands, snapshot fields, scenario.

## Verification

- Unit tests: laying a road on each terrain at its cost, refused on water, Lava, a mountain, a river
  channel, a field, and a building; a line skipping paved cells and stopping when Stone runs out;
  a building refused over a road; the player at 9 units/s on a road over Mud; a truck's path
  choosing a road that saves time and its trip taking less time; a bridge laid over ShallowWater
  and across a straight river, refused along a river, over a bend, over DeepWater, too long, or
  without land at both ends; a truck reaching a collector across a river only once a bridge stands;
  removing refunding half.
- Scenario: `roads` drags a road across Mud between the HQ and a Mine, sees a truck take it, lays a
  bridge over a river to a second Mine and sees a truck cross it, then removes a stretch.
- Both test suites pass; windowed screenshots show roads joining and a bridge on its piers.

## Decisions

1. **Roads speed up the player too:** 1.5 for trucks and the player.
2. **Buildings are refused over roads;** remove the road first.
3. **Bridges as proposed:** up to 12 cells, 3 Wood and 1 Stone a cell.
4. **Drag shape:** an L for roads (long leg first), straight only for bridges.
5. **Two cells wide only,** for roads and bridges, from a look at placeholder roads of both widths
   with a truck and the player: at one cell a truck fills the road and it reads as a path; at two
   it reads as a road beside 4 x 4 buildings. Roads and bridges are 2 x 2 tiles on an even grid.

## Implementation notes

- **A new `aimDrag` scenario command** drives the line tools (press at one world point, release at
  another, through the real input path); the existing `drag` works in screen pixels, which a
  scenario cannot aim at the ground with.
- **`placeRoad` and `placeBridge` lay free,** as `placeBuilding` places; `removeRoad` and
  `removeBridge` refund as the game does.
- **The snapshot's `roads`** is the full tile list (no separate "list" request); a truck's current
  speed was not added (the trip test covers road speed through the path).
- **A* stays exact:** its estimate is now the octile distance at road speed, the fastest ground.
- **A single-tile bridge** is allowed where it lands on two opposite sides; it takes the side
  that does.
- **A bridge with the player or a truck on it** cannot be removed, so nothing is left standing on
  water.
- **The Infrastructure cells sit at the top of the build menu** (reviewed): first placed at the end,
  after fifty building cells, they needed a scroll and were easy to miss, so they moved above the
  buildings, which got their own "Buildings" heading. That pushed some building cells below the menu's fold, so `clickUi`
  and `hoverUi` now scroll a clipped control into view first and act a frame later.
- **The throwaway width sample** (placeholder strips with a truck and the player) informed
  decision 5 and was removed.
