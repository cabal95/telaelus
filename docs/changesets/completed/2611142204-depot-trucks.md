# Depot Trucks

- Status: Implemented
- Id: 2611142204-depot-trucks
- Depends-on: none

## Summary

Adds the Depot and its trucks: cheap haulers, faster than the player on foot, that empty
collectors' buffers into storage on their own. A truck drives to the collector with the fullest buffer, loads one resource, drives to
the HQ, unloads, and repeats. Trucks serve every collector on the map, online or offline, with no
radius or route drawing, and find their way over walkable ground with a grid path search in the
sim. The player stops being the only truck.

## Motivation

The fifth follow-on of 2611141757-resource-tree (section 7 there, "Trucks", and decision 12). Since
the HQ network changeset, raw resources reach storage only when the player holds E beside a
collector. That chore is the intended start ("the player is the first truck"), but it does not
scale past a handful of Mines. Trucks are the next step of the progression (hand collection, then
trucks, then Material Transfer over the network), and the first moving units in the sim.

## Design

### Scope

- **In:** the Depot building and its model; trucks as sim units with their model; buying trucks at
  a Depot; dispatch; a path search over walkable fine cells; loading and unloading; truck states and
  their UI (the Depot panel, a collector's panel row, a truck hover); a `trucks` scenario.
- **Out:** roads (a speed bonus) and bridges (crossing water), drop points other than the HQ,
  hauling to processors (kept as a tuning lever in the plan, not built), truck upgrades (capacity,
  speed), truck fuel, Material Transfer (the research changeset), and trucks blocking or being
  blocked by the player or each other.

### The Depot (placeholder numbers)

| Building | Footprint | Cost | Holds | Tier |
|---|---|---|---|---|
| Depot | 4 x 4 | 40 Wood, 20 Stone | up to 4 trucks (1 to start) | 0 |

- A Depot holds trucks and draws no power. **It must be online** (connected to the HQ through
  hubs) for its trucks to take work: they still serve collectors anywhere, but are dispatched
  from the network. Offline, it is idle "Offline: trucks stay parked" with the usual offline ring;
  a truck already on a trip finishes it and then parks, as when the Depot is switched off.
- **A new Depot comes with 1 truck,** parked in its first bay.
- **Buying a truck:** the Depot panel has a "Buy truck" button (15 Iron, 10 Wood), enabled while the
  Depot has fewer than 4 and the cost is in storage. A new truck appears parked in the Depot's next
  free bay.
- **Switched off,** a Depot's trucks finish the trip they are on (unloading at the HQ) and then
  drive home and park. Switched back on, they take work again.
- **Destroyed,** a Depot's trucks go with it: each refunds half its cost like a building, and any
  cargo returns to storage like a refund (even past a limit, as returns do today). The truck it
  came with refunds nothing, since it was part of the Depot's cost.

### Trucks (placeholder numbers)

- A truck carries **one resource at a time**, up to **20 units**, at **8 units per second** on
  grass (faster than the player, who walks at 6), times the terrain speed under it (Sand 0.75, and
  so on), like the player. Trucks drive through the player and each other.
- **Dispatch:** an idle truck takes the collector whose buffer holds the most (in total), among
  collectors holding at least **10 units** (half a load) or full, that no other truck is heading
  to and that it can reach; ties go to the nearest. "Collector" means an extractor today (Mine,
  Lumber Camp, Oil Well, Lumber Mill, Drill Rig). Processors' buffers are left to the player.
- **The trip:** drive to a walkable cell beside the collector's footprint; **load** the buffer's
  most-held resource, up to 20, over 2 seconds; drive to a walkable cell beside the HQ; **unload**
  over 2 seconds into storage, as much as fits. With no room left, it waits at the HQ with its
  load ("Waiting: storage full (Iron Ore)") and unloads as room appears. Then it is idle again and
  dispatches, or, with nothing to do, drives home to its Depot and parks.
- **Heading home dispatches:** a truck on its way home takes new work from where it is, so after
  unloading at the HQ it goes straight to the next collector when one is waiting.
- **No way to the HQ:** a truck that reaches a collector but finds no path on to the HQ takes
  nothing and counts the collector unreachable, so nothing it loads can be stranded.
- **Changes on the way:** if the collector is destroyed or its buffer emptied (the player collected
  it) before the truck arrives, the truck drops the job and dispatches again. If a placed building
  now blocks the path, the truck searches again from where it is.
- **Unreachable:** a collector no path reaches (across a river, on an island) is skipped by
  dispatch; its panel says so.
- Load and unload times stand in for animation and keep a truck from being a teleporter at short
  range. A truck moves 20 units a round trip: on a 40-unit haul (80 there and back, 10 s, plus 4 s
  loading and unloading) that is about 1.4 units a second, enough for some fifteen base Mines, so a
  Depot's trucks are generous early and long hauls are what call for more.

### Paths

- Trucks path in the **sim**, over fine cells, with A* (8 directions, no corner cutting, diagonal
  steps cost the square root of 2, each step weighted by the inverse terrain speed so trucks prefer
  grass to sand). A cell is passable when it is walkable (`IsWalkable`: walkable terrain, no
  blocking feature, no field, no building). The search is bounded to cells already generated, and
  a path is a list of cell centers the truck follows at its speed.
- This replaces the game plan's "Godot NavigationServer navmesh" for trucks: the sim is
  authoritative and engine-free, the walk rules already live there per fine cell, and a sim path
  is unit-testable and the same for every future client. Paths are searched only at dispatch, on
  arrival at the collector, and when a step is blocked, so the cost stays off the per-tick path.
  (See the open questions.)

### UI

- **Trucks on the map:** each truck is a model placed at its sim position each frame, turned
  smoothly to its heading, with its cargo on the bed while loaded (below). Hovering one (within 28
  pixels) shows the placement tooltip outside build mode: "Truck" over its state ("To Mine: Iron
  Ore", "Loading", "To HQ: 20 Iron Ore", "Unloading", "Waiting: storage full (Iron Ore)",
  "Heading home", "Parked").
- **Depot panel:** a Trucks row ("2 / 4"), a row per truck (Truck 1 to Truck 4) with its state,
  and a "Buy truck (15 Iron, 10 Wood)" button, disabled when it cannot buy, over a note of what is
  short ("15 Iron (have 3)") or "Every bay is full".
- **Collector panel:** a Truck row while one is on its way or loading ("On its way", "Loading"),
  "No truck can reach" when every truck has found no path to it, and nothing otherwise.
- **Placement tooltip** for a Depot shows nothing new; the build menu lists it like any building.

### Assets

Two models, each with an accent color, kept preview sheets, and README rows:

- **Depot (4 x 4):** a garage across the back half (4 wide, about 1.7 deep, 1.3 tall) with two
  roller doors facing the front and a `depot_accent` band and roof trim; across the front half a
  paved apron painted with four parking bays (white lines, about 0.95 wide and 1.8 deep each), where
  parked trucks sit nose out; a fuel pump and a stack of tyres at one front corner
  (`depot_accent`, a bright red: the orange-reds were taken by the Burner Generator and the Mine).
- **Truck:** a small flatbed, **1.6 long, 0.8 wide, about 0.85 tall** (a fine cell is 1 unit; the
  player stands about 1.4), so four park side by side in a Depot's bays and one reads at a glance
  beside a 2 x 2 Mine. Authored facing North (the cab toward -Z), origin at its center on the
  ground:
  - a dark chassis on four wheels (radius 0.17, dark rubber with metal hubs);
  - a cab over the front third in `depot_accent`, with a dark windshield, side windows, and pale
    headlights;
  - a flat metal bed over the back two thirds with low side rails;
  - **cargo** is not baked in: the view puts the carried resource's item model on the bed, scaled
    to the bed (about 0.6 across), and hides it when empty, so every resource reads by the item
    icon players already know.
  The truck is not a building: it has its own folder (`assets/models/units/`), its own preview
  sheet, and a README there.

## Code changes

- `BuildingType.Depot` and its definition (`TruckSlots` 4, `TruckCost`).
- A `Truck` class (id, home Depot, position, heading, state, cargo type and amount, target
  building, path) and a `TruckState` enum (Parked, ToCollector, Loading, ToHq, Unloading,
  WaitingForRoom, ToDepot).
- `SimulationTrucks` (partial `Simulation`): `Trucks`, `TryBuyTruck(depotId)`, dispatch, stepping
  trucks after buildings each tick, loading and unloading, refunds on destroy, and
  `TruckFor(collector)` / `NoTruckCanReach` for the collector row; the offline Depot's idle
  reason.
- `SimulationPaths`: A* over fine cells and "a walkable cell beside a footprint".
- Presentation: `TruckViews` (models, cargo, hover lookup), `TruckText`, the Depot panel's rows
  and button, the collector's Truck row, the truck tooltip in `GameRoot`.
- Testing: snapshot `trucks` (id, depot, bay, state, position, cargo, amount, target),
  `ui.worldTip`, `ui.buyTruck`, and the detail panel's new rows; a `fillBuffer` scenario command
  (`Debug.FillBuffer`); `TruckTests` and `PathTests`; a `trucks` scenario; the schema's building
  list; the build menu's new cell.
- Assets: the Depot model and the truck model (a new `unit_gen.py` generator and the
  `assets/models/units/` folder), previews, README rows, the palette entry.

## Blueprint updates

- **buildings.md:** the Depot, trucks, dispatch, the trip, and destroy and switch-off rules.
- **buildings.md** also gains a Paths section (paths over walkable cells).
- **presentation.md / ui-style.md:** truck views and hover, the Depot panel, the collector row.
- **art-style.md:** the new accent.
- **testing.md:** new tests, snapshot fields, and scenario.
- **game-plan.md:** trucks path in the sim on the fine-cell grid instead of a navmesh, and are
  faster than the player rather than slow.
- **asset-pipeline.md:** the units folder and generator.

## Verification

- Unit tests: A* finds the shortest path around a building and a river and returns none to an
  island; prefers grass to sand when that is cheaper; buying a truck costs and caps at 4; dispatch
  picks the fullest buffer at or over 10, skips one already taken and one unreachable, breaks ties
  by distance; a truck loads one resource up to 20 and unloads into storage; waits when storage is
  full and finishes as room appears; drops a job when its collector is destroyed or emptied;
  re-paths around a building placed in its way; parks when switched off after its trip; a
  destroyed Depot refunds its trucks and their cargo; an offline collector is served the same as an
  online one.
- Scenario: a `trucks` scenario places a Mine away from the network, a Depot, and buys a truck;
  waits for the buffer to reach 10, sees the truck go out, load, return, and Iron Ore arrive in
  storage; checks the Depot panel's rows and the collector's Truck row; takes screenshots of the
  truck loaded on the way back.
- Both test suites pass; a windowed run shows the Depot, a moving truck, and its cargo.

## Decisions

1. **Paths are searched in the sim** on the fine-cell grid, not with the NavigationServer.
2. **Trucks are bought at the Depot,** up to 4, and a new Depot comes with 1.
3. **Dispatch takes a buffer at 10 units** (or full).
4. **One changeset** for paths, trucks, and the Depot.
5. **Trucks burn no fuel** for now.
6. **The Depot is 4 x 4,** and trucks drive at **8 units per second**, faster than the player.
7. **The Depot must be online** for its trucks to take work (decided after review).
