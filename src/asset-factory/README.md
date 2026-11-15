# Asset factory

Source and tooling for the game's generated 3D assets. Not a C# project, and not imported by
Godot (the Godot project is `src/Telaelus`).

- `models/`: generated `.obj` and `.mtl` files. These are the source of truth.
- `previews/`: kept sample sheets (game camera angle, two more yaws, and a top-down view).
- `tools/`: the generator (`gen.py`), the `.glb` exporter (`export_glb.py`), the preview renderer
  (`preview.py`, no window), and the Godot preview renderer (`preview/`, opens a window).
- `requirements.txt` and `setup_env.py`: the tools' private Python environment. Run
  `python src/asset-factory/setup_env.py` once per machine (and after `requirements.txt` changes);
  it creates the git-ignored `.venv/`.

Shipped `.glb` files live in `src/Telaelus/assets/models/`, not here.

How to generate, preview, convert, and ship an asset: see
[docs/asset-pipeline.md](../../docs/asset-pipeline.md).
