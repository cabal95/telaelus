# Resource Catalog

- Status: Implemented
- Id: 2611141930-resource-catalog
- Depends-on: none

## Summary

Adds every Stored resource in the catalog of 2611141757-resource-tree as a `ResourceType`, 63 in
all (53 new), each with its definition (name, kind, tier, and for a deposit its gather time) and an
item model and icon. Resources become **discovered** (a raw resource when the player first reveals
it on the map, anything else when storage first holds it), and the inventory lists every resource
by tier, discovered first and the rest darkened.

## Motivation

The resource-tree changeset decided the catalog; this makes it real so every later changeset
(storage limits, fields, recipes) builds on resources that already exist. It is split from
storage-limits to keep each commit reviewable: this one is mostly definitions and art.

## Design

### Scope

- **In:** the 53 new `ResourceType` values and their definitions, item models and icons,
  discovery, and the inventory listing.
- **Out:** storage classes and limits (storage-limits), fields for the new deposits
  (field-rebalance), recipes and the buildings that make new items (production tiers), and Power
  (a Capacity resource, not a `ResourceType`; power changeset). Until those land, the new resources
  exist but nothing produces them.

### The resources

`ResourceDef` gains a **tier**: 0 for raw, 1 to 4 as in the catalog. `ResourceKind` stays Deposit,
TerrainSourced, and Refined.

| Tier | Kind | Resources (new in bold) |
|---|---|---|
| 0 | Deposit | Wood, Stone, Iron Ore, Copper Ore, Coal, Crude Oil, **Bauxite, Limestone, Quartz, Titanium Ore, Sulfur, Rare Earths, Uranium Ore, Crystals** |
| 0 | TerrainSourced | Sand, Water, **Lithium** |
| 1 | Refined | Iron, Rocket Fuel, **Copper, Steel, Glass, Lumber, Charcoal, Cement, Concrete, Kerosene, Plastic, Hydrogen, Liquid Oxygen, Aluminum, Silicon, Sulfuric Acid, Rubber, Titanium, Carbon Fiber, Enriched Uranium** |
| 2 | Refined | **Hull Plate, Steel Beam, Copper Wire, Circuit, Insulated Cable, Machine Parts, Silicon Wafer, Processor, Magnet, Electric Motor, Battery, Solar Cell, Composite Panel, Ceramic Tile, Aluminum Frame, Armor Plate** |
| 3 | Refined | **Control Module, Computer, Thruster Assembly, Fuel Tank, Heat Shield, Fuel Rod, Life Support Unit, Sensor Array** |
| 4 | Refined | **Hull Section, Engine** |

- The enum is ordered by tier, then catalog order, so tables and the inventory read in that order.
- New deposits' gather times (placeholders): Limestone and Sulfur 4.5 s, Bauxite and Quartz 6 s,
  Titanium Ore 7.5 s, and Rare Earths, Uranium Ore, and Crystals 9 s. Those last three are not
  hand-harvestable; the rest are.
- Nothing about the existing ten changes: their names, gather times, fields, and recipes stay.

### Discovery

The simulation records each resource the first time it is **discovered**; like unlocking a
building, it never clears during play.

- **A deposit** is discovered when a field of it first lies in an explored map cell (on reveal, or
  at start for cells already explored, which is every cell in a world without fog).
- **Sand** is discovered when a Sand cell is first explored, and **Water** when a ShallowWater or
  DeepWater cell or a river is.
- **Anything else** (Lithium and every Refined resource) is discovered when storage first holds
  any of it.
- Holding a resource always discovers it, so `Debug.SeedStockpile` does too.

The new deposits have no fields yet, so they stay undiscovered until field-rebalance places them.

### Inventory

- The inventory panel (I) lists **every** resource under tier headings (Raw, Tier 1 to Tier 4).
  Within a heading, discovered resources come first in table order, then undiscovered ones.
- A discovered row shows the icon, name, and amount as today. An undiscovered row is darkened the
  way the build menu shows an undiscovered building: the icon as a `Silhouette`, the label
  "Undiscovered" in `TextDim`, and no amount, so the player can see how much is left to find but
  not what it is.
- The panel scrolls when taller than the window allows.
- The top bar is unchanged here (storage-limits adds pins): it keeps exactly today's eight raw
  resources, now as an explicit list rather than every non-refined resource.

### Drawing the new resources

- **Fields of the new deposits** have no field models yet, and no world places them, but the debug
  tools can. Such a field draws its item model in every slot (about 0.6 units tall), so placing
  one never breaks the field views; field-rebalance gives each its own models.
- **Startup pictures in batches.** Drawing all 63 icons plus the field and building pictures in
  one frame ran Direct3D 12 out of sampler descriptors and stalled the windowed game. The picture
  stage now draws at most 16 stages at once across every caller, each batch over two frames.
- **Scenarios wait for the pictures.** Because the icons now take several frames, a windowed
  scenario holds its first command until the icons and pictures are drawn, so screenshots show
  them.

### Item models and icons

Every new resource gets an item model in `item_gen.py`, rendered at startup into its icon like
today's ten. Most come from a few shape families with their own colors, so 53 models stay cheap to
make and read as related:

| Family | Resources |
|---|---|
| Ore lumps | Bauxite, Titanium Ore, Rare Earths, Uranium Ore (with a faint glow color) |
| Gems and crystals | Quartz, Crystals |
| Heaps | Limestone, Sulfur, Lithium, Charcoal, Cement |
| Canisters and barrels | Kerosene, Hydrogen, Liquid Oxygen, Sulfuric Acid |
| Ingots and bars | Copper, Steel, Aluminum, Titanium, Silicon, Enriched Uranium |
| Stacks | Lumber, Hull Plate, Steel Beam, Ceramic Tile, Composite Panel, Armor Plate, Concrete (blocks), Glass (panes), Carbon Fiber (sheets) |
| Spools and rolls | Copper Wire, Insulated Cable, Rubber, Plastic (pellet sack) |
| Electronics | Circuit, Silicon Wafer, Processor, Solar Cell, Battery, Magnet |
| Parts | Machine Parts (gears), Electric Motor, Aluminum Frame, and a distinct model for each tier-3 and tier-4 part |

## Code changes

- `ResourceType`: the 53 new values, ordered by tier then catalog order (existing values keep
  their names).
- `ResourceDefs`: `Tier`, the new definitions, and the new gather times.
- `Simulation`: a discovered set and `IsDiscovered(type)`, filled on reveal (deposit fields in the
  newly explored cells, Sand and Water terrain), at start for already explored cells, and whenever
  storage first holds a resource; reset with the world.
- `item_gen.py` and `mesh.py`: the new models from shared family functions, and their palette
  colors (the new glowing ones added to `GLOW`); exported to `src/Game/assets/models/items`.
- Presentation: the inventory panel's tier headings, discovered-first order, darkened undiscovered
  rows, and scrolling; the top bar's explicit list; `NodeModels` falling back to the item model for
  a deposit without field models; `PictureStage.RenderOnce` drawing in shared batches of 16;
  `GameRoot.PicturesReady`.
- Testing: the snapshot's `discovered` list and the inventory rows' `section` (`raw`, `tier1` to
  `tier4`) and `discovered`; the scenario schema's resource list; the scenario runner waiting for
  the startup pictures when windowed.

## Blueprint updates

- **resources.md:** the full resource table (tier, kind, hand-harvest, gather time, source) and
  discovery.
- **presentation.md:** the top bar's starting resources; the inventory's tier headings, order,
  undiscovered rows, and scrolling.
- **ui-style.md:** the top bar's list; the picture stage's batches.
- **art-style.md:** the new palette colors and the item shape families.
- **world-generation.md:** a world reset clears discoveries.
- **simulation.md:** discovery as world state, reset with the world.
- **testing.md:** `DiscoveryTests`, the new `ResourceTests` checks, the `inventory` scenario, the
  snapshot's `discovered` and inventory fields, and windowed runs waiting for the pictures.

## Verification

- Unit tests: every `ResourceType` has a definition with a tier; deposits have gather times;
  revealing a cell with a field discovers its deposit; Sand and Water discover from terrain;
  holding a resource discovers it; a world without fog discovers every placed deposit at start;
  discovery resets with the world; every resource has an item model file.
- Scenario: an `inventory` scenario opens the panel on the empty map (nothing discovered, every
  row darkened through the last tier), then places a Quartz field in sight and seeds Steel, and
  checks both move to the top of their tiers. `hud` checks the new row order and fields.
- `dotnet test src/Game.Sim.Tests` and `dotnet test src/Game.Integration.Tests` pass; a windowed
  run shows the inventory with all 63 icons (via the debug seed) and the darkened rows.
