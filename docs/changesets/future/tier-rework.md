# Tier Rework (plan)

- Status: Future (a working plan; it splits into changesets as each phase is agreed)

## Goal

Everything the player progresses through is grouped into the same four tiers: resources and
items, buildings, and research. Unlocking a tier makes its buildings buildable at once and its
items visible; the next tier is unlocked by research. A tier N building makes tier N items, so
"Tier 2" means the same stage of the game everywhere.

From the request (TODO.md): building tiers replace the "hold a building's whole cost once"
unlock; raw resources show grayed until first found; items show grayed until first made;
discovery notifications stay; building unlock notifications become one "Tier 2 buildings are now
available"; building tier unlocks come from research, one project per tier (2 to 4); the build
menu (and inventory) get a segmented tier picker; buildings are later merged into quality levels
or given research speed boosts.

## Today

- **Resource tiers** (resources.md): 0 raw (21 kinds); 1 refined (20: Iron, Copper, Steel, Glass,
  Lumber, Charcoal, Cement, Concrete, Kerosene, Plastic, Hydrogen, Liquid Oxygen, Aluminum,
  Silicon, Sulfuric Acid, Rubber, Titanium, Carbon Fiber, Enriched Uranium, Rocket Fuel); 2
  components (16); 3 assemblies (8); 4 ship parts (Hull Section, Engine).
- **Building tiers** (`BuildingDef.Tier`, buildings.md): 0 to 4, meaning roughly "unpowered" (0)
  and "powered" (1) and up, but not tied to what a building makes: tier 1 refined items come from
  tier 0 buildings (Smelter, Glassworks, Charcoal Kiln, Sawmill, Cement Kiln) and tier 1 buildings
  (Steel Mill, Refinery, Chemical Plant, ...) alike, and the tier does nothing in play.
- **Unlocking:** a building unlocks the first time storage holds its whole cost at once, with a
  "New building" notification. At the start nothing is unlocked.
- **Research:** two projects (Material Transfer, Advanced Material Transfer); a project has a
  fee, a time, and prerequisites. Research Labs speed it.
- **Inventory:** discovered rows first, then "Undiscovered" rows (a darkened icon, no name).

## Proposal

### The four tiers

Resource tiers shift so that resource tier N is what tier N buildings make (raw stays tier 0, the
world's). Today's tiers 1 to 4 become 1 to 4 below; the main change is splitting today's large
tier 1 into an unpowered tier 1 and a powered tier 2, and merging today's tiers 3 and 4.

| Tier | Internal name | Items (made here) | Buildings |
|---|---|---|---|
| 1 | Settlement | Iron, Copper, Glass, Charcoal, Lumber, Cement | Starters: Mine, Lumber Camp, Smelter, Glassworks, Charcoal Kiln, Sawmill, Cement Kiln; power and capacity: Water Wheel, Burner Generator, Water Pump, Sand Collector; logistics: Depot, Silo, Stockyard, Warehouse, Small Hub, Relay, roads and bridges; Research Lab |
| 2 | Industry | Steel, Concrete, Kerosene, Plastic, Rubber, Carbon Fiber, Sulfuric Acid, Hydrogen, Liquid Oxygen, Rocket Fuel, Aluminum, Silicon, Titanium | Gateways (built from tier 1): Steel Mill, Refinery, Oil Well, Drill Rig; then Tank Farm, Vault, Lumber Mill, Pumping Station, Sand Dredge, Rock Crusher, Quarry, Forester, Deep Well, Concrete Mixer, Chemical Plant, Electrolyzer, Fuel Plant, Aluminum Smelter, Electric Furnace, Brine Well |
| 3 | Components | Hull Plate, Steel Beam, Copper Wire, Circuit, Insulated Cable, Machine Parts, Silicon Wafer, Processor, Magnet, Electric Motor, Battery, Solar Cell, Composite Panel, Ceramic Tile, Aluminum Frame, Armor Plate | Gateways (built from tier 2): Fabricator, Electronics Works; then Tile Kiln, Semiconductor Fab, Air Separator, Deep Mine, Solar Array, Battery Bank |
| 4 | Assembly | Control Module, Computer, Thruster Assembly, Fuel Tank, Heat Shield, Fuel Rod, Life Support Unit, Sensor Array, Hull Section, Engine, Enriched Uranium | Gateway: Assembly Plant (built from tier 3); then Enrichment Plant, Fission Reactor, Shipyard |

Counts: 18 buildings in tier 1 (plus roads and bridges), 20 in tier 2, 8 in tier 3, 4 in tier 4.

- **Costs:** a tier's gateways are built from the tier below. Other costs mostly stay as they
  are; a few move to Steel where it clearly fits (see "Building costs" below).
- **Names:** the player only ever sees "Tier 1" to "Tier 4". The names in the table are for
  docs and code; tiers 3 and 4 will grow features that outgrow them.
- **Odd fits:** Enriched Uranium is a refined material but only feeds the Fuel Rod, so it sits in
  tier 4 with its maker, the Enrichment Plant, which stays one building. The Electrolyzer and Air
  Separator both make Liquid Oxygen at tiers 2 and 3 (the Air Separator as the better tier 3
  source, no water).
  Lithium stays raw (terrain-sourced), though only a tier 2 building (the Brine Well) reaches it.

### Unlocking tiers

- Tier 1 is unlocked from the start: every tier 1 building is buildable as soon as it is
  affordable. The "hold the whole cost once" rule goes.
- Tiers 2, 3, and 4 unlock by research: one project each, named for what drives the tier it
  opens ("Steel Works", "Electronics", "Computers"), each belonging to the tier before and paid in
  that tier's items. Completing one raises "Tier 2 buildings are now available" (lasting) in place
  of the per-building notification.
- **Research has tiers too.** A project belongs to a tier and can start once that tier is
  unlocked: Material Transfer tier 1, Advanced Material Transfer tier 2, each tier's unlock
  project in the tier below it. Later projects (building quality levels) slot into tiers the
  same way.

#### Tier unlock projects

For scale: one Smelter on Coal makes about 5 Iron a minute; Material Transfer costs 40 Iron and
20 Copper over 20 minutes, and Advanced Material Transfer 60 Steel and 20 Glass over 30. The
fees are modest for now because these projects will later gain prerequisites of their own.
Times are before Research Lab speedups.

| Project | Tier (needs) | Unlocks | Fee | Time |
|---|---|---|---|---|
| Steel Works | 1 | Tier 2 | 60 Iron, 60 Copper, 40 Glass, 80 Wood | 20 min |
| Electronics | 2 (Steel Works) | Tier 3 | 60 Steel, 60 Plastic, 40 Aluminum, 120 Wood | 30 min |
| Computers | 3 (Electronics) | Tier 4 | 60 Circuit, 20 Electric Motor, 40 Steel, 200 Wood | 45 min |

### What the player sees

- **Raw resources** (tier 0) are always listed, grayed (icon and name, "not yet found") until
  first discovered, so the player knows Coal exists before finding it. Today they are nameless.
- **Items** of a locked tier are not listed. Once their tier unlocks they show grayed until first
  made, then as now. Discovery notifications stay.
- **Build menu:** a segmented tier picker ("Tier 1" to "Tier 4", ui-style.md) across the top; a
  locked tier's cell is dim with a tooltip naming the research that unlocks it; the menu opens on
  the highest unlocked tier, or the last one viewed.
- **Inventory:** a new **stepper** control filters its rows: a left arrow, the current choice
  ("Resources" for raw, then "Tier 1" to "Tier 4"), and a right arrow. The current choice is drawn
  like a segmented picker's chosen cell, fixed to the width of the longest choice so the arrows
  never move; shorter choices are centered in it. The inventory reopens on the last choice viewed; that
  memory lasts for the session and is not saved. Proposed: the arrows stop at the ends (dimmed
  there, no wrap), and locked tiers are skipped, since their items are not listed. The stepper joins the UI patterns (ui-style.md)
  as a general control for stepping through a short ordered list in little space.
- **Research tree:** the same segmented tier picker as the build menu ("Tier 1" to "Tier 4"),
  locked tiers dim. A tier whose projects are all complete shows a green check mark after its
  name in the picker.

### Building quality levels (after the tiers land)

With buildings tiered, look at merging pairs that do the same job at two tiers into one building
with quality levels, unlocked by research in the higher tier: Mine and Drill Rig, Lumber Camp and
Lumber Mill, Water Pump and Pumping Station, Sand Collector and Sand Dredge, Oil Well and Deep
Well, Smelter and Electric Furnace. The alternative keeps the buildings and gives each quality
levels that only speed it up (25%, then 50%). This is its own design pass and changeset.

## Phases (each its own changeset)

1. **Tier model** (done: completed/2611261421-tier-model.md): resource tiers re-cut, building tiers 1 to 4 per the table, costs adjusted,
   tier 1 unlocked at start, tiers unlocked through research projects, research projects tiered,
   the tier unlocked notification; saves and the demo town migrate (an old save's tiers come from
   its research only; buildings it already has in a locked tier keep working, but no more can be
   placed). (Sim plus blueprints; the UI
   shows what it can without the pickers.)
2. **Visibility and pickers** (done: completed/2611261445-tier-pickers.md): raw resources grayed until found, items hidden until their tier
   then grayed until made; the build menu and inventory tier pickers; the research tree by tier.
3. **Quality levels:** the merge or speed-boost design, then its build (planned in
   building-levels.md).

## Decisions

1. Resource tiers re-cut as proposed; one or two items may still move.
2. Building table as proposed.
3. The player sees "Tier 1" to "Tier 4"; the names are internal only.
4. One research project per tier, named for what drives the tier: Steel Works, Electronics,
   Computers.
5. Enriched Uranium is tier 4; the Enrichment Plant stays one building.
6. The inventory gets a new stepper control (left arrow, current choice, right arrow) over
   "Resources" and "Tier 1" to "Tier 4", remembering the last choice for the session.
7. Old saves unlock tiers from research only; placed buildings in a locked tier stay.

## Building costs

Most costs stay as they are. Changes where Steel clearly fits:

| Building | Tier | Today | Proposed | Why |
|---|---|---|---|---|
| Deep Well | 2 | 60 Iron, 30 Stone | 40 Steel, 30 Stone | Deep drilling; the upgrade of the Oil Well |
| Deep Mine | 3 | 80 Iron, 40 Stone | 60 Steel, 30 Concrete | Tier 3 should cost tier 2 items; today it is Iron and Stone only |
| Rock Crusher | 2 | 40 Iron, 20 Stone | 30 Iron, 10 Steel, 20 Stone | Crushing jaws |
| Pumping Station | 2 | 40 Iron, 20 Stone | 30 Iron, 10 Steel, 20 Stone | The higher tier Water Pump |
| Sand Dredge | 2 | 40 Iron, 20 Stone | 30 Iron, 10 Steel, 20 Stone | The higher tier Sand Collector |

Kept on Iron on purpose: the gateways (Steel Mill, Refinery, Oil Well, Drill Rig), which must be
buildable from tier 1, and the Tank Farm, which Crude Oil needs, so oil does not wait on Steel.

## Still open

- The building cost changes above.
