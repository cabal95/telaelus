# Level Models

- Status: Implemented
- Id: 2611261712-level-models

## Summary

Add a 4 x 4 model for every level of the seven building families in the building levels plan
(future/building-levels.md): Mine, Lumber Camp, Water Pump, Sand Collector, Oil Well, Smelter, and
Kiln. That is 21 models, named `buildings/<family>_<level>` (`mine_1`, `mine_2`, `mine_3`). Eleven
are today's models refit to the 4 x 4 plot, and ten are new. Within a family, each level reads as
an upgrade of the one before, not as a different building.

This is models only: the game keeps loading today's models until the leveled buildings change
switches to these and deletes the ones it replaces.

## Motivation

The leveled buildings need a model per level, at one footprint per family. Landing the models
first lets them be reviewed on their own (shape, scale, and whether the levels read as upgrades)
before the game logic changes, and keeps the leveled buildings changeset to code.

## Design

### The plot and scale

- **Every level model** fills a 4 x 4 plot (`half(4)`, 1.9 to each side) facing North, its front
  toward -Z, with a ground pad or yard over the whole plot.
- **Structures grow a little:** a refit model's buildings are scaled up modestly, 1.2x for a 2 x 2
  original and 1.1x for a 3 x 3 (never more than 20%), and stand on the larger plot. Today's 2 x 2
  buildings looked very small, and this helps without doubling them. A level 1 still looks smaller
  on its plot than the higher levels, which keeps the upgrade read. The plot around a structure
  gets yard dressing in the family's own props (ore heaps, log piles, crates, pipes, sand), so it
  doesn't look empty. The "half size" level 1s are today's building at 0.75x.
- **Identity colors:** every level of a family uses the family's accent (`mine_accent`,
  `lumber_accent`, `smelter_accent`, and so on). A powered level adds a power pole (the existing
  `power_pole` helper), the tier 2 mark.

### The models

| Model | Level | Built from | Description |
|---|---|---|---|
| `mine_1` | Mine | refit `mine` | Today's timber headframe and ore cart, on a 4 x 4 stone pad with ore heaps and a second cart on the rails |
| `mine_2` | Drill Rig | refit `drill_rig` | Today's derrick and motor house on the 4 x 4 pad, an ore heap at the front |
| `mine_3` | Deep Mine | `deep_mine` | Already 4 x 4; unchanged |
| `lumber_camp_1` | Lumber Camp | new, from `lumber_camp` | Today's camp with the shed at about half size, a small log pile and a sawhorse |
| `lumber_camp_2` | Lumber Mill | refit `lumber_camp` | Today's camp, full size, on the 4 x 4 plot with a larger log yard and a power pole |
| `lumber_camp_3` | Advanced Lumber Mill | refit `lumber_mill` | Today's saw hall on the 4 x 4 plot with stacked lumber at the front |
| `water_pump_1` | Water Pump | new, from `water_pump` | Today's pump house at about half size, its intake pipe reaching toward the water at the front |
| `water_pump_2` | Pumping Station | new, from `water_pump` | Today's pump house, full size, with two pipes leaving the building, one at each end, and a power pole |
| `water_pump_3` | Advanced Pumping Station | new, from `pumping_station` | Today's pump hall with four intake pipes leaving its front edge, the door moved to the side wall, and a power pole |
| `sand_collector_1` | Sand Collector | new, from `sand_collector` | Today's hopper on legs at about half size, over a small sand pile |
| `sand_collector_2` | Sand Sifter | refit `sand_collector` | Today's hopper, full size, over a sand pile on the 4 x 4 plot, a sieve frame beside it and a power pole |
| `sand_collector_3` | Sand Dredge | refit `sand_dredge` | Today's scoop wheel, conveyor and hopper on the 4 x 4 plot |
| `oil_well_1` | Oil Well | refit `oil_well` | Today's pumpjack and tank on a 4 x 4 oil-stained pad with a second, smaller tank |
| `oil_well_2` | Oil Rig | new, from `deep_well` | A simplified `deep_well`: a shorter derrick over the wellhead, one tank, no extras, and a power pole |
| `oil_well_3` | Deep Well | `deep_well` | Already 4 x 4; unchanged |
| `smelter_1` | Smelter | refit `smelter` | Today's stone furnace block and chimney on the 4 x 4 plot, with an ore bin and a fuel pile |
| `smelter_2` | Steel Mill | new, from `smelter` and `steel_mill` | The `smelter` block, with the Steel Mill's tipping converter glowing beside it, a blue-roofed casting shed, and a power pole |
| `smelter_3` | Electric Smelter | new, from `smelter_2`, `electric_furnace` and `aluminum_smelter` | `smelter_2` with the chimney replaced by the Electric Furnace's round arc furnace and its three electrodes, and the Aluminum Smelter's skylight band along the shed |
| `kiln_1` | Charcoal Kiln | refit `charcoal_kiln` | Today's two brick domes and log stack on the 4 x 4 plot, with a charcoal heap |
| `kiln_2` | Cement Kiln | new, from `kiln_1` and `cement_kiln` | The two domes, with a short rotary kiln drum from `cement_kiln` on piers alongside and a small preheater stack |
| `kiln_3` | Tile Kiln | new, from `kiln_2` and `tile_kiln` | One dome, and the red brick tunnel kiln from `tile_kiln` shortened to fit, its car track out to the front, a chimney, and a power pole |

### Previews

- **One sheet per model:** `previews/buildings/<family>_<level>.png`.
- **A levels overview:** `previews/building_levels.png`, with the families as rows and levels 1 to
  3 across (`--each 3`), so each family's upgrade reads at a glance.

## Code changes

- `src/asset-factory/tools/building_gen.py`:
  - **New models:** a function for each of the 21 models. Refits draw today's model over the 4 x 4
    pad and add the yard dressing. New models build on today's helpers and parts.
  - **Shared props:** helpers for the yard dressing (ore heap, log pile, crate stack, sand pile,
    pipe run).
  - **`BUILDINGS`** includes the 21 new names; today's models stay as they are.
- **Exports:** the 21 `.glb` files in `src/Telaelus/assets/models/buildings/`, plus their imports.
- **Previews:** the 21 sheets and `previews/building_levels.png`.
- `src/Telaelus/assets/models/buildings/README.md`: the 21 level models in their own table, marked
  as not loaded yet.

No game code changes. `BuildingModels` still loads today's models.

## Blueprint updates

- docs/asset-pipeline.md: the `<family>_<level>` names and the `building_levels.png` overview under
  Kept previews.
- art-style.md: a note on leveled buildings (one plot per family, structures at real size, levels
  reading as upgrades, a power pole on a powered level).

## Verification

- The generator runs and exports all 21 models, and a headless import passes.
- Review the levels overview and each sheet: every model fits 4 x 4, and each family reads as one
  building growing.
- Sim and integration tests pass (unchanged, since the game doesn't load these yet).

## Notes

- **Retired later:** the leveled buildings changeset deletes today's models that these replace
  (`drill_rig`, `deep_mine`, `lumber_mill`, `pumping_station`, `sand_dredge`, `deep_well`,
  `steel_mill`, `electric_furnace`, `aluminum_smelter`, `cement_kiln`, `tile_kiln`,
  `air_separator`, and the level 1 sources) once nothing loads them.
- **Scale:** the operator chose a modest scale-up (at most 20%) over full doubling, which would make
  a 2 x 2 Mine stand taller than the Drill Rig.

## Implementation notes

- **Where the code lives:** the level models are a "Level models" section of `building_gen.py`
  (`LEVELS`), written by `build_all` after the other buildings.
- **How refits are built:** a refit takes today's model function's mesh, scales it about its base
  centre (`Mesh.scaled`), and moves it onto the plot (`_place`).
- **When parts change:** where a level changes part of a model, a helper redraws it
  (`_pump_house`, `_smelter_block`, `_dome`) so the part can be left out or rescaled. Examples are
  the Water Pump's single front intake (level 2 has two side intakes) and the Smelter's chimney
  (level 3 has the arc furnace).
- **Fit check:** every model was measured to fit within 1.9 of the centre on X and Z. A pole's
  crossbar and some pipe ends first stood a few hundredths over the edge and were pulled in.
- **Unchanged:** no existing model or game code changed. The 21 models are exported and imported
  but not loaded yet.
