# Building Costs and Gather Rates

- Status: Implemented
- Id: 2611121827-building-costs-and-gather-rates
- Depends-on: none

## Summary

Makes gathering slow and buildings cost resources, so the game plays like a survival game: the
player starts with nothing, gathers by hand one unit at a time over several seconds, and works
up through the buildings as their materials become available. A base extractor gathers a little
slower than the player; its benefit is that it works unattended, and from the second extractor
on, the buildings out-gather the player.

## Motivation

Today the player hand-harvests 10 units a second, an extractor pulls 5 a second (slower than the
player), and every building is free from the first second. Nothing has to be earned, so there is
no progression. Gather rates and building costs balance against each other, so they change
together.

## Design

### Gathering one unit at a time

Gathering becomes discrete: a gatherer works toward one whole unit and adds it to the stockpile
when the unit's time is up. Each deposit has a **gather time**, the seconds to gather one unit
by hand, which reads as how hard the material is to work:

| Deposit | Gather time (by hand) | Base extractor (x0.75) | Extractor |
|---|---|---|---|
| Wood | 3 s | 4 s | Lumber Camp |
| Stone | 4.5 s | 6 s | Mine |
| Coal | 4.5 s | 6 s | Mine |
| Iron Ore | 6 s | 8 s | Mine |
| Copper Ore | 6 s | 8 s | Mine |
| Crude Oil | 3 s (no hand gathering) | 4 s | Oil Well |

- **By hand.** While the player holds E on a field, progress toward the next unit grows; at the
  gather time one unit moves from the field to the stockpile and progress starts over. Letting
  go of E, or the target changing (walking away, or a nearer field), drops the partial progress.
  The HUD shows the progress as a bar beside the `[E] harvest` hint.
- **By extractor.** Each building has a **speed**; every building today is base level, speed
  0.75, slower than the player on purpose. An extractor's time per unit is the deposit's gather time divided by its speed (the third
  column). Progress carries on between ticks and resets only when it switches fields. One speed
  per building leaves room for higher levels later (a level 2 Mine at speed 1.5, say) without
  touching the deposit table.
- Field amounts are unchanged. A Grassland Wood field (250) takes 12.5 minutes by hand or about
  17 minutes for one Lumber Camp, so fields last; a building works a field for a long time.

### Building costs

Each building has a cost, paid from the stockpile when it is placed:

| Building | Cost | Gated by |
|---|---|---|
| Lumber Camp | 15 Wood | nothing: about 45 s of hand gathering |
| Mine | 25 Wood, 15 Stone | hand-gathered Stone |
| Smelter | 40 Stone, 15 Wood | Stone, ideally from a Mine |
| Oil Well | 25 Iron, 20 Stone, 10 Wood | Iron, so a working Smelter |
| Refinery | 40 Iron, 30 Stone, 20 Wood | Iron, so a working Smelter |

The intended path: hand-gather Wood for a Lumber Camp, hand-gather Stone (slowly) for a Mine,
let the Mine dig Stone for a Smelter, feed it Iron Ore, then build the oil chain. Costs lean
lightly on the real world: a building costs what most of it is made of (a Lumber Camp is
wood, a Smelter is a stone furnace), not every material it would really need. Copper Ore has
no use yet and stays out of the costs until there is Copper.

Coal is not a building material. It becomes the Smelter's fuel in a follow-up changeset (fuel
burned over time, with Wood or later Charcoal as other fuels), which is also where finding Coal
becomes a reason to explore.

### Unlocking: a building appears once its whole cost has been held

A building is **unlocked** once the stockpile has held its whole cost at once. Whenever an
amount rises, every still-locked building the stockpile can now afford unlocks, and that never
clears. So at the start nothing is unlocked; the 15th Wood unlocks the Lumber Camp, holding 25
Wood and 15 Stone adds the Mine, and so on. Unlocked is not the same as affordable, since the
stock can run short again after unlocking:

- The build line on the HUD lists only unlocked buildings, each with its key and cost. A key
  for a locked building does nothing.
- An unlocked building can be selected whether or not it is affordable. The ghost turns red when
  the player cannot afford it, the same as a blocked spot, and the HUD shows the cost with the
  missing amounts marked.
- Placing a building deducts its cost; a refused placement deducts nothing. There is no
  demolish yet, so there is no refund rule yet.

### Processors

Recipes slow down to half the pace of one base extractor, so one Mine keeps two Smelters busy
and one Oil Well keeps two Refineries busy:

- Smelter: 1 Iron Ore to 1 Iron, cycle 16 s (was 1 s).
- Refinery: 1 Crude Oil to 1 Rocket Fuel, cycle 8 s (was 2 s).

The ship launch cost (500 Rocket Fuel, 100 Iron) is unchanged. With one Oil Well and two
Refineries, the fuel takes about 33 minutes (one Refinery alone takes about 67); more of both
shorten it. It is a placeholder until
the ship parts land.

### Tests and scenarios

The debug tools stay free: `Debug.PlaceBuilding` places without a cost, and `seedStockpile`
counts as having held a resource, so it unlocks. Scenarios that place by clicking seed the
stockpile first.

## Code changes

- `ResourceDef`: a `GatherSeconds` (null for non-deposits). `ResourceDefs` fills the table above.
- `BuildingDef`: a `Cost` (a list of `ResourceAmount`) and a `Speed` (0.75 for every type today).
  `BuildingDefs` fills the costs above.
- `Simulation`:
  - Hand harvest: `HarvestRate` goes. A `HarvestProgress` (0 to 1, exposed for the HUD) grows
    while the intent is held on the same target, adds one unit at 1, and resets on release or a
    target change.
  - Extractors: `ExtractorRate` goes from `Recipes`. `StepExtractor` accumulates `Progress` on the
    building toward `GatherSeconds / Def.Speed` and takes one unit at a time; switching fields
    resets it.
  - Costs: `CanAfford(type)` and `IsUnlocked(type)`. `TryPlaceBuilding` also requires both and
    deducts the cost. `CanPlace` stays the spatial rule only.
  - The simulation keeps the set of unlocked buildings, added to in `AddToStockpile` when an
    amount rises and a locked building becomes affordable.
- `Recipes`: Smelter cycle 16 s, Refinery cycle 8 s.
- Presentation:
  - `BuildController`: ignores keys for locked buildings; the ghost is red when the selection is
    unaffordable.
  - `Hud`: the build line lists unlocked buildings with their costs (missing amounts marked), and
    the harvest hint gets a progress bar.
- Testing: the snapshot adds `player.harvestProgress` and `unlockedBuildings`. `place-at-mouse`,
  `place-on-player`, and any other scenario that places by click seed the stockpile.
  `harvest-input` holds E long enough for one Wood (3 s) and expects exactly 1.
- Unit tests: hand harvest takes a whole unit at the gather time and drops partial progress on
  release; an extractor takes `GatherSeconds / Speed` per unit; placement deducts the cost and is
  refused (with nothing deducted) when unaffordable or locked; unlocking happens when the whole
  cost is first held; the existing extractor, production, and launch tests move to the new rates.

## Blueprint updates

- resources.md: gather times per deposit and discrete hand harvesting with its progress; the
  stockpile's record of resources held.
- buildings.md: costs, speed, unlocking, the extractor's per-unit rate, the Smelter's 16 s and Refinery's 8 s cycles,
  and the HUD's build line.
- presentation.md: the HUD build line and harvest progress bar.
- testing.md: the new snapshot fields.
- game-plan.md: note that Wood and Stone now are the construction materials, as planned.

## Verification

- `dotnet build game.slnx`, `dotnet test src/Game.Sim.Tests`, `dotnet test
  src/Game.Integration.Tests`.
- In game, on a generated world: the build line starts empty; holding E on a tree adds one Wood
  every 3 s with a progress bar; the Lumber Camp appears once 15 Wood are held and placing it
  deducts them; a red ghost when short of materials.
