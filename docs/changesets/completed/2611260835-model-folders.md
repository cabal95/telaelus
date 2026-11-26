# Name Models by Their Folder

- Status: Implemented
- Id: 2611260835-model-folders

## Summary

Every model is named by its folder, with no prefix in its file name: `items/iron_ore.glb` (was
`items/item_iron_ore.glb`), `icons/moon.glb` (was `icons/icon_moon.glb`). The asset factory takes
the same folders for its sources and previews (`models/items/iron_ore.obj`,
`previews/items/iron_ore.png`), so a model has one name from generator to game.

## Motivation

The game's models already sit in folders (buildings, character, icons, infrastructure, items,
nature, terrain, units), but two of them also prefix every file (`item_`, `icon_`) and the others do
not, and the asset factory keeps all 270 or so sources and 165 previews in two flat folders. A
reader has to know which convention a model follows, and the flat factory folders only avoid
clashes because of the prefixes.

## Design

- **The rule:** a model's name is `<folder>/<name>`, the folder saying what kind of model it is and
  the name saying which one. File names carry no kind prefix. Folders stay as they are
  (`buildings`, `character`, `icons`, `infrastructure`, `items`, `nature`, `terrain`, `units`); with
  no prefix to repeat it, `infrastructure` keeps its full name.
- **Renames in the game** (`src/Telaelus/assets/models/`): the 63 `items/item_<name>.glb` become
  `items/<name>.glb` and the 5 `icons/icon_<name>.glb` become `icons/<name>.glb`. Every other model
  keeps its name. Each `.glb.import` moves with its model, so Godot keeps its uid.
- **No clash at runtime:** the game only ever loads a model by its folder path through
  `ModelLibrary` (`"icons/moon"`, `"nature/stone_a"`), never by a bare file name, so `items/stone`
  and a future `nature/stone` are different models. Two files of one name can only live in two
  folders, so names cannot collide.
- **Code that names models:** `ResourceIcons.ItemPath` builds `items/<resource>`; UI icon names
  drop `icon_` (`UiIcons.Fixed` is `sun`, `moon`, `power`; `ResearchTree.ResearchIconName` gives
  `material_transfer`); `TopBar` and `NotificationText` ask for `sun`, `moon`, `power`. The
  `ResourceTests` model check looks for `items/<resource>.glb`. `Town.tscn`'s model paths follow.
- **The asset factory** mirrors the game:
  - `models/<folder>/<name>.obj` and `.mtl`: `Mesh.write` takes `"<folder>/<name>"` and creates the
    folder. Each generator names its folder: `building_gen.py` writes `buildings/` and its road and
    bridge pieces `infrastructure/`; `item_gen.py` `items/` and `icons/`; `resource_gen.py`,
    `gen.py`'s trees and deposits `nature/`; `terrain_gen.py` and `gen.py`'s mountains `terrain/`;
    `character_gen.py` `character/`; `unit_gen.py` `units/`.
  - `export_glb.py` exports a folder in one go (`export_glb.py <game folder> models/<folder>/*.obj`),
    as now.
  - `previews/<folder>/<name>.png` for a model's own sheet (a variant family's shared sheet, such
    as `nature/bauxite.png` for `bauxite_a` to `_c`, sits in that folder too). Family overviews and
    arrangements stay at the top of `previews/` (`items.png`, `icons.png`, `infrastructure.png`,
    `ship_parts.png`, `character_poses.png`, `arrangement_<name>.png`), since they span a folder.
  - Scratch outputs stay in `test-output/`, as now.
- **No model changes:** files are moved and renamed, not regenerated; the factory's generators are
  re-run once to confirm they write the same files to the new places.

## Code changes

- `src/Telaelus/assets/models/items/` and `icons/`: `git mv` each `.glb` and `.glb.import`; the
  folders' `README.md`s.
- `src/Telaelus/Presentation/Ui/ResourceIcons.cs`, `UiIcons.cs`, `TopBar.cs`, `ResearchTree.cs`,
  `NotificationText.cs`: the names.
- `src/Telaelus/scenes/Town.tscn`: model paths (only if any point at items or icons).
- `src/Telaelus.Sim.Tests/ResourceTests.cs`: the item model check.
- `src/asset-factory/tools/*.py`: folder-qualified names in every generator, `Mesh.write`,
  `preview.py`'s default output path if it derives one, and `arrange.py`'s model lookups.
- `src/asset-factory/models/` and `previews/`: `git mv` into the folders.
- A headless import (`--import`) so the moved models re-import; the splash and demo save unchanged.

## Blueprint updates

- docs/asset-pipeline.md: the folders, `Mesh.write`'s names, export by folder, and the kept
  previews' places ("Kept previews").
- art-style.md and presentation.md: model names that change (items, icons).
- research.md: project icon names.
- testing.md: the `ResourceTests` model check.

## Verification

- Sim and integration tests pass; the headless runtime check's stderr is empty (every model
  loads).
- Re-running every generator writes no file outside its folder, and `git status` after it shows no
  change to any model.
- Windowed: the top bar's sun, moon, and Power icons and the inventory's item icons still show.

## Implementation notes

- Re-running every generator wrote no new file and changed only the 68 renamed item and icon
  sources, whose `mtllib` line now names the renamed `.mtl`. Those 68 `.glb`s were re-exported so
  their node names match too; the import kept every uid (Godot rewrote each `.import`'s paths).
- Sorting the previews showed the clash the rule removes: a deposit's family sheet
  (`previews/bauxite.png`) and the Bauxite item's sheet would both be `bauxite.png`; they are now
  `previews/nature/bauxite.png` and `previews/items/bauxite.png`.
- `character_poses.png` moved into `previews/character/` with the character's own sheet, since it
  shows only the character; `ship_parts.png` stays at the top (it spans buildings).
- `Mesh.write` refuses a name without a folder, so a new generator cannot drop a model at the top.
- Windowed runs of `storage` and `research` show the item, sun, power, and project icons.
