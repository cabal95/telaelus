"""Building models: one per building type, authored facing North (front toward -Z).

Each model fills its footprint (in fine cells, 1 unit each) minus a 0.1 inset per side, origin at
the footprint center on the ground. The front marks the building's facing; the accent color is
the building's identity color. See docs/blueprints/art-style.md and
src/Game/assets/models/buildings/README.md.
"""
import math

from mesh import Materials, Mesh, box, cylinder, prism_roof, quad, spike

INSET = 0.1


def half(cells):
    return cells / 2 - INSET


def mine():
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone"))
    # shaft opening under the headframe
    box(m, mat("stone_dark", 0.8), -0.42, 0.08, -0.05, 0.42, 0.14, 0.75)
    box(m, mat("shadow"), -0.32, 0.14, 0.05, 0.32, 0.141, 0.65)
    # headframe: two A-frames of timber over the shaft, a crossbeam, and the winding wheel
    for x in (-0.4, 0.4):
        cylinder(m, mat("wood_dark"), (x, 0.1, 0.05), (x * 0.5, 1.25, 0.35), 0.05, segs=6)
        cylinder(m, mat("wood_dark"), (x, 0.1, 0.65), (x * 0.5, 1.25, 0.35), 0.05, segs=6)
    cylinder(m, mat("wood"), (-0.3, 1.2, 0.35), (0.3, 1.2, 0.35), 0.05, segs=6)
    cylinder(m, mat("mine_accent"), (-0.05, 1.32, 0.35), (0.05, 1.32, 0.35), 0.2, segs=10)
    cylinder(m, mat("metal_dark"), (-0.12, 1.32, 0.35), (0.12, 1.32, 0.35), 0.04, segs=6)
    # rails and an ore cart at the front
    for x in (-0.15, 0.15):
        box(m, mat("metal_dark"), x - 0.02, 0.08, -hz, x + 0.02, 0.11, 0.0)
    box(m, mat("mine_accent"), -0.24, 0.16, -0.72, 0.24, 0.42, -0.36, mat_top=mat("mine_accent", 0.85))
    for x in (-0.18, 0.18):
        for z in (-0.66, -0.42):
            cylinder(m, mat("metal_dark"), (x - 0.07, 0.12, z), (x + 0.07, 0.12, z), 0.05, segs=6)
    for k, (x, z) in enumerate([(-0.1, -0.6), (0.08, -0.5), (0.0, -0.62), (0.12, -0.64)]):
        spike(m, mat("rock", 0.9 if k % 2 else 1.1), (x, 0.4, z), (x, 0.5, z), 0.09, segs=5)
    return m, mat.mats


def lumber_camp():
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("wood_dark"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("mud", 1.1))
    # plank shed at the back with a gabled roof
    box(m, mat("wood"), -0.75, 0.05, 0.05, 0.75, 0.75, 0.85)
    for x in (-0.45, -0.15, 0.15, 0.45):
        box(m, mat("wood_dark"), x - 0.015, 0.05, 0.04, x + 0.015, 0.75, 0.05)
    box(m, mat("shadow"), -0.2, 0.05, 0.04, 0.2, 0.5, 0.045)
    prism_roof(m, mat("lumber_accent"), -0.85, 0.85, -0.03, 0.93, 0.75, 1.15, mat_gable=mat("wood"))
    # log pile on the front left
    for k, (y, z) in enumerate([(0.17, -0.55), (0.17, -0.3), (0.38, -0.43)]):
        cylinder(m, mat("wood_dark"), (-0.8, y, z), (-0.1, y, z), 0.12, segs=7, mat_cap=mat("wood", 1.25))
    # saw bench on the front right
    box(m, mat("wood"), 0.15, 0.05, -0.7, 0.75, 0.32, -0.35)
    cylinder(m, mat("metal"), (0.45, 0.42, -0.535), (0.45, 0.42, -0.515), 0.16, segs=10)
    cylinder(m, mat("wood_dark"), (0.2, 0.42, -0.53), (0.7, 0.42, -0.53), 0.07, segs=6, mat_cap=mat("wood", 1.25))
    return m, mat.mats


def smelter():
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.1, hz)
    # furnace block, stepped in, with an accent band
    box(m, mat("stone"), -1.15, 0.1, -1.05, 1.15, 1.1, 1.2, mat_top=mat("stone", 0.9))
    box(m, mat("smelter_accent"), -1.17, 0.85, -1.07, 1.17, 1.0, 1.22)
    box(m, mat("stone", 0.92), -0.85, 1.1, -0.65, 0.85, 1.5, 0.95)
    # glowing mouth in the front face, under a dark arch
    box(m, mat("stone_dark"), -0.5, 0.1, -1.18, 0.5, 0.75, -1.05)
    quad(m, mat("furnace_glow"), (-0.36, 0.14, -1.181), (0.36, 0.14, -1.181), (0.36, 0.6, -1.181),
         (-0.36, 0.6, -1.181), (0, 0, -1))
    # chimney at the back right
    cylinder(m, mat("stone_dark"), (0.6, 1.1, 0.6), (0.6, 2.2, 0.6), 0.28, segs=8)
    cylinder(m, mat("smelter_accent"), (0.6, 1.95, 0.6), (0.6, 2.05, 0.6), 0.31, segs=8)
    cylinder(m, mat("shadow"), (0.6, 2.2, 0.6), (0.6, 2.201, 0.6), 0.2, segs=8)
    # ore heap by the front left corner
    spike(m, mat("rock"), (-0.95, 0.1, -1.0), (-0.95, 0.42, -1.0), 0.3, segs=6)
    return m, mat.mats


def refinery():
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    # two tanks at the back, accent stripes and domed tops
    for z, h in ((0.2, 1.6), (0.95, 1.85)):
        cylinder(m, mat("metal"), (0, 0.08, z), (0, h, z), 0.42, segs=12)
        cylinder(m, mat("refinery_accent"), (0, h * 0.55, z), (0, h * 0.55 + 0.18, z), 0.43, segs=12)
        spike(m, mat("metal", 1.1), (0, h, z), (0, h + 0.18, z), 0.42, segs=12)
    # pipes joining the tanks and running to the hut
    cylinder(m, mat("metal_dark"), (0.3, 1.1, 0.2), (0.3, 1.1, 0.95), 0.06, segs=6)
    cylinder(m, mat("metal_dark"), (-0.32, 0.5, -0.6), (-0.32, 0.5, 0.2), 0.06, segs=6)
    cylinder(m, mat("metal_dark"), (-0.32, 0.5, 0.2), (-0.32, 0.9, 0.2), 0.06, segs=6)
    # control hut at the front
    box(m, mat("stone"), -0.7, 0.08, -1.3, 0.55, 0.65, -0.6)
    box(m, mat("refinery_accent"), -0.76, 0.65, -1.36, 0.61, 0.75, -0.54)
    box(m, mat("shadow"), -0.15, 0.08, -1.301, 0.15, 0.45, -1.3)
    box(m, mat("metal_dark"), 0.25, 0.35, -1.301, 0.45, 0.5, -1.3)
    return m, mat.mats


BUILDINGS = {"mine": mine, "lumber_camp": lumber_camp, "smelter": smelter, "refinery": refinery}


def build_all():
    for name, fn in BUILDINGS.items():
        m, mats = fn()
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
    _ = math
