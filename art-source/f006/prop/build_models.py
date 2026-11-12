"""F006 T521: procedural Blender models of the PROP family (`f006-prop-*`), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `<out-dir>/{prop-crate-small,prop-crate-stack,prop-lamp-post,prop-pallet}.blend`. Flat emission materials in exact
`f004-palette` 1.0.0 colours, one tone per element (rig-v2, 1 world unit = 24 art px). Props are scenery (World Bible section 6),
at most 24 art px tall including the ink_0 outline, and use only the Art Bible 21.2 PROP margin tones: ink_0, ink_1, steel_0..3,
copper_0..2, dim_0, dim_1. No teal_*, no lit_* (the lamp is static: copper_2 head, steel_3 cap). Light from the top left: the left
face is lighter than the right face. Each model's origin is the centre of its base diamond on the ground.
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
ZS = 24.0 * math.sin(math.radians(60.0))
SHIFT = 0.02945
EPS = 0.004


def hs(w):
    """Half side of a box whose ground diamond is `w` px wide."""
    return w / (4 * SX)


def at(sx, sy):
    """World (x, y) of the ground point `sx` px right and `sy` px down of the model origin."""
    return (sx + 2 * sy) / (2 * SX), (sx - 2 * sy) / (2 * SX)


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


class Model:
    def __init__(self):
        self.quads = {}

    def q(self, cid, pts):
        self.quads.setdefault(cid, []).append([(x, y, z / ZS) for x, y, z in pts])

    def box(self, sx, sy, w, z0, z1, top, left, right):
        """Box with a `w` px wide ground diamond centred `sx`, `sy` from the origin; z0..z1 in px."""
        cx, cy = at(sx, sy)
        h = hs(w)
        x0, x1, y0, y1 = cx - h, cx + h, cy - h, cy + h
        self.q(top, [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)])
        self.q(left, [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)])
        self.q(right, [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)])
        return x0, x1, y0, y1

    def build(self, col):
        for cid, quads in self.quads.items():
            bm = bmesh.new()
            for pts in quads:
                bm.faces.new([bm.verts.new(p) for p in pts])
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            me = bpy.data.meshes.new("m-" + cid)
            bm.to_mesh(me)
            bm.free()
            me.materials.append(mat(cid))
            col.objects.link(bpy.data.objects.new("o-" + cid, me))


def crate(m, sx, sy, w, z0, z1, strap=True):
    """Steel crate: top steel_2, left steel_1, right steel_0, with a copper strap across the middle."""
    x0, x1, y0, y1 = m.box(sx, sy, w, z0, z1, "steel_2", "steel_1", "steel_0")
    if strap:
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        s = 1.6 / (4 * SX)                               # strap half width, 3.2 px
        m.q("copper_2", [(cx - s, y0, z1 + EPS * 20), (cx + s, y0, z1 + EPS * 20), (cx + s, y1, z1 + EPS * 20), (cx - s, y1, z1 + EPS * 20)])
        m.q("copper_1", [(cx - s, y0 - EPS, z0), (cx + s, y0 - EPS, z0), (cx + s, y0 - EPS, z1), (cx - s, y0 - EPS, z1)])
        m.q("copper_0", [(x1 + EPS, cy - s, z0), (x1 + EPS, cy + s, z0), (x1 + EPS, cy + s, z1), (x1 + EPS, cy - s, z1)])
        m.q("copper_2", [(x0, cy - s, z1 + EPS * 21), (x1, cy - s, z1 + EPS * 21), (x1, cy + s, z1 + EPS * 21), (x0, cy + s, z1 + EPS * 21)])


def crate_small():
    m = Model()
    crate(m, 0, 0, 20, 0, 8)
    return m


def crate_stack():
    m = Model()
    crate(m, -3, 0, 22, 0, 6)
    crate(m, 8, 1, 12, 0, 5, strap=False)
    crate(m, -4, -1, 14, 6, 12)
    return m


def lamp_post():
    m = Model()
    m.box(0, 0, 8, 0, 2, "steel_2", "steel_1", "steel_0")          # plinth
    m.box(0, 0, 2.6, 2, 12, "steel_2", "steel_2", "steel_1")       # post
    m.box(0, 0, 7, 12, 16, "copper_2", "copper_2", "copper_1")     # head: copper_2
    m.box(0, 0, 8, 16, 17.5, "steel_3", "steel_2", "steel_1")      # cap: steel_3
    return m


def pallet():
    m = Model()
    x0, x1, y0, y1 = m.box(0, 0, 24, 0, 2, "steel_1", "dim_1", "dim_0")
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    g = 0.9 / (4 * SX)
    for k in (-1, 1):
        yy = cy + k * (y1 - y0) / 6
        m.q("dim_0", [(x0, yy - g, 2 + EPS * 20), (x1, yy - g, 2 + EPS * 20), (x1, yy + g, 2 + EPS * 20), (x0, yy + g, 2 + EPS * 20)])
    return m


MODELS = {"prop-crate-small": crate_small, "prop-crate-stack": crate_stack, "prop-lamp-post": lamp_post, "prop-pallet": pallet}


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
        fn().build(bpy.context.scene.collection)
        for ob in bpy.context.scene.objects:
            ob.location.x -= SHIFT
            ob.location.y += SHIFT
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, name + ".blend"), compress=True)
    print("BUILD-OK", len(MODELS))


main()
