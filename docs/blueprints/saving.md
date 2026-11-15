# Saving and Loading

## Overview

A game can be saved and loaded. The sim writes its whole state to a versioned save and reads one
back, so a loaded game continues exactly as the saved one would have. Saves live in the player's
data folder as named saves plus two rotating autosaves. The main menu offers Continue and Load
Game; in a game, Escape opens the game menu (Resume, Save Game, Load Game, Main Menu, Quit).

## Current state

### What a save holds

- **The sim's whole state** (`Simulation.Save()` to a `SaveData`, `Simulation.Load(SaveData)`, and
  `Simulation.FromSave(SaveData)` for a new sim; `src/Telaelus.Sim/SaveData.cs` and
  `SimulationSave.cs`):
  - the world settings (kind, seed, planet, fog) and every generated chunk in full (its terrain,
    biome, feature, river flow, and explored layers as byte arrays), plus where the fog was last
    revealed from;
  - every field (id, type, map cell, motherlode, initial and current amount, the building working
    it, growing and time left) and the worked-out cells;
  - every building (id, type, anchor, facing, enabled, recipe and the waiting change, input, fuel,
    and output choices, Silo resource, input, output, and fuel buffers, the fuel burning and time
    left, cycle progress and the inputs it took, its current field, site, and plot, feeding state,
    and charge);
  - every truck (id, Depot, bay, state, position, heading, cargo, target, timer, and its path with
    the point it is heading for), and the cells trucks found unreachable;
  - the stockpile, unlocked buildings, discovered resources, ships launched, the time of day,
    research (active project, progress, paid fees, completed), road tiles and bridges, the player's
    position, the id counters, and the last capacity readings.
- **Amounts keep their order** (dictionaries are saved in insertion order), so a loaded game steps
  in the same order and continues identically.
- **Rebuilt on load:** who is online, the occupied-cell set, the explored count, and the network;
  the roads and the map raise their change events so every view redraws.
- **Not saved:** the player's held actions (harvesting, collecting by hand), which stop on load.
- **Chunks are saved in full,** not regenerated from the seed, so a save survives changes to the
  world generator.
- **The presentation's part:** the pinned resources and the camera's zoom, as a JSON object beside
  the sim's.

### The format

- `SaveFormat` (`src/Telaelus.Sim/SaveFormat.cs`) writes a `SaveFile` as gzip-compressed JSON
  (`System.Text.Json`, enums by name): a `SaveHeader` (format version, the game's version
  (`AppRoot.GameVersion`), the save time, play time in game seconds, the world seed, ships
  launched, and buildings), the sim's `SaveData`, and the presentation's object.
- **Versioned:** `SaveFormat.Version` is 1. A save with a newer version is listed but cannot be
  loaded ("Saved by a newer version"); a file that does not read is "The save is damaged". Both
  are `SaveFormatException`s. A change to the saved state adds a migration from the previous
  version.

### Where saves go

- `SaveStore` (presentation) keeps saves in `user://saves/` (Godot's per-user data folder,
  named after the project: `%APPDATA%\Godot\app_userdata\Telaelus\` on Windows;
  `SaveStore.Folder`): `<name>.save` and a `<name>.png` thumbnail beside it (320 x 180, the game as
  drawn; none headless).
- Writing goes to a temporary file that is then moved over the old one, so a crash mid-save never
  ruins the previous save.
- **Named saves:** the save dialog suggests the first free "Save n"; saving over an existing name
  asks first.
- **Autosaves:** every 5 game minutes of play (`GameRoot.AutosaveSeconds`, 300) into the older of
  `autosave-1` and `autosave-2`. Play time stops while the game menu or a save or load dialog is
  open, so autosaves never land then.
- `SaveStore.List()` lists every save newest first, with its header, or the reason it cannot be
  loaded.

### Loading

- `AppRoot.LoadGame(name)` reads the save (returning the problem when it cannot), frees the menu or
  the game in play, and shows the loading screen with the save's thumbnail behind the band for
  `AppRoot.LoadFrames` (2) frames, then creates a `GameRoot` from the save: the sim from
  `Simulation.FromSave`, the views built over it, the pins and zoom applied, and the play time
  carried on.
- A failed load shows its reason: on the main menu's save list, or on the game menu's status line.

### Unsaved progress

- `GameRoot.PlayTime` counts game seconds played (it stops while the game stands still);
  `HasUnsavedProgress` is true when more than a second has passed since the last save or load (a
  new game starts at none). Main Menu, Quit, and loading another save ask first when there is
  unsaved progress ("Unsaved progress will be lost.").

## Related

- simulation.md (the sim the save captures), presentation.md (the app's stages, the main menu),
  ui-style.md (the game menu and the save and load dialogs), testing.md (`SaveTests`, the
  `saveGame` and `loadGame` commands, the `save-load` scenario).
