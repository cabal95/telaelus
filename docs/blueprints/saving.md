# Saving and Loading

## Overview

A game can be saved and loaded. The sim writes its whole state to a versioned save and reads one
back, so a loaded game continues exactly as the saved one would have. Saves live in the player's
data folder as named saves plus two rotating autosaves. The main menu offers Continue and Load
Game; in a game, Escape opens the game menu (Resume, Save Game, Load Game, Trainer, Main Menu, Quit).

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
  - every building (id, type, anchor, facing, footprint width, enabled, recipe and the waiting change, crafting
    queue (each order's recipe and items left; absent in older saves, an empty queue), hold
    (recipe and target; absent in older saves, Queue mode), input
    choices by recipe (an older save's by input alone belong to its recipe), fuel
    and output choices, Silo resource, output buffer (an older save's input buffer and fuel slot
    go to storage on load), the fuel burning and time
    left, cycle progress and the inputs it took, its current field, site, and plot, feeding state,
    and charge). A Burner Generator saved before it grew to 4 x 4 (a width other than its own, or
    none) grows around its old center (its anchor one cell back on each axis) when every cell of
    the 4 x 4 can be built on; otherwise it is removed and its whole cost goes to storage (past a
    limit if need be), told to the player on the first step after the load;
  - every truck (id, Depot, bay, state, position, heading, cargo, target, timer, and its path with
    the point it is heading for), and the cells trucks found unreachable;
  - the stockpile, discovered resources, ships launched, the time of day,
    research (active project, progress, paid fees, completed), road tiles and bridges, the player's
    position, the id counters, and the last capacity readings.
- **Amounts keep their order** (dictionaries are saved in insertion order), so a loaded game steps
  in the same order and continues identically.
- **Rebuilt on load:** who is online, the occupied-cell set, the explored count, and the network;
  the roads and the map raise their change events so every view redraws.
- **Not saved:** the player's held actions (harvesting, collecting by hand), which stop on load.
- **Chunks are saved in full,** not regenerated from the seed, so a save survives changes to the
  world generator.
- **The presentation's part:** the pinned resources, the camera's zoom, and the trainer's step
  (0-based, or null with the trainer off; a loaded save resumes it), as a JSON object beside the
  sim's.

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
- No unlocked-building list is saved: the unlocked tier comes from the research (research.md). An
  older save's list is ignored, so it loads at the tier its research gives; buildings it has above
  that tier keep working, but no more can be placed.

### The demo save

- **What it is:** a working mining town shipped inside the game (`AppRoot.DemoSavePath`,
  `res://assets/saves/demo.save`, with `demo.png` as its thumbnail). The main menu's **Demo** loads
  it (`AppRoot.LoadDemo`) the way Load Game loads a save. Saving it makes a named save, so the demo
  never changes. The export presets include `assets/saves/*.save`, since Godot leaves out files it
  does not import.
- **The town** (`DemoTown`, in the sim): on generated world `DemoTown.Seed`, the world's fields
  within 12 map cells of the spawn are cleared and the town's own placed, then its buildings are
  bought and placed as a player would (each cost put in the stockpile first). Its Drill Rig is tier
  2, so it first researches Steel Works (its fee paid the same way, the work done at once). It
  has:
  - a Drill Rig on Iron Ore, powered by a Burner Generator;
  - Mines on Copper Ore and Coal, and a Lumber Camp;
  - five Smelters (four on Iron, one on Copper) and a Charcoal Kiln;
  - a Depot with its four trucks, a Stockyard and three Warehouses, a Research Lab on Material
    Transfer, eleven Small Hubs, and a road along the spawn's row.

  A Wood field out of the Lumber Camp's reach and a Stone field are left for gathering by hand.
  The builder explores the ground by walking the player over it first.
- **Balanced to keep running:** each hauled resource is used about as fast as it is mined, so the
  trucks never wait on full storage. Material Transfer finishes about 15 minutes after loading; the
  Smelters' Iron and Copper fill storage at about 35 minutes.
- **Made by running** `DemoTown.Create()` (built, then run `DemoTown.RunSeconds`, 300 game seconds,
  from a time of day that ends near midday). `--write-demo-save` (presentation.md) opens that as a
  game with the town's resources pinned and the camera at its widest, and saves it. Re-run it
  when the town should change; a newer save format migrates it like any save.

### Unsaved progress

- `GameRoot.PlayTime` counts game seconds played (it stops while the game stands still);
  `HasUnsavedProgress` is true when more than a second has passed since the last save or load (a
  new game starts at none). Main Menu, Quit, and loading another save ask first when there is
  unsaved progress ("Unsaved progress will be lost.").

## Related

- simulation.md (the sim the save captures), presentation.md (the app's stages, the main menu),
  ui-style.md (the game menu and the save and load dialogs), testing.md (`SaveTests`, the
  `saveGame`, `loadGame`, and `loadDemo` commands, the `save-load` and `demo` scenarios,
  `DemoTownTests`).
