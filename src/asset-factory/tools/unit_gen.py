"""Unit models: things that move (trucks), authored facing North (front toward -Z).

Origin at the unit's center on the ground. A unit occupies no cells; its size is chosen to read
beside buildings (a fine cell is 1 unit, the player about 1.4 tall). See
src/Telaelus/assets/models/units/README.md.
"""
from mesh import Materials, Mesh, box, cylinder, quad


def truck():
    """A small flatbed, 1.6 long, 0.8 wide, about 0.85 tall: a red cab over the front third, a
    flat bed with low rails behind (the game sets the cargo's item model on it), four wheels."""
    m, mat = Mesh(), Materials()
    # wheels, axles across X
    for z in (-0.5, 0.48):
        for x in (-0.33, 0.33):
            side = 1 if x > 0 else -1
            cylinder(m, mat("rubber"), (x, 0.17, z), (x + side * 0.07, 0.17, z), 0.17, segs=10, mat_cap=mat("metal_dark"))
    # chassis
    box(m, mat("metal_dark"), -0.33, 0.12, -0.78, 0.33, 0.3, 0.78)
    # cab over the front third
    box(m, mat("depot_accent"), -0.4, 0.3, -0.8, 0.4, 0.85, -0.25, mat_top=mat("depot_accent", 0.85))
    quad(m, mat("panel_dark"), (-0.33, 0.56, -0.801), (0.33, 0.56, -0.801), (0.33, 0.78, -0.801),
         (-0.33, 0.78, -0.801), (0, 0, -1))
    for x, n in ((-0.401, -1), (0.401, 1)):
        quad(m, mat("panel_dark"), (x, 0.56, -0.72), (x, 0.56, -0.38), (x, 0.78, -0.38), (x, 0.78, -0.72), (n, 0, 0))
    for x in (-0.3, 0.3):
        box(m, mat("glass_edge"), x - 0.06, 0.36, -0.81, x + 0.06, 0.44, -0.8)
    box(m, mat("metal_dark"), -0.4, 0.3, -0.82, 0.4, 0.36, -0.79)
    # flat bed with low rails
    box(m, mat("metal"), -0.4, 0.3, -0.22, 0.4, 0.36, 0.8)
    for x in (-0.4, 0.37):
        box(m, mat("metal_dark"), x, 0.36, -0.22, x + 0.03, 0.46, 0.8)
    box(m, mat("metal_dark"), -0.4, 0.36, 0.77, 0.4, 0.46, 0.8)
    box(m, mat("depot_accent"), -0.4, 0.36, -0.22, 0.4, 0.5, -0.19)
    return m, mat.mats


UNITS = {"truck": truck}


def build_all():
    for name, fn in UNITS.items():
        m, mats = fn()
        m.write(name, mats)


if __name__ == "__main__":
    build_all()
