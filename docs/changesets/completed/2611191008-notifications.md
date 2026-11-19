# Notifications

- Status: Implemented
- Id: 2611191008-notifications
- Depends-on: none

## Summary

Toast notifications stack on the right side of the screen, below the top bar, in two styles: a
**brief** toast that fades after 5 seconds, and a **lasting** toast with a close (X) button and a
15-second countdown. The first uses: a building unlocked and a resource discovered for the first
time (brief), and a research project completed, a blackout starting, and a ship launching
(lasting). The trainer mode will build on the
lasting style.

## Motivation

Unlocks, discoveries, and finished research happen silently today: a new building just appears
in the build menu, a new resource in the inventory, and the research tree shows a project done
only when it is opened. The player misses moments that tell them what they can do next. ui-style.md
already plans notifications as an overlay.

## Design

### The two styles

| | Brief | Lasting |
|---|---|---|
| For | Good to know; fine to miss | Worth reading |
| Lifetime | 5 s, then fades out over 0.3 s | 15 s, then fades out over 0.3 s |
| Close button | None | An X (two drawn lines) at its top right |
| Countdown | None shown | A thin bar along its bottom edge, shrinking to nothing |
| Hover | Holds it (the timer pauses) | Holds it (the timer and bar pause) |
| Click on the body | Nothing | Nothing |

- Timers run only while the game runs (not under the game menu or the save and load dialogs, which
  stand the game still), and pause while the pointer is over the toast, so the player can finish
  reading.
- Both styles share one look: a `Panel`-colored card about 280 units wide, an icon at the left
  (the building's picture or the resource's icon, 28 units), a title line in `Text` and an
  optional detail line in `TextDim`. A lasting toast has an accent strip down its left edge
  (`Accent`) so the two styles read apart at a glance.

### Placement and stacking

- The stack sits against the right edge, `Gap` in from it, starting `Gap` below the top bar; the
  newest is on top and older ones move down. Panels dock left, so toasts never cover an open
  panel. A modal (the build menu) covers them, and their timers keep running.
- At most 4 are shown. A fifth pushes the oldest brief toast out early (a lasting one is kept
  over a brief one); if all 4 are lasting, the oldest lasting one goes.
- The stack is an overlay (`HudOverlay`): it never takes keys. Only the toasts' own rectangles
  take the mouse (for hover and the X), so the world around them keeps its clicks.
- Toasts appear by sliding in 24 units from the right while fading in, over 0.2 s.

### What notifies

| Event | Style | Icon | Title | Detail |
|---|---|---|---|---|
| A building unlocked | Brief | Its picture | "New building: Mine" | "[B] to build" |
| A resource discovered | Brief | Its icon | "Discovered: Coal" | What works it ("A Mine can work it") or none |
| A research project completed | Lasting | The project's own icon (as in the research tree) | "Research complete: Material Transfer" | Its effect in a line, as the tree's drawer says it |
| A blackout starts | Lasting | `icon_power`, or the Water or Sand icon | "Blackout: Power" | "Demand 140 over supply 100" |
| A ship launches | Lasting | The Shipyard's picture | "Ship launched" | "Ships launched: 2" |

- **A blackout** notifies once when a capacity (Power, Water, or Sand) goes into blackout, and not
  again for that capacity until it has come out of it. A brownout does not notify.

- **Bursts merge.** Several unlocks in one frame make one toast ("New buildings: Mine, Smelter",
  the first's picture); the same for discoveries. Exploring a new area can discover three
  resources in one step, and one load of Stone can unlock two buildings.
- **Never from loading or a new world.** A new game discovers what is around the HQ and a load
  restores unlocks; neither notifies. Only a change during play does.

### The sim side

The sim stays free of UI: it records what happened, and the presentation decides how to show it.

- `Simulation` gains `event Action<SimEvent>? Happened`, raised when a building is first
  unlocked, a resource first discovered, a project completes, a capacity goes into blackout, or a
  ship launches; `SimEvent` is a small record (`Kind`: `Unlocked`, `Discovered`,
  `ResearchComplete`, `Blackout`, `ShipLaunched`, and the `BuildingType`, `ResourceType`,
  `ResearchProject`, or `CapacityType`).
- Raised only on a real change (the `HashSet.Add` that returns true), and not while the world is
  being reset or loaded (a flag set around `ResetWorld` and `Load`).
- This follows the existing `CellsRevealed` and `ChunkGenerated` events. One event with a kind,
  rather than five, keeps the next notifiers (the trainer) to one subscription.

## Code changes

- `src/Telaelus.Sim/SimEvent.cs` (new): `SimEventKind` and the `SimEvent` record.
- `src/Telaelus.Sim/SimulationEvents.cs` (new): `Happened`, `Raise`, and `Quietly`.
- `src/Telaelus.Sim/Simulation.cs`, `SimulationDiscovery.cs`, `SimulationResearch.cs`,
  `SimulationCapacity.cs`, `SimulationWorld.cs`, `SimulationSave.cs`: raise `Happened` from
  `UnlockAffordable`, `Discover`, `StepResearch`, a capacity's change into blackout, and
  `TryLaunchShip`; the quiet flag around reset and load.
- `src/Telaelus/Presentation/Ui/Notifications.cs` (new): the overlay: the stack, the two styles,
  timers, hover hold, the X, the cap of 4, and `Show(style, icon, title, detail)` for any caller.
- `src/Telaelus/Presentation/Ui/NotificationText.cs` (new): turns a `SimEvent` (or a merged burst)
  into a toast's style, icon, title, and detail.
- `src/Telaelus/Presentation/GameRoot.cs`: subscribes to `Happened`, merges a frame's events by
  kind, and shows them.
- `src/Telaelus/Testing/StateSnapshot.cs`: `ui.toasts`, each `style`, `title`, `detail`, and
  `remaining` seconds; the X registered as `toast/<index>/close` for `clickUi`.
- Tests:
  - Sim tests (`EventTests`): the event fires once per unlock, discovery, completion, and launch; once per
    blackout until it clears; not on a new world or a load; a burst of unlocks fires one event
    each.
  - A `notifications` scenario: collecting enough Stone unlocks a building and shows a brief toast
    that is gone after 5 s; seeding a new resource shows a discovery toast; finishing a project
    shows a lasting toast with its X; a blackout and a launch show theirs; hovering holds it; the X closes it; unclicked, it is gone
    after 15 s; a fifth toast pushes out the oldest brief one; screenshots of the stack.

## Blueprint updates

- ui-style.md: the toast card, the two styles, placement, stacking, and the "later notifications"
  overlay becoming real.
- presentation.md: the notification overlay and what notifies.
- simulation.md: `Happened` and `SimEvent`.
- testing.md: `ui.toasts`, the close control id, the new sim tests, and the scenario.

## Verification

- Sim and integration tests pass.
- A windowed run of the `notifications` scenario, with screenshots of a brief toast, a lasting one
  with its bar, and a full stack, reviewed for look and placement at 1280 x 720.
- In play: start a new game (no toasts), gather Stone until the Mine unlocks, and explore until
  something new is discovered.

## Decisions

1. **Detail lines:** kept; each points at the next step.
2. **Bursts:** one toast per burst.
3. **Hover:** holds the timer for both styles.
4. **Other lasting uses:** a blackout starting (once until it clears) and a ship launching; not
   storage full.

## Implementation notes

- **No new icons.** Research projects already have icon models (the research tree's), so a
  completed project's toast uses its own; the X is two lines drawn by the button, which stays
  crisp at any UI scale. That dropped `icon_research` and `icon_close` from the plan.
- **Pause.** The game stands still by dialog (`PausesGame`), not by pausing the scene tree, so
  `GameRoot` tells the overlay when it is paused.
- **Measuring.** A wrapping label with no width yet measures a word per line, which threw the
  stack's targets far down for a frame; each label is sized to the text column's width when made.
- **Bursts in the blackout test:** a Refinery with no generator blacks out Power as well as Water,
  so the sim test watches Water and checks Power raised once.
- In the scenario, a click on the world under the stack lands on a toast (as designed); the
  scenario moves the player so the Shipyard comes out from under it.

## Open questions (answered)

1. **Detail lines:** keep the short hint ("[B] to build", "A Mine can work it"), or titles only?
   Proposed: keep them; they point at the next step, which is the point of telling the player.
2. **Merging bursts:** one toast per burst (proposed), or one toast per item?
3. **Hover holds the timer:** for both styles (proposed), or lasting only?
4. **Other lasting uses now.** Candidates, each a line or two on top of this work:
   - **A blackout starts** (Power, Water, or Sand): lasting, "Blackout: Power", "Demand 140 over
     supply 100". The top bar already turns red, but a blackout stops buildings and is easy to
     miss while looking at the world. Proposed: yes, once per blackout (not again until it
     clears).
   - **A ship launches:** lasting, "Ship launched", "Ships launched: 2". Proposed: yes; it is the
     game's biggest moment and today it only changes a number in the Shipyard's panel.
   - **Storage full** the first time per resource: brief. Proposed: no, the hint overlay and the
     red pin already say it, and it would repeat often.
