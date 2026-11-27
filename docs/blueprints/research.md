# Research Blueprint

## Overview

Research is global to the base: one project runs at a time, every working Research Lab speeds it
up, and a complete project's effect lasts for the rest of the game. Progress is counted in
game-world time and never lost. Today there are two projects, both about moving raw resources:
Material Transfer and Advanced Material Transfer, the step after hand collection and trucks. Neither
carries the rare raw resources (Titanium Ore, Rare Earths, Uranium Ore, Crystals): those reach
storage only by hand or by truck.

## Current state

### Projects

- `ResearchProject` lists the projects; `ResearchDefs.For(project)` gives each a `ResearchDef`: a
  name, a description, its **work** (game seconds at one lab's speed), its **fee** (resources paid
  once, to begin), its **prerequisites** (projects that must be complete first), its place in the
  tier's tree (`Column`, `Row`, within the tier), its **tier** (`Tier`, 1 to 4: it is locked while that tier is), the
  building tier it unlocks if it is a tier's unlock project (`Unlocks`), the building level it
  unlocks if it is a level project (`UnlocksLevel`, a type and level; `ResearchDefs.UnlockingLevel`
  finds it), the family and research part if it is a speed project (`Speeds`), and for a transfer
  project the raw resources it carries (`Transfers`).

| Project | Tier | Work | Fee | Prerequisites | Place | Unlocks or transfers |
|---|---|---|---|---|---|---|
| Material Transfer | 1 | 1200 s (two game days at one lab) | 40 Iron, 20 Copper | none | column 0, row 0 | transfers Wood, Stone, Iron Ore, Copper Ore, Coal |
| Advanced Material Transfer | 2 | 1800 s (three game days) | 60 Steel, 20 Glass | Material Transfer | column 0, row 0 | transfers Crude Oil, Bauxite, Limestone, Quartz, Sulfur |
| Steel Works | 1 | 1200 s (two game days) | 60 Iron, 60 Copper, 40 Glass, 80 Wood | none | column 0, row 1 | unlocks Tier 2 |
| Electronics | 2 | 1800 s (three game days) | 60 Steel, 60 Plastic, 40 Aluminum, 120 Wood | Steel Works | column 0, row 1 | unlocks Tier 3 |
| Computers | 3 | 2700 s (four and a half game days) | 60 Circuit, 20 Electric Motor, 40 Steel, 200 Wood | Electronics | column 0, row 1 | unlocks Tier 4 |
| Powered Mining | 2 | 900 s | 40 Steel, 30 Copper | none | column 1, row 2 | Mine level 2, Drill Rig |
| Deep Mining | 3 | 1500 s | 60 Steel, 30 Concrete, 10 Electric Motor | Powered Mining | column 0, row 2 | Mine level 3, Deep Mine |
| Powered Logging | 2 | 900 s | 30 Steel, 40 Wood | none | column 1, row 3 | Lumber Camp level 2, Lumber Mill |
| Advanced Logging | 3 | 1500 s | 40 Steel, 20 Electric Motor | Powered Logging | column 0, row 3 | Lumber Camp level 3, Advanced Lumber Mill |
| Powered Pumping | 2 | 900 s | 30 Steel, 20 Copper | none | column 0, row 4 | Water Pump level 2, Pumping Station |
| Advanced Pumping | 3 | 1500 s | 40 Steel, 20 Concrete, 10 Electric Motor | Powered Pumping | column 0, row 4 | Water Pump level 3, Advanced Pumping Station |
| Sand Sifting | 2 | 900 s | 30 Steel, 20 Glass | none | column 0, row 5 | Sand Collector level 2, Sand Sifter |
| Dredging | 3 | 1500 s | 40 Steel, 10 Electric Motor | Sand Sifting | column 0, row 5 | Sand Collector level 3, Sand Dredge |
| Oil Rigs | 2 | 900 s | 40 Steel, 20 Copper | none | column 0, row 6 | Oil Well level 2, Oil Rig |
| Deep Drilling | 3 | 1500 s | 60 Steel, 20 Concrete, 10 Electric Motor | Oil Rigs | column 0, row 6 | Oil Well level 3, Deep Well |
| Steelmaking | 2 | 900 s | 60 Iron, 30 Copper, 40 Stone | none | column 1, row 7 | Smelter level 2, Steel Mill |
| Electric Smelting | 2 | 1200 s | 60 Steel, 30 Copper, 20 Glass | Steelmaking | column 2, row 7 | Smelter level 3, Electric Smelter |
| Cement | 1 | 600 s | 30 Iron, 40 Stone, 40 Wood | none | column 0, row 8 | Kiln level 2, Cement Kiln |
| Tile Firing | 3 | 1500 s | 40 Concrete, 20 Silicon, 20 Steel | Cement | column 0, row 8 | Kiln level 3, Tile Kiln |

(placeholders; a game day is `Simulation.DayLength`, 600 s. The unlock projects' fees are modest
because they will later gain prerequisites of their own.)

- **Level projects** (buildings.md, Levels) sit in their level's tier, a row per family. Each is
  described as "The Mine can be built or upgraded to a Drill Rig." and the level's description.
  Steelmaking is paid in tier 1 items, since Steel is made only once it is done; the other tier 2
  level fees take Steel. Its card, drawer, and toast show the level's building picture
  (`ResearchTree.Picture`) in place of an icon model.
- **Speed projects** make a family work faster at every level, level 1 included (buildings.md,
  Capacity): two steps per family, the second needing the first and replacing its part, not
  adding to it. Each is described as "Mines work 25% faster, at every level." (the second "...,
  in place of Better Mining I's 25%."; the Smelter's "at every level and on every recipe"). The
  first step needs nothing but its tier. Its card, drawer, and toast show the family's level 1
  building picture.

  | Project | Tier | Work | Fee | Prerequisites | Place | Speeds |
  |---|---|---|---|---|---|---|
  | Better Mining I | 1 | 600 s | 40 Iron, 20 Copper | none | column 0, row 2 | Mine +25% |
  | Better Mining II | 2 | 900 s | 60 Steel, 20 Concrete | Better Mining I | column 0, row 2 | Mine +50% |
  | Better Logging I | 1 | 600 s | 30 Iron, 40 Wood | none | column 0, row 3 | Lumber Camp +25% |
  | Better Logging II | 2 | 900 s | 40 Steel, 40 Lumber | Better Logging I | column 0, row 3 | Lumber Camp +50% |
  | Better Smelting I | 1 | 600 s | 40 Iron, 20 Glass | none | column 0, row 7 | Smelter +25% |
  | Better Smelting II | 2 | 900 s | 60 Steel, 30 Copper | Better Smelting I | column 0, row 7 | Smelter +50% |
  | Better Drilling I | 2 | 900 s | 40 Steel, 20 Copper | none | column 0, row 9 | Oil Well +25% |
  | Better Drilling II | 3 | 1500 s | 60 Steel, 20 Concrete, 10 Electric Motor | Better Drilling I | column 0, row 9 | Oil Well +50% |

  A speed chain sits first on its row in each section, so its line runs straight from one
  section to the next; the Oil Well's has a row of its own (row 9), since its level projects
  span the same tiers.

### Tiers

- **The unlocked tier** (`Simulation.UnlockedTier`, 1 to 4) is 1 plus the number of complete
  projects that unlock a tier. It is derived from research and never stored. It decides which
  buildings can be placed (buildings.md) and which projects can start.
- Each unlock project belongs to the tier below the one it unlocks and is paid in items of its own
  tier or below (and Wood). Completing one shows the "Research complete" toast and a second,
  "Tier 2 buildings and research are now available" (presentation.md).

### Running research

- **State** (on `Simulation`): `ActiveResearch` (the running project or null), and per project its
  work done (`ResearchProgress`, game seconds, accumulated in double so a long project does not
  drift), whether its fee is paid (`ResearchFeePaid`), and whether it is complete (`IsResearched`).
  `StateOf(project)` reads Locked (its tier above the unlocked tier, or a prerequisite incomplete),
  Available (never started), Running,
  Paused (fee paid or some work done, not running), or Complete. A world reset clears it all.
- **Starting** (`StartResearch`, checked by `CanStartResearch`): the project must be incomplete,
  not already running, its tier unlocked, its prerequisites complete, and its fee either paid or
  held in storage.
  The first start takes the fee; it is never charged again or refunded. The project becomes the
  active one, and any project that was running is paused with its progress kept.
- **Pausing** (`PauseResearch`) clears the active project. Resuming is starting again.
- **Progress:** each tick, after the buildings step, the active project gains `dt` times the
  research speed; at its full work it is complete and no project is active.
- **Research speed** (`ResearchSpeed`, from `SpeedOf`): the working labs (`WorkingLabs`: Research
  Labs switched on, online, and at a work speed above 0) are taken fastest first; the first adds
  its work speed, each one after adds its speed times half the weight of the one before, never
  under `LabFloor` (0.25). With every lab at full speed: one lab 1x, two 1.5x, three 1.75x, five
  2.25x. A lab in a power brownout at 80% counts 0.8 at its place. With no lab working, research
  waits.

### Effects

- **Transfer:** each tick, after research, every switched-on, online collector (`IsCollector`:
  extractors, the Quarry, the Deep Well and Deep Mine) moves the resources a complete project
  transfers (`Transfers(type)`) from its output buffer into storage, as far as room allows, at
  once. Other resources in the buffer, and offline collectors' buffers, stay for trucks and hand
  collection. Processors already deliver online and are not affected.

### The Research Lab

- See buildings.md: 4 x 5, 40 Stone, 30 Wood, 30 Iron, tier 1, draws 10 Power
  (`BuildingDef.Researches`). It reports "Idle: No research running" with no active project, and
  Working otherwise.

### UI

- **The research tree** (`ResearchTree`, a modal titled "Research"): T opens it (leaving build
  mode); T, Escape, the Close button, or the backdrop closes it. It fills the window below the top
  bar with a 24-unit margin of world showing round it (it overrides `Dialog.Place`). Its header
  reads "Speed 1.5x: 2 labs working", or "No labs working: research waits" in `Warning`. Below,
  the tree on the left (one canvas, scrolling both ways when it outgrows its area) and a 340-unit
  drawer on the right.
- **Sections:** the tree is one section per tier that has projects, side by side left to right,
  48 units apart, each as wide as its columns and all as tall as the tallest. A section is a faint
  panel (`Cell` at 35% opacity) headed in `SmallSize` `TextDim` caps, letter-spaced: "TIER 2", or
  while the tier is locked "TIER 3: UNLOCKED BY ELECTRONICS RESEARCH" (wrapping within the section).
  Once every project of the tier is complete, the `check` icon follows the heading, tinted `Good`.
- **The tree:** each project a 240 x 64 card placed in its tier's section by its column and row
  (80 units between columns, 24 between rows), with a line and arrowhead from each prerequisite's
  card to the project it opens, across sections too, in `Accent` once the prerequisite is complete
  and `PanelEdge` before. A card shows only
  the project's icon (`icons/<project>`, as `icons/material_transfer`) and name, and while the project
  runs, "Running" in `Accent`, an `Accent` outline, and a thin progress bar along its foot. A
  Locked card is drawn at half opacity; the selected card is lighter with a `Text` outline.
  Clicking a card selects it. The tree opens on the running project, else the first Available or
  Paused one, else the first, scrolled so its card is in view.
- **The drawer** shows the selected project: its icon and name, its state ("Available", "Running"
  in `Accent`, "Paused", "Locked", "Complete"); its description; "Transfers" and the icons of what
  it carries (each with its name as a tooltip); its prerequisites ("Needs Material Transfer
  first" in `Warning`, or "After Material Transfer"); "Cost to begin" and each fee resource's icon
  and amount (in `Short` when storage holds less; "Fee paid" once paid; "No fee"); "Time" and its
  work and time left ("2 days at 1 lab; 1 day 4 h left", or "waits for a lab" with no speed; the
  left part only once complete), as game days and hours of the 24-hour clock; a progress bar; and,
  at its foot, the full-width button: Start Research, Pause Research while running, Resume
  Research while paused, none once complete. The button is disabled when the project cannot
  start; hovering it names what is missing ("Needs Material Transfer first", "Needs 60 Steel
  (have 12)"), the project a start would pause ("Pauses Material Transfer"), or that the fee is
  paid once and not refunded.
- **The Research Lab's panel** adds a Research row ("Material Transfer, 45%, speed 1.5x", or "No
  research running") and a "Research tree [T]" button that opens the tree.

## Related

- buildings.md (the Research Lab and collectors' buffers), simulation.md (the tick order),
  presentation.md and ui-style.md (modals), art-style.md (the lab and icons), testing.md.
