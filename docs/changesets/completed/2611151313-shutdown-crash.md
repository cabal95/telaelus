# Shutdown Crash: Injected Input Events and Finalizers

- Status: Implemented
- Id: 2611151313-shutdown-crash
- Depends-on: 2611151236-research

## Summary

Fixes the intermittent crash on quitting (about one run in a hundred of a scenario that clicks UI
and presses Escape), and adds the tools that found it: a stress script that runs scenarios many
times and sorts what happens, with crash dumps, and shutdown breadcrumbs in every scenario run.

## Motivation

Integration runs sometimes died with 0xC0000005 (access violation) or 0xC000001D (illegal
instruction) and passed on a rerun. Event Viewer showed .NET's finalizer thread in
`GodotObject.Finalize`. A stress run of `trucks` (100 runs, 4 at once, dumps on) caught one with a
dump and its stderr:

- The run had passed: `report.json` was written, quit was requested, and the game root left the
  tree. It died after that.
- Godot's C# module, cleaning up at exit, reported "Leaked unsafe reference to object" for an
  `InputEventMouseButton` and an `InputEventKey` (Escape) the scenario had injected, then hit
  `FATAL: Condition "!rc_owner" is true` (`csharp_script.cpp`), while .NET's finalizer thread was in
  `GodotObject.Finalize` disposing a RefCounted object
  (`godotsharp_internal_refcounted_disposed`).

The cause: `CommandExecutor` creates input events in C# and hands them to `Input.ParseInputEvent`
or the UI, then drops them without disposing. Each keeps a managed wrapper with a reference until
the garbage collector finalizes it, which may not happen before quit. At exit Godot frees those
objects itself while the finalizer thread can be finalizing the same ones, and the two race.

## Design

### The fix

- **Dispose every injected event once delivered.** After `Input.ParseInputEvent` or the UI push
  (and for releases, drags, and deferred input alike), the executor disposes the event's managed
  wrapper. Godot keeps its own reference while it still needs the event, so delivery is unchanged;
  the wrapper no longer outlives the run.
- **Finish finalizers while the engine is alive.** When the game root leaves the tree on the way
  out, it runs `GC.Collect()` and `GC.WaitForPendingFinalizers()` (twice, so objects freed by one
  round's finalizers go too), so any other leftover C# wrapper (textures, meshes, events the game
  reads) is finalized before Godot's C# cleanup. This covers a normal quit as well as a scenario.

### The tools (kept)

- **`src/Game.Integration.Tests/tools/stress_scenarios.py`:** runs named scenarios (or `--all`) `--runs`
  times, `--jobs` at once, headless as the integration tests do, and classes each run: ok, failed
  (the scenario failed), shutdown (passed, then died), crash (died before its report), or timeout.
  It keeps each problem run's stdout, stderr, report, and shutdown log, prints the distinct crash
  signatures (the .NET fatal lines and top frames), and writes `summary.json` under
  `test-output/stress/<time>/`. With `--dumps` the .NET runtime writes a dump when a run dies
  (`DOTNET_DbgEnableMiniDump` and its settings, on the Godot process only).
- **`ShutdownTrace`:** a scenario run writes `shutdown.log` in its output folder, flushed line by
  line: the trace start, the report written and quit requested, the game root leaving the tree,
  and the last node's deletion. A run that dies while quitting shows how far it got.

## Code changes

- `CommandExecutor`: dispose each injected `InputEvent` after delivering it.
- `GameRoot`: collect and wait for finalizers in `_ExitTree`.
- `ShutdownTrace` (new) and its marks in `ScenarioRunner`.
- `tools/stress_scenarios.py` (new) in the integration test project.

## Blueprint updates

- **testing.md:** the stress script and how to read its results; `shutdown.log`; injected events
  disposed after delivery.
- **presentation.md:** the game root finishing finalizers on exit.

## Verification

- Before: `stress_scenarios.py trucks --runs 300 --jobs 4 --dumps` gives the crash rate without
  the fix (about 1%: 1 of 100 so far).
- After: the same 300 runs with no shutdown or crash runs, and no "Leaked unsafe reference" lines
  in stderr; `--all --runs 5` clean.
- Both test suites pass.

## Decisions

1. **The stress script lives in `src/Game.Integration.Tests/tools/`,** beside the integration tests
   it mirrors, in a folder for the project's scripts.

## Implementation notes

- **A forced repro (`--exit-garbage=<n>`).** The natural crash proved too rare to measure: after the
  first hit (1 in 100), 300 more unfixed `trucks` runs were all clean. So the runner gained a stress
  knob: just before quitting it drops n C# input events and starts a collection without waiting,
  so the finalizer thread is still at work as the engine shuts down. With 20000 events, unfixed:
  **20 of 20** runs crashed at shutdown with the exact signature from the first hit and from Event
  Viewer (`godotsharp_internal_refcounted_disposed` in `GodotObject.Finalize`, 0xC000001D) and
  5555 leaked references reported. Fixed: **0 of 100**, no leaked references.
- **Results with the fix:** `trucks` 600 natural runs with no shutdown or crash runs; every
  scenario 5 times (155 runs) clean; both suites pass.
- **A separate, older flake** turned up: `trucks` step 13 (a world click on the Depot that does not
  open its panel) fails about once in 200 to 600 runs. It is not from this fix: with the disposal
  taken out it still failed (1 in 600), and it never touches shutdown. Left for its own
  investigation.
- **Stress script extras:** the leaked-reference count per run (the crash's precondition) and the
  `--exit-garbage` pass-through. A lazy-GC option was tried and dropped: normal runs leave no
  references at exit either way, so it showed nothing.
- **Trace lines:** the game root marks leaving the tree and finishing finalizers; the watcher marks
  its own exit and deletion.
