# Resources Blueprint

## Overview

Resources are the materials the factory loop produces and consumes. Raw resources come from
resource fields on the map (by hand or by extractor); refined resources come from buildings (see
buildings.md). A single global stockpile holds all amounts, within storage limits.

## Current state

### Resource types

- `ResourceType` has 63 values, the catalog planned in 2611141757-resource-tree, ordered by tier
  and then catalog order. `ResourceDefs.For(type)` gives each a display name, a `Kind`, a **tier**
  (0 for raw, 1 to 4 for made goods, the tier of the buildings that make them, buildings.md;
  `ResourceDefs.MaxTier` is 4), whether the player can harvest
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
  | Bauxite, Quartz | Deposit | yes | 6 s | fields (Mine, Drill Rig) |
  | Limestone, Sulfur | Deposit | yes | 4.5 s | fields (Mine, Drill Rig) |
  | TitaniumOre | Deposit | yes | 7.5 s | fields (Mine, Drill Rig) |
  | RareEarths, UraniumOre, Crystals | Deposit | no | 9 s | fields (Drill Rig only) |
  | Sand | TerrainSourced | no | | capacity from a Sand Collector or Sand Dredge on Sand (buildings.md) |
  | Water | TerrainSourced | no | | capacity from a Water Pump or Pumping Station beside water (buildings.md) |
  | Lithium | TerrainSourced | no | | a Brine Well on a salt flat (buildings.md) |

  The refined resources, by tier. What makes them (a leveled building's level named, buildings.md):
  the Smelter Iron and Copper, its Steel Mill level Steel too, and its Electric Smelter level
  Aluminum, Silicon, and Titanium too; the Refinery Kerosene; the Fuel Plant Rocket Fuel (Kerosene
  and Hydrogen); the Electrolyzer Hydrogen and Liquid Oxygen (the only source of Liquid Oxygen);
  the Chemical Plant Plastic, Rubber, and Carbon Fiber; the Glassworks Glass; the Kiln Charcoal,
  its Cement Kiln level Cement too, and its Tile Kiln level Ceramic Tile too; the Fabricator Hull
  Plate, Steel Beam, Aluminum Frame, Machine Parts, and Composite Panel; the Electronics Works
  Copper Wire and Circuit; the Assembly Plant Control Module, Thruster Assembly, and Fuel Tank; the
  Shipyard Hull Section and Engine. The later tiers: the Sawmill Lumber; the Concrete Mixer
  Concrete; the Chemical Plant also Sulfuric Acid; the Enrichment Plant Enriched Uranium and Fuel
  Rod; the Fabricator also Magnet, Electric Motor, and Armor Plate; the Electronics Works also Insulated Cable and Battery; the Semiconductor Fab Silicon
  Wafer, Processor, and Solar Cell; the Assembly Plant also Computer, Heat Shield, Life Support
  Unit, and Sensor Array (buildings.md has the recipes). Every refined resource has a maker:

  | Tier | Refined resources |
  |---|---|
  | 1 | Iron, Copper, Glass, Lumber, Charcoal, Cement |
  | 2 | RocketFuel, Steel, Concrete, Kerosene, Plastic, Hydrogen, LiquidOxygen, Aluminum, Silicon, SulfuricAcid, Rubber, Titanium, CarbonFiber |
  | 3 | HullPlate, SteelBeam, CopperWire, Circuit, InsulatedCable, MachineParts, SiliconWafer, Processor, Magnet, ElectricMotor, Battery, SolarCell, CompositePanel, CeramicTile, AluminumFrame, ArmorPlate |
  | 4 | EnrichedUranium, ControlModule, Computer, ThrusterAssembly, FuelTank, HeatShield, FuelRod, LifeSupportUnit, SensorArray, HullSection, Engine |

- **Fuels** have a burn time (`ResourceDef.BurnSeconds`): Fuel Rod 300 s, Coal 40 s, Charcoal
  30 s, Wood 10 s (placeholders); and a speed (`ResourceDef.FuelSpeed`) for a building that makes
  things while it burns: Coal 1.3, Charcoal 1.15, Wood and the Fuel Rod 1 (buildings.md, Capacity). `ResourceDefs.Fuels` lists them longest-burning first, and
  `Combustibles` the same without the Fuel Rod, which only a Fission Reactor burns (buildings.md).
  Batteries, Solar Cells, Heat Shields, Sensor Arrays, and Computers also go into the power
  buildings' costs. A fuel is still a Stored
  resource; burning it in a fire is a use of it (buildings.md).
- Deposits are finite resource fields. Terrain-sourced resources never run out but need a building
  on or beside the right terrain. Every resource is defined so the production plan (see
  docs/game-plan.md) and the UI are settled; most have no source yet (no fields, no building, no
  recipe). Refined resources come from processing buildings (see buildings.md).

### Discovery

- The simulation records each resource the first time it is **discovered** (`IsDiscovered`); it
  never clears during play and resets with the world.
- A deposit is discovered when a field of it first lies in an explored map cell: when the cell is
  revealed, when a field is placed in an already explored cell, and for every cell at once in a
  world without fog. Sand is discovered when a Sand cell is explored, and Water when a ShallowWater
  or DeepWater cell or a river is; a debug edit to an explored cell's terrain or feature counts.
- Anything else (Lithium and every refined resource) is discovered when the stockpile first holds
  some of it, and holding a resource always discovers it (`Debug.SeedStockpile` included).
- Every deposit has fields in some biome (world-generation.md), so each is discovered when a field
  of it is first seen. Bauxite, Quartz, Sulfur, and Titanium Ore have no HQ base (a Stockyard or
  Silo holds them) and the precious three none either (a Vault holds them), so until then their
  collectors fill their buffers and wait.

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
  default of 1000 (`BiomeDef.DefaultFieldAmount`) when the biome does not list the type (see
  terrain.md). A field may not sit on a map cell with a feature, and only a deposit type can be a
  field (placing any other kind throws).
- In a generated world, fields come from generation: each chunk places deposits by its biomes'
  densities and weights as it is generated, plus the starter set of two Wood and one Stone near the
  spawn, 1000 each (see world-generation.md).
- The bounded test map spawns a fixed set of twenty-two fields, each at its biome's amount:

  | Type | Map cells | Biome | Total |
  |---|---|---|---|
  | Iron Ore | (10, 9), (11, 10), (9, 11); (4, 12) | Grassland (1000 each); Highlands (1600) | 4600 |
  | Copper Ore | (2, 11) | Highlands | 1200 |
  | Coal | (1, 9) | Highlands | 1600 |
  | Stone | (3, 9); (7, 10) | Highlands (1600); Grassland (1000) | 2600 |
  | Wood | (3, 4), (4, 3), (2, 5); (12, 3) | Forest (2000 each); Grassland (1000) | 7000 |
  | Crude Oil | (14, 6), (15, 7) | Desert | 4000 |
  | Limestone | (6, 12) | Grassland | 1000 |
  | Sulfur | (6, 1) | Swamp | 1000 |
  | Crystals | (0, 11) | Highlands (the map has no Volcanic; the default) | 1000 |
  | Quartz | (13, 0) | Tundra | 1000 |
  | Rare Earths, Uranium Ore | (13, 2); (15, 3) | Tundra | 600 each |
  | Bauxite, Titanium Ore | (15, 12); (13, 11) | Desert | 1000 each |

  It also has a Laterite motherlode on map cells (11..12, 13..14), so it holds twenty-three fields
  in all.

  That holds the launch's fuel from fields: 500 Rocket Fuel's Kerosene from Crude Oil (its
  Hydrogen and the launch's Liquid Oxygen come from Water and Power).
- **The ship bill** (buildings.md) is 4 Hull Sections, 1 Engine, 1 Control Module, 500 Rocket Fuel,
  and 200 Liquid Oxygen: in raw terms roughly 150 Iron Ore, 75 Coal or Charcoal, 75 Limestone, 60
  Bauxite, 16 Titanium Ore, 6 Quartz, 8 Sulfur, and about 600 Crude Oil with the fuel. The parts
  are Cargo, so a ship's parts need Warehouses beside the HQ's pool.
- A field blocks movement and placement on all its fine cells until it depletes and is removed,
  which reopens them.
- **Worked-out records:** when a field of any deposit but Wood depletes (and is not a Forester's),
  its map cell keeps the deposit it held (`WorkedOut`). A record blocks nothing; a Deep Well or
  Deep Mine beside one works it (buildings.md). The world shows a marker there (presentation.md).
- **Growing fields:** a Forester's field (`Owner`) is `Growing` until its `GrowLeft` runs out; a
  field is `Workable` (extractors take it, the player harvests it) only when grown, not a
  motherlode, and not depleted.
- **Motherlodes** (`MotherlodeDefs`): a field of `Size` 2 (2 x 2 map cells, 8 x 8 units) with a
  `Motherlode` kind instead of an amount. It never depletes; only the Deep Mines (or, for a Salt
  Dome, the Deep Wells) beside it work it, any number of them. Its `Type` is its largest yield. The field index lists it under all
  four of its map cells, so `FieldAt`, walking, and placement treat its 64 fine cells as the field.
  Seeing it discovers every resource it yields.

  | Kind | Biomes | Yields (share) | Worked by |
  |---|---|---|---|
  | Laterite | Grassland, Desert | Iron Ore 60%, Bauxite 40% | Deep Mine (Mine level 3) |
  | Porphyry | Highlands | Copper Ore 55%, Iron Ore 25%, Quartz 20% | Deep Mine |
  | Coal Measures | Highlands, Forest | Coal 50%, Iron Ore 30%, Limestone 20% | Deep Mine |
  | Salt Dome | Desert | Crude Oil 75%, Sulfur 25% | Deep Well (Oil Well level 3) |
  | Mineral Sands | Tundra | Titanium Ore 50%, Quartz 40%, Rare Earths 10% | Deep Mine |
  | Massive Sulfide | Volcanic, Swamp | Sulfur 45%, Copper Ore 35%, Iron Ore 20% | Deep Mine |

  A unit of the largest share takes 5 s (`MotherlodeDef.BaseSeconds`); a smaller share takes 5 s
  times the largest share over its own (Laterite Bauxite 7.5 s, Mineral Sands Rare Earths 25 s).
- **Charcoal** comes from the Kiln (the Charcoal Kiln, its level 1; 3 Wood to 1).

### Range to a field

- `DistanceToField(point, field)` is the distance from a point to the nearest point of the field's
  map-cell square (0 inside). `FieldsInRange(point, radius, types)` returns the non-depleted fields
  of any of the given types (or of one type) within a radius by that measure.

### Selecting a field

- Outside build mode, the field under the pointer (when no building is) gets the thin accent
  outline, a thin frame around its map cell (the same frame buildings get), and a left click opens its field detail panel (see
  presentation.md); the field keeps the outline while the panel is open. A left click never
  starts or stops hand-harvesting; a right click on a field walks to the ground beside it
  (player.md).
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
- Each tick the simulation finds the nearest hand-harvestable field within `HarvestRange` (1.5
  units) of the player, by distance to the field's square, as the `HarvestTarget`; a Crude Oil
  field is never a target. Gathering is one unit at a time: while the player
  holds the harvest intent, `HarvestProgress` grows from 0 toward 1 over the target's gather time;
  at 1 one unit moves from the field to the stockpile and progress starts over. Releasing the
  intent or a change of target (walking away, or a nearer field) drops the partial progress. A
  depleted field is removed.
- While the target's resource has no room for the unit (`HarvestBlocked`), progress does not
  advance; the hint shows "Storage full" (see presentation.md).

### Collecting by hand

- The harvest intent also works on buildings. Each tick the **interaction target**
  (`InteractTarget`) is the nearest building within `HarvestRange` of the player (distance to its
  footprint) that has something to collect, unless a hand-harvestable field is nearer; a
  building wins a tie. While a building is the target there is no harvest target.
- **Collect:** a building with units in its output buffer, online or offline (an offline
  building makes nothing, but what it holds can still be carried in). One hold of
  `HandCollectSeconds` (0.5 s), whatever the buffer holds, moves every type into storage at once,
  each as far as storage has room (a type needs room for a unit, or for all of it when less is
  left). What does not fit stays in the buffer, and the next hold collects it once there is room.
  With room for none of it (`InteractBlocked`) progress waits and the hint says "Storage full".
- `InteractProgress` runs 0 to 1 over the hold and resets on release or a new target;
  `InteractResource` (the first type in table order) and `InteractHeld` (its count and how many
  other types the buffer holds) give the hint its text.

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
  Copper Ore ~0.8 units, Coal ~0.6, Stone ~0.9, Bauxite ~0.7, Quartz ~0.9, Limestone ~0.8, Sulfur
  ~0.5, Titanium Ore ~0.8, Rare Earths and Uranium Ore ~0.7, Crystals ~1.1; Crude Oil is a flat
  pool, so its slot width sets its size; Wood 2.5 units for a field of 1000 or less, rising to 3.5
  at 2000 or more, so Forest trees stand taller) but capping the footprint to the slot size
  (1.2 units); it seats the model on the ground with a deterministic yaw and variant per slot. A
  deposit with no field models of its own would draw its item model in every slot at ~0.6 units;
  every deposit has its own today. The
  models are required: a missing file throws (see presentation.md). Assets and conventions live in
  `src/Telaelus/assets/models/nature/README.md`.
- The top bar shows the pinned resources' amounts and limits (Sand and Water are never pinned:
  they read as capacity beside Power once the network has any), and the inventory panel (I) the
  Cargo pool and every resource by tier, discovered or not;
  the hint overlay shows "[E] Harvest" with the resource's icon, name, and a progress bar toward
  the next unit when a hand-harvestable field is in range (see presentation.md).

## Related

- buildings.md (refined resources, extractors), player.md (harvest input, field blocking),
  presentation.md (rendering, the top bar, hint overlay, and inventory).
