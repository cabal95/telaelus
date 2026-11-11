"""Blender script: convert an OBJ (with MTL) to a flat-shaded GLB, Y up.
Run: blender -b --python obj_to_glb.py -- <in.obj> <out.glb>
"""
import sys
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis="NEGATIVE_Z", up_axis="Y")

for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = False  # keep the faceted low-poly look

def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# The MTL colors are authored as sRGB values; Blender and glTF treat them as linear, so convert.
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        for name in ("Base Color", "Emission Color"):
            sock = node.inputs.get(name)
            if sock is not None and not sock.is_linked:
                r, g, b, a = sock.default_value
                sock.default_value = (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), a)

bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", export_yup=True, export_apply=True)
print("exported", dst)
