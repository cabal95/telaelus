# Model asset pipeline

- Status: Implemented
- Id: 2611100906-model-asset-pipeline
- Depends-on: none

## Summary

Render resource nodes with CC0 glTF models, scale-normalized and footprint-capped, with a colored
box fallback when a model is missing.

## Motivation

Move node visuals from placeholder boxes toward real art, in a way that is robust to swapping models
and that keeps a model within its single grid cell.

## Design

- `NodeModels` maps each node `ResourceType` to one or more CC0 glTF variants and a target height
  (Ore ~0.8, Wood ~2.5 units).
- A model is scaled to its target height from its bounding box, but the scale is **capped so the
  footprint stays within ~0.85 of a cell** (if targeting the height would make it wider than a
  cell, width wins). It is seated on the ground and given a deterministic per-node yaw.
- When no model file is present, it builds a colored box at the target height so the build and game
  stay green.

## Code changes

- `src/Presentation/NodeModels.cs` (specs, `CreateVisual`, model instancing, `SeatAndNormalize` with
  the footprint cap, bounding-box computation from local transforms, the box fallback).
- `src/Presentation/ResourceNodeViews.cs` instances these visuals and shrinks them by remaining
  fraction.
- `src/assets/models/nature/README.md` documents the expected CC0 files and conventions.

## Blueprint updates

- Updates `resources.md` (node rendering) and `presentation.md` (the asset and model pipeline).

## Verification

With CC0 rock and tree models present, nodes render as models sized within a cell; without them,
colored boxes. Clean build and headless run.
