"""Resource item models: one small object per resource type, for the UI's resource icons.

Each is a single chunky object about 1 unit across, built to read as a silhouette at icon size;
the game renders it once at startup into an icon (ResourceIcons). Origin at the base center,
resting on y = 0. See docs/blueprints/art-style.md and src/Game/assets/models/items/README.md.
"""
import math
import random

from mesh import Materials, Mesh, box, centroid, cylinder, quad, spike
from resource_gen import add_blob, blob, vein_noise


def logs(_rnd):
    """Three logs stacked two and one, with light end rings."""
    m, mat = Mesh(), Materials()
    for z, y in ((-0.19, 0.18), (0.19, 0.18), (0.0, 0.5)):
        cylinder(m, mat("bark"), (-0.45, y, z), (0.45, y, z), 0.18, segs=7, mat_cap=mat("log_end"))
    return m, mat.mats


def stone_block(rnd):
    """A rough gray block: a squared-off lump with a few chipped facets."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.3, 0.0)
    tris = blob(rnd, centre, (0.46, 0.3, 0.38), 1, 0.07)
    add_blob(m, tris, centre, lambda t: mat("boulder", rnd.choice((1.0, 0.94, 1.05)))
             if centroid(*t)[1] > 0.2 else mat("boulder_dark"))
    return m, mat.mats


def ore_lump(rnd, rock, rock_dark, vein, vein_dark):
    """One rock lump with ore veins across its upper faces."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.32, 0.0)
    tris = blob(rnd, centre, (0.45, 0.34, 0.42), 1, 0.2)
    phase = rnd.uniform(0, 6)

    def pick(t):
        c = centroid(*t)
        n = vein_noise(c, phase)
        if c[1] > 0.2 and n > 0.2:
            return mat(vein) if rnd.random() < 0.65 else mat(vein_dark)
        return mat(rock) if rnd.random() < 0.6 else mat(rock_dark)
    add_blob(m, tris, centre, pick)
    return m, mat.mats


def iron_ore(rnd):
    return ore_lump(rnd, "ore_rock", "ore_rock_dark", "iron_rust", "iron_dark")


def copper_ore(rnd):
    return ore_lump(rnd, "copper_rock", "ore_rock_dark", "copper_green", "copper_orange")


def coal_lump(rnd):
    """An angular black lump; its upper facets catch the light so it reads on a dark panel."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.3, 0.0)
    tris = blob(rnd, centre, (0.42, 0.32, 0.4), 0, 0.3)
    add_blob(m, tris, centre, lambda t: mat("coal_facet") if centroid(*t)[1] > 0.38 and rnd.random() < 0.6
             else mat("coal_sheen") if rnd.random() < 0.4 else mat("coal"))
    return m, mat.mats


def barrel(_rnd):
    """A dark oil barrel with a brown band and a lighter lid."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("barrel"), (0, 0, 0), (0, 0.85, 0), 0.32, segs=10, mat_cap=mat("barrel_lid"))
    for y in (0.24, 0.6):
        cylinder(m, mat("barrel_band"), (0, y - 0.04, 0), (0, y + 0.04, 0), 0.335, segs=10)
    return m, mat.mats


def sand_heap(rnd):
    """A low tan heap."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.0, 0.0)
    tris = blob(rnd, centre, (0.5, 0.42, 0.46), 1, 0.08)
    add_blob(m, tris, centre, lambda t: mat("sand", rnd.choice((1.0, 0.93, 1.06))))
    return m, mat.mats


def droplet(_rnd):
    """A water drop: a low-poly lathe of the classic teardrop, round below and drawn up to a point.

    The profile is r = sin(t) * sin(t / 2)^2 for t from 0 (the tip) to pi (the bottom), in a few
    rings so the facets stay large. One water color lets the light shade it smoothly, with a
    small glint on the lit shoulder. The drop leans its tip 25 degrees away from the game camera
    (which looks down from +X +Z), so its icon shows the teardrop's profile, not its top.
    """
    m, mat = Mesh(), Materials()
    segs, rings = 10, 7
    height, width = 1.2, 0.8
    lean = math.radians(-25)

    def leaned(p):
        # Rotate about the horizontal axis across the camera's view, tipping the top away from +X +Z.
        x, y, z = p
        along = (x + z) / math.sqrt(2)
        across = (x - z) / math.sqrt(2)
        along, y = along * math.cos(lean) + y * math.sin(lean), -along * math.sin(lean) + y * math.cos(lean)
        return ((along + across) / math.sqrt(2), y, (along - across) / math.sqrt(2))

    rows = []
    for k in range(rings + 1):
        t = math.pi * k / rings
        r = math.sin(t) * math.sin(t / 2) ** 2 * width
        y = height * (0.5 + 0.5 * math.cos(t))
        rows.append([leaned((math.cos(2 * math.pi * i / segs) * r, y, math.sin(2 * math.pi * i / segs) * r))
                     for i in range(segs)])
    lowest = min(p[1] for row in rows for p in row)
    rows = [[(x, y - lowest, z) for x, y, z in row] for row in rows]
    centre = centroid(*[p for row in rows for p in row])
    for k in range(rings):
        upper, lower = rows[k], rows[k + 1]
        for i in range(segs):
            j = (i + 1) % segs
            for tri in ((upper[i], lower[i], lower[j]), (upper[i], lower[j], upper[j])):
                if len({tri[0], tri[1], tri[2]}) < 3:
                    continue
                # A glint on two facets of the upper shoulder facing the camera's left.
                glint = k == 2 and i in (2, 3)
                m.tri(mat("water_light") if glint else mat("water"), *tri, centre=centre)
    return m, mat.mats


def ingot(m, mat, cx, y, cz, yaw):
    """A trapezoid bar: wider at the base, flat on top."""
    bw, bd, tw, td, h = 0.36, 0.17, 0.29, 0.11, 0.2
    c, s = math.cos(yaw), math.sin(yaw)

    def p(x, yy, z):
        return (cx + x * c - z * s, y + yy, cz + x * s + z * c)
    b = [p(-bw, 0, -bd), p(bw, 0, -bd), p(bw, 0, bd), p(-bw, 0, bd)]
    t = [p(-tw, h, -td), p(tw, h, -td), p(tw, h, td), p(-tw, h, td)]
    centre = p(0, h * 0.5, 0)
    quad(m, mat("ingot_light"), t[0], t[1], t[2], t[3], (0, 1, 0))
    for i in range(4):
        j = (i + 1) % 4
        mid = centroid(b[i], b[j], t[j], t[i])
        quad(m, mat("ingot"), b[i], b[j], t[j], t[i], (mid[0] - centre[0], 0, mid[2] - centre[2]))


def ingots(_rnd):
    """Two gray ingots, one across the other."""
    m, mat = Mesh(), Materials()
    ingot(m, mat, 0.0, 0.0, 0.0, 0.0)
    ingot(m, mat, 0.0, 0.2, 0.0, math.pi / 2)
    return m, mat.mats


def canister(_rnd):
    """A white fuel canister with an orange band and a dark cap."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("canister"), (0, 0, 0), (0, 0.78, 0), 0.28, segs=10)
    cylinder(m, mat("fuel_orange"), (0, 0.3, 0), (0, 0.52, 0), 0.29, segs=10)
    cylinder(m, mat("metal_dark"), (0, 0.78, 0), (0, 0.92, 0), 0.1, segs=8)
    return m, mat.mats


# Item file name -> (builder, seed). File names match ResourceIcons in the game.
ITEMS = {
    "item_wood": (logs, 1),
    "item_stone": (stone_block, 2),
    "item_iron_ore": (iron_ore, 3),
    "item_copper_ore": (copper_ore, 4),
    "item_coal": (coal_lump, 5),
    "item_crude_oil": (barrel, 6),
    "item_sand": (sand_heap, 7),
    "item_water": (droplet, 8),
    "item_iron": (ingots, 9),
    "item_rocket_fuel": (canister, 10),
}


def build_all():
    for name, (fn, seed) in ITEMS.items():
        m, mats = fn(random.Random(seed))
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
