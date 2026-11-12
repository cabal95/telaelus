# Nature models

The models resource fields show, loaded by `NodeModels` (`src/Game/Presentation/`). A field fills
one map cell (4 x 4 units) and shows five models, one per depletion stage; each model fills one
slot of about 1.2 x 1.2 units. The look follows `docs/blueprints/art-style.md`.

Every file is required: the game throws if one is missing.

| Deposit | Files | Target height | Source |
|---|---|---|---|
| Iron Ore | `iron_ore_a.glb` to `iron_ore_c.glb` | about 0.8 | generated (`resource_gen.py`) |
| Copper Ore | `copper_ore_a.glb` to `copper_ore_c.glb` | about 0.8 | generated |
| Coal | `coal_a.glb` to `coal_c.glb` | about 0.6 | generated |
| Stone | `stone_a.glb` to `stone_c.glb` | about 0.9 | generated |
| Crude Oil | `crude_oil_a.glb` to `crude_oil_c.glb` | flat (about 0.05), sized by its slot | generated |
| Wood | `tree_a.glb`, `tree_b.glb`, `tree_c.glb` | 2.5 to 3.5 (by field size) | [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) (CC0) |

Generated models come from `src/asset-factory/tools/resource_gen.py` (see `docs/asset-pipeline.md`).

Each model is scaled to its target height from its bounding box (shrunk if needed to fit its
1.2-unit slot), seated on the ground, and given a deterministic variant and yaw per slot. The scale
targets and file names live in `src/Game/Presentation/NodeModels.cs`.

CC0 means public domain: free to use, modify, and ship, with no attribution required.
