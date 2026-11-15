# Asset Pipeline

How 3D assets are generated, previewed, converted, and updated. This is a how-to, not a
blueprint: it describes the workflow, not the art style or how the game consumes assets (see
`src/Telaelus/assets/models/*/README.md` for the in-game conventions: file names, scale targets,
slot sizes).

## Status

The generator, the `.glb` exporter, and both preview renderers (Python and Godot) exist and work.
Everything lives in `src/asset-factory/`:

```
src/asset-factory/
  README.md
  requirements.txt   pinned Python packages for the tools
  setup_env.py       creates the private Python environment in .venv/
  models/            generated .obj and .mtl (the source of truth, committed)
  previews/          kept sample sheets (final ones only)
  tools/
    gen.py           runs every generator; mountains
    mesh.py          shared building blocks: Mesh writer, PALETTE, solids
    terrain_gen.py   ground tiles, transition pieces, river pieces
    building_gen.py  building models
    unit_gen.py      unit models (the truck), exported to assets/models/units/
    resource_gen.py  resource field models (the deposits), and site models (worked-out markers, the motherlode base)
    item_gen.py      resource item models (the UI's resource icons) and icon models, exported to assets/models/icons/
    town_layout.py   the town scene's starting layout (src/Telaelus/scenes/Town.tscn); plain Python, run once
    arrange.py       composes test maps from terrain models (for previews)
    export_glb.py    OBJ to .glb exporter
    preview.py       preview renderer (no window)
    preview/         Godot preview renderer (render.gd + a minimal project.godot)
```

The `.glb` files are not stored here; the shipped copies live in `src/Telaelus/assets/models/`.
Throwaway output (scratch renders, test scenarios, trial exports) goes in the git-ignored
`test-output/`.

## Overview

```
gen.py  ->  models/<name>.obj + .mtl  ->  preview.py (PNG)  ->  review and adjust
                                      |                     \->  previews/<name>.png (kept)
                                      \->  export_glb.py  ->  <name>.glb  ->  src/Telaelus/assets/models/...
```

Every new or updated model ships with its kept preview (see Kept previews below).

Assets are described as code (a Python script emits geometry), not edited by hand, so any asset
can be tweaked by changing a parameter and regenerating.

## Setup (once per machine)

The tools run in a private Python environment inside the project, like `node_modules`: nothing is
installed globally. With Python 3 on `PATH`:

```bash
python src/asset-factory/setup_env.py
```

This creates `src/asset-factory/.venv/` (git-ignored) and installs the exact versions in
`requirements.txt` (numpy and Pillow). Run it again whenever `requirements.txt` changes. To add or
upgrade a package, install it into the environment, then pin the new version in
`requirements.txt`.

Run every tool with the environment's Python, written `<py>` below:

- Windows: `src/asset-factory/.venv/Scripts/python`
- macOS and Linux: `src/asset-factory/.venv/bin/python`

The generators, `arrange.py`, and `export_glb.py` use only the standard library and also run
with any Python 3; `preview.py` needs the environment.

## Tools

| Tool | Where | Used for |
|------|-------|----------|
| Python 3 | on `PATH` | creating the environment |
| Asset factory environment | `src/asset-factory/.venv` | generating, exporting, preview renders |
| Godot | `godot.editor` in `tools.json` | engine-accurate preview check (opens a window) |

## Source format

Generators write Wavefront `.obj` plus a sibling `.mtl`. Rules every generator follows:

- **Y is up.** The origin is the center of the model's base, resting on y=0.
- **Units are world units.** 1 map cell is 4 x 4 units; see the in-game README for the scale of
  each asset class (ore about 0.8 tall, trees about 2.5, buildings about 1, terrain tiles 4 x 4).
- **Counter-clockwise front faces** (the OBJ and glTF convention). The Godot preview renderer
  reverses winding, since Godot treats clockwise as front; the exported `.glb` keeps it as is.
- **Flat colors only.** Color comes from `Kd` in the `.mtl`; there are no textures. Optional `Ke`
  adds emission (for glowing materials) and `d` adds transparency.
- **Flat shading.** Faces are written as separate triangles with no shared vertices, so every
  face shades flat. This is the low-poly look.
- **Deterministic.** Every generator seeds its own `random.Random`, so regenerating gives
  identical geometry. Change the seed to get a new variant.
- **Tileable assets** (terrain overlays) must keep their shared edge profile identical across
  all variants so any tile can sit next to any other.
- **Cell-filling assets that do not tile** (mountains) stay inside their 4 x 4 cell and meet the
  ground at every edge, so they look finished alone and beside anything.

## Generating

```bash
<py> src/asset-factory/tools/gen.py
```

This writes every model to `src/asset-factory/models/`. To add an asset, write a function that
builds a `Mesh` (in the generator module for its family) and write it from that module's
`build_all` (or `gen.py`'s `__main__` block). Take colors from `PALETTE` in `mesh.py` through a
`Materials` collector (`mat("wood")`, or `mat("grass", 1.05)` for a shade), which names each
material and marks palette entries in `GLOW` as emissive; see docs/blueprints/art-style.md for
the palette and the look.

## Previewing

### Python renderer (default, no window)

```bash
<py> src/asset-factory/tools/preview.py <out.png> <model.obj> [<model.obj> ...]
```

It draws a 2 x 2 contact sheet: the game camera angle (yaw 45, pitch 48), two other yaws, and a
top-down view, orthographic, over a grass-colored ground, lit like the game (one directional light
at the game's angle, with shadows). It takes about a second, never opens a window, and prints
errors to the terminal.

- The camera frames the model tightly, with one scale for all four views.
- Defaults: 256 pixels per view and a 64-color palette, about 20 KB per sheet, so regenerating
  many previews keeps commits small. `--size`, `--colors` (0 for full color), `--margin`, and
  `--aa` (supersampling) override them.
- Several OBJ files given together are drawn in the same space.
- `--each COLS` makes a family overview instead: one game-camera tile per model, each framed on
  its own, in a grid `COLS` wide, in the order given. `previews/items.png` is made this way.
- Its lighting is a little flatter than the game's. Use it for shape and color; use the Godot
  renderer or the game for the final look.

### Godot renderer (engine check, opens a window)

A Godot script run against a minimal project (`src/asset-factory/tools/preview/` holds
`project.godot`). It uses the engine and renderer that draw the real game (Mobile on Direct3D 12,
with hard shadows), so use it for a final check. Its lighting is its own (a grey background and some
ambient light, so shapes read), not the game's.

```bash
<godot.editor> --path src/asset-factory/tools/preview --resolution 800x800 -s render.gd -- <abs model.obj|model.glb> <abs out.png> [ortho_size]
```

- **It always opens a window.** Rendering needs a GPU context, and `--headless` replaces the
  renderer with a dummy that returns no image, so it cannot produce previews. A headless display
  driver combined with a real rendering driver also returns no image, and `--no-window` does not
  stop Godot creating the window. The run is short (about a second for a preview).
- Paths after `--` must be absolute. `ortho_size` is optional; without it the camera frames the
  model.
- Godot prints a block of leaked-resource warnings at exit. They are harmless.

### Reviewing

- Read the PNG to review the result, then adjust the generator and rerun. Expect several rounds.
- For terrain, also check a multi-cell arrangement so seams, transitions, and river joins are
  seen in context, not just one tile. `arrange.py` composes a small test map the way the game
  places terrain models:

  ```bash
  <py> src/asset-factory/tools/arrange.py <ground|rivers> test-output/<name>.obj
  <py> src/asset-factory/tools/preview.py src/asset-factory/previews/arrangement_<name>.png test-output/<name>.obj
  ```

  The kept sheets are `previews/arrangement_ground.png` and `previews/arrangement_rivers.png`.
  Add a layout to `LAYOUTS` in `arrange.py` to check something new.

### Kept previews

`src/asset-factory/previews/` holds the reviewed look of every shipped model, so a reviewer can see
a change without running the tools. Every change that adds or updates a model updates its
previews in the same commit:

- **Its own sheet:** `previews/<model name>.png`, rendered by `preview.py` with its defaults from
  the model's final `.obj`. A model with variants (`iron_ore_a`, `_b`, `_c`) or one family drawn
  together may share one sheet named for the family (`previews/iron_ore.png`) that shows every
  variant.
- **Its family overview,** where one exists: `previews/items.png` (every resource item, in
  `ResourceType` order, `--each 8 --size 160 --colors 256`) and the terrain arrangements
  (`arrangement_<name>.png`, see below). Rerender the whole overview so it always shows every
  model of the family.
- Render kept previews after the last change to the model, so the sheet matches what ships.
  Scratch renders made while iterating stay out of `previews/`.

## Exporting to .glb

```bash
<py> src/asset-factory/tools/export_glb.py <out_dir> <model.obj> [<model.obj> ...]
```

Each model is written to `<out_dir>/<name>.glb`; pass the shipped folder (for example
`src/Telaelus/assets/models/terrain`) to export in place. It handles any number of models in one run
and takes milliseconds.

- Positions are copied as they are (OBJ and glTF are both Y up), and each triangle gets its face
  normal, so the model stays flat-shaded.
- **Color space.** The `.mtl` colors are authored as sRGB values; glTF stores linear color, so the
  exporter converts them. Without that step the model comes out visibly paler in the game.
- Materials are matte (metallic 0, roughness 1, specular 0) and double-sided; `Ke` becomes the
  emissive color and `d` below 1 turns on alpha blending.

## Updating an existing asset

1. Change the generator (parameters, seed, or logic) and rerun it.
2. Render the preview and review it against the previous result.
3. For anything tileable, render the arrangement test as well.
4. Export to `.glb` over the shipped file under `src/Telaelus/assets/models/`. Keep the file name;
   the game loads by name.
5. Update its kept previews (its own sheet and its family overview, see Kept previews).
6. Open the game (or run a scenario with screenshots, see `docs/blueprints/testing.md`) to
   confirm the asset reads correctly in place. Previews do not replace this.

## Adding a new asset

The same steps as updating, plus: follow the naming rules of its `README.md` under
`src/Telaelus/assets/models/` and add the model to that README's table, create its kept preview
sheet, add it to its family overview, and commit the new `.glb` with its `.import` file (see
Shipping below).

## Shipping an asset to the game

- Follow the folder and naming rules in the relevant `README.md` under
  `src/Telaelus/assets/models/` (for example `iron_ore_a.glb`, `tree_conifer_a.glb`).
- Godot caches imported assets. Replacing a `.glb` is not enough for a plain game run, which keeps
  drawing the old cached model. Re-import first with
  `<godot.editor> --headless --path src/Telaelus --import` (it exits on its own), or just open the
  editor once. When replacing an existing model the `.import` file is unchanged; for a new model
  Godot writes a sibling `.import` file, which is committed together with the model.
- Wiring a new asset class into the presentation code is a normal changeset (see
  `docs/development.md`); only the asset files themselves follow this document.

## Limits

- Procedural low-poly suits props, rocks, buildings, and terrain. Organic shapes come out chunky.
- OBJ has no rigging or animation. Rigged or animated assets need a different source (authoring
  in a 3D tool or a CC0 pack).
- Previews are static stills at fixed angles. They show silhouette, color, and scale, not
  animation or in-game readability at zoom.
