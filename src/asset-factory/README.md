# Asset factory

Source and tooling for the game's generated 3D assets. Not a C# project, and not imported by
Godot (the Godot project is `src/Game`).

- `models/`: generated `.obj` and `.mtl` files. These are the source of truth.
- `previews/`: kept sample sheets (game camera angle, two more yaws, and a top-down view).
- `tools/`: the generator (`gen.py`), the Blender converter (`obj_to_glb.py`), the Blender
  preview renderer (`render_preview.py`, no window), and the Godot preview renderer (`preview/`,
  opens a window).

Shipped `.glb` files live in `src/Game/assets/models/`, not here.

How to generate, preview, convert, and ship an asset: see
[docs/asset-pipeline.md](../../docs/asset-pipeline.md).
