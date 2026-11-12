"""Convert generated OBJ (+ MTL) models to flat-shaded GLB files. Standard library only.

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


def export(obj_path, glb_path):
    name = os.path.splitext(os.path.basename(obj_path))[0]
    mats, by_mat = read_obj(obj_path)
    blob = bytearray()
    views, accessors, prims, materials = [], [], [], []

    def add_view(data, target):
        while len(blob) % 4:
            blob.append(0)
        views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data), "target": target})
        blob.extend(data)
        return len(views) - 1

    for mat_name, tris in by_mat.items():
        if not tris:
            continue
        positions, normals = [], []
        for a, b, c in tris:
            n = face_normal(a, b, c)
            for p in (a, b, c):
                positions.append(p)
                normals.append(n)
        count = len(positions)
        pos_view = add_view(b"".join(struct.pack("<3f", *p) for p in positions), 34962)
        nrm_view = add_view(b"".join(struct.pack("<3f", *n) for n in normals), 34962)
        wide = count > 65535
        idx_view = add_view(struct.pack("<%d%s" % (count, "I" if wide else "H"), *range(count)), 34963)
        lo = [min(p[i] for p in positions) for i in range(3)]
        hi = [max(p[i] for p in positions) for i in range(3)]
        accessors += [
            {"bufferView": pos_view, "componentType": 5126, "count": count, "type": "VEC3", "min": lo, "max": hi},
            {"bufferView": nrm_view, "componentType": 5126, "count": count, "type": "VEC3"},
            {"bufferView": idx_view, "componentType": 5125 if wide else 5123, "count": count, "type": "SCALAR"},
        ]
        materials.append(material_json(mat_name, mats.get(mat_name, {"Kd": (1.0, 0.0, 1.0), "Ke": None, "d": 1.0})))
        base = len(accessors) - 3
        prims.append({"attributes": {"POSITION": base, "NORMAL": base + 1}, "indices": base + 2,
                      "material": len(materials) - 1})

    while len(blob) % 4:
        blob.append(0)
    doc = {
        "asset": {"version": "2.0", "generator": "asset-factory export_glb.py"},
        "extensionsUsed": ["KHR_materials_specular"],
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": name, "mesh": 0}],
        "meshes": [{"name": name, "primitives": prims}],
        "materials": materials,
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": len(blob)}],
    }
    js = json.dumps(doc, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    total = 12 + 8 + len(js) + 8 + len(blob)
    with open(glb_path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(js), b"JSON") + js)
        f.write(struct.pack("<I4s", len(blob), b"BIN\0") + bytes(blob))
    print("exported", glb_path)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for obj in sys.argv[2:]:
        export(obj, os.path.join(out_dir, os.path.splitext(os.path.basename(obj))[0] + ".glb"))
