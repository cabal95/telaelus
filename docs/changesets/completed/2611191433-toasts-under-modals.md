# Toasts Wait Behind Modals

- Status: Implemented
- Id: 2611191433-toasts-under-modals
- Depends-on: 2611191244-building-descriptions

## Summary

While a modal is open (the build menu, the research tree, the game menu, the save and load
dialogs, a question), the notification stack hides and its toasts stop counting down; when the
modal closes, the stack fades back in where it was.

## Motivation

The toasts already draw behind every modal: the backdrop dims them like the top bar (a toast's
text measures 153 against the modal title's 235, the backdrop's 35%), and the modal draws over
them where they meet. But at 1280 x 720 the stack's left edge sits under the build menu's right
edge, so a toast shows half covered beside the menu, its icon cut off, and reads as a toast drawn
over the modal. Two more problems come with it:

- The build menu and the research tree do not pause the game, so a toast keeps counting down
  behind them and can expire unread while the player picks a building.
- The trainer's step card sits in the same stack, half covered the same way.

## Design

- **Hidden while a modal is open.** The stack (every toast and the trainer's step card) fades out
  over 0.15 s when a modal opens and fades back in when the last modal closes. A panel (left
  docked) changes nothing.
- **Timers hold** while hidden, as they do under the game menu today, for every modal whether or
  not it pauses the game. A toast shows its full remaining time once the modal closes.
- **New toasts** raised while a modal is open (an unlock from storage filling while the build menu
  is up) join the stack hidden and show when it closes, newest on top as usual.
- **The trainer** keeps checking its step behind the modal (step 7, "Press [B] and choose the
  Lumber Camp", completes while the menu is open), so its card may read Done when the menu closes.

## Code changes

- `src/Telaelus/Presentation/Ui/Notifications.cs`: `Covered` (set each frame), the stack's fade
  (the overlay's own opacity, hidden at 0 so it takes no mouse), and the timers and slide-ins
  holding while covered; `StackShown`.
- `src/Telaelus/Presentation/GameRoot.cs`: sets `Covered` from `DialogHost.CapturesWorldInput` (a
  modal is open).
- Tests: the `notifications` scenario opens the build menu with a toast up (it hides, its
  `remaining` holds, and it shows again on Escape); a toast raised under the menu shows after it.
  The snapshot's `ui.toasts` gains `shown` (false while covered).

## Blueprint updates

- ui-style.md: the notifications under a modal (hidden, holding).
- presentation.md: `Covered`.
- testing.md: `ui.toasts[*].shown` and the scenario's new steps.

## Verification

- Sim and integration tests pass.
- Windowed screenshots of the build menu with a toast raised just before (none showing beside the
  menu) and just after closing it (the toast back, with its time).

## Implementation notes

- The whole overlay fades (its `Modulate`), so the trainer's step card goes with the toasts; at 0
  it is hidden, so nothing under a modal takes the mouse.
- The fade runs on real time, so it still plays while the game menu stands the game still.
- A build of the sim library left a corrupt reference assembly once (`CS0009`); clearing
  `obj/Debug/net8.0/ref` fixed it. Not a code problem.

## Decisions

1. **Hide or dim:** hide the stack under a modal.
2. **The trainer's step card:** hidden with the stack.
3. **Fade:** 0.15 s out and in.

## Open questions (answered)

1. **Hide, or keep dimmed:** hide the stack under a modal (proposed), or leave it dimmed as now
   and only hold the timers?
2. **The trainer's step card:** hide it with the stack (proposed), or keep it showing above the
   modal so the instruction stays in view while the player picks?
3. **Fade:** 0.15 s out and in (proposed), or show and hide at once?
