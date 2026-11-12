# Raw Resources

- Status: Implemented
- Id: 2611111844-raw-resources
- Depends-on: none

## Summary

Replaces the placeholder Ore and Wood with the eight raw resources the first ship launch needs:
Wood, Stone, Iron Ore, Copper Ore, Coal, and Crude Oil as resource fields, and Sand and Water as
terrain-sourced resources (defined now, collected once their buildings exist). Biomes offer the
deposits from the game plan, the Mine works any mineral deposit, a new Oil Well extracts Crude
Oil, and every deposit gets generated field models. The full production tree toward a ship, and
the power sources, are recorded in the game plan as intent for later changesets.

## Motivation

Today the loop is Ore and Wood to Metal and Fuel, which stands in for "a factory" but cannot grow
into a ship. Working backward from the ship (hull, engine, control, rocket fuel) gives a small,
fixed set of raw resources with a reason for each, and gives the biomes their signature
resources. Settling the raw layer first lets the intermediate buildings, the ship parts, and power
each land as their own changeset on top of it.

## Design

### The production tree (intent, recorded in the game plan)

Not built in this changeset; it is the plan the raw set is chosen for. Counts are placeholders
for tuning.

```
Ship launch = Hull Section x10 + Engine x1 + Control Module x1 + Rocket Fuel x200

Hull Section ── Hull Plate x4 ── Steel ──┬── Iron ── Iron Ore
             └─ Steel Beam x2 ── Steel   └── Coal
Engine ──────── Steel, Copper Wire ── Copper ── Copper Ore
             └─ Circuit ──┬── Copper Wire
                          └── Plastic ── Crude Oil (refinery by-product)
Control Module ─ Circuit, Hull Plate
             └─ Glass ── Sand
Rocket Fuel ──── Crude Oil + Water (refinery)
```

- Buildings in the chain: Smelter (Iron Ore to Iron, Copper Ore to Copper, Sand to Glass), Steel
  Mill (Iron and Coal to Steel), Press (Steel to Hull Plate or Steel Beam), Wire Mill (Copper to
  Copper Wire), Refinery (Crude Oil and Water to Rocket Fuel, with Plastic as a by-product), and
  Assembler (Circuits and the ship parts).
- Wood and Stone do not feed the ship: they are construction materials, for when buildings have
  a cost.
- **Power** (later): processors draw power; extractors stay unpowered at first. Sources: a Burner
  Generator (burns Coal or Wood), a Water Wheel (on a Straight river cell, small and free), an Oil
  Generator (burns a refinery product), and later a geothermal tap at Lava, solar, and nuclear.
- Left for later ships and planets: sulfur, biomass, crystals, rare metals, and uranium (the
  Swamp, Volcanic, and Tundra signatures).

### Resource types and definitions

`ResourceType` becomes:

| Type | Kind | Hand-harvest | Source |
|---|---|---|---|
| Wood | deposit | yes | Wood fields (trees) |
| Stone | deposit | yes | Stone fields (boulders) |
| IronOre | deposit | yes | Iron Ore fields |
| CopperOre | deposit | yes | Copper Ore fields |
| Coal | deposit | yes | Coal fields |
| CrudeOil | deposit | no | Crude Oil fields (oil seeps), by Oil Well only |
| Sand | terrain-sourced | no | a collector on Sand (later) |
| Water | terrain-sourced | no | a pump on ShallowWater (later) |
| Iron | refined | | Smelter |
| RocketFuel | refined | | Refinery |

- A new `ResourceDefs` table gives each type a display name, its `Kind` (Deposit,
  TerrainSourced, or Refined), and whether it can be hand-harvested. Rules read the table, not
  the enum.
- Only a Deposit type can have a resource field; placing a field of another kind throws.
- The player hand-harvests only hand-harvestable fields; standing by a Crude Oil field offers no
  harvest target.
- Ore becomes IronOre, Metal becomes Iron, and Fuel becomes RocketFuel. Sand and Water exist as
  types so the tree and the HUD order are settled, but nothing produces them yet.

### Biome deposits

Each biome's field table follows the game plan's signature resources (amounts per field):

| Biome | Deposits |
|---|---|
| Grassland | Wood 250, Stone 250, Iron Ore 250 |
| Forest | Wood 500 |
| Highlands | Iron Ore 400, Copper Ore 300, Coal 400, Stone 400 |
| Desert | Copper Ore 250, Crude Oil 500 |
| Swamp | Wood 250 |
| Tundra | Iron Ore 250 (rare metals and uranium later) |
| Volcanic | Iron Ore 400, Stone 400 (sulfur and crystals later) |
| Ocean | none |

### Extractors

- An extractor's definition lists the deposit types it works instead of a single type:
  - **Mine** (2 x 2, radius 6): Iron Ore, Copper Ore, Coal, and Stone.
  - **Lumber Camp** (2 x 2, radius 6): Wood.
  - **Oil Well** (new, 2 x 2, radius 3): Crude Oil. The short radius means it sits beside its
    seep.
- An extractor works the nearest non-depleted field of any type it works (its current field
  first, as today) and adds that field's type to the stockpile. Its work area and the ghost's
  field highlights and count cover every type it works.
- The Oil Well is key 5 in build mode and gets a generated model (`oil_well.glb`): a small
  pumpjack (a beam on an A-frame over a wellhead) with a tank, authored facing North like the
  other buildings, with its own accent color (oil black-and-yellow).

### Placeholder production

Until the Steel Mill, Refinery water input, and ship parts exist, the chain stays two steps so the
game still runs end to end:

- **Smelter:** 1 Iron Ore to 1 Iron, 1 s (unchanged in shape).
- **Refinery:** 1 Crude Oil to 1 Rocket Fuel, 2 s (Water joins as an input when the pump exists).
- **Launch:** 500 Rocket Fuel and 100 Iron (renamed costs, same amounts).

### Test map deposits

The fixed test map's fields change to show every deposit and still afford a launch:

| Type | Map cells | Biome | Total |
|---|---|---|---|
| Iron Ore | (10, 9), (11, 10), (9, 11) | Grassland | 750 |
| Iron Ore, Copper Ore, Coal, Stone | (4, 12), (2, 11), (1, 9), (3, 9) | Highlands | 400, 300, 400, 400 |
| Wood | (3, 4), (4, 3), (2, 5) | Forest | 1500 |
| Wood, Stone | (12, 3), (7, 10) | Grassland | 250, 250 |
| Crude Oil | (14, 6), (15, 7) | Desert | 1000 |

That is 1150 Iron Ore (enough for 100 Iron) and 1000 Crude Oil (enough for 500 Rocket Fuel).

### Field models

Each deposit has three generated variants (a, b, c), shown five per field as today
(`NodeModels`), with target heights:

| Deposit | Files | Height | Look |
|---|---|---|---|
| Iron Ore | `iron_ore_a..c` (renamed from `rock_a..c`) | 0.8 | grey rock with rust-red veins (today's model) |
| Copper Ore | `copper_ore_a..c` | 0.8 | brown-grey rock with green (malachite) and copper-orange veins |
| Coal | `coal_a..c` | 0.6 | a heap of black, faceted lumps with a slight sheen of dark grey |
| Stone | `stone_a..c` | 0.9 | pale grey boulders, a few stacked, no veins |
| Crude Oil | `crude_oil_a..c` | flat (0.05) | an oil slick: a glossy black pool with an iridescent rim (violet, teal, grey sheen) on a halo of stained ground that slopes down to the ground; it sits just above the ground tiles' relief so the ground never shows through, and its slot width sets its size |
| Wood | `tree_a..c` | 2.5 to 3.5 | unchanged (CC0) |

They follow art-style.md: flat colors from the palette, details nearly flat (no steep small
faces), new palette entries for the ore and oil colors.

## Code changes

### Simulation (`src/Game.Sim`)

- `ResourceType.cs`: the ten types above.
- New `ResourceDefs.cs`: `ResourceKind` (Deposit, TerrainSourced, Refined) and the `ResourceDef`
  record (name, kind, hand-harvestable) for each type.
- `BiomeDefs.cs`: the deposit tables above.
- `BuildingType` gains `OilWell`; `BuildingDef.Extracts` becomes a list of types (`Works`), with
  `IsExtractor` true when it is non-empty; `BuildingDefs` adds the Oil Well and the Mine's four
  types.
- Extraction (`Simulation`): an extractor picks the nearest field of any type it works and adds
  that field's type. `FieldsInRange` takes a list of types (a single-type overload remains).
- Hand-harvesting skips fields whose type is not hand-harvestable.
- `Recipes.cs`: the Smelter and Refinery recipes above. `Simulation`: `LaunchFuelCost` and
  `LaunchMetalCost` become `LaunchRocketFuelCost` and `LaunchIronCost`.
- `SimulationTerrain.SpawnInitialFields`: the test map deposits above.
- `SimulationDebug.PlaceField`: throws for a non-deposit type.

### Asset factory

- `src/asset-factory/tools/resource_gen.py` (new): the ore generator moved from `gen.py` and
  generalized (a host rock with veins and loose chunks, parameterized by palette colors) for iron
  and copper ore, plus coal, stone, and crude oil field models; `gen.py` runs it. New palette
  entries in `mesh.py` for the ore, coal, boulder, and oil colors (including the pool's rim sheen)
  and the Oil Well accent.
- `building_gen.py`: the Oil Well.
- Models and `.glb` exports in `src/Game/assets/models/nature/` and `buildings/`. The `rock_*`
  models and previews are removed (now `iron_ore_*`); kept previews are one sheet per deposit (its
  `a` variant) and one for the Oil Well.

### Presentation

- `NodeModels.cs`: model specs for every deposit type (paths and target heights).
- `BuildingModels.cs`: the Oil Well file name.
- `BuildController.cs`: key 5 selects the Oil Well; work-area highlights and the count use the
  extractor's type list.
- `Hud.cs`: resource totals in two lines, deposits then refined (Wood, Stone, Iron Ore, Copper
  Ore, Coal, Crude Oil; Iron, Rocket Fuel), using the names in `ResourceDefs` and skipping
  terrain-sourced types until they can be collected; the launch line shows Rocket Fuel and Iron;
  the build hint adds `[5] Oil Well`; the harvest hint names the resource.

### Testing

- `StateSnapshot`, `scenario.schema.json`: the new type names (the resource enum, and
  `OilWell` in the building enum).
- Scenarios that name Ore, Metal, or Fuel move to IronOre, Iron, and RocketFuel
  (`field-stage-views`, `harvest-input`, `place-at-mouse`).
- New scenario `resource-fields.json`: one field of each deposit type in a row with a screenshot;
  standing by the Crude Oil seep gives no harvest target and holding E gathers nothing; a Mine
  beside an Iron Ore field and a Coal field fills Iron Ore from the nearer one and no Coal; an Oil
  Well beside the seep fills Crude Oil; a second screenshot shows both.
- Unit tests: new `ResourceTests` (every type has a def and only deposits take fields; non-deposit
  fields are rejected; biomes offer only deposits, with their signature amounts; a Mine works
  Copper Ore first, then Coal when it depletes, and ignores Wood; an Oil Well pumps only the seep
  within its 3-unit radius; hand-harvest skips Crude Oil but takes Stone). Existing tests move to
  the new names; the Refinery test turns Crude Oil into Rocket Fuel; the test-map totals and field
  count change; the end-to-end launch places an Oil Well between the two seeps instead of Lumber
  Camps.

## Blueprint updates

- **resources.md**: the resource types and `ResourceDefs` (kinds and hand-harvesting), deposits
  only for fields, the biome deposit amounts by reference to terrain.md, the test-map fields, and
  field rendering per deposit type.
- **terrain.md**: the biome table's fields column becomes the deposit table above.
- **buildings.md**: extractors work a list of types; the Mine's types; the Oil Well (footprint,
  radius, key 5, model); the placeholder Smelter and Refinery recipes; launch costs in Rocket Fuel
  and Iron.
- **presentation.md**: the HUD's two resource lines.
- **art-style.md**: the new palette entries and the deposit size targets.
- **testing.md**: the current-scenarios list gains `resource-fields`, `place-at-mouse` counts an
  Iron Ore field, and the list catches up with `building-models` and the wider `terrain-types`
  from the previous changeset.
- Folder READMEs: `nature/` (every deposit's files, heights, and source) and `buildings/` (the
  Oil Well). **docs/asset-pipeline.md** (a how-to): `resource_gen.py` in the tools list.
- **docs/game-plan.md** (not a blueprint): a "Production tree" section with the ship tree, the
  buildings in the chain, the power sources, and what is left for later; the biome table notes
  which signatures exist now.

## Verification

- `dotnet build game.slnx -c Debug`; `dotnet test src/Game.Sim.Tests` and
  `dotnet test src/Game.Integration.Tests` pass.
- Previews of every deposit model and the Oil Well; a windowed `resource-fields` run and a test-map
  run to review each deposit in place at the default zoom.
- In the game: hand-harvest Iron Ore, mine Copper and Coal with one Mine, pump Crude Oil with an
  Oil Well, smelt Iron, refine Rocket Fuel, and launch.
