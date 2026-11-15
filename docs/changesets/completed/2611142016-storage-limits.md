# Storage Limits

- Status: Implemented
- Id: 2611142016-storage-limits
- Depends-on: none

## Summary

Gives every resource from 2611141930-resource-catalog a storage class and a limit, and adds the
storage buildings: a Silo (one chosen resource), a Stockyard (solids), a Tank Farm (fluids), a
Vault (radioactive and precious), and a Warehouse (a shared pool for manufactured goods, measured
in Cargo). A free HQ beside the spawn gives base capacity for the starting resources only.
Gathering and production stop at the limit with "Storage full", and the top bar shows pinned
resources as amount over limit.

## Motivation

The stockpile is unlimited today, so nothing pushes the player to build anything but producers.
Storage is the first follow-on of 2611141757-resource-tree (section 4 and decisions 1, 2, and 10
there), built on the full catalog that 2611141930-resource-catalog added. It makes the launch
a storage goal as well as a production one, makes the oil chain start with a Tank Farm, and places
the HQ that the network changeset will root its hubs at.

## Design

### Scope

- **In:** storage classes, limits, and Cargo weights for every resource; the HQ and the five
  storage buildings; full-storage rules; top bar pins; limits and pins in the inventory.
- **Out:** local buffers, collecting by hand, and online or offline (hq-network). Until it lands,
  every building works on the one storage as today.

### Storage classes

`ResourceDef` gains a **storage class**:

| Class | Limit | Resources |
|---|---|---|
| Bulk | per type | Wood, Stone, Iron Ore, Copper Ore, Coal, Bauxite, Limestone, Quartz, Titanium Ore, Sulfur, Lithium, Charcoal |
| Fluid | per type | Crude Oil, Kerosene, Rocket Fuel, Hydrogen, Liquid Oxygen, Sulfuric Acid |
| Secure | per type | Rare Earths, Uranium Ore, Crystals, Enriched Uranium, Fuel Rod |
| Cargo | one shared pool | every other tier 1 to 4 resource |
| None | unlimited | Sand, Water (they become Capacity in terrain-capacity) |

- A per-type limit is the sum, over the HQ and every standing storage building, of what each adds
  for that type.
- The Cargo pool's size is the sum of what the HQ and Warehouses add. Each Cargo resource has a
  **Cargo** weight, and the room for it is the pool's free Cargo divided by its weight.

### Storage buildings (placeholders for tuning)

| Building | Footprint | Cost | Adds |
|---|---|---|---|
| HQ | 6 x 10 | free, placed at spawn | the base (tables below) |
| Silo | 2 x 2 | 30 Wood | its chosen Bulk resource's Silo step |
| Stockyard | 3 x 3 | 40 Wood, 20 Iron | every Bulk resource's Stockyard step |
| Tank Farm | 3 x 3 | 30 Iron, 20 Stone | every Fluid resource's Tank Farm step |
| Warehouse | 3 x 3 | 40 Wood, 30 Stone | 200 Cargo |
| Vault | 3 x 3 | 40 Steel, 40 Stone | every Secure resource's Vault step |

**Bulk**

| Resource | HQ base | Silo step | Stockyard step |
|---|---|---|---|
| Wood, Stone | 100 | 300 | 200 |
| Iron Ore, Copper Ore, Coal, Charcoal, Limestone | 60 | 200 | 120 |
| Bauxite, Quartz, Titanium Ore, Sulfur | 0 | 150 | 80 |
| Lithium | 0 | 100 | 50 |

**Fluid**

| Resource | HQ base | Tank Farm step |
|---|---|---|
| Crude Oil, Rocket Fuel, Liquid Oxygen | 0 | 300 |
| Kerosene | 0 | 200 |
| Hydrogen | 0 | 200 |
| Sulfuric Acid | 0 | 150 |

**Secure**

| Resource | HQ base | Vault step |
|---|---|---|
| Rare Earths | 0 | 50 |
| Uranium Ore, Crystals | 0 | 30 |
| Enriched Uranium, Fuel Rod | 0 | 20 |

**Cargo weights** (the HQ gives a 150 Cargo pool)

| Weight | Resources |
|---|---|
| 0.25 | Copper Wire, Circuit, Silicon Wafer, Processor |
| 0.5 | Plastic, Silicon, Rubber, Carbon Fiber, Insulated Cable, Magnet, Ceramic Tile |
| 1 | Iron, Copper, Steel, Glass, Lumber, Cement, Aluminum, Titanium, Machine Parts, Battery, Solar Cell |
| 2 | Concrete, Hull Plate, Electric Motor, Composite Panel, Computer |
| 3 | Steel Beam, Aluminum Frame, Armor Plate |
| 4 to 10 | Control Module 4, Sensor Array 4, Heat Shield 6, Life Support Unit 6, Thruster Assembly 8, Fuel Tank 10 |
| 20 and up | Hull Section 20, Engine 25 |

What the HQ base means for play:

- The HQ stores only the starting materials (Wood, Stone, Iron Ore, Copper Ore, Coal, Charcoal,
  Limestone) and a small Cargo pool. Every other resource needs its storage building first: a
  resource with a limit of 0 cannot be gathered or produced.
- **The oil chain starts with a Tank Farm**: an Oil Well stays "Storage full: Crude Oil" until one
  stands, and the launch's 500 Rocket Fuel needs two.
- Every building cost that exists today (Wood, Stone, Iron) fits within the HQ's base, so storage
  never locks a building. The Vault needs Steel, so it stays locked until Steel is made.
- The Silo is the first storage building (Wood only); the Stockyard and Tank Farm need Iron, so
  they follow the Smelter. Storage buildings need no power at any tier.

### The HQ and the Silo

- **The HQ** is placed automatically when the world is made, 6 x 10 fine cells (1.5 by 2.5 map
  cells), facing South with its front toward the player: on a generated world centered on the
  spawn point's X and ending 3 fine cells north of it, wholly inside the spawn's clear Grassland,
  with no field generated under it; on the test map at (23, 16), clear of the tests' clear spot;
  and on the empty map at (56, 2), its north-east corner, clear of the center where tests and
  scenarios build. The empty map needs one too, or every limit there would be 0. It cannot be
  switched off or destroyed, is never unlocked, and is not in the build menu.
- **The Silo** holds one Bulk resource, chosen in its detail panel with a picker of the Bulk
  resources; a new Silo holds **Wood** until changed. Changing it moves its step to the new
  resource at once.

### When storage is full

Gathering and production respect the limit; nothing is ever destroyed to make it fit.

- **Hand-harvesting:** while the target's resource is at its limit, progress does not advance, and
  the hint shows "Storage full" in place of the progress bar.
- **Extractors:** progress stops at a whole unit; the unit is taken from the field only when there
  is room. Status: Idle, "Storage full: Wood". Progress beyond a unit still carries over when there
  is room, so gather timings are unchanged.
- **Processors:** start a cycle only when its outputs would fit; if room runs out during the cycle,
  the finished output waits in the building until it fits. Status: Idle, "Storage full: Iron",
  which shows before "Waiting for" its inputs, since it blocks even with the inputs in hand.
- **Room** is fractional for Cargo resources (50 free Cargo is room for 2.5 Hull Sections); a unit
  goes in only when a whole one fits.
- **Returns ignore limits.** A destroy refund and the inputs a switched-off processor returns are
  added in full, even past a limit, so nothing the player owns is lost.
- **Over the limit.** A type can sit above its limit (after a refund, destroying a storage
  building, or changing a Silo). Nothing is removed; gathering and production of that type wait
  until it falls below the limit. Destroying a storage building is always allowed.
- `Debug.SeedStockpile` ignores limits, so scenarios that seed a launch keep working.

### UI

- **Top bar:** shows **pinned** resources as "amount / limit" instead of every raw resource. The
  default pins are today's eight raw resources; all eight fit beside the launch progress at
  1280 x 720 (checked in a windowed run), so the fallback to six was not needed. Pins keep table
  order. An entry with no room for another unit shows in a new `Warning` color (the power
  changeset reuses it for brownout), so a resource with no storage yet ("0 / 0" Crude Oil) shows
  it too. Sand and Water, and pinned Cargo resources, show their amount alone.
- **Inventory panel (I):** a Cargo row above the tier headings ("Cargo used / pool"); each
  discovered row shows "amount / limit" (amount alone for Cargo resources and Sand and Water) and a
  Pin toggle ("Pin" or "Pinned"). Pins are presentation state for the session (no saves exist
  yet).
- **Building detail panel:** a storage building lists what it adds under "Adds to storage" (one
  line per resource with a step above 0, plus "Cargo pool" for the HQ and a Warehouse); the Silo
  adds its "Holds" picker. Storage buildings show no Power, State, progress, or Turn off, since
  they do no work to switch off (a switched-off store would still hold its stock); the HQ also has
  no Destroy.
- **Build menu:** Silo, Stockyard, Tank Farm, Warehouse, and Vault, with the usual unlock rules.

## Code changes

- `ResourceDefs`: `StorageClass` (Bulk, Fluid, Secure, Cargo, None), `Cargo` weight, and the
  per-type HQ base and Silo, Stockyard, Tank Farm, and Vault steps.
- `BuildingType` and `BuildingDefs`: HQ, Silo, Stockyard, Tank Farm, Warehouse, Vault, with a
  definition field for what each adds and flags for not destroyable, not switchable, and not
  buildable (HQ).
- `Building`: `SiloResource` (a Bulk type, default Wood).
- `Simulation`: `StorageLimit(type)`, `CargoUsed`, `CargoPool`, `RoomFor(type)`; gathering,
  extractors, and processors check room (the processor gains a waiting-output state);
  `SetSiloResource(id, type)`; HQ placement in `ResetWorld` for both world kinds; `BuildingStatus`
  reasons for "Storage full".
- `WorldGenerator`: the HQ's footprint beside the spawn, kept clear of fields like the spawn.
- Presentation: `ResourcePins` shared by the top bar and the inventory; top bar pins with amount
  over limit and the full color; the inventory's Cargo row, limits, and Pin toggles; storage rows
  and the Silo picker in the building detail panel; the `Warning` token in `UiTheme`; the hint's
  "Storage full" state; the build menu listing only buildable types; `BuildingModels` mapping Tank
  Farm to `tank_farm`.
- Testing: snapshot fields (`topBar.items` `limit` and `full`, `hint.storageFull`, inventory rows'
  `limit` and `pinned`, `cargo`, the detail panel's `adds`, `destroy` null when hidden); the
  schema's building list; `SimTestHelpers.Placed` (buildings other than the HQ); existing tests and
  scenarios updated for the HQ (building indexes, counts) and for Tank Farms where they make Crude
  Oil or Rocket Fuel.
- Assets: generated building models for the HQ, Silo, Stockyard, Tank Farm, Warehouse, and Vault
  (`building_gen.py`), each in its own accent color (`hq_accent` teal, `silo_accent` green,
  `stockyard_accent` purple, `tank_farm_accent` cyan, `warehouse_accent` pink, `vault_accent`
  gold), required like every model today, with their kept preview sheets.

## Blueprint updates

- **resources.md:** storage classes, limits, and Cargo weights in the resource table; full-storage
  and over-limit rules; the stockpile becomes "storage"; the top bar shows pinned resources.
- **buildings.md:** the six storage buildings and their table rows, the HQ's placement and
  restrictions, the Silo's picker, processors waiting on output room, the new status reasons, and
  that refunds ignore limits.
- **presentation.md / ui-style.md:** top bar pins and the full color, the inventory's Cargo row,
  limits, and pins, storage panel rows, the Silo picker, the `Warning` token, the "Storage full"
  hint.
- **world-generation.md:** the HQ's footprint beside the spawn.
- **art-style.md:** the six new accent colors.
- **testing.md:** new tests and scenarios.

## Verification

- Unit tests: every resource has a storage class, and every Bulk, Fluid, or Secure resource has its
  class's step and every Cargo resource a weight; limits sum the HQ and buildings; a limit of 0
  blocks gathering and production; a Silo's step follows its chosen resource; Cargo room divides by
  weight; hand-harvest, extractors, and processors stop at the limit and resume when room appears;
  a processor holds finished output until it fits; refunds and returned inputs pass the limit;
  destroying a storage building leaves stock over the limit untouched; the HQ exists at start in
  both world kinds, is clear of fields, and cannot be destroyed or switched off; every building
  cost fits the HQ's base.
- Scenarios: a `storage` scenario fills Wood to its limit and checks the "Storage full" hint and
  the top bar color, builds a Silo and sees the limit rise, and pins a resource from the
  inventory. The existing scenarios pass with the HQ present (the oil and launch scenarios gain a
  Tank Farm or seed past the limit).
- `dotnet test src/Game.Sim.Tests` and `dotnet test src/Game.Integration.Tests` pass; a windowed
  run shows the HQ beside the spawn, the new buildings, and the top bar at 1280 x 720.
