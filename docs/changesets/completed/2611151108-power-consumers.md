# Power Consumers: Solar, Batteries, Fission, and Day and Night

- Status: Implemented
- Id: 2611151108-power-consumers
- Depends-on: 2611151049-later-tiers

## Summary

Gives the later tiers' dead-end items a use in Part A, all in power: a **Solar Array** that
supplies Power from Solar Cells with no fuel, a **Battery Bank** built from Batteries that stores
surplus Power and gives it back in a shortage, and a **Fission Reactor** that burns Fuel Rods for a
lot of Power, built with Heat Shields and Sensor Arrays. A **day and night cycle** sets the Solar
Array's output, so solar and batteries pair up. Power becomes the first capacity that can be
stored.

## Motivation

2611151049-later-tiers made Batteries, Solar Cells, Heat Shields, Life Support Units, Sensor
Arrays, and Fuel Rods with nothing to consume them, and decided (its decision 2) that a fourth
changeset would add Part A uses: a Solar Array, a Battery Bank, and a Fission Reactor. Today all
Power comes from Water Wheels (on rivers only) and Burner Generators (fuel every 40 s each), so a
large late-game base spends a steady stream of Coal or Charcoal on power alone.

## Design

### Scope

- **In:** the three buildings and their models; a day and night cycle in the sim and its light in
  the presentation; stored Power (charge and discharge); fuels limited by building (Fuel Rods only
  in the reactor); a Fuel Rod burn time; the top bar's clock and stored-Power readout; the Battery
  Bank's Charge row.
- **Out:** throttling Burner Generators to demand (they still burn whenever switched on); a use
  for Life Support Units (left for Part B's crewed ships); storing Water or Sand; anything else
  that changes by day or night (no building lights, no night-only behavior); a Battery Bank model
  that shows its charge (it stays static).

### The buildings (placeholders)

| Building | Footprint | Cost | Tier | Supplies | Draws | Notes |
|---|---|---|---|---|---|---|
| Solar Array | 6 x 6 | 20 Solar Cell, 10 Aluminum Frame, 10 Concrete | 2 | up to 60 Power, by daylight | | No fuel; runs whenever switched on and online |
| Battery Bank | 3 x 4 | 20 Battery, 20 Steel, 10 Concrete | 2 | up to 50 Power (discharging) | | Stores 6000 Power-seconds (100 Power for a minute) |
| Fission Reactor | 6 x 8 | 40 Concrete, 20 Armor Plate, 10 Heat Shield, 6 Sensor Array, 10 Computer | 3 | 400 Power | 8 Water (its cooling) | Burns Fuel Rods: one every 300 s, whenever switched on |

- **Solar Array:** a provider like the Water Wheel, without the river, supplying 60 Power times the
  daylight (below): 60 at midday, none at night, about 37 averaged over a day, against the Burner
  Generator's steady 100 for 4 cells and a unit of Coal every 40 s. It trades ground and the night
  for fuel, and a Battery Bank carries it through the dark.
- **Fission Reactor:** a fuel-burning provider like the Burner Generator (burns whenever switched
  on, supplies only while a rod burns), with a Water draw: in a Water brownout or blackout it
  supplies less or nothing, as a Pumping Station does in a Power shortage. One reactor is four
  Burner Generators and a Fuel Rod every five minutes is far less fuel than their 30 Coal.
- **Its costs** use the tier-3 parts: Heat Shields (its containment), Sensor Arrays (its
  monitoring), and Computers (its control).
- **It stands anywhere** (Standard placement); its cooling is its Water draw, not a place by water.

### Day and night

- **The sim keeps the time of day** (`TimeOfDay`, 0 to 1 over `DayLength`, 600 s: ten minutes),
  advanced by the fixed-step clock with everything else, so co-op peers agree on it. A new game
  starts at 0.3, mid-morning.
- **Daylight** (`Daylight`, 0 to 1) follows the clock: night (0) from 0.85 to 0.15, dawn rising
  from 0 to 1 between 0.15 and 0.25, full day (1) from 0.25 to 0.75, and dusk falling back between
  0.75 and 0.85. That is about six minutes of full light, two of twilight, and two of dark. Only
  the Solar Array reads it today.
- **The presentation lights the world by it:** the light's energy and color fall from white at
  midday through warm at dawn and dusk to a dim blue moonlight at night (never so dark the map
  stops reading). Nothing else changes at night.
- **Debug and testing:** `SetTimeOfDay` in `Simulation.Debug`, a `setTimeOfDay` scenario command,
  and the snapshot's `timeOfDay` and `daylight`.

### Stored Power

- **A Battery Bank** holds `Charge` (Power-seconds, up to `StoreCapacity` 6000) and moves at most
  `StoreRate` (50 Power) in or out a second. It must be switched on and online to do either; it
  keeps its charge while off or offline.
- **Each tick, after supply and demand are summed** (`StepCapacity`, Power only): with a surplus,
  the banks charge from it (each up to its rate, filling the emptiest first); with a shortage, they
  discharge to cover it (each up to its rate and its charge), and what they give counts as supply.
  The state and speed then follow as today, so a bank turns a short brownout into Normal until it
  runs dry.
- A Burner Generator still burns whenever it is switched on, so its surplus is what the banks store;
  switching generators off and running on stored Power is the player's choice.
- `Capacity(Power)` gains the stored total (`Stored`, `StoreCapacity`) and whether the banks are
  charging or discharging this tick.

### Fuels by building

- **Fuel Rods get a burn time** (300 s) and so become a fuel, but only the Fission Reactor burns
  them, and it burns nothing else. A definition lists the fuels it takes (`BuildingDef.Fuels`): the
  reactor Fuel Rods; every other fuel burner Coal, Charcoal, and Wood as today. Best and the fuel
  picker offer only those.
- A rod is spent once taken, as any fuel is.

### UI

- **Top bar:** a small clock before the power readout, a sun icon by day and a moon icon by night
  with the hour ("Day 14:00", "Night 02:00", the day mapped to 24 hours), so the player can see
  the dark coming. The power readout starts with a Power icon (a lightning bolt, rendered from a
  model like the item icons) in place of the word: "[bolt] 80 / 200", or "Blackout: [bolt] 140 /
  100". With at least one Battery Bank, the power readout is followed by the Battery
  item's icon (the model-rendered icon, as the resource entries use) and "3200 / 6000", in the
  warning color while discharging.
- **Battery Bank panel:** a Charge row ("3200 / 6000, charging" or "discharging" or "full"), with
  a small bar.
- **Fission Reactor panel:** the fuel rows as for any fuel burner (Fuel Rods only), its Supplies row,
  and its Water draw.
- **Placement tooltip** as for any building; nothing new.

### Assets

Three building models with accent colors, kept preview sheets, and README rows:

- **Solar Array (6 x 6):** three rows of tilted dark-blue panels with silver lines on low steel
  frames, an inverter box with a power-yellow band at the front (`solar`, `solar_line`, and
  `power_accent`).
- **Battery Bank (3 x 4):** a concrete pad with two rows of white battery cabinets, each with a
  green charge light, a transformer at the front (`battery_accent`, a charge green). The model is
  static.
- **Fission Reactor (6 x 8):** a hyperboloid cooling tower at the back with a plume-white rim, a
  round reactor building with a domed roof, a turbine hall along the front, and an intake pipe
  (`reactor_accent`, a radiation blue-violet).
- **Top bar icons:** a sun and a moon for the clock and a lightning bolt for Power, rendered from
  small models like the item icons (`icon_sun`, `icon_moon`, `icon_power`): a pale yellow disc
  with short rays, a pale crescent, and a flat zig-zag bolt in `power_accent`.

## Code changes

- `BuildingType`, `BuildingDefs`: the three buildings; `BuildingDef.Fuels` (the fuels a burner
  takes) and `StoreCapacity` and `StoreRate` (a Battery Bank).
- `ResourceDefs`: the Fuel Rod's burn time; `Fuels` stays the longest-first order, filtered per
  building.
- `Building`: `Charge`.
- `SimulationCapacity`: banks charging and discharging in `StepCapacity`; fuel choice limited to
  the building's fuels; `CapacityReading` with the stored figures; the Solar Array's supply times
  `Daylight`.
- `Simulation`: `TimeOfDay`, `DayLength`, `Daylight`, advanced each tick; `Debug.SetTimeOfDay`.
- Presentation: the sun and ambient light by the time of day; the top bar's clock, Power icon,
  and battery readout; the Battery Bank's Charge row; model file names.
- Testing: unit tests, a `power-consumers` scenario, a `setTimeOfDay` command, the schema's
  building list, the build menu's cells, snapshot fields (a building's `charge`,
  `capacity.power.stored`, `timeOfDay`, `daylight`).
- Assets: three models, the sun, moon, and power icon models, previews, README rows, the new palette
  entries.

## Blueprint updates

- **buildings.md:** the three buildings; stored Power; fuels by building.
- **resources.md:** the Fuel Rod as a fuel; where Batteries, Solar Cells, Heat Shields, Sensor
  Arrays, and Fuel Rods go.
- **simulation.md:** the time of day.
- **presentation.md / ui-style.md:** the day and night light, the clock, the battery readout, and
  the Charge row.
- **art-style.md:** the new accents and models.
- **testing.md:** new tests and scenario.

## Verification

- Unit tests: the clock advancing and wrapping; daylight at night, dawn, midday, and dusk; the
  Solar Array's supply following daylight with no fuel; a Battery Bank charging from a surplus
  up to its rate and capacity, discharging into a shortage so the state stays Normal, and running
  dry into a blackout; a bank off or offline neither charging nor discharging but keeping its
  charge; the reactor burning a Fuel Rod every 300 s, supplying 400 Power, and supplying less in a
  Water brownout; the Smelter, Glassworks, and Burner Generator never taking a Fuel Rod and the
  reactor never taking Coal.
- Scenario: `power-consumers` places a Solar Array and a Battery Bank on a Burner Generator at
  midday, watches the bank charge, switches the generator off and sets night, and sees the bank
  carry the load (the top bar's battery readout falling, its clock showing night), then places a
  Fission Reactor with Fuel Rods and Water and checks its supply.
- Both test suites pass; windowed screenshots show the three models by day and the world at
  night.

## Decisions

1. **Battery Banks smooth:** they store any surplus and cover any shortage, up to 50 Power each, 6000
   stored.
2. **A day and night cycle comes with this changeset;** the Solar Array follows daylight (up to 60
   Power) and Battery Banks carry it through the night.
3. **The reactor's numbers:** 400 Power, a Fuel Rod every 300 s, 8 Water drawn; it stands anywhere.
4. **Life Support Units** stay without a consumer, for Part B's crewed ships.
5. **The Battery Bank model is static.**
6. **The top bar shows stored Power with the Battery item's icon,** not the word "Stored".
7. **Power gets an icon too:** a lightning bolt model in place of the word "Power" on the top bar.
8. **Ten-minute days** (six of full light, two of twilight, two of dark) and a dim but readable
   night.
9. **The clock** is a sun or moon icon with a 24-hour time ("Day 14:00").

## Implementation notes

- **The clock shows only the hour** ("14:00") beside its sun or moon icon, not "Day 14:00": with
  the clock, the bolt, and the battery readout added, the bar ran past 1280 wide, and the icon
  already says day or night. Every section now sits `Gap` (8) apart and each icon 3 units from its
  text. With a Battery Bank, "Ships: n" after a launch can still run past the edge.
- **The light never moves** (reviewed): a sun swinging across the sky made the hard shadows
  flicker and shudder, so the light keeps the art style's angle and only its color and energy
  follow the clock.
- **No ambient light at night:** the art style keeps ambient light off, so the night is the blue
  moonlight alone, at energy 0.76 (raised from 0.38 in review, which was too dark).
- **The power readout drops the word** in the snapshot too: `ui.topBar.power` reads "45 / 100" or
  "Blackout: 140 / 100" (the `power` scenario is updated).
- **The Charge row is text only** ("3200 / 6000, charging"), with no bar.
- **The reactor's Water** comes after Power in the capacity order, so its supply follows last
  tick's Water speed (a one-tick lag), the same rule in reverse to the Pumping Station's.
- **Icon models** live in a new `assets/models/icons/` folder (with its README), generated by
  `item_gen.py` (`ICONS`) and rendered by `ResourceIcons.Icon(name)`.
