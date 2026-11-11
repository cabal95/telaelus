"""Procedural generator for the experiment assets. Writes .obj/.mtl into models/.
Run: python gen.py
Conventions: Y up, origin at base centre, OBJ counter-clockwise front faces.
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


# ---------------------------------------------------------------- mountains
# Two families share three shapes: stone (bare rock, any non-snow ground) and snow (on Snow ground).
STONE_MATS = {
    "scree": ((0.44, 0.41, 0.36), None),
    "rock_dk": ((0.38, 0.36, 0.35), None),
    "rock": ((0.50, 0.47, 0.44), None),
    "rock_lt": ((0.63, 0.60, 0.57), None),
}
SNOW_MATS = {
    "snow": ((0.93, 0.95, 0.98), None),
    "snow_sh": ((0.80, 0.84, 0.90), None),
    "rock_dk": ((0.38, 0.36, 0.35), None),
    "rock": ((0.50, 0.47, 0.44), None),
}
HALF = 2.0   # tile is 4 x 4; nothing leaves it
N = 10       # grid cells per side


def cone(x, z, cx, cz, h, r, p=1.25):
    d = math.hypot(x - cx, z - cz)
    return h * max(0.0, 1 - d / r) ** p


def ridge(x, z, a, b, h, r, p=1.25):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
    d = math.hypot(x - (ax + t * dx), z - (az + t * dz))
    # height tapers toward ridge ends
    return h * (0.55 + 0.45 * math.sin(math.pi * t)) * max(0.0, 1 - d / r) ** p


def mountain_shape(seed, peaks):
    """Returns the surface triangles. Every vertex lies within the 4 x 4 cell, and every edge sits on the ground."""
    rnd = random.Random(seed)
    step = 2 * HALF / N
    xs = [-HALF + i * step for i in range(N + 1)]
    grid = {}
    for i in range(N + 1):
        for j in range(N + 1):
            x, z = xs[i], xs[j]
            on_border = i in (0, N) or j in (0, N)
            if on_border:
                y = 0.0
            else:
                x += rnd.uniform(-0.14, 0.14)
                z += rnd.uniform(-0.14, 0.14)
                d = min(HALF - abs(x), HALF - abs(z))
                w = max(0.0, min(1.0, d / 1.1))
                w = w * w * (3 - 2 * w)
                y = w * (sum(fn(x, z) for fn in peaks) + rnd.uniform(-0.15, 0.15))
                y = max(0.02, y)
            grid[(i, j)] = (x, y, z)
    surface = []
    for i in range(N):
        for j in range(N):
            a, b, c, d = grid[(i, j)], grid[(i, j + 1)], grid[(i + 1, j + 1)], grid[(i + 1, j)]
            surface += [(a, b, c), (a, c, d)] if (i + j) % 2 == 0 else [(a, b, d), (b, c, d)]
    return surface


def stone_mat(rnd, ay, ny):
    if ay < 0.5:
        return "scree" if rnd.random() < 0.6 else "rock_dk"
    return "rock_dk" if ay < 1.4 else "rock" if ay < 2.4 else "rock_lt"


def snow_mat(rnd, ay, ny):
    if ay < 1.0:
        return "snow_sh" if ny > 0.55 or rnd.random() < 0.3 else "rock_dk"
    if ay > 2.4:
        return "snow" if ny > 0.2 or rnd.random() < 0.5 else "rock"
    return "snow" if ny > 0.38 else "rock"


def paint(surface, seed, pick):
    rnd = random.Random(seed * 7 + 1)
    m = Mesh()
    for t in surface:
        n = norm(cross(sub(t[1], t[0]), sub(t[2], t[0])))
        ny = abs(n[1])
        ay = sum(p[1] for p in t) / 3 + rnd.uniform(-0.2, 0.2)
        m.tri(pick(rnd, ay, ny), *t, outward=(0, 1, 0))
    return m


MOUNTAIN_SHAPES = {
    "a": (11, [lambda x, z: cone(x, z, 0.1, -0.1, 4.0, 1.9),
               lambda x, z: cone(x, z, -0.9, 0.9, 1.4, 1.2)]),
    "b": (23, [lambda x, z: cone(x, z, -0.6, -0.5, 3.5, 1.7),
               lambda x, z: cone(x, z, 0.7, 0.6, 3.1, 1.6)]),
    "c": (37, [lambda x, z: ridge(x, z, (-1.1, 0.9), (1.1, -0.9), 3.7, 1.5),
               lambda x, z: cone(x, z, 0.9, 0.9, 1.2, 1.0)]),
}


def build_mountains():
    for suffix, (seed, peaks) in MOUNTAIN_SHAPES.items():
        shape = mountain_shape(seed, peaks)
        paint(shape, seed, stone_mat).write("mountain_" + suffix, STONE_MATS)
        paint(shape, seed, snow_mat).write("mountain_snow_" + suffix, SNOW_MATS)


# ---------------------------------------------------------------- ores
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


IRON_MATS = {
    "stone": ((0.52, 0.51, 0.50), None),
    "stone_dk": ((0.40, 0.40, 0.41), None),
    "iron_rust": ((0.66, 0.31, 0.17), None),
    "iron_dk": ((0.45, 0.20, 0.12), None),
    "iron_steel": ((0.30, 0.33, 0.42), None),
}
def iron_ore(seed):
    rnd = random.Random(seed)
    m = Mesh()
    # (centre x, z, radius xz, height)
    lumps = [(0.0, 0.0, 0.42, 0.80), (-0.36, 0.22, 0.26, 0.42), (0.34, -0.20, 0.28, 0.50),
             (0.18, 0.40, 0.2, 0.30), (-0.30, -0.34, 0.2, 0.28)]
    for k, (x, z, r, h) in enumerate(lumps):
        centre = (x, h * 0.18, z)
        tris = blob(rnd, centre, (r, h * 0.82, r), 1 if k == 0 else 0, 0.30 if k == 0 else 0.22)
        phase = rnd.uniform(0, 6)

        def pick(t, phase=phase):
            c = centroid(*t)
            n = vein_noise(c, phase)
            hi = c[1] > 0.10
            if hi and n > 0.35:
                return "iron_rust" if rnd.random() < 0.65 else "iron_dk"
            if hi and n > -0.25:
                return "iron_dk" if rnd.random() < 0.5 else "iron_steel"
            return "stone" if rnd.random() < 0.6 else "stone_dk"
        add_blob(m, tris, centre, pick)
    # angular raw-ore chunks resting on the cluster
    for x, y, z, r in [(0.05, 0.72, 0.05, 0.16), (-0.25, 0.42, 0.18, 0.11), (0.38, 0.45, -0.18, 0.12), (0.12, 0.28, 0.46, 0.09)]:
        centre = (x, y, z)
        tris = blob(rnd, centre, (r, r * 0.9, r), 0, 0.3, squash_base=False)
        add_blob(m, tris, centre, lambda t: rnd.choice(["iron_steel", "iron_steel", "iron_rust", "iron_dk"]))
    return m


if __name__ == "__main__":
    build_mountains()
    for suffix, seed in (("a", 5), ("b", 17), ("c", 31)):
        iron_ore(seed).write("rock_" + suffix, IRON_MATS)
