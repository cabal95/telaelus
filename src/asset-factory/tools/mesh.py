"""Shared mesh building blocks for the generators: vector helpers, the Mesh writer, the palette,
and simple solids. Conventions: Y up, origin at base centre, OBJ counter-clockwise front faces.
"""
import math
import os
import random

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "models")
os.makedirs(OUT, exist_ok=True)


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a):
    l = math.sqrt(dot(a, a)) or 1.0
    return (a[0] / l, a[1] / l, a[2] / l)
def centroid(*ps): return mul(ps[0] if len(ps) == 1 else __import__("functools").reduce(add, ps), 1.0 / len(ps))


class Mesh:
    def __init__(self):
        self.tris = []  # (material, a, b, c)

    def tri(self, mat, a, b, c, outward=None, centre=None):
        """Add a triangle, flipping winding so the normal faces `outward` (a vector) or away from `centre`."""
        n = cross(sub(b, a), sub(c, a))
        if outward is None and centre is not None:
            outward = sub(centroid(a, b, c), centre)
        if outward is not None and dot(n, outward) < 0:
            b, c = c, b
        self.tris.append((mat, a, b, c))

    def translated(self, d):
        m = Mesh()
        m.tris = [(mt, add(a, d), add(b, d), add(c, d)) for mt, a, b, c in self.tris]
        return m

    def extend(self, other):
        self.tris.extend(other.tris)

    def write(self, name, materials):
        with open(os.path.join(OUT, name + ".mtl"), "w") as f:
            for mname, (kd, ke) in materials.items():
                f.write("newmtl %s\nKd %.3f %.3f %.3f\n" % ((mname,) + kd))
                if ke:
                    f.write("Ke %.3f %.3f %.3f\n" % ke)
                f.write("\n")
        verts, by_mat = [], {}
        for mt, a, b, c in self.tris:
            idx = []
            for p in (a, b, c):
                verts.append(p)
                idx.append(len(verts))
            by_mat.setdefault(mt, []).append(idx)
        with open(os.path.join(OUT, name + ".obj"), "w") as f:
            f.write("mtllib %s.mtl\n" % name)
            for v in verts:
                f.write("v %.4f %.4f %.4f\n" % v)
            for mt, faces in by_mat.items():
                f.write("usemtl %s\n" % mt)
                for t in faces:
                    f.write("f %d %d %d\n" % tuple(t))
        print("wrote", name, len(self.tris), "tris")




# ---------------------------------------------------------------- palette
# Base colors (sRGB) shared by every generator; docs/blueprints/art-style.md lists the same table.
PALETTE = {
    # terrain
    "grass": (0.33, 0.49, 0.27),
    "sand": (0.78, 0.70, 0.48),
    "snow": (0.90, 0.92, 0.95),
    "rock": (0.48, 0.46, 0.44),
    "mud": (0.38, 0.32, 0.22),
    "shallow_water": (0.20, 0.38, 0.60),
    "deep_water": (0.10, 0.22, 0.42),
    "lava": (0.95, 0.42, 0.10),
    "lava_crust": (0.24, 0.13, 0.10),
    "wet_earth": (0.27, 0.23, 0.18),
    "puddle": (0.22, 0.24, 0.24),
    # materials
    "stone": (0.50, 0.47, 0.44),
    "stone_dark": (0.36, 0.34, 0.33),
    "wood": (0.58, 0.41, 0.24),
    "wood_dark": (0.40, 0.28, 0.17),
    "metal": (0.58, 0.60, 0.63),
    "metal_dark": (0.30, 0.31, 0.34),
    "shadow": (0.08, 0.07, 0.07),
    "furnace_glow": (1.00, 0.55, 0.18),
    # deposits
    "ore_rock": (0.52, 0.51, 0.50),
    "ore_rock_dark": (0.40, 0.40, 0.41),
    "iron_rust": (0.66, 0.31, 0.17),
    "iron_dark": (0.45, 0.20, 0.12),
    "iron_steel": (0.30, 0.33, 0.42),
    "copper_rock": (0.50, 0.44, 0.38),
    "copper_green": (0.22, 0.58, 0.45),
    "copper_orange": (0.82, 0.46, 0.22),
    "coal": (0.13, 0.13, 0.14),
    "coal_sheen": (0.24, 0.24, 0.27),
    "boulder": (0.70, 0.68, 0.64),
    "boulder_dark": (0.56, 0.54, 0.51),
    "oil": (0.05, 0.05, 0.06),
    "oil_sheen": (0.20, 0.19, 0.26),
    "oil_violet": (0.28, 0.18, 0.36),
    "oil_teal": (0.12, 0.30, 0.30),
    "oil_earth": (0.30, 0.25, 0.19),
    # trees (mid tones: a shaded crown must not read as black)
    "bark": (0.45, 0.32, 0.21),
    "bark_dark": (0.34, 0.24, 0.16),
    "conifer": (0.20, 0.42, 0.33),
    "conifer_light": (0.27, 0.52, 0.40),
    "conifer_dark": (0.16, 0.34, 0.27),
    "leaf": (0.33, 0.56, 0.26),
    "leaf_light": (0.44, 0.66, 0.31),
    "leaf_dark": (0.25, 0.45, 0.21),
    "swamp_leaf": (0.40, 0.46, 0.21),
    "swamp_leaf_light": (0.49, 0.54, 0.26),
    "swamp_leaf_dark": (0.31, 0.37, 0.17),
    "moss": (0.34, 0.40, 0.19),
    # resource items (UI icons)
    "log_end": (0.80, 0.66, 0.45),
    "coal_facet": (0.36, 0.36, 0.40),
    "barrel": (0.24, 0.24, 0.27),
    "barrel_lid": (0.38, 0.38, 0.42),
    "barrel_band": (0.45, 0.30, 0.18),
    "water": (0.26, 0.52, 0.86),
    "water_light": (0.45, 0.70, 0.95),
    "ingot": (0.62, 0.64, 0.68),
    "ingot_light": (0.78, 0.80, 0.84),
    "canister": (0.88, 0.88, 0.86),
    "fuel_orange": (0.95, 0.50, 0.15),
    # building accents (each building's identity color)
    "mine_accent": (0.85, 0.55, 0.20),
    "lumber_accent": (0.62, 0.40, 0.20),
    "smelter_accent": (0.78, 0.26, 0.20),
    "refinery_accent": (0.22, 0.42, 0.78),
    "oil_well_accent": (0.92, 0.74, 0.16),
}
GLOW = {"lava", "furnace_glow"}  # palette entries that are emissive
GLOW_STRENGTH = 0.5  # emission as a fraction of the base color


def shade(color, factor):
    return tuple(max(0.0, min(1.0, c * factor)) for c in color)


class Materials:
    """Collects the materials a model uses: name -> (Kd, Ke). Shaded variants get a suffix."""

    def __init__(self):
        self.mats = {}

    def __call__(self, key, factor=1.0):
        name = key if factor == 1.0 else "%s_%d" % (key, round(factor * 100))
        if name not in self.mats:
            kd = shade(PALETTE[key], factor)
            self.mats[name] = (kd, shade(kd, GLOW_STRENGTH) if key in GLOW else None)
        return name


# ---------------------------------------------------------------- solids
def quad(m, mat, a, b, c, d, outward):
    m.tri(mat, a, b, c, outward=outward)
    m.tri(mat, a, c, d, outward=outward)


def box(m, mat, x0, y0, z0, x1, y1, z1, bottom=False, mat_top=None):
    """Axis-aligned box. The bottom face is left out unless asked (it rests on the ground)."""
    p = [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    # p index bits: x(4) y(2) z(1)
    quad(m, mat_top or mat, p[2], p[3], p[7], p[6], (0, 1, 0))
    quad(m, mat, p[0], p[1], p[3], p[2], (-1, 0, 0))
    quad(m, mat, p[4], p[5], p[7], p[6], (1, 0, 0))
    quad(m, mat, p[0], p[4], p[6], p[2], (0, 0, -1))
    quad(m, mat, p[1], p[5], p[7], p[3], (0, 0, 1))
    if bottom:
        quad(m, mat, p[0], p[1], p[5], p[4], (0, -1, 0))


def cylinder(m, mat, a, b, r, segs=8, caps=True, mat_cap=None):
    """Cylinder from point a to point b with radius r (any orientation)."""
    axis = norm(sub(b, a))
    helper = (1, 0, 0) if abs(axis[0]) < 0.9 else (0, 0, 1)
    u = norm(cross(axis, helper))
    v = cross(axis, u)
    ring = []
    for i in range(segs):
        t = 2 * math.pi * (i + 0.5) / segs
        ring.append(add(mul(u, math.cos(t) * r), mul(v, math.sin(t) * r)))
    for i in range(segs):
        j = (i + 1) % segs
        p0, p1 = add(a, ring[i]), add(a, ring[j])
        q0, q1 = add(b, ring[i]), add(b, ring[j])
        out = add(ring[i], ring[j])
        quad(m, mat, p0, p1, q1, q0, out)
        if caps:
            m.tri(mat_cap or mat, a, p0, p1, outward=mul(axis, -1))
            m.tri(mat_cap or mat, b, q0, q1, outward=axis)


def spike(m, mat, base, tip, r, segs=8):
    axis = norm(sub(tip, base))
    helper = (1, 0, 0) if abs(axis[0]) < 0.9 else (0, 0, 1)
    u = norm(cross(axis, helper))
    v = cross(axis, u)
    pts = [add(base, add(mul(u, math.cos(2 * math.pi * i / segs) * r), mul(v, math.sin(2 * math.pi * i / segs) * r)))
           for i in range(segs)]
    for i in range(segs):
        m.tri(mat, pts[i], pts[(i + 1) % segs], tip, centre=base)


def prism_roof(m, mat, x0, x1, z0, z1, y0, y1, mat_gable=None):
    """Gabled roof with its ridge along X, from eave height y0 to ridge height y1."""
    zc = (z0 + z1) / 2
    quad(m, mat, (x0, y0, z0), (x1, y0, z0), (x1, y1, zc), (x0, y1, zc), (0, z1 - z0, -(y1 - y0) * 2))
    quad(m, mat, (x0, y0, z1), (x1, y0, z1), (x1, y1, zc), (x0, y1, zc), (0, z1 - z0, (y1 - y0) * 2))
    g = mat_gable or mat
    m.tri(g, (x0, y0, z0), (x0, y0, z1), (x0, y1, zc), outward=(-1, 0, 0))
    m.tri(g, (x1, y0, z0), (x1, y0, z1), (x1, y1, zc), outward=(1, 0, 0))
