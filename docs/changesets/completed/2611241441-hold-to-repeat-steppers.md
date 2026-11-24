# Hold to Repeat on Up/Down Buttons

- Status: Implemented
- Id: 2611241441-hold-to-repeat-steppers

## Summary

Holding an up/down button keeps stepping its value: one step on press, then after 0.5 s about 15
steps a second until release. A quick click still steps exactly once. It is a shared button kind,
so every future up/down control gets it; today that is the crafting quantity's "-" and "+".

## Motivation

The crafting quantity runs from one cycle to 99, and reaching 40 takes 39 clicks. Holding the
button should get there.

## Design

- **`StepButton`** (presentation, `Ui/StepButton.cs`): a `Button` that raises `Step` instead of
  `Pressed`.
  - **Press:** steps once at once, when the mouse button goes down (not on release, as other
    buttons act), so a click reads the same as before and a hold starts from a visible change.
  - **Hold:** after `RepeatDelay` (0.5 s) still held, steps every `1 / RepeatRate` s (15 a second)
    until released. The timing is real time, so it runs while the game is paused.
  - **Release** within 0.5 s: no further step, so a click is exactly one step.
  - **Stops** when released, when the pointer leaves the button (a drag off it cancels the
    repeat), or when the button turns disabled (the value reached its limit). It never steps past
    a limit, since a disabled button does not step.
- **Shift goes:** the quantity's Shift-click step of ten cycles is removed (holding replaces it).
- **The crafting quantity's "-" and "+"** (`buildingDetail/less`, `buildingDetail/more`) become
  `StepButton`s, each step one cycle. Their tooltips drop the Shift note and gain the hold:
  "One cycle more. Hold to repeat." and "One cycle fewer. Hold to repeat."
- **Pace check:** from one cycle, holding "+" for 3 s reaches about 1 + 2.5 x 15 = 38 cycles, and
  the 99 cap in about 7 s.
- **Scenario input:** a new `holdUi` `{ id, frames }` step presses the mouse over a registered
  control, waits the frames, and releases, the way `clickUi` clicks it, so a test can hold one.

## Code changes

- `src/Telaelus/Presentation/Ui/StepButton.cs` (new, with its `.uid` from the headless import):
  the press, delay, and repeat, driven by `_Process` with real-time delta.
- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: `_less` and `_more` as `StepButton`s, wired to
  `StepQuantity` through `Step`, with no Shift step.
- `src/Telaelus/Testing/` (the scenario runner) and `scenario.schema.json`: `holdUi`.
- `src/Telaelus.Integration.Tests/Scenarios/crafting-queue.json`: a click on "+" still steps one
  cycle; holding "+" for 45 frames (1.5 s) from one cycle lands on 17 (one on press, then 1 s of
  repeats); holding "-" back down stops at one cycle.

## Blueprint updates

- ui-style.md, Components: step buttons (press steps at once, 0.5 s delay, 15 a second, stops on
  release, leaving, or a limit).
- presentation.md: the quantity's "-" and "+" step one cycle and repeat when held (no Shift).
- testing.md: `holdUi`, and the `crafting-queue` scenario's new steps.

## Verification

- Sim tests (unchanged) and integration tests pass.
- In game: holding "+" on a Smelter's quantity climbs steadily after a short pause, and a click
  still adds one.

## Implementation notes

- The hold lands on exactly 17 at the tests' fixed 30 frames a second, so the scenario checks 17.
- `holdUi` fails, rather than scrolls, when a scroll container clips the control (a hold that
  waited a frame for the scroll would shift its timing); the quantity buttons are always in view.
- The snapshot has no field for a button's disabled state, so "-" disabled at one cycle is checked
  only through the quantity staying at 1.
