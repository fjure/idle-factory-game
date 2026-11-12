"""F006 T519: procedural Blender models of the FOUNDATION family (`f006-foundation-outline`, `-flat`), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Same recipe as `art-source/f006/ground/build_models.py` (flat emission palette materials, rig-v2, deck-sized 2 x 2 cell
slab, SHIFT pixel alignment). Both are near-flat: no wall, no plinth (R06, FR-012). The pixel pass adds the 1 px ink_0
outline, giving the 70 x 35 diamond hit shape.

* flat (occupied location): plate_1 top inside a plate_2 rim (the unchanged F004 plate role, Art Bible 21.2).
* outline (empty unlocked location): a floor_0 ring with thicker L-shaped corner marks; the interior stays transparent so
  the deck shows through and the build highlight is a tint on the sprite (Art Bible 21.4).
"""
import json
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PALETTE = json.load(open(os.path.join(REPO, "tools", "art-pipeline", "palette", "palette-v1.json"), encoding="utf-8"))
HEX = {c["id"]: c["hex"] for g in PALETTE["groups"] for c in g["colors"]}

HALF = 1.0
RING = 0.10
SHIFT = 0.02945
EPS = 0.002


def lin(c8):
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


_MATS = {}


def mat(cid):
    if cid in _MATS:
        return _MATS[cid]
    h = HEX[cid].lstrip("#")
    m = bpy.data.materials.new("pal-" + cid)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    n = nt.nodes.new("ShaderNodeEmission")
    n.inputs["Color"].default_value = (lin(int(h[0:2], 16)), lin(int(h[2:4], 16)), lin(int(h[4:6], 16)), 1.0)
    nt.links.new(n.outputs["Emission"], out.inputs["Surface"])
    _MATS[cid] = m
    return m


def rect(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def flat(col, name, poly, level, cid):
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((x, y, level * EPS)) for x, y in poly])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    col.objects.link(bpy.data.objects.new(name, me))


def flat_foundation(col):
    flat(col, "rim", rect(-HALF, HALF, -HALF, HALF), 1, "plate_2")
    i = HALF - RING
    flat(col, "top", rect(-i, i, -i, i), 2, "plate_1")


def outline_foundation(col):
    t = 0.2
    h = HALF
    for nm, r in (("n", rect(-h, h, h - t, h)), ("s", rect(-h, h, -h, -h + t)),
                  ("e", rect(h - t, h, -h, h)), ("w", rect(-h, -h + t, -h, h))):
        flat(col, "ring-" + nm, r, 1, "floor_0")
    arm, th = 0.6, 0.34
    for sx in (-1, 1):
        for sy in (-1, 1):
            xs = sorted((sx * h, sx * (h - arm)))
            ys = sorted((sy * h, sy * (h - th)))
            flat(col, "mk-h", rect(xs[0], xs[1], ys[0], ys[1]), 2, "floor_0")
            xs = sorted((sx * h, sx * (h - th)))
            ys = sorted((sy * h, sy * (h - arm)))
            flat(col, "mk-v", rect(xs[0], xs[1], ys[0], ys[1]), 2, "floor_0")


MODELS = {"foundation-flat": flat_foundation, "foundation-outline": outline_foundation}


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir>")
    out_dir = os.path.abspath(args[0])
    os.makedirs(out_dir, exist_ok=True)
    for name, fn in MODELS.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _MATS.clear()
        fn(bpy.context.scene.collection)
        for ob in bpy.context.scene.objects:
            ob.location.x -= SHIFT
            ob.location.y += SHIFT
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, name + ".blend"), compress=True)
    print("BUILD-OK", len(MODELS))


main()
