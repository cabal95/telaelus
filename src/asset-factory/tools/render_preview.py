"""Blender script: render the 2 x 2 preview contact sheet for an OBJ (or GLB) with no window.
Same views as the Godot preview: game camera angle, two more yaws, and a top-down view.

Run: blender -b --python render_preview.py -- <in.obj|in.glb> <out.png> [ortho_size]
"""
import math
import sys

import bpy
import numpy as np
from mathutils import Vector

CELL = 400
BG_SRGB = (0.33, 0.47, 0.30)  # roughly the game's grass


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin(rgb):
    return tuple(srgb_to_linear(c) for c in rgb)


argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
ortho_override = float(argv[2]) if len(argv) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# --- model (Blender is Z up; the OBJ/GLB importers map the file's Y up to it)
if src.lower().endswith(".glb"):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    bpy.ops.wm.obj_import(filepath=src, forward_axis="NEGATIVE_Z", up_axis="Y")
    # MTL colors are authored as sRGB; convert to linear like the GLB converter does.
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                for name in ("Base Color", "Emission Color"):
                    sock = node.inputs.get(name)
                    if sock is not None and not sock.is_linked:
                        r, g, b, a = sock.default_value
                        sock.default_value = (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), a)
                node.inputs["Roughness"].default_value = 0.9
                node.inputs["Specular IOR Level"].default_value = 0.0

meshes = [o for o in scene.objects if o.type == "MESH"]
for o in meshes:
    for poly in o.data.polygons:
        poly.use_smooth = False

corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
center = (lo + hi) / 2
size = hi - lo
extent = ortho_override or (max(size.x, size.y, size.z) * 1.8 + 0.5)

# --- ground plane so shadows read (slightly below zero, like the Godot preview)
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.002))
ground = bpy.context.object
gmat = bpy.data.materials.new("ground")
gmat.use_nodes = True
gnode = gmat.node_tree.nodes["Principled BSDF"]
gnode.inputs["Base Color"].default_value = (*lin(BG_SRGB), 1)
gnode.inputs["Roughness"].default_value = 1.0
gnode.inputs["Specular IOR Level"].default_value = 0.0
ground.data.materials.append(gmat)

# --- world (ambient) and sun, matching the game's light direction (Godot rotation -50, -40, 0)
world = bpy.data.worlds.new("w")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (*lin((0.6, 0.6, 0.65)), 1)
bg.inputs["Strength"].default_value = 0.5
scene.world = world

sun_data = bpy.data.lights.new("sun", "SUN")
sun_data.energy = 3.0
sun_data.angle = math.radians(3)
sun = bpy.data.objects.new("sun", sun_data)
scene.collection.objects.link(sun)
d_obj = Vector((0.413, -0.766, -0.492))          # light travel direction in game (Y up) coordinates
d = Vector((d_obj.x, -d_obj.z, d_obj.y))          # to Blender (Z up)
sun.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

# --- camera
cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = extent
cam_data.clip_end = 200
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

# --- render settings
scene.render.resolution_x = CELL
scene.render.resolution_y = CELL
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.display_settings.display_device = "sRGB"
try:
    scene.render.engine = "BLENDER_EEVEE"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE_NEXT"

views = [(45.0, 48.0), (135.0, 48.0), (225.0, 48.0), (45.0, 89.0)]  # (yaw, pitch above horizon)
sheet = np.zeros((CELL * 2, CELL * 2, 4), dtype=np.float32)
tmp = dst + ".tmp.png"
for i, (yaw, pitch) in enumerate(views):
    yr, pr = math.radians(yaw), math.radians(pitch)
    off_obj = Vector((30 * math.cos(pr) * math.sin(yr), 30 * math.sin(pr), 30 * math.cos(pr) * math.cos(yr)))
    off = Vector((off_obj.x, -off_obj.z, off_obj.y))
    cam.location = center + off
    cam.rotation_euler = (-off).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(tmp, check_existing=False)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)
    bpy.data.images.remove(img)
    col, row = i % 2, i // 2
    y0 = (1 - row) * CELL  # image rows start at the bottom
    sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px

out = bpy.data.images.new("sheet", CELL * 2, CELL * 2, alpha=True)
out.pixels = sheet.flatten().tolist()
out.filepath_raw = dst
out.file_format = "PNG"
out.save()
import os
os.remove(tmp)
print("saved", dst)
