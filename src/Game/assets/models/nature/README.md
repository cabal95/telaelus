# Nature models (CC0)

Drop CC0 low-poly glTF models here to upgrade resource fields from placeholder boxes to real
models. Until then, fields render as colored boxes automatically (no error, build stays green).
A field fills one map cell (4 x 4 units) and shows five models, one per depletion stage; each
model fills one slot of about 1.2 x 1.2 units.

**Source:** [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) (CC0). Quaternius and Poly
Pizza also have CC0 nature models. CC0 means public domain: free to use, modify, and ship, with
no attribution required.

**Format:** glTF `.glb` (Godot 4 imports it automatically on the next editor open).

**Expected files** (any you add are used; missing ones are skipped, and variants are chosen
deterministically per slot):

- Ore fields (target height ~0.8 units): `rock_a.glb`, `rock_b.glb`, `rock_c.glb`
- Wood fields (target height ~2.5 units): `tree_a.glb`, `tree_b.glb`, `tree_c.glb`

Each model is auto-scaled to its target height from its bounding box (shrunk if needed to fit
its 1.2-unit slot), seated on the ground, and given a deterministic random yaw per slot. The scale targets and file names live in
`src/Game/Presentation/NodeModels.cs`.

**Scale reference:** 1 fine cell = 1 world unit; 1 map cell = 4 units; player ~1.4 units; buildings ~1 unit; trees
~2.5 units; ore clusters ~0.8 units.
