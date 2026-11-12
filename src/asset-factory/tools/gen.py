"""Generates every asset-factory model into models/ (.obj + .mtl).
Run: python gen.py   (see docs/asset-pipeline.md)
Mountains live here; ground, transitions and rivers in terrain_gen.py; buildings in
building_gen.py; resource field models in resource_gen.py.
"""
import math
import random

from mesh import *  # noqa: F401,F403
import building_gen
import resource_gen
import terrain_gen


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


if __name__ == "__main__":
    build_mountains()
    terrain_gen.build_all()
    building_gen.build_all()
    resource_gen.build_all()
