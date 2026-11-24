# Show What a Recipe Needs Before Queuing It

- Status: Implemented
- Id: 2611241315-recipe-requirements-line

## Summary

Under a processor's recipe pickers, a dim line spells out the chosen recipe: its inputs, output, and
cycle time, with any input storage has none of tinted in the danger color.

## Motivation

The order picker on a queueing processor names only the output ("Steel"); the player learns what it
needs only after queuing it (the Recipe row then shows it) or from the building's tooltip in the
build menu. The same holds for the Recipe picker on the three buildings that do not queue.

## Design

- **The line:** right under the picker, updated as the choice changes:
  "2 Iron + 1 Coal or Charcoal + 1 Limestone to 1 Steel, 20 s", the format the Recipe row uses.
- **Shortfall:** an input storage holds less than one cycle's worth of (nothing included) is drawn
  in `UiTheme.Danger`. It reads storage whether or not the building is online. Alternatives ("Coal or Charcoal") are short only when every
  alternative is.
- **Where:** under the Recipe picker, which is the order picker on a queueing processor and picks
  the running recipe on the Electrolyzer, Brine Well, and Air Separator; shown whenever the picker
  is (a building with more than one recipe).

## Code changes

- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: a `RichTextLabel` under the Recipe picker,
  refreshed with the panel (`NeedsText`, in the Recipe row's wording, with BBCode color on short
  inputs); `RecipeNeeds` for the snapshot.
- Tests: `crafting-queue` checks the line for the Smelter's Iron ("1 Iron Ore to 1 Iron, 16 s",
  nothing short) and, once storage's last Iron Ore is taken away, Iron Ore short; the snapshot gains
  `ui.recipeNeeds` (`text` and `short`).

## Blueprint updates

- presentation.md: the line under the pickers.
- testing.md: `ui.recipeNeeds`.

## Verification

- Integration tests pass.
- Windowed screenshot of the Smelter's panel with Iron Ore short (the `crafting-queue` scenario's
  "queue-done" frame).

## Implementation notes

- Scenarios have no command to pick a picker item, so the line's change on a new choice is not
  checked; it is the same refresh that follows the picker (`PickedRecipe`).
- The arrow in the draft became "to", matching the Recipe row.
