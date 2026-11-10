# Nature models (CC0)

Drop CC0 low-poly glTF models here to upgrade resource nodes from placeholder boxes to real
models. Until then, nodes render as colored boxes automatically (no error, build stays green).

**Source:** [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) (CC0). Quaternius and Poly
Pizza also have CC0 nature models. CC0 means public domain: free to use, modify, and ship, with
no attribution required.

**Format:** glTF `.glb` (Godot 4 imports it automatically on the next editor open).

**Expected files** (any you add are used; missing ones are skipped, and variants are chosen
deterministically per node):

- Ore nodes (target height ~0.8 units): `rock_a.glb`, `rock_b.glb`, `rock_c.glb`
- Wood nodes (target height ~2.5 units): `tree_a.glb`, `tree_b.glb`, `tree_c.glb`

Each model is auto-scaled to its target height from its bounding box, seated on the ground, and
given a deterministic random yaw per node. The scale targets and file names live in
`src/Presentation/NodeModels.cs`.

**Scale reference:** 1 tile = 1 world unit (~2 m); player ~1.4 units; buildings ~1 unit; trees
~2.5 units; ore clusters ~0.8 units.
