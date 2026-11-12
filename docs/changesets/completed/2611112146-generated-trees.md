# Generated Trees

- Status: Implemented
- Id: 2611112146-generated-trees
- Depends-on: none

## Summary

Replaces the three CC0 Kenney trees with generated low-poly trees in three families, one per biome
that holds Wood fields: tall conifers in Forest, rounded broadleaf trees in Grassland, and
drooping swamp trees in Swamp, three variants each. A Wood field draws the family of its biome.
After this, every world model is generated; the character is the one CC0 asset left.

## Motivation

The trees are the last imported world models, and they do not follow the art style's palette. A
family per biome also makes regions readable at a glance: a forest edge looks different from a
meadow or a swamp, which matters now that the world is generated and the player explores it.

## Design

### Families

| Biome | Family | Files | Look |
|---|---|---|---|
| Forest | Conifer | `tree_conifer_a..c` | a short brown trunk under three or four stacked, faceted cones narrowing to a point, tall and narrow like a spruce (the first version was squat and read as a bush); deep blue-green |
| Grassland | Broadleaf | `tree_broadleaf_a..c` | a trunk with a fork under two or three overlapping faceted blobs forming a round crown; brighter green |
| Swamp | Swamp | `tree_swamp_a..c` | a thick, slightly leaning trunk flared at the base, under a wide, flattened crown with a ragged lower edge that hangs down; olive and dark green |

- **Chunky low-poly:** bold silhouettes from a few large faceted shapes (cones and lumpy blobs like
  the ore lumps), flat palette colors, two or three shades per crown so the facets read. A tree
  stays a few hundred triangles at most, so dense forests stay cheap to draw.
- Crowns use mid-tone greens, with no near-black shade, so a crown's shaded side does not read as
  black under the game's dark ambient light (see art-style.md).
- The three variants of a family differ in proportions and crown arrangement, so a forest does not
  look stamped out; the game also turns each slot's model by a deterministic yaw, as today.
- Sizes are unchanged: a tree is scaled to the field's height (2.5 units for a field of 250 or
  less, rising to 3.5 at 500 or more) and fits its 1.2-unit slot. Each family's shape suits that:
  conifers are tall and narrow, broadleaf and swamp crowns wider and lower, so the slot cap shrinks
  a wide crown a little rather than letting it overlap.

### Choosing a family

- A Wood field draws the family of the biome at its map cell. A Wood field anywhere else (only
  possible through debug placement, such as a scenario putting Wood on Desert sand) draws broadleaf.
- `NodeModels` picks the family from the biome, so a slot's model depends on the type, the biome,
  and the slot as before.

### Assets

- New generators in `src/asset-factory/tools/resource_gen.py` for the three families, with new
  palette entries in `mesh.py` (bark and the crown greens) and in art-style.md.
- The nine `.glb` files and their `.import` files in `src/Game/assets/models/nature/`; the three
  Kenney `tree_a..c` files and their `.import` files are removed.
- Kept previews: one sheet per family (`tree_conifer`, `tree_broadleaf`, `tree_swamp`, each with its
  three variants together), and `arrangement_trees` with all nine in a row.

## Code changes

- `NodeModels`: the Wood spec becomes three family specs; `Slot` takes the field's biome and picks
  the family's paths.
- `ResourceFieldViews` / `FieldTileView`: `Sync` takes the sim, and each field's biome (read at its
  map cell when its tile's batch is built) goes to `NodeModels.Slot`.

## Blueprint updates

- **resources.md:** Wood fields draw a tree family by biome (conifer in Forest, broadleaf in
  Grassland and anywhere else, swamp trees in Swamp), all generated; the CC0 trees are gone.
- **art-style.md:** the new palette entries and the tree families; under Sources, the character is
  the only CC0 import.
- **presentation.md:** `NodeModels` picks a Wood slot's model by biome.
- Folder README `nature/`: the tree families, their files, and that every file there is generated.

## Verification

- `dotnet build game.slnx -c Debug`; `dotnet test src/Game.Integration.Tests` passes (scenarios
  with Wood fields keep their behavior; only the models change).
- Previews of each family and the arrangement sheet.
- Windowed screenshots of a generated world showing a forest, a grassland, and a swamp at the
  default and widest zoom, checking silhouettes, colors, and that no crown reads black.
- `perf-walk` uncapped, three runs: frame 5.9, 6.5, and 6.5 ms (median 6.5, against 6.6 before),
  within the noise. Draw calls rose from about 583 to 642 and objects from about 1,117 to 1,176:
  the same number of models, but three tree families mean more distinct models in a tile, and each
  model is its own multimesh.
- Screenshots of seed 1 show conifers in the forest of chunk (1, 0), a broadleaf grove in the
  grassland, and swamp trees on the mud beside the lake, with no crown reading black.
