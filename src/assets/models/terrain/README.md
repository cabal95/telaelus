# Terrain tiles (CC0)

Terrain currently renders as flat colored placeholder tiles (grass green, water blue, mountain
cone), with green shoreline strips previewing the grass-to-water transitions. Dropping CC0 tile
models here and wiring them into `TerrainTiles.cs` replaces the placeholders; that wiring is the
next step once the files are in.

## Tiles we will want (Kenney Nature Kit or similar, CC0)

- **Grass:** a base ground tile plus a few grass-overlay variations (chosen per cell).
- **Water:** an open water tile.
- **Shoreline transitions:** a water tile with grass on one edge, and a water tile with grass on
  two adjacent edges (corner). The renderer counts a water cell's land neighbors and rotates the
  right tile to face them.
- **Mountain:** a mountain object placed on a grass base tile.

## Conventions

- One tile per **map cell**: 4 x 4 world units (4 x 4 fine cells), centered on the map cell.
- "Land" for shoreline purposes means any non-water cell (grass or mountain).
- The fixed test layout (a pond and a mountain cluster) lives in `Simulation.GenerateTerrain`.

## Scale reference

1 tile = 1 map cell = 4 x 4 world units. Author tiles at 4 x 4 Blender units, or at 1 x 1 and
scale by 4 in code.
