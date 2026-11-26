"""Resource item models: one small object per resource type, for the UI's resource icons.

Each is a single chunky object about 1 unit across, built to read as a silhouette at icon size;
the game renders it once at startup into an icon (ResourceIcons). Origin at the base center,
resting on y = 0. See docs/blueprints/art-style.md and src/Telaelus/assets/models/items/README.md.
"""
import math
import random

from mesh import Materials, Mesh, add, centroid, cross, cylinder, mul, norm, quad, spike, sub
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


# ---------------------------------------------------------------- shared shapes for the catalog
def frustum(m, mat, a, b, r0, r1, segs=10, mat_cap=None, caps=True):
    """A truncated cone from point a (radius r0) to point b (radius r1), on a vertical or any axis."""
    axis = norm(sub(b, a))
    helper = (1, 0, 0) if abs(axis[0]) < 0.9 else (0, 0, 1)
    u = norm(cross(axis, helper))
    v = cross(axis, u)
    ring = [add(mul(u, math.cos(2 * math.pi * (i + 0.5) / segs)), mul(v, math.sin(2 * math.pi * (i + 0.5) / segs)))
            for i in range(segs)]
    centre = mul(add(a, b), 0.5)
    for i in range(segs):
        j = (i + 1) % segs
        p0, p1 = add(a, mul(ring[i], r0)), add(a, mul(ring[j], r0))
        q0, q1 = add(b, mul(ring[i], r1)), add(b, mul(ring[j], r1))
        m.tri(mat, p0, p1, q1, centre=centre)
        m.tri(mat, p0, q1, q0, centre=centre)
        if caps and r0 > 0:
            m.tri(mat_cap or mat, a, p0, p1, outward=mul(axis, -1))
        if caps and r1 > 0:
            m.tri(mat_cap or mat, b, q0, q1, outward=axis)


def obox(m, mat, cx, cz, y0, y1, hw, hd, yaw, mat_top=None):
    """A box turned by yaw about Y, centred on (cx, cz), from y0 to y1, half width hw and half depth hd."""
    c, s = math.cos(yaw), math.sin(yaw)

    def p(x, y, z):
        return (cx + x * c - z * s, y, cz + x * s + z * c)
    b = [p(-hw, y0, -hd), p(hw, y0, -hd), p(hw, y0, hd), p(-hw, y0, hd)]
    t = [p(-hw, y1, -hd), p(hw, y1, -hd), p(hw, y1, hd), p(-hw, y1, hd)]
    quad(m, mat_top or mat, t[0], t[1], t[2], t[3], (0, 1, 0))
    centre = (cx, (y0 + y1) / 2, cz)
    for i in range(4):
        j = (i + 1) % 4
        mid = centroid(b[i], b[j], t[j], t[i])
        quad(m, mat, b[i], b[j], t[j], t[i], sub(mid, centre))


def heap(rnd, colour, colour_dark=None, radii=(0.5, 0.42, 0.46)):
    """A low heap of grains or powder, like Sand's."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.0, 0.0)
    tris = blob(rnd, centre, radii, 1, 0.08)
    add_blob(m, tris, centre, lambda t: mat(colour_dark) if colour_dark and rnd.random() < 0.3
             else mat(colour, rnd.choice((1.0, 0.93, 1.06))))
    return m, mat.mats


def block(rnd, colour, colour_dark):
    """A rough block like Stone's, in its own colors."""
    m, mat = Mesh(), Materials()
    centre = (0.0, 0.3, 0.0)
    tris = blob(rnd, centre, (0.46, 0.3, 0.38), 1, 0.07)
    add_blob(m, tris, centre, lambda t: mat(colour, rnd.choice((1.0, 0.94, 1.05)))
             if centroid(*t)[1] > 0.2 else mat(colour_dark))
    return m, mat.mats


def gems(rnd, colour, colour_light, count):
    """A cluster of six-sided crystals pointing up and out from a common base."""
    m, mat = Mesh(), Materials()
    for k in range(count):
        angle = 2 * math.pi * k / count + rnd.uniform(-0.3, 0.3)
        lean = 0.0 if k == 0 else rnd.uniform(0.35, 0.6)
        length = 0.75 if k == 0 else rnd.uniform(0.45, 0.6)
        r = 0.14 if k == 0 else 0.1
        base = (math.cos(angle) * 0.08 * (k > 0), 0.02, math.sin(angle) * 0.08 * (k > 0))
        direction = norm((math.cos(angle) * math.sin(lean), math.cos(lean), math.sin(angle) * math.sin(lean)))
        top = add(base, mul(direction, length))
        cylinder(m, mat(colour), base, top, r, segs=6, caps=False)
        spike(m, mat(colour_light), top, add(top, mul(direction, r * 1.6)), r, segs=6)
    return m, mat.mats


def bars(_rnd, colour, colour_light):
    """Two ingots, one across the other, like Iron's, in a metal's colors."""
    m, mat = Mesh(), Materials()
    tinted = lambda key: mat(colour_light) if key == "ingot_light" else mat(colour)
    ingot(m, tinted, 0.0, 0.0, 0.0, 0.0)
    ingot(m, tinted, 0.0, 0.2, 0.0, math.pi / 2)
    return m, mat.mats


def bottle(_rnd, body, band, cap, tall=False):
    """A gas or liquid canister: a body with a colored band and a cap; tall for gas bottles."""
    m, mat = Mesh(), Materials()
    height, radius = (0.95, 0.2) if tall else (0.78, 0.28)
    cylinder(m, mat(body), (0, 0, 0), (0, height, 0), radius, segs=10)
    cylinder(m, mat(band), (0, height * 0.55, 0), (0, height * 0.75, 0), radius + 0.01, segs=10)
    if tall:
        frustum(m, mat(body), (0, height, 0), (0, height + 0.12, 0), radius, radius * 0.5, segs=10)
        cylinder(m, mat(cap), (0, height + 0.12, 0), (0, height + 0.24, 0), 0.07, segs=8)
    else:
        cylinder(m, mat(cap), (0, height, 0), (0, height + 0.14, 0), 0.1, segs=8)
    return m, mat.mats


def sack(_rnd, colour, band):
    """A filled sack: a slumped box with a band and a tied neck."""
    m, mat = Mesh(), Materials()
    frustum(m, mat(colour), (0, 0, 0), (0, 0.55, 0), 0.38, 0.3, segs=8)
    cylinder(m, mat(band), (0, 0.26, 0), (0, 0.38, 0), 0.355, segs=8)
    frustum(m, mat(colour), (0, 0.55, 0), (0, 0.72, 0), 0.3, 0.08, segs=8)
    cylinder(m, mat(colour, 0.85), (0, 0.72, 0), (0, 0.8, 0), 0.07, segs=6)
    return m, mat.mats


def plates(_rnd, colour, colour_light, count, half, thick):
    """A stack of square plates, each turned a little."""
    m, mat = Mesh(), Materials()
    for k in range(count):
        y = k * (thick + 0.015)
        obox(m, mat(colour), 0, 0, y, y + thick, half, half, 0.12 * k, mat_top=mat(colour_light))
    return m, mat.mats


def spool(_rnd, wire, wire_light):
    """A spool: two end discs and a winding of wire between them."""
    m, mat = Mesh(), Materials()
    for x in (-0.36, 0.28):
        cylinder(m, mat("wood"), (x, 0.4, 0), (x + 0.08, 0.4, 0), 0.4, segs=12)
    cylinder(m, mat(wire), (-0.28, 0.4, 0), (0.28, 0.4, 0), 0.3, segs=12)
    for k in range(4):
        x = -0.24 + k * 0.15
        cylinder(m, mat(wire_light), (x, 0.4, 0), (x + 0.05, 0.4, 0), 0.305, segs=12, caps=False)
    return m, mat.mats


def roll(_rnd, colour, colour_light):
    """A rolled sheet lying on its side."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat(colour), (-0.42, 0.26, 0), (0.42, 0.26, 0), 0.26, segs=12, mat_cap=mat(colour_light))
    obox(m, mat(colour), 0, 0.3, 0.0, 0.03, 0.42, 0.18, 0.0)
    return m, mat.mats


def tire(_rnd):
    """Two rubber tires stacked flat, their treads lighter than their sidewalls."""
    m, mat = Mesh(), Materials()
    for k, (x, z) in enumerate(((0.0, 0.0), (0.04, -0.03))):
        y = k * 0.26
        cylinder(m, mat("rubber_light"), (x, y, z), (x, y + 0.24, z), 0.4, segs=14, mat_cap=mat("rubber"))
        cylinder(m, mat("metal"), (x, y + 0.24, z), (x, y + 0.25, z), 0.16, segs=10)
    return m, mat.mats


# ---------------------------------------------------------------- catalog: raw
def bauxite(rnd):
    return ore_lump(rnd, "bauxite_rock", "bauxite_dark", "bauxite_pale", "bauxite_rock")


def titanium_ore(rnd):
    return ore_lump(rnd, "titanium_rock", "ore_rock_dark", "titanium_vein", "metal")


def rare_earths(rnd):
    return ore_lump(rnd, "rare_rock", "ore_rock", "rare_pink", "rare_violet")


def uranium_ore(rnd):
    return ore_lump(rnd, "uranium_rock", "ore_rock_dark", "uranium_glow", "uranium_glow")


def quartz(rnd):
    return gems(rnd, "quartz_shade", "quartz", 4)


def crystals(rnd):
    return gems(rnd, "crystal", "crystal_light", 5)


def limestone(rnd):
    return block(rnd, "limestone", "limestone_dark")


def sulfur(rnd):
    return heap(rnd, "sulfur")


def lithium(rnd):
    return heap(rnd, "lithium", "rare_pink", radii=(0.46, 0.36, 0.42))


def charcoal(_rnd):
    """Three charred logs stacked two and one, like Wood's."""
    m, mat = Mesh(), Materials()
    for z, y in ((-0.19, 0.18), (0.19, 0.18), (0.0, 0.5)):
        cylinder(m, mat("charcoal"), (-0.42, y, z), (0.42, y, z), 0.17, segs=6, mat_cap=mat("charcoal_end"))
    return m, mat.mats


# ---------------------------------------------------------------- catalog: tier 1
def lumber(_rnd):
    """A crossed stack of sawn planks."""
    m, mat = Mesh(), Materials()
    for k in range(4):
        y = k * 0.13
        yaw = 0.0 if k % 2 == 0 else math.pi / 2
        for offset in (-0.16, 0.16):
            cx, cz = (0.0, offset) if k % 2 == 0 else (offset, 0.0)
            obox(m, mat("plank_dark"), cx, cz, y, y + 0.11, 0.46, 0.12, yaw, mat_top=mat("plank"))
    return m, mat.mats


def concrete(_rnd):
    """Two cast blocks, one on the other."""
    m, mat = Mesh(), Materials()
    obox(m, mat("concrete_dark"), 0, 0, 0.0, 0.32, 0.44, 0.3, 0.0, mat_top=mat("concrete"))
    obox(m, mat("concrete_dark"), 0.05, 0.02, 0.32, 0.62, 0.3, 0.24, 0.35, mat_top=mat("concrete"))
    return m, mat.mats


def glass(_rnd):
    """Three panes standing in a rack."""
    m, mat = Mesh(), Materials()
    obox(m, mat("wood_dark"), 0, 0, 0.0, 0.08, 0.42, 0.24, 0.0)
    for k in range(3):
        z = -0.14 + k * 0.14
        obox(m, mat("glass"), 0, z, 0.08, 0.9, 0.38, 0.025, 0.0, mat_top=mat("glass_edge"))
    return m, mat.mats


def silicon(_rnd):
    """A grown silicon boule on its side."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("silicon"), (-0.36, 0.24, 0), (0.3, 0.24, 0), 0.24, segs=10, mat_cap=mat("silicon_light"))
    frustum(m, mat("silicon"), (0.3, 0.24, 0), (0.46, 0.24, 0), 0.24, 0.06, segs=10)
    return m, mat.mats


def enriched_uranium(_rnd):
    """Glowing fuel pellets in a squat lead pot."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("lead"), (0, 0, 0), (0, 0.4, 0), 0.38, segs=10, mat_cap=mat("lead", 0.8))
    for k in range(5):
        a = 2 * math.pi * k / 5
        x, z = math.cos(a) * 0.17, math.sin(a) * 0.17
        cylinder(m, mat("uranium_glow"), (x, 0.4, z), (x, 0.62, z), 0.07, segs=6)
    return m, mat.mats


# ---------------------------------------------------------------- catalog: tier 2
def steel_beam(_rnd):
    """An I-beam lying along X."""
    m, mat = Mesh(), Materials()
    obox(m, mat("steel"), 0, 0, 0.0, 0.07, 0.48, 0.24, 0.0, mat_top=mat("steel_light"))
    obox(m, mat("steel"), 0, 0, 0.07, 0.43, 0.48, 0.05, 0.0, mat_top=mat("steel_light"))
    obox(m, mat("steel"), 0, 0, 0.43, 0.5, 0.48, 0.24, 0.0, mat_top=mat("steel_light"))
    return m, mat.mats


def circuit(_rnd):
    """A green board with chips and gold contacts along one edge."""
    m, mat = Mesh(), Materials()
    obox(m, mat("board"), 0, 0, 0.0, 0.06, 0.46, 0.34, 0.0)
    for x, z, w, d in ((-0.2, -0.08, 0.12, 0.12), (0.14, -0.1, 0.16, 0.1), (0.0, 0.14, 0.1, 0.08)):
        obox(m, mat("chip"), x, z, 0.06, 0.13, w, d, 0.0)
    for k in range(6):
        obox(m, mat("gold"), -0.35 + k * 0.14, 0.3, 0.06, 0.075, 0.04, 0.03, 0.0)
    return m, mat.mats


def silicon_wafer(_rnd):
    """A thin disc with a flat edge, its face ruled into dies."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("wafer"), (0, 0, 0), (0, 0.05, 0), 0.46, segs=16, mat_cap=mat("solar_line"))
    for k in range(-2, 3):
        obox(m, mat("wafer"), k * 0.16, 0, 0.05, 0.06, 0.012, 0.36, 0.0)
        obox(m, mat("wafer"), 0, k * 0.16, 0.05, 0.06, 0.36, 0.012, 0.0)
    return m, mat.mats


def processor(_rnd):
    """A square black chip with a metal lid and gold pins on every side."""
    m, mat = Mesh(), Materials()
    obox(m, mat("chip"), 0, 0, 0.04, 0.16, 0.34, 0.34, 0.0)
    obox(m, mat("metal"), 0, 0, 0.16, 0.2, 0.22, 0.22, 0.0)
    for k in range(-2, 3):
        for side in (-1, 1):
            obox(m, mat("gold"), k * 0.12, side * 0.4, 0.0, 0.08, 0.03, 0.06, 0.0)
            obox(m, mat("gold"), side * 0.4, k * 0.12, 0.0, 0.08, 0.06, 0.03, 0.0)
    return m, mat.mats


def magnet(_rnd):
    """A red horseshoe magnet with silver poles."""
    m, mat = Mesh(), Materials()
    obox(m, mat("magnet_red"), 0, 0.22, 0.0, 0.24, 0.4, 0.13, 0.0)
    for x in (-0.28, 0.28):
        obox(m, mat("magnet_red"), x, -0.08, 0.0, 0.24, 0.12, 0.2, 0.0)
        obox(m, mat("metal"), x, -0.36, 0.0, 0.24, 0.12, 0.08, 0.0)
    return m, mat.mats


def electric_motor(_rnd):
    """A finned motor body on feet with its shaft out the front."""
    m, mat = Mesh(), Materials()
    obox(m, mat("metal_dark"), 0, 0, 0.0, 0.1, 0.36, 0.3, 0.0)
    cylinder(m, mat("band_blue"), (-0.34, 0.38, 0), (0.28, 0.38, 0), 0.28, segs=10, mat_cap=mat("metal"))
    for k in range(4):
        x = -0.26 + k * 0.16
        cylinder(m, mat("metal"), (x, 0.38, 0), (x + 0.03, 0.38, 0), 0.31, segs=10, caps=False)
    cylinder(m, mat("metal"), (0.28, 0.38, 0), (0.5, 0.38, 0), 0.05, segs=6)
    return m, mat.mats


def battery(_rnd):
    """A cell: a green body with a silver base and a terminal on top."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("metal"), (0, 0, 0), (0, 0.1, 0), 0.26, segs=12)
    cylinder(m, mat("band_green"), (0, 0.1, 0), (0, 0.8, 0), 0.26, segs=12, mat_cap=mat("metal"))
    cylinder(m, mat("metal"), (0, 0.8, 0), (0, 0.9, 0), 0.09, segs=8)
    return m, mat.mats


def solar_cell(_rnd):
    """A framed blue cell ruled into squares, propped up to face the light."""
    m, mat = Mesh(), Materials()
    obox(m, mat("metal"), 0, 0, 0.0, 0.06, 0.46, 0.4, 0.0)
    obox(m, mat("solar"), 0, 0, 0.06, 0.1, 0.42, 0.36, 0.0)
    for k in (-0.21, 0.0, 0.21):
        obox(m, mat("solar_line"), k, 0, 0.1, 0.11, 0.01, 0.36, 0.0)
        obox(m, mat("solar_line"), 0, k * 0.85, 0.1, 0.11, 0.42, 0.01, 0.0)
    return m, mat.mats


def machine_parts(_rnd):
    """A toothed gear lying flat with a smaller one on top."""
    m, mat = Mesh(), Materials()
    for (y, r, teeth) in ((0.0, 0.32, 10), (0.14, 0.18, 7)):
        cylinder(m, mat("metal"), (0, y, 0), (0, y + 0.12, 0), r, segs=teeth * 2, mat_cap=mat("ingot_light"))
        for k in range(teeth):
            a = 2 * math.pi * k / teeth
            obox(m, mat("metal"), math.cos(a) * (r + 0.05), math.sin(a) * (r + 0.05), y, y + 0.12, 0.05, 0.04, a)
    cylinder(m, mat("metal_dark"), (0, 0.26, 0), (0, 0.28, 0), 0.06, segs=8)
    return m, mat.mats


def aluminum_frame(_rnd):
    """An open cube of silver struts."""
    m, mat = Mesh(), Materials()
    h, s, t = 0.7, 0.36, 0.04
    for x in (-s, s):
        for z in (-s, s):
            obox(m, mat("aluminum"), x, z, 0.0, h, t, t, 0.0, mat_top=mat("aluminum_light"))
    for y in (0.0, h - 2 * t):
        for z in (-s, s):
            obox(m, mat("aluminum"), 0, z, y, y + 2 * t, s, t, 0.0, mat_top=mat("aluminum_light"))
        for x in (-s, s):
            obox(m, mat("aluminum"), x, 0, y, y + 2 * t, t, s, 0.0, mat_top=mat("aluminum_light"))
    return m, mat.mats


def armor_plate(_rnd):
    """A thick olive plate with bolts at its corners."""
    m, mat = Mesh(), Materials()
    obox(m, mat("armor", 0.8), 0, 0, 0.0, 0.18, 0.46, 0.38, 0.0, mat_top=mat("armor"))
    for x in (-0.36, 0.36):
        for z in (-0.28, 0.28):
            cylinder(m, mat("metal"), (x, 0.18, z), (x, 0.23, z), 0.05, segs=6)
    return m, mat.mats


# ---------------------------------------------------------------- catalog: tiers 3 and 4
def control_module(_rnd):
    """A sloped console with a glowing screen and a row of buttons."""
    m, mat = Mesh(), Materials()
    obox(m, mat("panel_dark"), 0, 0, 0.0, 0.42, 0.44, 0.3, 0.0, mat_top=mat("metal"))
    obox(m, mat("screen"), 0, 0.31, 0.12, 0.38, 0.32, 0.01, 0.0)
    for k in range(4):
        obox(m, mat(("band_red", "band_yellow", "led", "band_blue")[k]), -0.24 + k * 0.16, 0.2, 0.42, 0.46, 0.04, 0.04, 0.0)
    return m, mat.mats


def computer(_rnd):
    """A dark tower with a column of status lights."""
    m, mat = Mesh(), Materials()
    obox(m, mat("panel_dark"), 0, 0, 0.0, 0.92, 0.24, 0.36, 0.0, mat_top=mat("metal_dark"))
    for k in range(5):
        obox(m, mat("led" if k % 2 == 0 else "screen"), 0.25, -0.2, 0.2 + k * 0.13, 0.26 + k * 0.13, 0.01, 0.06, 0.0)
    return m, mat.mats


def thruster_assembly(_rnd):
    """A combustion chamber over a flared bell nozzle with a hot throat."""
    m, mat = Mesh(), Materials()
    frustum(m, mat("nozzle"), (0, 0.0, 0), (0, 0.42, 0), 0.4, 0.13, segs=12, mat_cap=mat("nozzle_hot"))
    cylinder(m, mat("metal"), (0, 0.42, 0), (0, 0.72, 0), 0.2, segs=10, mat_cap=mat("metal_dark"))
    for x in (-0.24, 0.24):
        cylinder(m, mat("copper"), (x, 0.5, 0), (x * 0.5, 0.86, 0), 0.04, segs=6)
    return m, mat.mats


def fuel_tank(_rnd):
    """A white capsule tank on its side, held by two straps."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("canister"), (-0.3, 0.3, 0), (0.3, 0.3, 0), 0.28, segs=12, caps=False)
    for side in (-1, 1):
        frustum(m, mat("canister"), (side * 0.3, 0.3, 0), (side * 0.46, 0.3, 0), 0.28, 0.12, segs=12)
    for x in (-0.16, 0.16):
        cylinder(m, mat("fuel_orange"), (x, 0.3, 0), (x + 0.06, 0.3, 0), 0.29, segs=12, caps=False)
    return m, mat.mats


def heat_shield(_rnd):
    """A shallow dome of dark tiles, tipped toward the camera."""
    m, mat = Mesh(), Materials()
    frustum(m, mat("carbon"), (0, 0.0, 0), (0, 0.18, 0), 0.48, 0.34, segs=14, mat_cap=mat("carbon_weave"))
    frustum(m, mat("carbon_weave"), (0, 0.18, 0), (0, 0.28, 0), 0.34, 0.12, segs=14)
    return m, mat.mats


def fuel_rod(_rnd):
    """A bundle of tall rods with glowing tips, banded together."""
    m, mat = Mesh(), Materials()
    for k in range(7):
        x, z = (0.0, 0.0) if k == 0 else (math.cos(k) * 0.14, math.sin(k) * 0.14)
        cylinder(m, mat("metal"), (x, 0, z), (x, 0.9, z), 0.06, segs=6, mat_cap=mat("uranium_glow"))
    for y in (0.2, 0.62):
        cylinder(m, mat("lead"), (0, y, 0), (0, y + 0.06, 0), 0.22, segs=10)
    return m, mat.mats


def life_support_unit(_rnd):
    """A cabinet with a round fan grille and a green tank on its side."""
    m, mat = Mesh(), Materials()
    obox(m, mat("hull_dark"), 0, 0, 0.0, 0.7, 0.32, 0.28, 0.0, mat_top=mat("hull"))
    cylinder(m, mat("metal_dark"), (0, 0.42, 0.28), (0, 0.42, 0.31), 0.18, segs=12)
    cylinder(m, mat("band_green"), (0.42, 0, 0), (0.42, 0.62, 0), 0.12, segs=10, mat_cap=mat("metal"))
    return m, mat.mats


def sensor_array(_rnd):
    """A dish on a mast, tipped up toward the sky."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("metal_dark"), (0, 0, 0), (0, 0.08, 0), 0.26, segs=10)
    cylinder(m, mat("metal"), (0, 0.08, 0), (0, 0.42, 0), 0.05, segs=6)
    frustum(m, mat("hull"), (0.04, 0.42, 0.04), (-0.12, 0.68, -0.12), 0.08, 0.42, segs=14, mat_cap=mat("hull_dark"))
    spike(m, mat("band_red"), (-0.08, 0.6, -0.08), (-0.22, 0.86, -0.22), 0.03, segs=5)
    return m, mat.mats


def hull_section(_rnd):
    """A curved hull panel: a quarter ring of plating with ribs."""
    m, mat = Mesh(), Materials()
    segs, r0, r1, depth = 6, 0.42, 0.5, 0.42
    for k in range(segs):
        a0 = math.pi * (0.15 + 0.7 * k / segs)
        a1 = math.pi * (0.15 + 0.7 * (k + 1) / segs)
        outer = [(math.cos(a) * r1, math.sin(a) * r1 - 0.2) for a in (a0, a1)]
        inner = [(math.cos(a) * r0, math.sin(a) * r0 - 0.2) for a in (a0, a1)]
        for z0, z1 in ((-depth, depth),):
            quad(m, mat("hull"), (outer[0][0], outer[0][1], z0), (outer[1][0], outer[1][1], z0),
                 (outer[1][0], outer[1][1], z1), (outer[0][0], outer[0][1], z1),
                 (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), 0))
            quad(m, mat("hull_dark"), (inner[0][0], inner[0][1], z0), (inner[1][0], inner[1][1], z0),
                 (inner[1][0], inner[1][1], z1), (inner[0][0], inner[0][1], z1),
                 (-math.cos((a0 + a1) / 2), -math.sin((a0 + a1) / 2), 0))
            for z, out in ((z0, -1), (z1, 1)):
                quad(m, mat("hull_dark"), (inner[0][0], inner[0][1], z), (outer[0][0], outer[0][1], z),
                     (outer[1][0], outer[1][1], z), (inner[1][0], inner[1][1], z), (0, 0, out))
    for z in (-0.2, 0.2):
        for k in range(segs):
            a = math.pi * (0.15 + 0.7 * (k + 0.5) / segs)
            cylinder(m, mat("hull_dark"), (math.cos(a) * r1, math.sin(a) * r1 - 0.2, z - 0.03),
                     (math.cos(a) * r1, math.sin(a) * r1 - 0.2, z + 0.03), 0.04, segs=6)
    lowest = min(min(t[1][1], t[2][1], t[3][1]) for t in m.tris)
    return m.translated((0, -lowest, 0)), mat.mats


def engine(_rnd):
    """A complete engine: a tank-fed body over a large bell nozzle."""
    m, mat = Mesh(), Materials()
    frustum(m, mat("nozzle"), (0, 0.0, 0), (0, 0.5, 0), 0.44, 0.16, segs=14, mat_cap=mat("nozzle_hot"))
    cylinder(m, mat("metal_dark"), (0, 0.5, 0), (0, 0.66, 0), 0.22, segs=12)
    cylinder(m, mat("canister"), (0, 0.66, 0), (0, 1.0, 0), 0.3, segs=12, mat_cap=mat("fuel_orange"))
    for x in (-0.26, 0.26):
        cylinder(m, mat("copper"), (x, 0.66, 0), (x * 0.7, 0.5, 0), 0.04, segs=6)
    return m, mat.mats


def partial(fn, *args, **kwargs):
    return lambda rnd: fn(rnd, *args, **kwargs)


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
    "item_bauxite": (bauxite, 11),
    "item_limestone": (limestone, 12),
    "item_quartz": (quartz, 13),
    "item_titanium_ore": (titanium_ore, 14),
    "item_sulfur": (sulfur, 15),
    "item_rare_earths": (rare_earths, 16),
    "item_uranium_ore": (uranium_ore, 17),
    "item_crystals": (crystals, 18),
    "item_lithium": (lithium, 19),
    "item_copper": (partial(bars, "copper", "copper_light"), 20),
    "item_steel": (partial(bars, "steel", "steel_light"), 21),
    "item_glass": (glass, 22),
    "item_lumber": (lumber, 23),
    "item_charcoal": (charcoal, 24),
    "item_cement": (partial(sack, "sack", "concrete_dark"), 25),
    "item_concrete": (concrete, 26),
    "item_kerosene": (partial(bottle, "canister", "band_yellow", "metal_dark"), 27),
    "item_plastic": (partial(sack, "sack_white", "band_blue"), 28),
    "item_hydrogen": (partial(bottle, "canister", "band_red", "metal_dark", tall=True), 29),
    "item_liquid_oxygen": (partial(bottle, "frost", "band_blue", "metal", tall=True), 30),
    "item_aluminum": (partial(bars, "aluminum", "aluminum_light"), 31),
    "item_silicon": (silicon, 32),
    "item_sulfuric_acid": (partial(bottle, "band_yellow", "chip", "metal_dark"), 33),
    "item_rubber": (tire, 34),
    "item_titanium": (partial(bars, "titanium", "titanium_light"), 35),
    "item_carbon_fiber": (partial(roll, "carbon", "carbon_weave"), 36),
    "item_enriched_uranium": (enriched_uranium, 37),
    "item_hull_plate": (partial(plates, "steel", "steel_light", 3, 0.42, 0.08), 38),
    "item_steel_beam": (steel_beam, 39),
    "item_copper_wire": (partial(spool, "copper", "copper_light"), 40),
    "item_circuit": (circuit, 41),
    "item_insulated_cable": (partial(spool, "band_red", "magnet_red"), 42),
    "item_machine_parts": (machine_parts, 43),
    "item_silicon_wafer": (silicon_wafer, 44),
    "item_processor": (processor, 45),
    "item_magnet": (magnet, 46),
    "item_electric_motor": (electric_motor, 47),
    "item_battery": (battery, 48),
    "item_solar_cell": (solar_cell, 49),
    "item_composite_panel": (partial(plates, "carbon", "carbon_weave", 2, 0.44, 0.07), 50),
    "item_ceramic_tile": (partial(plates, "ceramic", "ceramic", 4, 0.3, 0.06), 51),
    "item_aluminum_frame": (aluminum_frame, 52),
    "item_armor_plate": (armor_plate, 53),
    "item_control_module": (control_module, 54),
    "item_computer": (computer, 55),
    "item_thruster_assembly": (thruster_assembly, 56),
    "item_fuel_tank": (fuel_tank, 57),
    "item_heat_shield": (heat_shield, 58),
    "item_fuel_rod": (fuel_rod, 59),
    "item_life_support_unit": (life_support_unit, 60),
    "item_sensor_array": (sensor_array, 61),
    "item_hull_section": (hull_section, 62),
    "item_engine": (engine, 63),
}


# ---- UI icons ---------------------------------------------------------------------------------
# Flat shapes for the top bar, rendered like the item icons. Each lies in the plane facing the
# game camera (which looks down from +X +Z at 48 degrees), so its icon shows it face on.

_PITCH = math.radians(48)
_FACING = (math.cos(_PITCH) / math.sqrt(2), math.sin(_PITCH), math.cos(_PITCH) / math.sqrt(2))
_ACROSS = (-1 / math.sqrt(2), 0.0, 1 / math.sqrt(2))
_UP = (-math.sin(_PITCH) / math.sqrt(2), math.cos(_PITCH), -math.sin(_PITCH) / math.sqrt(2))


def _on_face(u, v, d=0.0):
    return tuple(_ACROSS[i] * u + _UP[i] * v + _FACING[i] * d for i in range(3))


def _slab(m, front, side, poly, depth=0.1):
    """A convex polygon (u, v points, counter-clockwise as seen by the camera) as a thin slab."""
    lift = 0.6
    pts_f = [_on_face(u, v + lift, depth / 2) for u, v in poly]
    pts_b = [_on_face(u, v + lift, -depth / 2) for u, v in poly]
    neg = tuple(-c for c in _FACING)
    for k in range(1, len(poly) - 1):
        m.tri(front, pts_f[0], pts_f[k], pts_f[k + 1], outward=_FACING)
        m.tri(side, pts_b[0], pts_b[k + 1], pts_b[k], outward=neg)
    cu = sum(u for u, _ in poly) / len(poly)
    cv = sum(v for _, v in poly) / len(poly)
    for k in range(len(poly)):
        j = (k + 1) % len(poly)
        mu = (poly[k][0] + poly[j][0]) / 2 - cu
        mv = (poly[k][1] + poly[j][1]) / 2 - cv
        out = tuple(_ACROSS[i] * mu + _UP[i] * mv for i in range(3))
        quad(m, side, pts_f[k], pts_b[k], pts_b[j], pts_f[j], out)


def icon_sun(_rnd):
    """A pale yellow disc with eight short rays."""
    m, mat = Mesh(), Materials()
    disc = [(0.3 * math.cos(2 * math.pi * k / 16), 0.3 * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    _slab(m, mat("sun"), mat("sun", 0.8), disc)
    for k in range(8):
        a = 2 * math.pi * k / 8
        w = 0.08
        base = [(0.38 * math.cos(a + s * w), 0.38 * math.sin(a + s * w)) for s in (-1, 1)]
        tip = (0.56 * math.cos(a), 0.56 * math.sin(a))
        _slab(m, mat("sun"), mat("sun", 0.8), [base[0], base[1], tip])
    return m, mat.mats


def icon_moon(_rnd):
    """A pale crescent opening to the right."""
    m, mat = Mesh(), Materials()
    segs = 10
    r = 0.5
    for k in range(segs):
        a0 = math.pi / 2 + math.pi * k / segs
        a1 = math.pi / 2 + math.pi * (k + 1) / segs
        outer0, outer1 = (r * math.cos(a0), r * math.sin(a0)), (r * math.cos(a1), r * math.sin(a1))
        inner0, inner1 = (0.35 * r * math.cos(a0), r * math.sin(a0)), (0.35 * r * math.cos(a1), r * math.sin(a1))
        _slab(m, mat("moon"), mat("moon", 0.8), [outer0, outer1, inner1, inner0] if k not in (0, segs - 1)
              else ([outer0, outer1, inner1] if k == segs - 1 else [outer0, outer1, inner0]))
    return m, mat.mats


def icon_power(_rnd):
    """A lightning bolt in the power yellow: two slanted wedges, stepping down to a point."""
    m, mat = Mesh(), Materials()
    _slab(m, mat("power_accent"), mat("power_accent", 0.75), [(-0.02, 0.6), (0.28, 0.6), (0.08, 0.06), (-0.3, 0.06)])
    _slab(m, mat("power_accent"), mat("power_accent", 0.75), [(-0.22, 0.12), (0.26, 0.12), (-0.16, -0.62)])
    return m, mat.mats


def _crate(m, mat, cu, cv, size):
    """A front-on wooden crate: a square with a darker frame and a diagonal brace."""
    h = size / 2
    _slab(m, mat("plank"), mat("plank_dark"), [(cu - h, cv - h), (cu + h, cv - h), (cu + h, cv + h), (cu - h, cv + h)], 0.12)
    t = size * 0.12
    for poly in ([(cu - h, cv + h - t), (cu + h, cv + h - t), (cu + h, cv + h), (cu - h, cv + h)],
                 [(cu - h, cv - h), (cu + h, cv - h), (cu + h, cv - h + t), (cu - h, cv - h + t)],
                 [(cu - h + t * 0.4, cv - h + t), (cu - h + t * 1.6, cv - h + t), (cu + h - t * 0.4, cv + h - t), (cu + h - t * 1.6, cv + h - t)]):
        _slab(m, mat("plank_dark"), mat("plank_dark"), poly, 0.16)


def _arc_arrow(m, mat, band, cu, cv, r, width, a_from, a_to, segs=8):
    """An arrow along a circular arc from a_from to a_to (radians), its head at a_to."""
    for k in range(segs):
        a0 = a_from + (a_to - a_from) * k / segs
        a1 = a_from + (a_to - a_from) * (k + 1) / segs
        ri, ro = r - width / 2, r + width / 2
        poly = [(cu + ro * math.cos(a0), cv + ro * math.sin(a0)), (cu + ri * math.cos(a0), cv + ri * math.sin(a0)),
                (cu + ri * math.cos(a1), cv + ri * math.sin(a1)), (cu + ro * math.cos(a1), cv + ro * math.sin(a1))]
        if (a_to - a_from) > 0:
            poly.reverse()
        _slab(m, mat(band if band and k == segs // 2 else "hub_accent"), mat("hub_accent", 0.75), poly, 0.12)
    # the head: a triangle at the end, pointing along the arc
    tip = a_to + (0.35 if a_to > a_from else -0.35)
    pts = [(cu + (r + width * 1.3) * math.cos(a_to), cv + (r + width * 1.3) * math.sin(a_to)),
           (cu + (r - width * 1.3) * math.cos(a_to), cv + (r - width * 1.3) * math.sin(a_to)),
           (cu + r * math.cos(tip), cv + r * math.sin(tip))]
    if a_to < a_from:
        pts.reverse()
    _slab(m, mat("hub_accent"), mat("hub_accent", 0.75), pts, 0.12)


def icon_material_transfer(_rnd):
    """A crate with a green arrow arcing over it: the network carrying it."""
    m, mat = Mesh(), Materials()
    _crate(m, mat, 0.0, -0.22, 0.5)
    _arc_arrow(m, mat, None, 0.0, -0.05, 0.5, 0.11, math.radians(15), math.radians(150))
    return m, mat.mats


def icon_advanced_material_transfer(_rnd):
    """Two crates under a double green arrow with a yellow band: the network carrying more."""
    m, mat = Mesh(), Materials()
    _crate(m, mat, -0.25, -0.3, 0.38)
    _crate(m, mat, 0.25, -0.3, 0.38)
    _arc_arrow(m, mat, "power_accent", 0.0, -0.1, 0.48, 0.1, math.radians(15), math.radians(150))
    _arc_arrow(m, mat, None, 0.0, -0.1, 0.66, 0.08, math.radians(30), math.radians(140))
    return m, mat.mats


def _ring(m, mat, cu, cv, r, width, a_from, a_to, segs, depth=0.1):
    """A band along a circular arc from a_from to a_to (radians, a_from < a_to), as slabs."""
    ri, ro = r - width / 2, r + width / 2
    for k in range(segs):
        a0 = a_from + (a_to - a_from) * k / segs
        a1 = a_from + (a_to - a_from) * (k + 1) / segs
        _slab(m, mat("icon_mono"), mat("icon_mono", 0.75),
              [(cu + ro * math.cos(a0), cv + ro * math.sin(a0)), (cu + ro * math.cos(a1), cv + ro * math.sin(a1)),
               (cu + ri * math.cos(a1), cv + ri * math.sin(a1)), (cu + ri * math.cos(a0), cv + ri * math.sin(a0))], depth)


def icon_power_switch(_rnd):
    """The power symbol in a light neutral (the UI tints it): a ring broken at the top by a vertical bar."""
    m, mat = Mesh(), Materials()
    _ring(m, mat, 0.0, -0.06, 0.44, 0.13, math.radians(90 + 38), math.radians(90 + 360 - 38), 20)
    _slab(m, mat("icon_mono"), mat("icon_mono", 0.75), [(-0.065, -0.02), (0.065, -0.02), (0.065, 0.56), (-0.065, 0.56)])
    return m, mat.mats


def icon_network(_rnd):
    """A hub mast in a light neutral (the UI tints it): a tapered mast on a base, a ring around its top, a cap."""
    m, mat = Mesh(), Materials()
    _slab(m, mat("icon_mono"), mat("icon_mono", 0.75), [(-0.34, -0.62), (0.34, -0.62), (0.24, -0.48), (-0.24, -0.48)])
    _slab(m, mat("icon_mono"), mat("icon_mono", 0.75), [(-0.09, -0.48), (0.09, -0.48), (0.05, 0.3), (-0.05, 0.3)])
    _ring(m, mat, 0.0, 0.2, 0.34, 0.09, 0.0, 2 * math.pi, 24)
    cap = [(0.12 * math.cos(2 * math.pi * k / 12), 0.2 + 0.12 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    _slab(m, mat("icon_mono"), mat("icon_mono", 0.75), cap)
    return m, mat.mats


ICONS = {
    "icon_sun": icon_sun,
    "icon_moon": icon_moon,
    "icon_power": icon_power,
    "icon_material_transfer": icon_material_transfer,
    "icon_advanced_material_transfer": icon_advanced_material_transfer,
    "icon_power_switch": icon_power_switch,
    "icon_network": icon_network,
}


def build_all():
    for name, (fn, seed) in ITEMS.items():
        m, mats = fn(random.Random(seed))
        m.write("items/" + name.removeprefix("item_"), mats)
    for name, fn in ICONS.items():
        m, mats = fn(random.Random(0))
        m.write("icons/" + name.removeprefix("icon_"), mats)


if __name__ == "__main__":
    build_all()
