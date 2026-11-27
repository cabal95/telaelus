# Process Speed Research

- Status: Implemented
- Id: 2611262034-process-speed-research
- Depends-on: 2611261944-leveled-buildings

## Summary

Eight research projects make four building families work faster at every level: the Mine, Lumber
Camp, and Smelter each get a tier 1 project (+25%) and a tier 2 project (+50%, in place of the
first), and the Oil Well the same in tiers 2 and 3. A building's speed becomes a sum of parts
over a base of 100% (its level, its family's research, its fuel), scaled down by any shortage,
and the detail panel shows that speed and its parts.

## Motivation

Levels make a building faster by replacing it. Process speed research is the other half of the
plan in `future/building-levels.md` (phase 5): a lasting boost to every building of a family, old
and new, that the player picks in the research tree. Tier 1 research is nearly empty today
(Material Transfer, Steel Works, and Cement), so the first steps give the opening game real
research choices.

Speed is today a product of hidden factors (a level 1 extractor runs at 0.75 of hand gathering,
then times Power, times fuel), which is hard to show. Adding parts over a 100% base gives one
number the player can read and a tooltip that adds up.

## Design

- **Speed as a sum of parts:** a building works at
  `(100% + level part + research part + fuel part) x shortage`.
  - **Base, 100%:** a level 1 building. A level 1 extractor is the base speed, and hand gathering
    is what changes: the extractor's time per unit becomes the resource's gather time
    (`GatherSeconds`, today's extractor time, 4/3 of today's hand time), and the player gathers by
    hand at **+33%** (`HandGatherSpeed`, 4/3). So every rate in the game stays as it is today.
  - **Level part:** level 2 **+75%**, level 3 **+115%**, for the Mine, Lumber Camp, and Oil Well
    working a field. These are today's 1.3 and 1.6 measured from level 1 (1.73 and 2.13, rounded
    to 1.75 and 2.15). A Smelter's levels add no speed, and a deep level's site keeps its own
    rate, as today.
  - **Research part:** +25% after the I project, **+50%** after II (II replaces I).
  - **Fuel part:** the burning fuel's boost, as today (Coal +30%, Charcoal +15%), now added
    rather than multiplied.
  - **Shortage:** the slowest of the building's draws (a Power brownout at 60% gives x0.6; a
    blackout or a Water shortage to 0 stops it), multiplying the sum, as today. A shortage
    scales the whole sum: a building with half its Power works at half its boosted speed.
  - So a Drill Rig with Better Mining I works at 100 + 75 + 25 = 200%, and at 120% in a 60%
    brownout. A Deep Mine with Better Mining II tops out at 100 + 115 + 50 = 265%.
- **The projects:**

  | Family | First step (+25%) | Second step (+50%) |
  |---|---|---|
  | Mine | Better Mining I (tier 1) | Better Mining II (tier 2) |
  | Lumber Camp | Better Logging I (tier 1) | Better Logging II (tier 2) |
  | Smelter | Better Smelting I (tier 1) | Better Smelting II (tier 2) |
  | Oil Well | Better Drilling I (tier 2) | Better Drilling II (tier 3) |

  - **Needs:** a II project needs its I project. A I project needs only its tier: the boost
    applies at every level, level 1 included, so it does not wait for the level research. The
    Oil Well's steps sit a tier later because the Oil Well itself is tier 2.
  - **Descriptions:** "Mines work 25% faster, at every level." and "Mines work 50% faster, at
    every level, in place of Better Mining I's 25%." The Smelter's say "on every recipe".
- **Fees and work** (placeholders; a fee uses only items of the project's tier or below):

  | Project | Tier | Work | Fee |
  |---|---|---|---|
  | Better Mining I | 1 | 600 s | 40 Iron, 20 Copper |
  | Better Logging I | 1 | 600 s | 30 Iron, 40 Wood |
  | Better Smelting I | 1 | 600 s | 40 Iron, 20 Glass |
  | Better Mining II | 2 | 900 s | 60 Steel, 20 Concrete |
  | Better Logging II | 2 | 900 s | 40 Steel, 40 Lumber |
  | Better Smelting II | 2 | 900 s | 60 Steel, 30 Copper |
  | Better Drilling I | 2 | 900 s | 40 Steel, 20 Copper |
  | Better Drilling II | 3 | 1500 s | 60 Steel, 20 Concrete, 10 Electric Motor |

- **Where they sit in the tree:** on their family's row (Mine row 2, Lumber Camp row 3, Smelter
  row 7), first in each section, so the line from I to II runs straight from one section to the
  next without passing behind another card:
  - Tier 1, column 0: Better Mining I, Better Logging I, Better Smelting I (those rows are empty
    in Tier 1 today);
  - Tier 2, column 0: Better Mining II, Better Logging II, Better Smelting II; the level projects
    on those rows move right one column (Powered Mining and Powered Logging to column 1,
    Steelmaking to 1, Electric Smelting to 2);
  - the Oil Well's steps on a row of their own, row 9, column 0 of Tiers 2 and 3: its level
    projects span the same two tiers, so on a shared row one chain's line would cross the other.

  Each card's picture is the family's level 1 building.
- **What faster means:** everything that already runs at work speed speeds up with it: an
  extractor's gathering from a field, a deep level's work on a site (research and shortage only),
  and a Smelter's recipe cycle at every level and for every recipe. What does not change: Power
  draw, output buffers, and fuel burn by the second (a faster Smelter uses less fuel per ingot, as
  the fuel boost already does).
- **The Speed entry on the detail panel:** a building that works (not storage or a hub) shows a
  **Speed** entry in its status strip whenever its speed is not exactly 100%.
  - **Value:** the change from 100%: "+100%" for that Drill Rig, "+20%" in the brownout, "-100%"
    in a blackout.
  - **Tooltip:** each part in turn, then the total when a shortage applies:
    "+75% from level 2 building, +25% from Better Mining I" or "+75% from level 2 building,
    +25% from Better Mining I, x60% from Power brownout: +20%". The added parts sum to the value;
    a shortage is shown as the share it lets through.
  - **Tone:** good (green) with no shortage; warning while a shortage applies, whatever the total.
  - Brownouts keep their own Power entry too; the fuel line keeps naming its boost.
- **On screen otherwise:** the project's card, drawer, and completion toast as for any project,
  with the family's picture. The cycle and time-left readouts already follow work speed.
- **Saves:** research progress is saved by project name, so the eight new projects need no format
  change and no new save version. They go at the end of `ResearchProject`.

## Code changes

- `BuildingDefs.cs`: `BaseSpeed` becomes 1; the level 2 and 3 extractors' `Speed` becomes 1.75 and
  2.15 (read as 100% plus the level part).
- `Simulation.cs`: an extractor's time per unit is `GatherSeconds` (no division by `Def.Speed`;
  the level's speed is in `WorkSpeed`); hand gathering advances at `HandGatherSpeed` (4/3).
  `ResourceDefs` gather times become today's extractor times (each times 4/3), so hand and
  extractor rates are unchanged.
- `ResearchDefs.cs`:
  - eight `ResearchProject` values at the end of the enum;
  - `ResearchDef.Speeds`, a `(BuildingType Family, float Bonus)?` (0.25 or 0.5);
  - a `SpeedProject(name, family, bonus, tier, work, fee, needs, column, row)` helper writing the
    description, as `LevelProject` does.
- `SimulationResearch.cs`: `ResearchBonusOf(BuildingType)`, the largest bonus among the family's
  complete projects and the project it comes from, or none.
- `SimulationCapacity.cs`:
  - `SpeedParts(Building)`: the level part (an extractor on a field), research part, fuel part,
    and shortage, for the sim and the panel alike;
  - `WorkSpeed` becomes `(1 + parts) x shortage` from them; `FuelSpeed` becomes the fuel part.
- Presentation:
  - `ResearchTree.Picture` gives a speed project its family's level 1 picture;
    `ResourceIcons.UiIconNames` skips speed projects as it skips level projects;
  - `BuildingDetail.StripEntries` adds the Speed entry from `SpeedParts` (`speed` in the
    snapshot's strip), with a `speed` icon;
  - `NotificationText` uses the picture for the completion toast, as for a level project.
- Asset factory: a `speed` UI icon (a gauge with its needle to the right), with its kept preview.
- Tests:
  - `ProcessSpeedTests` (sim): each project's tier, place, fee tier, and needs; a Mine's rate
    unchanged from today with no research; a Mine at 125% after I and 150% after II (not 175%); a
    Drill Rig at 200% with Better Mining I, and 120% in a 60% brownout; a Deep Mine at 265% on a
    field; a Smelter on Coal with Better Smelting I at 155%; a Steel Mill's draw unchanged; a II
    project locked until its I is done; a save round trip keeping the boost;
  - existing rate tests (extractor, hand gathering, fuel) keep their numbers, which shows the
    rebase changed no rate;
  - `research` scenario: Tier 1's new cards and its section's check mark needing them;
  - a `process-speed` scenario: a level 1 Mine with no Speed entry, then "+25%" after Better
    Mining I; a Drill Rig's "+100%" and its tooltip; the same in a brownout, "+20%" in the
    warning tone;
  - `scenario.schema.json`: the eight projects in the research enum.

## Blueprint updates

- buildings.md: speed as parts over a 100% base, the level parts, and the shortage rule.
- resources.md: gather times as the extractor base, and hand gathering at +33%.
- research.md: a "Process speed projects" table beside the level projects, the replace rule, and
  where the cards sit.
- presentation.md: the detail panel's Speed entry, its value, tooltip, and tone.
- testing.md: `ProcessSpeedTests`, the `process-speed` scenario, and the `speed` strip entry.
- art-style.md and the UI icon list: the `speed` icon.
- future/building-levels.md: phase 5 done, with the speeds restated over a level 1 base.

## Verification

- Sim and integration tests pass, and the headless runtime check is clean.
- Windowed:
  - the research tree shows the Mine, Lumber Camp, and Smelter I projects in Tier 1, their II
    projects in Tier 2, and the Oil Well's in Tiers 2 and 3, on their family's rows;
  - a Mine shows no Speed entry, then "+25%" in green after Better Mining I, and fills its buffer
    faster;
  - a Drill Rig in a brownout shows its reduced Speed in the warning tone, its tooltip naming the
    brownout.

## Notes

- **Rounded level parts:** +75% and +115% are today's 1.3 and 1.6 over 0.75 (+73% and +113%),
  rounded, so a Drill Rig and Deep Mine get about 1% faster. Exact values would keep today's
  rates but read oddly.
- **Adding changes one old case:** a fuel boost used to multiply, and now adds. Nothing combines
  it with another part today (fuel burners have no level part), so no rate changes until Better
  Smelting is researched: a Smelter on Coal with Better Smelting I is 155%, where multiplying
  would give 162.5%.
- **Later:** the Water Pump, Sand Collector, and Kiln could follow the same pattern. For the Water
  Pump and Sand Collector, "faster" would mean more Water or Sand supplied, which needs its own
  rule. Not in this changeset.
- **Balance:** all fees, work times, and bonuses are placeholders to tune after playing.

## Implementation notes

- **Tree placement changed after a windowed look:** placed after the level projects, the line from
  Better Mining I to II ran behind the Powered Mining card. The speed steps now come first on
  their rows and the level projects move right; the Oil Well's steps have row 9 (Design above).
- **No rate changed:** the existing extractor, hand-gathering, fuel, and power rate tests passed
  unchanged after the rebase, which is the check that moving the 0.75 from the extractor to hand
  gathering kept every rate.
- **Description of a second step:** written from the first step's name and its 25%, since the
  project table is still being built when the descriptions are.
- **The `speed` icon** is `icon_mono` (the UI tints it): a half dial with ticks and a needle to the
  right.
- **Strip entries have no hover id**, so the `process-speed` scenario checks the tooltip through
  the snapshot's strip rather than by hovering.
