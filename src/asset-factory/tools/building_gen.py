"""Building models: one per building type, authored facing North (front toward -Z).

Each model fills its footprint (in fine cells, 1 unit each) minus a 0.1 inset per side, origin at
the footprint center on the ground. The front marks the building's facing; the accent color is
the building's identity color. See docs/blueprints/art-style.md and
src/Telaelus/assets/models/buildings/README.md.
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


def depot():
    """A garage across the back half with two roller doors, a fuel pump between them, tyres
    stacked behind, and a paved apron across the front half painted with four parking bays."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(4)
    # the apron, with bay lines dividing four slots 1 wide and 1.8 deep, nose out
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("stone_dark", 1.15))
    for x in (-1.0, 0.0, 1.0):
        box(m, mat("ceramic"), x - 0.03, 0.05, -1.85, x + 0.03, 0.055, -0.15)
    box(m, mat("ceramic"), -1.85, 0.05, -0.15, 1.85, 0.055, -0.09)
    # the garage
    box(m, mat("stone"), -1.85, 0.05, 0.1, 1.85, 1.3, 1.55, mat_top=mat("metal"))
    box(m, mat("depot_accent"), -1.87, 1.05, 0.08, 1.87, 1.18, 1.57)
    for z0, z1 in ((0.05, 0.15), (1.5, 1.6)):
        box(m, mat("depot_accent"), -1.9, 1.3, z0, 1.9, 1.38, z1)
    for x in (-0.95, 0.95):
        box(m, mat("metal_dark"), x - 0.7, 0.05, 0.09, x + 0.7, 0.95, 0.1)
        for y in (0.25, 0.45, 0.65, 0.85):
            box(m, mat("metal"), x - 0.68, y, 0.085, x + 0.68, y + 0.03, 0.09)
    # fuel pump between the doors
    box(m, mat("depot_accent"), -0.14, 0.05, -0.12, 0.14, 0.62, 0.08, mat_top=mat("metal_dark"))
    box(m, mat("panel_dark"), -0.09, 0.38, -0.121, 0.09, 0.52, -0.12)
    # tyres stacked behind the garage
    for k in range(3):
        y = 0.05 + k * 0.12
        cylinder(m, mat("rubber"), (1.45, y, 1.73), (1.45, y + 0.11, 1.73), 0.17, segs=10, mat_cap=mat("rubber_light"))
    cylinder(m, mat("rubber"), (1.05, 0.05, 1.73), (1.05, 0.16, 1.73), 0.17, segs=10, mat_cap=mat("rubber_light"))
    return m, mat.mats


def forester():
    """A tree plot, 4 x 4: a low fence round tilled ground, a potting shed with a green roof and a
    water tank in one corner, and a power pole. The trees are the plot's field, drawn by the game."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(4)
    box(m, mat("tilled"), -hx, 0, -hz, hx, 0.04, hz, mat_top=mat("tilled", 1.08))
    for k in range(-3, 4):
        box(m, mat("tilled", 0.85), k * 0.5 - 0.04, 0.04, -hz + 0.2, k * 0.5 + 0.04, 0.06, hz - 0.2)
    # the fence: posts and two rails on each side
    for (x0, z0, x1, z1) in ((-hx, -hz, hx, -hz), (-hx, hz, hx, hz), (-hx, -hz, -hx, hz), (hx, -hz, hx, hz)):
        for y in (0.18, 0.34):
            cylinder(m, mat("wood"), (x0, y, z0), (x1, y, z1), 0.025, segs=4)
    for k in range(9):
        t = -hx + k * (2 * hx / 8)
        for (x, z) in ((t, -hz), (t, hz), (-hx, t), (hx, t)):
            cylinder(m, mat("wood_dark"), (x, 0.0, z), (x, 0.42, z), 0.035, segs=4)
    # the shed and tank in the back right corner
    box(m, mat("wood"), 1.05, 0.04, 1.05, 1.8, 0.6, 1.8)
    prism_roof(m, mat("forester_accent"), 1.0, 1.85, 1.0, 1.85, 0.6, 0.9, mat_gable=mat("wood"))
    cylinder(m, mat("metal"), (1.45, 0.04, 0.55), (1.45, 0.55, 0.55), 0.22, segs=10, mat_cap=mat("water"))
    cylinder(m, mat("forester_accent"), (1.45, 0.38, 0.55), (1.45, 0.45, 0.55), 0.23, segs=10, caps=False)
    power_pole(m, mat, -1.7, 1.7, 1.2)
    return m, mat.mats


def quarry():
    """A cut stone face on a squared block at the back (the side that meets the mountain), a small
    derrick crane lifting a block, stacked cut blocks at the front, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("rock"))
    # the face: stepped benches cut into a block across the back
    for k, (z0, top) in enumerate(((0.55, 1.5), (0.15, 0.95), (-0.2, 0.45))):
        box(m, mat("rock", 1.0 - k * 0.04), -1.3, 0.06, z0, 1.3, top, hz, mat_top=mat("stone", 0.95))
    box(m, mat("quarry_accent"), -1.32, 0.95, 0.13, 1.32, 1.02, 0.17)
    # the derrick: a mast and a boom to the front, a block on its hook
    cylinder(m, mat("wood_dark"), (-0.8, 0.06, -0.6), (-0.8, 1.9, -0.6), 0.06, segs=6)
    cylinder(m, mat("wood_dark"), (-0.8, 0.4, -0.6), (0.4, 1.6, -0.9), 0.045, segs=5)
    cylinder(m, mat("metal_dark"), (0.4, 1.6, -0.9), (0.4, 0.75, -0.9), 0.012, segs=3)
    box(m, mat("stone"), 0.25, 0.5, -1.05, 0.55, 0.75, -0.75)
    box(m, mat("quarry_accent"), -0.95, 0.06, -0.75, -0.65, 0.3, -0.45)
    # cut blocks stacked at the front right
    for (x, z, y) in ((0.7, -1.0, 0.06), (1.05, -1.0, 0.06), (0.88, -0.95, 0.32)):
        box(m, mat("stone"), x - 0.16, y, z - 0.18, x + 0.16, y + 0.26, z + 0.18, mat_top=mat("stone", 1.06))
    power_pole(m, mat, -1.2, -1.15, 1.3)
    return m, mat.mats


def charcoal_kiln():
    """Two domed brick beehive kilns with dark doors and smoke vents, a stack of logs, and a heap of
    charcoal."""
    m, mat = Mesh(), Materials()
    hx, hz = half(2), half(2)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("mud"))
    for x in (-0.38, 0.4):
        cylinder(m, mat("kiln_accent"), (x, 0.05, 0.3), (x, 0.38, 0.3), 0.36, segs=12, caps=False)
        spike(m, mat("kiln_accent", 0.9), (x, 0.38, 0.3), (x, 0.82, 0.3), 0.36, segs=12)
        cylinder(m, mat("stone_dark"), (x, 0.78, 0.3), (x, 0.88, 0.3), 0.06, segs=6, mat_cap=mat("shadow"))
        box(m, mat("shadow"), x - 0.12, 0.05, -0.07, x + 0.12, 0.3, -0.05)
    # logs on the front left, charcoal heap on the front right
    for y, z in ((0.14, -0.55), (0.14, -0.33), (0.32, -0.44)):
        cylinder(m, mat("bark"), (-0.8, y, z), (-0.1, y, z), 0.09, segs=7, mat_cap=mat("log_end"))
    spike(m, mat("charcoal"), (0.45, 0.05, -0.45), (0.45, 0.3, -0.45), 0.3, segs=7)
    return m, mat.mats


def rock_crusher():
    """A hopper over a boxy jaw crusher, a short conveyor out to a sand pile, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(3)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone", 0.85))
    # the crusher body with a yellow band, and the hopper on top
    box(m, mat("metal_dark"), -0.75, 0.06, 0.0, 0.45, 0.95, 1.1, mat_top=mat("metal"))
    box(m, mat("power_accent"), -0.77, 0.7, -0.02, 0.47, 0.78, 1.12)
    spike(m, mat("metal"), (-0.15, 1.6, 0.55), (-0.15, 0.95, 0.55), 0.62, segs=4)
    box(m, mat("rock"), -0.45, 1.5, 0.3, 0.15, 1.62, 0.8)
    # conveyor to the front and the sand pile it feeds
    cylinder(m, mat("metal_dark"), (-0.15, 0.55, 0.0), (0.55, 0.3, -0.9), 0.08, segs=4)
    for t in (0.2, 0.5, 0.8):
        x, y, z = -0.15 + 0.7 * t, 0.55 - 0.25 * t, -0.9 * t
        cylinder(m, mat("metal_dark"), (x, 0.06, z), (x, y, z), 0.03, segs=4)
    spike(m, mat("sand"), (0.7, 0.06, -0.85), (0.7, 0.45, -0.85), 0.55, segs=8)
    box(m, mat("sand_accent"), 0.47, 0.06, 0.2, 1.2, 0.5, 0.9, mat_top=mat("metal_dark"))
    power_pole(m, mat, -1.2, -1.15, 1.4)
    return m, mat.mats


def deep_well():
    """A tall derrick at the center over a capped wellhead on an oil-stained pad (the old seep), a
    pump motor and a short tank to one side, a pipe rack, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(4)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("stone", 0.85))
    cylinder(m, mat("oil"), (0, 0.05, 0), (0, 0.055, 0), 1.0, segs=14, mat_cap=mat("oil"))
    top = 3.0
    legs = [(-0.7, -0.7), (0.7, -0.7), (0.7, 0.7), (-0.7, 0.7)]
    for x, z in legs:
        cylinder(m, mat("metal_dark"), (x, 0.05, z), (x * 0.25, top, z * 0.25), 0.05, segs=5)
    for y in (0.8, 1.6, 2.3):
        s = 0.7 - (y - 0.05) / (top - 0.05) * 0.525
        for i in range(4):
            (ax, az), (bx, bz) = legs[i], legs[(i + 1) % 4]
            cylinder(m, mat("oil_well_accent"), (ax / 0.7 * s, y, az / 0.7 * s), (bx / 0.7 * s, y, bz / 0.7 * s), 0.03, segs=4)
    box(m, mat("oil_well_accent"), -0.25, top - 0.05, -0.25, 0.25, top + 0.15, 0.25, mat_top=mat("metal_dark"))
    cylinder(m, mat("metal"), (0, 0.05, 0), (0, 0.45, 0), 0.18, segs=8, mat_cap=mat("metal_dark"))
    # pump motor and tank to the right, a pipe rack to them
    box(m, mat("metal_dark"), 0.95, 0.05, -0.45, 1.6, 0.6, 0.25, mat_top=mat("power_accent"))
    cylinder(m, mat("metal"), (1.3, 0.05, 1.05), (1.3, 0.9, 1.05), 0.42, segs=12)
    cylinder(m, mat("oil_well_accent"), (1.3, 0.55, 1.05), (1.3, 0.68, 1.05), 0.43, segs=12, caps=False)
    for z in (-0.1, 0.1):
        cylinder(m, mat("metal_dark"), (0.2, 0.3, z), (0.95, 0.3, z), 0.04, segs=5)
    cylinder(m, mat("metal_dark"), (1.3, 0.3, 0.25), (1.3, 0.3, 0.65), 0.04, segs=5)
    power_pole(m, mat, -1.65, -1.65, 1.5)
    return m, mat.mats


def deep_mine():
    """A steel headframe tower with a winding wheel over a shaft at the center, a winding house, an
    ore skip on rails to a tipple at the front, and a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(4)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone"))
    box(m, mat("shadow"), -0.45, 0.06, -0.45, 0.45, 0.065, 0.45)
    top = 2.8
    for x in (-0.55, 0.55):
        for z in (-0.5, 0.5):
            cylinder(m, mat("metal_dark"), (x, 0.06, z), (x * 0.6, top, z * 0.6), 0.06, segs=5)
        cylinder(m, mat("metal_dark"), (x, 0.06, 0.5), (x * 0.4, top - 0.4, 1.4), 0.05, segs=5)
    for y in (0.9, 1.8):
        cylinder(m, mat("mine_accent"), (-0.5, y, -0.45), (0.5, y, -0.45), 0.04, segs=4)
        cylinder(m, mat("mine_accent"), (-0.5, y, 0.45), (0.5, y, 0.45), 0.04, segs=4)
    cylinder(m, mat("metal"), (-0.4, top, 0), (0.4, top, 0), 0.06, segs=6)
    cylinder(m, mat("mine_accent"), (-0.06, top + 0.05, 0), (0.06, top + 0.05, 0), 0.42, segs=12)
    # the winding house at the back with a yellow band
    box(m, mat("stone"), -0.9, 0.06, 1.2, 0.9, 1.0, 1.85, mat_top=mat("metal"))
    box(m, mat("power_accent"), -0.92, 0.75, 1.18, 0.92, 0.83, 1.87)
    # rails from the shaft to a tipple at the front, a skip on them
    for x in (-0.15, 0.15):
        box(m, mat("metal_dark"), x - 0.02, 0.06, -1.7, x + 0.02, 0.09, -0.45)
    box(m, mat("mine_accent"), -0.25, 0.12, -1.1, 0.25, 0.42, -0.7, mat_top=mat("iron_rust"))
    box(m, mat("wood_dark"), -0.6, 0.06, -1.85, 0.6, 0.7, -1.6, mat_top=mat("wood"))
    spike(m, mat("iron_rust"), (1.1, 0.06, -1.2), (1.1, 0.5, -1.2), 0.5, segs=7)
    power_pole(m, mat, -1.65, -1.6, 1.5)
    return m, mat.mats


def steel_mill():
    """4 x 6: a tall brick hall running back from the front, a converter tipping at the back end
    under a chimney, a casting bay along one side, billets stacked at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(6)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    box(m, mat("brick"), -1.6, 0.08, -1.6, 0.7, 1.7, 2.0, mat_top=mat("metal_dark"))
    prism_roof(m, mat("steel_accent"), -1.7, 0.8, -1.7, 2.1, 1.7, 2.2, mat_gable=mat("brick", 0.9))
    box(m, mat("shadow"), -0.8, 0.08, -1.61, -0.1, 1.0, -1.6)
    # the casting bay on the right, low, with a steel-blue band
    box(m, mat("metal"), 0.75, 0.08, -1.2, 1.8, 0.9, 1.6, mat_top=mat("metal_dark"))
    box(m, mat("steel_accent"), 0.73, 0.65, -1.22, 1.82, 0.75, 1.62)
    # the converter at the back, tipped, and a chimney beside it
    cylinder(m, mat("metal_dark"), (-0.4, 1.75, 2.3), (0.3, 2.3, 2.5), 0.45, segs=10, mat_cap=mat("furnace_glow"))
    cylinder(m, mat("brick", 0.8), (-1.2, 1.7, 2.4), (-1.2, 3.3, 2.4), 0.22, segs=8)
    cylinder(m, mat("steel_accent"), (-1.2, 3.0, 2.4), (-1.2, 3.1, 2.4), 0.25, segs=8)
    cylinder(m, mat("shadow"), (-1.2, 3.3, 2.4), (-1.2, 3.301, 2.4), 0.15, segs=8)
    # billets at the front
    for k in range(3):
        for j in range(2 - (k > 1)):
            x = -1.2 + j * 0.42 + k * 0.1
            y = 0.08 + k * 0.12
            box(m, mat("steel"), x, y, -2.75, x + 0.38, y + 0.12, -2.0, mat_top=mat("steel_light"))
    power_pole(m, mat, 1.65, -2.65, 1.6)
    return m, mat.mats


def electrolyzer():
    """4 x 5: two rows of tall cells down its length with electrodes on top, a rectifier house at
    the back, and pipe outlets to two gas tanks at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(5)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.9))
    for x in (-0.9, 0.2):
        for k in range(4):
            z = -0.9 + k * 0.62
            box(m, mat("metal"), x - 0.3, 0.08, z - 0.24, x + 0.3, 1.05, z + 0.24, mat_top=mat("gas_accent"))
            for dx in (-0.12, 0.12):
                cylinder(m, mat("copper"), (x + dx, 1.05, z), (x + dx, 1.3, z), 0.04, segs=5)
    # the rectifier house at the back with a yellow band
    box(m, mat("stone"), -1.6, 0.08, 1.6, 1.0, 1.0, 2.3, mat_top=mat("metal"))
    box(m, mat("power_accent"), -1.62, 0.75, 1.58, 1.02, 0.83, 2.32)
    # two gas tanks at the front, oxygen white and hydrogen pale, piped from the cells
    for x, colour in ((-0.8, "ceramic"), (0.6, "gas_accent")):
        cylinder(m, mat(colour), (x - 0.5, 0.5, -1.85), (x + 0.5, 0.5, -1.85), 0.38, segs=12)
        cylinder(m, mat("metal_dark"), (x, 0.08, -1.85), (x, 0.15, -1.85), 0.4, segs=8)
        cylinder(m, mat("metal_dark"), (x, 0.9, -1.85), (x, 0.9, -1.2), 0.05, segs=5)
    power_pole(m, mat, 1.65, -2.15, 1.6)
    return m, mat.mats


def fuel_plant():
    """4 x 5: two process columns at the back joined by a pipe rack, a blending tank with a red
    band in the middle, and a loading arm over a pad at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(5)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    for x, h in ((-1.0, 2.8), (0.3, 2.3)):
        cylinder(m, mat("metal"), (x, 0.08, 1.4), (x, h, 1.4), 0.35, segs=10)
        cylinder(m, mat("fuel_accent"), (x, h * 0.6, 1.4), (x, h * 0.6 + 0.15, 1.4), 0.36, segs=10, caps=False)
        spike(m, mat("metal", 1.1), (x, h, 1.4), (x, h + 0.2, 1.4), 0.35, segs=10)
    for y in (0.7, 1.4):
        cylinder(m, mat("metal_dark"), (-1.0, y, 1.0), (1.4, y, 1.0), 0.05, segs=5)
    cylinder(m, mat("metal"), (0.4, 0.08, 0.0), (0.4, 1.2, 0.0), 0.6, segs=14)
    cylinder(m, mat("fuel_accent"), (0.4, 0.75, 0.0), (0.4, 0.95, 0.0), 0.61, segs=14, caps=False)
    spike(m, mat("metal", 1.1), (0.4, 1.2, 0.0), (0.4, 1.35, 0.0), 0.6, segs=14)
    # the loading pad and arm at the front
    box(m, mat("stone"), -1.4, 0.08, -2.25, 0.6, 0.12, -1.0)
    cylinder(m, mat("metal_dark"), (-0.4, 0.12, -1.2), (-0.4, 1.2, -1.2), 0.06, segs=6)
    cylinder(m, mat("fuel_accent"), (-0.4, 1.2, -1.2), (-0.4, 0.9, -1.9), 0.05, segs=6)
    power_pole(m, mat, 1.65, -2.15, 1.6)
    return m, mat.mats


def chemical_plant():
    """5 x 6, the largest: a reactor vessel and a tall distillation tower at the back, a pipe rack
    across the middle, a control house, and drums at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(5), half(6)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    cylinder(m, mat("metal"), (-1.2, 0.08, 1.7), (-1.2, 1.4, 1.7), 0.75, segs=14)
    spike(m, mat("chemical_accent"), (-1.2, 1.4, 1.7), (-1.2, 1.9, 1.7), 0.75, segs=14)
    cylinder(m, mat("metal_dark"), (0.9, 0.08, 2.0), (0.9, 3.4, 2.0), 0.3, segs=10)
    for y in (1.0, 1.8, 2.6):
        cylinder(m, mat("chemical_accent"), (0.9, y, 2.0), (0.9, y + 0.08, 2.0), 0.36, segs=10, caps=False)
    # the pipe rack across the middle
    for x in (-2.0, -0.6, 0.8, 2.0):
        cylinder(m, mat("metal_dark"), (x, 0.08, 0.2), (x, 1.0, 0.2), 0.05, segs=5)
    for k, y in enumerate((0.8, 0.95)):
        for z in (0.1, 0.3):
            cylinder(m, mat("chemical_accent" if k else "metal"), (-2.1, y, z), (2.1, y, z), 0.05, segs=6)
    # control house and drums at the front
    box(m, mat("stone"), 0.6, 0.08, -2.6, 2.1, 0.9, -1.4, mat_top=mat("metal"))
    box(m, mat("chemical_accent"), 0.58, 0.68, -2.62, 2.12, 0.76, -1.38)
    for k in range(5):
        x = -2.0 + (k % 3) * 0.45
        z = -2.4 + (k // 3) * 0.45
        cylinder(m, mat("chemical_accent", 0.85 if k % 2 else 1.0), (x, 0.08, z), (x, 0.5, z), 0.18, segs=8, mat_cap=mat("metal_dark"))
    power_pole(m, mat, 2.15, 2.65, 1.6)
    return m, mat.mats


def aluminum_smelter():
    """3 x 8: a long, narrow pot line under a low saw-tooth roof with fat bus bars along its side, a
    fume duct along the ridge, and ingots stacked at the front end, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(8)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone", 0.85))
    box(m, mat("metal"), -1.1, 0.06, -3.0, 0.9, 0.95, 3.8, mat_top=mat("metal_dark"))
    for k in range(7):
        z0 = -3.0 + k * 0.97
        quad(m, mat("aluminum_accent"), (-1.15, 0.95, z0), (0.95, 0.95, z0), (0.95, 1.35, z0 + 0.6), (-1.15, 1.35, z0 + 0.6), (0, 0.8, -0.6))
        quad(m, mat("glass"), (-1.15, 1.35, z0 + 0.6), (0.95, 1.35, z0 + 0.6), (0.95, 0.95, z0 + 0.97), (-1.15, 0.95, z0 + 0.97), (0, 0.6, 0.8))
    cylinder(m, mat("metal_dark"), (-0.1, 1.5, -3.0), (-0.1, 1.5, 3.8), 0.12, segs=8)
    # bus bars down the right side
    for y in (0.35, 0.55):
        box(m, mat("copper"), 0.92, y, -2.9, 1.05, y + 0.1, 3.7)
    # ingots at the front end
    for k in range(3):
        y = 0.06 + k * 0.1
        box(m, mat("aluminum_accent"), -0.9 + k * 0.05, y, -3.85, 0.3 - k * 0.05, y + 0.1, -3.2, mat_top=mat("aluminum_light"))
    power_pole(m, mat, 1.15, -3.6, 1.6)
    return m, mat.mats


def electric_furnace():
    """4 x 4: a squat round arc furnace in the middle with three graphite electrodes rising through
    its lid, a transformer box at the back, and a glowing tap at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(4)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    cylinder(m, mat("metal_dark"), (0, 0.08, 0), (0, 1.0, 0), 1.0, segs=16)
    cylinder(m, mat("furnace_accent"), (0, 0.7, 0), (0, 0.85, 0), 1.02, segs=16, caps=False)
    spike(m, mat("metal"), (0, 1.0, 0), (0, 1.3, 0), 1.0, segs=16)
    for k in range(3):
        a = 2 * math.pi * k / 3
        x, z = math.cos(a) * 0.35, math.sin(a) * 0.35
        cylinder(m, mat("coal"), (x, 1.1, z), (x, 2.3, z), 0.1, segs=6)
        cylinder(m, mat("furnace_accent"), (x, 1.9, z), (x, 2.0, z), 0.13, segs=6)
    box(m, mat("metal"), -1.3, 0.08, 1.15, 0.3, 1.0, 1.85, mat_top=mat("furnace_accent"))
    box(m, mat("furnace_glow"), -0.2, 0.08, -1.45, 0.2, 0.35, -0.95)
    cylinder(m, mat("metal_dark"), (0, 0.4, -0.9), (0, 0.3, -1.3), 0.08, segs=6)
    power_pole(m, mat, 1.65, -1.65, 1.6)
    return m, mat.mats


def fabricator():
    """4 x 6: a high hall with a hydraulic press frame at the back, a roller line and a gantry crane
    over an open bay down one side, and plates, beams, and crates at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(6)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    box(m, mat("metal"), -1.8, 0.08, -1.0, 0.4, 1.9, 2.8, mat_top=mat("metal_dark"))
    box(m, mat("fabricator_accent"), -1.82, 1.5, -1.02, 0.42, 1.62, 2.82)
    box(m, mat("shadow"), -1.2, 0.08, -1.01, -0.2, 1.3, -1.0)
    # the press frame rising through the roof at the back
    for x in (-1.3, -0.3):
        box(m, mat("fabricator_accent"), x - 0.12, 1.9, 1.6, x + 0.12, 2.9, 2.0)
    box(m, mat("fabricator_accent"), -1.45, 2.7, 1.55, -0.15, 3.0, 2.05)
    # the open bay on the right with a gantry crane and a roller line
    for z in (-1.0, 2.7):
        for x in (0.6, 1.8):
            cylinder(m, mat("metal_dark"), (x, 0.08, z), (x, 1.6, z), 0.05, segs=5)
        cylinder(m, mat("fabricator_accent"), (0.6, 1.6, z), (1.8, 1.6, z), 0.06, segs=5)
    cylinder(m, mat("fabricator_accent"), (1.2, 1.6, -1.0), (1.2, 1.6, 2.7), 0.06, segs=5)
    for k in range(9):
        z = -0.8 + k * 0.42
        cylinder(m, mat("metal_dark"), (0.75, 0.3, z), (1.65, 0.3, z), 0.06, segs=6)
    # plates, beams, and crates at the front
    for k in range(3):
        y = 0.08 + k * 0.08
        box(m, mat("steel"), -1.7, y, -2.75, -0.7, y + 0.07, -1.6, mat_top=mat("steel_light"))
    for k in range(2):
        box(m, mat("steel"), -0.4, 0.08 + k * 0.16, -2.7 + k * 0.05, 0.6, 0.22 + k * 0.16, -2.55 + k * 0.05)
    box(m, mat("wood"), 0.9, 0.08, -2.7, 1.4, 0.45, -2.2)
    power_pole(m, mat, 1.65, 2.65, 1.6)
    return m, mat.mats


def electronics_works():
    """4 x 5: a clean low building with a saw-tooth glass roof, a clean-room annex, and wire spools
    at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(5)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.9))
    box(m, mat("ceramic"), -1.8, 0.08, -0.9, 1.0, 0.9, 2.3, mat_top=mat("metal"))
    box(m, mat("electronics_accent"), -1.82, 0.7, -0.92, 1.02, 0.78, 2.32)
    for k in range(4):
        z0 = -0.9 + k * 0.8
        quad(m, mat("glass"), (-1.8, 0.9, z0), (1.0, 0.9, z0), (1.0, 1.35, z0 + 0.5), (-1.8, 1.35, z0 + 0.5), (0, 0.7, -0.7))
        quad(m, mat("electronics_accent"), (-1.8, 1.35, z0 + 0.5), (1.0, 1.35, z0 + 0.5), (1.0, 0.9, z0 + 0.8), (-1.8, 0.9, z0 + 0.8), (0, 0.6, 0.8))
    # the clean-room annex to the right, white with a green door
    box(m, mat("ceramic"), 1.1, 0.08, 0.3, 1.85, 0.75, 1.6, mat_top=mat("ceramic", 0.9))
    box(m, mat("electronics_accent"), 1.3, 0.08, 0.29, 1.6, 0.55, 0.3)
    # copper wire spools at the front
    for k, x in enumerate((-1.4, -0.85, -0.3)):
        cylinder(m, mat("wood"), (x, 0.3, -1.75), (x + 0.3, 0.3, -1.75), 0.24, segs=10)
        cylinder(m, mat("copper"), (x + 0.04, 0.3, -1.75), (x + 0.26, 0.3, -1.75), 0.2, segs=10, caps=False)
    power_pole(m, mat, 1.65, -2.15, 1.6)
    return m, mat.mats


def tile_kiln():
    """3 x 7: a long brick tunnel kiln with a chimney at the back end, a car track running its
    length, and stacks of white tiles at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(7)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone", 0.85))
    # the tunnel: a long brick vault
    n = 8
    for k in range(n):
        a0 = math.pi * k / n
        a1 = math.pi * (k + 1) / n
        p0 = (math.cos(a0) * 0.85 - 0.35, 0.06 + math.sin(a0) * 0.85)
        p1 = (math.cos(a1) * 0.85 - 0.35, 0.06 + math.sin(a1) * 0.85)
        quad(m, mat("kiln_accent", 1.0 if k % 2 else 0.9), (p0[0], p0[1], -2.2), (p1[0], p1[1], -2.2),
             (p1[0], p1[1], 3.0), (p0[0], p0[1], 3.0), (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), 0))
    for z, glow in ((-2.2, False), (3.0, True)):
        sgn = -1 if z < 0 else 1
        pts = [(math.cos(math.pi * k / n) * 0.85 - 0.35, 0.06 + math.sin(math.pi * k / n) * 0.85, z) for k in range(n + 1)]
        for k in range(n):
            m.tri(mat("brick", 0.8), (-0.35, 0.06, z), pts[k], pts[k + 1], outward=(0, 0, sgn))
    box(m, mat("furnace_glow"), -0.6, 0.06, -2.21, -0.1, 0.45, -2.2)
    cylinder(m, mat("brick", 0.85), (0.9, 0.06, 2.8), (0.9, 2.2, 2.8), 0.2, segs=8)
    cylinder(m, mat("shadow"), (0.9, 2.2, 2.8), (0.9, 2.201, 2.8), 0.13, segs=8)
    # the car track out of the mouth, a kiln car with tiles on it
    for x in (-0.55, -0.15):
        box(m, mat("metal_dark"), x - 0.02, 0.06, -3.4, x + 0.02, 0.09, -2.2)
    box(m, mat("metal_dark"), -0.7, 0.1, -3.1, 0.0, 0.25, -2.5, mat_top=mat("ceramic"))
    for k in range(3):
        box(m, mat("ceramic"), 0.35, 0.06 + k * 0.08, -3.3 + k * 0.03, 1.2, 0.13 + k * 0.08, -2.6 + k * 0.03, mat_top=mat("ceramic", 1.05))
    power_pole(m, mat, 1.15, 0.5, 1.6)
    return m, mat.mats


def assembly_plant():
    """6 x 8: a large assembly hall with a tall bay door at the front, an overhead crane rail on the
    roof, and a finished thruster on a stand outside, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(6), half(8)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.85))
    box(m, mat("metal"), -2.7, 0.08, -2.0, 2.7, 2.4, 3.8, mat_top=mat("metal_dark"))
    box(m, mat("assembly_accent"), -2.72, 2.0, -2.02, 2.72, 2.15, 3.82)
    box(m, mat("assembly_accent", 0.7), -1.2, 0.08, -2.01, 1.2, 2.0, -2.0)
    for x in (-1.0, -0.5, 0.0, 0.5, 1.0):
        box(m, mat("metal_dark"), x - 0.02, 0.1, -2.02, x + 0.02, 1.95, -2.01)
    # the crane rail along the roof
    for x in (-2.0, 2.0):
        box(m, mat("assembly_accent"), x - 0.1, 2.4, -2.0, x + 0.1, 2.6, 3.8)
    box(m, mat("metal_dark"), -2.1, 2.6, 0.8, 2.1, 2.75, 1.1)
    # a thruster on its stand outside the door
    box(m, mat("metal_dark"), 1.6, 0.08, -3.6, 2.6, 0.3, -2.6)
    spike(m, mat("nozzle"), (2.1, 0.3, -3.1), (2.1, 1.2, -3.1), 0.42, segs=12)
    cylinder(m, mat("steel_light"), (2.1, 1.2, -3.1), (2.1, 1.5, -3.1), 0.25, segs=10)
    power_pole(m, mat, -2.65, -3.6, 1.6)
    return m, mat.mats


def shipyard():
    """8 x 12: a fabrication hall along the back half and a launch pad on the front half with a
    service gantry beside it. The ship is not in the model: the game stands its parts on the pad."""
    m, mat = Mesh(), Materials()
    hx, hz = half(8), half(12)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("stone", 0.8))
    box(m, mat("shipyard_accent"), -3.7, 0.08, 0.4, 3.7, 3.2, 5.7, mat_top=mat("metal"))
    box(m, mat("assembly_accent"), -3.72, 2.7, 0.38, 3.72, 2.85, 5.72)
    box(m, mat("shadow"), -1.5, 0.08, 0.39, 1.5, 2.6, 0.4)
    # the pad: a raised round deck with a flame trench
    cylinder(m, mat("stone"), (0, 0.08, -3.0), (0, 0.3, -3.0), 2.3, segs=20, mat_cap=mat("stone", 1.1))
    box(m, mat("shadow"), -0.4, 0.3, -3.6, 0.4, 0.31, -2.4)
    # the service gantry beside the pad
    for x in (2.5, 3.3):
        for z in (-3.6, -2.4):
            cylinder(m, mat("metal_dark"), (x, 0.08, z), (x, 6.0, z), 0.07, segs=5)
    for y in (1.5, 3.0, 4.5, 6.0):
        box(m, mat("power_accent"), 2.4, y - 0.08, -3.7, 3.4, y, -2.3)
        box(m, mat("metal_dark"), 1.0, y - 0.05, -3.1, 2.5, y + 0.02, -2.9)
    power_pole(m, mat, -3.65, -5.6, 1.8)
    return m, mat.mats


def display_stand():
    """A short post with a round plinth, about 0.6 high; the game sets the output's item on it."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("metal_dark"), (0, 0, 0), (0, 0.06, 0), 0.22, segs=10)
    cylinder(m, mat("metal_dark"), (0, 0.06, 0), (0, 0.5, 0), 0.05, segs=6)
    cylinder(m, mat("metal_dark"), (0, 0.5, 0), (0, 0.58, 0), 0.28, segs=12, mat_cap=mat("metal"))
    cylinder(m, mat("power_accent"), (0, 0.52, 0), (0, 0.56, 0), 0.29, segs=12, caps=False)
    return m, mat.mats


def ship_engine():
    """The ship's engine as it stands on the pad: a thrust frame over two bells, about 1.0 high."""
    m, mat = Mesh(), Materials()
    for x in (-0.35, 0.35):
        spike(m, mat("nozzle"), (x, 0.0, 0), (x, 0.55, 0), 0.3, segs=12)
        cylinder(m, mat("nozzle_hot"), (x, 0.0, 0), (x, 0.03, 0), 0.3, segs=12, caps=False)
    cylinder(m, mat("metal_dark"), (0, 0.5, 0), (0, 1.0, 0), 0.82, segs=16, mat_cap=mat("metal"))
    return m, mat.mats


def ship_hull_ring():
    """One hull section of the ship: a ring 1.0 high, white with a dark band."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("shipyard_accent"), (0, 0, 0), (0, 1.0, 0), 0.82, segs=16, mat_cap=mat("hull_dark"))
    cylinder(m, mat("assembly_accent"), (0, 0.42, 0), (0, 0.58, 0), 0.83, segs=16, caps=False)
    return m, mat.mats


def ship_nose():
    """The ship's nose with its control module: a white cone with a window band, about 1.6 high."""
    m, mat = Mesh(), Materials()
    cylinder(m, mat("shipyard_accent"), (0, 0, 0), (0, 0.4, 0), 0.82, segs=16)
    cylinder(m, mat("screen"), (0, 0.22, 0), (0, 0.32, 0), 0.83, segs=16, caps=False)
    spike(m, mat("shipyard_accent"), (0, 0.4, 0), (0, 1.6, 0), 0.82, segs=16)
    return m, mat.mats


def sawmill():
    """3 x 5: an open-sided plank shed over a long saw bench, logs feeding in at the back, and
    stacked planks at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(5)
    box(m, mat("wet_earth"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("mud", 1.1))
    # the shed: posts and a gabled plank roof over the bench
    for x in (-1.1, 1.1):
        for z in (-1.0, 0.4, 1.8):
            cylinder(m, mat("wood_dark"), (x, 0.05, z), (x, 1.2, z), 0.06, segs=5)
    prism_roof(m, mat("lumber_accent"), -1.3, 1.3, -1.2, 2.0, 1.2, 1.75, mat_gable=mat("plank_dark"))
    # the saw bench down the middle, its round blade, a log on the carriage
    box(m, mat("plank_dark"), -0.35, 0.05, -0.9, 0.35, 0.5, 1.9, mat_top=mat("plank"))
    cylinder(m, mat("steel_light"), (-0.02, 0.62, 0.3), (0.02, 0.62, 0.3), 0.32, segs=14)
    cylinder(m, mat("bark"), (0, 0.68, 0.9), (0, 0.68, 1.85), 0.18, segs=8, mat_cap=mat("log_end"))
    # logs waiting at the back
    for k in range(3):
        y = 0.2 + (0.3 if k == 2 else 0.0)
        x = -0.9 + k * 0.6 if k < 2 else -0.6
        cylinder(m, mat("bark"), (x - 0.3, y, 2.15), (x + 0.6, y, 2.15), 0.17, segs=8, mat_cap=mat("log_end"))
    # stacked planks at the front, either side of the open middle
    for side in (-1, 1):
        for k in range(4):
            y = 0.05 + k * 0.09
            box(m, mat("plank"), side * 0.55 - 0.35, y, -2.25, side * 0.55 + 0.35, y + 0.08, -1.4,
                mat_top=mat("plank", 1.1))
    return m, mat.mats


def cement_kiln():
    """3 x 7: a long sloped rotary kiln drum on piers, a preheater tower and chimney at the back, and
    a clinker pile at the front; it burns fuel and has no power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(7)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("stone", 0.85))
    # the preheater tower at the back, its chimney, the fuel hopper beside it
    box(m, mat("concrete"), -0.9, 0.06, 1.9, 0.6, 2.4, 3.3, mat_top=mat("concrete_dark"))
    box(m, mat("cement_accent"), -0.92, 1.9, 1.88, 0.62, 2.05, 3.32)
    cylinder(m, mat("concrete_dark"), (0.95, 0.06, 2.9), (0.95, 3.0, 2.9), 0.2, segs=8)
    cylinder(m, mat("shadow"), (0.95, 3.0, 2.9), (0.95, 3.001, 2.9), 0.13, segs=8)
    box(m, mat("metal_dark"), 0.75, 0.06, 1.7, 1.3, 0.8, 2.3, mat_top=mat("coal"))
    # the drum: sloping down from the tower to the front, on three piers, with tyre rings
    a, b = (-0.15, 1.35, 1.9), (-0.15, 0.65, -2.3)
    cylinder(m, mat("cement_accent", 0.85), a, b, 0.42, segs=14, mat_cap=mat("metal_dark"))
    for t in (0.15, 0.5, 0.85):
        p = tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
        q = tuple(a[i] + (b[i] - a[i]) * (t + 0.02) for i in range(3))
        cylinder(m, mat("metal_dark"), p, q, 0.47, segs=14, caps=False)
        box(m, mat("concrete_dark"), -0.45, 0.06, p[2] - 0.15, 0.15, p[1] - 0.4, p[2] + 0.15)
    # the glowing firing end and the clinker pile at the front
    cylinder(m, mat("furnace_glow"), (-0.15, 0.66, -2.31), (-0.15, 0.6, -2.4), 0.3, segs=12)
    spike(m, mat("concrete_dark"), (0.6, 0.06, -2.9), (0.6, 0.6, -2.9), 0.55, segs=9)
    spike(m, mat("concrete"), (-0.75, 0.06, -3.0), (-0.75, 0.4, -3.0), 0.4, segs=9)
    return m, mat.mats


def concrete_mixer():
    """3 x 4: a raised cement silo on legs, a mixing drum on a frame, and a chute at the front, a
    power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(4)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.06, hz, mat_top=mat("concrete", 0.9))
    # the silo at the back, raised on four legs, with a cone underneath
    for x in (-0.95, -0.15):
        for z in (0.55, 1.35):
            cylinder(m, mat("metal_dark"), (x, 0.06, z), (x, 1.2, z), 0.05, segs=5)
    spike(m, mat("cement_accent"), (-0.55, 1.4, 0.95), (-0.55, 0.85, 0.95), 0.5, segs=12)
    cylinder(m, mat("cement_accent"), (-0.55, 1.4, 0.95), (-0.55, 2.6, 0.95), 0.5, segs=12, mat_cap=mat("metal"))
    cylinder(m, mat("power_accent"), (-0.55, 2.3, 0.95), (-0.55, 2.38, 0.95), 0.51, segs=12, caps=False)
    # the mixing drum, tilted on its frame
    box(m, mat("metal_dark"), 0.1, 0.06, -0.8, 1.2, 0.5, 0.6, mat_top=mat("metal"))
    cylinder(m, mat("concrete_dark"), (0.65, 0.95, 0.5), (0.65, 0.7, -0.6), 0.45, segs=12, mat_cap=mat("metal_dark"))
    cylinder(m, mat("cement_accent"), (0.65, 0.88, 0.2), (0.65, 0.84, 0.05), 0.47, segs=12, caps=False)
    # the chute down to the front, a wet heap below it
    quad(m, mat("metal"), (0.4, 0.75, -0.6), (0.9, 0.75, -0.6), (0.9, 0.25, -1.5), (0.4, 0.25, -1.5), (0, 0.9, -0.5))
    spike(m, mat("concrete"), (0.65, 0.06, -1.6), (0.65, 0.3, -1.6), 0.35, segs=9)
    power_pole(m, mat, 1.15, 1.65, 1.6)
    return m, mat.mats


def brine_well():
    """4 x 6: a small wellhead and pump at the back and two shallow evaporation ponds in pale
    blue-white down the front, a salt heap, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(6)
    box(m, mat("sand"), -hx, 0, -hz, hx, 0.05, hz, mat_top=mat("sand", 1.05))
    # the wellhead and its pump house at the back
    box(m, mat("concrete"), -1.6, 0.05, 1.5, 0.2, 0.9, 2.8, mat_top=mat("lithium_accent"))
    box(m, mat("lithium_accent"), -1.62, 0.7, 1.48, 0.22, 0.78, 2.82)
    cylinder(m, mat("metal_dark"), (0.8, 0.05, 2.2), (0.8, 0.9, 2.2), 0.18, segs=8, mat_cap=mat("metal"))
    cylinder(m, mat("lithium_accent"), (0.8, 0.9, 2.2), (0.8, 1.0, 2.2), 0.24, segs=8)
    cylinder(m, mat("metal_dark"), (0.8, 0.5, 2.0), (0.8, 0.5, 1.2), 0.06, segs=6)
    # two ponds down the front: low berms round shallow brine, the nearer one drying to white
    for z0, z1, top in ((-0.3, 1.1, "brine"), (-2.75, -0.5, "frost")):
        box(m, mat(top), -1.65, 0.05, z0 + 0.12, 1.65, 0.1, z1 - 0.12)
        for bx0, bz0, bx1, bz1 in ((-1.8, z0, 1.8, z0 + 0.12), (-1.8, z1 - 0.12, 1.8, z1),
                                   (-1.8, z0, -1.65, z1), (1.65, z0, 1.8, z1)):
            box(m, mat("sand", 0.8), bx0, 0.05, bz0, bx1, 0.18, bz1)
    cylinder(m, mat("metal_dark"), (0.8, 0.2, 1.2), (0.8, 0.2, -0.4), 0.05, segs=6)
    # a salt heap at the side
    spike(m, mat("sack_white"), (1.45, 0.05, -2.2), (1.45, 0.5, -2.2), 0.4, segs=9)
    power_pole(m, mat, 1.65, 2.65, 1.6)
    return m, mat.mats


def semiconductor_fab():
    """5 x 6: a sealed white clean-room block with a stepped roof of air handlers, a chemical tank,
    and a loading door at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(5), half(6)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete", 0.95))
    # the clean room and its raised back block
    box(m, mat("ceramic"), -2.2, 0.08, -1.6, 1.4, 1.3, 2.8, mat_top=mat("metal"))
    box(m, mat("ceramic"), -2.2, 1.3, 0.8, 1.4, 1.9, 2.8, mat_top=mat("metal"))
    box(m, mat("fab_accent"), -2.22, 1.1, -1.62, 1.42, 1.2, 2.82)
    # air handlers along the roof, fans on top
    for k in range(3):
        x = -1.6 + k * 1.1
        box(m, mat("metal_dark"), x - 0.35, 1.9, 1.1, x + 0.35, 2.25, 2.5, mat_top=mat("metal"))
        cylinder(m, mat("shadow"), (x, 2.25, 1.8), (x, 2.26, 1.8), 0.25, segs=10)
    for k in range(2):
        x = -1.3 + k * 1.4
        box(m, mat("metal_dark"), x - 0.3, 1.3, -1.2, x + 0.3, 1.55, 0.4, mat_top=mat("metal"))
    # the loading door at the front, off to the right of the stand, with a violet frame
    box(m, mat("fab_accent"), 0.3, 0.08, -1.62, 1.2, 0.95, -1.6)
    box(m, mat("metal_dark"), 0.38, 0.08, -1.63, 1.12, 0.87, -1.62)
    # the chemical tank and pipes on the right
    cylinder(m, mat("metal"), (2.0, 0.08, 1.6), (2.0, 1.4, 1.6), 0.38, segs=10, mat_cap=mat("fab_accent"))
    cylinder(m, mat("metal_dark"), (1.62, 0.9, 1.6), (1.4, 0.9, 1.6), 0.05, segs=6)
    power_pole(m, mat, 2.15, -2.65, 1.6)
    return m, mat.mats


def air_separator():
    """4 x 5: two tall cold boxes, a compressor house, and a frosted Liquid Oxygen tank at the
    front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(5)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete", 0.9))
    # the compressor house at the back
    box(m, mat("metal"), -1.8, 0.08, 1.0, 0.6, 1.0, 2.3, mat_top=mat("metal_dark"))
    box(m, mat("gas_accent"), -1.82, 0.8, 0.98, 0.62, 0.88, 2.32)
    for k in range(3):
        cylinder(m, mat("shadow"), (-1.4 + k * 0.7, 1.0, 1.65), (-1.4 + k * 0.7, 1.01, 1.65), 0.22, segs=10)
    # two tall cold boxes, square columns with bands
    for x in (1.0, 1.55):
        box(m, mat("ceramic"), x - 0.22, 0.08, 0.3, x + 0.22, 3.0, 0.75, mat_top=mat("metal"))
        for y in (1.0, 2.0, 2.8):
            box(m, mat("gas_accent"), x - 0.23, y, 0.29, x + 0.23, y + 0.08, 0.76)
    cylinder(m, mat("metal_dark"), (0.6, 0.7, 1.4), (1.0, 0.7, 0.75), 0.06, segs=6)
    # the frosted oxygen tank lying at the front, on saddles
    for x in (-1.3, -0.3):
        box(m, mat("metal_dark"), x - 0.1, 0.08, -1.75, x + 0.1, 0.35, -1.05)
    cylinder(m, mat("frost"), (-1.65, 0.6, -1.4), (0.05, 0.6, -1.4), 0.38, segs=12, mat_cap=mat("gas_accent"))
    cylinder(m, mat("gas_accent"), (-0.85, 0.6, -1.4), (-0.75, 0.6, -1.4), 0.39, segs=12, caps=False)
    power_pole(m, mat, 1.65, -2.15, 1.6)
    return m, mat.mats


def enrichment_plant():
    """6 x 7: a long hall of centrifuge cascades under a raised roof, a containment dome at the back,
    and a cask pad at the front, a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(6), half(7)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete", 0.9))
    # the containment dome at the back
    cylinder(m, mat("concrete"), (-1.0, 0.08, 2.0), (-1.0, 1.4, 2.0), 1.2, segs=18)
    n = 6
    for k in range(n):
        a0 = (math.pi / 2) * k / n
        a1 = (math.pi / 2) * (k + 1) / n
        r0, y0 = 1.2 * math.cos(a0), 1.4 + 1.2 * math.sin(a0)
        r1, y1 = 1.2 * math.cos(a1), 1.4 + 1.2 * math.sin(a1)
        for s in range(18):
            b0 = 2 * math.pi * s / 18
            b1 = 2 * math.pi * (s + 1) / 18
            p = [(-1.0 + r * math.cos(b), y, 2.0 + r * math.sin(b)) for r, y, b in
                 ((r0, y0, b0), (r0, y0, b1), (r1, y1, b1), (r1, y1, b0))]
            mid = (math.cos((b0 + b1) / 2) * math.cos(a0), math.sin(a0) + 0.1, math.sin((b0 + b1) / 2) * math.cos(a0))
            quad(m, mat("concrete", 1.05), p[0], p[1], p[2], p[3], mid)
    cylinder(m, mat("nuclear_accent"), (-1.0, 1.1, 2.0), (-1.0, 1.25, 2.0), 1.21, segs=18, caps=False)
    # the cascade hall along the right: a raised roof, rows of centrifuges visible through its open front
    box(m, mat("metal"), 0.5, 0.08, -1.8, 2.8, 1.6, 3.3, mat_top=mat("metal_dark"))
    box(m, mat("nuclear_accent"), 0.48, 1.3, -1.82, 2.82, 1.42, 3.32)
    box(m, mat("shadow"), 0.7, 0.08, -1.81, 2.6, 1.2, -1.8)
    for row in range(2):
        for k in range(4):
            x = 0.95 + k * 0.45
            z = -2.1 - row * 0.4
            cylinder(m, mat("metal_dark" if row else "steel_light"), (x, 0.08, z), (x, 0.9, z), 0.12, segs=8)
    # the cask pad at the front left, two casks with hazard bands
    box(m, mat("concrete_dark"), -2.6, 0.08, -3.3, -1.1, 0.14, -2.0)
    for x in (-2.2, -1.5):
        cylinder(m, mat("metal_dark"), (x, 0.14, -2.65), (x, 0.9, -2.65), 0.28, segs=10, mat_cap=mat("metal"))
        cylinder(m, mat("nuclear_accent"), (x, 0.55, -2.65), (x, 0.65, -2.65), 0.29, segs=10, caps=False)
    power_pole(m, mat, -2.65, 0.6, 1.8)
    return m, mat.mats


def solar_array():
    """6 x 6: three rows of tilted dark-blue panels ruled in silver on low steel frames, and an
    inverter box with a power-yellow band at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(6), half(6)
    box(m, mat("grass", 0.8), -hx, 0, -hz, hx, 0.04, hz, mat_top=mat("mud", 1.15))
    tilt = math.radians(28)
    depth = 1.2
    for row, zc in enumerate((1.9, 0.3, -1.3)):
        # each row a long panel rising toward the back, on legs
        y0, y1 = 0.35, 0.35 + depth * math.sin(tilt)
        z0, z1 = zc - depth * math.cos(tilt) / 2, zc + depth * math.cos(tilt) / 2
        for x in (-2.5, 0.0, 2.5):
            cylinder(m, mat("metal_dark"), (x, 0.04, z0 + 0.1), (x, y0 + 0.05, z0 + 0.1), 0.04, segs=5)
            cylinder(m, mat("metal_dark"), (x, 0.04, z1 - 0.1), (x, y1 - 0.05, z1 - 0.1), 0.04, segs=5)
        n = (0, math.cos(tilt), -math.sin(tilt))
        quad(m, mat("solar"), (-2.75, y0, z0), (2.75, y0, z0), (2.75, y1, z1), (-2.75, y1, z1), n)
        quad(m, mat("metal_dark"), (-2.75, y0, z0), (-2.75, y1, z1), (2.75, y1, z1), (2.75, y0, z0), (0, -n[1], -n[2]))
        for k in range(1, 6):
            x = -2.75 + k * 5.5 / 6
            quad(m, mat("solar_line"), (x - 0.015, y0 + 0.004, z0), (x + 0.015, y0 + 0.004, z0),
                 (x + 0.015, y1 + 0.004, z1), (x - 0.015, y1 + 0.004, z1), n)
        ym, zm = (y0 + y1) / 2, (z0 + z1) / 2
        quad(m, mat("solar_line"), (-2.75, ym - 0.01 + 0.005, zm - 0.01), (2.75, ym - 0.01 + 0.005, zm - 0.01),
             (2.75, ym + 0.01 + 0.005, zm + 0.01), (-2.75, ym + 0.01 + 0.005, zm + 0.01), n)
    # the inverter at the front, off to the side
    box(m, mat("metal"), 1.6, 0.04, -2.85, 2.6, 0.7, -2.35, mat_top=mat("metal_dark"))
    box(m, mat("power_accent"), 1.58, 0.5, -2.87, 2.62, 0.58, -2.33)
    return m, mat.mats


def battery_bank():
    """3 x 4: a concrete pad with two rows of white battery cabinets, each with a green charge
    light, and a transformer at the front."""
    m, mat = Mesh(), Materials()
    hx, hz = half(3), half(4)
    box(m, mat("concrete_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete"))
    for x in (-0.75, 0.35):
        for k in range(3):
            z = 0.0 + k * 0.6
            box(m, mat("ceramic"), x - 0.38, 0.08, z, x + 0.38, 1.0, z + 0.5, mat_top=mat("metal"))
            box(m, mat("battery_accent"), x - 0.39, 0.82, z - 0.01, x + 0.39, 0.88, z + 0.51)
            box(m, mat("led"), x - 0.08, 0.6, z - 0.012, x + 0.08, 0.68, z - 0.01)
    # the transformer at the front, with fins and a yellow band
    box(m, mat("metal_dark"), -0.6, 0.08, -1.7, 0.4, 0.75, -1.0, mat_top=mat("metal"))
    for k in range(4):
        z = -1.62 + k * 0.18
        box(m, mat("metal_dark"), 0.4, 0.15, z, 0.55, 0.65, z + 0.06)
    box(m, mat("power_accent"), -0.62, 0.55, -1.72, 0.42, 0.62, -0.98)
    return m, mat.mats


def fission_reactor():
    """6 x 8: a hyperboloid cooling tower at the back with a white rim, a round reactor building with
    a domed roof, a turbine hall along the front, and an intake pipe; a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(6), half(8)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete", 0.9))
    # the cooling tower: a lathe narrowing to a waist and flaring to its lip
    cx, cz = 1.2, 2.1
    segs = 18
    profile = [(1.55, 0.08), (1.25, 1.2), (1.05, 2.4), (1.12, 3.4), (1.25, 3.9)]
    for k in range(len(profile) - 1):
        (r0, y0), (r1, y1) = profile[k], profile[k + 1]
        for s in range(segs):
            a0 = 2 * math.pi * s / segs
            a1 = 2 * math.pi * (s + 1) / segs
            p = [(cx + r0 * math.cos(a0), y0, cz + r0 * math.sin(a0)), (cx + r0 * math.cos(a1), y0, cz + r0 * math.sin(a1)),
                 (cx + r1 * math.cos(a1), y1, cz + r1 * math.sin(a1)), (cx + r1 * math.cos(a0), y1, cz + r1 * math.sin(a0))]
            out = (math.cos((a0 + a1) / 2), 0.2, math.sin((a0 + a1) / 2))
            quad(m, mat("concrete", 1.05 if k % 2 else 0.95), p[0], p[3], p[2], p[1], out)
    cylinder(m, mat("sack_white"), (cx, 3.75, cz), (cx, 3.9, cz), 1.27, segs=segs, caps=False)
    # its open top: a dark disc just inside the lip
    cylinder(m, mat("shadow"), (cx, 3.86, cz), (cx, 3.87, cz), 1.2, segs=segs)
    cylinder(m, mat("reactor_accent"), (cx, 0.9, cz), (cx, 1.05, cz), 1.33, segs=segs, caps=False)
    # the reactor building: a drum with a domed roof
    rx, rz = -1.4, 1.2
    cylinder(m, mat("concrete"), (rx, 0.08, rz), (rx, 1.8, rz), 1.1, segs=16)
    cylinder(m, mat("reactor_accent"), (rx, 1.5, rz), (rx, 1.65, rz), 1.11, segs=16, caps=False)
    n = 5
    for k in range(n):
        b0 = (math.pi / 2) * k / n
        b1 = (math.pi / 2) * (k + 1) / n
        for s in range(16):
            a0 = 2 * math.pi * s / 16
            a1 = 2 * math.pi * (s + 1) / 16
            r0, y0 = 1.1 * math.cos(b0), 1.8 + 0.8 * math.sin(b0)
            r1, y1 = 1.1 * math.cos(b1), 1.8 + 0.8 * math.sin(b1)
            p = [(rx + r0 * math.cos(a0), y0, rz + r0 * math.sin(a0)), (rx + r0 * math.cos(a1), y0, rz + r0 * math.sin(a1)),
                 (rx + r1 * math.cos(a1), y1, rz + r1 * math.sin(a1)), (rx + r1 * math.cos(a0), y1, rz + r1 * math.sin(a0))]
            quad(m, mat("concrete", 1.1), p[0], p[3], p[2], p[1],
                 (math.cos((a0 + a1) / 2) * math.cos(b0), math.sin(b0) + 0.1, math.sin((a0 + a1) / 2) * math.cos(b0)))
    # the turbine hall along the front
    box(m, mat("metal"), -2.7, 0.08, -3.7, 2.7, 1.3, -1.6, mat_top=mat("metal_dark"))
    box(m, mat("reactor_accent"), -2.72, 1.05, -3.72, 2.72, 1.17, -1.58)
    box(m, mat("shadow"), -0.5, 0.08, -3.71, 0.5, 0.9, -3.7)
    for x in (-1.9, 1.9):
        cylinder(m, mat("metal_dark"), (x, 1.3, -2.6), (x, 1.75, -2.6), 0.18, segs=8)
    # the intake pipe from the side to the tower
    cylinder(m, mat("metal_dark"), (2.85, 0.35, 0.3), (cx, 0.35, 0.3), 0.14, segs=8)
    cylinder(m, mat("metal_dark"), (cx, 0.35, 0.3), (cx, 0.35, cz - 1.4), 0.14, segs=8)
    power_pole(m, mat, -2.65, -0.9, 1.8)
    return m, mat.mats


def research_lab():
    """4 x 5: a low white lab with a glass-roofed bay at the back, a small dome with a slot and a dish
    on the roof, and a bench of instruments under an awning at the front; a power pole."""
    m, mat = Mesh(), Materials()
    hx, hz = half(4), half(5)
    box(m, mat("stone_dark"), -hx, 0, -hz, hx, 0.08, hz, mat_top=mat("concrete", 0.95))
    # the lab block and its mint band
    box(m, mat("ceramic"), -1.8, 0.08, -1.0, 1.2, 1.0, 1.4, mat_top=mat("metal"))
    box(m, mat("research_accent"), -1.82, 0.78, -1.02, 1.22, 0.88, 1.42)
    box(m, mat("shadow"), -0.4, 0.08, -1.01, 0.4, 0.75, -1.0)
    # the glass-roofed bay at the back
    box(m, mat("ceramic"), -1.8, 0.08, 1.4, 1.2, 0.7, 2.3, mat_top=mat("metal"))
    quad(m, mat("glass"), (-1.8, 0.7, 1.4), (1.2, 0.7, 1.4), (1.2, 1.0, 2.3), (-1.8, 1.0, 2.3), (0, 0.95, -0.3))
    # the dome on the roof, its slot, and a dish
    cylinder(m, mat("metal"), (-0.6, 1.0, 0.4), (-0.6, 1.15, 0.4), 0.55, segs=14)
    n = 4
    for k in range(n):
        b0 = (math.pi / 2) * k / n
        b1 = (math.pi / 2) * (k + 1) / n
        for s in range(14):
            a0 = 2 * math.pi * s / 14
            a1 = 2 * math.pi * (s + 1) / 14
            slot = s in (10, 11) and k < 3
            r0, y0 = 0.55 * math.cos(b0), 1.15 + 0.55 * math.sin(b0)
            r1, y1 = 0.55 * math.cos(b1), 1.15 + 0.55 * math.sin(b1)
            p = [(-0.6 + r0 * math.cos(a0), y0, 0.4 + r0 * math.sin(a0)), (-0.6 + r0 * math.cos(a1), y0, 0.4 + r0 * math.sin(a1)),
                 (-0.6 + r1 * math.cos(a1), y1, 0.4 + r1 * math.sin(a1)), (-0.6 + r1 * math.cos(a0), y1, 0.4 + r1 * math.sin(a0))]
            quad(m, mat("shadow" if slot else "ceramic"), p[0], p[3], p[2], p[1],
                 (math.cos((a0 + a1) / 2) * math.cos(b0), math.sin(b0) + 0.1, math.sin((a0 + a1) / 2) * math.cos(b0)))
    cylinder(m, mat("metal_dark"), (0.7, 1.0, 0.6), (0.7, 1.4, 0.6), 0.04, segs=5)
    spike(m, mat("metal"), (0.7, 1.55, 0.45), (0.7, 1.4, 0.75), 0.28, segs=10)
    # the instrument bench under an awning at the front
    for x in (-1.6, 1.0):
        cylinder(m, mat("metal_dark"), (x, 0.08, -2.25), (x, 0.9, -2.25), 0.04, segs=5)
    quad(m, mat("research_accent"), (-1.75, 0.9, -2.4), (1.15, 0.9, -2.4), (1.15, 1.0, -1.0), (-1.75, 1.0, -1.0), (0, 1, -0.07))
    box(m, mat("metal"), -1.5, 0.08, -2.05, -0.6, 0.45, -1.7, mat_top=mat("metal_dark"))
    box(m, mat("screen"), -1.4, 0.45, -1.95, -0.95, 0.7, -1.9)
    box(m, mat("metal"), 0.35, 0.08, -2.05, 0.95, 0.45, -1.7, mat_top=mat("metal_dark"))
    cylinder(m, mat("glass"), (0.65, 0.45, -1.88), (0.65, 0.7, -1.88), 0.08, segs=8)
    power_pole(m, mat, 1.65, 2.15, 1.6)
    return m, mat.mats


# ---- Infrastructure: road and bridge pieces ------------------------------------------------------
# Each covers a 2 x 2 tile, origin at its center on the ground. A road piece is authored with its open
# edges toward North (-Z) first, then East (+X): the end opens North, the straight North and South,
# the corner North and East, the tee every edge but West. The game turns each to fit its neighbors.

ROAD_TOP = 0.03
EDGE = 0.16


def _road(open_edges):
    m, mat = Mesh(), Materials()
    box(m, mat("gravel_edge"), -1.0, 0, -1.0, 1.0, ROAD_TOP, 1.0, mat_top=mat("gravel"))
    # a darker verge along every closed edge, and a few pebbles
    strips = {
        "N": (-1.0, -1.0, 1.0, -1.0 + EDGE),
        "S": (-1.0, 1.0 - EDGE, 1.0, 1.0),
        "W": (-1.0, -1.0, -1.0 + EDGE, 1.0),
        "E": (1.0 - EDGE, -1.0, 1.0, 1.0),
    }
    for edge, (x0, z0, x1, z1) in strips.items():
        if edge not in open_edges:
            box(m, mat("gravel_edge"), x0, ROAD_TOP, z0, x1, ROAD_TOP + 0.006, z1)
    for x, z in ((-0.4, 0.3), (0.35, -0.45), (0.1, 0.55)):
        box(m, mat("gravel_edge"), x - 0.05, ROAD_TOP, z - 0.04, x + 0.05, ROAD_TOP + 0.012, z + 0.04)
    return m, mat.mats


def road_patch():
    """A lone 2 x 2 patch of gravel with a darker verge all round."""
    return _road("")


def road_end():
    """A road's end, open to the North."""
    return _road("N")


def road_straight():
    """A straight run, open North and South."""
    return _road("NS")


def road_corner():
    """A corner, open North and East."""
    return _road("NE")


def road_tee():
    """A tee, open North, East, and South."""
    return _road("NES")


def road_cross():
    """A crossing, open on every edge."""
    return _road("NESW")


DECK = 0.14


def _deck(m, mat, z0, z1, y0, y1):
    """Planks across a deck running North-South from z0 (at height y0) to z1 (at y1), with rails."""
    n = 8
    for k in range(n):
        za = z0 + (z1 - z0) * k / n + 0.01
        zb = z0 + (z1 - z0) * (k + 1) / n - 0.01
        ya = y0 + (y1 - y0) * k / n
        yb = y0 + (y1 - y0) * (k + 1) / n
        shade = 1.0 if k % 2 else 0.9
        quad(m, mat("bridge_wood", shade), (-0.92, ya, za), (0.92, ya, za), (0.92, yb, zb), (-0.92, yb, zb), (0, 1, 0))
        quad(m, mat("bridge_rail"), (-0.92, ya - 0.06, za), (0.92, ya - 0.06, za), (0.92, ya, za), (-0.92, ya, za), (0, 0, -1))
    # stringers under the planks, and a rail on posts along each side
    for x in (-0.6, 0.6):
        quad(m, mat("bridge_rail"), (x, y0 - 0.08, z0), (x, y0 - 0.08, z1), (x, y1 - 0.08, z1), (x, y1 - 0.08, z0), (1, 0, 0))
    for x in (-0.95, 0.95):
        for k in range(3):
            z = z0 + (z1 - z0) * (0.12 + 0.38 * k)
            y = y0 + (y1 - y0) * (0.12 + 0.38 * k)
            box(m, mat("bridge_rail"), x - 0.04, y, z - 0.04, x + 0.04, y + 0.38, z + 0.04)
        cylinder(m, mat("bridge_wood"), (x, y0 + 0.36, z0), (x, y1 + 0.36, z1), 0.035, segs=5)


def bridge_deck():
    """A 2 x 2 bridge deck running North-South: planks across, a rail on posts along each side."""
    m, mat = Mesh(), Materials()
    _deck(m, mat, -1.0, 1.0, DECK, DECK)
    return m, mat.mats


def bridge_ramp():
    """A deck that slopes down toward the North onto the bank."""
    m, mat = Mesh(), Materials()
    _deck(m, mat, -1.0, 1.0, ROAD_TOP + 0.01, DECK)
    return m, mat.mats


def bridge_pier():
    """Two piles under a deck, down into the water, with a cross beam."""
    m, mat = Mesh(), Materials()
    for x in (-0.6, 0.6):
        cylinder(m, mat("bridge_rail"), (x, -0.7, 0), (x, DECK - 0.08, 0), 0.09, segs=6)
    box(m, mat("bridge_wood"), -0.75, DECK - 0.2, -0.07, 0.75, DECK - 0.08, 0.07)
    return m, mat.mats


BUILDINGS = {"mine": mine, "lumber_camp": lumber_camp, "smelter": smelter, "refinery": refinery,
             "oil_well": oil_well, "hq": hq, "silo": silo, "stockyard": stockyard, "tank_farm": tank_farm,
             "warehouse": warehouse, "vault": vault, "small_hub": small_hub, "relay": relay,
             "water_wheel": water_wheel, "burner_generator": burner_generator, "lumber_mill": lumber_mill,
             "drill_rig": drill_rig, "water_pump": water_pump, "pumping_station": pumping_station,
             "sand_collector": sand_collector, "sand_dredge": sand_dredge, "glassworks": glassworks,
             "depot": depot, "forester": forester, "quarry": quarry, "charcoal_kiln": charcoal_kiln,
             "rock_crusher": rock_crusher, "deep_well": deep_well, "deep_mine": deep_mine,
             "steel_mill": steel_mill, "electrolyzer": electrolyzer, "fuel_plant": fuel_plant,
             "chemical_plant": chemical_plant, "aluminum_smelter": aluminum_smelter,
             "electric_furnace": electric_furnace, "fabricator": fabricator,
             "electronics_works": electronics_works, "tile_kiln": tile_kiln, "assembly_plant": assembly_plant,
             "shipyard": shipyard, "display_stand": display_stand, "ship_engine": ship_engine,
             "ship_hull_ring": ship_hull_ring, "ship_nose": ship_nose, "sawmill": sawmill,
             "cement_kiln": cement_kiln, "concrete_mixer": concrete_mixer, "brine_well": brine_well,
             "semiconductor_fab": semiconductor_fab, "air_separator": air_separator,
             "enrichment_plant": enrichment_plant, "solar_array": solar_array,
             "battery_bank": battery_bank, "fission_reactor": fission_reactor,
             "research_lab": research_lab, "road_patch": road_patch, "road_end": road_end,
             "road_straight": road_straight, "road_corner": road_corner, "road_tee": road_tee,
             "road_cross": road_cross, "bridge_deck": bridge_deck, "bridge_ramp": bridge_ramp,
             "bridge_pier": bridge_pier}


def build_all():
    for name, fn in BUILDINGS.items():
        m, mats = fn()
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
    _ = math
