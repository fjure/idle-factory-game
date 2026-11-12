"""F006 T520: procedural Blender models of the DEPOT family (12 sprites: stages s1..s3 x fills empty/low/high/full), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `<out-dir>/depot-s<k>-<fill>.blend` and `<out-dir>/depot-s<k>-<fill>.tones.json` (the palette ids used). Flat emission
materials in exact `f004-palette` 1.0.0 colours, one tone per face (no shading). Art Bible 21.2 DEPOT row: slab `steel_1` top,
`steel_2` rim, `steel_0` side (A3: no `plate_*`); hopper collar `copper_*`; delivery detail = the hopper opening (`dim_0`
interior, `res_ore_*` / `res_coal` fill); lamp = `copper_2` head + `steel_3` cap (no `lit_*`, no `teal_*`).
Coordinates: "c units" where a square of half side c has a screen half width of c art px on the 2:1 view (rig-v2, 24 px/unit):
screen x = (cx + cy) / 2, screen up = (cy - cx) / 4 + z (z in art px). Visible faces: top, -y (screen left), +x (screen right).
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
SZ = 24.0 * math.sin(math.radians(60))   # screen px per world unit of height
SLAB_HW, SLAB_H = 39, 6                  # slab core half width (art px) and thickness
HOP_HW, PIT_HW, COLLAR_Z = 17, 12, 18    # hopper collar outer / opening half width, collar top height
PIT_Z = 13                               # pit floor height

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
    """pts: four (cx, cy, z) corners in c units / art px."""
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((W(x), W(y), Z(z))) for x, y, z in pts])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    col.objects.link(bpy.data.objects.new(name, me))


def box(col, name, x0, x1, y0, y1, z0, z1, top, left=None, right=None):
    """Visible faces only: top (z1), left (-y, at y0) and right (+x, at x1); None skips a face."""
    if top:
        quad(col, name + "-t", [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], top)
    if left:
        quad(col, name + "-l", [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], left)
    if right:
        quad(col, name + "-r", [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], right)


def cube(col, name, cx, cy, h, z0, z1, top, left, right):
    box(col, name, cx - h, cx + h, cy - h, cy + h, z0, z1, top, left, right)


def slab(col):
    h = SLAB_HW
    box(col, "slab", -h, h, -h, h, 0, SLAB_H, "steel_2", "steel_0", "steel_0")
    box(col, "slab-top", -h + 3, h - 3, -h + 3, h - 3, SLAB_H, SLAB_H + 0.01, "steel_1")


def hopper(col, lip):
    z0, z1, o, i = SLAB_H, COLLAR_Z, HOP_HW, PIT_HW
    # pit: dim_0 floor and inner walls (the faces that look into the opening)
    quad(col, "pit-floor", [(-i, -i, PIT_Z), (i, -i, PIT_Z), (i, i, PIT_Z), (-i, i, PIT_Z)], "dim_0")
    quad(col, "pit-in-l", [(-i, i, PIT_Z), (i, i, PIT_Z), (i, i, z1), (-i, i, z1)], "dim_0")   # +y wall, faces -y
    quad(col, "pit-in-r", [(-i, -i, PIT_Z), (-i, i, PIT_Z), (-i, i, z1), (-i, -i, z1)], "dim_0")  # -x wall, faces +x
    top = "copper_1"
    # collar: four wall boxes (outer faces copper_0), ring top copper_1
    box(col, "wall-front-l", -o, o, -o, -i, z0, z1, top, "copper_0", "copper_0")      # -y wall
    box(col, "wall-front-r", i, o, -i, i, z0, z1, top, None, "copper_0")             # +x wall
    box(col, "wall-back-l", -o, -i, -i, i, z0, z1, top, None, None)                  # -x wall
    box(col, "wall-back-r", -o, o, i, o, z0, z1, top, None, None)                    # +y wall
    # copper_1 bands on the outer faces (visible collar detail)
    box(col, "band-l", -o - 0.4, o + 0.4, -o - 0.4, -o - 0.4, 11, 13, None, "copper_1", None)
    box(col, "band-r", o + 0.4, o + 0.4, -o - 0.4, o + 0.4, 11, 13, None, None, "copper_1")
    if lip:  # full state: copper_2 lip on the inner edge of the collar
        l = i + 2.5
        box(col, "lip-f", -l, l, -l, -i, z1, z1 + 1.5, "copper_2", "copper_2", None)
        box(col, "lip-r", i, l, -i, i, z1, z1 + 1.5, "copper_2", None, "copper_2")
        box(col, "lip-b1", -l, -i, -i, i, z1, z1 + 1.5, "copper_2")
        box(col, "lip-b2", -l, l, i, l, z1, z1 + 1.5, "copper_2")


def ore(col, name, h, z0, z1):
    cube(col, name, 0, 0, h, z0, z1, "res_ore_1", "res_ore_0", "res_ore_0")


def coal(col, name, cx, cy, z0, z1):
    cube(col, name, cx, cy, 2.2, z0, z1, "res_coal", "res_coal", "ink_0")


def fill(col, state):
    if state == "empty":
        return
    if state == "low":
        ore(col, "ore", 10, PIT_Z, PIT_Z + 2)
        coal(col, "coal-a", -3, 4, PIT_Z + 2, PIT_Z + 4.5)
        coal(col, "coal-b", 4, -3, PIT_Z + 2, PIT_Z + 4.5)
    elif state == "high":
        ore(col, "ore-0", 11, PIT_Z, PIT_Z + 4)
        ore(col, "ore-1", 8, PIT_Z + 4, PIT_Z + 8)
        coal(col, "coal-a", -3, 4, PIT_Z + 8, PIT_Z + 10.5)
        coal(col, "coal-b", 5, -4, PIT_Z + 4, PIT_Z + 6.5)
    else:  # full: heaped above the rim
        ore(col, "ore-0", 11, PIT_Z, COLLAR_Z)
        ore(col, "ore-1", 9, COLLAR_Z, COLLAR_Z + 4)
        ore(col, "ore-2", 6, COLLAR_Z + 4, COLLAR_Z + 7)
        coal(col, "coal-a", -2, 3, COLLAR_Z + 7, COLLAR_Z + 9.5)
        coal(col, "coal-b", 5, -5, COLLAR_Z + 4, COLLAR_Z + 6.5)


def lamp(col):
    cx, cy = -33, 33
    box(col, "lamp-post", cx - 2, cx + 2, cy - 2, cy + 2, SLAB_H, 27, "steel_2", "steel_1", "steel_1")
    box(col, "lamp-head", cx - 4, cx + 4, cy - 4, cy + 4, 27, 33, "copper_2", "copper_2", "copper_1")
    box(col, "lamp-cap", cx - 3, cx + 3, cy - 3, cy + 3, 33, 35, "steel_3", "steel_3", "steel_3")


def frame_s1(col):
    for nm, (cx, cy) in (("post-l", (-34, -34)), ("post-r", (34, 34))):
        box(col, nm, cx - 2, cx + 2, cy - 2, cy + 2, SLAB_H, 14, "steel_3", "steel_1", "steel_1")


def frame_s2(col):
    frame_s1(col)
    # low parapet along the two back edges, flush to the slab rim
    h = SLAB_HW
    box(col, "par-bl", -h, -h + 4, -h + 4, h, SLAB_H, 14, "steel_2", None, "steel_1")
    box(col, "par-br", -h + 4, h - 4, h - 4, h, SLAB_H, 14, "steel_2", "steel_0", "steel_1")


def frame_s3(col):
    frame_s1(col)
    # gantry beam between the left post and the lamp post, with a copper_1 hoist block
    box(col, "gantry-post", -35, -31, -35, -31, 14, 24, "steel_3", "steel_1", "steel_1")
    box(col, "beam", -34.5, -31.5, -31, 31, 24, 28, "steel_2", "steel_1", "steel_1")
    box(col, "hoist", -34.5, -31.5, -4, 2, 17, 24, "copper_1", "copper_0", "copper_0")


STAGES = {"s1": frame_s1, "s2": frame_s2, "s3": frame_s3}
FILLS = ("empty", "low", "high", "full")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir>")
    out_dir = os.path.abspath(args[0])
    os.makedirs(out_dir, exist_ok=True)
    n = 0
    for st, frame in STAGES.items():
        for f in FILLS:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            _MATS.clear()
            USED.clear()
            col = bpy.context.scene.collection
            slab(col)
            hopper(col, f == "full")
            fill(col, f)
            frame(col)
            lamp(col)
            name = "depot-%s-%s" % (st, f)
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, name + ".blend"), compress=True)
            with open(os.path.join(out_dir, name + ".tones.json"), "w") as fh:
                json.dump(sorted(USED), fh)
            n += 1
    print("BUILD-OK", n)


main()
