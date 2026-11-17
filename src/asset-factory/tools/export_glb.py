"""Convert generated OBJ (+ MTL) models to flat-shaded GLB files, and write animated models
(export_rig, called by character_gen.py). Standard library only.

Run: python export_glb.py <out_dir> <model.obj> [<model.obj> ...]
Each model is written to <out_dir>/<name>.glb.

The OBJ is Y up with counter-clockwise front faces, which is also glTF's convention, so positions
are copied as they are. Every triangle gets its own three vertices and its face normal (flat
shading). MTL colors are authored as sRGB and converted to linear for glTF. Materials are matte
(metallic 0, roughness 1, specular 0) and double-sided, matching what the game expects.
"""
import json
import math
import os
import struct
import sys


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def read_mtl(path):
    mats, cur = {}, None
    with open(path) as f:
        for line in f:
            p = line.split()
            if not p:
                continue
            if p[0] == "newmtl":
                cur = mats.setdefault(p[1], {"Kd": (1.0, 1.0, 1.0), "Ke": None, "d": 1.0})
            elif cur is not None and p[0] in ("Kd", "Ke"):
                cur[p[0]] = tuple(float(v) for v in p[1:4])
            elif cur is not None and p[0] == "d":
                cur["d"] = float(p[1])
    return mats


def read_obj(path):
    """Returns (materials from the mtllib, {material: [triangle (a, b, c)]}) in file order."""
    verts, by_mat, mats, cur = [], {}, {}, None
    with open(path) as f:
        for line in f:
            p = line.split()
            if not p:
                continue
            if p[0] == "mtllib":
                mats = read_mtl(os.path.join(os.path.dirname(path), p[1]))
            elif p[0] == "v":
                verts.append(tuple(float(v) for v in p[1:4]))
            elif p[0] == "usemtl":
                cur = p[1]
                by_mat.setdefault(cur, [])
            elif p[0] == "f":
                idx = [int(t.split("/")[0]) for t in p[1:]]
                idx = [i - 1 if i > 0 else len(verts) + i for i in idx]
                for k in range(1, len(idx) - 1):  # fan-triangulate polygons
                    by_mat.setdefault(cur, []).append((verts[idx[0]], verts[idx[k]], verts[idx[k + 1]]))
    return mats, by_mat


def face_normal(a, b, c):
    u = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    v = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    length = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) or 1.0
    return (n[0] / length, n[1] / length, n[2] / length)


def material_json(name, m):
    kd = [srgb_to_linear(c) for c in m["Kd"]]
    out = {
        "name": name,
        "doubleSided": True,
        "pbrMetallicRoughness": {
            "baseColorFactor": kd + [m["d"]],
            "metallicFactor": 0.0,
            "roughnessFactor": 1.0,
        },
        "extensions": {"KHR_materials_specular": {"specularFactor": 0.0}},
    }
    if m["Ke"]:
        out["emissiveFactor"] = [min(1.0, srgb_to_linear(c)) for c in m["Ke"]]
    if m["d"] < 1.0:
        out["alphaMode"] = "BLEND"
    return out


class Glb:
    """The binary buffer, buffer views, accessors, and materials of one .glb being written."""

    def __init__(self):
        self.blob = bytearray()
        self.views, self.accessors, self.materials = [], [], []

    def view(self, data, target=None):
        while len(self.blob) % 4:
            self.blob.append(0)
        v = {"buffer": 0, "byteOffset": len(self.blob), "byteLength": len(data)}
        if target is not None:
            v["target"] = target
        self.views.append(v)
        self.blob.extend(data)
        return len(self.views) - 1

    def floats(self, values, kind, with_bounds=False):
        """An accessor over float tuples (or plain floats for SCALAR); returns its index."""
        width = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[kind]
        rows = [v if width > 1 else (v,) for v in values]
        acc = {"bufferView": self.view(b"".join(struct.pack("<%df" % width, *r) for r in rows)),
               "componentType": 5126, "count": len(rows), "type": kind}
        if with_bounds:
            acc["min"] = [min(r[i] for r in rows) for i in range(width)]
            acc["max"] = [max(r[i] for r in rows) for i in range(width)]
        self.accessors.append(acc)
        return len(self.accessors) - 1

    def primitives(self, by_mat, mats, offset=(0.0, 0.0, 0.0)):
        """Flat-shaded primitives, one per material, with positions moved by -offset."""
        prims = []
        for mat_name, tris in by_mat.items():
            if not tris:
                continue
            positions, normals = [], []
            for a, b, c in tris:
                n = face_normal(a, b, c)
                for p in (a, b, c):
                    positions.append((p[0] - offset[0], p[1] - offset[1], p[2] - offset[2]))
                    normals.append(n)
            count = len(positions)
            pos_view = self.view(b"".join(struct.pack("<3f", *p) for p in positions), 34962)
            nrm_view = self.view(b"".join(struct.pack("<3f", *n) for n in normals), 34962)
            wide = count > 65535
            idx_view = self.view(struct.pack("<%d%s" % (count, "I" if wide else "H"), *range(count)), 34963)
            lo = [min(p[i] for p in positions) for i in range(3)]
            hi = [max(p[i] for p in positions) for i in range(3)]
            self.accessors += [
                {"bufferView": pos_view, "componentType": 5126, "count": count, "type": "VEC3", "min": lo, "max": hi},
                {"bufferView": nrm_view, "componentType": 5126, "count": count, "type": "VEC3"},
                {"bufferView": idx_view, "componentType": 5125 if wide else 5123, "count": count, "type": "SCALAR"},
            ]
            self.materials.append(material_json(mat_name, mats.get(mat_name, {"Kd": (1.0, 0.0, 1.0), "Ke": None, "d": 1.0})))
            base = len(self.accessors) - 3
            prims.append({"attributes": {"POSITION": base, "NORMAL": base + 1}, "indices": base + 2,
                          "material": len(self.materials) - 1})
        return prims

    def write(self, path, scene):
        """Writes the file: `scene` holds scene, scenes, nodes, meshes, and any animations."""
        while len(self.blob) % 4:
            self.blob.append(0)
        doc = {
            "asset": {"version": "2.0", "generator": "asset-factory export_glb.py"},
            "extensionsUsed": ["KHR_materials_specular"],
            **scene,
            "materials": self.materials,
            "accessors": self.accessors,
            "bufferViews": self.views,
            "buffers": [{"byteLength": len(self.blob)}],
        }
        js = json.dumps(doc, separators=(",", ":")).encode()
        js += b" " * (-len(js) % 4)
        total = 12 + 8 + len(js) + 8 + len(self.blob)
        with open(path, "wb") as f:
            f.write(struct.pack("<4sII", b"glTF", 2, total))
            f.write(struct.pack("<I4s", len(js), b"JSON") + js)
            f.write(struct.pack("<I4s", len(self.blob), b"BIN\0") + bytes(self.blob))
        print("exported", path)


def export(obj_path, glb_path):
    name = os.path.splitext(os.path.basename(obj_path))[0]
    mats, by_mat = read_obj(obj_path)
    glb = Glb()
    prims = glb.primitives(by_mat, mats)
    glb.write(glb_path, {
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": name, "mesh": 0}],
        "meshes": [{"name": name, "primitives": prims}],
    })


def export_rig(name, joints, clips, mats, glb_path):
    """Writes an animated model: a tree of rigid parts, each moved by its joint node.

    joints: in parent-first order, each {"name", "parent" (a joint name or None), "pivot" (its rest
        position in model space), "tris" ({material: [(a, b, c)]} in model space, at rest)}.
    clips: each {"name", "times" [seconds], "rotations" {joint: [(x, y, z, w)]}, "translations"
        {joint: [(x, y, z)] node translations}}, one value per time, interpolated linearly.
    mats: {material: (Kd, Ke)} as sRGB, the generators' Materials table.
    """
    mats = {k: {"Kd": kd, "Ke": ke, "d": 1.0} for k, (kd, ke) in mats.items()}
    glb = Glb()
    index = {j["name"]: i + 1 for i, j in enumerate(joints)}
    pivots = {j["name"]: j["pivot"] for j in joints}
    nodes = [{"name": name, "children": [index[j["name"]] for j in joints if j["parent"] is None]}]
    meshes = []
    for j in joints:
        parent = pivots[j["parent"]] if j["parent"] else (0.0, 0.0, 0.0)
        node = {"name": j["name"], "translation": [j["pivot"][i] - parent[i] for i in range(3)]}
        if j["tris"]:
            meshes.append({"name": j["name"], "primitives": glb.primitives(j["tris"], mats, j["pivot"])})
            node["mesh"] = len(meshes) - 1
        children = [index[k["name"]] for k in joints if k["parent"] == j["name"]]
        if children:
            node["children"] = children
        nodes.append(node)
    animations = []
    for clip in clips:
        samplers, channels = [], []
        times = glb.floats(clip["times"], "SCALAR", with_bounds=True)
        for path, kind, tracks in (("rotation", "VEC4", clip["rotations"]), ("translation", "VEC3", clip["translations"])):
            for joint, values in tracks.items():
                samplers.append({"input": times, "output": glb.floats(values, kind), "interpolation": "LINEAR"})
                channels.append({"sampler": len(samplers) - 1, "target": {"node": index[joint], "path": path}})
        animations.append({"name": clip["name"], "samplers": samplers, "channels": channels})
    glb.write(glb_path, {
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": nodes,
        "meshes": meshes,
        "animations": animations,
    })


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for obj in sys.argv[2:]:
        export(obj, os.path.join(out_dir, os.path.splitext(os.path.basename(obj))[0] + ".glb"))
