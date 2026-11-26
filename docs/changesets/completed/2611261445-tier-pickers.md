# Tier Pickers

- Status: Implemented
- Id: 2611261445-tier-pickers

## Summary

The build menu, research tree, and inventory each show one tier at a time. The build menu and the
research tree get a segmented tier picker ("Tier 1" to "Tier 4"). A locked tier's cell is dimmed,
with a tooltip naming the research that unlocks it. In the research tree, a tier whose projects
are all complete shows a green check mark after its name. The inventory gets a new **arrow
picker** control: a left arrow, the current choice ("Resources", then "Tier 1" to "Tier 4"), and
a right arrow. Every resource row shows its name, grayed until discovered, and the nameless
"Undiscovered" rows are gone. This is phase 2 of the tier rework plan (future/tier-rework.md),
after 2611261421-tier-model.

## Motivation

With the tier model in place, the build menu lists 52 buildings, and 32 of them are locked
silhouettes at the start. The inventory lists 63 rows, and most of them are nameless. Showing one
tier at a time keeps each list short and tells the player what the next tier holds and how to
reach it. Naming resources that haven't been found yet ("Coal", grayed) gives the player
something to look for.

## Design

### Segmented picker additions (`SegmentedPicker`)

- **Disabled cells:** `SetEnabled(index, enabled, tooltip)`. A disabled cell draws its label in
  `TextDim` and ignores clicks and hover. Its tooltip, if any, still shows on hover (ui-style.md
  tooltips). In dropdown form, the item is disabled with the same tooltip.
- **A trailing icon:** `SetIcon(index, texture, color)` draws a small icon (14 px) after the label,
  inside the cell's padding. `CellsWidth` counts it, so the space rule ("segmented or dropdown")
  still holds.
- **The check icon:** a new `icons/check` model, a check mark in the light neutral that the UI
  tints, like the power switch. In the picker it is tinted `Good`.

### The arrow picker (new component, `ArrowPicker`)

- **Layout:** one row with a left arrow button, the current choice, and a right arrow button. The
  current choice is drawn like a segmented picker's chosen cell (`Accent` outline, label at
  `SmallSize`, 8 padding). Its width is fixed to the widest choice, so the arrows never move.
  Shorter choices are centered.
- **Behavior:**
  - The arrows step through the choices in order. They stop at the ends and are dimmed there;
    they don't wrap.
  - Choices can be hidden (`SetVisible(index, visible)`), and the arrows skip them.
  - `Chosen` is raised on every change.
- **Ids:** `<id>/prev`, `<id>/next`, and `<id>/current`, for scenarios.
- **UI patterns:** it joins the patterns in ui-style.md as the control for stepping through a
  short ordered list in little space, beside the segmented picker.

### Build menu

- **The tier picker** (`buildMenu/tier`: "Tier 1" to "Tier 4") sits at the top of the menu, and
  the "Infrastructure" and "Buildings" headings go.
- **Cells:** under the picker, only the chosen tier's cells, in one grid sorted by name (ordinal,
  as now), colored when affordable and grayed when not. Road and Bridge are tier 1 cells, sorted
  in with the buildings: Bridge first, Road between Research Lab and Sand Collector. They keep
  their own behavior: clicking one selects its line tool, and its hover shows the per-tile price.
  With locked tiers no longer shown, the "Undiscovered" cell state and its sorting go away.
- **Locked tiers:** a locked tier's cell is disabled, with the tooltip "Unlocked by Steel Works
  research" (or Electronics, or Computers).
- **Where it opens:** on the last tier viewed this session. The first time, it opens on the highest
  unlocked tier. A tier that unlocks while the menu is closed doesn't change the choice.
- **Persistence:** the remembered tier is presentation state. It isn't saved, and it resets with a
  new game or a load.

### Research tree

- **The tier picker** (`researchTree/tier`) sits at the top of the cards area. Only the chosen
  tier's projects are drawn, placed by `Column` and `Row`, which now count within the tier.

  | Tier | Projects (column, row) |
  |---|---|
  | 1 | Material Transfer (0, 0), Steel Works (0, 1) |
  | 2 | Advanced Material Transfer (0, 0), Electronics (0, 1) |
  | 3 | Computers (0, 0) |

- **Arrows** are drawn only between projects of the same tier. A prerequisite in another tier
  still shows in the drawer ("Needs Steel Works").
- **Locked tiers:** a locked tier's cell is disabled, with the same tooltip as in the build menu.
- **Check mark:** a tier whose projects are all complete shows the check icon after its name, in
  `Good`. A tier with no projects (tier 4 today) shows no check mark.
- **Where it opens:** on the tier of the project it selects. That is the running project, or the
  first project that can be worked on, as now.

### Inventory

- **The arrow picker** (`inventory/tier`: "Resources", "Tier 1" to "Tier 4") sits under the Cargo
  row. Only the chosen choice's rows are listed, and the tier headings and rules go.
- **Choices:**
  - "Resources" is the raw resources (tier 0).
  - Locked tiers are hidden from the picker, since their items can't be made yet.
- **Where it opens:** on the last choice viewed this session. The first time, it opens on
  "Resources". As with the build menu, the choice isn't saved and resets with a new game or a
  load.
- **Rows:**
  - Every row shows its icon and name.
  - An undiscovered row is grayed, with its icon darkened as now and its name in `TextDim`. Its
    tooltip reads "Not yet found" for a raw resource and "Not yet made" for an item.
  - Discovered rows still come first, then undiscovered ones, each in table order.
  - The Pin toggle shows only on discovered rows, as now.
- **Visibility rule:** items of a locked tier are never listed. Once their tier unlocks, they show
  grayed until discovered (first held), then as now. Raw resources are always listed.

### Unchanged

- Discovery toasts, the top bar and its pins, and the "Tier 2 buildings and research are now
  available" toast.

## Code changes

- `src/Telaelus/Presentation/Ui/SegmentedPicker.cs`: disabled cells with tooltips, a trailing
  icon, and `CellsWidth` counting icons.
- `src/Telaelus/Presentation/Ui/ArrowPicker.cs` (new): the arrow picker.
- `src/Telaelus/Presentation/Ui/BuildMenu.cs`:
  - the tier picker in place of the two headings;
  - Road and Bridge sorted into the tier 1 grid by name;
  - cells filtered by tier;
  - the remembered tier;
  - remove `BuildCellState.Undiscovered`.
- `src/Telaelus/Presentation/Ui/ResearchTree.cs`:
  - the tier picker with its check marks;
  - cards and arrows filtered by tier;
  - the size from the chosen tier's columns and rows.
- `src/Telaelus.Sim/ResearchDefs.cs`: `Column` and `Row` within each tier.
- `src/Telaelus/Presentation/Ui/InventoryPanel.cs`:
  - the arrow picker;
  - rows filtered by choice;
  - named, grayed undiscovered rows with tooltips;
  - the remembered choice;
  - no headings.
- `src/Telaelus/Presentation/Ui/UiIcons.cs`: the `check` icon.
- `src/asset-factory/tools/item_gen.py`: `icon_check`, exported to `assets/models/icons/check.glb`,
  with its kept preview and `icons.png` rerendered.
- `src/Telaelus/Testing/StateSnapshot.cs`:
  - `ui.tier` (the build menu's or tree's chosen tier);
  - `ui.tiers` (each picker cell's label, enabled, and checked);
  - `ui.inventoryChoice`;
  - `ui.inventory` rows keep their fields, with `name` added.
- The scenario runner: `clickUi` on an arrow picker's `prev` and `next`. `selectUi` and
  `clickUi` on a picker cell already exist.
- Scenarios:
  - `build-menu` updated: it opens on Tier 1, Tier 2 to 4 are disabled with their tooltips, and
    after Steel Works Tier 2 shows its buildings.
  - `research` updated: the tier picker, cards per tier, and the check mark on Tier 1 once
    Material Transfer and Steel Works are complete.
  - `inventory` updated: it opens on Resources with every raw row named and grayed until
    discovered, the arrows step to Tier 1 and stop there while Tier 2 is locked, and the choice is
    remembered on reopening.
  - `hud` and the menu-driven placement scenarios are adjusted for the new cells.
  - A new `arrow-picker` scenario: fixed width across choices, ends dimmed, hidden choices
    skipped.

## Blueprint updates

- ui-style.md:
  - the arrow picker pattern and component;
  - disabled segmented cells and the trailing icon;
  - the check icon in `Good`.
- presentation.md:
  - the build menu's tier picker and remembered tier;
  - the research tree by tier;
  - the inventory's arrow picker and named rows.
- research.md: `Column` and `Row` within a tier, and the tree drawn one tier at a time.
- testing.md: the snapshot fields, the scenario changes, and `arrow-picker`.
- The icons README: `check`.

## Verification

- Sim and integration tests pass.
- Windowed:
  - **New game:** the build menu shows Tier 1 with the other tiers dimmed and their tooltips, and
    the inventory opens on Resources with named gray rows.
  - **After Steel Works:** Tier 2 is open in both, and the research tree's Tier 1 cell has its
    check mark once Material Transfer is done too.

## Notes

- **Phase 3,** building quality levels, is its own design pass.
- **The arrow picker is meant to be reused.** Any short ordered choice in a narrow panel can use
  it.

## Implementation notes

- **Snapshot fields** as built:
  - `ui.tier` and `ui.tiers` (each `label`, `enabled`, `checked`) for the build menu and the
    research tree, with research cards gaining `shown`;
  - `ui.inventoryChoice`, `ui.inventoryArrows` (`prev`, `next`), and `ui.inventoryPicker`
    (`currentWidth`, `nextX`, which the `arrow-picker` scenario checks stay fixed);
  - inventory rows gaining `name`.
- **The research tree's `checked`** comes from the research state, not from the icon: headless
  runs draw no icons.
- **Disabled cells:** they draw a darker border (`SegmentedPicker.DisabledStyle`) as well as a
  `TextDim` label, since a dim label alone read too much like a cell that just isn't chosen. The
  arrow picker's disabled arrows use the same style.
- **Picking a research tier** selects that tier's first project, so the drawer always matches the
  tree.
- **Building silhouettes are gone.** With no "Undiscovered" cells left, nothing showed them, so
  `BuildingPictures` renders one picture per building instead of two and `PictureStage.Fill`
  lost its silhouette option.
- **Scenario timing:** scenarios that switch the build menu's tier wait two frames before clicking
  a cell, because the grid lays out the frame after the switch.
