# Trucks Unload on the HQ's Pad

- Status: Implemented
- Id: 2611191242-hq-unloading-pad
- Depends-on: 2611191223-pin-and-truck-fixes

## Summary

Trucks unload on the HQ's landing pad, the circle in front of its entrance, instead of at any cell
beside the HQ. A truck drives onto the pad, unloads, and drives off; while the pad is taken, the
next truck waits its turn in front of it.

## Motivation

Today a truck's goal is any cell sharing an edge with the HQ's 6 x 10 footprint, so trucks stop at
its sides and back, often out of sight behind the hall, and unloading reads as a truck pausing by
a wall. The pad is already modeled as the landing base's arrival point; using it makes deliveries
visible and gives the HQ a front door.

## Design

### The pad

- The HQ's model has its pad 3.6 units in front of its center (a 2.3-unit circle), inside the
  footprint. The pad's spot is `Simulation.HqPad` (the HQ's center plus 3.6 along its front).
- The HQ's **forecourt** (the open ground the pad sits on, before the hall) is crossed only by the
  pad moves: a truck drives to the pad's front, just outside the HQ, and backs on in a straight
  run. No path enters the HQ, so every other truck and the player go round it as before.

### Unloading

- **ToHq** drives to the pad instead of beside the HQ: its path ends at the pad's cell.
- **One truck on the pad at a time.** A truck arriving while another holds the pad waits on the
  forecourt's edge in front of the pad (**WaitingForPad**), in the order they arrived; when the
  pad frees, the first waiting truck drives on.
- **Unloading** is unchanged once on the pad (0.25 s a unit, the whole load into storage). The
  truck **backs in**: it stops in front of the pad, turns its tail to the entrance, and reverses
  onto the pad, so it unloads with its bed to the hall; empty, it drives off nose first and heads
  home.
- **A busy pad slows deliveries.** At 20 units a load, a truck holds the pad about 6 s (driving
  on, 5 s unloading, driving off), so about ten loads a minute through the pad. With one truck
  keeping up with about three collectors, queues form only once many trucks serve one HQ.

### What the player sees

- A truck on the pad sits on its circle, its bed to the entrance.
- Waiting trucks line up in front of it, the first nearest the pad.
- The truck tooltip (hovering a truck) reads "Unloading" on the pad and "Waiting for the pad" in
  the queue.

## Code changes

- `src/Telaelus.Sim/SimulationPad.cs` (new): `HqPad`, the pad's front, `PathToHq`, the line
  (`PadQueue`, kept in each waiting truck's `Timer`), `PadHolder`, and the straight pad moves.
- `src/Telaelus.Sim/SimulationTrucks.cs`: ToHq paths to the pad's front; the new states stepped;
  unloading on the pad ends by pulling off.
- `src/Telaelus.Sim/Truck.cs`: `WaitingForPad`, `BackingOnPad`, `LeavingPad` (saved by name, so
  no new save fields; older saves load as before).
- `src/Telaelus/Presentation/Ui/TruckText.cs`: "Waiting for the pad", "Backing onto the pad".
- Tests: a truck unloading on the pad's cell; a second truck waiting until the first leaves; a
  truck reaching the pad from any side of the HQ; `TruckBalanceTests` still holding (the pad does
  not change the balance); the `trucks` scenario's screenshot on the pad.

## Blueprint updates

- buildings.md (Trucks, Paths): the pad, the forecourt, ToHq, WaitingForPad.
- presentation.md: the truck tooltip's new state.
- testing.md: the new tests and state.

## Verification

- Sim and integration tests pass, the truck balance among them.
- A windowed run with screenshots of a truck on the pad and two waiting.
- The demo save: trucks unload on the pad without a standing queue.

## Implementation notes

- **No forecourt passability.** The plan made the forecourt passable to trucks heading for the
  pad; driving to the pad's front (outside the HQ) and backing on in a straight run needs none,
  so paths never enter the HQ and the passability rules are unchanged.
- **The truck balance.** Two trucks on six Mines 40 out delivered 92% (95% before): on the empty
  map the HQ sits in a corner with its pad facing away from the Mines, so every trip drives round
  to the front. The test's bar is now 90%; backing at full speed changed nothing (92%), so the
  cost is the route, not the backing. One truck still keeps up with three Mines, and still falls
  behind four.
- Two truck tests with fixed waits got 60 s instead of 40 for the longer trip.
- The waiting line backs into place facing the HQ (half speed); the next truck drives up to the
  front, turns, and backs on.

## Decisions

1. **Trucks on the pad:** one at a time.
2. **The queue:** a line in front of the pad.
3. **Facing:** back in, bed to the entrance.
4. **The forecourt:** passable only to a truck heading for the pad.

## Open questions (answered)

1. **Trucks on the pad at once:** one (proposed), or two side by side (the pad is 2.3 across, a
   truck 0.8 wide), for half the waiting?
2. **The queue:** wait in a line in front of the pad (proposed), or wait wherever they are on the
   road when the pad is taken?
3. **Facing:** nose to the entrance while unloading (proposed), or back up to it like a delivery
   (a turn on the pad first)?
4. **The forecourt for passing trucks:** any truck may drive across it (proposed, it is open
   ground), or only a truck heading for the pad?
