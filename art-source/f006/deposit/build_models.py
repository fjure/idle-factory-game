"""F006 T520: procedural Blender models of the DEPOSIT family (vein scenery rest-s1..s3) and the 6 vein-chip frames.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `deposit-rest-s{1,2,3}.blend`, `chip-0..5.blend` and a `.tones.json` per model. Flat emission materials in exact
`f004-palette` 1.0.0 colours. Art Bible 21.2 DEPOSIT row: `ink_0`, `res_ore_0`, `res_ore_1`, `res_coal`, `floor_1` patch,
`steel_3` highlight rim (added by the pixel pass for the static highlight). FX vein chip row: `res_ore_1`, `res_ore_0`,
`steel_3`, `ink_0`. Same c-unit coordinates as the depot builder (screen x = (cx + cy) / 2, up = (cy - cx) / 4 + z).
All models share the world origin (the patch centre), so the pixel pass crops them with one common window.
"""
import json
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PALETTE = json.load(open(os.path.join(REPO, "tools", "art-pipeline", "palette", "palette-v1.json"), encoding="utf-8"))
HEX = {c["id"]: c["hex"] for g in PALETTE["groups"] for c in g["colors"]}

SX = 24.0 * math.sqrt(0.5)
SZ = 24.0 * math.sin(math.radians(60))
PATCH_HW = 24
_MATS, USED = {}, set()


def lin(c8):
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mat(cid):
    USED.add(cid)
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


def W(c):
    return c / (2 * SX)


def Z(z):
    return z / SZ


def quad(col, name, pts, cid):
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((W(x), W(y), Z(z))) for x, y, z in pts])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    col.objects.link(bpy.data.objects.new(name, me))


def box(col, name, x0, x1, y0, y1, z0, z1, top, left=None, right=None):
    if top:
        quad(col, name + "-t", [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], top)
    if left:
        quad(col, name + "-l", [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], left)
    if right:
        quad(col, name + "-r", [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], right)


def chunk(col, name, cx, cy, h, z0, z1):
    box(col, name, cx - h, cx + h, cy - h, cy + h, z0, z1, "res_ore_1", "res_ore_0", "res_coal")


def coal(col, name, cx, cy, h, z1):
    box(col, name, cx - h, cx + h, cy - h, cy + h, 0, z1, "res_coal", "res_coal", "ink_0")


def patch(col):
    box(col, "patch", -PATCH_HW, PATCH_HW, -PATCH_HW, PATCH_HW, 0, 0, "floor_1")


def vein(col, stage):
    patch(col)
    chunk(col, "c-main", -4, 4, 6, 0.02, 14)
    chunk(col, "c-left", -2, -8, 4.5, 0.02, 9)
    chunk(col, "c-right", 9, 5, 4, 0.02, 7)
    coal(col, "k-a", 11, -6, 3, 4)
    coal(col, "k-b", -12, 0, 2.5, 3)
    if stage >= 2:
        chunk(col, "c-back", -14, 12, 4, 0.02, 9)
        coal(col, "k-c", 2, -13, 2.5, 3)
    if stage >= 3:
        chunk(col, "c-spire", -9, 6, 3, 0.02, 16)
        chunk(col, "c-far", 14, 14, 3.5, 0.02, 6)
        coal(col, "k-d", 15, 0, 2.5, 3)


CHIP_TOP = (-4, 4, 14)    # impact point: the top of the main chunk (cx, cy, z)
# per frame: (dx, dy, dz, half size) offsets from CHIP_TOP; 2 to 3 chips on ballistic arcs over 6 frames, frame 0 = impact
CHIPS = {
    0: [(0, 0, 0.5, 1.4), (1, 1, 1.5, 1.1)],
    1: [(-2, 1, 5, 1.4), (3, 2, 4, 1.2), (6, -4, 3, 1.1)],
    2: [(-4, 3, 8, 1.4), (6, 3, 6, 1.2), (11, -6, 6, 1.1)],
    3: [(-6, 4, 8, 1.3), (9, 4, 7, 1.2), (15, -8, 5, 1.1)],
    4: [(-8, 5, 4, 1.2), (12, 5, 3, 1.1), (19, -10, 2, 1.0)],
    5: [(-10, 6, 0.5, 1.0), (14, 6, 0, 1.0)],
}


def chips(col, i):
    cx0, cy0, z0 = CHIP_TOP
    for k, (dx, dy, dz, h) in enumerate(CHIPS[i]):
        x, y, z = cx0 + dx, cy0 + dy, z0 + dz
        box(col, "chip-%d-%d" % (i, k), x - h, x + h, y - h, y + h, z - h, z + h, "res_ore_1", "res_ore_0", "res_ore_0")
    if i == 0:   # impact spark: a steel_3 cube on the hit point
        box(col, "spark", cx0 - 1.0, cx0 + 1.0, cy0 - 1.0, cy0 + 1.0, z0 + 0.5, z0 + 2.5, "steel_3", "steel_3", "steel_3")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir>")
    out_dir = os.path.abspath(args[0])
    os.makedirs(out_dir, exist_ok=True)
    jobs = [("deposit-rest-s%d" % s, (lambda col, s=s: vein(col, s))) for s in (1, 2, 3)]
    jobs += [("chip-%d" % i, (lambda col, i=i: chips(col, i))) for i in range(6)]
    for name, fn in jobs:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _MATS.clear()
        USED.clear()
        fn(bpy.context.scene.collection)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, name + ".blend"), compress=True)
        with open(os.path.join(out_dir, name + ".tones.json"), "w") as fh:
            json.dump(sorted(USED), fh)
    print("BUILD-OK", len(jobs))


main()
