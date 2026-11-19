# Collectors Need the Network

- Status: Implemented
- Id: 2611191129-collectors-need-network
- Depends-on: 2611191051-trainer

## Summary

Every collector works only while online: a Mine, Lumber Camp, or Oil Well outside the HQ's network
idles "Offline: outside the network" instead of filling its buffer. To keep the opening easy, the
HQ reaches about 35% further (coverage 14 to 19, link 16 to 22) and the Small Hub about 15% further
(coverage 8 to 9, link 12 to 14).

## Motivation

Today the network matters only to buildings that draw Power, Water, or Sand, to processors (which
reach storage through it), and to Material Transfer. The three unpowered extractors (Mine, Lumber
Camp, Oil Well) work anywhere, so the first buildings a player places never meet the network, and
hubs mean nothing until power. Requiring the network from the first camp makes expansion a choice
with a cost (a Small Hub, 10 Wood and 5 Stone) and makes the trainer's hints true ("It needs ...
the HQ's network", "Buildings work only inside it"). The bigger HQ and Small Hub keep the starter
set and the first ring of deposits inside reach without a hub.

## Design

### Collectors offline

- A switched-on collector advances only while **online**: an extractor's progress toward its next
  unit, and a producer's. Going offline drops the unit in hand (progress back to 0, as switching
  off does); online again, it starts a whole unit over.
- Only the Mine, Lumber Camp, and Oil Well change. Every other collector draws Power (Lumber Mill,
  Drill Rig, Quarry, Deep Well, Deep Mine) or Power and Water (Forester), so it already stops
  offline.
- **Status:** an offline extractor is idle "Offline: outside the network" (checked before "No
  fields in range" and "Full: waiting for pickup"). Powered collectors keep their "No Power:
  offline".
- **What it holds:** a collector's buffer keeps its contents when it goes offline, and the player
  can still collect them by hand.
- **Trucks serve only online collectors.** A Depot dispatches only to online collectors (today it
  takes them online or offline). A truck driving to a collector that goes offline turns for home
  empty; one loading at it heads home empty too (a truck takes its load only when loading ends).
- The placement tooltip's network line ("Online" / "Offline") turns `Warning` with "Offline: will
  not work" for a building that needs the network (every building but hubs and storage), so the
  player sees it before placing.

### Reach

| Hub | Coverage radius | Link radius |
|---|---|---|
| HQ | 14 to **19** (+36%) | 16 to **22** (+38%) |
| Small Hub | 8 to **9** (+12.5%) | 12 to **14** (+17%) |
| Relay | 3 (unchanged) | 30 (unchanged) |

- Rounded to whole units so the network overlay's rings and the docs read cleanly; exact
  percentages would be 18.9 / 21.6 and 9.2 / 13.8 (see the open questions).
- The starter set (two Wood and one Stone, 4 to 6 map cells from the spawn) sits about 12 to 24
  units from the HQ's center; at 19, a camp placed beside the nearer fields is covered.

## Code changes

- `src/Telaelus.Sim/BuildingDefs.cs`: the HQ's and Small Hub's radii.
- `src/Telaelus.Sim/Simulation.cs`: `StepExtractor` (and `StepProducer` for consistency) reset
  progress and return while offline; `BuildingStatusOf` gives "Offline: outside the network".
- `src/Telaelus.Sim/SimulationTrucks.cs`: dispatch only to online collectors; a truck on its way to
  or loading at one that goes offline drops the job and heads home empty.
- `src/Telaelus/Presentation/BuildController.cs`: the placement tooltip's offline line reads
  "Offline: will not work" for everything but a hub (a hub keeps "Offline (no hub in reach)"); it
  was already `Warning`.
- `src/Telaelus/Presentation/Trainer.cs`: no change (its hints become true).
- `src/Telaelus.Sim/DemoTown.cs`: checked; any collector outside reach gets a Small Hub, and the
  demo save is rewritten.
- Tests:
  - Sim: an offline Mine, Lumber Camp, and Oil Well make nothing and report the new status; going
    offline drops the unit in hand; one brought online by a Small Hub starts again; hand
    collection still empties an offline buffer; a truck never dispatches to an offline collector,
    and one on its way turns home when it goes offline; the new radii (a building at 18.5 from the HQ online, at 19.5 offline; two Small Hubs
    13.5 apart linked).
  - Existing sim tests and scenarios that place an unpowered extractor out of reach get a hub or
    move in; tests that measure reach at the old radii are updated.

## Blueprint updates

- buildings.md: the hub table's radii; Production ("online or not" becomes online only); the
  offline status; buffers staying collectable.
- resources.md: collecting by hand from offline collectors still works.
- buildings.md (Trucks): dispatch to online collectors only, and the turn home.
- presentation.md: the placement tooltip's offline warning.
- testing.md: the new and changed tests.

## Verification

- Sim and integration tests pass.
- The demo save loads with every collector online (`buildings[*].online`).
- In play: a Lumber Camp placed past the HQ's reach shows "Offline: will not work" while placing
  and idles once placed; a Small Hub between brings it online and it starts.

## Decisions

1. **Rounding:** whole units (19 / 22 and 9 / 14).
2. **The HQ's link radius:** grows with its coverage.
3. **Offline progress:** reset to 0, as switching off does.
4. **Trucks:** serve online collectors only; collecting by hand from an offline one still works.
5. **The placement warning:** "Offline: will not work" in `Warning`.

## Implementation notes

- **Every collector** now drops its unit in hand offline, not only the three unpowered ones: the
  check sits before the collector step for all of them. Powered collectors already made nothing
  offline; now they also start a whole unit over, which matches switching off.
- **Trucks take a load only when loading ends**, so the planned "heads to the HQ with what it
  has" became "heads home empty" for a collector cut off mid-load.
- **Tests.** About twenty sim tests placed a Mine, Lumber Camp, or Oil Well off the network on the
  empty or test map. Tests about extraction now use `SimTestHelpers.PlaceOnline` (place, then
  chain Small Hubs from the HQ); `Placed` leaves those hubs out so counts hold. The truck tests'
  map lays two Relays and two Small Hubs over its south-west instead, since a per-test chain
  blocked their later placements; the save tests connect their Mine last for the same reason. The
  end-to-end launch run gained two hubs for its collectors. Three network tests were re-measured
  for the new radii. The Material Transfer test's far Mine stays offline and now starts with ore.
- **Scenarios.** Six placed collectors off the network: `field-detail`, `building-detail`,
  `resource-fields`, `trucks`, and `roads` gained Small Hubs (and in `roads` two Relays over the
  river) placed after their buildings so the indexes they check hold; `network` now shows the
  offline Mine making nothing, its seeded buffer still collected by hand, and the Mine working once
  the hubs connect it.
- A hang while fixing them: two truck tests loop until a truck heads for the HQ, which never
  happened with their Mine offline. Running with `--blame-hang-timeout` found them.
- **The demo save** needed no change: every building in it was already online (its test checks
  that), and a rewrite differed only in its timestamp, so it was left as committed.

## Open questions (answered)

1. **Rounding:** whole units (proposed: 19 / 22 and 9 / 14), or the exact +35% and +15% (18.9 /
   21.6 and 9.2 / 13.8)?
2. **The HQ's link radius:** grow it with its coverage (proposed), or keep 16 so only what it
   covers changes?
3. **Offline progress:** hold it and resume (proposed), or drop it to 0 as switching off does?
4. **Trucks and offline collectors:** keep picking up from them (proposed, so nothing strands), or
   only from online ones so the whole chain needs the network?
5. **The placement warning:** add "Offline: will not work" (proposed), or leave the tooltip's
   plain "Offline" as it is?
