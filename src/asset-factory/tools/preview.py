"""Render a 2 x 2 preview contact sheet for one or more generated OBJ models.

Run with the asset factory's environment (see setup_env.py):
    .venv/Scripts/python tools/preview.py <out.png> <model.obj> [<model.obj> ...] [options]

Views: the game camera (yaw 45, pitch 48), yaw 135, yaw 225, and top-down. Orthographic, lit like
the game (one directional light with shadows on a grass-colored ground). Several OBJ files are
drawn together in the same space, which is how arrangement checks are rendered.

With --each COLS the sheet is a family overview instead: every model is framed on its own from
the game camera only, in a grid COLS wide, in the order given (how previews/items.png is made).

Options:
    --size N      pixels per view (default 256), so the sheet is 2N x 2N
    --margin F    empty border around the model, as a fraction of the view (default 0.06)
    --colors N    quantize to an N-color palette (default 64; 0 keeps full color)
    --aa N        supersampling factor for anti-aliasing (default 3)
    --each COLS   one game-camera tile per model, in a grid COLS wide (--size is per tile)
"""
import argparse
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_glb import read_obj  # noqa: E402

GROUND_SRGB = (0.33, 0.47, 0.30)  # roughly the game's grass
LIGHT_DIR = np.array([0.413, -0.766, -0.492])  # direction light travels, game coordinates (Y up)
AMBIENT = 0.45
SUN = 0.75
VIEWS = [(45.0, 48.0), (135.0, 48.0), (225.0, 48.0), (45.0, 89.0)]  # (yaw, pitch above horizon)


def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def load(paths):
    """All triangles as an (n, 3, 3) array, with per-triangle base color and emission (linear)."""
    tris, colors, emits = [], [], []
    for path in paths:
        mats, by_mat = read_obj(path)
        for name, faces in by_mat.items():
            m = mats.get(name, {"Kd": (1.0, 0.0, 1.0), "Ke": None})
            kd = srgb_to_linear(m["Kd"])
            ke = srgb_to_linear(m["Ke"]) if m.get("Ke") else np.zeros(3)
            for face in faces:
                tris.append(face)
                colors.append(kd)
                emits.append(ke)
    return np.array(tris, dtype=np.float64), np.array(colors), np.array(emits)


def basis(forward):
    """Right and up vectors for a camera looking along `forward` (world Y up)."""
    f = forward / np.linalg.norm(forward)
    right = np.cross(f, [0.0, 1.0, 0.0])
    if np.linalg.norm(right) < 1e-6:
        right = np.array([1.0, 0.0, 0.0])
    right /= np.linalg.norm(right)
    up = np.cross(right, f)
    return f, right, up


def rasterize(tri2d, depth, w, h, zbuf, ids=None, tri_index=None):
    """Depth-test triangles (screen x, y with per-vertex depth) into zbuf (smaller is nearer)."""
    for i in range(len(tri2d)):
        (x0, y0), (x1, y1), (x2, y2) = tri2d[i]
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-12:
            continue
        xmin, xmax = max(int(math.floor(min(x0, x1, x2))), 0), min(int(math.ceil(max(x0, x1, x2))), w - 1)
        ymin, ymax = max(int(math.floor(min(y0, y1, y2))), 0), min(int(math.ceil(max(y0, y1, y2))), h - 1)
        if xmin > xmax or ymin > ymax:
            continue
        xs, ys = np.meshgrid(np.arange(xmin, xmax + 1) + 0.5, np.arange(ymin, ymax + 1) + 0.5)
        w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / area
        w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / area
        w2 = 1.0 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        z = w0 * depth[i, 0] + w1 * depth[i, 1] + w2 * depth[i, 2]
        region = zbuf[ymin:ymax + 1, xmin:xmax + 1]
        nearer = inside & (z < region)
        region[nearer] = z[nearer]
        if ids is not None:
            ids[ymin:ymax + 1, xmin:xmax + 1][nearer] = tri_index[i]


def render_view(tris, colors, emits, normals, yaw, pitch, extent, center, px):
    yr, pr = math.radians(yaw), math.radians(pitch)
    offset = np.array([math.cos(pr) * math.sin(yr), math.sin(pr), math.cos(pr) * math.cos(yr)])
    fwd, right, up = basis(-offset)
    scale = px / extent

    def to_screen(p):
        rel = p - center
        return np.stack([(rel @ right) * scale + px / 2, px / 2 - (rel @ up) * scale], -1), rel @ fwd

    # Shadow map along the light, covering the model and the ground around it.
    lf, lr, lu = basis(LIGHT_DIR)
    smap_px = 1024
    reach = extent * 1.5
    lscale = smap_px / reach

    def to_light(p):
        rel = p - center
        return np.stack([(rel @ lr) * lscale + smap_px / 2, smap_px / 2 - (rel @ lu) * lscale], -1), rel @ lf

    sxy, sz = to_light(tris)
    smap = np.full((smap_px, smap_px), np.inf)
    rasterize(sxy, sz, smap_px, smap_px, smap)

    xy, z = to_screen(tris)
    zbuf = np.full((px, px), np.inf)
    ids = np.full((px, px), -1, dtype=np.int64)
    rasterize(xy, z, px, px, zbuf, ids, np.arange(len(tris)))

    # World position of every pixel: on the model from its depth, elsewhere on the ground (y = 0).
    gx, gy = np.meshgrid(np.arange(px) + 0.5, np.arange(px) + 0.5)
    plane = center + ((gx - px / 2) / scale)[..., None] * right - ((gy - px / 2) / scale)[..., None] * up
    hit = ids >= 0
    t_ground = -plane[..., 1] / fwd[1] if abs(fwd[1]) > 1e-9 else np.zeros_like(gx)
    depth = np.where(hit, zbuf, t_ground)
    world = plane + depth[..., None] * fwd

    lxy, lz = to_light(world)
    lx = np.clip(lxy[..., 0].astype(int), 0, smap_px - 1)
    ly = np.clip(lxy[..., 1].astype(int), 0, smap_px - 1)
    lit = lz <= smap[ly, lx] + 0.03 * extent / 4

    sun_dir = -LIGHT_DIR / np.linalg.norm(LIGHT_DIR)
    tid = np.where(hit, ids, 0)
    n = normals[tid]
    n = np.where((np.sum(n * fwd, -1) > 0)[..., None], -n, n)  # face the camera (double-sided)
    ndl = np.where(hit, np.clip(np.sum(n * sun_dir, -1), 0, 1), max(sun_dir[1], 0))
    base = np.where(hit[..., None], colors[tid], srgb_to_linear(GROUND_SRGB))
    shade = AMBIENT + SUN * ndl * lit
    rgb = base * shade[..., None] + np.where(hit[..., None], emits[tid], 0)
    return linear_to_srgb(rgb)


def prepare(paths, views, margin):
    """Triangles, colors, emission, normals, centre, and the extent that fits every view."""
    tris, colors, emits = load(paths)
    e1, e2 = tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0]
    normals = np.cross(e1, e2)
    normals /= np.linalg.norm(normals, axis=1, keepdims=True) + 1e-12

    pts = tris.reshape(-1, 3)
    center = (pts.min(0) + pts.max(0)) / 2
    # One scale for all views: the widest projected extent among them, plus the margin.
    extent = 0.0
    for yaw, pitch in views:
        yr, pr = math.radians(yaw), math.radians(pitch)
        _, right, up = basis(-np.array([math.cos(pr) * math.sin(yr), math.sin(pr), math.cos(pr) * math.cos(yr)]))
        rel = pts - center
        extent = max(extent, np.ptp(rel @ right), np.ptp(rel @ up))
    return tris, colors, emits, normals, center, extent / (1 - 2 * margin)


def render_tile(model, yaw, pitch, size, aa):
    tris, colors, emits, normals, center, extent = model
    img = render_view(tris, colors, emits, normals, yaw, pitch, extent, center, size * aa)
    return img.reshape(size, aa, size, aa, 3).mean((1, 3))  # box-filter the supersamples


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("models", nargs="+")
    ap.add_argument("--size", type=int, default=256)
    ap.add_argument("--margin", type=float, default=0.06)
    ap.add_argument("--colors", type=int, default=64)
    ap.add_argument("--aa", type=int, default=3)
    ap.add_argument("--each", type=int, default=0, metavar="COLS")
    a = ap.parse_args()

    if a.each:
        game_view = VIEWS[0]
        tiles = [render_tile(prepare([path], [game_view], a.margin), *game_view, a.size, a.aa) for path in a.models]
        blank = np.ones_like(tiles[0]) * linear_to_srgb(srgb_to_linear(GROUND_SRGB))
        tiles += [blank] * (-len(tiles) % a.each)
        sheet = np.vstack([np.hstack(tiles[i:i + a.each]) for i in range(0, len(tiles), a.each)])
    else:
        model = prepare(a.models, VIEWS, a.margin)
        tiles = [render_tile(model, yaw, pitch, a.size, a.aa) for yaw, pitch in VIEWS]
        sheet = np.vstack([np.hstack(tiles[:2]), np.hstack(tiles[2:])])
    image = Image.fromarray((sheet * 255 + 0.5).astype(np.uint8), "RGB")
    if a.colors:
        image = image.quantize(colors=a.colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    image.save(a.out, optimize=True)
    print("saved %s (%d bytes)" % (a.out, os.path.getsize(a.out)))


if __name__ == "__main__":
    main()
