# Building Levels (plan)

- Status: Future (a working plan, phase 3 of tier-rework.md; it becomes changesets once agreed)

## Goal

With buildings in tiers (2611261421-tier-model, 2611261445-tier-pickers), cut the number of
buildings that do the same job at two tiers. Merge them into one building with **levels**, where a
higher level may run faster, draw Power, work more, or add recipes that make sense for that
building. Alongside the levels, add research that makes **processes** faster.

Research about buildings then follows two paths:

1. **Level unlocks:** a project unlocks a higher level of certain buildings ("Powered Mining" lets
   the Mine be built or upgraded to level 2).
2. **Process speed:** a project makes one process faster wherever it runs ("Better Ore Smelting":
   Iron Ore and Copper Ore smelt 25% faster).

## Decided so far

- **Approach C:** merge into leveled buildings, and add process speed research.
- **Families:**
  - Mine, Lumber Camp, Water Pump, Sand Collector, and Oil Well;
  - the Smelter (with the Steel Mill, Electric Furnace, and Aluminum Smelter);
  - the Kiln (Charcoal, Cement, and Tile Kilns).
- **Kept separate:** the Fabricator and Assembly Plant.
- **Dropped:** the Air Separator. The Electrolyzer stays a single building.
- **New recipes at a higher level are fine** when they make sense for the building (a Smelter
  making Steel).
- **Every family is 4 x 4 at every level,** so an upgrade never needs room to grow.
- **Each level has its own model,** and the levels read as upgrades of one building, not
  different buildings.
- **Extraction speed:** the Mine, Lumber Camp, and Oil Well families run at speed 0.75 at level 1,
  1.3 at level 2, and 1.6 at level 3. Process speed research multiplies on top of these. Today's
  Drill Rig and Lumber Mill run at 2, so powered extraction is somewhat slower than now.
- **The research tree goes back to one screen,** with a visible section per tier instead of the
  tier picker. A level's prerequisite is often in the tier below ("Deep Mining", tier 3, needs
  "Powered Mining", tier 2), and that link has to be visible (Research tree, below).

## Families

Each level is its own definition: name, tier, cost, draws, speed or supply, what it works or
makes, and model. Every family is 4 x 4 at every level. A level's tier is the tier its unlock
research sits in.

| Family | Level 1 | Level 2 | Level 3 |
|---|---|---|---|
| **Mine** | **Mine** (tier 1): unpowered, radius 6, speed 0.75. Model: today's `mine` | **Drill Rig** (2): 10 Power, radius 8, speed 1.3, adds Rare Earths, Uranium Ore, Crystals. Model: today's `drill_rig` | **Deep Mine** (3): 30 Power, speed 1.6, adds worked-out cells and motherlodes around it (Deep levels, below). Model: today's `deep_mine` |
| **Lumber Camp** | **Lumber Camp** (1): unpowered, radius 6, speed 0.75. Model: new, today's `lumber_camp` with the building about half the size | **Lumber Mill** (2): 5 Power, radius 8, speed 1.3. Model: today's `lumber_camp` | **Advanced Lumber Mill** (3, new): 10 Power, radius 8, speed 1.6. Model: today's `lumber_mill` |
| **Water Pump** | **Water Pump** (1): unpowered, 4 Water. Model: new, today's `water_pump` with the building about half the size | **Pumping Station** (2): 5 Power, 10 Water. Model: new, today's `water_pump` with two pipes leaving the building, one at each end | **Advanced Pumping Station** (3, new): 10 Power, 16 Water. Model: new, like today's `pumping_station` with four pipes leaving one building edge and the door moved to the empty side |
| **Sand Collector** | **Sand Collector** (1): unpowered, 2 Sand. Model: new, today's `sand_collector` hopper at about half size | **Sand Sifter** (2, new): 5 Power, 4 Sand. Model: today's `sand_collector` | **Sand Dredge** (3): 10 Power, 8 Sand. Model: today's `sand_dredge`, its hopper block kept as the link to the levels below |
| **Oil Well** | **Oil Well** (2): unpowered, radius 3, speed 0.75. Model: today's `oil_well` | **Oil Rig** (2, new): 10 Power, speed 1.3. Model: new, a slightly simplified `deep_well` | **Deep Well** (3): 15 Power, speed 1.6, adds worked-out oil cells and Salt Domes around it. Model: today's `deep_well` |
| **Smelter** | **Smelter** (1): burns fuel; Iron, Copper. Model: today's `smelter` | **Steel Mill** (2): 20 Power; Iron, Copper, Steel. Model: new, the `smelter` block with the Steel Mill's glowing ladle and blue-roofed shed beside it and a power pole | **Electric Smelter** (2): 60 Power; adds Aluminum, Silicon, Titanium (the Electric Furnace's and Aluminum Smelter's recipes). Model: new, the `smelter` block with the Electric Furnace's round electrode furnace in place of the chimney, and the Aluminum Smelter's skylight band |
| **Kiln** | **Charcoal Kiln** (1): Charcoal. Model: today's `charcoal_kiln` (two domes and a log pile) | **Cement Kiln** (1): burns fuel; adds Cement. Model: new, the two domes with a short rotary kiln tube from `cement_kiln` alongside | **Tile Kiln** (3): 30 Power; adds Ceramic Tile. Model: new, one dome and the red tunnel kiln from `tile_kiln`, shortened to fit |

That makes 40 buildings: 11 removed by merging, and the Air Separator dropped.

- **Retired models:** `lumber_mill` moves to level 3. `steel_mill`, `electric_furnace`,
  `aluminum_smelter`, `cement_kiln`, `tile_kiln`, and `air_separator` are only parts sources for
  the new models.
- **Existing models refit to 4 x 4:**
  - Models now smaller than 4 x 4 (Mine, Drill Rig, Oil Well, Smelter, Charcoal Kiln, and the
    sand ones) stand on the larger plot, with their pad or yard grown to fill it.
  - Models that were bigger (Steel Mill, Aluminum Smelter, Cement and Tile Kilns) are already
    being redrawn above.
- **The Smelter's two tier 2 levels:** Aluminum, Silicon, and Titanium are tier 2 items, so level
  3 stays tier 2, with its own project after level 2's.
- **The Kiln's level 2 is tier 1:** Cement is a tier 1 item. Level 2 is the first one that burns
  fuel, since Charcoal needs none.
- **Speeds are per family as given.** For the providers (Water Pump, Sand Collector) the levels
  raise the supply. Today's top supplies stay at level 3.
- **Power and fuel:** a powered level of a fuel-burning family stops burning fuel. Smelter levels 2
  and 3 and Kiln level 3 run on Power alone. Steel's Coal or Charcoal is still an input.

### Not leveled

The Fabricator and Assembly Plant, the Refinery, Chemical Plant, and Fuel Plant, the Electronics
Works and Semiconductor Fab, the Electrolyzer, generators, stores, hubs, the Depot, the Research
Lab, the Glassworks, the Sawmill, the renewables, the Concrete Mixer, the Enrichment Plant, the
Fission Reactor, and the Shipyard.

## Deep levels

Today the Deep Mine and Deep Well stand **on** their site. The Deep Mine covers exactly one
worked-out map cell (4 x 4 fine cells) or a motherlode's center. The Deep Well covers a worked-out
oil cell or a Salt Dome. As level 3s they place like the rest of their family and work what is
**around** them.

- **Work area:** the map cells the footprint touches or borders, so a Deep Mine beside a
  worked-out Iron Ore cell works it. Four Deep Mines, one on each edge, all work the same cell.
  Extractors already share a field today, so this matches.
- **What a deep level works:**
  - everything its level 2 works, at its own speed;
  - worked-out cells and motherlodes in its work area, at today's deep rate (1 unit every 20 s
    for the Mine, 26.7 s for the Well);
  - a live field first, when one is in range.
- **No cap on stacking:** a worked-out cell is endless, so Deep Mines stacked around one give as
  much as you can power (30 Power each). That is the intent.
- **Motherlodes** work the same way, with several Deep Mines around one. The Deep Well's Salt Dome
  becomes a cell it works in range.

## Research

- **Level unlocks:** one project per leveled building (each level above 1), in that level's tier:
  - "Powered Mining" (tier 2), "Deep Mining" (tier 3);
  - "Powered Logging" (2), "Advanced Logging" (3);
  - "Powered Pumping" (2), "Advanced Pumping" (3);
  - "Sand Sifting" (2), "Dredging" (3);
  - "Oil Rigs" (2), "Deep Drilling" (3);
  - "Steelmaking" (2), "Electric Smelting" (2);
  - "Cement" (1), "Tile Firing" (3).
- **Process speed:** two steps per family, multiplying the family's speed at every level (on top
  of each level's own speed). The first is +25% in tier 2 and the second +50% in tier 3, the
  second needing the first. The first families:
  - Mine: "Better Mining I", "Better Mining II";
  - Lumber Camp: "Better Logging I", "Better Logging II";
  - Oil Well: "Better Drilling I", "Better Drilling II";
  - Smelter: "Better Smelting I", "Better Smelting II" (every Smelter recipe).

  More families can follow the same pattern later.
- **The research tree** fills every tier, including tier 4, which has no projects today.

### Research tree: one screen with tier sections

This replaces the tier picker from 2611261445-tier-pickers. The build menu and inventory keep
theirs.

- **Layout:** the whole tree on one scrolling canvas, laid out as bands left to right: Tier 1,
  Tier 2, Tier 3, Tier 4. Each band has a heading ("Tier 2") and a faint panel behind its cards.
  A project's `Column` and `Row` stay within its tier, so the tier decides the band.
- **Prerequisite lines cross bands,** so "Powered Mining" (Tier 2) visibly leads to "Deep Mining"
  (Tier 3).
- **A locked tier's band:** its cards are dimmed, and its heading reads "Tier 3: unlocked by
  Electronics research".
- **A completed tier** shows the green check after its heading, as the picker cell does now.
- **Where it opens:** scrolled to the selected project's band (the running project, else the first
  Available or Paused one).

## Saves

The level model changes saved buildings enough that old saves are not carried over.

- **Save version:** the save file gets a save version, starting at 1 with this change. A save
  without one is version 0.
  - Today's header already has a `FormatVersion` (1), which only refuses saves from a newer build.
  - Proposed: replace it with the new save version under a new field name, so every existing save
    reads as version 0.
- **Unsupported saves:** a save below the current version is listed on the load screen as not
  loadable ("Saved by an older version"), like a damaged or newer save today, and cannot be
  loaded.
- **Cleaning up:** when the load screen opens and finds any unsupported saves, a popup asks
  whether to delete them ("Delete 3 saves from an older version?", Delete or Keep).
- **The demo save** is rewritten at version 1.
- **No migration:** nothing maps a Drill Rig to a level 2 Mine. Old saves simply stop loading.

## Phases (each its own changeset)

1. **Leveled buildings:**
   - the level model (each level's definition, a building's level, and saving it);
   - the seven families at 4 x 4;
   - the level unlock projects;
   - placing at a level;
   - upgrading in place (paying the difference);
   - the build menu's level choice and the detail panel's Upgrade action;
   - dropping the Air Separator.
2. **Level models:** done (completed/2611261712-level-models.md); phase 1 switches to them and
   removes the models they replace.
3. **Deep levels:** the "around" work area (could fold into phase 1).
4. **Research tree sections:** done (completed/2611261650-research-tree-sections.md).
5. **Process speed research:** the speed model and the eight projects.
6. **Save version:** done (completed/2611261642-save-version.md). Phase 1 raises
   `SaveFormat.Version` to 2.

## Decisions

1. **Models and numbers** as in the Families table.
2. **Deep levels** work the cells **around** them, with no cap on how many share a cell.
3. **Level names** as in the table, each level with its own name.
4. **Getting a level:** build at any unlocked level, or upgrade in place by paying the difference.
5. **Level unlocks:** one research project per leveled building.
6. **Process speed:** two steps per family (+25%, +50%), starting with the Mine, Lumber Camp, Oil
   Well, and Smelter.
7. **Saves:** a new save version (1); older saves are not loadable, and the load screen offers to
   delete them.
