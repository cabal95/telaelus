# Asset Pipeline

How 3D assets are generated, previewed, converted, and updated. This is a how-to, not a
blueprint: it describes the workflow, not the art style or how the game consumes assets (see
`src/Game/assets/models/*/README.md` for the in-game conventions: file names, scale targets,
slot sizes).

## Status

The pipeline is experimental. The generator, the Blender converter, and both preview renderers
(Blender and Godot) exist and work. Everything lives in `src/asset-factory/`:

```
src/asset-factory/
  README.md
  models/            generated .obj and .mtl (the source of truth, committed)
  previews/          kept sample sheets (final ones only)
  tools/
    gen.py           generators
    obj_to_glb.py    Blender conversion script
    render_preview.py  Blender preview renderer (no window)
    preview/         Godot preview renderer (render.gd + a minimal project.godot)
```

The `.glb` files are not stored here; the shipped copies live in `src/Game/assets/models/`.
Throwaway output (scratch renders, test scenarios, trial exports) goes in the git-ignored
`test-output/`.

## Overview

```
gen.py  ->  models/<name>.obj + .mtl  ->  preview render (PNG)  ->  review and adjust
                                      \->  Blender  ->  <name>.glb  ->  src/Game/assets/models/...
```

Assets are described as code (a Python script emits geometry), not edited by hand, so any asset
can be tweaked by changing a parameter and regenerating.

## Tools

Paths come from `tools.json` at the repo root (see `tools.template.json`). Never guess or search
for them.

| Tool | Key | Used for |
|------|-----|----------|
| Python 3 | on `PATH` | running the generator (standard library only) |
| Blender | `blender.editor` | converting OBJ to `.glb`, window-free preview renders |
| Godot | `godot.editor` | engine-accurate preview check (opens a window) |

## Source format

Generators write Wavefront `.obj` plus a sibling `.mtl`. Rules every generator follows:

- **Y is up.** The origin is the center of the model's base, resting on y=0.
- **Units are world units.** 1 map cell is 4 x 4 units; see the in-game README for the scale of
  each asset class (ore about 0.8 tall, trees about 2.5, buildings about 1, terrain tiles 4 x 4).
- **Counter-clockwise front faces** (the OBJ convention). Godot treats clockwise as front, so the
  renderer reverses winding; Blender and the glTF export handle it on import.
- **Flat colors only.** Color comes from `Kd` in the `.mtl`; there are no textures. Optional `Ke`
  adds emission (for glowing materials) and `d` adds transparency.
- **Flat shading.** Faces are written as separate triangles with no shared vertices, so every
  face shades flat. This is the low-poly look.
- **Deterministic.** Every generator seeds its own `random.Random`, so regenerating gives
  identical geometry. Change the seed to get a new variant.
- **Tileable assets** (terrain overlays) must keep their shared edge profile identical across
  all variants so any tile can sit next to any other. See `border_h` in the mountain generator.

## Generating

```bash
python src/asset-factory/tools/gen.py
```

This writes every model to `src/asset-factory/models/`. To add an asset, write a function
that returns a `Mesh`, call `mesh.write(name, materials)` from the `__main__` block, and keep
materials as a dict of `name: (Kd, Ke_or_None)`.

## Previewing

Two renderers produce the same 2 x 2 contact sheet: the game camera angle (yaw 45, pitch -48),
two other yaws, and a top-down view. All are orthographic over a grass-colored ground, lit like
the game (one shadowed directional light at (-50, -40, 0)).

### Blender renderer (default, no window)

```bash
<blender.editor> -b --python src/asset-factory/tools/render_preview.py -- <abs model.obj|model.glb> <abs out.png> [ortho_size]
```

Blender in background mode never opens a window, so this is the one to use while working
unattended. It takes about 10 seconds. It accepts an `.obj` or a `.glb`. As with the converter,
the Store launcher detaches and prints nothing, so wait for the PNG to appear. Its shadows are a
little crisper than the game's.

### Godot renderer (engine check, opens a window)

A Godot script run against a minimal project (`src/asset-factory/tools/preview/` holds
`project.godot`). It uses the engine that draws the real game, so use it for a final check.

```bash
<godot.editor> --path src/asset-factory/tools/preview --resolution 800x800 -s render.gd -- <abs model.obj|model.glb> <abs out.png> [ortho_size]
```

- **It always opens a window.** Rendering needs a GPU context, and `--headless` replaces the
  renderer with a dummy that returns no image, so it cannot produce previews. A headless display
  driver combined with a real rendering driver also returns no image, and `--no-window` does not
  stop Godot creating the window. The run is short (about a second for a preview).
- Godot prints a block of leaked-resource warnings at exit. They are harmless.

### Both renderers

- Paths after `--` must be absolute.
- `ortho_size` is optional. Without it the camera frames the model; pass a number to frame a
  larger arrangement (the tile range test uses 22; a single tile uses 9).
- Save a sheet worth keeping to `src/asset-factory/previews/<model name>.png`.
- Read the PNG to review the result, then adjust the generator and rerun. Expect several rounds.

For tileable assets, also check a multi-tile arrangement so seams and edges are seen in context,
not just one tile: write a throwaway script (in `test-output/`) that translates copies of the
tiles by 4 units into one OBJ, and render that with an `ortho_size` of about 22.

## Converting to .glb with Blender

Blender runs headless: it imports the OBJ and exports glTF.

```bash
<blender.editor> -b --python src/asset-factory/tools/obj_to_glb.py -- <abs in.obj> <abs out.glb>
```

`obj_to_glb.py` imports the OBJ with Y up, forces flat shading, converts the material colors
from sRGB to linear, and exports a `.glb` with Y up. The `.glb` is what ships in
`src/Game/assets/models/`, matching the existing nature models. Godot can also import `.obj`
directly, so the conversion is for consistency rather than a hard requirement.

Things to know:

- **Color space.** The `.mtl` colors are authored as sRGB values (that is what the Godot OBJ
  preview assumes). Blender and glTF treat them as linear, so the script converts them. Without
  that step the `.glb` comes out visibly paler than the preview.
- **Microsoft Store Blender.** The Store install lives under `WindowsApps`, and Windows refuses to
  run that executable directly (access denied). Point `blender.editor` at the Store launcher
  alias instead: `<user>/AppData/Local/Microsoft/WindowsApps/blender-launcher.exe`. It runs
  headless but detaches and prints nothing, so wait for the output file to appear instead of
  reading stdout. A standard (non-Store) install runs normally and blocks until done.
- **Checking the result.** Both preview renderers also load `.glb`; pass the `.glb` path as the model
  to confirm geometry, orientation, scale, and color survived the export.

## Updating an existing asset

1. Change the generator (parameters, seed, or logic) and rerun it.
2. Render the preview and review it against the previous result.
3. For anything tileable, render the arrangement test as well.
4. Convert to `.glb`, render the `.glb` once to confirm it matches, and copy it over the shipped file under `src/Game/assets/models/`. Keep the
   file name; the game loads by name.
5. Open the game (or run a scenario with screenshots, see `docs/blueprints/testing.md`) to
   confirm the asset reads correctly in place. Previews do not replace this.

## Shipping an asset to the game

- Follow the folder and naming rules in the relevant `README.md` under
  `src/Game/assets/models/` (for example `rock_a.glb`, `tree_a.glb`).
- Godot caches imported assets. Replacing a `.glb` is not enough for a plain game run, which keeps
  drawing the old cached model. Re-import first with
  `<godot.editor> --headless --path src/Game --import` (it exits on its own), or just open the
  editor once. When replacing an existing model the `.import` file is unchanged; for a new model
  Godot writes a sibling `.import` file, which is committed together with the model.
- Wiring a new asset class into the presentation code is a normal changeset (see
  `docs/development.md`); only the asset files themselves follow this document.

## Limits

- Procedural low-poly suits props, rocks, buildings, and terrain. Organic shapes come out chunky.
- OBJ has no rigging or animation. Rigged or animated assets need a different source (Blender
  authoring or a CC0 pack).
- Previews are static stills at fixed angles. They show silhouette, color, and scale, not
  animation or in-game readability at zoom.
