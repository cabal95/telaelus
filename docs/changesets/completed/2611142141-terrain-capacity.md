# Terrain Capacity

- Status: Implemented
- Id: 2611142141-terrain-capacity
- Depends-on: none

## Summary

Makes Water and Sand Capacity resources on the HQ network, like Power: providers built on the
right terrain supply them, buildings that need them reserve them while switched on, and a
shortage brownouts and then blackouts the consumers, by the same rule as Power. Adds four
providers (the Water Pump and Sand Collector, and their larger powered forms, the Pumping Station
and Sand Dredge), the Glassworks as Sand's first use (Glass, by fuel and 2 Sand), and Water as the
Refinery's cooling.

## Motivation

The fourth follow-on of 2611141757-resource-tree (section 5, decisions 3 and 14 there). Water and
Sand are defined, discovered, and pinned to the top bar, but nothing provides or uses them. As
Capacity, their quantity never matters, only how much the network can supply at once, which ties
the base to rivers, lakes, and desert and gives the first non-raw recipe (Glass) a place to start.

## Design

### Scope

- **In:** one Capacity model for Power, Water, and Sand; the four providers and the Glassworks with
  their models; the Refinery needing Water; Water and Sand readouts in the top bar, inventory,
  panel, and tooltip.
- **Out:** the Rock Crusher (Stone into Sand capacity anywhere; see the open questions), Concrete
  and the other Water and Sand recipes (production tiers), and anything that stores Water or Sand.

### One Capacity model

The power changeset built supply, demand, overload, speed, and state for Power. This generalizes
it to a **capacity type**: Power, Water, or Sand.

- A building definition's `PowerDraw` and `PowerSupply` become **Draws** and **Supplies**, each a
  small map from capacity type to amount (the Refinery draws 25 Power and 2 Water). Every existing
  caller moves to the maps and the two fields are removed.
- Each tick, for each capacity type: supply is the sum over online, switched-on providers that run;
  demand the sum over online, switched-on consumers, working or not; the state and speed follow
  the power rule exactly (double the overload up to 25% over, then a blackout).
- A consumer that needs several capacity types runs at the **slowest** of their speeds; it is
  blocked ("Blackout: Sand", "No Water: offline") when any of them is. Offline, the reason names
  the first type it draws, so a Refinery reads "No Power: offline", and a power blackout now reads
  "Blackout: Power" where it read "Blackout".
- Water and Sand are never stored. They keep their `ResourceType`, icon, and discovery; their
  storage class stays Unlimited and nothing ever adds them to storage.

### Providers (placeholders)

| Building | Footprint | Cost | Supplies | Draws | Placement |
|---|---|---|---|---|---|
| Water Pump | 2 x 2 | 20 Wood, 10 Stone | 4 Water | | On buildable land with at least one footprint cell beside (edge-adjacent to) ShallowWater, DeepWater, or a river channel |
| Pumping Station | 3 x 3 | 40 Iron, 20 Stone | 16 Water | 10 Power | The same rule |
| Sand Collector | 2 x 2 | 20 Wood, 10 Stone | 2 Sand | | Every footprint cell on Sand terrain |
| Sand Dredge | 3 x 3 | 40 Iron, 20 Stone | 8 Sand | 10 Power | Every footprint cell on Sand terrain |

- The small providers are tier 0 (unpowered, cheap); the large ones tier 1, so "four small or one
  large" holds: a Pumping Station is four Pumps' worth for some Power.
- A powered provider supplies only at its power speed: in a power brownout at 80% a Pumping
  Station supplies 12.8 Water, and in a power blackout nothing. So a power shortage can cascade
  into a water shortage, which reads as cause and effect.
- Offline providers supply nothing, like offline generators.

### Consumers

| Building | Footprint | Cost | Recipe | Draws | Fuel |
|---|---|---|---|---|---|
| Glassworks (new) | 3 x 6 | 40 Stone, 20 Wood | 1 Glass every 12 s, no stored inputs | 2 Sand | burns fuel, like the Smelter |
| Refinery (changed) | 2 x 3 | as today | as today | 25 Power, 2 Water | |

- The Glassworks is tier 0: a furnace, so it burns fuel like the Smelter (Coal, Charcoal, or Wood,
  Best by default) and needs no power. Glass goes to the Cargo pool (weight 1).
- The Refinery's Water is its cooling; without it the oil chain stalls the same way it does without
  power.

### UI

- **Top bar:** the Sand and Water pins become capacity readouts like Power, demand over supply
  beside their icon ("2 / 4", or "Blackout: 6 / 4" in a blackout), colored normal, `Warning`, or
  `Danger`. Power keeps its own place on the right ("Power 10 / 100").
- **Inventory:** the Sand and Water rows show the same text ("Water (2 / 4)") and color instead of
  an amount.
- **Building panel:** the Power row generalizes to one row per capacity type the building draws or
  supplies, named for the type (Water: "Draws 2 (running at 80%)"; Sand: "Supplies 2"); a provider
  slowed by its own draws shows "Supplies 12.8 of 16". A recipe with no inputs reads "1 Glass,
  12 s".
- **Placement tooltip:** for each capacity type the building draws or supplies, the network's
  figure with it added ("Draws 2 Sand: Sand 4 / 2"), colored by the state it would give.
- **Placing a provider** on the wrong ground is refused like any bad spot (the ghost goes red).

### Assets

Five building models, each with an accent color, kept preview sheets, and README rows:

- **Water Pump:** a small pump house with a pipe reaching down toward the water on one side, and a
  hand-wheel valve (`water_accent`, a light blue).
- **Pumping Station:** a larger pump hall with two intake pipes, a tank, and a power pole
  (`water_accent`, with `power_accent`).
- **Sand Collector:** a hopper on legs over a sand pile, with a short chute (`sand_accent`, a warm
  tan).
- **Sand Dredge:** a conveyor and scoop wheel over a sand pit, a motor house, and a power pole
  (`sand_accent`, with `power_accent`).
- **Glassworks:** a long building, 3 wide and 6 deep: a stone furnace with a glowing kiln mouth at
  the back, a cooling line of panes down its length, and a rack of finished panes at the front
  (`glass_accent`, a pale cyan).

## Code changes

- `CapacityType` (Power, Water, Sand), `CapacityState`, `CapacityReading`, and
  `CapacityResources.CapacityOf` (Water and Sand to their types).
- `BuildingDefs`: the `Draws` and `Supplies` maps replacing `PowerDraw` and `PowerSupply`; a
  `Placement` rule (Standard, AcrossStraightRiver, BesideWater, OnSand) replacing `OnStraightRiver`;
  the five new buildings; the Refinery's 2 Water.
- `Recipes`: the Glassworks recipe (no inputs, 1 Glass, 12 s).
- `SimulationPower` becomes `SimulationCapacity`: `Capacity(type)` with supply, demand, state, and
  speed per capacity type (replacing `PowerSupply`, `PowerDemand`, `PowerSpeed`, `PowerState`);
  `WorkSpeed` (now public) as the slowest of a building's types; providers scaling their supply by
  it; `CapacityBlock` replacing `PowerBlock`.
- `Simulation`: placement for the new rules; status reasons per capacity type.
- Presentation: `HudOverlay.CapacityText`, `CapacityColor`, and `AmountColor` (replacing
  `PowerText` and `PowerColor`); the top bar's Sand and Water readouts, inventory rows, panel
  capacity rows, tooltip lines; the model file names.
- Testing: snapshot `capacity` per type (replacing `power`), the top bar items' `text` and `tone`,
  `CapacityTests`, a `capacity` scenario, the schema's building list, and existing tests and
  scenarios updated (Refineries given a Water Pump by the test map's pond, "Blackout: Power", the
  build menu's new cells, `power` reading `capacity.power`).
- Assets: five models, previews, README rows, the new palette entries.

## Blueprint updates

- **buildings.md:** the Capacity model replacing the Power section's specifics, the providers and
  their placement, the Glassworks, the Refinery's Water.
- **resources.md:** Water and Sand as Capacity resources, never stored.
- **presentation.md / ui-style.md:** the readouts, panel rows, and tooltip lines.
- **art-style.md:** the new accents.
- **testing.md:** new tests and scenario.

## Verification

- Unit tests: supply and demand per type over online buildings; a consumer drawing two types running
  at the slower; a powered provider's supply scaled by power speed and cut by a power blackout; the
  placement rules (beside water by edge, all-Sand); the Glassworks making Glass from Sand and fuel
  and stopping without either; the Refinery stopping without Water; Water and Sand never entering
  storage.
- Scenario: a `capacity` scenario places a Glassworks with no Sand (blackout on Sand), adds Sand
  Collectors and sees Glass made, pushes Sand into a brownout, and checks the Sand readout's color
  on the top bar.
- Both test suites pass; a windowed run shows the five models and the readouts.

## Decisions

1. **The Glassworks is a tier-0 furnace** that burns fuel, like the Smelter.
2. **The Refinery draws 2 Water** for cooling from this changeset on.
3. **The Rock Crusher waits for the renewables changeset.**
4. **The large providers come now:** the Pumping Station and the Sand Dredge.
5. **The Glassworks is 3 x 6.**
