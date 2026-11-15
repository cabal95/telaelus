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


def oil_well():
    """A pumpjack over a wellhead at the front, its crank and counterweight at the back, and a tank."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone", 0.8))
    # wellhead at the front
    cylinder(m, mat("metal_dark"), (0.0, 0.06, -0.62), (0.0, 0.3, -0.62), 0.09, segs=8)
    cylinder(m, mat("oil_well_accent"), (0.0, 0.3, -0.62), (0.0, 0.36, -0.62), 0.12, segs=8)
    # samson post: an A-frame holding the walking beam at its middle
    for x in (-0.16, 0.16):
        cylinder(m, mat("metal_dark"), (x, 0.06, -0.25), (x * 0.4, 0.95, -0.05), 0.035, segs=6)
        cylinder(m, mat("metal_dark"), (x, 0.06, 0.15), (x * 0.4, 0.95, -0.05), 0.035, segs=6)
    # walking beam, horsehead over the wellhead, crank and counterweight at the back
    box(m, mat("oil_well_accent"), -0.06, 0.92, -0.62, 0.06, 1.02, 0.5)
    box(m, mat("oil"), -0.08, 0.7, -0.78, 0.08, 1.06, -0.6, mat_top=mat("oil_well_accent"))
    cylinder(m, mat("metal_dark"), (0.0, 0.66, -0.7), (0.0, 0.36, -0.62), 0.015, segs=4)
    box(m, mat("metal_dark"), -0.22, 0.06, 0.35, 0.22, 0.42, 0.55)
    box(m, mat("oil"), -0.24, 0.2, 0.42, 0.24, 0.62, 0.72)
    cylinder(m, mat("metal_dark"), (0.0, 0.62, 0.55), (0.0, 0.95, 0.5), 0.03, segs=5)
    # storage tank on the side
    cylinder(m, mat("metal"), (0.58, 0.06, -0.05), (0.58, 0.62, -0.05), 0.25, segs=10)
    cylinder(m, mat("oil_well_accent"), (0.58, 0.4, -0.05), (0.58, 0.48, -0.05), 0.26, segs=10)
    cylinder(m, mat("metal_dark"), (0.1, 0.18, -0.62), (0.45, 0.18, -0.15), 0.03, segs=5)
    return m, mat.mats


def hq():
    """The landing base: a long hall with an entrance at the front, a comms tower at the back, and a
    landing pad before the door."""
    m, mat = Mesh(), Materials()
    hx, hz = half(6), half(10)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.1, hz, mat_top=mat("stone", 0.9))
    # landing pad at the front
    cylinder(m, mat("metal_dark"), (0, 0.1, -3.6), (0, 0.14, -3.6), 1.15, segs=16)
    cylinder(m, mat("hq_accent"), (0, 0.14, -3.6), (0, 0.15, -3.6), 0.95, segs=16)
    cylinder(m, mat("metal_dark"), (0, 0.15, -3.6), (0, 0.16, -3.6), 0.8, segs=16)
    # the hall, with an accent band, a shallow roof, and an entrance under an awning
    box(m, mat("hull"), -2.4, 0.1, -2.3, 2.4, 1.5, 2.6)
    box(m, mat("hq_accent"), -2.42, 1.1, -2.32, 2.42, 1.3, 2.62)
    prism_roof(m, mat("hull_dark"), -2.5, 2.5, -2.4, 2.7, 1.5, 2.0, mat_gable=mat("hull"))
    box(m, mat("shadow"), -0.5, 0.1, -2.31, 0.5, 0.95, -2.3)
    box(m, mat("hq_accent"), -0.8, 1.0, -2.75, 0.8, 1.08, -2.3)
    for x in (-1.7, -1.1, 1.1, 1.7):
        box(m, mat("screen"), x - 0.2, 0.6, -2.31, x + 0.2, 0.9, -2.3)
    # comms tower at the back, with a dish and a mast
    cylinder(m, mat("hull"), (0, 0.1, 3.7), (0, 3.0, 3.7), 0.62, segs=12, mat_cap=mat("hull_dark"))
    cylinder(m, mat("hq_accent"), (0, 2.2, 3.7), (0, 2.45, 3.7), 0.64, segs=12)
    cylinder(m, mat("metal"), (0, 3.0, 3.7), (0, 3.9, 3.7), 0.05, segs=6)
    spike(m, mat("band_red"), (0, 3.9, 3.7), (0, 4.1, 3.7), 0.06, segs=5)
    cylinder(m, mat("metal_dark"), (0.3, 3.0, 3.5), (0.55, 3.25, 3.25), 0.05, segs=6)
    cylinder(m, mat("hull_dark"), (0.55, 3.25, 3.25), (0.75, 3.45, 3.05), 0.5, segs=12)
    return m, mat.mats


def silo():
    """A tall round silo with a conical cap, an accent band, and a ladder up its front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone"))
    cylinder(m, mat("metal"), (0, 0.08, 0.05), (0, 2.0, 0.05), 0.66, segs=14)
    for y in (0.6, 1.3):
        cylinder(m, mat("metal", 0.85), (0, y, 0.05), (0, y + 0.04, 0.05), 0.67, segs=14, caps=False)
    cylinder(m, mat("silo_accent"), (0, 1.6, 0.05), (0, 1.8, 0.05), 0.68, segs=14)
    spike(m, mat("silo_accent", 0.85), (0, 2.0, 0.05), (0, 2.5, 0.05), 0.7, segs=14)
    for x in (-0.12, 0.12):
        box(m, mat("metal_dark"), x - 0.02, 0.08, -0.66, x + 0.02, 2.0, -0.62)
    for k in range(8):
        y = 0.25 + k * 0.22
        box(m, mat("metal_dark"), -0.12, y, -0.66, 0.12, y + 0.02, -0.62)
    return m, mat.mats


def stockyard():
    """A fenced yard of material heaps (stone, ore, logs), open at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("mud", 1.15))
    # fence along the back and sides, rails in the accent color
    for x0, z0, x1, z1 in ((-hx, hz - 0.08, hx, hz), (-hx, -hz, -hx + 0.08, hz), (hx - 0.08, -hz, hx, hz)):
        box(m, mat("stockyard_accent"), x0, 0.42, z0, x1, 0.5, z1)
        box(m, mat("stockyard_accent"), x0, 0.22, z0, x1, 0.28, z1)
    for x, z in ((-hx + 0.04, -hz + 0.04), (hx - 0.04, -hz + 0.04), (-hx + 0.04, hz - 0.04), (hx - 0.04, hz - 0.04),
                 (0, hz - 0.04), (-hx + 0.04, 0), (hx - 0.04, 0)):
        box(m, mat("wood_dark"), x - 0.06, 0.06, z - 0.06, x + 0.06, 0.62, z + 0.06)
    # heaps
    spike(m, mat("boulder"), (-0.75, 0.06, 0.55), (-0.75, 0.75, 0.55), 0.62, segs=7)
    spike(m, mat("iron_rust"), (0.75, 0.06, 0.6), (0.75, 0.6, 0.6), 0.55, segs=7)
    for y, z in ((0.17, -0.55), (0.17, -0.28), (0.4, -0.42)):
        cylinder(m, mat("bark"), (-0.2, y, z), (0.9, y, z), 0.13, segs=7, mat_cap=mat("log_end"))
    return m, mat.mats


def tank_farm():
    """Three tanks joined by pipes, with accent bands and a valve box at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    for x, z, r, h in ((-0.62, 0.55, 0.62, 1.35), (0.68, 0.5, 0.56, 1.15), (0.55, -0.6, 0.42, 0.9)):
        cylinder(m, mat("metal"), (x, 0.08, z), (x, h, z), r, segs=14)
        cylinder(m, mat("tank_farm_accent"), (x, h * 0.6, z), (x, h * 0.6 + 0.16, z), r + 0.01, segs=14)
        spike(m, mat("metal", 1.1), (x, h, z), (x, h + 0.16, z), r, segs=14)
    cylinder(m, mat("metal_dark"), (-0.62, 0.35, 0.55), (0.68, 0.35, 0.5), 0.06, segs=6)
    cylinder(m, mat("metal_dark"), (0.55, 0.3, -0.6), (0.6, 0.3, 0.5), 0.06, segs=6)
    cylinder(m, mat("metal_dark"), (-0.62, 0.3, 0.55), (-0.62, 0.3, -0.75), 0.06, segs=6)
    box(m, mat("metal_dark"), -0.85, 0.08, -1.2, -0.35, 0.45, -0.85, mat_top=mat("tank_farm_accent"))
    cylinder(m, mat("band_red"), (-0.6, 0.45, -1.02), (-0.6, 0.55, -1.02), 0.1, segs=8)
    return m, mat.mats


def warehouse():
    """A long shed with a gabled roof, a roller door at the front, and crates beside it."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.9))
    box(m, mat("metal"), -1.25, 0.08, -0.85, 1.25, 1.15, 1.3)
    prism_roof(m, mat("warehouse_accent"), -1.35, 1.35, -0.95, 1.4, 1.15, 1.75, mat_gable=mat("metal"))
    box(m, mat("metal_dark"), -0.55, 0.08, -0.86, 0.55, 0.85, -0.85)
    for k in range(5):
        y = 0.16 + k * 0.15
        box(m, mat("metal", 0.85), -0.55, y, -0.87, 0.55, y + 0.03, -0.86)
    for x, z, s in ((-1.0, -1.15, 0.2), (-0.62, -1.18, 0.16), (0.95, -1.12, 0.22)):
        box(m, mat("wood"), x - s, 0.08, z - s, x + s, 0.08 + 2 * s, z + s, mat_top=mat("wood", 1.2))
    return m, mat.mats


def vault():
    """A squat armored bunker with a round vault door in its front and hazard bands."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.1, hz, mat_top=mat("stone", 0.8))
    box(m, mat("concrete_dark"), -1.2, 0.1, -1.0, 1.2, 0.95, 1.2, mat_top=mat("concrete"))
    box(m, mat("concrete"), -0.9, 0.95, -0.7, 0.9, 1.25, 0.9, mat_top=mat("concrete", 1.08))
    for y in (0.15, 0.78):
        box(m, mat("vault_accent"), -1.22, y, -1.02, 1.22, y + 0.1, 1.22)
    cylinder(m, mat("vault_accent"), (0, 0.5, -1.0), (0, 0.5, -1.08), 0.42, segs=14)
    cylinder(m, mat("metal"), (0, 0.5, -1.08), (0, 0.5, -1.14), 0.34, segs=14, mat_cap=mat("metal", 1.15))
    for a in (0, math.pi / 2):
        x, y = math.cos(a) * 0.22, math.sin(a) * 0.22
        cylinder(m, mat("metal_dark"), (-x, 0.5 - y, -1.15), (x, 0.5 + y, -1.15), 0.03, segs=5)
    cylinder(m, mat("uranium_glow"), (0.75, 1.25, 0.2), (0.75, 1.32, 0.2), 0.08, segs=8)
    return m, mat.mats


def small_hub():
    """A short mast on a square base, with a glowing ring near its top."""
    m, mat = Mesh(), Materials()
    hx, hz = half(1), half(1)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone"))
    box(m, mat("metal_dark"), -0.18, 0.08, -0.18, 0.18, 0.3, 0.18, mat_top=mat("metal"))
    cylinder(m, mat("metal"), (0, 0.3, 0), (0, 1.35, 0), 0.05, segs=6)
    cylinder(m, mat("hub_accent"), (0, 1.1, 0), (0, 1.18, 0), 0.2, segs=10)
    cylinder(m, mat("metal_dark"), (0, 1.35, 0), (0, 1.4, 0), 0.09, segs=8)
    spike(m, mat("hub_accent"), (0, 1.4, 0), (0, 1.55, 0), 0.05, segs=5)
    return m, mat.mats


def relay():
    """A tall lattice tower with a dish facing out and the glowing hub ring."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.9))
    top = 2.6
    legs = [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)]
    for x, z in legs:
        cylinder(m, mat("metal_dark"), (x, 0.08, z), (x * 0.25, top, z * 0.25), 0.04, segs=5)
    for k in range(4):
        y0 = 0.4 + k * 0.55
        s0 = 0.5 - (y0 - 0.08) / (top - 0.08) * 0.375
        y1 = y0 + 0.55
        s1 = 0.5 - (y1 - 0.08) / (top - 0.08) * 0.375
        for i in range(4):
            (ax, az), (bx, bz) = legs[i], legs[(i + 1) % 4]
            cylinder(m, mat("metal"), (ax * s0 * 2, y0, az * s0 * 2), (bx * s1 * 2, y1, bz * s1 * 2), 0.02, segs=4)
    cylinder(m, mat("hub_accent"), (0, top - 0.3, 0), (0, top - 0.2, 0), 0.24, segs=10)
    cylinder(m, mat("metal"), (0, top, 0), (0, top + 0.5, 0), 0.04, segs=5)
    spike(m, mat("hub_accent"), (0, top + 0.5, 0), (0, top + 0.68, 0), 0.05, segs=5)
    cylinder(m, mat("metal_dark"), (0, top - 0.7, -0.1), (0, top - 0.7, -0.25), 0.05, segs=5)
    cylinder(m, mat("hull"), (0, top - 0.7, -0.25), (0, top - 0.7, -0.33), 0.32, segs=12, mat_cap=mat("hull_dark"))
    return m, mat.mats


def power_pole(m, mat, x, z, height=1.6):
    """A wooden power pole with a crossbar and a yellow insulator band: the powered tier's mark."""
    cylinder(m, mat("wood_dark"), (x, 0.06, z), (x, height, z), 0.05, segs=6)
    cylinder(m, mat("wood_dark"), (x - 0.25, height - 0.15, z), (x + 0.25, height - 0.15, z), 0.03, segs=4)
    cylinder(m, mat("power_accent"), (x, height - 0.45, z), (x, height - 0.35, z), 0.07, segs=6)


def water_wheel():
    """A paddle wheel on an axle between two stone piers, across the flow (along Z), with a
    generator housing on the east bank."""
    m, mat = Mesh(), Materials()
    radius, half_width = 0.85, 0.55
    centre_y = 0.55
    # piers on the banks, the axle across the channel
    for x in (-1.35, 1.35):
        box(m, mat("stone_dark"), x - 0.32, 0, -0.45, x + 0.32, 0.75, 0.45, mat_top=mat("stone"))
    cylinder(m, mat("metal_dark"), (-1.4, centre_y, 0), (1.4, centre_y, 0), 0.07, segs=6)
    # two rims of segments, spokes, and paddles between them
    segs = 10
    for side in (-half_width, half_width):
        points = [(side, centre_y + radius * math.cos(2 * math.pi * k / segs), radius * math.sin(2 * math.pi * k / segs))
                  for k in range(segs)]
        for k in range(segs):
            cylinder(m, mat("wood_dark"), points[k], points[(k + 1) % segs], 0.04, segs=4)
            cylinder(m, mat("wood"), (side, centre_y, 0), points[k], 0.025, segs=4)
    for k in range(segs):
        a = 2 * math.pi * (k + 0.5) / segs
        y, z = centre_y + radius * math.cos(a), radius * math.sin(a)
        cylinder(m, mat("water_wheel_accent"), (-half_width, y, z), (half_width, y, z), 0.09, segs=4)
    # generator housing on the east pier
    box(m, mat("metal"), 1.12, 0.75, -0.3, 1.58, 1.15, 0.3, mat_top=mat("water_wheel_accent"))
    cylinder(m, mat("power_accent"), (1.35, 1.15, 0), (1.35, 1.25, 0), 0.08, segs=6)
    return m, mat.mats


def burner_generator():
    """A squat boiler with a chimney, a fuel hopper at the front, and a yellow junction box."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    cylinder(m, mat("metal_dark"), (-0.55, 0.45, 0.15), (0.45, 0.45, 0.15), 0.42, segs=12, mat_cap=mat("metal"))
    cylinder(m, mat("burner_accent"), (-0.1, 0.45, 0.15), (0.05, 0.45, 0.15), 0.44, segs=12, caps=False)
    for x in (-0.4, 0.3):
        box(m, mat("metal_dark"), x - 0.08, 0.08, -0.2, x + 0.08, 0.2, 0.5)
    cylinder(m, mat("stone_dark"), (0.5, 0.08, 0.5), (0.5, 1.6, 0.5), 0.16, segs=8)
    cylinder(m, mat("burner_accent"), (0.5, 1.35, 0.5), (0.5, 1.45, 0.5), 0.18, segs=8)
    cylinder(m, mat("shadow"), (0.5, 1.6, 0.5), (0.5, 1.601, 0.5), 0.11, segs=8)
    box(m, mat("metal"), -0.55, 0.08, -0.85, -0.05, 0.55, -0.4, mat_top=mat("coal"))
    box(m, mat("power_accent"), 0.2, 0.08, -0.8, 0.6, 0.45, -0.45, mat_top=mat("metal_dark"))
    power_pole(m, mat, 0.75, -0.75, 1.3)
    return m, mat.mats


def lumber_mill():
    """A covered saw hall with logs feeding in at the front, a yellow powered stripe, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("wood_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("mud", 1.1))
    box(m, mat("wood"), -1.1, 0.06, -0.4, 1.1, 0.95, 1.25)
    box(m, mat("power_accent"), -1.12, 0.7, -0.42, 1.12, 0.8, 1.27)
    prism_roof(m, mat("lumber_accent"), -1.2, 1.2, -0.5, 1.35, 0.95, 1.45, mat_gable=mat("wood"))
    box(m, mat("shadow"), -0.35, 0.06, -0.41, 0.35, 0.6, -0.4)
    # conveyor of logs into the hall
    box(m, mat("metal_dark"), -0.3, 0.06, -1.3, 0.3, 0.3, -0.4, mat_top=mat("metal"))
    for z in (-1.15, -0.85, -0.55):
        cylinder(m, mat("bark"), (-0.35, 0.42, z), (0.35, 0.42, z), 0.11, segs=7, mat_cap=mat("log_end"))
    # sawn plank stack on the side
    for k in range(3):
        y = 0.06 + k * 0.1
        box(m, mat("plank_dark"), 0.55, y, -1.25, 1.25, y + 0.09, -0.75, mat_top=mat("plank"))
    power_pole(m, mat, -1.2, -1.15)
    return m, mat.mats


def drill_rig():
    """A steel derrick over a drill head, a motor house in the mine's orange, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone"))
    top = 2.4
    legs = [(-0.55, -0.55), (0.55, -0.55), (0.55, 0.55), (-0.55, 0.55)]
    for x, z in legs:
        cylinder(m, mat("metal_dark"), (x, 0.08, z), (x * 0.3, top, z * 0.3), 0.05, segs=5)
    for y in (0.7, 1.4):
        s = 0.55 - (y - 0.08) / (top - 0.08) * 0.385
        for i in range(4):
            (ax, az), (bx, bz) = legs[i], legs[(i + 1) % 4]
            cylinder(m, mat("metal"), (ax / 0.55 * s, y, az / 0.55 * s), (bx / 0.55 * s, y, bz / 0.55 * s), 0.03, segs=4)
    box(m, mat("mine_accent"), -0.25, top - 0.1, -0.25, 0.25, top + 0.15, 0.25, mat_top=mat("power_accent"))
    cylinder(m, mat("metal"), (0, 0.08, 0), (0, top - 0.1, 0), 0.08, segs=6)
    cylinder(m, mat("metal_dark"), (0, 0.08, 0), (0, 0.3, 0), 0.22, segs=8)
    # motor house at the front with a yellow band
    box(m, mat("mine_accent"), -1.2, 0.08, -1.25, -0.25, 0.75, -0.7, mat_top=mat("mine_accent", 0.85))
    box(m, mat("power_accent"), -1.22, 0.55, -1.27, -0.23, 0.63, -0.68)
    # ore pile beside the head
    spike(m, mat("iron_rust"), (0.85, 0.08, 0.7), (0.85, 0.45, 0.7), 0.35, segs=6)
    power_pole(m, mat, 1.15, -1.1)
    return m, mat.mats


def water_pump():
    """A small pump house at the back, its intake pipe reaching down toward the water at the front,
    and a hand-wheel valve."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone"))
    # plank pump house with a blue roof
    box(m, mat("wood"), -0.65, 0.06, -0.05, 0.65, 0.7, 0.8)
    box(m, mat("shadow"), -0.18, 0.06, -0.06, 0.18, 0.5, -0.05)
    prism_roof(m, mat("water_accent"), -0.75, 0.75, -0.15, 0.9, 0.7, 1.05, mat_gable=mat("wood"))
    # intake pipe out of the front, bending down over the edge
    cylinder(m, mat("metal_dark"), (0.45, 0.35, -0.05), (0.45, 0.35, -0.75), 0.08, segs=8)
    cylinder(m, mat("metal_dark"), (0.45, 0.35, -0.75), (0.45, 0.02, -0.85), 0.08, segs=8)
    cylinder(m, mat("water_accent"), (0.45, 0.35, -0.42), (0.45, 0.35, -0.32), 0.1, segs=8)
    # hand-wheel valve on a post
    cylinder(m, mat("metal_dark"), (-0.35, 0.06, -0.55), (-0.35, 0.45, -0.55), 0.05, segs=6)
    cylinder(m, mat("water_accent"), (-0.35, 0.47, -0.62), (-0.35, 0.47, -0.55), 0.17, segs=10)
    cylinder(m, mat("metal_dark"), (-0.35, 0.47, -0.64), (-0.35, 0.47, -0.62), 0.05, segs=6)
    return m, mat.mats


def pumping_station():
    """A pump hall with two intake pipes out of the front, a water tank at the back, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone"))
    # the hall, with a blue band and a flat metal roof
    box(m, mat("stone"), -1.2, 0.08, -0.7, 0.55, 0.95, 0.6, mat_top=mat("metal"))
    box(m, mat("water_accent"), -1.22, 0.7, -0.72, 0.57, 0.8, 0.62)
    box(m, mat("shadow"), -0.5, 0.08, -0.71, -0.1, 0.55, -0.7)
    # two intake pipes out of the front, bending down
    for x in (-0.95, 0.25):
        cylinder(m, mat("metal_dark"), (x, 0.4, -0.7), (x, 0.4, -1.25), 0.1, segs=8)
        cylinder(m, mat("metal_dark"), (x, 0.4, -1.25), (x, 0.02, -1.33), 0.1, segs=8)
        cylinder(m, mat("water_accent"), (x, 0.4, -1.0), (x, 0.4, -0.9), 0.12, segs=8)
    # water tank at the back right, piped from the hall
    cylinder(m, mat("metal"), (0.85, 0.08, 0.75), (0.85, 1.55, 0.75), 0.45, segs=12)
    cylinder(m, mat("water_accent"), (0.85, 1.0, 0.75), (0.85, 1.2, 0.75), 0.46, segs=12)
    spike(m, mat("metal", 1.1), (0.85, 1.55, 0.75), (0.85, 1.72, 0.75), 0.45, segs=12)
    cylinder(m, mat("metal_dark"), (0.55, 0.6, 0.3), (0.85, 0.6, 0.3), 0.06, segs=6)
    box(m, mat("power_accent"), 0.6, 0.08, -0.6, 0.95, 0.4, -0.3, mat_top=mat("metal_dark"))
    power_pole(m, mat, 1.15, -1.15)
    return m, mat.mats


def sand_collector():
    """A hopper on four legs over a sand pile, with a short chute at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("sand", 0.9), -hx, 0, -hz, hx, 0.04, hz, mat_top=mat("sand"))
    spike(m, mat("sand"), (0.1, 0.04, 0.15), (0.1, 0.42, 0.15), 0.62, segs=8)
    for x, z in ((-0.55, -0.35), (0.55, -0.35), (0.55, 0.6), (-0.55, 0.6)):
        cylinder(m, mat("wood_dark"), (x, 0.04, z), (x * 0.8, 0.95, z * 0.8 + 0.03), 0.04, segs=5)
    # the hopper: a box over a funnel, with a tan band
    box(m, mat("wood"), -0.5, 0.95, -0.35, 0.5, 1.3, 0.55, mat_top=mat("sand", 1.05))
    box(m, mat("sand_accent"), -0.52, 1.18, -0.37, 0.52, 1.28, 0.57)
    spike(m, mat("wood_dark"), (0, 0.95, 0.1), (0, 0.65, 0.1), 0.42, segs=4)
    # chute to the front
    box(m, mat("sand_accent"), -0.12, 0.62, -0.75, 0.12, 0.7, 0.05)
    box(m, mat("sand"), -0.1, 0.04, -0.85, 0.1, 0.16, -0.65)
    return m, mat.mats


def sand_dredge():
    """A scoop wheel over a sand pit at the front, a conveyor up to a hopper, a motor house, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("sand", 0.9), -hx, 0, -hz, hx, 0.04, hz, mat_top=mat("sand"))
    # the pit: a darker patch at the front
    box(m, mat("sand", 0.7), -1.0, 0.04, -1.3, 0.6, 0.045, -0.3)
    # scoop wheel over the pit, turning about X
    cx, cy, cz, r = -0.2, 0.75, -0.8, 0.6
    segs = 10
    pts = [(cx, cy + r * math.cos(2 * math.pi * k / segs), cz + r * math.sin(2 * math.pi * k / segs)) for k in range(segs)]
    for k in range(segs):
        cylinder(m, mat("metal_dark"), pts[k], pts[(k + 1) % segs], 0.04, segs=4)
        cylinder(m, mat("metal"), (cx, cy, cz), pts[k], 0.025, segs=4)
        if k % 2 == 0:
            x, y, z = pts[k]
            box(m, mat("sand_accent"), x - 0.15, y - 0.07, z - 0.07, x + 0.15, y + 0.07, z + 0.07)
    cylinder(m, mat("metal_dark"), (cx - 0.3, cy, cz), (cx + 0.3, cy, cz), 0.06, segs=6)
    for x in (cx - 0.3, cx + 0.3):
        cylinder(m, mat("metal_dark"), (x, 0.04, cz - 0.2), (x, cy, cz), 0.04, segs=5)
        cylinder(m, mat("metal_dark"), (x, 0.04, cz + 0.2), (x, cy, cz), 0.04, segs=5)
    # conveyor from the wheel up to the hopper at the back
    cylinder(m, mat("metal_dark"), (cx, 0.9, -0.3), (cx, 1.35, 0.55), 0.09, segs=4)
    box(m, mat("metal"), -0.75, 0.04, 0.45, 0.35, 1.45, 1.3, mat_top=mat("sand", 1.05))
    box(m, mat("sand_accent"), -0.77, 1.15, 0.43, 0.37, 1.27, 1.32)
    # motor house on the right with a yellow band
    box(m, mat("metal_dark"), 0.6, 0.04, -0.2, 1.25, 0.7, 0.6, mat_top=mat("metal"))
    box(m, mat("power_accent"), 0.58, 0.5, -0.22, 1.27, 0.58, 0.62)
    power_pole(m, mat, 1.15, -1.15)
    return m, mat.mats


def glassworks():
    """A long works, 3 by 6: a stone furnace with a glowing kiln mouth at the back, a cooling line
    of panes down its length under an open roof frame, and a rack of finished panes at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(6)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.9))
    # the furnace at the back, its kiln mouth facing down the line
    box(m, mat("stone"), -1.25, 0.08, 1.3, 1.25, 1.35, 2.85, mat_top=mat("stone", 0.9))
    box(m, mat("glass_accent"), -1.27, 1.05, 1.28, 1.27, 1.17, 2.87)
    box(m, mat("stone_dark"), -0.55, 0.08, 1.2, 0.55, 0.8, 1.3)
    quad(m, mat("furnace_glow"), (-0.4, 0.12, 1.199), (0.4, 0.12, 1.199), (0.4, 0.65, 1.199),
         (-0.4, 0.65, 1.199), (0, 0, -1))
    cylinder(m, mat("stone_dark"), (0.75, 1.35, 2.3), (0.75, 2.5, 2.3), 0.25, segs=8)
    cylinder(m, mat("glass_accent"), (0.75, 2.2, 2.3), (0.75, 2.3, 2.3), 0.28, segs=8)
    cylinder(m, mat("shadow"), (0.75, 2.5, 2.3), (0.75, 2.501, 2.3), 0.17, segs=8)
    # the cooling line: a long bench with upright panes, the first still glowing
    box(m, mat("metal_dark"), -0.45, 0.08, -2.0, 0.45, 0.35, 1.2, mat_top=mat("metal"))
    for k in range(6):
        z = 0.95 - k * 0.5
        box(m, mat("furnace_glow" if k == 0 else "glass"), -0.35, 0.35, z - 0.03, 0.35, 0.85, z + 0.03,
            mat_top=mat("glass_edge"))
    # an open roof frame over the line
    for x in (-1.1, 1.1):
        for z in (-1.9, -0.6, 0.7):
            cylinder(m, mat("wood_dark"), (x, 0.08, z), (x, 1.2, z), 0.05, segs=5)
        cylinder(m, mat("wood_dark"), (x, 1.2, -1.95), (x, 1.2, 1.25), 0.05, segs=5)
    for z in (-1.9, -0.6, 0.7):
        cylinder(m, mat("glass_accent"), (-1.1, 1.2, z), (1.1, 1.2, z), 0.045, segs=5)
    # the rack of finished panes at the front
    box(m, mat("wood"), -1.1, 0.08, -2.75, 1.1, 0.16, -2.25)
    for k in range(5):
        x = -0.85 + k * 0.42
        box(m, mat("glass"), x - 0.17, 0.16, -2.6, x + 0.17, 0.7, -2.53, mat_top=mat("glass_edge"))
    box(m, mat("glass_accent"), -1.1, 0.7, -2.7, 1.1, 0.76, -2.3)
    return m, mat.mats


BUILDINGS = {"mine": mine, "lumber_camp": lumber_camp, "smelter": smelter, "refinery": refinery,
             "oil_well": oil_well, "hq": hq, "silo": silo, "stockyard": stockyard, "tank_farm": tank_farm,
             "warehouse": warehouse, "vault": vault, "small_hub": small_hub, "relay": relay,
             "water_wheel": water_wheel, "burner_generator": burner_generator, "lumber_mill": lumber_mill,
             "drill_rig": drill_rig, "water_pump": water_pump, "pumping_station": pumping_station,
             "sand_collector": sand_collector, "sand_dredge": sand_dredge, "glassworks": glassworks}


def build_all():
    for name, fn in BUILDINGS.items():
        m, mats = fn()
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
    _ = math
