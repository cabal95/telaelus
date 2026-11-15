# Loading Screen and Main Menu

- Status: Implemented
- Id: 2611151428-loading-and-menu
- Depends-on: 2611151401-roads-and-bridges

## Summary

The game starts on a **loading screen** instead of dropping straight into a world: a still image of
a small town built from the game's own pieces, with "Loading" at the bottom center, shown while the
one-time startup work runs (the resource icons, building and field pictures, and UI icons drawn on
the picture stage, and the models loaded). Then a **main menu** over the same town, live, with a
slight blur behind the menu: **New Game** and **Quit** today, with **Load Game** and **Options**
planned for their own changesets. The town is a **Godot scene** you can open and arrange in the
editor; a render command turns it into the loading screen's image.

## Motivation

Save and load needs somewhere to choose a save and a way to start a game other than "the moment the
window opens". Today `GameRoot` builds a random world in `_Ready`, and the startup pictures draw in
batches over the first frames of play (scenarios wait for them before screenshots). A loading
screen gives that work a place, and the menu gives New Game, Load Game, and Options a place.

## Design

### Flow

1. **Boot.** The main scene becomes `scenes/App.tscn`, whose root (`AppRoot`) owns the app's
   stages. Godot's own boot splash (shown while the engine starts) is set to the same town image,
   so the window never shows blank between the engine's splash and ours.
2. **Loading screen** (`LoadingScreen`): the town image filling the window (cropped to cover, never
   stretched), "Loading" in the bottom center over a soft dark band, with a thin progress bar under
   it. It runs the one-time work in order and advances the bar: load every model once
   (`ModelLibrary`), then the resource icons, the building pictures, the field pictures, and the UI
   icons. These move out of `GameRoot` to `AppRoot`, so they are drawn once per run and shared by
   every game (and a later loaded one). Headless runs draw no pictures, so loading takes a frame.
3. **Main menu** (`MainMenu`): the town scene, gently alive (below), seen through its own camera, with the menu
   panel at the left third over a blurred strip of it (the panel's backdrop samples the screen and
   blurs it, darkened a little). The panel holds the title ("Game", the working title) and the
   buttons, top to bottom: **New Game**, **Quit**. **Load Game** and **Options** are planned
   between them and not shown yet. Enter or a click on New Game starts a game; Escape does nothing
   on the menu (Quit is the way out).
4. **New Game:** the menu and its town are freed and a `GameRoot` is created with a new world (a
   random seed, or `--seed=<n>`), given the shared pictures, and added. Everything in play works
   as today.
5. **Back to the menu** is not part of this change (there is no pause menu yet); closing the window
   quits as today.

### The town scene

- `scenes/Town.tscn`, a plain Godot scene: a ground of terrain tiles, a few buildings (an HQ, a
  Smelter, a Glassworks, a Lumber Camp, a Depot with a truck, a Water Wheel on a river), resource
  fields (trees, stone, ore), a road with a bridge, the character, a camera at the game's angle,
  and the sun at midday. Built from the `.glb` models by instancing, so it uses the shipped models
  and stays current when they change. A starting layout is generated once (a script places the
  pieces), then it is yours to arrange in the editor.
- **Rendering the image:** `<godot.editor> --path src/Game -- --render-splash` loads the town scene
  in a 1920 x 1080 viewport, waits for it to draw, saves `assets/ui/splash.png`, and quits. The
  image is committed; re-run the command after changing the scene. (A windowed run, since headless
  cannot render.)
- **The menu** instances the same scene live, with the same camera, so the menu's background and the
  loading image match, and keeps it **gently alive**: the town's truck drives a loop along its road
  and over the bridge at truck speed, and the light follows a clock running at the game's pace
  (`DayLight`, starting mid-morning), so the town slowly warms toward dusk while the menu is open.
  The splash render is taken at midday with the truck parked, so the image is the same every time.

### Scenarios and the control channel

- **A scenario starts at the main menu.** The runner attaches to `AppRoot`, waits for loading to
  finish, and runs its commands from the menu. A new command `newGame` `{ seed? }` goes into a new
  game whatever the current state: from the menu it starts one, and in a game it replaces it with a
  new one (the seed when given, else the scenario seed, 1). Every existing scenario gains it as its
  first step (a script does the edit). `initMap` and the rest then act on the game as today.
  Commands that work the menu and the options come later, with those screens.
- A new `menu` scenario checks the loading screen's progress, the menu's buttons, and starting a
  game from it (state checks; a windowed run screenshots both screens).
- Snapshot: `app` (`loading`, `menu`, or `game`) and, on the loading screen, its `progress`; the
  menu's buttons under `ui` while it shows. Every game field stays where it is once in a game.
- The live control channel attaches to `AppRoot` the same way; a client sends `newGame` first.

### Assets

- `scenes/Town.tscn` (and its generator script, `tools/town_layout.py` in the asset factory or a
  small editor script, whichever keeps the scene easy to edit by hand).
- `assets/ui/splash.png` (1920 x 1080, rendered from the scene).
- A blur shader for the menu panel's backdrop (`assets/ui/blur.gdshader`).

## Code changes

- `AppRoot`, `LoadingScreen`, `MainMenu` (new), `scenes/App.tscn` as the main scene,
  `scenes/Town.tscn`.
- `GameRoot`: created by `AppRoot` with a seed and the shared pictures, no longer the main scene;
  the picture nodes move to `AppRoot`.
- `ScenarioRunner` and `ControlChannel`: attach to `AppRoot`; the `newGame` command; the runner's
  wait for pictures becomes a wait for loading.
- A `--render-splash` mode.
- Scenarios: `newGame` first in each; the `menu` scenario; the schema.

## Blueprint updates

- **presentation.md:** the app's stages (boot, loading, menu, game), the shared pictures, the town
  scene and splash rendering.
- **ui-style.md:** the loading screen and the menu panel.
- **testing.md:** scenarios start at the menu; `newGame`; the snapshot's `app`; the `menu` scenario.
- **simulation.md:** the world is created on New Game, not at launch.

## Verification

- The `menu` scenario (headless and windowed): loading reaches 100% and gives way to the menu; the
  menu shows New Game and Quit; New Game starts a game in a fresh world.
- Every existing scenario passes with `newGame` as its first step; both suites pass.
- `--render-splash` writes the image; a windowed run shows it on the loading screen, and the live
  town behind the blurred menu.
- Startup: the window shows the town image from the first frame (the engine's splash, then ours).

## Decisions

1. **An explicit `newGame` step** begins every scenario and goes into a new game whatever the current
   state (starting one from the menu, replacing one in play). Menu and options commands come later.
2. **The menu's town is gently alive:** the truck drives its loop and the light follows the clock.
3. **Quit is on the menu,** and the panel shows the title ("Game" until there is a name).
4. **A script creates the town's starting layout,** which you then arrange in the editor.

## Implementation notes

- **The town's ground runs well past the town** (map cells -8..17 by -8..15 around a town in 0..9 by
  0..7) so the camera never sees its edge, and the camera's size is 30. The generator is
  `src/asset-factory/tools/town_layout.py` (plain Python); re-running it overwrites hand edits.
- **The old main scene** (`scenes/Main.tscn`) is gone; `scenes/App.tscn` replaces it.
- **Commands before a game:** `wait`, `pause`, `step`, `expect`, `snapshot`, and `screenshot` work on
  the loading screen and the menu (they go through the app's tree); anything that needs a game
  fails with "no game yet: start one with newGame". `newGame` clears any input still queued.
- **The control channel** needs nothing new: a client sends `newGame` among its commands. Starting
  it with `--start-paused` holds the loading screen, which is how its screenshot was taken.
- **The runner and the channel process after the game** (`ProcessPriority` 100), keeping the order
  they had as the game's children.
- **Reviewed:** the blur covers the whole window rather than a left-third strip, at half the
  darkness (27.5% toward the tint), with the menu kept at the left; the truck drives at a third of
  road speed (4 units a second).
