# Leveled Buildings

- Status: Implemented
- Id: 2611261944-leveled-buildings

## Summary

Seven building families become single building types with **levels**: Mine, Lumber Camp, Water
Pump, Sand Collector, Oil Well, Smelter, and Kiln. Each level is its own definition, with name,
tier, cost, draws or supplies, speed, what it works or makes, and model, and every level is 4 x 4.

- **Unlocking:** a level above 1 is unlocked by its own research project.
- **Building:** you can build any unlocked level directly, or upgrade a placed building in place,
  paying the difference in cost.
- **Deep levels:** the Mine's and Oil Well's level 3s (Deep Mine, Deep Well) work worked-out
  cells, motherlodes, and Salt Domes **around** them instead of standing on one.
- **Removed:** twelve building types merge into the families or are dropped (the Air Separator).
- **Models:** the game switches to the level models (2611261712-level-models) and deletes the ones
  they replace.
- **Saves:** the save version rises to 2, so saves from before this change are not loadable.

This is phase 1 (with phase 3, the deep levels) of the building levels plan
(future/building-levels.md).

## Motivation

Today the same job is done by two or three separate buildings across the tiers (a Mine, a Drill
Rig, a Deep Mine). The plan merges them so the build menu shrinks, an upgrade keeps the building
where it stands, and research decides when a better level is available.

## Design

### Levels

- **`BuildingType`** keeps one value per family (`Mine`, `LumberCamp`, `WaterPump`, `SandCollector`,
  `OilWell`, `Smelter`, `Kiln`). `CharcoalKiln` is renamed `Kiln`.
- **Removed types:** `DrillRig`, `LumberMill`, `PumpingStation`, `SandDredge`, `DeepWell`,
  `DeepMine`, `SteelMill`, `ElectricFurnace`, `AluminumSmelter`, `CementKiln`, `TileKiln`, and
  `AirSeparator` (dropped).
- **Definitions:** `BuildingDefs.For(type, level)` gives a level's definition, and
  `BuildingDefs.Levels(type)` how many a type has (1 for every other building).
  `BuildingDefs.For(type)` is level 1.
- **A placed building** has a `Level` (1 to 3), and its `Def` is that level's definition.
- **The model** is `buildings/<family>_<level>` for a leveled building and today's file for the
  rest.
- **Every level of a family is 4 x 4** and keeps its family's placement rule:
  - the Water Pump is beside water;
  - the Sand Collector is on sand (all 16 cells);
  - the others are standard.
- **Recipes:** a higher level keeps every recipe of the level below, in the same order, and adds
  its own after them. So a queue's or hold's recipe index stays valid through an upgrade.

### The families

Costs, tiers, and numbers per level. A level's tier is the tier its unlock research sits in. Speeds
are 0.75, 1.3, and 1.6, and process speed research (a later changeset) multiplies on top of them.

| Family | Level 1 | Level 2 | Level 3 |
|---|---|---|---|
| Mine | **Mine** (1): 25 Wood, 15 Stone; unpowered; radius 6, speed 0.75; today's ores | **Drill Rig** (2): 25 Wood, 20 Stone, 30 Iron; 10 Power; radius 8, speed 1.3; adds Rare Earths, Uranium Ore, Crystals | **Deep Mine** (3): 25 Wood, 20 Stone, 30 Iron, 60 Steel, 30 Concrete; 30 Power; radius 8, speed 1.6; adds worked-out cells and motherlodes around it |
| Lumber Camp | **Lumber Camp** (1): 10 Wood; unpowered; radius 6, speed 0.75 | **Lumber Mill** (2): 30 Wood, 20 Iron; 5 Power; radius 8, speed 1.3 | **Advanced Lumber Mill** (3): 40 Wood, 20 Iron, 30 Steel; 10 Power; radius 8, speed 1.6 |
| Water Pump | **Water Pump** (1): 20 Wood, 10 Stone; supplies 4 Water | **Pumping Station** (2): 20 Wood, 20 Stone, 30 Iron, 10 Steel; 5 Power; 10 Water | **Advanced Pumping Station** (3): 20 Wood, 20 Stone, 30 Iron, 40 Steel, 20 Concrete; 10 Power; 16 Water |
| Sand Collector | **Sand Collector** (1): 20 Wood, 10 Stone; 2 Sand | **Sand Sifter** (2): 20 Wood, 10 Stone, 20 Iron, 10 Steel; 5 Power; 4 Sand | **Sand Dredge** (3): 20 Wood, 10 Stone, 20 Iron, 40 Steel, 20 Concrete; 10 Power; 8 Sand |
| Oil Well | **Oil Well** (2): 25 Iron, 20 Stone, 10 Wood; unpowered; radius 3, speed 0.75 | **Oil Rig** (2): 25 Iron, 20 Stone, 10 Wood, 30 Steel; 10 Power; radius 3, speed 1.3 | **Deep Well** (3): 25 Iron, 20 Stone, 10 Wood, 50 Steel, 30 Concrete; 15 Power; radius 3, speed 1.6; adds worked-out oil cells and Salt Domes around it |
| Smelter | **Smelter** (1): 40 Stone, 15 Wood; burns fuel; Iron, Copper | **Steel Mill** (2): 60 Stone, 15 Wood, 40 Iron; 20 Power, no fuel; adds Steel | **Electric Smelter** (2): 60 Stone, 15 Wood, 40 Iron, 50 Steel, 20 Copper; 60 Power; adds Aluminum, Silicon, Titanium |
| Kiln | **Charcoal Kiln** (1): 30 Stone, 10 Wood; Charcoal | **Cement Kiln** (1): 40 Stone, 10 Wood, 20 Iron, 20 Lumber; burns fuel; adds Cement | **Tile Kiln** (3): 40 Stone, 10 Wood, 20 Iron, 20 Lumber, 20 Steel; 30 Power, no fuel; adds Ceramic Tile |

- **Each level's cost includes the level below's.** A level's direct cost is the level 1 cost plus
  each upgrade step, so building a level directly and building level 1 then upgrading cost the
  same, and so does the refund. The steps are the upgrade prices:

  | Family | Level 1 | Step to level 2 | Level 2 direct | Step to level 3 | Level 3 direct |
  |---|---|---|---|---|---|
  | Mine | 25 Wood, 15 Stone | 30 Iron, 5 Stone | 25 Wood, 20 Stone, 30 Iron | 60 Steel, 30 Concrete | 25 Wood, 20 Stone, 30 Iron, 60 Steel, 30 Concrete |
  | Lumber Camp | 10 Wood | 20 Wood, 20 Iron | 30 Wood, 20 Iron | 10 Wood, 30 Steel | 40 Wood, 20 Iron, 30 Steel |
  | Water Pump | 20 Wood, 10 Stone | 30 Iron, 10 Steel, 10 Stone | 20 Wood, 20 Stone, 30 Iron, 10 Steel | 30 Steel, 20 Concrete | 20 Wood, 20 Stone, 30 Iron, 40 Steel, 20 Concrete |
  | Sand Collector | 20 Wood, 10 Stone | 20 Iron, 10 Steel | 20 Wood, 10 Stone, 20 Iron, 10 Steel | 30 Steel, 20 Concrete | 20 Wood, 10 Stone, 20 Iron, 40 Steel, 20 Concrete |
  | Oil Well | 25 Iron, 20 Stone, 10 Wood | 30 Steel | 25 Iron, 20 Stone, 10 Wood, 30 Steel | 20 Steel, 30 Concrete | 25 Iron, 20 Stone, 10 Wood, 50 Steel, 30 Concrete |
  | Smelter | 40 Stone, 15 Wood | 20 Stone, 40 Iron | 60 Stone, 15 Wood, 40 Iron | 50 Steel, 20 Copper | 60 Stone, 15 Wood, 40 Iron, 50 Steel, 20 Copper |
  | Kiln | 30 Stone, 10 Wood | 10 Stone, 20 Iron, 20 Lumber | 40 Stone, 10 Wood, 20 Iron, 20 Lumber | 20 Steel | 40 Stone, 10 Wood, 20 Iron, 20 Lumber, 20 Steel |

- **Cycle times and output buffers** carry over from today's building at each level. A recipe's
  time is unchanged; a level's speed scales it, as speed does today.
- **The Steel recipe** is level 2's and **Silicon** level 3's. Both still take Coal or Charcoal as
  an input, as today, even though those levels burn no fuel.
- **The Oil Well** keeps its radius 3 at every level.

### Unlocking a level

- **Level 1** is unlocked by its tier, as today.
- **A higher level** is unlocked when its research project is complete
  (`ResearchDef.UnlocksLevel`, the type and level). That project sits in the level's tier, so the
  tier must be open first.
- **`IsUnlocked(type, level)`**; `IsUnlocked(type)` is level 1.

### Getting a level

- **Build menu:** each level is its own cell in its level's tier, named and pictured as that level
  ("Drill Rig" in Tier 2). It is registered by the level's name without spaces
  (`buildMenu/DrillRig`). Choosing an unlocked level's cell places that level.
- **Locked level cells:** when a level's tier is open but its research isn't done, its cell still
  shows in that tier.
  - **Look:** a new `Locked` cell state, the picture in grayscale and dimmed, the name in
    `TextDim`.
  - **Tooltip:** the level's description, then "Needs Powered Mining research". When that
    project's own prerequisite isn't done either, a second line names it ("Needs Deep Mining
    research", then "Deep Mining needs Powered Mining").
  - **Clicking it** does nothing.
  - **Order:** unlocked cells come first, by name, then locked ones, by name.
  - **Locked tiers:** a tier still locked shows nothing; its tier picker cell already says what
    opens it. So a locked cell only appears once its tier is open.
- **Upgrading in place:** a placed building's detail panel shows **Upgrade to Drill Rig** while it
  is below its family's top level.
  - **Hover:** the price, which is the difference in cost per resource (the next level's cost less
    this level's, exactly the step above since each level's cost includes the one below's), short
    amounts marked as in the build menu.
  - **Disabled:** while the next level is locked (tooltip "Needs Powered Mining research") or the
    price isn't held.
  - **Upgrading** (`Simulation.TryUpgrade(id)`) pays the price and swaps the level in place. Its id,
    position, facing, on/off state, queue or hold, input choices, and buffers stay. A running
    cycle finishes at the new speed. If the new level burns no fuel, its fuel slot goes back to
    storage. The network recounts its draws.
  - **One level at a time:** a Mine goes to Drill Rig, then to Deep Mine.
- **Destroying** refunds half of what the building cost in total, as now: every level paid, by the
  original placement plus each upgrade.

### Deep levels (work around)

- **Work area:** the map cells the building's footprint touches or borders.
- **What a Deep Mine works:**
  - everything a Drill Rig works, in radius, at speed 1.6, a live field first;
  - with no live field in range, a worked-out cell (of a deposit today's Deep Mine works) or a
    motherlode in its work area, at today's deep rate (1 unit every 20 s).
- **What a Deep Well works:** live Crude Oil in radius first; else a worked-out oil cell or a Salt
  Dome in its work area, at 1 unit every 26.7 s.
- **No cap:** any number of deep levels may work the same worked-out cell or motherlode, so four
  Deep Mines, one on each edge, all work it.
- **Removed:** the `OverSite` placement rule and `Sites`, which only the Deep Mine and Deep Well
  used.

### Research

Fourteen level projects, one per level above 1. In the research tree each family is a row:

| Row | Tier 1 | Tier 2 | Tier 3 |
|---|---|---|---|
| 0 | Material Transfer | Advanced Material Transfer | |
| 1 | Steel Works | Electronics | Computers |
| 2 | | **Powered Mining**: 40 Steel, 30 Copper; 900 s | **Deep Mining**: 60 Steel, 30 Concrete, 10 Electric Motor; 1500 s |
| 3 | | **Powered Logging**: 30 Steel, 40 Wood; 900 s | **Advanced Logging**: 40 Steel, 20 Electric Motor; 1500 s |
| 4 | | **Powered Pumping**: 30 Steel, 20 Copper; 900 s | **Advanced Pumping**: 40 Steel, 20 Concrete, 10 Electric Motor; 1500 s |
| 5 | | **Sand Sifting**: 30 Steel, 20 Glass; 900 s | **Dredging**: 40 Steel, 10 Electric Motor; 1500 s |
| 6 | | **Oil Rigs**: 40 Steel, 20 Copper; 900 s | **Deep Drilling**: 60 Steel, 20 Concrete, 10 Electric Motor; 1500 s |
| 7 | | **Steelmaking**: 60 Iron, 30 Copper, 40 Stone; 900 s; then **Electric Smelting** (column 1): 60 Steel, 30 Copper, 20 Glass; 1200 s | |
| 8 | **Cement**: 30 Iron, 40 Stone, 40 Wood; 600 s | | **Tile Firing**: 40 Concrete, 20 Silicon, 20 Steel; 1500 s |

- **Prerequisites:** a level 3 project needs its level 2 project (Deep Mining needs Powered
  Mining), and Electric Smelting needs Steelmaking. The Kiln's Tile Firing needs Cement.
- **Steelmaking** is paid in tier 1 items, because Steel can only be made once it's done. It
  becomes the way into Steel, as the Steel Mill was. Every other tier 2 level fee takes Steel, so
  in practice it comes first.
- **Descriptions** name the level and what it adds ("The Mine can be built or upgraded to a Drill
  Rig: powered, faster, and it reaches Rare Earths, Uranium Ore, and Crystals.").
- **Icons:** each project uses the level's building picture (as the Road and Bridge cells use
  pictures), so no new icon models are needed.
- **Completion toasts:** finishing a level project shows the normal "Research complete" toast. Its
  detail is the description, which already says what can now be built.

### Removing the Air Separator

The Air Separator is dropped. Liquid Oxygen comes from the Electrolyzer only. The tier test's Air
Separator exception goes.

### The demo town

The demo town completes Steel Works and Powered Mining, so its Drill Rig is a level 2 Mine. Its
Smelters stay level 1. Its layout is rechecked for 4 x 4 footprints, moving buildings where they no
longer fit, and the demo save is rewritten.

### Saves

`SaveFormat.Version` rises to 2. Each saved building stores its `Level`. Saves from before this
change (version 1) are "Saved by an older version" (2611261642-save-version).

## Code changes

- **Sim:**
  - **`BuildingType.cs`:** the twelve types removed and `CharcoalKiln` renamed `Kiln`.
  - **`BuildingDefs.cs`:** per-level definitions for the seven families, `For(type, level)`,
    `Levels(type)`, the level names, 4 x 4 footprints, costs, draws, supplies, speeds, radii,
    `BurnsFuel`; remove `OverSite` and `Sites`; descriptions per level.
  - **`Recipes.cs`:** `Recipes.For(type, level)`, a family's recipes by level.
  - **`Building.cs`:** `Level`, with `Def` following it.
  - **`Simulation.cs`:**
    - `TryPlaceBuilding(type, level, ...)`, `IsUnlocked(type, level)`, `UpgradePrice(id)`, and
      `TryUpgrade(id)`;
    - destroy refunds by total paid (`Building.Paid`, summed through upgrades).
  - **Extraction:** the deep work area (worked-out cells, motherlodes, Salt Domes) and its
    fallback after live fields. `MotherlodeDefs` drops its Deep Mine type checks.
  - **Capacity:** supplies and draws by level.
  - **`ResearchDefs.cs`:**
    - the fourteen level projects, `UnlocksLevel`, and the new rows;
    - the project picture: a level project names its level, for the tree to picture.
  - **`SaveData.cs`, `SimulationSave.cs`:** `Level` and `Paid` on each building.
  - **`SaveFormat.cs`:** `Version` 2.
  - **`DemoTown.cs`:** research and layout as above.
- **Presentation:**
  - **`BuildingModels.cs`:** `<family>_<level>` for leveled types.
  - **`BuildMenu.cs`:** a cell per level, named for the level; a level whose research isn't done
    shows as a `Locked` cell with its research in the tooltip, after the unlocked cells.
  - **`BuildController.cs`:** `Select(type, level)`, with the ghost and placement tooltip at that
    level.
  - **`BuildingDetail.cs`:** the Upgrade button, its hover price, and the disabled tooltip.
  - **`ResearchTree.cs`:** a level project's card shows its level's building picture.
  - **`BuildingPictures.cs`:** a picture per level.
  - **`StateSnapshot.cs`:** `buildings[n].level`; a build menu cell's `level`; `ui.upgrade` (label,
    enabled, tooltip) in the detail panel.
- **Scenario runner:**
  - `placeBuilding` gains `level`;
  - `clickUi` on `buildingDetail/upgrade`;
  - the schema's building type list updated.
- **Assets:**
  - **Deleted (models, previews, and generator entries):**
    - `drill_rig`, `deep_mine`, `lumber_mill`, `pumping_station`, `sand_dredge`, `deep_well`;
    - `steel_mill`, `electric_furnace`, `aluminum_smelter`;
    - `cement_kiln`, `tile_kiln`, `air_separator`;
    - the level 1 sources `mine`, `lumber_camp`, `water_pump`, `sand_collector`, `oil_well`,
      `smelter`, `charcoal_kiln`.
  - **Kept as code:** the level model functions draw from the old ones in the generator, so the
    generator keeps those functions, unexported.
  - **Rewritten:** the demo save and the splash (if the town scene uses a removed model).
- **Tests:**
  - every sim test and scenario naming a merged type moves to its family and level;
  - new `LevelTests`:
    - each family's levels, costs, and numbers;
    - a level locked until its project, and level 1 by tier;
    - building at a level;
    - upgrading pays the difference, keeps the queue, hold, and choices, finishes a running cycle,
      and returns fuel when the level stops burning it;
    - an upgrade refused while locked or short;
    - `IsUnlocked(type, level)` false for a level whose tier is open but whose project isn't done;
    - destroying refunds half the total paid;
    - a save round trip keeps the level;
  - deep work tests:
    - four Deep Mines around one worked-out cell all producing;
    - a live field preferred;
    - a motherlode and a Salt Dome worked from beside them;
  - the tier tests updated (the Air Separator exception gone, the gateways now Refinery, Oil Well,
    and the Smelter's and Mine's levels);
  - `build-menu` updated: with Tiers 2 and 3 open but no level research, Tier 2 lists the Drill
    Rig, Lumber Mill, and other level 2s as `locked` after the unlocked cells, with "Needs Powered
    Mining research" on hover, and a click on one leaves the menu open;
  - a new `building-upgrade` scenario: a Mine upgraded to a Drill Rig from its panel (disabled
    with its tooltip until Powered Mining, then paying the difference), the model swapping in
    place, and windowed screenshots before and after.

## Blueprint updates

- buildings.md:
  - the level model, the seven families, and the per-level tables in place of the removed
    buildings' rows;
  - upgrading, the price, and the refund;
  - the deep work area in place of `OverSite`;
  - the At a glance table rebuilt (one row per level).
- research.md: the level projects, `UnlocksLevel`, the new rows, and level pictures on cards.
- presentation.md: the build menu's level cells (locked ones dimmed, naming their research), the
  detail panel's Upgrade, and level models.
- resources.md: what makes each item, by level; Liquid Oxygen from the Electrolyzer only.
- saving.md: version 2, and the building's level and paid cost.
- testing.md: the new tests, the scenario, `placeBuilding`'s `level`, and the snapshot fields.
- The buildings README: the level models now loaded, the removed files gone.

## Verification

- Sim and integration tests pass, and the headless runtime check is clean.
- Windowed:
  - a new game builds a Mine, researches Steel Works and Powered Mining, and upgrades it to a Drill
    Rig in place;
  - four Deep Mines around a worked-out Iron Ore cell all produce;
  - the build menu shows each level in its tier, locked ones dimmed with their research named;
  - the demo loads with its Drill Rig.

## Implementation notes

- **No fuel slot to return:** the design said an upgrade to a level that burns no fuel returns the
  burning unit. A burning unit was spent when it was taken, so there is nothing to return; the
  upgrade puts out the fire (`Burning` and `BurnLeft` cleared) and the running cycle finishes.
- **A deep level finds its site before it steps:** an offline or new Deep Mine had no output or
  unit time until its first step found a site, so `OutputChoices` and `UnitSeconds` fall back to
  `DeepSiteAround` for a deep level (`SiteOf`).
- **The demo gained a Mine:** at speed 1.3 the demo's Drill Rig alone no longer keeps the four iron
  Smelters busy, so a second level 1 Mine stands beside it.
- **The panel follows an upgrade:** pressing Upgrade reopens the detail panel on the same building,
  so its title and picture show the new level; the snapshot gains the panel's `title`.
- **Tier 1 gained a project:** Cement (the Cement Kiln level) is a tier 1 project, so the research
  scenario completes it before checking Tier 1's mark.
- **Costs amended after implementing:** the first costs let a level cost less of a resource than
  the level below (the Drill Rig had no Wood), so upgrading cost more than building the level
  directly. Each level's cost now includes the level below's (the table above), and
  `LevelTests.EachLevelsCost_IncludesTheLevelBelows` keeps it so.
- **Retired models:** the nineteen standalone models are deleted; their generator functions stay
  as parts sources for the level models.

## Notes

- **The largest change since tiers:** most tests touch a merged building. The tests move by type
  and level, with the same behavior checked.
- **Balance:** level costs reuse today's buildings' costs where a level is today's building. New
  levels are priced between their neighbours. All numbers are placeholders to tune after playing.
- **Placement is harder for some starters:** every family is 4 x 4, so a Sand Collector needs 16
  sand cells and a Water Pump a 4 x 4 spot beside water. If that proves too strict, the Sand rule
  could accept most cells on sand instead of all.
