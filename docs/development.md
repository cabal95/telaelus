# Development Process: Spec-Driven Development

We develop this project spec-first. Before a code change, we write a spec, and the spec drives
the code. There are two kinds of spec, with distinct roles and names.

- **Blueprints** describe the *current state*: how an area of the project works and the decisions
  behind it, as they are right now.
- **Changesets** describe a *unit of change*: what we are adding or altering, the code it
  requires, and the blueprint updates it entails.

Blueprints answer "how does X work today?" Changesets answer "what changed, and why?"

## Blueprints (living, present-tense)

A blueprint is a living document for one area of the project. It always reflects the present.

- **Present tense, current state only.** A blueprint describes what *is*, never what *was*. It
  carries no history and no "previously." If terrain has three types, the terrain blueprint lists
  three types, with no hint there were ever two.
- **Authoritative.** When a blueprint and the code disagree, that is a defect in one of them to be
  reconciled, not accepted drift.
- **Changed only by changesets.** A blueprint is edited as part of a changeset that spells out the
  edit. The blueprint's history lives in git and in the changeset that made each change.

Location: `docs/blueprints/<area>.md`, one per area. Current areas: `simulation`, `terrain`,
`world-generation`, `resources`, `buildings`, `player`, `presentation`, `art-style`, `testing`. See `docs/blueprints/INDEX.md` for the area index and the template.

## Changesets (one change, permanent once implemented)

A changeset describes one coherent change. It contains:

- **Title and status**, and once implemented, its **id**.
- **Depends-on** (if any).
- **Summary** and **Motivation**.
- **Design**: usage, construction, features, key values.
- **Code changes**: the C# and asset or scene changes required.
- **Blueprint updates**: which blueprint(s) change and how, so each reflects the new present.
- **Verification**: how we confirm it works.

See `docs/changesets/INDEX.md` for the completed-changeset index and the template.

### Location and status lifecycle

1. **Proposed** - drafted as `docs/changesets/<slug>.md` (no prefix), so active work is easy to
   find and not buried among completed specs.
2. **Accepted** - the spec is approved before any code is written. Still `docs/changesets/<slug>.md`.
3. **Implemented** - the code is written; the sim unit tests pass (`dotnet test
   src/Telaelus.Sim.Tests`), and the integration tests pass when the change touches presentation or
   input; we review the running result; any small tweak is folded back into the
   code, the changeset, and the blueprint so the changeset stays truthful; then the changeset is
   stamped with its prefix, moved to `docs/changesets/completed/<YYMMDDHHMM>-<slug>.md`,
   its `Depends-on` is re-checked against current main, and it is committed. A large deviation from
   an Accepted spec means the spec was wrong, so we rework the changeset rather than patch it.

Once committed, an Implemented changeset is immutable; a later change is a new changeset.

**Future** is a parking place outside the lifecycle: `docs/changesets/future/<slug>.md` holds a
plan we expect to want but have decided not to build yet, so the research and decisions behind it
are not lost. A future changeset records what we learned (measurements, options weighed, why it
waits) as well as the plan, and may be rough or out of date. Nothing in it is approved. When its
time comes, it moves to `docs/changesets/<slug>.md` as **Proposed**, is brought up to date against
the current blueprints, and goes through the normal lifecycle from there.

### When a change needs a changeset

- A changeset is required when a change alters what the blueprints describe: new behavior, a new
  or changed rule, or a design change.
- A fix that brings the code in line with what a blueprint already says needs no changeset. It is
  a normal commit with a regression test (a unit test, or a scenario for input or presentation),
  and the commit message names the bug and the blueprint it restores.
- If the blueprint is silent on the behavior, or describes the buggy behavior as the rule, the fix
  changes the rule, so it is a changeset.
- Reproducing a bug first (a test, an ad hoc scenario, or the live channel) changes no code and
  can happen before any changeset is drafted.

### Tests

- New or changed sim behavior gets unit tests in the same changeset.
- New input, rendering, or UI behavior gets a permanent scenario (an integration test) when it can
  be checked through the state snapshot.
- A changeset's **Verification** section names the unit tests and scenarios it adds, changes, or
  relies on.
- Passing `dotnet test src/Telaelus.Sim.Tests` is required before a changeset is stamped Implemented.
  When the changeset touches presentation or input, `dotnet test src/Telaelus.Integration.Tests` must
  pass too; running it for every changeset is recommended.
- While implementing, use ad hoc scenarios with screenshots to see that new behavior looks right,
  not only that its state is right.
  See `docs/blueprints/testing.md` for how the tests and scenarios are organized.

### Filename, ordering, and Depends-on

- A completed changeset is `YYMMDDHHMM-slug.md`. The `YYMMDDHHMM` prefix is assigned at the moment
  it moves to **Implemented**, not when it was created, so it reflects when the change actually
  landed (and, with it, the dependencies that actually existed at that point).
- The prefix is the primary apply/sort order. Because it is stamped at implementation, prefix
  order matches real landing order.
- **Depends-on** is a shallow "apply me after these" edge listing changeset ids. It is not
  verified or deep-inspected; it is only an ordering hint. It is added only when prefix order would
  not already place a prerequisite first: two changesets sharing a prefix that depend on each
  other, or a changeset that landed out of its natural order (for example a long-lived branch that
  merged main in). In normal flow it is rarely needed.
- **Replay** (for example reconstructing the project in a blank repo from the changesets alone):
  apply completed changesets in prefix order, honoring any `Depends-on` edges topologically.
  Independent changesets may apply in any order.
- **Reference** a changeset by its full id (prefix + slug), never the bare slug, since slugs such
  as `update-tile-size` recur.

## Commit mapping

One changeset is one commit: the changeset file (moved to `completed/`), the code, and the
blueprint edits land together, so git history and the changeset record stay aligned.

## Worked example

- A changeset establishes two terrain types, dirt and water. After it lands, the terrain blueprint
  lists dirt and water.
- A later changeset adds desert: it describes how desert is used and constructed, its features, the
  C# changes, and the edit to the terrain blueprint to cover three types.
- After it lands, the terrain blueprint lists three types, with no trace that it once listed two.
  To learn that desert was added, you read that changeset or git history, not the blueprint.

## Baseline

The project predates this process, so its current state is captured as a set of **baseline
changesets** in `completed/` (project-foundation, player-and-grid, resources-and-harvest,
buildings-and-production, ship-launch, model-asset-pipeline, animated-character, terrain,
impassability). Their prefixes use faked sequential values to encode the linear build order; being
sequential, they need no `Depends-on`. They are the recipe the blank-repo replay test follows to
confirm the specs reproduce the game. The ~15 real commits that predate the process are left as-is
and are not retrofitted into changesets.

## Relationship to the other docs

- `development.md` (this file), `code-style.md`, and `CLAUDE.md` are *process* docs: how we work.
  They are not blueprints or changesets.
- `game-plan.md` holds the long-term vision and roadmap (the "why" and "where we are going").
  Current-state detail lives in the blueprints, which are authoritative for "what is."

## Deployment references (future)

We have not addressed deployment yet. Material to revisit when we do:

- [Building Xbox games with Godot](https://developer.microsoft.com/en-us/games/articles/2026/06/building-xbox-games-with-godot/)
  (Microsoft): deploying Godot games to Xbox.
