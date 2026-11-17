# Truck Balance

- Status: Implemented
- Id: 2611162030-truck-balance
- Depends-on: 2611161940-click-to-move

## Summary

Trucks drive slower and take time to load and unload each unit, so one truck keeps up with about
three collectors 40 units from the HQ instead of eight, and building more trucks matters. Trucks
load only what storage has room for, and unload everything they carry, so a truck never sits at
the HQ holding a load. A Depot can sell trucks, for half a bought truck's cost back.

## Motivation

The game plan says trucks are cheap and you build more of them for more throughput (and later
upgrade them). Before this change one truck did the work of a whole outpost. Measured in the sim
(one truck, a Depot beside the HQ, Stone Mines in a column about 38 units out, ten minutes after
a minute's warm-up):

| Mines | Made | Delivered | Buffers full |
|---|---|---|---|
| 2 | 0.33/s | 0.34/s | 0% |
| 4 | 0.67/s | 0.69/s | 0% |
| 6 | 1.00/s | 1.01/s | 0% |
| 8 | 1.33/s | 1.27/s | 3% |

(A base Mine makes Stone every 6 s, 0.17/s; Iron Ore every 8 s; a Lumber Camp Wood every 4 s.)
A trip is out to the collector and back, and it carried 20 units at 8 units a second with 2 s to
load and 2 s to unload: about 1.3 units a second at that distance, eight Mines' worth.

Separately, a truck loaded the resource its collector held most of, whether or not storage had
room, then waited at the HQ (`WaitingForRoom`) until room appeared. With a resource nothing used
(Stone with no building going up, say) it waited forever, and every truck that picked up the same
resource joined it, until the Depot had none left to work.

## Design

### The numbers

A trip takes `2d / speed + load + unload` and moves one load. Aimed at **about three base
collectors per truck 40 units from the HQ, and two at 80**:

| | Before | After |
|---|---|---|
| Capacity (`TruckDefs.Capacity`) | 20 | 20 (unchanged) |
| Speed (`TruckDefs.Speed`) | 8 | 3 (half the player's 6; 4.5 on a road) |
| Loading and unloading | 2 s each | 0.25 s per unit each (`LoadSecondsPerUnit`): 5 s for a full load |
| Dispatch at (`TruckDefs.DispatchAt`) | 10 | 10 |
| Cost | 15 Iron, 10 Wood | unchanged |

Time per unit loaded means a later upgrade to a bigger load is partly offset by the time it takes
to fill and empty. Measured after the change (`TruckBalanceTests`, same layout):

| Mines | Distance | Trucks | Delivered of a full rate |
|---|---|---|---|
| 3 | ~38 | 1 | 99% |
| 4 | ~38 | 1 | 82% |
| 6 | ~38 | 1 | 55% |
| 6 | ~38 | 2 | 100% |
| 4 | ~18 | 1 | 103% |
| 6 | ~18 | 1 | 80% |

So one truck keeps up with three Mines about 40 units out and four about 20 out. At 80 the trip
time gives two (the bounded test map is too small to measure it). A Drill Rig (Iron Ore every
3 s) keeps most of a truck busy on its own, and a Depot's four trucks serve about twelve
collectors near the HQ.

### Loading only what storage can take

- **Room counts what is on the way.** A resource's room for a truck is `RoomFor(type)` less what
  every other loaded truck is already carrying of it toward the HQ (`ToHq` or `Unloading`).
- **Loading** takes the resource giving the biggest load, in whole units up to the capacity and
  that room (the most-held on a tie). With room for none of what the buffer holds, it takes
  nothing and goes home. Loading takes 0.25 s for each unit of the load on arrival.
- **Dispatch** goes by that load: at least `DispatchAt`, or at least one unit when the buffer is
  full. A collector full of a resource storage cannot take is not a job; it waits, full, until room
  appears, and its own status already says so.

### Unloading everything (the fallback)

- A truck unloads its whole load at the HQ, even past the storage limit, as a destroy refund does.
  That only happens when room it counted on was taken on the way: the player collecting by hand
  from another building, or a store destroyed. A truck can never be stuck at the HQ.
- `WaitingForRoom` stays in `TruckState` only so older saves still load; a truck in it unloads at
  once.

### Selling trucks

- The Depot's panel gains **Sell truck** under Buy truck, naming what it gives back ("Sell truck
  (8 Iron, 5 Wood back)"). It sells a parked truck before one out on a trip, the highest bay
  first (`TruckToSell`).
- A sold truck gives back what destroying its Depot would for it (`RefundOf`): half a bought
  truck's cost, rounded up, and any load in full. The truck the Depot came with gives nothing
  ("Sell truck (nothing back)").
- A Depot may sell every truck, and buy again into its free bays. Selling makes trying a Depot
  with fewer trucks quick, and is a permanent feature.

### Unchanged

The trip itself (dispatch, drive beside the collector, load, drive beside the HQ, unload, head
home dispatching on the way), paths, Depots and their four bays, Material Transfer, and saves.

## Code changes

- `src/Telaelus.Sim/Truck.cs`: `Speed` 3; `LoadSeconds` becomes `LoadSecondsPerUnit` (0.25);
  `WaitingForRoom` documented as older saves only.
- `src/Telaelus.Sim/SimulationTrucks.cs`: loading and unloading timed per unit; `RoomForTruck`
  (room less the loads on the way); `LoadFor` (the load a collector gives a truck) used by dispatch
  and loading; unloading the whole load.
- `src/Telaelus.Sim/SimulationTrucks.cs` (selling): `TruckToSell`, `TrySellTruck`, and `RefundOf`
  (now also what a destroyed Depot's refund sums).
- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: the Sell truck button.
- `src/Telaelus/Testing/StateSnapshot.cs`: `ui.sellTruck`.
- `src/Telaelus.Sim.Tests/TruckTests.cs`: selling (what comes back, down to none and buying again,
  a parked truck first, a load coming back); the 20-unit haul given time for the slower trip; the
  waiting test replaced by loading only what has room, room counting another truck's load, and
  unloading everything when the room was taken on the way.
- `src/Telaelus.Sim.Tests/TruckBalanceTests.cs` (new): one truck keeps up with three Mines about
  40 units out, falls behind four, and two trucks keep up with six.
- Scenarios: `trucks` and `roads` wait longer for the slower trips; `trucks` sells its second
  truck from the panel and buys it again.
- The demo save rewritten.

## Blueprint updates

- buildings.md (Trucks): the numbers and the balance, room, dispatch by load, per-unit loading,
  unloading everything, and selling.
- presentation.md: the Depot panel's Sell truck button.
- testing.md: `TruckBalanceTests`, the new `TruckTests`, `ui.sellTruck`, the `trucks` scenario,
  and `waitingForRoom` as older saves only.
- game-plan.md: trucks are slow (slower than walking) and one keeps up with about three
  collectors.

## Verification

- Sim and integration tests pass, `TruckBalanceTests` among them.
- The demo save: three of its four trucks were out a minute after loading (one before), its
  Smelters stay fed, and none waits at the HQ.

## Decisions

1. **The target:** about three base collectors per truck 40 units out, and two at 80.
2. **The lever:** slower trucks, with the capacity kept at 20 and loading and unloading at 0.25 s
   a unit, so later capacity upgrades are partly offset by the time to load. Solving for the
   target gave a speed of 3.
3. **The HQ fix:** in this changeset, with a fallback: a truck that cannot fully unload unloads
   everything anyway, past the limit, so it can never be stuck.
4. **The demo save:** rewritten.
5. **Selling trucks** (added after the balance was tried): a Depot sells a truck for half a bought
   one's cost back, so truck counts are quick to try, and it stays as a feature.

## Implementation notes

- **Loading time** is set on arrival from the load the collector would give then; the load taken
  at the end is worked out again (the buffer may have grown a little meanwhile).
- **A test that fails without the fix:** with the room reservation turned off,
  `Room_CountsTheLoadsOtherTrucksAreCarrying` ends 5 over the limit (65 against 60); with it, the
  stock ends at the limit exactly.
- **The demo town** still keeps its trucks working (its tests pass unchanged): its collectors are
  20 to 45 units from the HQ and it has four trucks for four collectors.

## Open questions (answered)

1. **The target:** about three base collectors per truck near the HQ (40 units) and two at 80
   (proposed)? Fewer makes trucks a bigger part of the game and Depots fill sooner (four trucks
   each); more keeps logistics light.
2. **The lever:** smaller loads and time per unit loaded (proposed), keeping trucks faster than the
   player? The other lever is slower trucks, which also reads on screen (trucks crawling) but
   would make them slower than walking.
3. **Fold in the HQ fix?** Proposed: yes, in this changeset, since smaller loads make more trips
   and the stuck trucks show up sooner.
4. **Rewrite the demo save:** yes (proposed), so it shows the new balance.
