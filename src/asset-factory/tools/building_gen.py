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


BUILDINGS = {"mine": mine, "lumber_camp": lumber_camp, "smelter": smelter, "refinery": refinery,
             "oil_well": oil_well, "hq": hq, "silo": silo, "stockyard": stockyard, "tank_farm": tank_farm,
             "warehouse": warehouse, "vault": vault, "small_hub": small_hub, "relay": relay}


def build_all():
    for name, fn in BUILDINGS.items():
        m, mats = fn()
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
    _ = math
