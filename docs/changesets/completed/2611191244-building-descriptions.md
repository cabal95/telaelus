# Building Descriptions

- Status: Implemented
- Id: 2611191244-building-descriptions
- Depends-on: 2611191242-hq-unloading-pad

## Summary

Every buildable building gets a one-line description of what it makes or gives you and what it
needs. The build menu's hover tooltip shows it above the cost, so a player knows what a Rock
Crusher is for before building one.

## Motivation

The build menu's tooltip lists only the cost. Names like Rock Crusher, Relay, or Air Separator say
little about what they produce, need, or unlock, so the only way to learn is to build one and read
its panel.

## Design

- `BuildingDefs.DescriptionOf(type)` gives each building's description, a sentence of at most
  about 70 characters in plain words (the table below), "" for the HQ. It names resources and buildings as the game shows them.
- **The build menu's tooltip:** the description first, in `TextDim`, broken between words into
  lines of about 44 characters (`TipLine.Wrapped`), then the cost lines as now. An undiscovered
  building keeps its cost and its description a mystery.
- A test checks every buildable building has a description, under 80 characters, with no long
  dash.

### The descriptions

| Building | Description |
|---|---|
| Mine | Digs Iron Ore, Copper Ore, Coal, Stone, and other ore deposits nearby. |
| Lumber Camp | Cuts Wood from nearby forests. |
| Smelter | Smelts Iron Ore or Copper Ore into Iron or Copper; burns fuel. |
| Refinery | Refines Crude Oil into Kerosene; needs Power and Water. |
| Oil Well | Pumps Crude Oil; build it right beside an oil seep. |
| Silo | Raises the storage limit of one chosen Bulk resource, such as Wood. |
| Stockyard | Raises the Wood, Stone, ore, and other Bulk storage limits. |
| Tank Farm | Raises the Crude Oil, Kerosene, and other Fluid storage limits. |
| Warehouse | Adds Cargo space for Iron, Steel, parts, and other goods. |
| Vault | Raises the Rare Earths, Uranium Ore, and other Secure storage limits. |
| Small Hub | Extends the HQ's network to buildings nearby. |
| Relay | Links far-off hubs back to the HQ's network over a long distance. |
| Water Wheel | Makes Power with no fuel; build it across a straight river. |
| Burner Generator | Makes lots of Power by burning Coal, Charcoal, or Wood. |
| Lumber Mill | A faster, powered Lumber Camp: cuts nearby Wood; needs Power. |
| Drill Rig | Powered Mine: digs ores plus Rare Earths, Uranium Ore, Crystals. |
| Water Pump | Supplies a little Water; build it beside water. |
| Pumping Station | Supplies four Pumps' worth of Water beside water; needs Power. |
| Sand Collector | Supplies a little Sand; build it on sand. |
| Sand Dredge | Supplies four Collectors' worth of Sand on sand; needs Power. |
| Glassworks | Makes Glass from Sand; burns fuel. |
| Depot | Its trucks haul resources from collectors back to the HQ. |
| Forester | Plants a Wood field that regrows on open grass; needs Power, Water. |
| Quarry | Cuts Stone (or Limestone) beside a mountain; needs Power. |
| Charcoal Kiln | Turns Wood into Charcoal for fuel and steelmaking. |
| Rock Crusher | Supplies Sand anywhere by crushing Stone; needs Power. |
| Deep Well | Pumps Crude Oil from a worked-out oil site or Salt Dome; needs Power. |
| Deep Mine | Keeps mining a worked-out ore site or a motherlode; needs Power. |
| Steel Mill | Makes Steel from Iron, Coal or Charcoal, and Limestone; needs Power. |
| Electrolyzer | Makes Hydrogen or Liquid Oxygen from Water; needs Power. |
| Fuel Plant | Makes Rocket Fuel from Kerosene and Hydrogen; needs Power. |
| Chemical Plant | Makes Plastic, Rubber, and more from Crude Oil and Sulfur; needs Power. |
| Aluminum Smelter | Makes Aluminum from Bauxite; needs lots of Power. |
| Electric Furnace | Makes Silicon from Quartz or Titanium from Titanium Ore; needs Power. |
| Fabricator | Makes parts such as Hull Plate and Steel Beam from metals; needs Power. |
| Electronics Works | Makes Copper Wire and Circuits from Copper and Plastic; needs Power. |
| Tile Kiln | Fires Silicon and Aluminum into Ceramic Tile; needs Power. |
| Assembly Plant | Assembles advanced parts such as Control Module and Fuel Tank. |
| Shipyard | Builds Hull Sections and Engines, and launches your ship. |
| Sawmill | Saws Wood into Lumber. |
| Cement Kiln | Bakes Limestone into Cement; burns fuel. |
| Concrete Mixer | Mixes Cement into Concrete; needs Power, Sand, and Water. |
| Brine Well | Draws Lithium from desert salt flats; needs Power and Water. |
| Semiconductor Fab | Makes Silicon Wafers, Processors, and Solar Cells; needs Power. |
| Air Separator | Makes Liquid Oxygen from the air; needs Power. |
| Enrichment Plant | Turns Uranium Ore into Enriched Uranium and Fuel Rods; needs Power. |
| Solar Array | Makes Power from sunlight, none at night; no fuel. |
| Battery Bank | Stores spare Power and gives it back during shortages. |
| Fission Reactor | Makes huge Power by burning Fuel Rods; needs Water to cool. |
| Research Lab | Speeds up research; each extra lab adds more. Needs Power. |

Some summarize longer lists: the Electronics Works also makes Insulated Cable and Battery, the
Fabricator Magnet and Electric Motor among others, and the Assembly Plant Computer and Heat Shield
among others. A few use plurals ("Circuits", "Hull Sections", "Processors") where the game's item
names are singular.

## Code changes

- `src/Telaelus.Sim/BuildingDefs.cs`: the description table and `DescriptionOf`.
- `src/Telaelus/Presentation/Ui/BuildMenu.cs`: the description as the tooltip's first line.
- `src/Telaelus/Presentation/Ui/Tooltip.cs`: `TipLine.Wrapped`, a long text as several lines.
- `src/Telaelus/Testing/StateSnapshot.cs`: nothing new (the tooltip lines already reach
  `ui.tooltip`).
- Tests: the sim test above; the `build-menu` scenario hovering a cell and seeing its description
  first, and an undiscovered cell showing none.

## Blueprint updates

- buildings.md: `Description`, and the descriptions (a column in the tables, or one list).
- presentation.md and ui-style.md: the tooltip's description line.
- testing.md: the new test and scenario checks.

## Verification

- Sim and integration tests pass.
- Windowed screenshots of the build menu's tooltip on a few buildings.

## Implementation notes

- **A table, not a field:** the descriptions are one dictionary in `BuildingDefs`
  (`DescriptionOf`) rather than a property set in each of fifty definitions, so they read and edit
  together.
- **Wrapping by characters:** the tooltip is one rich text label with wrapping off; breaking the
  description into lines of about 44 characters keeps its layout as it is and shows in the
  snapshot's `ui.tooltip` as separate lines.

## Decisions

1. **The detail panel:** not for now; the description shows in the build menu only.
2. **Undiscovered buildings:** the description stays hidden with the cost.
3. **The wording:** as in the table.

## Open questions (answered)

1. **The detail panel:** show the description there too (proposed), or only in the build menu?
2. **Undiscovered buildings:** keep the description hidden with the cost (proposed), or show what
   it does so the player knows what to aim for?
3. **The wording** in the table: edit any you want reworded before it goes in.
