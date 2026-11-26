"""Compose small test maps from the generated terrain models, placed the way the game's
TerrainTiles does (ground tiles, priority edge and corner pieces, river pieces turned to their
connections), and write them as one OBJ for preview.py.

Run: python arrange.py <layout> <out.obj>     layouts: ground, rivers
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_glb import read_obj  # noqa: E402

MODELS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "models")
PRIORITY = {"grass": 7, "snow": 6, "sand": 5, "salt_flat": 4, "mud": 3, "rock": 2, "lava": 1, "shallow_water": 0,
            "deep_water": 0}
DIRS = {"N": (0, -1), "E": (1, 0), "S": (0, 1), "W": (-1, 0)}
EDGE_YAW = {"N": 0, "W": 90, "S": 180, "E": 270}
ROT = {"N": "W", "W": "S", "S": "E", "E": "N"}  # what +90 degrees does to an edge
CANON = {"pool": (), "end": ("N",), "straight": ("N", "S"), "bend": ("N", "E"), "junction": ("N", "E", "S"),
         "cross": ("N", "E", "S", "W")}


def rot(p, yaw):
    x, y, z = p
    for _ in range(int(yaw // 90) % 4):
        x, z = z, -x  # +90: x' = x cos + z sin, z' = -x sin + z cos
    return (x, y, z)


class Scene:
    def __init__(self):
        self.mats, self.tris = {}, []

    def place(self, name, cx, cz, yaw=0):
        mats, by = read_obj(os.path.join(MODELS, "terrain", name + ".obj"))
        for mname, faces in by.items():
            key = name + "/" + mname
            self.mats[key] = mats[mname]
            for f in faces:
                self.tris.append((key, [tuple(a + b for a, b in zip(rot(p, yaw), (cx * 4, 0, cz * 4))) for p in f]))

    def write(self, path):
        base = os.path.splitext(path)[0]
        with open(base + ".mtl", "w") as f:
            for i, (k, m) in enumerate(self.mats.items()):
                f.write("newmtl m%d\nKd %.3f %.3f %.3f\n" % ((i,) + m["Kd"]))
                if m["Ke"]:
                    f.write("Ke %.3f %.3f %.3f\n" % m["Ke"])
        idx = {k: i for i, k in enumerate(self.mats)}
        with open(base + ".obj", "w") as f:
            f.write("mtllib %s.mtl\n" % os.path.basename(base))
            n = 0
            for k, t in self.tris:
                f.write("usemtl m%d\n" % idx[k])
                for p in t:
                    f.write("v %.4f %.4f %.4f\n" % p)
                f.write("f %d %d %d\n" % (n + 1, n + 2, n + 3))
                n += 3


def compose(terrain, rivers, out):
    """terrain: rows of terrain names (row = z, north first); rivers: {(x, z): flow edge}."""
    h, w = len(terrain), len(terrain[0])
    s = Scene()

    def t(x, z):
        return terrain[z][x] if 0 <= x < w and 0 <= z < h else None

    def connects(x, z, e):
        if (x, z) not in rivers:
            return False
        dx, dz = DIRS[e]
        nx, nz = x + dx, z + dz
        if rivers[(x, z)] == e:
            nt = t(nx, nz)
            return (nx, nz) in rivers or nt is None or "water" in nt
        opp = {"N": "S", "S": "N", "E": "W", "W": "E"}[e]
        return rivers.get((nx, nz)) == opp

    for z in range(h):
        for x in range(w):
            ter = t(x, z)
            if (x, z) in rivers:
                conns = {e for e in DIRS if connects(x, z, e)}
                shape = {0: "pool", 1: "end", 3: "junction", 4: "cross"}.get(len(conns))
                if len(conns) == 2:
                    shape = "straight" if conns in ({"N", "S"}, {"E", "W"}) else "bend"
                for k in range(4):
                    c = set(CANON[shape])
                    for _ in range(k):
                        c = {ROT[e] for e in c}
                    if c == conns:
                        break
                s.place("river_%s_%s" % (shape, ter), x, z, 90 * k)
            else:
                s.place("ground_%s_%s" % (ter, "abc"[(x * 7 + z * 3) % 3]), x, z, 90 * ((x + 2 * z) % 4))
            own = PRIORITY[ter]
            for e, (dx, dz) in DIRS.items():
                nt = t(x + dx, z + dz)
                if nt and PRIORITY[nt] > own:
                    opp = {"N": "S", "S": "N", "E": "W", "W": "E"}[e]
                    mouth = connects(x + dx, z + dz, opp)
                    s.place(("edge_mouth_" if mouth else "edge_") + nt, x, z, EDGE_YAW[e])
            for sx, sz, yaw in ((-1, -1, 0), (-1, 1, 90), (1, 1, 180), (1, -1, 270)):
                best, bp = None, own
                for nx, nz in ((x + sx, z), (x, z + sz), (x + sx, z + sz)):
                    nt = t(nx, nz)
                    if nt and PRIORITY[nt] > bp:
                        best, bp = nt, PRIORITY[nt]
                if best:
                    s.place("corner_" + best, x, z, yaw)
    s.write(out)


LAYOUTS = {
    # Every terrain beside others, shorelines on shallow and deep water, and lava; a salt flat in
    # sand, as the game places one.
    "ground": ([
        ["grass", "grass", "sand", "sand", "snow", "sand", "sand"],
        ["grass", "shallow_water", "shallow_water", "sand", "snow", "sand", "salt_flat"],
        ["mud", "shallow_water", "deep_water", "rock", "rock", "sand", "salt_flat"],
        ["mud", "mud", "lava", "lava", "rock", "sand", "sand"],
    ], {}),
    # Every river shape: a cross and a junction on grass draining into a pond, and a bend,
    # straights, ends and a pool on sand.
    "rivers": ([
        ["grass", "grass", "grass", "sand", "sand"],
        ["grass", "grass", "grass", "sand", "sand"],
        ["grass", "grass", "grass", "sand", "sand"],
        ["grass", "shallow_water", "shallow_water", "sand", "sand"],
    ], {(1, 0): "S", (0, 1): "E", (2, 1): "W", (1, 1): "S", (1, 2): "S", (3, 0): "S", (3, 1): "S", (3, 2): "E",
        (4, 2): "E", (4, 0): "S"}),
}


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in LAYOUTS:
        sys.exit(__doc__)
    compose(*LAYOUTS[sys.argv[1]], sys.argv[2])
