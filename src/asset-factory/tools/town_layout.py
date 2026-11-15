"""Write the town scene's starting layout: src/Telaelus/scenes/Town.tscn.

The town is the loading screen's image and the main menu's backdrop: a small settlement built from
the shipped models (instanced, so it stays current when they change). This script places a
starting layout once; after that the scene is meant to be arranged by hand in the Godot editor,
and running this again overwrites those changes.

Run (no asset-factory environment needed):
    python src/asset-factory/tools/town_layout.py

Coordinates are world units (one map cell is 4 x 4); a building is placed at its footprint
center, a road or bridge piece at its 2 x 2 tile's center. The camera looks down from +X +Z as
the game's does.
"""
import math
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
OUT = REPO / "src" / "Telaelus" / "scenes" / "Town.tscn"

# The town sits in map cells 0..9 by 0..7 (40 x 32 units); the ground runs well past it so the
# camera's view never reaches its edge. The river runs North-South down column 6.
GROUND_X = range(-8, 18)
GROUND_Z = range(-8, 16)
RIVER_X = 6

resources = {}  # res path -> id
nodes = []


def res(path):
    if path not in resources:
        resources[path] = f"m{len(resources) + 1}"
    return resources[path]


def model(name, path, x, z, yaw_deg=0.0, y=0.0, parent="."):
    nodes.append(f'[node name="{name}" parent="{parent}" instance=ExtResource("{res(path)}")]\n'
                 f"position = Vector3({x:g}, {y:g}, {z:g})\n"
                 + (f"rotation = Vector3(0, {math.radians(yaw_deg):.6f}, 0)\n" if yaw_deg else ""))


def group(name, parent="."):
    nodes.append(f'[node name="{name}" type="Node3D" parent="{parent}"]\n')


def glb(folder, name):
    return f"res://assets/models/{folder}/{name}.glb"


def build():
    # Ground: grass cells in three variants, turned by a fixed pattern; the river down one column.
    group("Ground")
    for mx in GROUND_X:
        for mz in GROUND_Z:
            cx, cz = mx * 4 + 2, mz * 4 + 2
            if mx == RIVER_X:
                model(f"River_{mx}_{mz}", glb("terrain", "river_straight_grass"), cx, cz, parent="Ground")
            else:
                h = (mx * 73856093) ^ (mz * 19349663)
                variant = "abc"[h % 3]
                model(f"Grass_{mx}_{mz}", glb("terrain", f"ground_grass_{variant}"), cx, cz, (h >> 3) % 4 * 90,
                      parent="Ground")

    # Buildings, fronts toward the camera (South) where it reads better.
    group("Buildings")
    for name, file, x, z, yaw in [
        ("HQ", "hq", 6, 11, 180),
        ("Smelter", "smelter", 13.5, 8.5, 180),
        ("Glassworks", "glassworks", 18.5, 9, 180),
        ("BurnerGenerator", "burner_generator", 18, 15, 180),
        ("Depot", "depot", 13, 16, 180),
        ("WaterWheel", "water_wheel", 26, 10, 0),
        ("LumberCamp", "lumber_camp", 33, 15, 180),
        ("Silo", "silo", 36, 15.5, 180),
        ("ResearchLab", "research_lab", 33, 26.5, 0),
        ("Warehouse", "warehouse", 37.5, 26.5, 0),
    ]:
        model(name, glb("buildings", file), x, z, yaw, parent="Buildings")

    # Fields: a stand of trees north-east, stone and ore south-west.
    group("Fields")
    for name, file, x, z in [
        ("Trees1", "tree_broadleaf_a", 34, 6), ("Trees2", "tree_conifer_a", 38, 6),
        ("Trees3", "tree_broadleaf_b", 38, 10), ("Trees4", "tree_conifer_b", 30, 2),
        ("Trees5", "tree_broadleaf_c", 34, 2), ("Stone", "stone_a", 6, 26),
        ("IronOre", "iron_ore_a", 2, 22), ("CopperOre", "copper_ore_b", 10, 29),
    ]:
        model(name, glb("nature", file), x, z, parent="Fields")

    # The road along z 20..21 from x 10 to 37, crossing the river on a two-tile bridge at x 24..27.
    group("Roads")
    tiles = [x for x in range(10, 38, 2) if not 24 <= x < 28]
    for x in tiles:
        west = x - 2 >= 10
        east = x + 2 < 38
        if not west:
            piece, yaw = "road_end", 270      # open East
        elif not east:
            piece, yaw = "road_end", 90       # open West
        else:
            piece, yaw = "road_straight", 90  # East-West
        model(f"Road_{x}", glb("infrastructure", piece), x + 1, 21, yaw, parent="Roads")
    model("BridgeWest", glb("infrastructure", "bridge_ramp"), 25, 21, 90, parent="Roads")
    model("BridgeEast", glb("infrastructure", "bridge_ramp"), 27, 21, 270, parent="Roads")
    model("BridgePier", glb("infrastructure", "bridge_pier"), 27, 21, 90, parent="Roads")

    # The truck's loop: out along the north lane, back along the south lane.
    points = [(11, 20.55), (36.6, 20.55), (37.3, 21), (36.6, 21.45), (11, 21.45), (10.3, 21), (11, 20.55)]
    data = ", ".join(f"0, 0, 0, 0, 0, 0, {x}, 0, {z}" for x, z in points)
    tilts = ", ".join("0" for _ in points)
    route = (f'[sub_resource type="Curve3D" id="TruckRoute"]\n'
             f'_data = {{\n"points": PackedVector3Array({data}),\n"tilts": PackedFloat32Array({tilts})\n}}\n'
             f"point_count = {len(points)}\n")
    nodes.append('[node name="TruckRoute" type="Path3D" parent="."]\ncurve = SubResource("TruckRoute")\n')
    nodes.append('[node name="Follow" type="PathFollow3D" parent="TruckRoute"]\nrotation_mode = 1\nloop = true\n')
    model("Truck", glb("units", "truck"), 0, 0, parent="TruckRoute/Follow")

    # The character by the HQ.
    res("res://assets/models/character/character.tscn")
    nodes.append(f'[node name="Character" parent="." instance=ExtResource("{resources["res://assets/models/character/character.tscn"]}")]\n'
                 "position = Vector3(10.5, 0, 17.5)\n"
                 f"rotation = Vector3(0, {math.radians(200):.6f}, 0)\n")

    # The camera at the game's angle (yaw 45, pitch -48), orthographic, looking at the town's middle.
    target = (20.0, 0.0, 16.0)
    pitch, yaw, dist = math.radians(48), math.radians(45), 60.0
    cam = (target[0] + math.sin(yaw) * math.cos(pitch) * dist,
           target[1] + math.sin(pitch) * dist,
           target[2] + math.cos(yaw) * math.cos(pitch) * dist)
    nodes.append('[node name="Camera" type="Camera3D" parent="."]\n'
                 f"position = Vector3({cam[0]:.4f}, {cam[1]:.4f}, {cam[2]:.4f})\n"
                 f"rotation = Vector3({-pitch:.6f}, {yaw:.6f}, 0)\n"
                 "projection = 1\nsize = 30.0\nfar = 200.0\ncurrent = true\n")
    # The sun at the art style's angle, as in the game (TownLife sets its color by the time of day).
    nodes.append('[node name="Sun" type="DirectionalLight3D" parent="."]\n'
                 f"rotation = Vector3({math.radians(-50):.6f}, {math.radians(-40):.6f}, 0)\n"
                 "shadow_enabled = true\ndirectional_shadow_max_distance = 120.0\n")
    nodes.append('[node name="Environment" type="WorldEnvironment" parent="."]\nenvironment = SubResource("Environment")\n')
    return route


def write():
    route = build()
    env = ('[sub_resource type="Environment" id="Environment"]\n'
           "background_mode = 1\nbackground_color = Color(0.1, 0.12, 0.11, 1)\n"
           "ambient_light_source = 1\ntonemap_mode = 0\n")
    lines = ["[gd_scene format=3]\n",
             '[ext_resource type="Script" path="res://Presentation/TownLife.cs" id="script"]\n']
    for path, rid in resources.items():
        kind = "PackedScene"
        lines.append(f'[ext_resource type="{kind}" path="{path}" id="{rid}"]\n')
    lines.append(route)
    lines.append(env)
    lines.append('[node name="Town" type="Node3D"]\nscript = ExtResource("script")\n')
    lines.extend(nodes)
    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"wrote {OUT} ({len(nodes)} nodes, {len(resources)} models)")


if __name__ == "__main__":
    write()
