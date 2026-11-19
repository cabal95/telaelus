# Buildings Blueprint

## Overview

Buildings are placed on the fine grid and run the production chain that turns raw resources into
fuel and then into ship parts. Building the ship's parts and its fuel lets the player launch a
ship from a Shipyard, which closes the on-planet loop.

## Current state

### Building types and definitions

- `BuildingType` has fifty-one values: **Mine**, **Lumber Camp**, **Smelter**, **Refinery**,
  **Oil Well**, the **HQ**, the storage buildings **Silo**, **Stockyard**, **Tank Farm**,
  **Warehouse**, and **Vault**, the hubs **Small Hub** and **Relay**, the generators **Water
  Wheel** and **Burner Generator**, the powered extractors **Lumber Mill** and **Drill Rig**, the
  Water and Sand providers **Water Pump**, **Pumping Station**, **Sand Collector**, and **Sand
  Dredge**, the **Glassworks**, the **Depot**, and the renewables **Forester**, **Quarry**,
  **Charcoal Kiln**, **Rock Crusher**, **Deep Well**, and **Deep Mine**, and the tier-1 refining
  **Steel Mill**, **Electrolyzer**, **Fuel Plant**, **Chemical Plant**, **Aluminum Smelter**, and
  **Electric Furnace**, and the parts buildings **Fabricator**, **Electronics Works**, **Tile
  Kiln**, **Assembly Plant**, and **Shipyard**, and the later tiers' **Sawmill**, **Cement
  Kiln**, **Concrete Mixer**, **Brine Well**, **Semiconductor Fab**, **Air Separator**, and
  **Enrichment Plant**, and the power buildings **Solar Array**, **Battery Bank**, and **Fission
  Reactor**, and the **Research Lab**.
- Each type has a `BuildingDef` (in `BuildingDefs`): a display name, a footprint in fine cells
  (width along X, depth along Z when facing North), a cost, a speed, and for an extractor, the
  deposit types it works (`Works`; a building is an extractor when the list is not empty) and its
  work radius. It also has its `StorageRole` (None, Hq, Silo, ClassStore with the class it
  `StoresClass`, or Warehouse), the `CargoPool` it adds, whether it is `Permanent` (cannot be
  switched off or destroyed), whether it is `Buildable` (placed by the player; the world places
  the HQ), a hub's `CoverageRadius` and `LinkRadius` (`IsHub` when either is set), and the sizes of
  its buffers: `OutputBuffer` (units) and `InputCycles` (cycles' worth of each recipe input). It
  has a `Tier` (0 unpowered, 1 the first powered tier), the capacity it `Draws` and `Supplies`
  (maps from `CapacityType` to amount; `Draw(type)` and `Supply(type)` read 0 for a type not
  listed, and `IsProvider` when it supplies any), `BurnsFuel` (a fuel slot, `FuelSlotSize` 5
  units), and its `Placement` rule (Standard, AcrossStraightRiver, BesideWater, OnSand,
  OnDesertSand, or the renewables' three).

  | Type        | Footprint | Cost                       | Works                             | Work radius |
  |-------------|-----------|----------------------------|-----------------------------------|-------------|
  | Lumber Camp | 2 x 2     | 15 Wood                    | Wood                              | 6 units     |
  | Mine        | 2 x 2     | 25 Wood, 15 Stone          | Iron Ore, Copper Ore, Coal, Stone, Bauxite, Quartz, Limestone, Sulfur, Titanium Ore | 6 units     |
  | Smelter     | 3 x 3     | 40 Stone, 15 Wood          |                                   |             |
  | Oil Well    | 2 x 2     | 25 Iron, 20 Stone, 10 Wood | Crude Oil                         | 3 units     |
  | Refinery    | 2 x 3     | 40 Iron, 30 Stone, 20 Wood |                                   |             |

  The storage buildings (what each adds is in resources.md):

  | Type | Footprint | Cost | Adds |
  |---|---|---|---|
  | HQ | 6 x 10 | free, placed by the world | every per-type resource's HQ base, and 150 Cargo |
  | Silo | 2 x 2 | 30 Wood | its chosen Bulk resource's Silo step |
  | Stockyard | 3 x 3 | 40 Wood, 20 Iron | every Bulk resource's class step |
  | Tank Farm | 3 x 3 | 30 Iron, 20 Stone | every Fluid resource's class step |
  | Warehouse | 3 x 3 | 40 Wood, 30 Stone | 200 Cargo |
  | Vault | 3 x 3 | 40 Steel, 40 Stone | every Secure resource's class step |

  The Oil Well's short radius means it sits beside its seep.
- **Costs** lean lightly on the real world: a building costs what most of it is made of (a Lumber
  Camp is wood, a Smelter is a stone furnace), not every material it would really need. The
  intended path is a Lumber Camp from hand-gathered Wood, a Mine from hand-gathered Stone, a
  Smelter from the Mine's Stone, then the oil chain from the Smelter's Iron. Copper Ore and Coal
  are in no cost yet.
- **Speed**: every building is base level today, `BuildingDef.BaseSpeed` 0.75, so a base extractor
  is slower than the player by hand. Its benefit is that it works unattended; two together
  out-gather the player. Higher levels would raise the speed.

- A `Recipe` has inputs, outputs (each a `ResourceAmount`), and a cycle time. Recipes are defined
  centrally:
  - Extractors (Mine, Lumber Camp, Oil Well): no recipe; each takes one unit at a time at the
    deposit's gather time divided by its speed (at base speed: Wood 4 s, Stone and Coal 6 s, Iron
    Ore and Copper Ore 8 s, Crude Oil 4 s).
  - Smelter: 1 Iron Ore to 1 Iron, or 1 Copper Ore to 1 Copper, cycle 16 s (burns fuel).
  - Refinery: 1 Crude Oil to 1 Kerosene, cycle 8 s (Rocket Fuel is made from it in the Fuel Plant).
  - Glassworks: no inputs to 1 Glass, cycle 12 s (its Sand is capacity, below).
  - Charcoal Kiln: 3 Wood to 1 Charcoal, cycle 15 s (no fuel slot: part of the wood burns).
  Processors run at half the pace of one base extractor, so one Mine keeps two Smelters busy and
  one Oil Well two Refineries.
  These are placeholders until the production chain toward a ship (Steel Mill, ship parts; see
  docs/game-plan.md) is built.

### Placement state: anchor and facing

- A `Building` has an id, type, definition, an **anchor** (its minimum-corner fine cell), a
  **facing** (North, East, South, West), an optional recipe, its current field (extractors), and
  cycle state (`Crafting`, `Progress`).
- Anchor and facing are the authoritative placement. East and West swap the footprint's width and
  depth, so a 2 x 3 Refinery facing East is 3 x 2. `Width`, `Depth`, and `Position` (the footprint
  center as a world point) are derived from them, not stored. Facing is kept for belt input and
  output sides later.

### Placement

- `CanPlace(type, anchorX, anchorZ, facing)` applies the standard rule: every footprint cell is in
  bounds, explored (fog of war, simulation.md), buildable (`IsBuildable`: Grass, Sand, Snow, or Rock with no blocking feature, so a
  river bank is buildable but its channel and a mountain are not; see terrain.md), unoccupied,
  and not covered by a resource field, and no unit is inside the
  footprint. Today the only unit is the player, whose cell is the fine cell their position floors
  to (`CellAt`), the same point test movement collision uses, so a building can go exactly where
  the player could not walk into. A building may be placed right next to the player. No building
  may sit on a resource. The rule is the definition's `Placement`: Standard is the rule above;
  BesideWater adds that at least one footprint cell is edge-adjacent (not corner) to ShallowWater,
  DeepWater, or river channel; OnSand adds that every footprint cell is Sand terrain;
  OnDesertSand (the Brine Well's salt flats) adds that every cell is Sand terrain in a Desert map
  cell; and
  AcrossStraightRiver is the Water Wheel's own rule (Capacity, below). The renewables add three
  (Renewables, below): BesideMountain (the standard rule, with a cell edge-adjacent to a mountain),
  OpenGrassCell (the footprint is exactly one map cell, all Grass), and OverSite (the footprint is
  exactly a worked-out map cell of a deposit in the definition's `Sites`, or exactly a motherlode's
  center 4 x 4 when the motherlode's kind is worked by this type; on a motherlode the cells need
  only be walkable). `SnapAnchor` gives the ghost's anchor for the last two: the map cell's
  corner, or a motherlode's center.
- An extractor may be placed with no matching field in its work area; it idles until one is in
  range. This allows planning ahead and later lets resources be created around a building.
- **Unlocking**: a building is unlocked (`IsUnlocked`) once the stockpile has held its whole cost
  at once (checked whenever an amount rises); it stays unlocked when the stock later runs short,
  so it can still be selected while unaffordable. Only a buildable type unlocks (never the HQ).
  At the start nothing is unlocked. `CanAfford` is whether the stockpile holds the whole cost now.
- `TryPlaceBuilding(type, anchorX, anchorZ, facing)` is placement as the player does it: the spot
  must pass `CanPlace` and the building must be unlocked and affordable. It deducts the cost, adds
  the building, and marks every footprint cell occupied; a refused placement changes nothing.
  There is no demolish yet, so no refund. `Debug.PlaceBuilding` places by `CanPlace` alone, free
  and whether or not unlocked. A building blocks player movement on its cells
  for as long as it stands.

### Hubs and the HQ network

- **Hubs** connect buildings to the HQ. Radii are in world units (one fine cell), from the hub's
  footprint center:

  | Hub | Footprint | Cost | Coverage radius | Link radius |
  |---|---|---|---|---|
  | HQ | 6 x 10 | placed by the world | 19 | 22 |
  | Small Hub | 1 x 1 | 10 Wood, 5 Stone | 9 | 14 |
  | Relay | 2 x 2 | 20 Iron, 10 Stone | 3 | 30 |

- Two switched-on hubs **link** when the distance between their centers is at most the larger of
  their link radii. The hubs linked to the HQ, directly or through others, are online; a cluster
  that does not reach the HQ is offline, not a second network.
- A building is **online** (`Building.Online`) when it is an online hub or an online hub
  **covers** it: the nearest point of its footprint (`DistanceToFootprint`) is within the hub's
  coverage radius, the same measure as an extractor's work radius. Everything else is offline.
  A switched-off hub neither links nor covers, so switching one off cuts its branch.
- `RecomputeNetwork` runs when a building is added (placing, the world's HQ), removed, or switched
  on or off: a flood fill over the hubs from the HQ, then a coverage pass. Nothing is computed per
  tick. `LinkedHubs(hub)` lists the online hubs a hub links to, and `WouldBeOnline(type, anchor,
  facing)` whether a building placed there would be online (for the placement ghost).
- Storage buildings count toward limits only while online (resources.md).

### Capacity

- **Capacity** (`CapacityType`: Power, Water, Sand, in that order) is supplied by providers and
  reserved by consumers while they are switched on; it is never stored. Each tick, before
  buildings work (`StepCapacity`): every switched-on fuel-burning provider burns (a Burner
  Generator burns whenever it is on, with or without demand, online or not; switching it off is how
  fuel is saved); then for each type in order, **supply** is what every online, switched-on
  provider that runs supplies (a Burner Generator only while a unit burns), times that provider's
  own speed (below), and **demand** is what every online, switched-on consumer draws, working or
  not, so a blackout cannot flap. `Capacity(type)` reads a type's `CapacityReading` (supply,
  demand, speed, state).
- **Overload** is demand over supply less 1. The state (`CapacityState`, from `StateFor`) is
  Normal at or under supply, Brownout up to `BrownoutLimit` (25%) over, and Blackout past it or
  with demand and no supply. The type's speed is 1 when Normal, 1 less twice the overload in a
  Brownout (0.8 at 10% over, 0.5 at 25%), 0 in a Blackout.
- A building's speed (`WorkSpeed`) is the slowest speed of the types it draws (1 when it draws
  none), and 0 offline. A consumer's extractor progress and processor cycle advance at their speed
  times it; at 0 they do not start or advance. A provider that draws capacity supplies times its
  speed: a Pumping Station in a power brownout at 80% supplies 12.8 Water, and none in a power
  blackout, so a power shortage cascades into a water shortage. The order (a provider's draws come
  before what it supplies) makes that speed this tick's.

| Provider | Footprint | Cost | Supplies | Draws | Placement |
|---|---|---|---|---|---|
| Water Wheel | 4 x 2 | 40 Wood, 20 Stone | 20 Power | | Across a Straight river map cell (below); runs whenever it stands |
| Burner Generator | 2 x 2 | 60 Stone, 40 Iron | 100 Power | | Standard; burns fuel whenever switched on |
| Water Pump | 2 x 2 | 20 Wood, 10 Stone | 4 Water | | BesideWater |
| Pumping Station | 3 x 3 | 40 Iron, 20 Stone | 16 Water | 10 Power | BesideWater |
| Sand Collector | 2 x 2 | 20 Wood, 10 Stone | 2 Sand | | OnSand |
| Sand Dredge | 3 x 3 | 40 Iron, 20 Stone | 8 Sand | 10 Power | OnSand |
| Rock Crusher | 3 x 3 | 40 Iron, 20 Stone | 4 Sand | 15 Power | Standard; eats 1 Stone every 10 s (`Eats`) |

| Consumer | Footprint | Cost | Draws | Speed | Works | Buffer |
|---|---|---|---|---|---|---|
| Lumber Mill | 3 x 3 | 30 Wood, 20 Iron | 5 Power | 2.0 | Wood, radius 8 | 40 |
| Drill Rig | 3 x 3 | 30 Iron, 20 Stone | 10 Power | 2.0 | the Mine's deposits and Rare Earths, Uranium Ore, Crystals, radius 8 | 40 |
| Refinery | 2 x 3 | as above | 25 Power, 2 Water (its cooling) | | | 10, inputs 5 cycles |
| Glassworks | 3 x 6 | 40 Stone, 20 Wood | 2 Sand | | | 10 |

- The small providers and the Glassworks are tier 0; the Pumping Station, Sand Dredge, Lumber
  Mill, Drill Rig, and Refinery tier 1. A Pumping Station is four Pumps' worth for some Power, and
  a Sand Dredge four Collectors'.
- **The Glassworks** is a furnace: it burns fuel like the Smelter (Coal, Charcoal, or Wood, Best by
  default) and needs no power. Its Glass goes to the Cargo pool.

- **The Water Wheel's placement** (`CanPlaceAcrossRiver`): its whole footprint lies within one map
  cell whose river is Straight, its 4-unit side across the flow (facing North or South on a
  North-South river, East or West on an East-West one); each cell is buildable or river channel,
  explored, free, and not the player's.

### Trucks

- **The Depot** (4 x 4, 40 Wood and 20 Stone, tier 0) holds up to 4 trucks (`TruckSlots`), parked
  in bays across the apron at its front (`BayPosition`), nose out. A new Depot comes with 1;
  `TryBuyTruck` buys another into the next free bay for `TruckDefs.Cost` (15 Iron, 10 Wood) while
  a bay is free (`CanBuyTruck`). `TrySellTruck` sells one (`TruckToSell`: a parked truck before
  one out on a trip, the highest bay first) for what `RefundOf` gives: half a bought truck's cost,
  rounded up (8 Iron, 5 Wood; the truck the Depot came with gives nothing), and any load it
  carries, in full. A Depot may sell every truck and buy again. It draws nothing, but its trucks take work only while it is
  **online**: offline it is idle "Offline: trucks stay parked".
- **A truck** (`Truck`, in `Trucks`) carries one resource at a time, up to `TruckDefs.Capacity`
  (20), at `TruckDefs.Speed` (3 units per second, half the player's 6; 4.5 on a road) times the
  terrain speed under it, and loads and unloads at `TruckDefs.LoadSecondsPerUnit` (0.25 s a unit:
  5 s for a full load). Trucks occupy no cells and pass through the player and each other.
- **Balance:** one truck keeps up with about three base collectors 40 units from the HQ (four at
  20, two at 80); a Depot's four trucks serve about twelve near the HQ. Unloading on the HQ's
  pad costs a little when the pad faces away from the collectors (two trucks deliver about 92% of
  six Mines 40 out on the empty map, whose HQ faces its pad away from them). Upgrades that raise the
  capacity are partly offset by the time each unit takes to load. `TruckBalanceTests` measures it.
- **Room:** a truck's room for a resource is storage's room (`RoomFor`) less what every other
  loaded truck is carrying of it to the HQ (`ToHq` or `Unloading`). A truck's load from a
  collector (`LoadFor`) is the resource giving the biggest load, in whole units up to its capacity
  and that room (the most-held on a tie), or none when storage can take none of what it holds.
- **Each tick, after buildings** (`StepTrucks`), a truck steps through its trip
  (`TruckState`):
  - **Parked** or **ToDepot** (heading home): while its Depot is switched on and online, it
    **dispatches**:
    among online collectors (`IsCollector`: extractors and the Quarry, Deep Well, and Deep Mine)
    whose load is at least `TruckDefs.DispatchAt` (10), or at least 1 when the
    buffer is full, that no truck targets (`TruckFor`) and that it has not found unreachable, it
    takes the one with the biggest load, nearest on a tie, and the first a path reaches. A
    collector full of what storage cannot take is no job; it stays full until room appears.
  - **ToCollector:** it drives beside the collector; if the collector is destroyed, goes offline,
    or its buffer falls under 1 unit first, it drops the job and heads home (dispatching again on
    the way).
  - **Loading** (0.25 s for each unit of its load on arrival): then it takes its load as it
    stands then and drives to the HQ's pad (**ToHq**, below). A collector that goes offline while it loads
    sends it home empty (the load is taken only at the end). With nothing left to take it heads home; with
    no path to the HQ it takes nothing and counts the collector unreachable.
  - **At the HQ's pad** (`HqPad`, 3.6 units in front of the HQ's center, its landing pad): a
    truck drives to the pad's front (2 units further out, just outside the HQ) and joins the line
    there (**WaitingForPad**, its place in line kept in its `Timer`). One truck holds the pad at a
    time (`PadHolder`: backing on, unloading on it, or pulling off). The next in line, when the pad
    is free, drives up to the front, turns its tail to the entrance, and backs on at half speed
    (**BackingOnPad**); it unloads on the pad with its bed to the hall, then drives off nose first
    to the front (**LeavingPad**) and heads home. The others wait in a line out from the front,
    1.8 units apart, facing the HQ, moving up as it shortens (`PadQueue`). The pad moves are
    straight runs over the HQ's forecourt, so paths never enter the HQ. With no path to the pad's
    front (a building on it), a truck unloads beside the HQ as before.
  - **Unloading** (0.25 s a unit): it puts the whole load into the stockpile, even past the limit
    when room it counted on was taken on the way (the player collecting by hand, a store
    destroyed), as a destroy refund does, so a truck never waits at the HQ. Empty, it heads home.
    (**WaitingForRoom** is only in older saves, where a truck waited for room; it unloads at once.)
  - **ToDepot:** on arrival it parks in its bay, facing out.
- A switched-off or offline Depot's trucks finish the trip they are on, then park; they dispatch
  again when it is switched back on and online.
- **Destroying a Depot** removes its trucks; its refund (`DestroyRefund`) adds each truck's
  `RefundOf`, as selling it would.
- `NoTruckCanReach(collector)` is true when every truck has found no path to it; the unreachable
  set clears whenever a building is placed or removed.

### Paths

- Trucks and the player path in the sim (`FindPathBeside`, `FindPathTo`, and the player's walk,
  player.md): A* over fine cells in 8 directions
  without cutting corners, each step costing its length over the terrain speed of the cell it
  enters (`CellSpeed`: a road or bridge's 1.5, else the terrain's, so Mud and Sand cost more, a
  path goes round them when that is cheaper, and takes a road where it saves time; the estimate is
  the octile distance at road speed, so the search stays exact). Bridge cells are walkable, so a
  path crosses water only on a bridge. A cell is
  passable when it is walkable (`IsWalkable`) and its chunk already exists (a search never
  generates the world); a truck's own Depot's apron (the front half of its footprint, where the
  bays are) is passable to it, so it can leave and reach its bay; the shed behind is not, so it
  drives round. For the player a cell must also be explored. "Beside" a building is a cell sharing an
  edge with its footprint. A search gives up as unreachable after 60,000 cells; the player's
  search then (or when its goal cannot be reached) gives the path to the cell it found nearest the
  goal.
- A path is the list of cell centers to drive through. A truck searches only on dispatch, on
  leaving a collector or the HQ, and when the next cell of its path has become impassable (a
  building placed on it), when it searches again for the same end; a collector it then cannot
  reach is dropped and counted unreachable.

### Recipes and refining

- `Recipes.For(type)` lists a processor's recipes, the default first (an empty list for anything
  else); every recipe has one output, named for it in a picker (`Recipe.Name`). A building runs one
  (`RecipeIndex`, `Building.Recipe`). `SetRecipe` changes it at once between cycles, or, during a
  cycle, when that cycle finishes (`PendingRecipe`); the input buffer keeps what it holds, and
  what the running recipe cannot use goes back to storage whenever the building is online.
- **Alternative inputs:** a recipe input may be filled by any of several types in the same amount
  (`Recipe.Alternatives`, `OptionsFor`; Steel's and Silicon's carbon is Coal or Charcoal).
  `SetInputChoice` picks one (`Building.InputChoices`) or clears it for **Auto**, which takes the
  one most at hand (`AtHand`: the input buffer, plus storage when online), the first on a tie
  (`ChosenInput`, `ResolvedInputs`). A cycle records what it took (`CycleInputs`), refunded if the
  building is destroyed mid-cycle. A building waiting on an Auto input names all of them ("Waiting
  for Coal or Charcoal"); on a chosen one, that one.

| Building | Footprint | Cost | Draws | Recipes (inputs to 1 output, cycle) |
|---|---|---|---|---|
| Steel Mill | 4 x 6 | 60 Stone, 40 Iron | 20 Power | Steel: 2 Iron, 1 Coal or Charcoal, 1 Limestone, 20 s |
| Electrolyzer | 4 x 5 | 40 Iron, 20 Copper | 50 Power, 4 Water | Hydrogen: no inputs, 6 s; Liquid Oxygen: no inputs, 6 s |
| Fuel Plant | 4 x 5 | 40 Steel, 20 Copper | 20 Power | Rocket Fuel: 1 Kerosene, 1 Hydrogen, 8 s |
| Chemical Plant | 5 x 6 | 40 Steel, 20 Copper | 25 Power, 2 Water | Plastic: 2 Crude Oil, 10 s; Rubber: 1 Crude Oil, 1 Sulfur, 12 s; Carbon Fiber: 2 Plastic, 15 s |
| Aluminum Smelter | 3 x 8 | 50 Steel, 30 Stone | 60 Power | Aluminum: 2 Bauxite, 20 s |
| Electric Furnace | 4 x 4 | 50 Steel, 20 Copper | 40 Power | Silicon: 1 Quartz, 1 Coal or Charcoal, 15 s; Titanium: 2 Titanium Ore, 20 s |

- All six are tier 1 with an output buffer of 10 and 5 cycles of each input. Steel is the gate:
  the Steel Mill is built from Stone and Iron, the rest of the tier from Steel (the Electrolyzer
  from Iron and Copper).

**Parts.** The buildings that make the ship's parts are grouped by process, not by part: the
Fabricator forms and machines metal and composites, the Electronics Works makes wire and circuits,
the Tile Kiln fires ceramics, the Assembly Plant assembles tier-3 parts, and the Shipyard builds
the tier-4 Hull Section and Engine. A recipe may make more than one unit (`Recipes.MakeMany`).

| Building | Footprint | Cost | Tier | Draws | Recipes (inputs to output, cycle) |
|---|---|---|---|---|---|
| Fabricator | 4 x 6 | 40 Steel, 20 Copper | 2 | 30 Power | Hull Plate: 2 Steel, 12 s; Steel Beam: 3 Steel, 15 s; Aluminum Frame: 2 Aluminum, 12 s; Machine Parts: 2 Steel, 1 Rubber, 15 s; Composite Panel: 2 Carbon Fiber, 1 Aluminum, 20 s |
| Electronics Works | 4 x 5 | 30 Steel, 30 Copper, 10 Glass | 2 | 20 Power, 2 Water | 2 Copper Wire: 1 Copper, 6 s; Circuit: 2 Copper Wire, 1 Plastic, 10 s |
| Tile Kiln | 3 x 7 | 40 Stone, 20 Steel | 2 | 30 Power | 2 Ceramic Tile: 1 Silicon, 1 Aluminum, 15 s |
| Assembly Plant | 6 x 8 | 20 Steel Beam, 20 Hull Plate, 10 Circuit | 3 | 40 Power | Control Module: 4 Circuit, 2 Hull Plate, 2 Glass, 40 s; Thruster Assembly: 4 Titanium, 2 Machine Parts, 6 Ceramic Tile, 45 s; Fuel Tank: 4 Aluminum Frame, 4 Composite Panel, 40 s |
| Shipyard | 8 x 12 | 40 Steel Beam, 20 Hull Plate, 10 Circuit | 4 | 50 Power | Hull Section: 4 Hull Plate, 2 Steel Beam, 60 s; Engine: 2 Thruster Assembly, 2 Fuel Tank, 4 Machine Parts, 90 s |

- Each tier is built from the one before: tier-2 buildings from Steel and Copper (and Stone, Glass),
  the Assembly Plant and Shipyard from tier-2 parts. All have an output buffer of 10 (the
  Shipyard's 2) and 5 cycles of each input.

**Later tiers.** Every catalog item has a maker. The later tiers add recipes to buildings above,
each listed after the ones in the tables:

- Chemical Plant: 2 Sulfuric Acid: 1 Sulfur, 8 s (the plant's Water).
- Fabricator: 2 Magnet: 1 Rare Earths, 1 Iron, 12 s; Electric Motor: 1 Steel, 2 Copper Wire, 1
  Magnet, 15 s; Armor Plate: 2 Steel, 1 Titanium, 15 s.
- Electronics Works: 2 Insulated Cable: 2 Copper Wire, 1 Rubber, 8 s; Battery: 1 Lithium, 1
  Copper, 1 Plastic, 15 s.
- Assembly Plant: Computer: 2 Processor, 2 Circuit, 2 Insulated Cable, 40 s; Heat Shield: 6 Ceramic
  Tile, 2 Composite Panel, 40 s; Life Support Unit: 2 Electric Motor, 2 Machine Parts, 2 Glass,
  45 s; Sensor Array: 1 Processor, 2 Magnet, 2 Glass, 40 s.

And seven buildings, all processors (output buffer 10, 5 cycles of each input):

| Building | Footprint | Cost | Tier | Draws | Recipes (inputs to output, cycle) |
|---|---|---|---|---|---|
| Sawmill | 3 x 5 | 30 Wood, 20 Stone | 0 | | Lumber: 2 Wood, 8 s |
| Cement Kiln | 3 x 7 | 40 Stone, 20 Iron, 20 Lumber | 0 | burns fuel | Cement: 2 Limestone, 12 s |
| Concrete Mixer | 3 x 4 | 30 Iron, 20 Lumber | 1 | 10 Power, 2 Sand, 2 Water | 2 Concrete: 1 Cement, 10 s |
| Brine Well | 4 x 6 | 30 Steel, 20 Concrete | 1 | 15 Power, 2 Water | Lithium: no inputs, 10 s |
| Semiconductor Fab | 5 x 6 | 40 Steel, 30 Concrete, 20 Glass | 2 | 40 Power, 4 Water | 2 Silicon Wafer: 1 Silicon, 10 s; Processor: 2 Silicon Wafer, 1 Circuit, 1 Sulfuric Acid, 20 s; Solar Cell: 1 Silicon Wafer, 1 Glass, 2 Copper Wire, 15 s |
| Air Separator | 4 x 5 | 30 Steel, 20 Concrete, 6 Electric Motor | 2 | 40 Power | Liquid Oxygen: no inputs, 5 s |
| Enrichment Plant | 6 x 7 | 40 Concrete, 20 Armor Plate, 10 Electric Motor, 10 Computer | 3 | 80 Power, 2 Water | Enriched Uranium: 2 Uranium Ore, 1 Sulfuric Acid, 30 s; Fuel Rod: 2 Enriched Uranium, 2 Steel, 40 s |

- The Cement Kiln burns fuel like the Glassworks (Coal, Charcoal, or Wood, Best by default) and
  needs no Power, so Cement and Concrete come early. The Brine Well stands on Desert Sand
  (OnDesertSand) and the Air Separator anywhere; both make their output from nothing, as the
  Electrolyzer does, which keeps its Liquid Oxygen recipe beside the Air Separator.
- The new buildings' costs use the new materials: Lumber, Concrete, Electric Motors, Armor Plate,
  and Computers. The power buildings (below) take Batteries, Solar Cells, Heat Shields, Sensor
  Arrays, and Fuel Rods; Life Support Units have no consumer yet (they wait for Part B's crewed
  ships).

### Power buildings

| Building | Footprint | Cost | Tier | Supplies | Draws | Notes |
|---|---|---|---|---|---|---|
| Solar Array | 6 x 6 | 20 Solar Cell, 10 Aluminum Frame, 10 Concrete | 2 | 60 Power times the daylight | | `Solar`; no fuel |
| Battery Bank | 3 x 4 | 20 Battery, 20 Steel, 10 Concrete | 2 | stored Power (below) | | `StoreCapacity` 6000, `StoreRate` 50 |
| Fission Reactor | 6 x 8 | 40 Concrete, 20 Armor Plate, 10 Heat Shield, 6 Sensor Array, 10 Computer | 3 | 400 Power | 8 Water | Burns Fuel Rods (`Fuels`), whenever switched on |

- **The Solar Array** supplies 60 Power times `Simulation.Daylight` (simulation.md): 60 at midday,
  none at night (its State reads "Idle: Night: supplies nothing"), about 37 over a day.
- **Stored Power:** a Battery Bank holds `Charge` (Power-seconds, up to `StoreCapacity`) and moves at
  most `StoreRate` Power in or out. Each tick, once Power's supply and demand are summed
  (`StepStore`), a surplus charges the switched-on, online banks, emptiest first, and a shortage
  draws on them, fullest first, each up to its rate and charge; what they give counts as supply
  before the state and speed are worked out, so a bank holds a shortage at Normal until it runs dry
  or the shortage outruns the banks' rates. A bank off or offline keeps its charge. The Power
  reading carries `Stored`, `StoreCapacity` (every bank, on or not), and `StoreFlow` (positive
  charging, negative discharging). A bank reports Working online, "Offline: stores nothing"
  offline. Burner Generators still burn whenever switched on, so their surplus is what banks store.
- **The Fission Reactor** is a fuel-burning provider like the Burner Generator that burns only Fuel
  Rods (300 s each) and draws Water for its cooling; it stands anywhere. Water comes after Power in
  the order, so its Power follows last tick's Water speed: in a Water brownout at half speed it
  supplies 200.

### Renewables

| Building | Footprint | Cost | Tier, draws | Placement | Does |
|---|---|---|---|---|---|
| Forester | 4 x 4 | 30 Wood, 20 Stone | 1; 2 Power, 5 Water | OpenGrassCell | Grows a 150-Wood field on its own cell (`Plot`) |
| Quarry | 3 x 3 | 40 Iron, 30 Wood | 1; 10 Power | BesideMountain | 1 Stone (or Limestone) every 6 s; buffer 40 |
| Charcoal Kiln | 2 x 2 | 30 Stone, 10 Wood | 0 | Standard | Its recipe above; buffer 10, inputs 5 cycles |
| Rock Crusher | 3 x 3 | 40 Iron, 20 Stone | 1; 15 Power | Standard | A Sand provider (Capacity, above) |
| Deep Well | 4 x 4 | 60 Iron, 30 Stone | 1; 15 Power | OverSite: worked-out Crude Oil, or a Salt Dome | 1 Crude Oil every 26.7 s on a worked-out cell; buffer 20 |
| Deep Mine | 4 x 4 | 80 Iron, 40 Stone | 2; 30 Power | OverSite: worked-out Iron Ore, Copper Ore, Bauxite, Titanium Ore, Quartz, or Limestone, or a solid motherlode | 1 unit every 20 s on a worked-out cell; buffer 40 |

- **The Forester's plot:** placed, it plants a Wood field of 150 on its own map cell
  (`ResourceField.Owner` is the Forester, `Growing` with `GrowLeft` 180 s). Growth advances at its
  work speed while it is switched on (the slower of its Power and Water speeds, 0 offline); a
  growing field cannot be worked or harvested (`Workable`). Grown, it is an ordinary Wood field for
  Lumber Camps, Lumber Mills, and the player. When it is cut down the plot stands bare
  (`PlotWait`, 30 s at the same speed) and then plants again. Destroying the Forester removes its
  field. A Forester's field leaves no worked-out record.
- **Collectors without a field** (`ProduceSeconds` above 0: the Quarry, Deep Well, and Deep Mine)
  step their progress at their work speed and put one unit of their output (`OutputOf`) into their
  buffer every `UnitSeconds`; a whole unit waits while the buffer is full ("Full: waiting for
  pickup"), like an extractor. `OutputChoices` lists what one may yield, the default first: the
  Quarry Stone, and Limestone too beside a Highlands mountain; a building over a worked-out cell
  that deposit; a building on a motherlode its yields (resources.md). `SetOutputChoice` picks one;
  it takes effect on the next unit, and what is in the buffer stays.
- **On a motherlode** a building's rate is the motherlode's for the chosen yield (5 s for its
  largest share, longer in proportion for a smaller one). Its **reach** (`ReachOf`, where trucks
  stop and the player collects with E) is the whole motherlode, since nobody walks on the ring
  around it; any other building's reach is its footprint.
- A placed Deep Well or Deep Mine records its site (`SiteMotherlode` or `SiteDeposit`). Only one
  building fits on a motherlode's center, so a motherlode yields one resource at a time.
- **The Rock Crusher** eats (`Eats`): while switched on, each tick it works through its current
  Stone at its work speed and, when that is used up, takes the next from storage (online only).
  With none it supplies nothing ("Waiting for Stone").

### Fuel

- **Fuels by building:** a definition lists the fuels it takes (`BuildingDef.Fuels`): Coal,
  Charcoal, and Wood (`ResourceDefs.Combustibles`) for every fire, Fuel Rods alone for the Fission
  Reactor. A burner takes, and its picker and Best offer, only its own; `SetFuelChoice` refuses any
  other.
- A fuel burner (the Burner Generator, the Smelter, the Glassworks, the Cement Kiln, the Fission
  Reactor) has a **fuel slot** (`FuelSlot`, unburned units
  by type) and a fire: the unit burning (`Burning`) and its seconds left (`BurnLeft`). When the fire
  is out it takes the next unit, from the slot first and then, online, from storage, and burns it
  for its burn time (resources.md). A unit taken is spent: switching off or destroying the building
  does not return it, though unburned slot units are refunded.
- **Fuel choice** (`FuelChoice`, `SetFuelChoice`): one fuel by name, or null for **Best**, the
  longest-burning fuel at hand (`ResourceDefs.Fuels` order), falling back when it runs out.
  `BestFuelAt` names what Best would take next.
- **The Smelter** burns fuel on top of its ore: it starts a cycle only with fuel at hand, and its
  cycle advances only while a unit burns, taking the next when one burns out mid-cycle (a 16 s
  cycle uses 0.4 Coal or 1.6 Wood). A finished cycle waiting for room burns nothing.

### Buffers

- Extractors (Mine, Lumber Camp, Oil Well) have a 20-unit output buffer (any mix of the types they
  work). Processors (Smelter, Refinery) have a 10-unit output buffer and an input buffer of 5
  cycles' worth of each recipe input. The HQ, hubs, and storage buildings have none.
- `Building.Output` and `Building.Input` hold the amounts by type; a building keeps them when it
  goes offline or online.

### The HQ and the Silo

- **The HQ** is placed when the world is made (`ResetWorld`), facing South, so its front faces a
  player south of it: on a generated world at the generator's `HqAnchor`, centered on the spawn
  point's X and ending 3 fine cells north of it, inside the spawn's clear Grassland with no field
  under it (see world-generation.md); on the test map at `TestMapHqAnchor` (23, 16), clear of the
  tests' clear spot; on the empty map at `EmptyMapHqAnchor` (56, 2), its north-east corner, clear
  of the center where tests and scenarios build. It cannot be switched off or destroyed, is never
  unlocked, and is not in the build menu.
- **A Silo** holds one Bulk resource (`Building.SiloResource`, Wood when placed).
  `SetSiloResource(id, type)` changes it, moving its step to the new resource at once; it refuses
  anything but a Silo and a Bulk resource.

### Production

- Each tick, every building steps. A collector (`IsCollector`) works only while **online**:
  offline it does nothing, and going offline drops the unit in hand (`Progress` back to 0, as
  switching off does), so it starts a whole unit over once online again. Its buffer keeps what it
  holds, which the player can still collect by hand (resources.md); trucks serve only online
  collectors.
- An extractor works one field at a time: its current field, or if that is gone, the nearest
  non-depleted field of any type it works within its work area (distance from its footprint center
  to the field's square, at most the work radius). It works toward one unit of that field's type
  (`Progress`, in seconds, reset when it takes up a new field), puts the unit into its **output
  buffer** at the gather time over its speed (collectors never deliver to storage themselves;
  the player collects by hand, resources.md), and clears its current field when that
  field depletes. When the buffer has no room for the unit, the unit waits at the field: progress
  holds at the gather time and the unit is taken as soon as there is room. Several extractors may work the same field. With no
  field in range, it idles, and it picks up a field as soon as one is in range.
- A processor, when idle, starts a cycle only when its inputs are at hand and its outputs have
  somewhere to go. Inputs come from its input buffer first, then (online only) from storage.
  Outputs go into storage when it is online and they fit (Cargo outputs together in the pool), else
  into its output buffer; offline, only into the buffer. A finished cycle with nowhere to put its
  output waits (progress holds at the cycle time). Each tick an online processor's output buffer
  drains into storage as far as room allows.
- Storage buildings do no work each tick; they only raise limits while they stand.
- Online buildings share the one storage. Moving raw resources from collectors to storage is the
  player's hand work, then Depot trucks', until research lets online collectors drain their
  buffers into storage (Material Transfer for Wood, Stone, Iron Ore, Copper Ore, and Coal; Advanced
  Material Transfer for the rest; research.md).

### The Research Lab

| Building | Footprint | Cost | Tier | Draws |
|---|---|---|---|---|
| Research Lab | 4 x 5 | 40 Stone, 30 Wood, 30 Iron | 1 | 10 Power |

- It does no work of its own: while switched on and online it adds to the research speed
  (`BuildingDef.Researches`; research.md). Its State is "Idle: No research running" with no active
  project.

### Switching off, status, and destroying

- `Building.Enabled` is true when placed. `SetBuildingEnabled(id, enabled)` switches a building.
  A switched-off building does nothing, and switching off drops the work in hand: progress goes
  back to 0, and a processor's running cycle ends with its inputs returned to the stockpile
  (fuel, once buildings burn it, will not be). Switched back on, it starts a whole unit or cycle
  over.
- `BuildingStatusOf(building)` gives a `BuildingStatus`: a state (`Working`, `Idle`, `Off`) and,
  when not working, a reason. Off reads "Switched off". An offline extractor is idle "Offline:
  outside the network" (a powered one reads "No Power: offline" first). An extractor works while it has a field
  or one in range to take up and buffer room for its unit; with no field it is idle with "No fields
  in range", and with a full buffer "Full: waiting for pickup". A processor whose outputs have
  nowhere to go, before a cycle or with a finished one waiting, is idle "Storage full: Iron" online
  or "Full: collect by hand" offline; otherwise it works during a cycle, else is idle "Waiting for"
  its missing inputs ("Waiting for Crude Oil", and offline "Waiting for Iron Ore (load by hand)").
  A consumer that is offline is idle "No Power: offline" (naming the first type it draws, in
  `CapacityType` order), and in a blackout of a type it draws "Blackout: Water" (checked first). A
  fuel burner with no fuel at hand is idle "Waiting for fuel" (offline "Waiting for fuel (load by
  hand)"); an offline provider is idle "Offline: supplies nothing", and an eating provider with
  nothing to eat "Waiting for Stone". A collector without a field works until its buffer is full
  ("Full: waiting for pickup"); a Forester always reports Working. Storage
  buildings and hubs report Working (they do no work).
- A permanent building (the HQ) cannot be switched off or destroyed: `SetBuildingEnabled` and
  `TryDestroyBuilding` return false for it.
- `TryDestroyBuilding(id)` removes a building, frees its cells, and adds its refund
  (`DestroyRefund`) to the stockpile, even past a storage limit, together with everything in its
  buffers: half of each cost amount, rounded up (a Mine's 25 Wood and
  15 Stone return 13 Wood and 8 Stone), plus the inputs of a processor's running cycle in full.
  Fuel, once buildings burn it, will not be refunded. The refund follows the type, so a building
  placed for free through the debug tools refunds the same.
- `BuildingAt(x, z)` finds the building covering a fine cell, and `BuildingById(id)` finds one by
  id.

### Ship launch (win condition)

- The ship bill, `Simulation.LaunchCost`, is 4 Hull Sections, 1 Engine, 1 Control Module, 500
  Rocket Fuel, and 200 Liquid Oxygen. `LaunchBillMet` is true when storage holds all of it.
- A ship launches from a Shipyard: `LaunchShipyard` is the first switched-on, online Shipyard, or
  null. `CanLaunchShip` is true when the bill is met and there is one. `TryLaunchShip` consumes the
  bill and increments `ShipsLaunched`. The player launches with the Launch ship button in a Shipyard's panel
  (presentation.md), enabled while that Shipyard is switched on and online and the bill is met. This is the current win that closes the initial loop.

### Rendering and interaction

- `BuildingViews` renders each building's model (`BuildingModels`, files in
  `src/Telaelus/assets/models/buildings/`), placed at the footprint center and turned to its facing.
  Each model is authored facing North with its front toward -Z, fills its footprint minus a 0.1
  inset per side, and carries its type's accent color: Mine (a headframe over a shaft, ore cart at
  the front, orange), Lumber Camp (a plank shed, log pile and saw bench at the front, brown),
  Smelter (a stone furnace with a chimney and a glowing mouth at the front, red), Refinery (two
  tanks joined by pipes, a control hut at the front, blue), Oil Well (a pumpjack whose horsehead
  hangs over a wellhead at the front, with a crank, a counterweight, and a small tank, yellow and
  black), HQ (a hall with a teal band and an entrance at the front, a landing pad before it, and a
  comms tower with a dish at the back, teal), Silo (a tall round silo with a conical cap and a
  ladder at the front, green), Stockyard (a fenced yard of stone, ore, and log heaps, open at the
  front, purple rails), Tank Farm (three banded tanks joined by pipes, a valve box at the front,
  cyan), Warehouse (a gabled shed with a roller door at the front and crates beside it, pink roof),
  Vault (a squat armored bunker with a round door at the front and gold hazard bands), Small Hub (a
  short mast on a square base with a glowing hub-green ring), Relay (a tall lattice tower with a dish
  and the same glowing ring), Water Wheel (a paddle wheel on an axle between two stone piers on the
  banks, a generator housing on one, blue), Burner Generator (a banded boiler with a chimney, a
  coal hopper at the front, and a yellow junction box, orange-red), Lumber Mill (a saw hall with a
  yellow powered stripe, logs feeding in at the front, and a plank stack), Drill Rig (a steel
  derrick over a drill head, an orange motor house at the front with a yellow band, an ore pile),
  Water Pump (a plank pump house with a light-blue roof, an intake pipe bending down off the front,
  and a hand-wheel valve), Pumping Station (a pump hall with two intake pipes off the front, a
  banded water tank, light blue), Sand Collector (a hopper on four legs over a sand pile with a
  chute to the front, tan), Sand Dredge (a scoop wheel over a sand pit at the front, a conveyor up
  to a hopper, a motor house, tan), Glassworks (a long works: a stone furnace with a glowing kiln
  mouth at the back, a cooling line of panes down its length under an open roof frame, and a rack
  of finished panes at the front, pale cyan), Depot (a garage across the back half with two roller
  doors and a fuel pump between them, tyres behind, and an apron painted with four bays at the
  front, red trim); the refining, renewable, and parts buildings are described in art-style.md;
  every powered building also has a wooden power pole with a yellow band. The
  truck (a unit, not a building) is a small flatbed, 1.6 long and 0.8 wide, with a red cab and an
  empty bed the presentation sets the cargo on. The front
  marks the facing, so a
  rotated building reads as rotated.
- `BuildController` drives build mode: a building is selected from the build menu (B; see
  presentation.md), and only an unlocked one can be (`Select`); Escape cancels; R rotates the
  facing. The ghost is a translucent copy of the selected building's
  model, turned to the chosen facing, over a thin plate covering the exact footprint; it is placed
  at the anchor whose footprint center is nearest the mouse's ground point, and every surface of
  it is tinted green when placeable and affordable, red when not. Left-click issues the placement command.
- The pointer is the viewport mouse position unless `PointerOverride` holds a world ground point
  (set by scenarios); that point is projected to the screen each frame and then goes through the
  same screen-to-ground ray, so aiming stays fixed on the ground while the camera moves.
  `CurrentGhost()` reports the ghost (anchor, size, center, placeability, fields in range) for the
  current pointer, and `ResetBuildMode()` returns to no selection, facing North, and the real mouse
  (used by the `initMap` testing command).
- Outside build mode, the placed building under the pointer gets a thin accent outline (a
  frame of four bars 0.14 units wide just outside its footprint, unless the pointer is over UI), and a left click on it opens its detail
  panel (see presentation.md); a left click on the world that hits neither a building nor a
  resource field (which opens its own panel, see resources.md) closes a panel. The building the detail panel shows (`Focused`) keeps its outline until the panel
  closes, so the player can tell which one they are looking at.
- For an extractor, the ghost also shows its work area as a flat translucent disc of the work
  radius around the footprint center and highlights each field in range of a type it works.
- **Line tools.** The build menu's Road and Bridge cells select a line tool (`BuildController.Line`,
  `LineTool`) instead of a building. The left button pressed on a cell starts a drag; its release
  lays the line from there to the cell under the pointer (`TryLayRoad`, `TryLayBridge`; terrain.md).
  Shift with the left removes instead. The line tool takes only the left button (without Ctrl), so
  a right drag still pans and a right click still walks. Pressing the left button during a drag,
  or Escape, cancels it; Escape again leaves build mode. The ghost is a plate on each tile of
  the drag (or the tile under the pointer before one starts): green to lay, amber where the stock
  runs out, red where it cannot go, and a remove tint on what a removing drag would take. The
  placement tooltip names the tool and, during a drag, the tiles and cost ("Road: 7 tiles, 28
  Stone", short amounts marked) or what is wrong with a bridge, and the keys ("Drag to lay
  Shift-drag to remove   Esc cancel").
- The build menu lists every building (discovered first, then undiscovered silhouettes), and
  while placing, a tooltip beside the pointer shows the building's name, its cost, and for an
  extractor, the count of fields in range. A cost amount the stockpile is short of shows what it
  holds, as "15 Stone (have 3)". See presentation.md and ui-style.md.

## Related

- resources.md (stockpile, fields, range), simulation.md (commands, grid), presentation.md (views,
  screen UI, build UI).
