# Trucks Drive Only Through Explored Land

- Status: Implemented
- Id: 2611241218-trucks-explored-paths

## Summary

A truck's path, like the player's, passes only through explored map cells. A collector a truck can
reach only through fog counts as unreachable until the fog lifts.

## Motivation

Trucks path through unexplored map cells today (buildings.md, Paths: "For the player a cell must
also be explored", which leaves trucks free of the rule). A truck vanishing into the fog and coming
out the other side shows the player a route they cannot see or walk, and gives away the shape of
land they have not explored.

## Design

- **Passable for a truck:** a cell is passable when it is walkable, its chunk exists, and its map
  cell is explored (`IsExplored`), the same test the player's walk uses. A truck's own Depot apron
  stays passable to it, as now (the Depot always stands on explored land).
- **Unreachable through fog:** a collector whose only route crosses fog gets no path, so the truck
  counts it unreachable and the panel reads "No truck can reach", as for a walled-off collector.
- **Fog lifting:** the unreachable set also clears whenever new map cells are explored (today it
  clears only when a building is placed or removed and when roads change), so a truck tries again
  once the player has walked the route.
- **A truck already driving** keeps its path; it re-searches only at the usual points (dispatch,
  leaving a collector or the HQ, its next cell turning impassable). Fog never returns, so a path
  found through explored land stays explored.

## Code changes

- `src/Telaelus.Sim/SimulationPaths.cs`: `IsPassable` uses the player's test (`PlayerCanWalk`) outside
  the truck's own Depot; the class comment states one rule for both.
- `src/Telaelus.Sim/SimulationFog.cs`: `MarkExplored` clears `_unreachable` when a map cell is newly
  explored.
- Tests (`TruckTests`, on `FoggedPeninsula`: a fog map whose Mine is ringed by water but for one
  unexplored map cell): the Mine is unreachable until `RevealAll`, then a truck sets off for it; a
  path to it is null until the player explores the way round, then never enters an unexplored map
  cell.

## Blueprint updates

- buildings.md, Paths: a cell is passable to a truck or the player when it is walkable, its chunk
  exists, and it is explored. Trucks: the unreachable set also clears when map cells are explored.

## Verification

- Sim tests pass.
- Integration tests pass (`trucks` runs on explored land and should not change).
