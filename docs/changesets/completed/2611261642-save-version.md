# Save Version

- Status: Implemented
- Id: 2611261642-save-version

## Summary

Every save file carries a **save version**, starting at 1 with this change. A save without one is
version 0. A save older than the current version is listed but cannot be loaded ("Saved by an
older version"), and the load screen offers to delete such saves. This replaces today's
`FormatVersion`, so every save made before this change becomes version 0 and stops loading. It
prepares for the building levels (future/building-levels.md), which change saved buildings too
much to carry old saves over.

## Motivation

The building levels merge and resize buildings, and we've decided not to migrate old saves. The
game needs a clear way to recognise those saves, say why they won't load, and let the player clear
them out, rather than loading them wrong or failing with "The save is damaged".

## Design

### The version

- **Where it lives:** `SaveHeader.SaveVersion` (int) replaces `SaveHeader.FormatVersion`, and
  `SaveData.FormatVersion` is removed, so a save has one version, in its header.
  - **Missing means 0:** when the field is missing, it reads as 0. Every existing save has
    `FormatVersion` and no `SaveVersion`, so each reads as version 0. Their `FormatVersion` is
    ignored on read.
  - **Current version:** `SaveFormat.Version` is 1, and every save written from now on carries it.
- **Reading** (`SaveFormat.Read`) refuses three kinds of save, each a `SaveFormatException` with a
  `Kind`:
  - a file that doesn't read: `Damaged`, "The save is damaged" (as now);
  - a version above the current one: `Newer`, "Saved by a newer version" (as now);
  - a version below it: `Older`, "Saved by an older version" (new).
- **The rule from now on:** a change to the saved state that older saves can't be read into raises
  `SaveFormat.Version`, which makes the older saves `Older`. A change that only adds a field with
  a safe default keeps the version, as hold mode did. Migrations are no longer written.

### Unsupported saves on the load screen

- **The list** (`SaveList`, used by the main menu's Load Game and the in-game Load Game dialog)
  shows an `Older` save as it shows a damaged or newer one today: its name and time, with the
  reason in `Warning`, and Load disabled. `SaveEntry` gains `Outdated` (its problem is `Older`).
- **The prompt:** when the list is shown and holds any outdated saves, the list's confirmation row
  (the one Delete uses) asks:
  - "2 saves are from an older version and cannot be loaded. Delete them?"
  - **Delete** deletes them and their thumbnails (`SaveStore.Delete`), autosaves included, and
    refreshes the list.
  - **Keep** dismisses the prompt until the load screen is next opened.
  - It is a prompt in the list, not a separate dialog: the main menu has no dialog host, and the
    same row already confirms a single Delete in both places.
- **Continue** already loads the newest *loadable* save, so an outdated save never becomes
  Continue's target. With no loadable save, Continue stays hidden, as now.
- **Load Game** on the main menu stays enabled while any save exists (loadable or not), so the
  player can reach the prompt.

### The demo save

`assets/saves/demo.save` is rewritten at version 1 (`--write-demo-save`).

## Code changes

- `src/Telaelus.Sim/SaveFormat.cs`:
  - `SaveHeader.SaveVersion` in place of `FormatVersion`;
  - `SaveFormat.Version` 1;
  - `Read`'s three kinds;
  - `SaveFormatException.Kind` (`Damaged`, `Newer`, `Older`).
- `src/Telaelus.Sim/SaveData.cs`: remove `FormatVersion`.
- Where saves are written (`GameRoot`'s save, `DemoSaveWriter`): set the header's `SaveVersion`
  to `SaveFormat.Version`.
- `src/Telaelus/Presentation/SaveStore.cs`: `SaveEntry.Outdated`.
- `src/Telaelus/Presentation/Ui/SaveList.cs`: the outdated-saves prompt (Delete, Keep) on showing
  the list.
- `src/Telaelus/assets/saves/demo.save` and `demo.png`: rewritten.
- `src/Telaelus/Testing/StateSnapshot.cs`:
  - each listed save gains `outdated`;
  - the list's prompt text, or null, as `savesPrompt`.
- The scenario runner: `saveGame` gains an optional `version`, which writes the save with that
  version (0 leaves the field out) so a scenario can plant an old save.
- Sim tests (`SaveTests`):
  - a written save carries version 1 and loads;
  - a save with no `SaveVersion` (today's files, with `FormatVersion` 1) is refused as `Older`
    with "Saved by an older version";
  - version 2 is refused as `Newer`;
  - a damaged file is `Damaged`.
- Scenarios:
  - `save-load` updated for the version;
  - a new `save-version` scenario:
    - it plants two version 0 saves and one current save;
    - the load list shows the old ones as "Saved by an older version" with Load disabled;
    - the prompt counts two;
    - Keep hides the prompt until the list is reopened;
    - Delete leaves only the current save;
    - Continue still loads the current save.

## Blueprint updates

- saving.md:
  - the save version in place of the format version;
  - the three refusals;
  - the rule (no more migrations);
  - outdated saves, the delete prompt, and the demo save at version 1.
- presentation.md: the save list's outdated rows and prompt.
- testing.md: the snapshot fields, `saveGame`'s `version`, the new tests, and the `save-version`
  scenario.

## Verification

- Sim and integration tests pass.
- Windowed: with a save from before this change in the saves folder, the main menu's Load Game
  shows it as "Saved by an older version" and offers to delete it. Delete removes it. A new save
  loads.

## Notes

- **Every save made before this change stops loading,** including the operator's own. That is
  the intent, ahead of the building levels.
- **The version is in the header,** which the load list already reads, so outdated saves are
  found without loading the game.

## Implementation notes

- `SaveHeader.SaveVersion` has no default, so a file without the field reads as 0. The game
  (`GameRoot.MakeSave`, through `AppRoot.SaveGame`) and the demo writer set it.
- `saveGame`'s `version` writes the field with the given number (0 included), which reads the same
  as a file without it.
- The save list's confirmation row now holds the action it confirms, so the single Delete and the
  outdated-saves Delete share it. Its question wraps, since the outdated one is long.
  `SaveList.Open` (used by both load screens) lists the saves and asks. `Refresh` (after a delete)
  doesn't ask.
- Scenarios can't drive the main menu's buttons, so `save-version` checks the prompt through the
  in-game Load Game dialog, which uses the same list.
- A sim test builds a file as saves were written before this change (a `FormatVersion` of 1 in the
  header and sim data, no `SaveVersion`) and checks it is refused as `Older`.
