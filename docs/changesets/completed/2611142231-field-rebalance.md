# Field Rebalance

- Status: Implemented
- Id: 2611142231-field-rebalance
- Depends-on: none

## Summary

Makes resource fields fewer and larger: about four times the amount in each field and about a
third as many fields. A found field now lasts most of the mid game, and finding one is an event.
Every generated world gets a small guaranteed **starter set** of building materials near the spawn
(Wood and Stone). The eight deposits that had no fields (Bauxite, Quartz, Limestone, Sulfur,
Titanium Ore, Rare Earths, Uranium Ore, Crystals) get biome fields and field models, and the Mine
and Drill Rig learn to work them.

## Motivation

The seventh follow-on of 2611141757-resource-tree (section 8 there). Fields today are small and
everywhere: across twelve seeds (measured for this draft), about 30 fields lie within 16 map cells
of the spawn, and Wood, Stone, and Iron Ore are each 3 to 11 cells away. With storage limits,
buffers, and trucks in place, small fields mean constant re-siting and little reason to haul from
far away. Larger, rarer fields give trucks and the network a job and make the coming renewable
bypasses (the renewables changeset) the answer to a near map running dry.

The same survey found Coal 16 to 91 cells from the spawn (median about 20). Ores and Coal are left
to be found: the first expedition is part of the start, and Wood burns in the Smelter meanwhile.
Wood and Stone are guaranteed because every first building needs them.

The new deposits have been defined, discoverable, and listed in the inventory since the resource
catalog, but no world placed them. Placing them now lets a player find and stockpile them ahead
of the production tiers that use them.

## Design

### Scope

- **In:** every biome's field amounts, density, and deposit mix; the global default amount; the
  starter set on generated worlds; fields for the eight new deposits, with their field models; the
  Mine and Drill Rig working them; the fixed test map's amounts (they follow the biomes); tests
  updated to match.
- **Out:** motherlodes and the infinite collectors (the renewables changeset), field footprints (a
  field stays one map cell), and anything that uses the new deposits (the production tiers).

### Amounts, density, and deposits (placeholders)

Every existing amount times 4; every density divided by 3, except the Forest (below). Deposits new
to a biome are marked "new".

| Biome | Fields: amount each (weight) | Density, now to new |
|---|---|---|
| Grassland | Wood 1000 (2), Stone 1000 (1), Iron Ore 1000 (1), Limestone 1000 (1) new | 0.04 to 0.013 |
| Forest | Wood 2000 (1) | 0.30 to 0.15 |
| Highlands | Iron Ore 1600 (3), Copper Ore 1200 (2), Coal 1600 (2), Stone 1600 (2), Bauxite 1600 (1) new, Limestone 1600 (1) new, Quartz 1600 (1) new | 0.07 to 0.023 |
| Desert | Copper Ore 1000 (1), Crude Oil 2000 (2), Bauxite 1000 (1) new, Titanium Ore 1000 (1) new | 0.04 to 0.013 |
| Swamp | Wood 1000 (2), Sulfur 1000 (1) new | 0.06 to 0.02 |
| Tundra | Iron Ore 1000 (2), Quartz 1000 (1) new, Titanium Ore 1000 (1) new, Rare Earths 600 (1) new, Uranium Ore 600 (1) new | 0.03 to 0.01 |
| Volcanic | Iron Ore 1600 (2), Stone 1600 (2), Sulfur 1600 (2) new, Crystals 400 (1) new | 0.04 to 0.013 |
| Ocean | none | 0 |

- `BiomeDef.DefaultFieldAmount` (a field placed with no amount where the biome does not list the
  type, as the debug tools do) goes from 250 to 1000.
- **The Forest halves rather than thirds:** its fields are its trees, and at a tenth of its cells it
  would stop reading as a forest. At 0.15 with 2000 Wood each, a Forest holds about twice the
  Wood it does today in a given area.
- **The new deposits follow the resource-tree plan's biomes,** with one change: the plan put
  Titanium Ore on the Ocean shore and in Tundra, but Ocean cells are water and take no fields, so
  it goes to the Desert (beach and desert mineral sands) and Tundra. The precious three (Rare
  Earths, Uranium Ore, Crystals) are smaller fields at lower weight.
- Adding deposits to a biome thins its old ones a little at the same density (Grassland Iron Ore
  goes from one field in four to one in five); the Swamp and Tundra double their old weights so
  Wood and Iron Ore stay the common find there.
- Fields still never touch (a candidate becomes a field only if no neighboring candidate rolled
  lower). That rule thins dense biomes most, so cutting the density removes fewer fields than it
  suggests: measured below, the count falls to about 55 to 60%, not a third.
- What a field shows does not change: its stage is its remaining fraction of what it started with,
  so a 1000-unit field looks full at 1000 and steps down every 200.
- At base speed a Mine takes 10.7 s an Iron Ore unit, so one Mine works a 1000 field for about three
  hours of play, and a Drill Rig (speed 2, 4 s a unit) about 67 minutes.

### Working the new deposits

| Building | Works today | Adds |
|---|---|---|
| Mine | Iron Ore, Copper Ore, Coal, Stone | Bauxite, Quartz, Limestone, Sulfur, Titanium Ore |
| Drill Rig | Iron Ore, Copper Ore, Coal, Stone | Bauxite, Quartz, Limestone, Sulfur, Titanium Ore, Rare Earths, Uranium Ore, Crystals |

- The five common deposits are hand-harvestable already (resources.md) and the Mine works them
  like any ore. The precious three are not hand-harvestable and need the powered Drill Rig.
- **Storage limits apply as they are:** the common five are Bulk, but only Limestone has an HQ base
  (60); Bauxite, Quartz, Sulfur, and Titanium Ore are held only once a Stockyard (or a Silo set to
  one) stands. The precious three are Secure, with no HQ base, so nothing holds them until a Vault
  stands. Until then their collectors fill their buffers and wait ("Full: waiting for pickup"), and
  a hand collect or a truck stops at "Storage full". This is intended: they are found early and
  worked once the player builds for them.

### The starter set

Every generated world places these fields when the spawn's chunks generate, in addition to what
the biomes roll there:

| Field | Count | Amount |
|---|---|---|
| Wood | 2 | 1000 each |
| Stone | 1 | 1000 |

- They sit between 4 and 6 map cells from the spawn cell (inside the starting sight of 6 map
  cells, so all three show when the world opens; outside the spawn's field gap of 2 and clear of
  the HQ), each on its own cell chosen by a hash of the seed among open cells (walkable,
  no feature, not water) in that ring, not touching each other or any rolled field. The rolled
  deposits beside a starter field are dropped so they never touch.
- A starter field takes 1000 whatever biome it lands in. Its terrain is left as it is.
- If the ring has too few open cells (a spawn hemmed in by water and mountains), the search widens
  a cell at a time up to 16; a world whose spawn has no exit is already rejected by the spawn
  search, so this is a safety net, not a design case.
- The fixed test map and the empty map get no starter set: the test map places its fields by hand
  and the empty map has none.

### The fixed test map

Its fields keep their places and take the new biome amounts, so its totals become 4600 Iron Ore,
1200 Copper Ore, 1600 Coal, 2600 Stone, 7000 Wood, and 4000 Crude Oil. It also gains one field of
each new deposit in a matching biome, so they can be seen and tested there: Limestone (12, 14) in
Grassland, Sulfur (6, 1) in the Swamp, Quartz (13, 0), Rare Earths (13, 2), and Uranium Ore
(15, 3) in the Tundra, Bauxite (15, 12) and Titanium Ore (13, 12) in the Desert, and Crystals
(0, 11) in the Highlands (the map has no Volcanic biome, so it takes the default 1000). Tests that count
amounts (field totals, biome amounts) move with it; tests that drain a field set its amount
explicitly already and are unaffected.

### Assets

Eight field model families, three variants each (`_a`, `_b`, `_c`, like the existing deposits), made
by `resource_gen.py`, replacing the item-model stand-in these deposits fall back to today. Each
family gets its kept preview sheet (`previews/<deposit>.png`) and README rows:

- **Bauxite:** reddish-brown rounded clay lumps with pale ochre streaks, low (about 0.7).
- **Quartz:** pale grey rock with milky white six-sided crystal points standing out of it (about
  0.9).
- **Limestone:** blocky pale cream slabs with flat tops and stepped edges (about 0.8).
- **Sulfur:** a low yellow crust over grey rock with bright yellow knobs (about 0.5).
- **Titanium Ore:** dark grey-black rock with a metallic blue-grey sheen in its veins (about 0.8).
- **Rare Earths:** pale rock flecked pink and violet, the item icon's colors (about 0.7).
- **Uranium Ore:** dark rock with yellow-green veins that glow faintly (`uranium_glow`, already in the
  palette; about 0.7).
- **Crystals:** a cluster of tall violet crystal shards that glow (`crystal_light`, already in the
  palette; about 1.1).

Colors match each deposit's item icon so a field and its icon read as the same thing.

## Code changes

- `BiomeDefs`: the new amounts, densities, deposit mixes, and weights; `DefaultFieldAmount` 1000.
- `BuildingDefs`: the Mine's and Drill Rig's `Works` lists.
- `WorldGenerator`: the starter set (cells chosen once with the spawn, placed by the chunk that
  holds each, rolled deposits beside them dropped); a `StarterFields` list for tests.
- `SimulationTerrain`: the test map's new-deposit fields.
- Presentation: `NodeModels` field specs for the eight deposits, and the tree height scaled to the
  new amounts (2.5 units at 1000 or less, 3.5 at 2000 or more, so Forest trees still stand taller).
- Tests: `ResourceTests`, `FieldTests` (test map totals), `WorldGenTests` (the starter set on ten
  seeds: present, in the ring, open cells, never touching another field; the new deposits
  generating), `ExtractorTests` for the new `Works`, the discovery test (the test map now shows
  the newer deposits), and the tests and scenarios whose numbers follow the amounts
  (`HarvestTests`, `DebugTests`, `TerrainModelTests`, `harvest-input`, `building-detail`,
  `field-detail`, `field-stage-views`).
- Assets: 24 field models, eight preview sheets, README rows.

## Blueprint updates

- **resources.md:** the biome deposit table's amounts, densities, and new deposits; the default
  amount; the starter set; which extractor works each deposit.
- **world-generation.md:** the starter set and how it avoids rolled fields; the densities.
- **buildings.md:** the Mine's and Drill Rig's `Works`.
- **art-style.md:** the new field models' looks and sizes.
- **testing.md:** the new and changed tests.

## Verification

- Unit tests: the biome table's new amounts and deposits; the starter set on ten seeds (two Wood
  and one Stone, between 4 and 6 cells from the spawn, on open cells, none touching another
  field); each new deposit appearing in its biomes on generated worlds; a Mine working Limestone
  and a Drill Rig working Crystals; the fixed test map's totals; fields still never touching.
- A measured survey over the same twelve seeds before and after, reported in the changeset: fields
  within 16 map cells of the spawn, and the nearest of each type.
- Both test suites pass; a windowed run shows the new field models on the test map and the
  thinner spread on a generated world.

## Measured

The same twelve seeds as the motivation's survey, after the change (fields generated around the
spawn when the world opens; distances in map cells):

| | Before | After |
|---|---|---|
| Fields generated | 1359 to 2334 (about 1860 on average) | 720 to 1551 (about 1090) |
| Fields within 16 cells of the spawn | 23 to 45 (about 32) | 10 to 30 (about 18, three of them the starter set) |
| Nearest Wood, Stone | 3 to 9 | 4 to 6 (the starter set) |
| Nearest Iron Ore | 4 to 11 | 5 to 21 |
| Nearest Coal | 16 to 91 | 18 to 101 |
| Nearest Copper Ore | 10 to 59 | 11 to 64 |
| Nearest Crude Oil | 14 to 85, none in 4 of 12 | 25 to 90, none in 4 of 12 |

So the field count falls to about 55 to 60% rather than a third (the never-touch rule above); the
amounts are four times larger, so the resource in a given area is about 2.3 times what it was. If
play wants scarcer fields, the densities can drop further.

## Decisions

1. **Amounts times 4 and density divided by 3,** to try in play.
2. **The Forest's density halves** instead, so forests still look like forests.
3. **The starter set is two Wood and one Stone;** ores and Coal are left to be found.
4. **The new deposits get fields now,** with field models.
5. **The starter ring is 4 to 6 map cells** (drafted as 4 to 9), so the starter set is in sight when
   the world opens; found while checking a generated world, where fields beyond 6 cells started
   under fog.
