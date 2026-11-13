# Field Detail Panel

- Status: Implemented
- Id: 2611122136-field-detail-panel
- Depends-on: none

## Summary

Clicking a resource field opens a field detail panel, patterned on the building detail panel: the
field's resource name, a picture of a full field of that deposit, and two status rows (how much
is left, and what is working it). Fields get the same hover and open outline as
buildings.

## Motivation

A field's only feedback today is how many models it still shows (its depletion stage). The player
cannot tell how much is left or whether a building is already working it. The panel answers that with the patterns players already know from buildings.

## Design

### Selecting a field

- Outside build mode, the field under the pointer gets the thin accent outline, a frame around
  its 4 x 4 map cell. A left click opens its panel. (The outline was a filled plate under the
  footprint, which showed only as a rim around a building but as a filled square on a field,
  where little stands on the cell; it is now a frame of four thin bars for both.)
- While the panel is open, its field keeps the outline (as the building panel does).
- A click on the world closes a panel only when it selects nothing: empty ground now, since
  fields and buildings both select. Clicking a field while a building's panel is open switches
  to the field's panel, and the other way round (one panel at a time).
- E still harvests as before; clicking a field never starts or stops harvesting.

### The panel

A panel (ui-style.md), with the same layout as the building detail panel:

1. **Title**: the resource's name, as "Iron Ore".
2. **Picture**: a still, 180-unit picture of a full field (all five slots, as a field at 5/5
   shows them) of the field's deposit, drawn once at startup like the build menu's pictures, so
   the panel holds no live 3D view. Each deposit is drawn once, from one fixed variation (a fixed
   slot seed and starting amount): fields differ slightly, and the picture is a likeness, not a
   copy. Wood is drawn once per tree family (conifer, broadleaf, swamp), and the panel shows the
   family the field's biome uses, so a Forest field shows conifers; the biome only picks the
   picture and is never shown.
3. **Status rows**:
   - **Remaining**: "212 / 250", the amount left and the field's starting amount, with a
     progress bar of the fraction left below the rows.
   - **Worked by**: the placed, switched-on extractors working this field right now, as
     "2 Mines", or "nothing".

The UI shows no biome anywhere yet, and gather speeds stay out of the UI, so the panel shows
neither.
4. **Actions**: none yet besides Close.

The rows refresh every frame; the panel closes itself when the field depletes and is removed.

### Testing

- Snapshot: `hoveredField` and `focusedField` (field ids, like `hoveredBuilding` and
  `focusedBuilding`); while the field panel is open, `ui.open` is `"fieldDetail"`, `ui.field` its
  id, and `ui.rows` its rows (`remaining`, `workedBy`).
- A `field-detail` scenario: hovering outlines a field; clicking opens its panel with the right
  title and rows; a Mine placed beside it shows in Worked by;
  harvesting lowers Remaining; clicking a building switches panels; empty ground closes it; the
  panel closes when its field depletes.

## Code changes

- `Presentation/Ui/FieldDetail.cs` (new): the panel. `BuildingDetail` and `FieldDetail` share the
  status rows and progress bar through small helpers rather than a common base (they
  have little else in common).
- `Presentation/Ui/FieldPictures.cs` (new): draws each deposit's full field once at startup on
  the picture stage (loose framing), placing the five slot models with `NodeModels.Slot` for a
  fixed seed and amount, around the cell center; Wood once per tree family. Headless runs have no
  pictures, and the picture box keeps its size.
- `PictureStage`: a fill that stages several models (a field's slots) together.
- `NodeModels`: `TreeFamilyOf(biome)`, the one rule for which trees a biome's Wood fields draw,
  used by `Slot` and the pictures (the biome-keyed family table becomes a name-keyed one).
- `Presentation/Ui/StatusRows.cs` (new): the status rows and the progress bar, shared by both
  detail panels and the hint overlay.
- In the scenario, fields the panel's clicks aim at sit to the east of the player: a panel covers
  the left third of the screen, and clicks on it never reach the world.
- `BuildController`: the field under the pointer (`HoveredField`), its outline, and a
  `FieldClicked` event; `EmptyClicked` only for clicks that hit neither a building nor a field;
  a focused field's outline.
- `Simulation`: `ExtractorsWorking(field)`, the placed, switched-on extractors whose current
  field is this one. (Display only; no rule changes.)
- `GameRoot`: creates the panel and wires the clicks.
- Testing: the snapshot fields and the scenario.
- Run the headless import after adding the script, and commit its `.uid` file.

## Blueprint updates

- resources.md: selecting a field and its panel.
- presentation.md: the field detail panel in the screen UI.
- buildings.md: a click on a field no longer counts as "selects nothing".
- testing.md: the snapshot fields and the scenario.

## Verification

- `dotnet build game.slnx` and both test suites.
- The `field-detail` scenario windowed at 1280x720, reading the screenshots: the outline, the
  panel's field picture for an ore field and a Forest Wood field (conifers), and Remaining falling
  while harvesting.
