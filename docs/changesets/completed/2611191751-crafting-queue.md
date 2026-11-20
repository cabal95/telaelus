# Crafting Queue

- Status: Implemented
- Id: 2611191751-crafting-queue
- Depends-on: 2611191454-building-table

## Summary

Every processor (a building with recipes) crafts from a queue instead of running its recipe
forever. The player picks a recipe and a quantity in the building's panel and presses **Craft**,
which adds an entry to the end of the queue. The building works through the queue in order and
goes idle when it is empty. An idle building keeps reserving its Power, Water, and Sand, as a
switched-on building does today.

## Motivation

A processor runs its recipe as long as its inputs last. A Charcoal Kiln placed early eats every
unit of Wood in storage, starving construction and the Sawmill, and the only way to stop it is to
switch it off and on by hand. The same holds for a Smelter, a Sawmill, or a Fabricator whose input
something else needs. A queue makes the player say how much they want, so a building takes only
what that order needs and then stops.

## Design

### The queue

- A processor holds a **crafting queue** (`Building.Queue`): an ordered list of entries, each a
  recipe and a count of items still to make (`CraftOrder`: `RecipeIndex`, `Remaining`).
- **Crafting.** The building crafts the first entry's recipe. Each finished cycle takes that
  recipe's output amount off the entry's count; an entry at 0 leaves the queue and the next one
  starts. A building with an empty queue starts no cycle and is **Idle**, "Queue empty".
- **Strict order.** The first entry waits for its inputs ("Waiting for Wood") rather than letting
  a later entry go ahead (OQ3).
- **Adding.** `QueueCraft(id, recipeIndex, count)` appends an entry. With 5 of A and then 5 of B
  queued, queueing 5 more of A adds a third entry after B; the first entry's count does not grow.
  When the last entry is already that recipe, its count grows instead (OQ4).
- **Counts in items.** A count is a number of items and always a whole number of cycles: for a
  recipe that makes 2 Copper Wire a cycle, the quantity steps 2, 4, 6. The queue shows items ("10
  Copper Wire").
- **Removing.** `CancelCraft(id, entryIndex)` removes an entry. Removing the first entry while its
  cycle runs stops the cycle and returns the inputs it took (`CycleInputs`) to storage, or to the
  input buffer when offline, as destroying a building mid-cycle does (OQ5).
- **A cap.** A queue holds at most 4 entries; Craft is disabled when it is full.
- The recipe picker no longer switches what a queueing building runs: the running recipe is the
  first entry's, and a change of recipe happens only between entries, after the last cycle of one
  finishes. `SetRecipe` and `PendingRecipe` stay for the three that run without a queue.
- **Alternative inputs.** The input choice (Coal or Charcoal, or Auto) is kept per recipe
  (`InputChoices` keyed by recipe and input), so it survives the queue moving between recipes. The
  picker under the recipe picker shows and sets the choice for the selected recipe.
- **Stale inputs.** What goes back to storage when online is what no recipe in the queue can use,
  so hand-loaded inputs for a later entry stay in the buffer.

### Idle draw

- Capacity is already reserved by every switched-on consumer "working or not", so an idle
  processor keeps drawing its Power, Water, and Sand. Nothing changes there; the blueprint names it.
- A fuel burner (Smelter, Glassworks, Cement Kiln) takes no new fuel while idle, but the unit
  already burning burns out (`BurnLeft` counts down whether or not a cycle runs) and the rest of it
  is lost: a Coal that lasts 8 s, with the queue emptying after 5, burns its last 3 s for nothing.
  A queued cycle takes up whatever is left of the burning unit before taking a new one.

### Which buildings

- Every processor but three: the Smelter, Refinery, Glassworks, Charcoal Kiln, Steel Mill,
  Fuel Plant, Chemical Plant, Aluminum Smelter, Electric Furnace, Fabricator, Electronics Works,
  Tile Kiln, Assembly Plant, Shipyard, Sawmill, Cement Kiln, Concrete Mixer, Semiconductor Fab, and
  Enrichment Plant.
- The Glassworks makes from nothing but queues: it burns fuel, and its Glass fills the shared Cargo
  pool. The Electrolyzer, Brine Well, and Air Separator do not queue (Decision 9): they take
  nothing from storage, draw their Power and Water idle or not, and each output has its own storage
  limit, so they run their recipe until it is full, as now, with the Recipe picker switching it.
  Collectors, producers, power buildings, and the Rock Crusher do not change.

### The building panel

Under the status rows (the State and the Recipe row, now "Crafting: 2 Steel Beam of 6") and the
progress bar, in this order:

1. **The recipe picker**, shown only when the building has more than one recipe, then the input
   picker for an input with alternatives, as now.
2. **The quantity**: a spin box in items, stepping by the recipe's output amount, from one cycle's
   worth to 99 cycles' worth, starting at one cycle's worth (1 item, or 2 for a two-unit recipe).
3. **Craft**: adds the entry. Disabled when the queue is full.
4. **The queue**: one row per entry, "Charcoal x 5", the first marked as crafting, each with an X
   that removes it. "Queue empty" when there is none.

### New buildings, saves, and the demo

- A new processor starts with an empty queue and stays idle until the player queues something.
- An older save's processor loads with an empty queue; a cycle it was running finishes and
  delivers, then it idles (OQ7). Its `RecipeIndex` becomes the picker's selection.
- The demo town queues work on its processors (each Smelter 99 Iron, the last 99 Copper, and the
  Charcoal Kiln 99 Charcoal) and the demo save is rewritten.

### Scenarios and tests

- Scenario commands `queueCraft` (`x`, `z`, `recipe` by index or name, `count`) and `cancelCraft`
  (`x`, `z`, `entry`); `setRecipe` becomes the picker's selection only.
- The snapshot's building gains `queue` (a list of `{recipe, remaining}`); `ui` gains the panel's
  queue rows and the quantity.

## Code changes

- `src/Telaelus.Sim/Building.cs`: `Queue` (a list of `CraftOrder`); `RecipeIndex` becomes the
  running entry's recipe; `PendingRecipe` removed; `InputChoices` keyed by recipe and input.
- `src/Telaelus.Sim/CraftOrder.cs` (new): the entry.
- `src/Telaelus.Sim/SimulationRecipes.cs`: `QueueCraft`, `CancelCraft`; `SetRecipe` and the
  pending recipe removed; `ChosenInput` and `SetInputChoice` per recipe; stale inputs against the
  whole queue.
- `src/Telaelus.Sim/Simulation.cs`: `StepProcessor` starts a cycle only for the first entry and
  counts it down; an idle fuel burner burns out its current unit; `BuildingStatusOf` gives "Queue
  empty".
- `src/Telaelus.Sim/SaveData.cs`, `SimulationSave.cs`: the queue saved; `PendingRecipe` read and
  ignored from older saves; older input choices moved to the building's recipe.
- `src/Telaelus.Sim/DemoTown.cs`: queued work.
- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: the quantity, Craft, and the queue rows; the
  recipe picker selects what to queue.
- `src/Telaelus/Testing/ScenarioCommand.cs`, `CommandExecutor.cs`, `StateSnapshot.cs`,
  `scenario.schema.json`: the new commands and snapshot fields.
- Sim tests and scenarios that place processors (about 19 test files and 12 scenarios) queue
  work where they relied on a processor running forever. A test helper queues a large order.

## Blueprint updates

- `buildings.md`: "Recipes and refining" describes the queue, the counts in items, strict order,
  removing, the cap, and per-recipe input choices in place of `SetRecipe`; "Capacity" notes an idle
  processor still draws; "Switching off, status, and destroying" adds "Queue empty".
- `presentation.md`: the building panel's quantity, Craft button, and queue rows.
- `testing.md`: the new commands, snapshot fields, and tests.
- `save.md` (or wherever saves are described): the queue field.

## Verification

- `CraftingQueueTests`:
  - A Charcoal Kiln queued for 2 Charcoal takes 6 Wood, makes 2 Charcoal, then idles with the rest
    of the Wood in storage.
  - With A x 5 and B x 5 queued, queueing A x 5 makes a third entry; the building finishes A, then
    B, then A.
  - Queueing the same recipe as the last entry grows that entry.
  - A Copper Wire order counts by 2.
  - The first entry waits for its inputs while the second's are at hand.
  - Cancelling the running entry returns its cycle's inputs.
  - An idle Electric Furnace still reserves its 40 Power.
  - An idle Glassworks takes no new fuel, and the unit it was burning burns out.
  - The queue survives a save and load; an older save loads with an empty queue.
  - Per-recipe input choices survive the queue switching recipes.
- A scenario (`crafting-queue.json`): open a Smelter, pick Copper, set 5, press Craft, queue Iron,
  see both rows, remove one, and watch the Copper count down and the building idle when done, with
  screenshots of the panel.
- The existing sim tests and integration scenarios pass with queued work.

## Implementation notes

- **Which buildings queue** is derived, not listed: a processor queues when it burns fuel or any
  of its recipes takes an input (`Building.Queues`), which leaves exactly the Electrolyzer, Brine
  Well, and Air Separator running.
- **The quantity is a stepper, not a spin box:** "-" and "+" buttons (a cycle a press, ten with
  Shift) around the number. A text field would take the keyboard while focused, away from the
  game's hotkeys, and the buttons are what a scenario can press.
- **Removing an order** never merges its neighbours: removing Copper from Iron, Copper, Iron leaves
  two Iron orders.
- The panel's Recipe row and progress bar hide while the queue is empty, since nothing is crafting;
  the State row reads "Idle: Queue empty".
- Existing tests that need a processor working queue an order of 10000 (`SimTestHelpers.Order`);
  the five scenarios that place processors queue 99. The later-tiers scenario seeds a Silicon so
  its Semiconductor Fab's Silicon Wafer order finishes and the Processor order comes up.

## Decisions

1. **Makers from nothing:** the Glassworks, Electrolyzer, Brine Well, and Air Separator queue too.
2. **Fuel while idle:** no new fuel; the unit already burning burns out and its rest is lost.
3. **Strict order:** the first entry waits for its inputs and blocks the rest.
4. **Same recipe twice in a row:** grows the last entry.
5. **Cancelling the running entry:** stops at once and returns the cycle's inputs.
6. **Repeat:** no; counted orders only.
7. **Older saves:** processors load with an empty queue.
8. **Quantity and cap:** the quantity starts at one cycle's worth (1 item); a queue holds 4 entries.
9. **Which buildings:** the Electrolyzer, Brine Well, and Air Separator skip the queue after all
   (reversing 1 for them): each output has its own storage limit and shares no space, so running
   until full costs nothing. The Glassworks still queues.

## Open questions (answered)

1. **Makers from nothing:** queue the Glassworks, Electrolyzer, Brine Well, and Air Separator too
   (proposed: one rule for every processor, and the Glassworks burns fuel), or leave them running
   forever, since they take nothing from storage but the Glassworks' fuel?
2. **Fuel while idle:** an idle fuel burner burns nothing (proposed), or does "consume all the
   resources it needs to be active" include fuel, so a Smelter waiting with an empty queue keeps
   burning Coal?
3. **Strict order:** the first entry waits for its inputs and blocks the rest (proposed), or a
   later entry whose inputs are at hand goes ahead?
4. **Same recipe twice in a row:** queueing A when the last entry is already A grows that entry
   (proposed), or always adds a new one?
5. **Cancelling the running entry:** stop at once and return the cycle's inputs (proposed), or let
   the running cycle finish and remove the rest?
6. **A repeat option:** add a "Repeat" entry that runs until removed (useful for the Electrolyzer
   feeding a Fuel Plant), or only counted orders for now (proposed)?
7. **Older saves:** load processors with an empty queue (proposed), or give each a large order of
   its current recipe so a running factory keeps running?
8. **Quantity defaults:** a spin box starting at 5 items (rounded up to whole cycles),
   cap 8 entries (proposed), or other numbers?
