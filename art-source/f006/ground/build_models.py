"""F006 T519: procedural Blender models of the GROUND family (`f006-ground-base`, `-deck`, `-hatch`), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `<out-dir>/{ground-base,ground-deck,ground-hatch}.blend`. Flat emission materials in exact `f004-palette` 1.0.0
colours, one tone per element (no shading), the T435 plate recipe (rig-v2, 1 world unit = 24 art px): the deck core is a
2 x 2 cell slab (68 x 34 px at the 2:1 view) shifted by SHIFT so its rows are pixel-aligned; the pixel pass grows it by
1 px to the Art Bible 70 x 35 diamond. Ground allowance (Art Bible 21.2): ink_0 (hatch outline), ink_1, dim_0, floor_0,
floor_1. No floor_2, no steel_*, no plate_*, no lit_*, no teal_*. Layers sit EPS apart (no coplanar faces).
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

SX = 24.0 * math.sqrt(0.5)      # 16.97 screen px per world unit along (x + y)
HALF = 1.0                      # deck core half side: 68 x 34 px
SHIFT = 0.02945                 # +0.5 art px up the screen (T435): centres the diamond on a pixel row
EPS = 0.002


def wh(px_half_width):
    """World half side of a square whose screen half width is `px_half_width` art px."""
    return px_half_width / (2 * SX)


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


def sq(h):
    return rect(-h, h, -h, h)


def field(col):
    flat(col, "field", rect(-4.0, 4.0, -4.0, 4.0), 0, "floor_0")


def deck_core(col):
    flat(col, "deck", sq(HALF), 1, "floor_1")


def hatch(col):
    """Hatch over a location: ink_0 outline, dim_0 frame, floor_0 leaf with ink_1 slats along the 2:1 direction, and a
    dim_0 pad at the centre that stays free of slats, the backing of the section 8.5 padlock and level digit overlay."""
    flat(col, "outline", sq(wh(32)), 2, "ink_0")
    flat(col, "frame", sq(wh(31)), 3, "dim_0")
    leaf = wh(28)
    flat(col, "leaf", sq(leaf), 4, "floor_0")
    pitch, width, k = 0.34, 0.15, -8
    while -leaf + k * pitch < leaf:
        a, b = max(-leaf + k * pitch, -leaf), min(-leaf + k * pitch + width, leaf)
        if b - a > 1e-6:
            flat(col, "slat-%d" % k, rect(a, b, -leaf, leaf), 5, "ink_1")
        k += 1
    flat(col, "pad", sq(wh(18)), 6, "dim_0")


MODELS = {
    "ground-base": [field],
    "ground-deck": [deck_core],
    "ground-hatch": [deck_core, hatch],
}


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir>")
    out_dir = os.path.abspath(args[0])
    os.makedirs(out_dir, exist_ok=True)
    for name, parts in MODELS.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _MATS.clear()
        col = bpy.context.scene.collection
        for p in parts:
            p(col)
        if name != "ground-base":
            for ob in bpy.context.scene.objects:
                ob.location.x -= SHIFT
                ob.location.y += SHIFT
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, name + ".blend"), compress=True)
    print("BUILD-OK", len(MODELS))


main()
