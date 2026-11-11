# Changesets

A changeset is one coherent unit of change: what we are adding or altering, the code it requires,
and the blueprint updates it entails. A changeset in progress lives here as `<slug>.md`; once
implemented it is stamped `YYMMDDHHMM-slug.md` and moved to `completed/`. (The lifecycle and naming
rules are in ../development.md.)

## Completed

Newest last. One line each; open the file for the full spec.

- **2611100901-project-foundation** - Stands up the Godot C# project, the sim/presentation
  architecture, the fixed-step loop, and the orthographic camera.
- **2611100902-player-and-grid** - Adds the tile grid, a drivable player with camera-relative
  movement, and a follow camera with zoom.
- **2611100903-resources-and-harvest** - Adds resource nodes, the stockpile, hand-harvesting, and
  the HUD.
- **2611100904-buildings-and-production** - Adds grid building placement and the Ore to Metal to
  Fuel production chain.
- **2611100905-ship-launch** - Adds the ship launch win condition (500 Fuel, 100 Metal).
- **2611100906-model-asset-pipeline** - Renders resource nodes with footprint-capped CC0 models and
  a colored box fallback.
- **2611100907-animated-character** - Renders the player from an animated character scene with a
  capsule fallback.
- **2611100908-terrain** - Adds tile-based terrain (grass, water, mountain) with a fixed layout and
  colored tiles with shoreline previews.
- **2611100909-impassability** - Blocks movement and placement on water, mountain, resource nodes,
  and buildings.

## Adding a changeset

Copy the template below to `docs/changesets/<slug>.md`. When it is implemented, stamp its prefix,
move it to `completed/`, and add a one-line entry above.

```markdown
# <Title>

- Status: Proposed | Accepted | Implemented
- Id: <YYMMDDHHMM-slug> (assigned when Implemented)
- Depends-on: <id(s), or none>

## Summary

One or two sentences: what this changeset does.

## Motivation

Why we are making this change.

## Design

How it works: usage, construction, features, and key values (sizes, rates, costs).

## Code changes

The C# and asset or scene changes required: files, types, methods, and behavior.

## Blueprint updates

Which blueprint(s) change and how, so each reflects the new present after this lands.

## Verification

How we confirm it works: build, headless run, and the in-game check.
```
