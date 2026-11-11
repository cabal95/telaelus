# Animated character

The player loads `character.tscn` from this folder. `PlayerView` finds its `AnimationPlayer` and
plays clips whose names contain "idle" and "walk"/"run" (case-insensitive). All model-specific
wiring (skeleton, animations, skin) lives in the scene, so swapping the character is an asset
change, not a code change. If `character.tscn` is absent, the player falls back to a capsule.

## Source files

- `character.fbx` - the rigged model (`Root/Skeleton3D/characterMedium`).
- `character-idle.fbx`, `character-run.fbx` - animation clips on the same skeleton.
- `character-male.png` - the skin texture.

## character.tscn

`character.tscn` was assembled from the files above: an instance of `character.fbx`, an
`AnimationPlayer` holding the merged `idle` and `run` clips, and a `StandardMaterial3D` material
override carrying the skin. Open and edit it in the Godot editor like any scene.

To rebuild it by hand (for example after swapping the model): right-click `character.fbx` ->
New Inherited Scene, add an `AnimationPlayer`, load the animation FBXs (imported with
Import As = Animation Library) into it, set the mesh's surface material override to a
`StandardMaterial3D` whose albedo texture is the skin, and save as `character.tscn`.

## Scale

The model uses the FBX importer's centimeter conversion (`Root` scale 100). Tune the character's
overall size on the scene root or via the FBX import scale; `PlayerView` does not rescale it.
