# Animated character

- Status: Implemented
- Id: 2611100907-animated-character
- Depends-on: none

## Summary

Render the player from a hand-assembled animated character scene, with a capsule fallback.

## Motivation

Replace the capsule with a real animated character, keeping all model-specific wiring in an asset
(the scene) rather than in code.

## Design

- The character is a Godot scene (`src/assets/models/character/character.tscn`) that packages the
  model, an `AnimationPlayer` with idle and run clips, and the skin as a material override.
- `PlayerView` loads the scene, finds its `AnimationPlayer`, and plays an idle or walk clip based on
  movement. Clips are matched by their leaf name (stripping any `library/` and `Root|` prefixes) so
  it ignores library names and selects the real clip.
- Facing is applied by rotating `PlayerView`'s own wrapper node, which preserves the scene's baked
  orientation and scale. If the scene is absent it falls back to a capsule.

## Code changes

- `src/Presentation/PlayerView.cs` (load the scene, find the `AnimationPlayer`, play idle/walk,
  `ClipName` leaf matching, face by rotating the wrapper, capsule fallback). `GameRoot` drives
  `PlayerView` with the player position and move direction.
- The character assets and `character.tscn`; `src/assets/models/character/README.md`.

## Blueprint updates

- Updates `player.md` (the character and camera) and `presentation.md` (the character asset).

## Verification

The character stands and plays idle, plays run while moving, faces the move direction, and stays
upright. Clean build and headless run.
