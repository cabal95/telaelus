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
MTN_MATS = {
    "cliff": ((0.34, 0.32, 0.31), None),
    "scree": ((0.44, 0.41, 0.36), None),
    "rock_dk": ((0.38, 0.36, 0.35), None),
    "rock": ((0.50, 0.47, 0.44), None),
    "rock_lt": ((0.63, 0.60, 0.57), None),
    "snow": ((0.93, 0.95, 0.98), None),
}
HALF = 2.0   # tile is 4 x 4
N = 8        # grid cells per side


def border_h(s):
    """Height profile shared by all four tile edges so neighbouring tiles join seamlessly."""
    return 1.15 + 0.85 * (1 + math.cos(math.pi * s / HALF)) / 2


def base_h(x, z):
    return 1.15 + 0.85 * max((1 + math.cos(math.pi * x / HALF)) / 2, (1 + math.cos(math.pi * z / HALF)) / 2)


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


def mountain_tile(seed, peaks):
    rnd = random.Random(seed)
    step = 2 * HALF / N
    inner = [-HALF + i * step for i in range(N + 1)]
    out_d = [0.45, 0.9, 1.3]              # ring distances beyond the tile edge
    xs = [-HALF - d for d in reversed(out_d)] + inner + [HALF + d for d in out_d]
    R = len(out_d)
    ring_of = [R - k for k in range(R)] + [0] * (N + 1) + [k + 1 for k in range(R)]  # 3,2,1,0..0,1,2,3
    factor = [1.0, 0.72, 0.38, 0.0]
    jit = [0.14, 0.16, 0.22, 0.30]
    M = len(xs)
    grid = {}
    for i in range(M):
        for j in range(M):
            ring = max(ring_of[i], ring_of[j])
            x, z = xs[i], xs[j]
            on_border = ring == 0 and (i - R in (0, N) or j - R in (0, N))
            if ring > 0 or not on_border:
                x += rnd.uniform(-jit[ring], jit[ring])
                z += rnd.uniform(-jit[ring], jit[ring])
            if ring == 0:
                d = min(HALF - abs(x), HALF - abs(z))
                w = max(0.0, min(1.0, d / 1.1))
                w = w * w * (3 - 2 * w)
                y = base_h(x, z) + w * (sum(fn(x, z) for fn in peaks) + rnd.uniform(-0.18, 0.18))
                if on_border:
                    y = border_h(x if j - R in (0, N) else z)
                y = max(0.05, y)
            else:
                # scree/foothills: continue the shared edge height outward, stepping down to the ground
                hb = base_h(max(-HALF, min(HALF, x)), max(-HALF, min(HALF, z)))
                y = hb * factor[ring] * rnd.uniform(0.8, 1.15) if ring < R + 1 and factor[ring] > 0 else 0.0
            grid[(i, j)] = (x, y, z)
    m = Mesh()
    for i in range(M - 1):
        for j in range(M - 1):
            a, b, c, d = grid[(i, j)], grid[(i, j + 1)], grid[(i + 1, j + 1)], grid[(i + 1, j)]
            quad = [(a, b, c), (a, c, d)] if (i + j) % 2 == 0 else [(a, b, d), (b, c, d)]
            ring = max(min(ring_of[i], ring_of[i + 1]), min(ring_of[j], ring_of[j + 1])) if True else 0
            for t in quad:
                if max(p[1] for p in t) <= 0.001:
                    continue
                ay = sum(p[1] for p in t) / 3 + rnd.uniform(-0.25, 0.25)
                if ring >= 1:
                    mat = "scree" if rnd.random() < 0.5 else "rock_dk"
                else:
                    mat = "rock_dk" if ay < 1.5 else "rock" if ay < 2.4 else "rock_lt" if ay < 3.1 else "snow"
                m.tri(mat, *t, outward=(0, 1, 0))
    return m


def build_mountains():
    tiles = {
        "mountain_a": mountain_tile(11, [lambda x, z: cone(x, z, 0.1, -0.1, 2.4, 2.1),
                                         lambda x, z: cone(x, z, -0.9, 0.9, 1.1, 1.3)]),
        "mountain_b": mountain_tile(23, [lambda x, z: cone(x, z, -0.7, -0.6, 2.0, 1.8),
                                         lambda x, z: cone(x, z, 0.8, 0.7, 1.8, 1.7)]),
        "mountain_c": mountain_tile(37, [lambda x, z: ridge(x, z, (-1.3, 1.1), (1.3, -1.1), 2.2, 1.6),
                                         lambda x, z: cone(x, z, 0.9, 0.9, 0.9, 1.1)]),
    }
    for name, m in tiles.items():
        m.write(name, MTN_MATS)


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
