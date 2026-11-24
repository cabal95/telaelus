# One Flat Color for Network Coverage

- Status: Implemented
- Id: 2611241432-flat-network-overlay

## Summary

Where hub coverage discs overlap, the network overlay no longer darkens. All covered ground shows
one even tint.

## Motivation

Each disc is translucent, so two overlapping discs draw twice as dark, and a dense base turns into
a patchwork of shades that mean nothing. The overlay should answer one question: is this ground
covered?

## Design

- **One plane, one tint:** coverage draws as a single flat plane over the ground, sized each frame
  to the shown hubs' circles. Its shader gets every circle (center, radius, online or not) and
  colors each point once: the online accent where any online hub covers it, else gray where an
  offline or switched-off one does, else nothing. Overlapping coverage never darkens, and where
  online and offline coverage overlap the ground reads online, which is the true answer for a
  building there.
- **The placement ghost** (green) keeps its own disc, drawn over the plane, so while placing a hub
  the player still sees its reach on top of existing coverage.
- The shader holds up to 256 hubs; past that it drops offline circles first.
- Link lines and the offline `Warning` rings are unchanged.

## Code changes

- `src/Telaelus/Presentation/NetworkView.cs`: the coverage plane and its shader (inline, as a
  `Shader` built in code), the ghost disc kept as before.
- Windowed check with the `network` scenario's `overlay` and `placing-hub` screenshots (the HQ's
  coverage overlaps a Small Hub's).

## Blueprint updates

- presentation.md: Network view draws coverage as one flat tint, online over offline, with the
  ghost on top.

## Verification

- Integration tests pass.
- A screenshot with two overlapping online hubs shows no darker lens where they meet.

## Implementation notes

- Drafted with the stencil buffer (`BaseMaterial3D` stencil, online discs first). On the
  project's Mobile renderer the stencil had no effect on these translucent discs (the overlap
  still darkened), so the design moved to one plane whose shader takes the circles. The
  outcome the changeset asked for is the same.
- Screenshots: `overlay` shows the HQ's and a Small Hub's coverage as one even tint, and
  `placing-hub` the green ghost over it.
