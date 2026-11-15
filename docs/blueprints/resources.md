# Resources Blueprint

## Overview

Resources are the materials the factory loop produces and consumes. Raw resources come from
resource fields on the map (by hand or by extractor); refined resources come from buildings (see
buildings.md). A single global stockpile holds all amounts, within storage limits.

## Current state

### Resource types

- `ResourceType` has 63 values, the catalog planned in 2611141757-resource-tree, ordered by tier
  and then catalog order. `ResourceDefs.For(type)` gives each a display name, a `Kind`, a **tier**
  (0 for raw, 1 to 4 for made goods; `ResourceDefs.MaxTier` is 4), whether the player can harvest
  it by hand, and for a deposit, its gather time (`GatherSeconds`: the seconds to gather one unit
  by hand, which reads as how hard the material is to work; an extractor divides it by its speed,
  see buildings.md); rules read this table, not the enum:

  | Type | Kind | Hand-harvest | Gather time | Source |
  |---|---|---|---|---|
  | Wood | Deposit | yes | 3 s | Wood fields (trees) |
  | Stone | Deposit | yes | 4.5 s | Stone fields (boulders) |
  | IronOre | Deposit | yes | 6 s | Iron Ore fields |
  | CopperOre | Deposit | yes | 6 s | Copper Ore fields |
  | Coal | Deposit | yes | 4.5 s | Coal fields |
  | CrudeOil | Deposit | no | 3 s | Crude Oil fields (oil seeps), by an Oil Well only |
  | Bauxite, Quartz | Deposit | yes | 6 s | no fields yet |
  | Limestone, Sulfur | Deposit | yes | 4.5 s | no fields yet |
  | TitaniumOre | Deposit | yes | 7.5 s | no fields yet |
  | RareEarths, UraniumOre, Crystals | Deposit | no | 9 s | no fields yet |
  | Sand | TerrainSourced | no | | capacity from a Sand Collector or Sand Dredge on Sand (buildings.md) |
  | Water | TerrainSourced | no | | capacity from a Water Pump or Pumping Station beside water (buildings.md) |
  | Lithium | TerrainSourced | no | | a Brine Well (not yet built) |

  The refined resources, by tier (Iron from the Smelter and Rocket Fuel from the Refinery; nothing
  makes the others yet):

  | Tier | Refined resources |
  |---|---|
  | 1 | Iron, RocketFuel, Copper, Steel, Glass, Lumber, Charcoal, Cement, Concrete, Kerosene, Plastic, Hydrogen, LiquidOxygen, Aluminum, Silicon, SulfuricAcid, Rubber, Titanium, CarbonFiber, EnrichedUranium |
  | 2 | HullPlate, SteelBeam, CopperWire, Circuit, InsulatedCable, MachineParts, SiliconWafer, Processor, Magnet, ElectricMotor, Battery, SolarCell, CompositePanel, CeramicTile, AluminumFrame, ArmorPlate |
  | 3 | ControlModule, Computer, ThrusterAssembly, FuelTank, HeatShield, FuelRod, LifeSupportUnit, SensorArray |
  | 4 | HullSection, Engine |

- **Fuels** have a burn time (`ResourceDef.BurnSeconds`): Coal 40 s, Charcoal 30 s, Wood 10 s
  (placeholders). `ResourceDefs.Fuels` lists them longest-burning first. A fuel is still a Stored
  resource; burning it in a fuel slot is a use of it (buildings.md).
- Deposits are finite resource fields. Terrain-sourced resources never run out but need a building
  on or beside the right terrain. Every resource is defined so the production plan (see
  docs/game-plan.md) and the UI are settled; most have no source yet (no fields, no building, no
  recipe). Refined resources come from processing buildings (see buildings.md).

### Discovery

- The simulation records each resource the first time it is **discovered** (`IsDiscovered`); like
  unlocking, it never clears during play and resets with the world.
- A deposit is discovered when a field of it first lies in an explored map cell: when the cell is
  revealed, when a field is placed in an already explored cell, and for every cell at once in a
  world without fog. Sand is discovered when a Sand cell is explored, and Water when a ShallowWater
  or DeepWater cell or a river is; a debug edit to an explored cell's terrain or feature counts.
- Anything else (Lithium and every refined resource) is discovered when the stockpile first holds
  some of it, and holding a resource always discovers it (`Debug.SeedStockpile` included).
- The new deposits have no fields yet, so they stay undiscovered.

### Resource fields

- A `ResourceField` occupies exactly one map cell (all 16 of its fine cells). It has an `Id`, a
  `Type` (a deposit type), its map cell (`MapX`, `MapZ`), an `InitialAmount`, and a remaining
  `Amount`. It is `Depleted` when empty, `RemainingFraction` reports how full it is, and `MinX`,
  `MinZ`, and `Center` give its world-space square.
- `StageCount` is 5. `Stage` is `ceil(RemainingFraction * StageCount)`: 5 when full, 1 just before
  depletion, 0 when depleted. Stages are presentation only; the amount is continuous.
- The field owns its placement. The simulation keeps a list of fields (stable order for stepping
  and view sync) and an index from map cell to field; `FieldAt(x, z)` finds the field covering a
  fine cell with one lookup. Adding or removing a field updates both.
- A field placed without an explicit amount takes its biome's amount for its type, or the global
  default of 250 (`BiomeDef.DefaultFieldAmount`) when the biome does not list the type (see
  terrain.md). A field may not sit on a map cell with a feature, and only a deposit type can be a
  field (placing any other kind throws).
- In a generated world, fields come from generation: each chunk places deposits by its biomes'
  densities and weights as it is generated (see world-generation.md).
- The bounded test map spawns a fixed set of fourteen fields, each at its biome's amount:

  | Type | Map cells | Biome | Total |
  |---|---|---|---|
  | Iron Ore | (10, 9), (11, 10), (9, 11); (4, 12) | Grassland (250 each); Highlands (400) | 1150 |
  | Copper Ore | (2, 11) | Highlands | 300 |
  | Coal | (1, 9) | Highlands | 400 |
  | Stone | (3, 9); (7, 10) | Highlands (400); Grassland (250) | 650 |
  | Wood | (3, 4), (4, 3), (2, 5); (12, 3) | Forest (500 each); Grassland (250) | 1750 |
  | Crude Oil | (14, 6), (15, 7) | Desert | 1000 |

  That is enough for a ship launch: 100 Iron from Iron Ore and 500 Rocket Fuel from Crude Oil.
- A field blocks movement and placement on all its fine cells until it depletes and is removed,
  which reopens them.

### Range to a field

- `DistanceToField(point, field)` is the distance from a point to the nearest point of the field's
  map-cell square (0 inside). `FieldsInRange(point, radius, types)` returns the non-depleted fields
  of any of the given types (or of one type) within a radius by that measure.

### Selecting a field

- Outside build mode, the field under the pointer (when no building is) gets the thin accent
  outline, a thin frame around its map cell (the same frame buildings get), and a left click opens its field detail panel (see
  presentation.md); the field keeps the outline while the panel is open. Clicking never starts
  or stops hand-harvesting.
- `ExtractorsWorking(field)` lists the switched-on extractors whose current field it is (display
  only; it changes no rule).

### Storage limits

- Each `ResourceDef` has a `ResourceStorage`: a **storage class** and its amounts.

  | Class | Limit | Resources |
  |---|---|---|
  | Bulk | per type | Wood, Stone, Iron Ore, Copper Ore, Coal, Bauxite, Limestone, Quartz, Titanium Ore, Sulfur, Lithium, Charcoal |
  | Fluid | per type | Crude Oil, Kerosene, Rocket Fuel, Hydrogen, Liquid Oxygen, Sulfuric Acid |
  | Secure | per type | Rare Earths, Uranium Ore, Crystals, Enriched Uranium, Fuel Rod |
  | Cargo | one shared pool | every other tier 1 to 4 resource |
  | Unlimited | none | Sand, Water |

- A per-type resource's limit (`StorageLimit`) is the sum, over every **online** building (see
  buildings.md: a storage building out of a hub's reach adds nothing), of what it adds
  (`StorageAdded`): the HQ adds the resource's **HQ base**, a Silo set to it adds its
  **Silo step**, and a class store of its class (Stockyard for Bulk, Tank Farm for Fluid, Vault for
  Secure) adds its **class step**. The amounts (placeholders for tuning):

  | Resources | HQ base | Silo step | Class step |
  |---|---|---|---|
  | Wood, Stone | 100 | 300 | 200 |
  | Iron Ore, Copper Ore, Coal, Charcoal, Limestone | 60 | 200 | 120 |
  | Bauxite, Quartz, Titanium Ore, Sulfur | 0 | 150 | 80 |
  | Lithium | 0 | 100 | 50 |
  | Crude Oil, Rocket Fuel, Liquid Oxygen | 0 | | 300 |
  | Kerosene, Hydrogen | 0 | | 200 |
  | Sulfuric Acid | 0 | | 150 |
  | Rare Earths | 0 | | 50 |
  | Uranium Ore, Crystals | 0 | | 30 |
  | Enriched Uranium, Fuel Rod | 0 | | 20 |

- The **Cargo pool** (`CargoPool`) is what the HQ (150) and the online Warehouses (200 each) add. Each
  Cargo resource takes up its **Cargo** weight per unit (`CargoUsed` sums them): 0.25 for Copper
  Wire, Circuit, Silicon Wafer, Processor; 0.5 for Plastic, Silicon, Rubber, Carbon Fiber, Insulated
  Cable, Magnet, Ceramic Tile; 1 for Iron, Copper, Steel, Glass, Lumber, Cement, Aluminum,
  Titanium, Machine Parts, Battery, Solar Cell; 2 for Concrete, Hull Plate, Electric Motor,
  Composite Panel, Computer; 3 for Steel Beam, Aluminum Frame, Armor Plate; Control Module and
  Sensor Array 4, Heat Shield and Life Support Unit 6, Thruster Assembly 8, Fuel Tank 10, Hull
  Section 20, Engine 25.
- `RoomFor(type)` is how much more fits: the limit less the amount for a per-type resource, the
  free pool over the weight for a Cargo resource, and infinite for Sand and Water. It is never
  below 0.
- **Sand and Water are capacity, not stock** (`CapacityResources.CapacityOf`): their quantity never
  matters, only how much the network supplies at once against what its consumers draw (buildings.md,
  Capacity). Nothing adds them to storage, so their stock stays 0; they keep their storage class
  (Unlimited), icon, and discovery.
- **Gathering and production add only what fits** (hand-harvesting and extractors, see below and
  buildings.md; processors, see buildings.md). **Returns** (a destroy refund, the inputs a
  switched-off processor hands back) and `Debug.SeedStockpile` add in full, even past a limit.
  An amount can sit above its limit (after a return, or when a storage building is destroyed or a
  Silo changes); nothing is removed, and gathering and production of it wait until it falls
  below the limit.
- With only the HQ, the starting materials (Wood, Stone, Iron Ore, Copper Ore, Coal, Charcoal,
  Limestone) and 150 Cargo fit; every other per-type resource needs its storage building first.
  Every building cost fits the HQ's base except the Vault's Steel.

### Stockpile and hand-harvesting

- The stockpile is a map from `ResourceType` to amount, exposed read-only. It starts empty: the
  player begins with nothing. Gathering adds whole units, so amounts stay whole.
- Whenever an amount rises, the simulation unlocks each building whose whole cost the stockpile
  now holds (see buildings.md).
- Each tick the simulation finds the nearest hand-harvestable field within `HarvestRange` (1.5
  units) of the player, by distance to the field's square, as the `HarvestTarget`; a Crude Oil
  field is never a target. Gathering is one unit at a time: while the player
  holds the harvest intent, `HarvestProgress` grows from 0 toward 1 over the target's gather time;
  at 1 one unit moves from the field to the stockpile and progress starts over. Releasing the
  intent or a change of target (walking away, or a nearer field) drops the partial progress. A
  depleted field is removed.
- While the target's resource has no room for the unit (`HarvestBlocked`), progress does not
  advance; the hint shows "Storage full" (see presentation.md).

### Collecting and loading by hand

- The harvest intent also works on buildings. Each tick the **interaction target**
  (`InteractTarget`) is the nearest building within `HarvestRange` of the player (distance to its
  footprint) that has something to collect or load, unless a hand-harvestable field is nearer; a
  building wins a tie. While a building is the target there is no harvest target.
- **Collect** (`InteractAction.Collect`): a building with units in its output buffer. Each unit (the
  first type in table order, at most 1) moves into storage after the resource's gather time over 10
  (`HandMoveSpeedup`), or 0.3 s (`HandMoveSeconds`) for a resource with no gather time; Iron Ore
  takes 0.6 s. With no room for the next unit (`InteractBlocked`) progress waits and the hint says
  "Storage full".
- **Load** (`InteractAction.Load`): an offline processor with an empty output buffer and a recipe
  input short of its input buffer's 5 cycles' worth and held in storage, or else an offline fuel
  burner whose fuel slot has room for a unit of its fuel (its choice, or with Best the
  longest-burning fuel storage holds). Each unit moves from storage into the input buffer or the
  fuel slot at the same pace; recipe inputs load before fuel.
- `InteractProgress` runs 0 to 1 per unit and resets on release or a new target or action;
  `InteractResource` and `InteractAmounts` give the hint its resource and counts.

### Rendering

- `ResourceFieldViews` gives each field `StageCount` model slots at deterministic, jittered
  positions inside its map cell (a quincunx around the center, kept clear of the cell edge) and
  shows the first `Stage` slots, so a field loses one model per stage. The slots of every field in
  a render tile are drawn together, one multimesh per model, for each terrain tile built (see
  presentation.md); a tile's batch is rebuilt when a field in it changes stage or is added or
  removed, so a depleted field's models disappear. A field in an unexplored map cell is not
  drawn (and cannot be hovered or clicked) until the player has seen its cell.
- `NodeModels` picks and places each slot's model from its deposit's glTF models (three variants each,
  all generated by the asset factory; a Wood field draws its biome's tree family: conifers in
  Forest, swamp trees in Swamp, broadleaf trees in Grassland and anywhere else), scaling to a target height (Iron Ore and
  Copper Ore ~0.8 units, Coal ~0.6, Stone ~0.9; Crude Oil is a flat pool, so its slot width sets
  its size; Wood 2.5 units for a field of 250
  or less, rising to 3.5 at 500 or more, so larger fields look larger) but capping the footprint to the slot size
  (1.2 units); it seats the model on the ground with a deterministic yaw and variant per slot. A
  deposit with no field models yet (the new deposits, which no world places) draws its item model
  in every slot at ~0.6 units, so a field placed through the debug tools still shows. The
  models are required: a missing file throws (see presentation.md). Assets and conventions live in
  `src/Game/assets/models/nature/README.md`.
- The top bar shows the pinned resources' amounts and limits, and the inventory panel (I) the
  Cargo pool and every resource by tier, discovered or not;
  the hint overlay shows "[E] Harvest" with the resource's icon, name, and a progress bar toward
  the next unit when a hand-harvestable field is in range (see presentation.md).

## Related

- buildings.md (refined resources, extractors), player.md (harvest input, field blocking),
  presentation.md (rendering, the top bar, hint overlay, and inventory).
