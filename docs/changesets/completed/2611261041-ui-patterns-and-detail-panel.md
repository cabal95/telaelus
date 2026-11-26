# UI Patterns, and a Cleaner Building Detail Panel

- Status: Implemented
- Id: 2611261041-ui-patterns-and-detail-panel

## Summary

Two parts, landing together. First, a set of UI patterns for the whole game, saying which control
or display to use for which job (a status strip, text rows, a segmented picker, a dropdown, an
icon toggle, step buttons, buttons, notes, section headings), so new UI picks from a short list
instead of inventing. Second, the building detail panel rebuilt with those patterns: its
glanceable states move into an icon strip beside the picture, its controls are grouped under
section headings, and its buttons size to their labels.

## Motivation

The detail panel has grown to 39 possible elements, almost all as label-and-value text rows or
dropdowns. States the player checks at a glance (online, on, power) read as words they have to
parse, two-way choices (Queue or Hold) hide behind dropdowns, and on a processor the controls fall
below the fold. The coming tier pickers (build menu, inventory) need a tab-like control too, so
this is the moment to settle the patterns once.

## Part 1: UI patterns (for ui-style.md)

Each pattern says what it is for, then how it looks.

1. **Status strip** (new): small icons, each with a short value beside it (a number, a percent,
   a time) or none. For **states and quantities the player checks at a glance**: on or off,
   online or not, a capacity drawn or supplied, a charge, a fuel burning, a truck.
   - Every icon has a tooltip with the full sentence the text row says today ("Draws 10 Power,
     running at 80%").
   - Values are signed where they flow: "-10" for a draw, "+35" for a supply.
   - Color carries the state: `Text` normal, `TextDim` for off or "not applicable now",
     `Warning` for degraded or cut off (a brownout, offline, no truck can reach), `Danger` for
     broken (a blackout). The value takes the same color. A new `Good` token (green, the hubs'
     glowing green, 0.30, 0.95, 0.70) marks on and online: the on/off toggle is `Good` on and
     `TextDim` off (off is the player's choice, not a problem), and Network is `Good` online and
     `Warning` offline.
   - Icons are 20 units, values `SmallSize`, a `Gap` between entries, like the top bar, which
     already works this way.
   - **Rows and columns are both supported.** A row wraps to a further line when it runs out of
     width; a column wraps into a further column beside it when it runs out of height (a given
     maximum, such as the height of the picture beside it). The pattern is the
     icon-value-tooltip entry; which way it runs is the screen's choice: the top bar is a row, the
     building detail panel a column beside its picture.
2. **Text rows** (the current status rows): for **values that need words**: lists ("Iron Ore 2
   (500), Stone 1 (250)"), sentences, or names ("Growing: 2:10 left", "Laterite motherlode").
   Keep a row only when an icon plus a number cannot say it.
3. **Segmented picker** (new; the "multi-element selection"): a row of flat cells, no
   background, each with a 2 unit border: `TextDim`-gray when not chosen, `Accent` when chosen,
   lightening on hover. Exactly one is chosen. Labels are `SmallSize` (14, like status rows; the
   padding and border make it read larger), with 8 units of side padding, cells 32 units tall
   and 4 apart. For **mutually exclusive options with short labels (or icons), where seeing all
   of them at once helps**: Queue / Hold, Best / Coal / Charcoal / Wood, Auto / Coal / Charcoal,
   and the tier pickers ("Tier 1" ... "Tier 4") in the build menu and inventory, where it works
   as tabs.
4. **Dropdown** (`OptionButton`, as now): for **choices whose options do not fit on one row**, and
   for **recipes**, which stay dropdowns on every building for consistency (a Smelter's two as an
   Assembly Plant's seven) until a better recipe control comes along.
   - **Segmented or dropdown: space decides.** The hard rule is that a segmented picker's cells
     fit the room it has on one row, each label whole (no ellipsis, no wrapping). Option counts
     are only a guide. Measured at 14 with 8 units of padding (the Panel Strip Lab samples,
     https://claude.ai/artifact/16X7rvA2RJXvQXjF1kzUVY): "Tier x" fits 6 cells in a side panel
     at 16:9 (394.7 units inside), 4 at 4:3 (288) and in the narrowest (248), and about 19 in the
     build menu; Fuel's four options take about 252 units, fitting every side panel but the
     narrowest. So the build menu's tier picker can grow past four tiers, while the inventory
     panel's may need another control (a dropdown, or arrows that page through tiers) if tiers
     are added.
   - A control chooses one way when it is built for a building or screen, not as the window
     resizes, so it does not change form under the player.
5. **Icon toggle** (new): a single icon button with two looks, for **a binary that has a natural
   icon and acts on the thing itself**: the building's on/off as a power symbol (a circle broken
   at the top by a line, what players know), `Good` green when on, `TextDim` when off, with a
   tooltip ("On: click to turn off"). A two-word binary like Queue / Hold uses a segmented
   picker instead.
6. **Step buttons** (`StepButton`, as now): for **a number the player sets**, between "-" and "+",
   the value in the middle.
7. **Buttons:** for **actions (verbs)** that do something once: Craft, Buy truck, Launch ship,
   Destroy. A button never shows a state; a label that changes with state ("Turn off" /
   "Turn on") is a sign it should be a toggle.
   - **Sized to their label,** not stretched to the panel: the label plus the theme's 16 units of
     side padding, never narrower than 96 units so a short label ("Craft") still makes a fair
     target. In a narrow side panel a button sits centered; in a wider panel or a modal it is
     placed where the layout reads best (left with the content it acts on, or right where a
     dialog's actions gather).
   - A **stack of buttons** (the game menu's list) shares one width, twice its widest button's, so the
     stack lines up; a button **in a row with other controls** (Craft after the quantity, Buy and
     Sell truck side by side) takes its label's width in that row.
   - Small icon-like buttons (the queue's X, "-" and "+") keep their square size.
8. **Notes** (dim `SmallSize` lines): **under a control**, explaining what it does now ("Best:
   burns Coal (40 s per unit)"), or why something cannot happen. Never on their own.
9. **Section headings** (new for panels): a `SmallSize` `TextDim` caps label (letter-spaced) over
   a 1 unit `PanelEdge` rule, to group a panel's controls once it has more than one kind of
   control (Production, Fuel, Trucks).

## Part 2: the building detail panel

Top to bottom. Every element in today's panel is accounted for.

1. **Title** (as now).
2. **Top block:** the turntable picture at 180 units (as now; its size gets a final look on a
   real display once the rest is in), with the **status strip** (pattern 1) as a column to its
   right, wrapping into a second column beside the first when it is taller than the picture.
   Entries in this order, each shown only when it applies:
   - **On/off** icon toggle (pattern 5); replaces the Switch row and the Turn off / Turn on button.
     Not on storage buildings, as now.
   - **Network:** a hub mast icon; `Good` online, `Warning` offline. Replaces the Network row.
   - **Power, Water, Sand:** the capacity icon (the bolt, the drop, the sand heap) with "-10" for
     a draw or "+35" for a supply, colored by state; the tooltip has today's sentence. Replaces
     the three rows.
   - **Charge** (Battery Bank): the battery icon and "53%"; tooltip "3200 / 6000, charging".
   - **Fuel burning:** the burning fuel's item icon and "22 s"; tooltip "Coal, 22 s left, +30%
     speed". Replaces the Burning row.
   - **Truck** (extractors): the truck icon while one is on its way or loading, `Warning` when no
     truck can reach; tooltip says which. Replaces the Truck row.
3. **State line:** "Working" or "Idle: Holding: 50 Iron" as one `BodySize` line (no label), in
   `Text` working, `TextDim` idle, `Warning` for a problem (no power, offline, storage full).
   Replaces the State row.
4. **Text rows** that stay (pattern 2): Research, Site, Plot, Eats, In range, Working on, Recipe,
   Buffer, Links, Reach.
5. **Progress bar** (as now; the "Progress" label goes, since the bar under the Recipe row is
   self-explanatory).
6. **Sections** (pattern 9), each only when it has something:
   - **Launch** (Shipyard): as now, its button sized to its label.
   - **Production** (processors): Recipe (a dropdown, pattern 4) and its needs line; Mode as a
     segmented Queue / Hold; Input as a segmented Auto / Coal / Charcoal with its note; then the
     order row (Craft at its label's width) and queue, or the Hold at row and storage line.
   - **Output** (collectors with no field): a dropdown (its labels carry rates, "Iron Ore, every
     5 s", too long for cells).
   - **Fuel:** a segmented Best / Coal / Charcoal / Wood (or Best / Fuel Rod) with its note; a
     dropdown where the cells do not fit (the narrowest panel).
   - **Storage** (storage buildings): the Silo's Holds picker (a dropdown) and Adds to storage.
   - **Trucks** (Depot): a row of truck icons, one per bay. A bought truck's icon is colored by
     its state (`Text` driving, loading, or unloading; `TextDim` parked; `Warning` waiting or
     blocked) with its state in the tooltip ("Truck 2: To HQ: 20 Iron Ore"); an empty bay is a
     dim outline ("Empty bay"). It replaces the Trucks and Truck 1 to 4 rows. Then Buy truck and
     Sell truck side by side at their labels' widths, with the buy note under them.
   - **Research** (Research Lab): the Open research button, sized to its label.
7. **Destroy** alone at the bottom, sized to its label and centered (it is full width today).

## Code changes

- New controls in `src/Telaelus/Presentation/Ui/`: `StatusStrip` (entries of icon, value, color,
  and tooltip; row or column, with a wrap length), `SegmentedPicker`, `IconToggle`, and a
  `SectionHeading` helper; `UiTheme.Good`; button sizing in the theme (minimum width 96, no
  horizontal fill) with each panel placing its buttons.
- New icon models in `item_gen.py`'s icons: `icons/power_switch` (the power symbol) and
  `icons/network` (a hub mast); the truck icon drawn from `units/truck` (`UiIcons.Pictures`, as
  the road and bridge icons are), the battery, water, sand, and fuel icons from their items, and
  the bolt from `icons/power`.
- `BuildingDetail.cs`: rebuilt around them.
- `StateSnapshot.cs`: `ui.strip` (each entry's id, value, and state), `ui.trucks` (each bay's
  state), segmented pickers' choices (`ui.mode`, `ui.fuel`, `ui.input` keep their meaning);
  scenarios that read `ui.rows.network`, `ui.rows.switch`, `ui.rows.power`, `ui.rows.burning`,
  `ui.rows.trucks`, and click `buildingDetail/toggle` move to the new ids. `selectUi` learns to
  pick a segmented picker's cell.
- Other screens with full-width buttons (the game menu, save and load, the choice dialog) follow
  the button pattern where it changes their look; the build menu and inventory tier pickers come
  with the tier rework, not here.

## Blueprint updates

- ui-style.md: the patterns (Part 1), the new components, and `Good`.
- presentation.md: the building detail panel (and the stale "Power (On or Off)" sentence fixed).
- art-style.md: the new icons.
- testing.md: the snapshot fields and scenario changes.

## Verification

- Sim tests (unchanged) and integration tests pass.
- Windowed screenshots of the panel for a Smelter, a Refinery in a brownout, a Depot, a Battery
  Bank, a hub, a Silo, and a Shipyard, before and after, then a final look on a real display
  (the picture size among it).

## Notes

- Decided with the Panel Strip Lab samples (https://claude.ai/artifact/16X7rvA2RJXvQXjF1kzUVY):
  the strip as a column beside the picture, wrapping; segmented cells at 14 with 8 units of
  padding.

## Implementation notes

- Shapes: `StatusEntry` (id, icon, value, `Tone`, tip) with `Tone` colored through one table,
  `UiTheme.ColorOf`; `StatusStrip` (a `FlowContainer`, row or column, reusing its nodes);
  `SegmentedPicker` (cells registered as `<id>/<label>`, its own dropdown fallback, the form
  chosen once per building); `IconToggle`; `SectionHeading`; `UiTheme.SizeToLabel` for buttons.
- Buy truck and Sell truck read just that: with their costs in the label they never fit side by
  side. The cost and refund moved to their tooltips; the note under them shows the cost dimmed
  when nothing is short.
- The small "Mode" and input labels went (the cells explain themselves); Recipe, Holds, and Queue
  keep theirs. A building with no fuel burning shows no fuel entry; the Fuel note says so.
- The state line's tone comes from a presentation-side list of calm idle reasons (Switched off,
  Queue empty, Holding, No research running, Night); every other reason is `Warning`. The sim
  exposes no reason kind, so this matches reason text.
- Fuel's four cells measure 236 units, under the narrowest panel's 248, so its dropdown fallback
  is built but no building reaches it today.
- Depot bays use 32-unit icons (20 was unreadable).
- The game menu's buttons share one width, twice its widest button's (240 units; one label wide read too narrow); the choice dialog's answers and the
  save and load buttons size to their labels, at least 96.
- Two new icons, `icons/power_switch` and `icons/network`, in a light neutral (`icon_mono`) so
  the UI's tint shows true; the truck icon is drawn from `units/truck`.
- Seen, not changed: a disabled button has no background (the theme has no disabled style), and
  strip entries other than the toggle are not registered for `hoverUi`.
