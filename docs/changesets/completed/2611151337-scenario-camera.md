# Scenario Camera: Deterministic Starts and On-Screen Clicks

- Status: Implemented
- Id: 2611151337-scenario-camera
- Depends-on: 2611151313-shutdown-crash

## Summary

Fixes the `trucks` flake (step 13, a world click on the Depot that sometimes opens nothing, about
one run in 300 to 600). Scenario runs start the game from a fixed world, `initMap` and
`teleportPlayer` put the camera on the player at once, and `click` refuses to click a point outside
the window instead of clicking there.

## Motivation

The stress script with temporary diagnostics around the click (`trucks`, 2100 runs) caught a
failure and showed why:

- **The startup world is random.** With no `--seed`, `GameRoot` builds a generated world from a
  random seed (`GD.Randi`) before the scenario runner starts, and the camera starts at that world's
  spawn.
- **Nothing moves the camera to the scenario's start.** `initMap` swaps in the test map and
  `teleportPlayer` moves the player, but the attached camera eases toward the player over many
  frames (`CameraRig` follow), so for the first frames it sits somewhere between the random spawn
  and the player.
- **`trucks` clicks at frame 3.** The aim point (48, 17.5) is projected through that moving
  camera. In passing runs the camera was at (64.8, 53.2) and the click landed at screen y -240,
  already above the window, but its ray still met the Depot. In the failing run the camera was at
  (-39.9, -29.4), the click landed at (1384, 1901), well outside the window, and hit nothing.
  Starting with `--seed=8` reproduces a third camera position every time (the click lands at x
  3468), so the position follows from the startup seed.

So the scenario passed by luck: it clicked outside the window, through a camera still on its way.

## Design

- **A fixed startup world for scenario runs.** With `--scenario` and no `--seed`, `GameRoot` uses
  seed 1, so the world built before the first command is the same every run. (Interactive runs keep
  a random seed.)
- **The camera jumps to the player** after `initMap` and after `teleportPlayer`: a new
  `CameraRig.JumpTo(player)` attaches the camera and centers it on the player at once, with no
  slide. A scenario's first frames then see the view a player would after the camera settles.
- **`click` refuses an off-screen point.** When the aim point (or the mouse) projects outside the
  window, `click` fails with "click at (x, y) is outside the window", so a scenario cannot depend
  on clicking where a player cannot.
- **`trucks`** is unchanged unless the stricter `click` or the jump changes what it sees (it should
  not: the Depot is on screen once the camera is on the player).

## Code changes

- `GameRoot`: the scenario default seed.
- `CameraRig`: `JumpTo`.
- `CommandExecutor`: jump the camera in `InitMap` and `TeleportPlayer`; the off-screen check in
  `Click`.
- Scenarios: any whose expectations relied on the camera still easing in (to be found by running
  them all) get their camera or wait steps adjusted.

## Blueprint updates

- **testing.md:** scenario runs start from seed 1; `initMap` and `teleportPlayer` put the camera on
  the player; `click` fails off screen.

## Verification

- `trucks` with `--seed=8` (formerly a far camera) and every seed from 1 to 30: the camera on the
  player at the click.
- `stress_scenarios.py trucks --runs 600 --jobs 4`: no failures.
- `stress_scenarios.py --all --runs 5`: clean; both test suites pass.

## Decisions

1. **Scenario runs start from seed 1** when no `--seed` is given, removing the last random input to
   a scenario's start; interactive runs keep a random world.

## Implementation notes

- **No scenario needed changes:** every scenario passed on its first run with the camera jump, the
  fixed seed, and the off-screen guard, so none had been relying on the camera still easing in or
  on an off-screen click beyond `trucks`'s lucky one.
- **Results:** `trucks` with every startup seed from 1 to 30 passed with the click on screen (the
  guard would have failed it otherwise); 600 `trucks` stress runs clean; every scenario clean; both
  suites pass.
- **Not every off camera failed before:** of seeds 1 to 30 only seed 8 started the camera
  somewhere unusual, and its far click still happened to meet the Depot; the run that failed came
  from a seed whose camera sent the click's ray past it.
- **Diagnostics used** (temporary prints around the click and a debug copy of the scenario) were
  removed after the cause was found.
