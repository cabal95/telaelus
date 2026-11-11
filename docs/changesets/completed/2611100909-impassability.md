# Impassability

- Status: Implemented
- Id: 2611100909-impassability
- Depends-on: none

## Summary

Block movement and building placement on water, mountain, resource nodes (until depleted), and
buildings.

## Motivation

Make the terrain and occupants matter to the gameplay rather than being decoration.

## Design

- `IsPassable(x, z)` is grass only and gates building-placement terrain. `IsWalkable(x, z)` is
  passable terrain with no resource node and no building, and gates player movement.
- Player movement checks walkability per axis, so the player slides along obstacles. A node blocks
  until it depletes and is removed (reopening the tile); a building blocks while it stands.
- The node footprint cap (changeset 2611100906-model-asset-pipeline) keeps a node's visual within
  its tile so the visual matches the per-tile collision.

## Code changes

- `Simulation`: `IsPassable`, `IsWalkable`, `IsWalkableAt`; `MovePlayer` checks `IsWalkableAt` per
  axis; `CanPlace` rejects non-passable tiles.

## Blueprint updates

- Updates `terrain.md` (passability), `player.md` (movement blocking), and `buildings.md`
  (placement terrain, building blocking).

## Verification

The player is blocked at water, mountain, nodes, and buildings, and slides along them; harvesting a
node reopens its tile; the build ghost turns red over blocked tiles. Clean build and headless run.
