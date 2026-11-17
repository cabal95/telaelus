# Low-Poly Character

- Status: Implemented
- Id: 2611162133-low-poly-character
- Depends-on: none

## Summary

The player's character becomes a generated low-poly model in flat matte colors, like every other
model in the game, with its own idle and run clips. The textured CC0 character and its FBX files
go away, and the art style loses its one exception.

## Motivation

Every world model is generated low-poly with flat colors (art-style.md), except the character,
which is a smooth, textured CC0 import. Up close it reads as from another game. The generator
already builds trucks, buildings, and items, but its exporter writes only static meshes, so the
character has stayed an import.

## Design

### The figure

- **A colonist in a work suit,** about 1.4 units tall (the current height, so it still reads
  beside buildings and trucks), built from chunky separate parts: head, torso, hips, upper and
  lower arms, hands, upper and lower legs, and boots. The parts are boxes and tapered boxes with a
  few bevels, in the same mesh helpers the trucks use.
- **Proportions:** slightly stylized, with a head a little large for the body (about one fifth of
  the height), so the figure reads at the default zoom where the face is a few pixels.
- **Colors** from the shared material table, flat and matte: a suit in one strong color that
  stands out against grass, sand, and rock (see OQ1), darker boots and gloves, a lighter chest
  panel or belt, and a skin tone for the face. No texture and no emission.
- **Authored facing +Z** (south, toward the viewer's lower left), since `PlayerView` turns its
  wrapper by `atan2(x, z)` and expects the model's front at +Z. Origin at the feet, centered.

### Rigid parts, no skin

Each part hangs from a joint node and moves with it entirely: it pivots, it never bends or
stretches. This is the usual low-poly approach and matches the game's faceted look. The joints
form a small tree: hips (the root, which also bobs), torso, head, and for each side the shoulder,
elbow, hip, and knee. Hands and boots ride on the lower arm and leg.

### The clips

- **idle:** a slow breath (the torso rising a few hundredths of a unit and settling) and a slight
  arm sway, about 3 seconds, looping.
- **run:** a run cycle matched to the player's speed (6 units a second): two strides per cycle of
  about 0.6 seconds, so the feet do not slide much against the ground. Arms swing opposite the
  legs, the knees bend on the forward swing, and the hips bob twice per cycle. Looping.
- The clip names contain "idle" and "run", which is all `PlayerView` matches on, so its code does
  not change.

### Export

`export_glb.py` learns a second kind of model: a node tree with one mesh per node, plus glTF
animations that key each joint node's rotation (and the hips' translation) with linear
interpolation. Godot imports that as a scene with an `AnimationPlayer` holding the clips.
Existing static models keep their current single-mesh path, so they export unchanged.

### Previews

The kept preview is a sheet of the character from the game's camera angle and from the front and
side, plus a strip of poses sampled from each clip (four from run, two from idle). It shows
silhouette, color, and the range of motion; the feel of the run is judged in a windowed run.

## Code changes

- `src/asset-factory/tools/character_gen.py` (new): the parts, the joint tree, and the two clips
  as keyframe tables (time, joint, rotation).
- `src/asset-factory/tools/export_glb.py`: node trees and animations; static models unchanged.
- `src/asset-factory/tools/mesh.py`: the `suit`, `skin`, `hair`, and `boot` palette entries.
- `src/Telaelus/assets/models/character/`: `character.glb` and its `.import` replace
  `character.fbx`, `character-idle.fbx`, `character-run.fbx`, `character-male.png`, and their
  `.import` files. `character.tscn` instances the `.glb` (keeping its uid, so Town.tscn still
  finds it), with no material override and no baked scale; `character.glb.import` sets both clips
  to loop.
- `src/Telaelus/assets/models/character/README.md` (rewritten): the size, facing, joints, and clips, like
  the units README.
- `src/Telaelus/Presentation/PlayerView.cs`: unchanged.
- `src/Telaelus/assets/ui/splash.png` re-rendered (Town.tscn shows the character), and the demo
  save's `demo.png` rewritten.

## Blueprint updates

- art-style.md: the character is generated like every other model; the CC0 exception and its
  mentions go. A short "Character" section gives its height, colors, facing, and parts.
- asset-pipeline.md: the exporter writes animated node trees; the Limits note about rigging is
  narrowed to deforming (skinned) models, which still need a 3D tool.
- player.md: the character scene is the generated model with idle and run clips (the matching rule
  is unchanged).

## Verification

- `dotnet build`, the sim tests, and the integration tests pass (the scenarios read state, not the
  model, so none should change).
- The headless runtime check prints nothing on stderr (the import is clean).
- The preview sheet, reviewed for silhouette and color beside the truck and a building.
- A windowed run of the `walk` scenario with screenshots, standing and running, and then your own
  play to judge the feel; expect a round or two of tuning the run.

## Decisions

1. **Suit color:** orange.
2. **Head:** bare, with brown hair, two dark eyes, and a nose.
3. **Clips:** idle and run only; a harvest swing can be its own changeset.
4. **Old files:** the FBX character and its texture are deleted.

## Implementation notes

- The preview needed no new renderer code: `character_gen.py` writes the sampled poses as OBJs
  (`character_pose_0..5`), and `preview.py --each 6` lays them out.
- `export_glb.py` grew a small `Glb` writer shared by both paths; the truck re-exports byte for
  byte the same as before.
- The import probe showed `idle` (3 s) and `run` (0.6 s) both looping, under an
  `AnimationPlayer` beside the joint tree. A zoomed windowed run showed the idle and the run cycle
  facing the walk direction.

## Open questions (answered)

1. **Suit color:** an orange work suit (proposed: it stands out on grass, sand, and rock and reads
   as "colonist"), or something else, such as the Depot's red or a blue?
2. **Head:** bare head with a simple face (two dark eyes, proposed), or a helmet or hard hat with a
   visor, which reads more "planet colony" and hides the need for a face?
3. **Clips:** idle and run only (proposed), or also a harvest clip (a swing while E is held)? A
   harvest clip needs a small `PlayerView` change to play it while harvesting, so it could be its
   own changeset.
4. **Old files:** delete the FBX character and its texture (proposed; git keeps them), or keep
   them in the repo unused?
