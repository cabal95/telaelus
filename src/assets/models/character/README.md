# Animated character (place your character set here)

The player renders an imported character scene when `character.tscn` exists in this folder, and
falls back to a capsule otherwise, so the build stays green until the asset is ready.

## What the code expects

`src/assets/models/character/character.tscn` whose tree contains:

- the skinned mesh (a `Skeleton3D` with a `MeshInstance3D`), and
- an `AnimationPlayer` with at least an idle and a walk clip. `PlayerView` picks clips whose
  names contain "idle" and "walk"/"run" (case-insensitive), so exact names do not matter.

## Assembling it from an FBX model + FBX animations + PNG (Godot editor)

1. Copy the files into this folder: the model `.fbx`, each animation `.fbx`, and the skin `.png`.
2. Open the editor (`--editor --path src`). Godot 4.7 imports FBX natively (via ufbx). If the
   character comes in huge or tiny, select the model `.fbx` in the FileSystem dock, open the
   Import dock, and set the import scale (FBX is often in centimeters, so `0.01` is common), then
   Reimport.
3. For each animation `.fbx`: select it, in the Import dock set **Import As = Animation Library**,
   then Reimport.
4. Open the model `.fbx` as a scene (or instance it in a new scene). Add an `AnimationPlayer` if
   the model has none. In its Animation panel, use the **Animation Libraries** list to load each
   imported animation library.
5. If the skin did not come through, assign the `.png` as the mesh material's albedo texture.
6. Save the scene as `character.tscn` in this folder, then run the game.

## If the animations do not match the model (e.g., Mixamo)

The animation and model skeletons must line up. For Mixamo-style humanoid rigs, use Godot's
retargeting: on both the model and animation imports, set the skeleton to **Retarget** with a
BoneMap using the `SkeletonProfileHumanoid` profile. See Godot's "Retargeting 3D Skeletons" docs.

## Scale reference

Player is about 1.4 world units tall (1 tile is about 2 m). Match the character to roughly that
height; `PlayerView` does not auto-scale the character the way node models are scaled, so set the
size via the FBX import scale.
