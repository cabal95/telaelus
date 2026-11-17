# Top Bar Cleanup

- Status: Implemented
- Id: 2611162105-top-bar-cleanup
- Depends-on: 2611162030-truck-balance

## Summary

The top bar loses the ship launch progress, which moves into the Shipyard's panel with a Launch
ship button (the L key goes), and Water and Sand move from the pinned resources on the left to
readouts beside Power on the right. Power, Water, and Sand show only once the network has any of
them. The pins show their amount alone, red when storage is full and dim with no storage at all,
with the limit in the tooltip, and the bar's entries sit twice as far apart. The bar stops
feeling crowded.

## Motivation

At 1280 wide the bar carried eight pins (six deposits plus Sand and Water), the clock, power, the
Battery store once there is one, and five ship bill entries ("0 / 4   0 / 1   0 / 1   0 / 500
0 / 200") from the first minute of a game, hours before any of them can move. Sand and Water also
read differently from the pins beside them: they are capacity (demand over supply, like Power),
not stock, so they belong with Power.

## Design

### The top bar

- **Left:** the pinned resources. Sand and Water are no longer pins and can never be pinned
  (`ResourcePins.CanPin`): the defaults are the six starting deposits, the inventory shows no Pin
  toggle for them, and a loaded save's pins drop them. Power is not a resource, so it never was.
- **Right:** the clock, then **Power**, **Water**, and **Sand** (`CapacityType` order), each only
  once the network has any supply or demand of it (for Power, a Battery Bank counts too), then the
  Battery store once there is a bank. Each reads like Power did: its icon and demand over supply
  ("2 / 4"), "Blackout:" before the icon in a blackout, in normal text, `Warning` in a brownout,
  or `Danger` in a blackout, with a name tooltip.
- **Gone:** the five ship bill entries, "[L] Launch ship", "Build a Shipyard to launch", and
  "Ships: n".

### The pins' amounts (added after review)

- **A pin** is its icon and its amount ("45"), in normal text; in `Danger` when storage has no
  room for another unit (`TopBar.PinColor`); in `TextDim` while it has no storage at all (a limit
  of 0, as Crude Oil before a Tank Farm), so a default pin is not red from the first minute.
- **Its tooltip** names it with its amount and limit ("Wood 45 / 100"; the amount alone for a Cargo
  resource), worked out when the pointer comes over it.
- **Spacing:** every entry on the bar (each pin, and each section on the right) sits two `Gap`s
  (16 units) apart, from one.
- The inventory keeps "amount / limit" and `Warning` for full.

### The Shipyard's panel

Under its progress bar, above the recipe picker, a **Ship launch** section:

- a **Launch ship** button, enabled only while this Shipyard is switched on and online and the
  whole bill is in storage, and under it why not ("Needs the whole bill in storage", "This
  Shipyard is offline", "This Shipyard is switched off");
- "Ships launched: n" once there is one;
- the ship bill, a row per entry with its icon, name, and "have / need", in `Short` until met.

The button comes before the bill so it shows, grayed out, without scrolling: the hint the first
time. There is no key and no cue on the top bar. Launching works as before (`TryLaunchShip`).

### Unchanged

The inventory still lists Hull Sections, Engines, and the rest with their amounts; the sim, the
bill, and the launch itself do not change.

## Code changes

- `src/Telaelus/Presentation/Ui/TopBar.cs`: the bill, launch, and ships entries removed; a
  readout per capacity, shown once it has supply or demand; `PinText`, `PinColor`, the pins'
  tooltip with the limit, and double spacing.
- `src/Telaelus/Presentation/Ui/HudOverlay.cs`: `ToneOf` names `TextDim` "dim".
- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: the Shipyard's Ship launch section;
  `Initialize` takes the resource icons for its rows.
- `src/Telaelus/Presentation/GameRoot.cs`: the L key removed.
- `src/Telaelus/Presentation/Ui/ResourcePins.cs`, `InventoryPanel.cs`: `CanPin`; Sand and Water
  out of the defaults and never pinned.
- `src/Telaelus/Testing/StateSnapshot.cs`: `ui.topBar.water` and `ui.topBar.sand` beside `power`
  (null while hidden), each with a tone (`powerTone`, `waterTone`, `sandTone`); `ui.topBar.launch`
  removed; the building detail's `launch`.
- Scenarios: `hud`, `refining`, `capacity`, `storage`, and `first-ship`.

## Blueprint updates

- presentation.md: the top bar; the Shipyard panel's Ship launch section; the inventory's Pin
  toggle; no L key.
- ui-style.md: the top bar's sections, pins, and spacing; the `Warning` and `Danger` uses.
- buildings.md: launching with the Shipyard panel's button.
- resources.md: Sand and Water beside Power, never pinned.
- testing.md: the snapshot fields and the scenarios.

## Verification

- Sim and integration tests pass.
- Windowed `storage` and `hud` screenshots reviewed: full Wood "100" in red, Crude Oil dim, the
  Stone tooltip "Stone 0 / 100", and the doubled spacing.
- Windowed: the `capacity` bar shows Sand beside the clock in the brownout color, with no Power
  (none on that network); the `first-ship` Shipyard panel shows Launch ship grayed out with why,
  above the bill, without scrolling, and after the launch "Ships launched: 1".

## Decisions

1. **The Ship launch section:** the bill rows, the button with what stops it, and the ships
   launched.
2. **The L key:** dropped; launching is the Shipyard panel's button.
3. **A cue on the bar:** none. A grayed-out Launch ship in the Shipyard's panel is the hint.
4. **When the capacities show:** once the network has any supply or demand, for Power as well as
   Water and Sand.
5. **Pins:** Sand, Water, and Power cannot be pinned.
6. **Amounts only** (after review): pins drop "/ limit" and turn red when full, with the limit in
   the tooltip; the bar's entries sit twice as far apart.

## Implementation notes

- **The section's place.** At first it sat below the recipe picker, and the Launch ship button was
  below the fold at 720 high; it moved under the progress bar, and the button went above the bill
  rows, so it shows without scrolling.
- **Tones in the snapshot.** `capacity` checked the Sand pin's tone (brownout in `Warning`); the
  readouts stay strings, as `power` was, with a tone field beside each.
- **Crude Oil.** It is a default pin and has no storage until a Tank Farm, so it counts as full
  from the start; red there would read as an alarm in every new game. A limit of 0 shows dim
  instead, and red is kept for storage that has filled.
- **Power hidden.** Power used to show "0 / 0" from the start; it now appears with the first
  generator, consumer, or Battery Bank on the network.

## Open questions (answered)

1. **What the Ship launch section shows:** the bill rows, the Launch ship button with what stops
   it, and the ships launched (proposed)? Anything else you want there?
2. **Keep the L key?** Proposed: yes, it still launches from anywhere, and the button shows it.
3. **A cue on the bar when a ship can launch?** Proposed: a short "[L] Launch ship" on the right of
   the bar only while a ship can launch. Or nothing, until notifications exist.
4. **When Water and Sand show:** once the network has any supply or demand of them (proposed); or
   always, like Power; or once discovered.
5. **Sand and Water pins:** no longer pinnable (proposed).
