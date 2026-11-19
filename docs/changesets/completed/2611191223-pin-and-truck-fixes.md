# Pin and Truck Fixes

- Status: Implemented
- Id: 2611191223-pin-and-truck-fixes
- Depends-on: 2611191129-collectors-need-network

## Summary

Two bug fixes. The top bar no longer starts with resources pinned: a new game pins what it has
discovered around the HQ, each later first discovery pins itself while the bar has room, and the
bar holds at most 8. Trucks no longer drive through their own Depot: only its apron (the bays) is
open to them, so they drive round the shed.

## Motivation

- **Pins.** A new game showed six fixed deposits (some not yet discovered, Crude Oil dim with no
  storage), so the bar was full of things the player had never seen. Pins should follow what the
  player finds, and the bar has room for 8.
- **Trucks.** A truck's whole home Depot counted as passable so it could leave and reach its bay,
  which let its path run straight through the shed to a collector behind it.

## Design

### Pins

- `ResourcePins` starts empty and holds at most `Max` (8).
- **Auto-pin:** a new game pins every resource it has discovered (the cells around the HQ are
  discovered quietly when the world is made), in table order, while there is room; after that,
  each `Discovered` event pins that resource while there is room. Sand and Water never pin
  (`CanPin`).
- **By hand:** the inventory's Pin toggle still pins and unpins; while 8 are pinned, an unpinned
  row's toggle is disabled with the tooltip "The top bar holds 8: unpin one first". Unpinning
  makes room for the next discovery.
- **Saves** keep the pins as before; a loaded save keeps at most 8. Saves made before this keep
  their old six.

### Trucks

- A truck's own Depot is passable to it only on its **apron**: the front half of its footprint,
  where the bays stand (cells whose center lies in front of the Depot's center along its facing).
  The shed half is a wall like any building, so a path to a collector behind the Depot goes round.

## Code changes

- `src/Telaelus/Presentation/Ui/ResourcePins.cs`: no defaults; `Max`, `IsFull`, `AutoPin`; `Toggle`
  and `SetAll` respect the cap.
- `src/Telaelus/Presentation/Ui/InventoryPanel.cs`: the Pin toggle disabled while full.
- `src/Telaelus/Presentation/GameRoot.cs`: `PinDiscovered` for a new game; `AutoPin` on each
  `Discovered` event.
- `src/Telaelus/Testing/CommandExecutor.cs`: `initMap` starts the pins afresh, as a new game, so a
  test map does not inherit the generated world's.
- `src/Telaelus.Sim/SimulationPaths.cs`: `IsPassable` allows only a home Depot's apron (`OnApron`).
- Tests: `TruckTests.ATruck_DrivesRoundItsDepotsShed_NotThroughIt` (fails without the fix); the
  `hud`, `storage`, and `inventory` scenarios follow the new pins, and `storage` checks the cap.

## Blueprint updates

- presentation.md: the top bar's pins and the inventory's disabled Pin toggle.
- buildings.md: a home Depot's apron only.
- testing.md: `initMap` resetting the pins, the new truck test, and the three scenarios.

## Verification

- Sim and integration tests pass.
- In play: a new game's bar shows only what is discovered near the HQ, and new discoveries add
  themselves up to 8; a truck leaving a Depot with a Mine behind it drives round the shed.
