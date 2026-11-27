# Telaelus

*Pronounced tel-AY-lus* (/tɛˈleɪləs/): three syllables, "tel" as in "tell", "ay" as in "day",
"lus" as in "Venus".

## The name

Telaelus blends two Roman deities: **Tellus**, the earth, and **Caelus**, the sky. In Roman myth
Caelus is the sky god and the husband of Terra, the earth, who is identified with Tellus. The game
follows the same path: build up on a planet's surface, then take to the sky.

## Download

To play the latest version, download it from the
[Releases page](https://github.com/cabal95/telaelus/releases/latest).

## A learning project

Telaelus is a learning project first and a playable game second. It exists to learn game
development by doing it: rendering, the game loop, systems design, and the math behind a 2.5D
world. It grows in small, playable steps, and every stage should be a game you can actually play.

## The game

![A busy town around the HQ: mines, smelters, a fission reactor, solar arrays, a shipyard, and
factories between the sea and a forest](docs/images/showcase.png)

Telaelus comes in two parts that share one story: you keep a fleet of ships fueled and supplied,
and you use that fleet to explore and fight in space.

- **Part A, on the planet** (in progress). A factory and logistics game seen from an angled,
  top-down camera, in low-poly, flat-colored 3D. You land on a world hidden by fog, explore it on
  foot, and build mines, camps, and processing plants to turn raw resources into fuel and ship
  parts. Trucks haul from outlying collectors, and a network of hubs links your buildings to the
  HQ.
- **Part B, in space** (later). Exploration and combat with the fleet Part A builds. Its style is
  still open.

It is single-player for now, built so co-op multiplayer can come later.

### What plays today

- Procedural worlds with biomes, rivers, and mountains, under fog of war that lifts as you explore.
- Gathering by hand, and buildings that extract, refine, and manufacture along production chains up
  to ship parts.
- Power from many sources across day and night, storage limits, and a research tree.
- Depot trucks that dispatch themselves, plus roads and bridges.
- A loading screen, a main menu, and saving and loading.

## Built with

- **Godot 4.7 and C# (.NET 8).** The simulation is its own engine-free C# library, run on a fixed
  clock, and the Godot side only draws it and handles input. That split keeps the game logic
  testable and leaves room for multiplayer.
- **Claude, with human review.** Claude, Anthropic's AI, writes the code, and a human reviews every
  change in order to learn how the code works, not just what it does. Each change is first written
  up as a spec (a changeset) and approved before any code is written.
- **UI and graphics designed by AI, too,** through a feedback loop. Python generators build the 3D
  models, and each model is rendered to preview images. Scripted scenarios run the game and take
  screenshots. The AI looks at those images itself, revises, and repeats, and a human reviews the
  result.

## Getting started

You need:

- Godot 4.7.2, .NET edition.
- The .NET SDK 9.0.200 or later. The projects target .NET 8, but the solution file needs SDK 9.

Copy `tools.template.json` to `tools.json` and set the Godot path for your machine. Then:

```bash
dotnet build Telaelus.slnx -c Debug
```

```bash
dotnet test src/Telaelus.Sim.Tests
```

To run the game, launch Godot with `--path src/Telaelus`, or open that folder in the Godot editor.
[CLAUDE.md](CLAUDE.md) has the full list of commands, including the integration tests and the
scenario runner.

The screenshot above comes from the `showcase` scenario. To refresh it, run that scenario windowed
(see CLAUDE.md) and copy its `01-showcase.png` over `docs/images/showcase.png`.

## Read more

- [docs/game-plan.md](docs/game-plan.md): the vision and the roadmap.
- [docs/blueprints/INDEX.md](docs/blueprints/INDEX.md): how each system works today.
- [docs/development.md](docs/development.md): how we work, from spec to commit.
- [docs/asset-pipeline.md](docs/asset-pipeline.md): how the models are made.
