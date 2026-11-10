# Game Plan

> Living design document. This is a learning project first and a playable game second.
> Decisions here are provisional and will change as we learn. When we change a decision,
> we update this file and note why.

## Goals

1. **Learn by building.** Daniel wants hands-on involvement in the parts worth learning:
   rendering, game loop, systems design, and the math behind a 2.5D world. Prior
   experience: many years of C#, C, and assembly, plus JavaScript-family languages, and a
   pass through basic OpenGL last year.
2. **Stay playable at every stage.** The output is a game you can actually play, not a
   rendered demo video. We prefer vertical slices that run over broad systems that do not.
3. **Keep it incremental.** Build the smallest thing that is fun or instructive, get it
   running, then grow it.

## The Game (concept)

A two-part game sharing one fiction: you keep a fleet of ships fueled and supplied, and you
use that fleet to explore and fight in space.

### Part A: On-Planet (build this first)

A 2.5D top-down game at a slightly tilted camera angle (think the Age of Empires isometric
or dimetric look). Core loop:

- Land on a planet.
- Harvest raw resources from the terrain.
- Route those resources through a **factory / production chain** to refine fuel and other
  goods the ship needs.
- Load the goods back onto the ship.

Think of it as a focused factory and logistics mini-game (in the spirit of Factorio or
Satisfactory, scoped down) that feeds the space game.

Planets can vary later. Procedural generation of terrain and resource distribution fits
cleanly on top of this once the core loop works, so we treat it as a later layer rather
than a day-one requirement.

### Part B: In-Space (build this second)

Space exploration and combat. The style is still open:

- Turn-based tactics, or
- Real-time strategy (fleet command), or
- First-person / third-person flying and shooting.

We defer this choice. The on-planet game produces fuel and resources, which gives the space
game its stakes, so Part A is the natural starting point regardless of where Part B lands.

## Scope Discipline

To avoid the classic "huge game, never ships" trap, we define milestones as playable slices.

### Milestone 0: Hello, world (window + loop)

- A window opens, a game loop runs at a stable frame rate, input is read.
- Draw something that moves. This is where the OpenGL refresher happens.

### Milestone 1: A world you can look at and move around

- Render a tile-based ground plane at the 2.5D camera angle.
- Camera pan and zoom.
- A controllable unit or cursor on the ground.

### Milestone 2: Resources exist

- Resource nodes placed on the map.
- A way to designate harvesting and watch a resource count go up.

### Milestone 3: The factory loop

- Place a building that consumes an input resource and produces an output over time.
- A simple production chain: raw -> intermediate -> fuel.
- Enough UI to see inventories and rates.

### Milestone 4: The ship needs fuel

- A ship entity that consumes the fuel you produced.
- A win/continue condition tied to keeping it fueled. This closes the first real game loop.

Later milestones (procedural planets, saving/loading, the space game) come after Milestone 4
proves the core loop is fun.

## Technical Stack

### Decided

- **Engine: Godot 4 (.NET / C# edition).** Chosen for the balance of a higher-level engine
  (built-in game loop via per-frame callbacks, scene tree, editor, physics, audio, UI) with
  an open-source, layered API we can descend into (custom shaders, the low-level
  RenderingDevice) when a topic is worth learning in depth. Being open source means we can
  read the engine itself when we want to understand how something works.
- **Fallback: Unity.** If Godot's C# workflow or tooling gets in the way, Unity is the
  backup (also C#, excellent 2.5D support), accepting its closed source and heavier setup.
- **Language: C#.** Daniel's strongest language and a first-class Godot language.
- **Toolchain: .NET SDK 8 or newer** (required for the Godot C# build). Editor options on
  Windows: Visual Studio, Rider, or VS Code with the C# Dev Kit. We pin the exact Godot 4.x
  version at install time.

### Batteries Godot already includes (we do NOT add libraries for these)

- Rendering (2D and 3D), shaders, scene tree, input mapping, audio, UI, animation.
- TileMap and GridMap for tile / grid worlds (directly relevant to the factory grid).
- Pathfinding: AStarGrid2D, AStar2D, NavigationServer.
- Procedural noise: FastNoiseLite (useful for procedural planets later).
- Math types (Vector2/3, Transform) and scene / resource serialization for saves.

### Libraries and tools to add only when needed (deferred, not day-one)

- **Testing:** a C# test runner such as GdUnit4 or Chickensoft GoDotTest, added at the first
  milestone where automated tests pay off.
- **Live debug UI:** imgui-godot (Dear ImGui for Godot) for inspecting and tweaking a
  systems-heavy game at runtime. Add once the factory has enough knobs to be worth it.
- **Entity scaling:** Godot models entities as scene-tree nodes. A factory game can grow to
  thousands of items on belts. If node overhead becomes a bottleneck, consider a C# ECS (for
  example Arch or Flecs.NET) for the high-count simulation while keeping nodes for
  presentation. This is a known decision point, revisited when counts actually hurt.
- **Saves:** start with Godot's Resource system or System.Text.Json; revisit if state grows
  complex.

## Open Questions

- Space game genre (turn-based / RTS / action).
- Art style and how we source art (programmer art first; real art later).
- 2.5D rendering: real 3D geometry with an angled camera, or 2D isometric sprites.

## How We Work

- Small, running increments over big-bang features.
- Each milestone ends with something playable.
- This document is the source of truth for the plan; we keep it current.
