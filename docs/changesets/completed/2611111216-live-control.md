# Live Control Channel

- Status: Implemented
- Id: 2611111216-live-control
- Depends-on: none (requires 2611111204-scenario-runner, already implemented)

## Summary

Add a debug-only localhost HTTP endpoint that lets an agent drive a running game with the same
commands scenarios use: send input, wait, take screenshots, and read state, one request at a
time. The agent can also pause the game and step it frame by frame, to inspect fine-grained
changes.

## Motivation

- Scenarios check scripted behavior, but during development an agent also needs to explore: try
  something, look at the screen, decide what to do next. That is how it confirms things *look*
  right, not only that the state is right.
- Using the scenario command set means anything explored live can be saved as a scenario file and
  kept as an integration test, with no translation.
- Some changes happen within a frame or two (a ghost snapping to a new anchor, a model
  disappearing at a stage boundary). Pausing and stepping one frame at a time, with a screenshot
  and snapshot after each step, makes those visible.

## Design

### Enabling it

- `<godot.editor> --path src/Game --resolution 1280x720 -- --control-port=<n>` starts the game
  with the channel open on `127.0.0.1:<n>`. It is attached only in debug builds and only when the
  argument is present; it never binds to anything but the loopback address.
- It can be combined with `--fixed-fps 30` for repeatable timing. Without it, the game runs in real
  time as normal.
- `--start-paused` starts the game paused once the world is built, so the agent can set up before
  anything moves.
- Screenshots go to a run folder as in scenario-runner: `test-output/runs/<yyMMdd-HHmmss>-live/`
  by default, or `--output=<dir>`.

### Endpoints

- `GET /status`: `{ "ready": true, "paused": bool, "frame": n, "output": "<run folder>" }` once
  the world is built. Used to wait for startup.
- `POST /commands`: the body is one command or an array of commands, in exactly the scenario step
  format (including `expect`). The response returns after the last command finishes (so a `wait`
  or `hold` holds the response) and contains, per command, its outcome; any `expect` results; the
  absolute path of any screenshot taken; and the state snapshot after the last command.
- `GET /snapshot`: the current state snapshot.
- `POST /quit`: responds, then exits the game.
- Errors: an invalid or unknown command returns HTTP 400 with the reason and runs nothing from that
  request. A failed `expect` is not an HTTP error; it is reported in the response, and the game
  keeps running. A command that fails at runtime (for example an invalid placement) stops the rest
  of that request and is reported in the response's `error` field. Unknown paths or methods return
  404.
- The `/commands` response has `passed` (every `expect` passed), `error`, `results` (per command:
  `index`, `command`, `input`, `outcome`, `message`, `detail`, with a screenshot's absolute `path`
  in its detail), and the final `snapshot`.
- `--control-port` is ignored when `--scenario` is also given.

### Pause and frame stepping

- Three commands, available over the channel and in scenario files:
  - `pause`: pauses the game at the end of the current frame.
  - `resume`: resumes normal running.
  - `step` `{ "frames": n }`: runs exactly n frames, then stays paused. If the game is running,
    it pauses first.
- Pausing uses Godot's scene-tree pause. Game nodes stop processing (the sim clock, camera, views,
  and animations freeze), while the control channel and the command executor run with
  `ProcessMode.Always`, so they keep taking commands. Rendering continues, so a screenshot while
  paused shows the frozen frame.
- With `--fixed-fps 30` (the sim's tick rate), every stepped frame advances exactly one sim tick,
  so `step 1` is one tick. Without it, a stepped frame uses whatever the real frame time was.
- **While paused, input waits for `step`.** `press`, `keyDown`, `keyUp`, and `click` do not inject
  anything; they add events to a pending-input queue. The next `step` unpauses, injects the whole
  queue in order, flushes it, and then runs its frames, so everything queued since the last step
  arrives together in that step's first frame. This is how to test simultaneous input: two keys,
  or a key and a click, in the same frame.
- `press` and `click` put the down event in the next step and queue the up event for the step
  after it, so the key or button reads as held for exactly one frame. That lets both kinds of
  input code see it: `_UnhandledInput` handlers get the event, and code that polls
  `Input.IsPhysicalKeyPressed` (the movement keys) sees it held during that frame. For longer
  holds, use `keyDown`, `step`, and `keyUp`.
- `wait n` and `hold` take time by definition, so while paused they run as steps (`wait n` steps n
  frames; `hold` steps its frames with the key down, delivering any queued input first) and leave
  the game paused. Setup, `aimAt`, `screenshot`, `snapshot`, and `expect` take no frames.
- While running, input commands inject immediately. Input commands in one request with no
  time-taking command between them land in the same frame, and `press` and `click` release on the
  following frame, matching the paused behavior.
- The snapshot gains `paused`, and `frame` now counts only frames the game processed (paused
  frames do not count), so each step's state can be compared with the last. It also gains
  `input.heldKeys` (keys the executor pressed that Godot reports as down), so simultaneous input
  can be checked. A paused `press` shows its key as held until the step after its down step.
- The scenario runner also runs with `ProcessMode.Always`, so scenario files can use `pause` and
  `step`. A new permanent scenario, `pause-step`, covers them.
- `initMap` also resets build mode (no selection, facing North, no aim override) through
  `BuildController.ResetBuildMode()`. A live session keeps one game running across requests, so
  without this, a facing left from earlier commands carried into a fresh map.
- **Input delivery** (from a probe run on Godot 4.7.2): Godot buffers injected input events and
  delivers them when its input buffer is flushed, which happens at the start of the next frame. A
  paused node never receives input, and an event injected while paused is dropped if the tree is
  still paused when the buffer flushes. Injecting in the same frame as unpausing was also dropped,
  because the step had re-paused before the next frame's flush. What worked: unpause, inject, then
  call `Input.FlushBufferedEvents()` immediately; the input arrived within the stepped frame. The
  executor always flushes right after injecting, paused or not, so input lands in a known frame.
  The probe also confirmed that paused nodes do not process and that stepping ran exactly 1 and
  exactly 5 frames.

### Execution

- A `TcpListener` bound to `IPAddress.Loopback`, with a minimal HTTP/1.1 parser (request line,
  headers, `Content-Length` body; each response closes the connection), runs on a background
  thread, accepts requests, and queues them. (The draft named `HttpListener`. On Windows it goes
  through http.sys, which can need admin rights or a URL reservation; a loopback `TcpListener`
  needs neither and is loopback-only by construction.) The `ControlChannel` node drains the queue on Godot's main thread and runs the
  commands with the shared `CommandExecutor` from scenario-runner, then completes the response.
  All game access happens on the main thread.
- One request runs at a time, in arrival order.
- Every request and its result is appended to `session.json` in the run folder, so a live session
  can be reviewed afterward or trimmed into a scenario file.

### Agent workflow

- Start the game in the background with `--control-port`, poll `/status` until ready, then send
  commands with `curl`, for example:

  ```bash
  curl -s localhost:7777/commands -d '[{"press":"Key4"},{"aimAt":{"cell":[36,30]}},{"wait":2},{"screenshot":"ghost"}]'
  ```

  Read the returned screenshot path to see the frame, and decide on the next request. Finish with
  `/quit`.
- When a sequence proves useful as a check, copy its commands into a scenario file under
  `src/Game.Integration.Tests/Scenarios/`.

### Out of scope

- Remote access, authentication, or use outside debug builds.
- Streaming video or pushing events to the client; the agent asks, the game answers.

## Code changes

- New `src/Game/Testing/ControlChannel.cs`: the node (`ProcessMode.Always`) that owns the
  `HttpListener`, the request queue, main-thread dispatch to `CommandExecutor`, JSON responses, and
  the session log.
- `CommandExecutor`: runs with `ProcessMode.Always`; adds `pause`, `resume`, and `step`; holds input
  in a pending queue while paused and delivers it at the next step; schedules the release half of
  `press` and `click` for the following frame; runs `wait` and `hold` as steps while paused; and
  flushes input right after injecting it.
- `StateSnapshot`: adds `paused` and `input.heldKeys`; `frame` comes from the executor.
- `GameRoot`: attach the channel in debug builds. `TestArgs` parses `--control-port` and
  `--start-paused`.
- `ScenarioRunner`: `ProcessMode.Always`; uses the new shared `TestPaths` (repo root, relative
  paths, run folders), which the channel uses too.
- `ScenarioCommand` and `scenario.schema.json`: `pause`, `resume`, and `step`.
- `BuildController`: `ResetBuildMode()`, called by `initMap`.
- New scenario `src/Game.Integration.Tests/Scenarios/pause-step.json`.

## Blueprint updates

- `testing.md`: the live channel (enabling it, endpoints, pause and frame stepping, execution
  model, session log, and the agent workflow), `pause`, `resume`, and `step` in the command list,
  the new snapshot fields, `initMap` resetting build mode, and the `pause-step` scenario.
- `presentation.md`: `GameRoot` attaches the control channel when given `--control-port`.
- `buildings.md`: `BuildController.ResetBuildMode()`.

Process docs (not blueprints) also change in this commit: `CLAUDE.md` (how to start a live session
and the curl pattern).

## Verification

- Start the game with `--control-port`; `/status` reports ready.
- The `build-rotate` scenario's steps, sent in two requests split before the R press, return two
  screenshots showing the ghost rotated, and the snapshots show North then East.
- An unknown command returns 400 and changes nothing; a false `expect` is reported while the game
  keeps running.
- With `--start-paused`, `/status` reports paused and the frame number does not advance between
  two status calls.
- While paused, `step 1` advances the frame by exactly one and the snapshot shows one sim tick of
  movement for a held direction key; `step 5` advances the frame by five.
- While paused, `press R` with a Refinery selected changes nothing until `step 1`; after the step
  the facing has changed and the game is still paused.
- While paused, `keyDown W` and `keyDown D` followed by `step 2` move the player diagonally on
  screen (straight along world -Z, since the camera looks along the diagonal), and both keys show
  as held in that frame; `keyDown D` plus `click` followed by `step 1` delivers
  both in the same frame.
- `/quit` exits the game, and `session.json` lists every request.
- A connection attempt from another machine is refused (the listener binds to loopback only).
- Launching without `--control-port` behaves exactly as before, and the integration tests still
  pass, including the new `pause-step` scenario.
