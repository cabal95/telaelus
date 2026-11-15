# Saving and Loading

- Status: Implemented
- Id: 2611151454-save-and-load
- Depends-on: 2611151428-loading-and-menu

## Summary

A game can be saved and loaded. The sim writes its whole state to a versioned save and reads one
back, so a loaded game continues exactly as the saved one would have. Saves live in the player's data
folder as named slots plus a rotating autosave. The main menu gains **Load Game**,
and an in-game menu (Escape) gives **Resume**, **Save Game**, **Load Game**, **Main Menu**, and
**Quit**, which is also the first way back to the main menu.

## Motivation

Everything a game builds now lives in the sim: research progress, the clock, Battery charge, roads
and bridges, trucks mid-trip, buildings mid-cycle. None of it survives closing the window. The
loading screen and menu (2611151428-loading-and-menu) left a place for Load Game. Co-op later needs
the same thing in another form (the whole state to send a joining peer), so the save is built as a
sim feature, not a presentation one.

## Design

### What a save holds

- **The sim's whole state** (`Simulation.Save()` to a `SaveData`, `Simulation.Load(SaveData)`):
  - the world settings (kind, seed, planet, fog), every generated chunk in full (terrain, biomes,
    features, river flows), what is explored;
  - every field (type, map cell, size, motherlode, initial and current amount, growing state);
    worked-out cells;
  - every building (type, anchor, facing, id, enabled, recipe and the waiting change, input and
    fuel and output choices, Silo resource, buffers, fuel slot and fire, cycle progress and the
    inputs it took, plot and site, feeding state, charge);
  - every truck (id, Depot, bay, state, position, heading, cargo, target, timer, and its path), and
    the cells trucks found unreachable;
  - the stockpile, unlocked buildings, discovered resources, ships launched, the time of day,
    research (active project, progress, paid fees, completed), road tiles and bridges, the player's
    position, the id counters, and the last capacity readings (so the first tick after a load
    sees what the saved one would have).
- **Not saved:** what load rebuilds (who is online, the occupied-cell set, the explored count, the
  network), and the harvest and interaction in progress (an action the player was holding a key
  for).
- **Chunks are saved in full,** not regenerated from the seed, so a save survives changes to the
  world generator.
- **The presentation's part:** the pinned resources and the camera's zoom, saved alongside.

### The format

- **JSON, gzip-compressed** (`.save`), with a header: a format version, the game's version, the save
  time, the play time, the world seed, and a short summary (ships launched, buildings). The sim's
  part is `System.Text.Json` over plain data records (`SaveData`), engine-free like the rest of the
  sim.
- **Versioned:** `SaveData.FormatVersion` starts at 1. A save with a newer version than the game
  knows is listed but cannot be loaded ("Saved by a newer version"). Later changesets that change
  the state add a migration from the previous version.
- **A thumbnail:** a small screenshot (320 x 180 PNG) taken when saving, beside the save file, for
  the load list.

### Where saves go

- `user://saves/` (Godot's per-user data folder): `<name>.save` and `<name>.png`.
- **Slots:** named saves (the save dialog suggests "Save 1", "Save 2", and so on, editable;
  saving over a name asks first); **autosave** every 5 game minutes of play into two rotating files
  (`autosave-1`, `autosave-2`), counted in play time, which stops while the game menu or a save or
  load dialog is open.
- Saving writes to a temporary file and renames it over the old one, so a crash mid-save never
  ruins the previous save.

### UI

- **In-game menu** (Escape, when nothing else uses it: no build mode, no open dialog): a modal titled
  "Paused" over the game (the sim pauses while it is open) with Resume, Save Game, Load Game, Main
  Menu, and Quit. Main Menu and Quit ask first if there is unsaved play ("Unsaved progress will be
  lost").
- **Save dialog:** the list of existing saves (newest first) to overwrite, and a name field with Save.
- **Load dialog** (from the main menu or the in-game menu): saves newest first, each with its
  thumbnail, name, save time, play time, and summary; Load, and Delete (asks first). A save that
  cannot be loaded is shown dimmed with the reason. Loading shows the loading screen's band
  ("Loading" and the bar) over the save's thumbnail while the world is rebuilt.
- **Main menu:** Load Game between New Game and Quit, disabled when there are no saves; Continue
  above New Game when there are saves, loading the newest.

### Testing support

- Scenario commands `saveGame` `{ name }` and `loadGame` `{ name }` (writing to a scenario's own
  folder, not the player's saves), and the snapshot's `app` showing the loaded game.
- Unit tests: a round trip (save, load into a fresh sim, save again: identical); a loaded game
  continuing identically (run, save at a tick, run on; load, run the same ticks: the same state);
  every kind of state above surviving; a newer format refused; a bad file refused without harm.

## Code changes

- `Game.Sim`: `SaveData` and its records; `Simulation.Save` and `Simulation.Load` (rebuilding the
  derived state); `SaveFormat` (JSON, gzip, header, version check).
- Presentation: a `SaveStore` (listing, writing atomically, reading, deleting, thumbnails); the
  in-game menu, the save and load dialogs; Load Game and Continue on the main menu; autosave;
  `AppRoot.LoadGame`.
- Testing: unit tests; a `save-load` scenario; the commands; the schema.

## Blueprint updates

- A new **saving.md** blueprint: what is saved, the format, where saves go, the slots, and loading.
- **presentation.md / ui-style.md:** the in-game menu and the dialogs; the main menu's buttons.
- **simulation.md:** Save and Load in the sim.
- **testing.md:** tests, commands, scenario.

## Verification

- Unit tests above; both suites pass.
- `save-load` scenario: the game menu standing the game still, saving through the dialog, changing
  the stockpile, loading the save back through the dialog (asking first), the `saveGame` and
  `loadGame` commands, and the menu then offering Continue and Load Game. The state's depth
  (buildings mid-cycle, roads, research, trucks mid-trip) is covered by the unit tests.
- Windowed: the in-game menu, the save and load dialogs with thumbnails, Continue on the menu.

## Decisions

1. **Slots:** named saves and two rotating autosaves every 5 game minutes; no quicksave or quickload.
2. **Continue** on the main menu loads the newest save.
3. **Escape opens the in-game menu,** pausing the game, with Main Menu as the way back.
4. **Thumbnails** show in the load list.
5. **Main Menu and Quit warn** about unsaved progress.

## Implementation notes

- The Load Game dialog's unsaved-progress question was added alongside Main Menu and Quit, since
  loading also throws away play.
- Resume replaces the game menu's Close button (they did the same).
- A scenario's or live run's saves go to its own output folder (`<output>/saves`), so runs never
  touch the player's saves and the menu always starts with none.
- Not verified on screen: the main menu's Load Game list and the loading screen over a thumbnail
  (two frames). The same `SaveList` is checked in the in-game load dialog.
