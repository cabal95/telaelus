# Production Tiers 2: First Ship Parts

- Status: Implemented
- Id: 2611151033-first-ship-parts
- Depends-on: 2611151006-production-refining

## Summary

The second production-tier changeset: the parts of the first ship and the buildings that make them,
so the launch is a ship built from parts instead of a pile of Iron. Tier 2 forms materials into
components (Hull Plate, Steel Beam, Aluminum Frame, Copper Wire, Circuit, Machine Parts, Composite
Panel, Ceramic Tile) in three buildings grouped by process (a general **Fabricator**, an
**Electronics Works**, a **Tile Kiln**); tier 3 assembles them in the **Assembly Plant**; tier 4, the
**Shipyard**, builds Hull Sections and the Engine and launches the ship from its pad. Every building
with a choice of output shows what it makes on a **display stand** at its front.

## Motivation

The second of the three changesets 2611151006-production-refining set out (decision 1 there), and
the plan's first ship (2611141757-resource-tree, section 3, "First ship"): Hull Section (Hull
Plate, Steel Beam), Engine (Thruster Assembly, Fuel Tank, Machine Parts), Control Module, Rocket
Fuel, Liquid Oxygen. Refining now makes every tier-1 material the ship needs; nothing turns them
into parts yet, and the launch still asks for 100 Iron as a stand-in for a ship.

## Design

### Scope

- **In:** the 13 part recipes below; five buildings and their models; the display stand on every
  building with a choice of output; the launch from the Shipyard with the new ship bill; the top
  bar's launch progress for it.
- **Out:** the later tiers (changeset 3: Insulated Cable, Silicon Wafer, Processor, Magnet,
  Electric Motor, Battery, Solar Cell, Armor Plate, Computer, Heat Shield, Life Support Unit,
  Sensor Array, Lumber, Cement, Concrete, and the rest); building costs in parts beyond the few
  below; a launch animation (the ship simply leaves the pad's model; see the open questions).

### The recipes (placeholders)

Every recipe has one output (it may make more than one unit).

| Tier | Item | Inputs | Makes | Building | Cycle |
|---|---|---|---|---|---|
| 2 | Hull Plate | 2 Steel | 1 | Fabricator | 12 s |
| 2 | Steel Beam | 3 Steel | 1 | Fabricator | 15 s |
| 2 | Aluminum Frame | 2 Aluminum | 1 | Fabricator | 12 s |
| 2 | Copper Wire | 1 Copper | 2 | Electronics Works | 6 s |
| 2 | Circuit | 2 Copper Wire, 1 Plastic | 1 | Electronics Works | 10 s |
| 2 | Machine Parts | 2 Steel, 1 Rubber | 1 | Fabricator | 15 s |
| 2 | Composite Panel | 2 Carbon Fiber, 1 Aluminum | 1 | Fabricator | 20 s |
| 2 | Ceramic Tile | 1 Silicon, 1 Aluminum | 2 | Tile Kiln | 15 s |
| 3 | Control Module | 4 Circuit, 2 Hull Plate, 2 Glass | 1 | Assembly Plant | 40 s |
| 3 | Thruster Assembly | 4 Titanium, 2 Machine Parts, 6 Ceramic Tile | 1 | Assembly Plant | 45 s |
| 3 | Fuel Tank | 4 Aluminum Frame, 4 Composite Panel | 1 | Assembly Plant | 40 s |
| 4 | Hull Section | 4 Hull Plate, 2 Steel Beam | 1 | Shipyard | 60 s |
| 4 | Engine | 2 Thruster Assembly, 2 Fuel Tank, 4 Machine Parts | 1 | Shipyard | 90 s |

All parts are Cargo (their weights are already set in the catalog: a Hull Section 20, an Engine
25, a Control Module 4, and so on), so a ship's parts need Warehouses beside the HQ's pool.

### The buildings (placeholders)

| Building | Footprint | Cost | Tier | Draws | Recipes |
|---|---|---|---|---|---|
| Fabricator | 4 x 6 | 40 Steel, 20 Copper | 2 | 30 Power | Hull Plate, Steel Beam, Aluminum Frame, Machine Parts, Composite Panel |
| Electronics Works | 4 x 5 | 30 Steel, 30 Copper, 10 Glass | 2 | 20 Power, 2 Water | Copper Wire, Circuit |
| Tile Kiln | 3 x 7 | 40 Stone, 20 Steel | 2 | 30 Power | Ceramic Tile |
| Assembly Plant | 6 x 8 | 20 Steel Beam, 20 Hull Plate, 10 Circuit | 3 | 40 Power | Control Module, Thruster Assembly, Fuel Tank |
| Shipyard | 8 x 12 | 40 Steel Beam, 20 Hull Plate, 10 Circuit | 4 | 50 Power | Hull Section, Engine; launches the ship |

- Processors as in refining: one recipe at a time behind the Recipe picker, an output buffer of 10
  units, and 5 cycles of each input. The Shipyard's buffer holds 2 (its parts are large).
- **Each tier is built from the one before:** tier-2 buildings from Steel and Copper, the Assembly
  Plant and Shipyard from tier-2 parts, so every step opens the next.
- **Grouped by process, not by part:** the Fabricator forms and machines metal and composites, the
  Electronics Works makes wire and boards, and the Tile Kiln fires ceramics. Each looks like its
  process, and each has room for the later tiers' parts as new recipes (Insulated Cable, Silicon
  Wafer, and Processor in the Electronics Works; Armor Plate in the Fabricator; and so on), so
  changeset 3 needs few new buildings. The Assembly Plant is already the general assembler for
  tier 3.
- The Tile Kiln is long like the Glassworks (a tunnel kiln); the Shipyard is the largest building,
  a fabrication hall beside a launch pad.

### The launch

- **The ship bill:** 4 Hull Sections, 1 Engine, 1 Control Module, 500 Rocket Fuel, and 200 Liquid
  Oxygen (replacing the 100 Iron). In raw terms that is roughly 150 Iron Ore, 75 Coal or Charcoal,
  75 Limestone, 60 Bauxite, 16 Titanium Ore, 6 Quartz, 8 Sulfur, about 600 Crude Oil with the fuel,
  and a lot of Water and Power.
- **It launches from a Shipyard:** the launch needs at least one switched-on, online Shipyard. L
  launches as today (the top bar's "[L] Launch ship"), consuming the bill, and the ship lifts off
  the Shipyard's pad. With the bill met and no Shipyard, the top bar says "Build a Shipyard to
  launch".

### UI

- **Top bar:** the launch progress lists the ship bill: Hull Section, Engine, Control Module,
  Rocket Fuel, and Liquid Oxygen, each its icon and "have / need" (short in the short color), then
  "[L] Launch ship" or "Build a Shipyard to launch". It replaces the Rocket Fuel, Liquid Oxygen,
  and Iron entries.
- **Building panels** need nothing new: the Recipe picker and rows from refining cover them.
- **The display stand:** every building with a choice of output shows its current output's item
  model on a small stand at its front edge, turning slowly, so a row of Fabricators reads at a
  glance (this one makes Hull Plate, that one Machine Parts). It covers every processor with more
  than one recipe (the Smelter, Electrolyzer, Chemical Plant, Electric Furnace, Fabricator,
  Electronics Works, Assembly Plant, and Shipyard) and every collector with an Output picker (the
  Quarry, and a Deep Mine or Deep Well on a motherlode). It shows the running recipe's output, or
  the waiting one's once a change is chosen, and nothing while switched off.

### Assets

Five building models and the display stand, with accent colors, kept preview sheets, and README
rows (every part already has its item model and icon):

- **Fabricator (4 x 6):** a high hall with a hydraulic press frame at the back, a roller line and a
  gantry crane over an open bay down one side, and plates, beams, and crates of parts at the front
  (`fabricator_accent`, a machine orange).
- **Electronics Works (4 x 5):** a clean low building with a saw-tooth glass roof, a clean-room
  annex, and wire spools at the front (`electronics_accent`, a circuit green).
- **Tile Kiln (3 x 7):** a long brick tunnel kiln with a chimney at the back end, a car track
  running its length, and stacks of white tiles at the front (`kiln_accent`, shared with the
  Charcoal Kiln).
- **Assembly Plant (6 x 8):** a large assembly hall with a tall bay door at the front, an overhead
  crane rail on the roof, and a finished thruster on a stand outside (`assembly_accent`, a deep
  blue).
- **Shipyard (8 x 12):** a fabrication hall along the back half, and a launch pad on the front half
  with a service gantry beside it; the ship stands on the pad, shown as parts arrive (hull rings
  stacking up, the engine below) and gone after a launch (`shipyard_accent`, a launch white with
  `power_accent`).
- **Display stand:** a short post with a round plinth, about 0.6 high, in `metal_dark` with a thin
  `power_accent` rim; the game sets the item model on it (about 0.6 across) and turns it. It stands
  at the middle of the building's front edge, just inside its footprint, so it turns with the
  building.

## Code changes

- `Recipes`: the 13 recipes and the five buildings' lists; recipes that make more than one unit.
- `BuildingType` and `BuildingDefs`: the five buildings (the Shipyard's smaller buffer).
- The launch: the ship bill as a list (`LaunchCost`), `CanLaunchShip` needing an online,
  switched-on Shipyard, and a reason when it cannot ("Build a Shipyard to launch").
- Presentation: the display stands (a view per building with a choice of output, placed at its
  front edge by its facing, its item swapped when the output changes); the top bar's launch entries
  from the bill; the Shipyard's ship model shown by how
  much of the bill is in storage; model file names.
- Testing: tests; a `first-ship` scenario; the schema's building list; the build menu's cells; the
  launch tests and the end-to-end test rebuilt for the ship bill (seeding the parts or running the
  chain, see verification).
- Assets: five building models (the Shipyard with its ship as separate parts) and the display
  stand, previews, README rows, the new palette entries.
- Testing also: the snapshot's display stand per building (the item it shows, or null).

## Blueprint updates

- **buildings.md:** the five buildings, their recipes, the launch from the Shipyard.
- **resources.md:** where each part comes from; the ship bill.
- **presentation.md / ui-style.md:** the launch progress and its Shipyard note; the display stands.
- **art-style.md:** the new accents and models.
- **testing.md:** new tests and scenario.

## Verification

- Unit tests: each recipe's inputs, output, amount, and cycle; the Fabricator's five recipes; a recipe making 2 units; each
  building's recipe list; the Assembly Plant switching between its three parts; the launch needing
  the full bill and an online Shipyard; the end-to-end launch with the parts chain (seeded with the
  tier-1 materials, so it tests the parts chain and the launch rather than repeating refining's).
- Scenario: a `first-ship` scenario seeds Steel, builds a Fabricator and sees a Hull Plate made,
  checks the Fabricator's display stand show its part and change with its recipe, then seeds the
  bill and launches from a Shipyard, checking the top bar before and after. (The Assembly Plant
  is covered by the unit tests' end-to-end run rather than the scenario.)
- Both test suites pass; a windowed run shows the five models, display stands, and the ship on the
  pad.

## Decisions

1. **Buildings grouped by process:** a general Fabricator (metal and composite parts), an
   Electronics Works (wire and circuits), and a Tile Kiln (ceramics) for tier 2; the Assembly Plant
   for tier 3; the Shipyard for tier 4. (A single Fabricator for every tier-2 part was considered;
   it would read poorly in a base and lose the kiln's and electronics' own looks.)
2. **A display stand** shows the current output on every building with a choice of output.
3. **The ship bill:** 4 Hull Sections, 1 Engine, 1 Control Module, 500 Rocket Fuel, 200 Liquid Oxygen.
4. **The ship launches from an online Shipyard,** growing on its pad as its parts arrive.
5. **The Assembly Plant and Shipyard are built from parts** (Steel Beams, Hull Plates, Circuits).

## Implementation notes

- **A `setRecipe` scenario command** (`{ anchor, index }`, backed by `Simulation.Debug.SetRecipe`)
  so the scenario can change the Fabricator's recipe without driving the picker.
- **The top bar hides the bill entries once the bill is met,** leaving "[L] Launch ship" or "Build a
  Shipyard to launch"; five entries plus the note did not fit at 1280 wide. The right-hand entries
  now sit `Gap` (8) apart instead of `Padding`.
- **The display stand** stands 0.4 inside the middle of the front edge; the snapshot reports what
  it shows per building (`display`).
- **The ship on the pad** shows on every Shipyard from what storage holds: the engine, then up to
  four hull rings stacked on it (on the pad without it), then the nose once there is a Control
  Module. It is a separate prop set (`ship_engine`, `ship_hull_ring`, `ship_nose`), not part of
  the Shipyard model.
