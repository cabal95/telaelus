# Production Tiers 1: Refining

- Status: Implemented
- Id: 2611151006-production-refining
- Depends-on: none

## Summary

The first of the production-tier changesets, which build the chain toward the first ship. This one
builds **tier 1, refining**: every refined resource the first ship needs, the buildings that make
them, and the two recipe controls they need. Processors can now hold **several recipes** (one chosen
at a time) and recipes can take **either of two inputs** (Coal or Charcoal). The Smelter gains
Copper; the Refinery makes Kerosene instead of Rocket Fuel; and six new buildings make Steel,
Hydrogen and Liquid Oxygen, Rocket Fuel, Plastic, Rubber, Carbon Fiber, Aluminum, Silicon, and
Titanium. The launch adds Liquid Oxygen. The parts (tiers 2 to 4) and the full first ship follow in
the next changeset.

## Motivation

The ninth follow-on of 2611141757-resource-tree (section 10 there, "production tiers"). The economy
around production is in place (storage, the network, power, Water and Sand, trucks, larger fields,
renewables), but the only recipes are Iron, Rocket Fuel, Glass, and Charcoal, and the launch still
takes 500 Rocket Fuel and 100 Iron. The plan's first ship needs 24 more items in four tiers and
about a dozen buildings; that is too much for one reviewable changeset, so this proposes three
(see the open questions), in chain order:

1. **Refining (this one):** the tier-1 materials the first ship needs, the recipe controls, and the
   buildings that make them.
2. **First ship parts:** the tier-2 to tier-4 parts of the first ship (Hull Plate, Steel Beam,
   Copper Wire, Circuit, Machine Parts, Ceramic Tile, Aluminum Frame, Composite Panel, Control
   Module, Thruster Assembly, Fuel Tank, Hull Section, Engine), assembly buildings, and the launch
   built from parts.
3. **The later tiers:** the rest of the catalog (Lumber, Cement, Concrete, Sulfuric Acid, Insulated
   Cable, Silicon Wafer, Processor, Magnet, Electric Motor, Battery, Solar Cell, Armor Plate,
   Computer, Heat Shield, Life Support Unit, Sensor Array, Lithium's Brine Well, the Air Separator,
   Enriched Uranium and Fuel Rods), with what Part B needs deciding the order.

## Design

### Scope

- **In:** multi-recipe processors with a Recipe picker; alternative inputs with an Auto picker;
  the tier-1 recipes below; the Smelter's Copper and the Refinery's Kerosene; six new buildings and
  their models; the launch's Liquid Oxygen.
- **Out:** everything in changesets 2 and 3 above; Sulfuric Acid (it feeds only later-tier items);
  the Air Separator (the Electrolyzer's oxygen covers the first ship).

### Recipe controls

- **Several recipes:** a processor may have more than one recipe and runs **one at a time**, the
  first by default. Its panel has a **Recipe** picker. A change takes effect when the building is
  between cycles: a running cycle finishes first (its output is made). The input buffer keeps what
  it holds; units the new recipe does not use go back to storage when the building is online
  (offline, they wait in the buffer until it is, and a destroy refunds them).
- **Alternative inputs:** a recipe input may be "Coal or Charcoal". The panel has an **input
  picker** for it, defaulting to **Auto** (whichever storage holds more of, switching when that
  changes), with a dim note naming the choice ("Auto: uses Coal (Coal 120, Charcoal 40)"). Choosing
  one by name uses only it ("Waiting for Charcoal" when it runs out). This is the plan's picker
  (resource-tree section 1), with equal quantities, so Auto needs no weighing.

### The recipes (placeholders)

| Item | Inputs | Building | Cycle | Storage |
|---|---|---|---|---|
| Copper | 1 Copper Ore | Smelter (new recipe; burns fuel) | 16 s | Cargo |
| Kerosene | 1 Crude Oil | Refinery (replaces its Rocket Fuel; draws Power and Water as now) | 8 s | Tank Farm |
| Steel | 2 Iron, 1 Coal or Charcoal, 1 Limestone | Steel Mill | 20 s | Cargo |
| Hydrogen | none (Water capacity) | Electrolyzer, Hydrogen recipe | 6 s | Tank Farm |
| Liquid Oxygen | none (Water capacity) | Electrolyzer, Oxygen recipe | 6 s | Tank Farm |
| Rocket Fuel | 1 Kerosene, 1 Hydrogen | Fuel Plant | 8 s | Tank Farm |
| Plastic | 2 Crude Oil | Chemical Plant, Plastic recipe | 10 s | Cargo |
| Rubber | 1 Crude Oil, 1 Sulfur | Chemical Plant, Rubber recipe | 12 s | Cargo |
| Carbon Fiber | 2 Plastic | Chemical Plant, Carbon Fiber recipe | 15 s | Cargo |
| Aluminum | 2 Bauxite | Aluminum Smelter | 20 s | Cargo |
| Silicon | 1 Quartz, 1 Coal or Charcoal | Electric Furnace, Silicon recipe | 15 s | Cargo |
| Titanium | 2 Titanium Ore | Electric Furnace, Titanium recipe | 20 s | Cargo |

- Rocket Fuel keeps its name and storage but is now made in two steps (Kerosene, then the Fuel
  Plant with Hydrogen), as the plan's rocket fuel chain sets out. Existing Rocket Fuel in storage
  stays.
- Every recipe has one output (the plan's rule). Processors keep today's buffers: an output buffer
  of 10 and 5 cycles of each input.

### The buildings (placeholders)

| Building | Footprint | Cost | Tier | Draws | Recipes |
|---|---|---|---|---|---|
| Smelter (changed) | as now | as now | 0 | | Iron, Copper |
| Refinery (changed) | as now | as now | 1 | 25 Power, 2 Water | Kerosene |
| Steel Mill | 4 x 6 | 60 Stone, 40 Iron | 1 | 20 Power | Steel |
| Electrolyzer | 4 x 5 | 40 Iron, 20 Copper | 1 | 50 Power, 4 Water | Hydrogen, Liquid Oxygen |
| Fuel Plant | 4 x 5 | 40 Steel, 20 Copper | 1 | 20 Power | Rocket Fuel |
| Chemical Plant | 5 x 6 | 40 Steel, 20 Copper | 1 | 25 Power, 2 Water | Plastic, Rubber, Carbon Fiber |
| Aluminum Smelter | 3 x 8 | 50 Steel, 30 Stone | 1 | 60 Power | Aluminum |
| Electric Furnace | 4 x 4 | 50 Steel, 20 Copper | 1 | 40 Power | Silicon, Titanium |

- **Sizes:** tier-1 industry is bigger than the tier-0 start (2 x 2 to 3 x 3), and most of it is
  long rather than square, like the Glassworks (3 x 6), so each reads by its shape: the Aluminum
  Smelter's pot line is the longest and thinnest (3 x 8), the Steel Mill and Chemical Plant are the
  largest halls, and only the round arc furnace stays square. A footprint is width by depth facing
  North, and R turns it, so a long building fits along a row of others or across it.
- **Order of play:** Copper from the Smelter and Iron make the Electrolyzer; the Steel Mill needs
  only Stone and Iron; everything after it costs Steel. So Steel is the gate into the rest of tier 1,
  and the heavy-power buildings (the Electrolyzer, Aluminum Smelter, Electric Furnace) push the
  player to scale power, as the plan intends ("infinite inputs, real cost").
- The Electrolyzer draws Water and Power whether it makes Hydrogen or Liquid Oxygen; one per gas is
  the simple setup.

### The launch

- Today: 500 Rocket Fuel and 100 Iron. This changeset adds **200 Liquid Oxygen**, so the oxidizer
  is part of the launch from now on. Changeset 2 replaces the Iron with the ship's parts (Hull
  Section, Engine, Control Module).

### UI

- **Building panel:** a **Recipe** picker for a building with more than one recipe (the Recipe row
  shows the running one, adding "(next: Copper)" while a change waits), and an input picker for an
  alternative input, labeled with it ("Coal or Charcoal"), with its dim note. The Recipe row names
  an alternative input as all of them ("2 Iron + 1 Coal or Charcoal + 1 Limestone to 1 Steel,
  20 s"), and the Buffer row lists the inputs as resolved.
- **Build menu and placement** are unchanged; the new buildings appear like any other.
- **Top bar:** the launch progress shows Liquid Oxygen as a third entry ("0 / 200").

### Assets

Six building models with accent colors, kept preview sheets, and README rows (every item already
has its model and icon from the resource catalog):

- **Steel Mill (4 x 6):** a tall brick hall running back from the front, a converter vessel
  tipping at the back end under a chimney, a casting bay along one side, and steel billets stacked
  at the front (`steel_accent`, a steel blue), with a power pole.
- **Electrolyzer (4 x 5):** two rows of tall cells down its length with electrodes on top, a
  rectifier house at the back, and pipe outlets to two gas tanks at the front, one white (oxygen)
  and one pale (hydrogen) (`gas_accent`, a pale aqua), with a power pole.
- **Fuel Plant (4 x 5):** two process columns at the back joined by a pipe rack, a blending tank
  with a red band in the middle, and a loading arm over a pad at the front (`fuel_accent`, a rocket
  red-orange), with a power pole.
- **Chemical Plant (5 x 6):** the largest: a reactor vessel and a tall distillation tower at the
  back, a tangle of pipes on a rack across the middle, a control house, and drums at the front
  (`chemical_accent`, a chemical violet), with a power pole.
- **Aluminum Smelter (3 x 8):** a long, narrow pot line under a low saw-tooth roof with fat bus
  bars along its side, a fume duct along the ridge, and ingots stacked at the front end
  (`aluminum_accent`, a pale silver-blue), with a power pole.
- **Electric Furnace (4 x 4):** a squat round arc furnace in the middle with three graphite
  electrodes rising through its lid, a transformer box at the back, and a glowing tap at the
  front (`furnace_accent`, an electric blue), with a power pole.

## Code changes

- `Recipe`: alternative inputs (a list of types per input, with equal amounts).
- `Recipes`: a list of recipes per building type; the new recipes; the Refinery's Kerosene.
- `Building`: the chosen recipe index and each alternative input's choice (null for Auto).
- `Simulation`: processors use the chosen recipe; `SetRecipe` (taking effect between cycles,
  returning unused inputs), `SetInputChoice`, Auto's rule; inputs available and consumed by the
  chosen alternative; status reasons naming the alternative ("Waiting for Coal or Charcoal").
- `BuildingType` and `BuildingDefs`: the six buildings.
- The launch: `LaunchLiquidOxygenCost` 200.
- Presentation: the Recipe and input pickers and their notes; the top bar's third launch entry;
  model file names.
- Testing: snapshot `recipe` per building and `ui.recipe` and `ui.input`; `RefiningTests`; a
  `refining` scenario; the schema's building list; the build menu's cells (its undiscovered-cell
  check now uses the first undiscovered cell, as the grid scrolls); existing tests updated for
  Kerosene; the end-to-end launch test rebuilt on the empty map with placed fields, since the
  refining chain's big buildings and their pumps do not fit round the test map's fields.
- Assets: six models, previews, README rows, the new palette entries.

## Blueprint updates

- **buildings.md:** multi-recipe processors, alternative inputs, the new buildings and recipes,
  the Refinery's change.
- **resources.md:** where each tier-1 item comes from; the rocket fuel chain.
- **presentation.md / ui-style.md:** the pickers and the launch entry.
- **art-style.md:** the new accents and models.
- **testing.md:** new tests and scenario.

## Verification

- Unit tests: each recipe's inputs, output, and cycle; a Smelter switching from Iron to Copper
  between cycles, returning its unused Iron Ore; a Steel Mill on Auto taking whichever of Coal and
  Charcoal storage holds more of, and only Charcoal when set; an Electrolyzer switching gas; the
  Fuel Plant making Rocket Fuel from Kerosene and Hydrogen; the Electric Furnace's two recipes; the
  heavy draws (an Aluminum Smelter on too little power in a brownout); the launch needing Liquid
  Oxygen; an end-to-end test from fields to a launch through the new chain.
- Scenario: a `refining` scenario builds a Steel Mill and switches its picker, an Electrolyzer on
  each recipe, and a Fuel Plant, and checks the panels and the launch entry.
- Both test suites pass; a windowed run shows the six models.

## Decisions

1. **Three changesets:** refining (this one), then the first ship's parts, then the later tiers.
2. **The launch adds 200 Liquid Oxygen** in this changeset.
3. **Switching recipes** finishes the running cycle first and returns unused inputs to storage.
4. **Steel is the gate:** the Steel Mill is built from Stone and Iron, and the Fuel Plant, Chemical
   Plant, Aluminum Smelter, and Electric Furnace from Steel.
5. **Bigger, mostly long footprints:** Steel Mill 4 x 6, Electrolyzer 4 x 5, Fuel Plant 4 x 5,
   Chemical Plant 5 x 6, Aluminum Smelter 3 x 8, Electric Furnace 4 x 4.
