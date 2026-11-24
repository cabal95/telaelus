# Collect a Building's Whole Buffer in One Hold

- Status: Implemented
- Id: 2611241311-collect-all-by-hand

## Summary

Holding E at a building collects everything its output buffer holds, every type, in one 0.5 s hold,
instead of one unit at a time.

## Motivation

Collecting is unit by unit today: each unit takes the resource's gather time over 10, so emptying a
Mine's 20 Iron Ore takes 12 s of holding E. That is busywork the player repeats at every collector.

## Design

- **One hold:** `InteractProgress` runs 0 to 1 over `HandCollectSeconds` (0.5 s), whatever the
  buffer holds. At 1 every type in the output buffer moves to storage at once, each as far as
  storage has room.
- **Partial room:** what does not fit stays in the buffer. If some moved, progress resets and a
  further hold collects again when room appears; with no room for any of it, progress waits and the
  hint reads "Storage full", as now.
- **Hint:** names the first type in table order and its count, plus "+N more" for further types
  ("[E] Collect Stone (5) +1 more").
- `HandMoveSpeedup` and `HandMoveSeconds` go (no per-resource pace).

## Code changes

- `src/Telaelus.Sim/SimulationNetwork.cs`: `HandCollectSeconds`; `Interact` collects the whole
  buffer; `InteractBlocked` is true when nothing fits; `InteractHeld` reports the first type's count
  and the number of other types. `NextCollect` and `HandSeconds` go.
- `src/Telaelus/Presentation/Ui/HintOverlay.cs`: the "+N more" suffix.
- Tests (`NetworkTests`): a 0.5 s hold empties a two-type buffer (nothing before it is done);
  partial room leaves the rest and blocks. The existing full-storage test still holds. The
  `network` scenario's comment changes (one Iron Ore, collected in the one hold).

## Blueprint updates

- resources.md, Collecting by hand.
- presentation.md: the hint.

## Verification

- Sim and integration tests pass.

## Implementation notes

- The prior changeset (2611241302-network-only-processing) removed hand loading, so this one
  lands after it in prefix order and needs no Depends-on.
- No scenario holds E at a building with two types, so the "+N more" text is checked through
  `InteractHeld` in the sim test, not on screen.
