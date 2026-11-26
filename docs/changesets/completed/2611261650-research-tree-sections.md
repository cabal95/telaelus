# Research Tree Sections

- Status: Implemented
- Id: 2611261650-research-tree-sections

## Summary

The research tree goes back to one screen. Its projects are grouped into a section per tier, side
by side from left to right, with prerequisite lines drawn across sections. This replaces the tree's
tier picker from 2611261445-tier-pickers. The build menu and inventory keep their pickers.

A section's heading names its tier. A locked tier's heading says which research unlocks it, and a
completed tier's heading shows the green check mark. This is phase 4 of the building levels plan
(future/building-levels.md), and it lands before the leveled buildings, which add prerequisites
across tiers ("Deep Mining", tier 3, needs "Powered Mining", tier 2).

## Motivation

With one tier on screen at a time, a project whose prerequisite sits in another tier can't show
that link. Today Electronics needs Steel Works, which shows only in the drawer. The building levels
will add many such links. Seeing the whole tree at once, sectioned by tier, keeps the tiers visible
and the prerequisites traceable.

## Design

- **Layout:**
  - The tree is one canvas, scrolling both ways as now, laid out as sections left to right: Tier 1,
    Tier 2, and so on.
  - Each section is as wide as its own columns of cards, with a 48-unit gap to the next.
  - Every section is as tall as the tallest.
  - Inside a section, cards are placed by their `Column` and `Row` within the tier, with today's
    gaps (80 between columns, 24 between rows).
- **Which tiers show:** only tiers that have projects. Today that is Tiers 1 to 3. Tier 4 appears
  once it has a project.
- **A section:**
  - **Panel:** a faint panel behind its cards, the `Cell` color at low opacity with the
    `CellRadius`.
  - **Heading:** at its top, at `SmallSize` in caps like a section heading ("TIER 2"), letter-spaced
    over a 1-unit `PanelEdge` rule (the section heading pattern, ui-style.md).
  - **A locked tier** (above the unlocked tier): the heading continues "TIER 3: UNLOCKED BY
    ELECTRONICS RESEARCH" in `TextDim`. Its cards are drawn at half opacity, as Locked cards
    already are.
  - **A completed tier** (every project complete): the heading has the `check` icon after it,
    tinted `Good`.
- **Prerequisite lines** run from each prerequisite's card to the project it opens, in any section,
  in `Accent` once the prerequisite is complete and `PanelEdge` before (as now). A line between
  sections crosses the gap between them. Electronics' line from Steel Works (Tier 1 to Tier 2) is
  the first.
- **Where it opens:** the tree selects the running project, else the first Available or Paused
  one, else the first, as now. It scrolls so that project's card is in view.
- **The drawer** is unchanged.

## Code changes

- `src/Telaelus/Presentation/Ui/ResearchTree.cs`:
  - **Removed:** the tier picker, `ShowTier`, `LayOutTier`, `Tier`, and `TierCells`.
  - **Layout:** each tier's offset and width from its projects' columns, card positions offset by
    their section, and the canvas sized to every section.
  - **Drawing:** `TreeCanvas` draws the section panels, headings, rules, and check marks, and every
    prerequisite line.
  - **Opening:** it scrolls to the selected card.
  - `Cards` drops `Shown`.
  - `Sections` (tier, heading text, locked, checked) is added for the snapshot.
- `src/Telaelus/Presentation/Ui/SegmentedPicker.cs`: remove `SetIcon` and the `icons` room in
  `SetOptions` and `CellsWidth`. The tree was their only user. Disabled cells stay, since the
  build menu uses them.
- `src/Telaelus/Testing/StateSnapshot.cs`: for the research tree, drop `tier`, `tiers`, and each
  card's `shown`, and add `sections` (each `tier`, `heading`, `locked`, `checked`).
- Scenario `research`:
  - its sections: Tier 1 to 3, with Tier 2 locked ("TIER 2: UNLOCKED BY STEEL WORKS RESEARCH")
    until Steel Works;
  - Tier 1 checked once Material Transfer and Steel Works are done;
  - selecting Advanced Material Transfer's card directly, now that it is always on screen;
  - a windowed screenshot of the whole tree.

## Blueprint updates

- research.md: the tree as sections (in place of the tier picker), its headings and check marks,
  lines across sections, and opening scrolled to the selected card.
- ui-style.md: the segmented picker's trailing mark goes, and the `check` icon's use moves to the
  research tree's section headings.
- testing.md: the research tree's snapshot fields and the `research` scenario.
- The icons README: the `check` icon is now used by the research tree's headings.

## Verification

- Sim and integration tests pass.
- Windowed: the tree shows Tiers 1 to 3 side by side. Steel Works' line crosses into Tier 2 to
  Electronics. Tier 2's heading names Steel Works until it completes, and Tier 1 shows the check
  once its projects are done.

## Notes

- **Room to grow:** with the building levels, each tier gains several projects. The sections then
  grow down (rows) more than across (columns), so the tree scrolls mostly vertically within a
  window's width of tiers.

## Implementation notes

- **Panels:** the section panels are drawn by the tree canvas under the lines, so lines between
  sections run over the panels and under the cards. Each heading is a label with the `check` icon
  beside it.
- **Heading width:** each heading is sized to its text, so the check mark follows the words. A long
  locked heading wraps at the section's width, which is why headings have 44 units of room (two
  lines).
- **Computers moved to row 1,** level with Electronics, so the line from Electronics into Tier 3
  runs straight instead of rising to row 0.
- **Opening:** scrolling to the selected card is deferred a frame, until the tree has laid out.
- **Removed:** the segmented picker's trailing icon (`SetIcon`, the icon room in `SetOptions`). The
  research tree was its only user.
