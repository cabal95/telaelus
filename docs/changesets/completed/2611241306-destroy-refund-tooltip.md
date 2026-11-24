# Destroy Refund in a Tooltip, and a Fixed-Width Panel

- Status: Implemented
- Id: 2611241306-destroy-refund-tooltip

## Summary

The detail panel's Destroy button reads just "Destroy", with the refund in its tooltip. Side panels
keep their set width whatever their content.

## Motivation

The button reads "Destroy (refund 13 Wood, 8 Stone)"; a building with a long refund makes the button
wider than the panel, and the panel grows to fit it. ui-style.md already sets a panel at a third of
the window (at least 280 units), but nothing stops content pushing past it: the body's scroll has
horizontal scrolling disabled, so its content's minimum width becomes the panel's.

## Design

- **Destroy button:** "Destroy", and its tooltip "Refund: 13 Wood, 8 Stone" ("Refund: nothing" when
  empty). The confirm step is unchanged: the first click reads "Confirm destroy" (its tooltip still
  the refund), the second destroys.
- **Panel width:** a side panel is exactly its set width. Content that cannot shrink is clipped
  rather than widening it: buttons and pickers in a panel cut long text with an ellipsis; status
  rows already wrap.

## Code changes

- `src/Telaelus/Presentation/Ui/BuildingDetail.cs`: Destroy reads "Destroy"; its refund
  (`DestroyTip`) shows through the host's hover tooltip (`ShowHover`), kept current while hovered;
  the Destroy button and the pickers trim long text with an ellipsis.
- `src/Telaelus/Presentation/Ui/Dialog.cs`: a panel's body scrolls horizontally `ShowNever` rather
  than `Disabled`, so the body's minimum width no longer sets the panel's.
- Tests: `building-detail` expects `ui.destroy` "Destroy", `ui.destroyTip`, the hover tooltip
  (with a "destroy-tip" screenshot), and `ui.width` 426.7 (a third of 1280); `storage` expects the
  new text and tip. The snapshot gains `ui.destroyTip` and `ui.width`.

## Blueprint updates

- presentation.md: the Destroy button text and tooltip.
- ui-style.md, Panel: content never widens a panel; long button text ends in an ellipsis.
- testing.md: the new snapshot fields.

## Verification

- Integration tests pass.
- Windowed screenshot of the Mine's panel with the refund tooltip beside the Destroy button, the
  panel at its width.

## Implementation notes

- The tooltip uses the game's own hover tooltip (as the build menu and research tree do) rather
  than Godot's `TooltipText`, so it matches the other tips and the snapshot's `ui.tooltip` sees it.
