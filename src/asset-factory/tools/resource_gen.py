"""Resource field models: the deposits a field shows, three variants (a, b, c) each.

A field shows five of these, one per depletion stage; the game scales each to its deposit's target
height and fits it in a 1.2-unit slot (NodeModels). Origin at the base center, resting on y = 0.
See docs/blueprints/art-style.md and src/Game/assets/models/nature/README.md.
"""
import math
import random

from mesh import Materials, Mesh, add, centroid, cylinder, mul, norm, quad, spike


# ---------------------------------------------------------------- lumps
def icosahedron():
    t = (1 + math.sqrt(5)) / 2
    vs = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
          (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    vs = [norm(v) for v in vs]
    fs = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
          (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
          (8, 6, 7), (9, 8, 1)]
    return vs, fs


def subdivide(vs, fs):
    vs = list(vs)
    cache = {}

    def mid(a, b):
        key = (min(a, b), max(a, b))
        if key not in cache:
            vs.append(norm(mul(add(vs[a], vs[b]), 0.5)))
            cache[key] = len(vs) - 1
        return cache[key]
    out = []
    for a, b, c in fs:
        ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
        out += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
    return vs, out


def blob(rnd, centre, radii, subdiv, jitter, squash_base=True):
    """A faceted lump: deformed (sub)divided icosahedron. Returns [(a,b,c)] with centre-relative outward winding."""
    vs, fs = icosahedron()
    for _ in range(subdiv):
        vs, fs = subdivide(vs, fs)
    pts = []
    for v in vs:
        k = 1 + rnd.uniform(-jitter, jitter)
        p = (v[0] * radii[0] * k, v[1] * radii[1] * k, v[2] * radii[2] * k)
        p = add(p, centre)
        if squash_base:
            p = (p[0], max(0.0, p[1]), p[2])
        pts.append(p)
    tris = []
    for a, b, c in fs:
        pa, pb, pc = pts[a], pts[b], pts[c]
        if squash_base and max(pa[1], pb[1], pc[1]) <= 0.001:
            continue  # flat underside, never seen
        tris.append((pa, pb, pc))
    return tris


def add_blob(m, tris, centre, pick):
    for t in tris:
        m.tri(pick(t), *t, centre=centre)


def vein_noise(p, phase):
    return math.sin(p[0] * 5.1 + phase) * math.sin(p[2] * 4.3 + phase * 1.7) + 0.6 * math.sin(p[1] * 7.0 + phase)


# ---------------------------------------------------------------- ores
ORE_LUMPS = [(0.0, 0.0, 0.42, 0.80), (-0.36, 0.22, 0.26, 0.42), (0.34, -0.20, 0.28, 0.50),
             (0.18, 0.40, 0.2, 0.30), (-0.30, -0.34, 0.2, 0.28)]  # (centre x, z, radius xz, height)
ORE_CHUNKS = [(0.05, 0.72, 0.05, 0.16), (-0.25, 0.42, 0.18, 0.11), (0.38, 0.45, -0.18, 0.12),
              (0.12, 0.28, 0.46, 0.09)]


def ore(seed, rock, rock_dark, vein, vein_dark, chunk_colors):
    """A rock cluster with veins on its upper faces and loose ore chunks on top."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    for k, (x, z, r, h) in enumerate(ORE_LUMPS):
        centre = (x, h * 0.18, z)
        tris = blob(rnd, centre, (r, h * 0.82, r), 1 if k == 0 else 0, 0.30 if k == 0 else 0.22)
        phase = rnd.uniform(0, 6)

        def pick(t, phase=phase):
            c = centroid(*t)
            n = vein_noise(c, phase)
            hi = c[1] > 0.10
            if hi and n > 0.35:
                return mat(vein) if rnd.random() < 0.65 else mat(vein_dark)
            if hi and n > -0.25:
                return mat(vein_dark) if rnd.random() < 0.5 else mat(chunk_colors[0])
            return mat(rock) if rnd.random() < 0.6 else mat(rock_dark)
        add_blob(m, tris, centre, pick)
    for x, y, z, r in ORE_CHUNKS:
        centre = (x, y, z)
        tris = blob(rnd, centre, (r, r * 0.9, r), 0, 0.3, squash_base=False)
        add_blob(m, tris, centre, lambda t: mat(rnd.choice(chunk_colors)))
    return m, mat.mats


def iron_ore(seed):
    return ore(seed, "ore_rock", "ore_rock_dark", "iron_rust", "iron_dark",
               ["iron_steel", "iron_steel", "iron_rust", "iron_dark"])


def copper_ore(seed):
    return ore(seed, "copper_rock", "ore_rock_dark", "copper_green", "copper_orange",
               ["copper_orange", "copper_orange", "copper_green", "copper_rock"])


# ---------------------------------------------------------------- coal
def coal(seed):
    """A low heap of angular black lumps."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    heap = (0.0, 0.0, 0.0)
    add_blob(m, blob(rnd, heap, (0.5, 0.42, 0.45), 1, 0.18), heap,
             lambda t: mat("coal", rnd.choice((1.0, 0.85))))
    for _ in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        d = rnd.uniform(0.0, 0.38)
        x, z = math.cos(a) * d, math.sin(a) * d
        y = 0.32 * (1 - (d / 0.5) ** 2) + 0.04
        r = rnd.uniform(0.09, 0.15)
        centre = (x, y, z)
        add_blob(m, blob(rnd, centre, (r, r * 0.8, r), 0, 0.35, squash_base=False), centre,
                 lambda t: mat("coal_sheen") if centroid(*t)[1] > y + r * 0.3 and rnd.random() < 0.5 else mat("coal"))
    return m, mat.mats


# ---------------------------------------------------------------- stone
def stone(seed):
    """Two or three pale boulders, one resting on the others."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    boulders = [(-0.18, 0.0, 0.05, 0.42, 0.5), (0.26, 0.0, -0.12, 0.32, 0.38), (0.0, 0.42, 0.0, 0.26, 0.3)]
    if rnd.random() < 0.5:
        boulders.append((0.12, 0.0, 0.36, 0.2, 0.22))
    for x, y0, z, r, h in boulders:
        centre = (x, y0 + h * 0.5, z)
        tris = blob(rnd, centre, (r, h * 0.5, r * rnd.uniform(0.85, 1.1)), 1, 0.08, squash_base=y0 == 0.0)
        add_blob(m, tris, centre, lambda t: mat("boulder", rnd.choice((1.0, 0.95, 1.04)))
                 if centroid(*t)[1] > 0.12 else mat("boulder_dark"))
    return m, mat.mats


# ---------------------------------------------------------------- crude oil
def crude_oil(seed):
    """An oil slick: a flat, glossy black pool with an iridescent rim, on a halo of stained ground.

    Everything lies within 0.05 of the ground, so it reads as a pool rather than an object.
    """
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    n = 16
    lobes = [(rnd.uniform(0, 2 * math.pi), rnd.uniform(0.08, 0.16)) for _ in range(2)]

    def radius(a):
        r = 0.42 + sum(h * math.cos(2 * (a - ph)) for ph, h in lobes)
        return r * rnd.uniform(0.92, 1.08)

    outline = []
    for k in range(n):
        a = 2 * math.pi * k / n
        r = radius(a)
        outline.append((math.cos(a) * r, math.sin(a) * r))

    def ring(scale, y):
        return [(x * scale, y, z * scale) for x, z in outline]

    # The pool sits just above the ground tiles' relief (up to 0.035) so the ground never shows
    # through it; the stained halo slopes from the ground up to the pool's edge.
    halo = ring(1.22, 0.0)       # stained ground just past the pool
    rim = ring(1.0, 0.045)       # the pool's edge, where the sheen shows
    inner = ring(0.82, 0.048)    # the black middle
    up = (0, 1, 0)
    for k in range(n):
        j = (k + 1) % n
        m.tri(mat("oil_earth", rnd.choice((1.0, 0.9))), halo[k], halo[j], rim[j], outward=up)
        m.tri(mat("oil_earth", rnd.choice((1.0, 0.9))), halo[k], rim[j], rim[k], outward=up)
        sheen = mat(("oil_sheen", "oil_violet", "oil_teal")[k % 3])
        m.tri(sheen, rim[k], rim[j], inner[j], outward=up)
        m.tri(mat("oil"), rim[k], inner[j], inner[k], outward=up)
        m.tri(mat("oil"), (0.0, 0.05, 0.0), inner[k], inner[j], outward=up)
    return m, mat.mats


# ---------------------------------------------------------------- trees
def crown_shade(t, centre):
    """Lighter on faces that look up, darker underneath, so a crown's facets read."""
    c = centroid(*t)
    up = c[1] - centre[1]
    return "light" if up > 0.15 else "dark" if up < -0.1 else "mid"


def conifer(seed):
    """A short trunk under three or four stacked cones narrowing to a point."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    cylinder(m, mat("bark"), (0, 0, 0), (0, 0.55, 0), 0.11, segs=6, mat_cap=mat("bark_dark"))
    tiers = rnd.choice((3, 4))
    y = 0.35
    radius = rnd.uniform(0.55, 0.65)
    for k in range(tiers):
        height = rnd.uniform(1.1, 1.3) - k * 0.1
        lean = (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04))
        shade = ("conifer_dark", "conifer", "conifer_light", "conifer_light")[k]
        spike(m, mat(shade), (0, y, 0), (lean[0], y + height, lean[1]), radius, segs=rnd.choice((6, 7)))
        y += height * rnd.uniform(0.5, 0.58)
        radius *= rnd.uniform(0.74, 0.8)
    return m, mat.mats


def broadleaf(seed):
    """A forked trunk under two or three overlapping lumpy blobs forming a round crown."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    cylinder(m, mat("bark"), (0, 0, 0), (0, 0.95, 0), 0.1, segs=6, mat_cap=mat("bark_dark"))
    for side in (-1, 1):
        cylinder(m, mat("bark"), (0, 0.8, 0), (side * 0.28, 1.25, rnd.uniform(-0.1, 0.1)), 0.06, segs=5)
    lumps = [(0.0, 1.55, 0.0, 0.72)]
    for k in range(rnd.choice((2, 3))):
        a = rnd.uniform(0, 2 * math.pi)
        lumps.append((math.cos(a) * 0.38, rnd.uniform(1.35, 1.55), math.sin(a) * 0.38, rnd.uniform(0.45, 0.58)))
    for x, y, z, r in lumps:
        centre = (x, y, z)
        tris = blob(rnd, centre, (r, r * 0.85, r), 1, 0.16, squash_base=False)
        add_blob(m, tris, centre, lambda t, c=centre: mat({"light": "leaf_light", "mid": "leaf", "dark": "leaf_dark"}[crown_shade(t, c)]))
    return m, mat.mats


def swamp_tree(seed):
    """A thick, leaning trunk flared at the base under a wide, flat crown with a hanging fringe."""
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    lean = (rnd.uniform(-0.18, 0.18), rnd.uniform(-0.18, 0.18))
    top = (lean[0], 1.35, lean[1])
    # Tapered trunk: wide at the base, narrow at the top.
    segs = 7
    for i in range(segs):
        a0 = 2 * math.pi * i / segs
        a1 = 2 * math.pi * (i + 1) / segs
        b0 = (math.cos(a0) * 0.2, 0.0, math.sin(a0) * 0.2)
        b1 = (math.cos(a1) * 0.2, 0.0, math.sin(a1) * 0.2)
        t0 = (top[0] + math.cos(a0) * 0.09, top[1], top[2] + math.sin(a0) * 0.09)
        t1 = (top[0] + math.cos(a1) * 0.09, top[1], top[2] + math.sin(a1) * 0.09)
        quad(m, mat("bark_dark" if i % 2 else "bark"), b0, b1, t1, t0, (math.cos(a0) + math.cos(a1), 0, math.sin(a0) + math.sin(a1)))
    # Root flares at the base.
    for k in range(3):
        a = 2 * math.pi * k / 3 + rnd.uniform(-0.3, 0.3)
        spike(m, mat("bark_dark"), (math.cos(a) * 0.12, 0.0, math.sin(a) * 0.12),
              (math.cos(a) * 0.38, 0.0, math.sin(a) * 0.38), 0.08, segs=4)
    # Wide, flattened crown.
    centre = (top[0], 1.55, top[2])
    tris = blob(rnd, centre, (0.95, 0.38, 0.85), 1, 0.15, squash_base=False)
    add_blob(m, tris, centre, lambda t: mat({"light": "swamp_leaf_light", "mid": "swamp_leaf", "dark": "swamp_leaf_dark"}[crown_shade(t, centre)]))
    # A ragged fringe hanging from the crown's edge.
    for k in range(9):
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.15, 0.15)
        rx, rz = math.cos(a) * 0.78, math.sin(a) * 0.7
        base = (centre[0] + rx, centre[1] - 0.12, centre[2] + rz)
        tip = (base[0] * 1.0, centre[1] - rnd.uniform(0.45, 0.7), base[2])
        spike(m, mat("moss"), base, tip, rnd.uniform(0.12, 0.18), segs=4)
    return m, mat.mats


TREES = {
    "tree_conifer": (conifer, (201, 213, 227)),
    "tree_broadleaf": (broadleaf, (231, 243, 257)),
    "tree_swamp": (swamp_tree, (261, 273, 287)),
}


DEPOSITS = {
    "iron_ore": (iron_ore, (5, 17, 31)),
    "copper_ore": (copper_ore, (41, 53, 67)),
    "coal": (coal, (71, 83, 97)),
    "stone": (stone, (101, 113, 127)),
    "crude_oil": (crude_oil, (131, 143, 157)),
}


def build_all():
    for name, (fn, seeds) in list(DEPOSITS.items()) + list(TREES.items()):
        for suffix, seed in zip("abc", seeds):
            m, mats = fn(seed)
            m.write("%s_%s" % (name, suffix), mats)
