"""Terrain models: ground tiles, transition edge and corner pieces, and river pieces.

Every model is one 4 x 4 map cell (or a piece of one), origin at the cell center, north is -Z.
Ground levels: land surfaces at y = 0, liquid surfaces (water, lava) at y = -0.1. Land carries a
skirt from its surface down to -0.1 on every open side, which shows as a bank beside a liquid.
See docs/blueprints/art-style.md and src/Telaelus/assets/models/terrain/README.md.
"""
import math
import random

from mesh import Materials, Mesh, quad, spike

HALF = 2.0
LIQUID_Y = -0.1
STRIP_Y = 0.006       # transition pieces float just above the ground tile they cover (which is flat near its edges)
STRIP_DEPTH = 0.4     # strip width at its ends and the corner square size

LAND = ["grass", "sand", "snow", "rock", "mud"]
SALT_FLAT = "salt_flat"  # land that outranks nothing it borders, so it has ground tiles only
LIQUID = ["shallow_water", "deep_water", "lava"]
OUTRANKING = ["grass", "snow", "sand", "mud", "rock", "lava"]  # terrains that draw transition pieces
RIVER_BANKS = ["grass", "sand", "snow", "mud", "rock"]
RIVER_SHAPES = {
    # canonical connections (N, E, S, W); the game rotates the piece to the cell's connections
    "pool": (),
    "end": ("N",),
    "straight": ("N", "S"),
    "bend": ("N", "E"),
    "junction": ("N", "E", "S"),
    "cross": ("N", "E", "S", "W"),
}
SHADES = (0.94, 1.0, 1.05)


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def edge_dist(x, z):
    return min(HALF - abs(x), HALF - abs(z))


def grid_surface(m, rnd, n, height, pick, jitter=0.08):
    """A heightfield over the cell. Border vertices stay exactly on the grid so tiles meet."""
    step = 2 * HALF / n
    pts = {}
    for i in range(n + 1):
        for j in range(n + 1):
            x, z = -HALF + i * step, -HALF + j * step
            if 0 < i < n and 0 < j < n and jitter:
                x += rnd.uniform(-jitter, jitter)
                z += rnd.uniform(-jitter, jitter)
            pts[(i, j)] = (x, height(x, z, i, j), z)
    for i in range(n):
        for j in range(n):
            a, b, c, d = pts[(i, j)], pts[(i, j + 1)], pts[(i + 1, j + 1)], pts[(i + 1, j)]
            for t in ([(a, b, c), (a, c, d)] if (i + j) % 2 == 0 else [(a, b, d), (b, c, d)]):
                m.tri(pick(t), *t, outward=(0, 1, 0))
    return pts


def skirt(m, mat, pts, n, bottom=LIQUID_Y):
    """Vertical walls from the border of a grid surface down to `bottom`, facing out."""
    sides = [
        ([(0, k) for k in range(n + 1)], (-1, 0, 0)),
        ([(n, k) for k in range(n + 1)], (1, 0, 0)),
        ([(k, 0) for k in range(n + 1)], (0, 0, -1)),
        ([(k, n) for k in range(n + 1)], (0, 0, 1)),
    ]
    for keys, out in sides:
        for ka, kb in zip(keys, keys[1:]):
            a, b = pts[ka], pts[kb]
            if a[1] > bottom + 1e-4 or b[1] > bottom + 1e-4:
                quad(m, mat, (a[0], bottom, a[2]), a, b, (b[0], bottom, b[2]), out)


def wall(m, mat, a, b, top, bottom, toward):
    """A vertical wall under the segment a-b (x, z), from `top` down to `bottom`."""
    quad(m, mat, (a[0], bottom, a[1]), (a[0], top, a[1]), (b[0], top, b[1]), (b[0], bottom, b[1]),
         (toward[0], 0, toward[1]))


def disc(m, mat_top, mat_side, rnd, cx, cz, r, y, n=7, base=0.0):
    """An irregular flat polygon at height y, with sides down to `base`."""
    pts = []
    for k in range(n):
        t = 2 * math.pi * (k + rnd.uniform(-0.25, 0.25)) / n
        rr = r * rnd.uniform(0.7, 1.15)
        pts.append((cx + math.cos(t) * rr, cz + math.sin(t) * rr))
    for k in range(n):
        p, q = pts[k], pts[(k + 1) % n]
        m.tri(mat_top, (cx, y, cz), (p[0], y, p[1]), (q[0], y, q[1]), outward=(0, 1, 0))
        if y - base > 0.002:
            quad(m, mat_side, (p[0], base, p[1]), (p[0], y, p[1]), (q[0], y, q[1]), (q[0], base, q[1]),
                 (p[0] + q[0] - 2 * cx, 0, p[1] + q[1] - 2 * cz))


# ---------------------------------------------------------------- details
def detail_spots(rnd, count, margin, keep):
    spots, tries = [], 0
    while len(spots) < count and tries < count * 40:
        tries += 1
        x, z = rnd.uniform(-HALF + margin, HALF - margin), rnd.uniform(-HALF + margin, HALF - margin)
        if keep(x, z) and all(math.hypot(x - a, z - b) > 0.35 for a, b in spots):
            spots.append((x, z))
    return spots


def add_details(m, mat, terrain, rnd, keep=lambda x, z: True, margin=0.6):
    # Details stay nearly flat: steep faces turned from the sun read as black specks in the game.
    if terrain == "grass":
        for x, z in detail_spots(rnd, 9, margin, keep):
            disc(m, mat("grass", rnd.choice((1.12, 1.2, 0.9))), mat("grass", 0.9), rnd, x, z,
                 rnd.uniform(0.14, 0.24), 0.008, n=6)
    elif terrain == "sand":
        for x, z in detail_spots(rnd, 5, margin, keep):
            length, ang = rnd.uniform(0.6, 1.0), rnd.uniform(-0.3, 0.3)
            ux, uz = math.cos(ang) * length / 2, math.sin(ang) * length / 2
            nx, nz = -uz / length * 0.16, ux / length * 0.16
            a, b = (x - ux, 0.0, z - uz), (x + ux, 0.0, z + uz)
            ridge_a, ridge_b = (x - ux * 0.9, 0.035, z - uz * 0.9), (x + ux * 0.9, 0.035, z + uz * 0.9)
            quad(m, mat("sand", 1.08), (a[0] - nx, 0, a[2] - nz), (b[0] - nx, 0, b[2] - nz), ridge_b, ridge_a,
                 (-nx, 0.5, -nz))
            quad(m, mat("sand", 0.9), (a[0] + nx, 0, a[2] + nz), (b[0] + nx, 0, b[2] + nz), ridge_b, ridge_a,
                 (nx, 0.5, nz))
    elif terrain == "snow":
        for x, z in detail_spots(rnd, 4, margin, keep):
            spike(m, mat("snow", 1.04), (x, 0.0, z), (x + rnd.uniform(-0.1, 0.1), rnd.uniform(0.06, 0.1), z),
                  rnd.uniform(0.3, 0.45), segs=6)
    elif terrain == "rock":
        for x, z in detail_spots(rnd, 3, margin, keep):
            disc(m, mat("stone_dark", rnd.choice((0.95, 1.1))), mat("stone_dark", 0.8), rnd, x, z,
                 rnd.uniform(0.25, 0.4), rnd.uniform(0.03, 0.06))
        for x, z in detail_spots(rnd, 6, margin, keep):
            spike(m, mat("rock", rnd.choice((0.85, 1.15))), (x, 0.0, z), (x, 0.025, z), 0.08, segs=5)
    elif terrain == SALT_FLAT:
        salt_cracks(m, mat, rnd, 0.5)
    elif terrain == "mud":
        for x, z in detail_spots(rnd, 3, margin, keep):
            disc(m, mat("puddle"), mat("puddle"), rnd, x, z, rnd.uniform(0.22, 0.38), 0.006)
        for x, z in detail_spots(rnd, 4, margin, keep):
            spike(m, mat("mud", 0.85), (x, 0.0, z), (x, 0.03, z), rnd.uniform(0.15, 0.22), segs=5)


def salt_cracks(m, mat, rnd, margin):
    """Thin, low ridges along the edges of an irregular hexagon pattern, like the polygons of a
    dried salt crust. Shared hexagon corners move together, so neighboring polygons still meet."""
    r = 0.42
    inner = HALF - margin
    jitter_seed = rnd.random()

    def corner(x, z):
        key = (round(x * 1000), round(z * 1000))
        j = random.Random(hash((key, jitter_seed)))
        return (x + j.uniform(-0.1, 0.1), z + j.uniform(-0.1, 0.1))

    edges = set()
    dx_hex, dz_hex = r * math.sqrt(3), r * 1.5
    for row in range(-4, 5):
        for col in range(-4, 5):
            cx = col * dx_hex + (row % 2) * dx_hex / 2
            cz = row * dz_hex
            pts = [(round(cx + r * math.cos(math.radians(60 * k + 30)), 3),
                    round(cz + r * math.sin(math.radians(60 * k + 30)), 3)) for k in range(6)]
            for k in range(6):
                a, b = pts[k], pts[(k + 1) % 6]
                edges.add((min(a, b), max(a, b)))
    width, height = 0.03, 0.012
    for a0, b0 in sorted(edges):
        a, b = corner(*a0), corner(*b0)
        if max(abs(a[0]), abs(a[1]), abs(b[0]), abs(b[1])) > inner or rnd.random() < 0.15:
            continue
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dz)
        nx, nz = -dz / length * width, dx / length * width
        top_a, top_b = (a[0], height, a[1]), (b[0], height, b[1])
        quad(m, mat("salt_crack"), (a[0] - nx, 0, a[1] - nz), (b[0] - nx, 0, b[1] - nz), top_b, top_a,
             (-nx, 0.5, -nz))
        quad(m, mat("salt_crack", 0.9), (a[0] + nx, 0, a[1] + nz), (b[0] + nx, 0, b[1] + nz), top_b, top_a,
             (nx, 0.5, nz))


# ---------------------------------------------------------------- ground tiles
def land_tile(terrain, seed):
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    relief = 0.008 if terrain == SALT_FLAT else 0.035  # a salt flat is nearly level

    def height(x, z, i, j):
        if i in (0, 8) or j in (0, 8):
            return 0.0
        # Flat near the edges, where transition strips lie; gentle relief further in.
        return rnd.uniform(-relief, relief) * smoothstep((edge_dist(x, z) - 0.45) / 0.6)

    pts = grid_surface(m, rnd, 8, height, lambda t: mat(terrain, rnd.choice(SHADES)))
    skirt(m, mat(terrain, 0.72), pts, 8)
    add_details(m, mat, terrain, rnd)
    return m, mat.mats


def liquid_tile(terrain, seed):
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    if terrain == "lava":
        seeds = [(rnd.uniform(-HALF, HALF), rnd.uniform(-HALF, HALF)) for _ in range(7)]

        def crust(x, z):
            d = sorted(math.hypot(x - a, z - b) for a, b in seeds)
            return d[1] - d[0] > 0.22

        def height(x, z, i, j):
            if i in (0, 10) or j in (0, 10):
                return LIQUID_Y
            return LIQUID_Y + (0.03 if crust(x, z) else 0.0)

        def pick(t):
            if all(p[1] > LIQUID_Y + 0.01 for p in t):
                return mat("lava_crust", rnd.choice(SHADES))
            return mat("lava")

        grid_surface(m, rnd, 10, height, pick, jitter=0.1)
    else:
        def height(x, z, i, j):
            if i in (0, 8) or j in (0, 8):
                return LIQUID_Y
            return LIQUID_Y + rnd.uniform(-0.012, 0.012)

        grid_surface(m, rnd, 8, height, lambda t: mat(terrain, rnd.choice((0.95, 1.0, 1.04))))
    return m, mat.mats


# ---------------------------------------------------------------- transitions
def surface_y(terrain):
    return LIQUID_Y + 0.01 if terrain in LIQUID else STRIP_Y


def ragged(rnd, xs):
    """Inner-edge depth for each x sample: STRIP_DEPTH at the ends, ragged between."""
    ds = []
    for k, x in enumerate(xs):
        if k in (0, len(xs) - 1):
            ds.append(STRIP_DEPTH)
        else:
            ds.append(STRIP_DEPTH + rnd.uniform(-0.1, 0.14))
    return ds


def strip(m, mat, terrain, rnd, x0, x1, y):
    """One run of a north-edge strip from x0 to x1, with walls under its open sides."""
    n = max(2, int(round((x1 - x0) / 0.2)))
    xs = [x0 + (x1 - x0) * k / n for k in range(n + 1)]
    ds = ragged(rnd, xs)
    for k in range(n):
        a, b = (xs[k], y, -HALF), (xs[k + 1], y, -HALF)
        c, d = (xs[k + 1], y, -HALF + ds[k + 1]), (xs[k], y, -HALF + ds[k])
        quad(m, mat(terrain, rnd.choice(SHADES)), a, b, c, d, (0, 1, 0))
        if terrain in LAND:
            wall(m, mat(terrain, 0.72), (xs[k], -HALF + ds[k]), (xs[k + 1], -HALF + ds[k + 1]), y, LIQUID_Y, (0, 1))
    # Outer edge, in the strip's own color: closes the gap under the raised strip, which
    # otherwise shows the tile beneath as a hairline.
    wall(m, mat(terrain, 1.0), (x0, -HALF), (x1, -HALF), y, LIQUID_Y, (0, -1))
    if terrain in LAND:
        wall(m, mat(terrain, 0.72), (x0, -HALF), (x0, -HALF + ds[0]), y, LIQUID_Y, (-1, 0))
        wall(m, mat(terrain, 0.72), (x1, -HALF), (x1, -HALF + ds[-1]), y, LIQUID_Y, (1, 0))


def edge_piece(terrain, seed, mouth=False):
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    y = surface_y(terrain)
    inner = HALF - STRIP_DEPTH
    if mouth:
        strip(m, mat, terrain, rnd, -inner, -1.0, y)
        strip(m, mat, terrain, rnd, 1.0, inner, y)
    else:
        strip(m, mat, terrain, rnd, -inner, inner, y)
    return m, mat.mats


def corner_piece(terrain, seed):
    """The north-west corner square, with a small rounded bulge toward the cell's middle."""
    m, mat = Mesh(), Materials()
    y = surface_y(terrain)
    c = -HALF + STRIP_DEPTH
    outline = [(-HALF, -HALF), (c, -HALF), (c, c - 0.1), (c + 0.1, c + 0.1), (c - 0.1, c), (-HALF, c)]
    centre = (-HALF + STRIP_DEPTH * 0.5, -HALF + STRIP_DEPTH * 0.5)
    for k in range(len(outline)):
        p, q = outline[k], outline[(k + 1) % len(outline)]
        m.tri(mat(terrain, 1.0), (centre[0], y, centre[1]), (p[0], y, p[1]), (q[0], y, q[1]), outward=(0, 1, 0))
    wall(m, mat(terrain, 1.0), outline[0], outline[1], y, LIQUID_Y, (0, -1))
    wall(m, mat(terrain, 1.0), outline[-1], outline[0], y, LIQUID_Y, (-1, 0))
    if terrain in LAND:
        for p, q in zip(outline[1:], outline[2:]):
            wall(m, mat(terrain, 0.72), p, q, y, LIQUID_Y, ((p[0] + q[0]) / 2 - centre[0], (p[1] + q[1]) / 2 - centre[1]))
    return m, mat.mats


# ---------------------------------------------------------------- rivers
def in_channel(x, z, conns):
    """The sim's channel (middle 2 x 2 plus a 2-wide arm per connection), with arms running on
    past the cell edge so a connected edge is not treated as a bank."""
    if abs(x) <= 1 and abs(z) <= 1:
        return True
    return (("N" in conns and abs(x) <= 1 and z <= -1) or ("S" in conns and abs(x) <= 1 and z >= 1)
            or ("E" in conns and abs(z) <= 1 and x >= 1) or ("W" in conns and abs(z) <= 1 and x <= -1))


def river_piece(shape, terrain, seed):
    rnd = random.Random(seed)
    m, mat = Mesh(), Materials()
    conns = RIVER_SHAPES[shape]
    n, step = 16, 0.25

    def deep(x, z):
        return all(in_channel(x + dx * step, z + dz * step, conns) for dx in (-1, 0, 1) for dz in (-1, 0, 1))

    def height(x, z, i, j):
        return LIQUID_Y if deep(x, z) else 0.0

    def pick(t):
        ys = [p[1] for p in t]
        if max(ys) <= LIQUID_Y + 1e-6:
            return mat("shallow_water", rnd.choice((0.97, 1.0, 1.03)))
        if min(ys) < -1e-6:
            return mat("wet_earth", rnd.choice(SHADES))
        return mat(terrain, rnd.choice(SHADES))

    pts = grid_surface(m, rnd, n, height, pick, jitter=0)
    skirt(m, mat(terrain, 0.72), pts, n)

    def bank(x, z):
        return all(not in_channel(x + dx, z + dz, conns) for dx in (-0.45, 0, 0.45) for dz in (-0.45, 0, 0.45))

    add_details(m, mat, terrain, rnd, keep=bank, margin=0.5)
    return m, mat.mats


# ---------------------------------------------------------------- build
def build_all():
    for k, terrain in enumerate(LAND + LIQUID):
        for v, suffix in enumerate("abc"):
            seed = 1000 + k * 10 + v
            m, mats = land_tile(terrain, seed) if terrain in LAND else liquid_tile(terrain, seed)
            m.write("terrain/ground_%s_%s" % (terrain, suffix), mats)
    for k, terrain in enumerate(OUTRANKING):
        edge = edge_piece(terrain, 2000 + k)
        edge[0].write("terrain/edge_" + terrain, edge[1])
        mouth = edge_piece(terrain, 2100 + k, mouth=True)
        mouth[0].write("terrain/edge_mouth_" + terrain, mouth[1])
        corner = corner_piece(terrain, 2200 + k)
        corner[0].write("terrain/corner_" + terrain, corner[1])
    for k, terrain in enumerate(RIVER_BANKS):
        for s, shape in enumerate(RIVER_SHAPES):
            m, mats = river_piece(shape, terrain, 3000 + k * 10 + s)
            m.write("terrain/river_%s_%s" % (shape, terrain), mats)
    build_salt_flat()


def build_salt_flat():
    for v, suffix in enumerate("abc"):
        m, mats = land_tile(SALT_FLAT, 1500 + v)
        m.write("terrain/ground_%s_%s" % (SALT_FLAT, suffix), mats)
