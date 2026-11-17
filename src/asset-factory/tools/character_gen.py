"""The player character: a colonist in an orange work suit, about 1.4 tall, facing +Z (south).

Rigid low-poly parts, each hung from a joint and moved by it whole (nothing bends), so the model
is a tree of joints rather than a skinned mesh. Two looping clips: idle and run.

Run: python character_gen.py [<glb out dir>]
Writes models/character.obj (the rest pose) and models/character_pose_*.obj (poses sampled from
the clips, for the pose preview), and with an out dir also <dir>/character.glb (the animated
model). See src/Telaelus/assets/models/character/README.md.
"""
import math
import os
import sys

from mesh import OUT, Materials, Mesh, add, box, quad, sub

# ---------------------------------------------------------------- quaternions (x, y, z, w)


def q_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def q_axis(axis, degrees):
    h = math.radians(degrees) / 2
    s = math.sin(h)
    return (axis[0] * s, axis[1] * s, axis[2] * s, math.cos(h))


def q_euler(x=0.0, y=0.0, z=0.0):
    """Degrees, applied X first, then Y, then Z. A positive X tips a part's top toward +Z."""
    return q_mul(q_axis((0, 0, 1), z), q_mul(q_axis((0, 1, 0), y), q_axis((1, 0, 0), x)))


def q_rotate(q, v):
    qv = (v[0], v[1], v[2], 0.0)
    conj = (-q[0], -q[1], -q[2], q[3])
    r = q_mul(q_mul(q, qv), conj)
    return (r[0], r[1], r[2])


# ---------------------------------------------------------------- the figure

def tapered(m, mat, cx, cz, y0, w0, d0, y1, w1, d1, dz1=0.0, mat_top=None, bottom=True):
    """A box whose bottom (w0 x d0 at y0) and top (w1 x d1 at y1, moved dz1 in Z) differ."""
    b = [(cx + sx * w0 / 2, y0, cz + sz * d0 / 2) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    t = [(cx + sx * w1 / 2, y1, cz + dz1 + sz * d1 / 2) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    centre = (cx, (y0 + y1) / 2, cz + dz1 / 2)
    for i in range(4):
        j = (i + 1) % 4
        quad(m, mat, b[i], b[j], t[j], t[i], sub(add(b[i], t[j]), add(centre, centre)))
    quad(m, mat_top or mat, *t, (0, 1, 0))
    if bottom:
        quad(m, mat, *b, (0, -1, 0))


HIP_Y = 0.70      # the hips' pivot, where the legs hang from
LEG_X = 0.095     # each leg's distance from the middle
KNEE_Y = 0.37
SHOULDER_Y = 1.03
ARM_X = 0.235
ELBOW_Y = 0.79
NECK_Y = 1.10


def figure():
    """The joints, parent first: (name, parent, pivot, Mesh) at rest, in model space."""
    mat = Materials()
    suit, suit_dark, suit_light = mat("suit"), mat("suit", 0.78), mat("suit", 1.08)
    joints = []

    def joint(name, parent, pivot, build):
        m = Mesh()
        build(m)
        joints.append((name, parent, pivot, m))

    def hips(m):
        tapered(m, suit_dark, 0, 0, 0.62, 0.30, 0.18, 0.76, 0.32, 0.19)
        box(m, mat("boot"), -0.165, 0.72, -0.1, 0.165, 0.775, 0.1)            # belt
        box(m, mat("metal"), -0.035, 0.725, 0.1, 0.035, 0.77, 0.105)        # buckle

    def torso(m):
        tapered(m, suit, 0, 0, 0.76, 0.31, 0.18, 1.08, 0.38, 0.21, dz1=0.01, mat_top=suit_light)
        box(m, mat("canister"), -0.186, 0.93, 0.085, 0.186, 0.96, 0.117)    # reflective stripe
        box(m, mat("canister"), -0.186, 0.93, -0.106, 0.186, 0.96, -0.074)
        box(m, mat("suit", 0.9), -0.09, 1.075, -0.07, 0.09, 1.12, 0.08)     # collar

    def head(m):
        tapered(m, mat("skin"), 0, 0.01, 1.11, 0.20, 0.20, 1.37, 0.24, 0.24)
        tapered(m, mat("hair"), 0, -0.005, 1.33, 0.255, 0.255, 1.41, 0.22, 0.21)  # hair cap
        box(m, mat("hair"), -0.128, 1.20, -0.125, 0.128, 1.34, -0.07)           # back of the hair
        for x in (-0.055, 0.055):
            box(m, mat("shadow"), x - 0.022, 1.25, 0.128, x + 0.022, 1.29, 0.136)  # eyes
        box(m, mat("skin", 0.9), -0.02, 1.2, 0.128, 0.02, 1.245, 0.15)          # nose

    def upper_arm(side):
        def build(m):
            x = side * ARM_X
            tapered(m, suit, x, 0, ELBOW_Y - 0.01, 0.09, 0.10, SHOULDER_Y + 0.04, 0.11, 0.12)
        return build

    def forearm(side):
        def build(m):
            x = side * ARM_X
            tapered(m, suit, x, 0, 0.6, 0.08, 0.09, ELBOW_Y + 0.01, 0.09, 0.10)
            tapered(m, mat("boot"), x, 0.005, 0.5, 0.08, 0.085, 0.61, 0.095, 0.10)  # glove
        return build

    def thigh(side):
        def build(m):
            x = side * LEG_X
            tapered(m, suit_dark, x, 0, KNEE_Y - 0.01, 0.12, 0.13, HIP_Y, 0.14, 0.16)
        return build

    def shin(side):
        def build(m):
            x = side * LEG_X
            tapered(m, suit_dark, x, 0, 0.12, 0.11, 0.12, KNEE_Y + 0.01, 0.125, 0.13)
            tapered(m, mat("boot"), x, 0.025, 0.0, 0.13, 0.22, 0.14, 0.13, 0.16, dz1=-0.02)  # boot
        return build

    joint("hips", None, (0.0, HIP_Y, 0.0), hips)
    joint("torso", "hips", (0.0, 0.76, 0.0), torso)
    joint("head", "torso", (0.0, NECK_Y, 0.0), head)
    for side, tag in ((1, "l"), (-1, "r")):  # facing +Z, the character's left is +X
        joint("upper_arm_" + tag, "torso", (side * ARM_X, SHOULDER_Y, 0.0), upper_arm(side))
        joint("forearm_" + tag, "upper_arm_" + tag, (side * ARM_X, ELBOW_Y, 0.0), forearm(side))
    for side, tag in ((1, "l"), (-1, "r")):
        joint("thigh_" + tag, "hips", (side * LEG_X, HIP_Y, 0.0), thigh(side))
        joint("shin_" + tag, "thigh_" + tag, (side * LEG_X, KNEE_Y, 0.0), shin(side))
    return joints, mat.mats


# ---------------------------------------------------------------- clips
# Each clip is a period and a pose function of the phase (0..1): {joint: euler degrees} plus the
# hips' lift. Joints it leaves out stay at rest.

def idle(p):
    w = math.sin(2 * math.pi * p)
    breath = (1 - math.cos(2 * math.pi * p)) / 2
    return {
        "torso": (1.5 * breath, 0, 0),
        "head": (-1.5 * breath, 4 * math.sin(2 * math.pi * p), 0),
        "upper_arm_l": (1.5 * w, 0, 5 + 1.5 * breath),
        "upper_arm_r": (-1.5 * w, 0, -5 - 1.5 * breath),
        "forearm_l": (-8, 0, 0),
        "forearm_r": (-8, 0, 0),
    }, -0.006 * breath


def run(p):
    a = 2 * math.pi * p
    s, c = math.sin(a), math.cos(a)
    return {
        "hips": (0, -8 * s, 0),
        "torso": (10, 14 * s, 0),
        "head": (-6, -6 * s, 0),
        "upper_arm_l": (40 * s, 0, 7),
        "upper_arm_r": (-40 * s, 0, -7),
        "forearm_l": (-70 - 15 * max(0.0, -s), 0, 0),
        "forearm_r": (-70 - 15 * max(0.0, s), 0, 0),
        "thigh_l": (-42 * s - 8, 0, 0),
        "thigh_r": (42 * s - 8, 0, 0),
        "shin_l": (20 + 75 * max(0.0, c) ** 1.5, 0, 0),
        "shin_r": (20 + 75 * max(0.0, -c) ** 1.5, 0, 0),
    }, 0.035 * abs(s) - 0.05


CLIPS = [("idle", 3.0, idle, 30), ("run", 0.6, run, 24)]  # name, seconds, pose, keys per loop


# ---------------------------------------------------------------- posing

def posed(joints, pose):
    """The whole figure as one Mesh in model space, posed by a clip's (angles, lift)."""
    angles, lift = pose
    world = {}
    out = Mesh()
    for name, parent, pivot, m in joints:
        q = q_euler(*angles.get(name, (0, 0, 0)))
        if parent is None:
            pos, rot = add(pivot, (0, lift, 0)), q
        else:
            ppos, prot, ppivot = world[parent]
            pos, rot = add(ppos, q_rotate(prot, sub(pivot, ppivot))), q_mul(prot, q)
        world[name] = (pos, rot, pivot)
        out.tris += [(mt, *(add(pos, q_rotate(rot, sub(v, pivot))) for v in (a, b, c))) for mt, a, b, c in m.tris]
    return out


def rig(joints):
    """The joints and clips in export_glb.export_rig's shape."""
    rig_joints = []
    for name, parent, pivot, m in joints:
        by_mat = {}
        for mt, a, b, c in m.tris:
            by_mat.setdefault(mt, []).append((a, b, c))
        rig_joints.append({"name": name, "parent": parent, "pivot": pivot, "tris": by_mat})
    pivots = {name: pivot for name, _, pivot, _ in joints}
    clips = []
    for clip_name, seconds, pose, keys in CLIPS:
        times = [seconds * i / keys for i in range(keys + 1)]
        rotations = {name: [] for name, _, _, _ in joints}
        hips = []
        for i in range(keys + 1):
            angles, lift = pose((i % keys) / keys)
            for name in rotations:
                rotations[name].append(q_euler(*angles.get(name, (0, 0, 0))))
            hips.append(add(pivots["hips"], (0, lift, 0)))
        rotations = {k: v for k, v in rotations.items() if any(abs(q[3] - 1) > 1e-9 for q in v)}
        clips.append({"name": clip_name, "times": times, "rotations": rotations, "translations": {"hips": hips}})
    return rig_joints, clips


POSES = [("idle", 0.0), ("idle", 0.5), ("run", 0.0), ("run", 0.125), ("run", 0.25), ("run", 0.375)]


def build_all():
    joints, mats = figure()
    posed(joints, ({}, 0.0)).write("character", mats)
    clips = {name: pose for name, _, pose, _ in CLIPS}
    for i, (clip, phase) in enumerate(POSES):
        posed(joints, clips[clip](phase)).write("character_pose_%d" % i, mats)
    return joints, mats


if __name__ == "__main__":
    joints, mats = build_all()
    if len(sys.argv) > 1:
        from export_glb import export_rig
        os.makedirs(sys.argv[1], exist_ok=True)
        rig_joints, clips = rig(joints)
        export_rig("character", rig_joints, clips, mats, os.path.join(sys.argv[1], "character.glb"))
