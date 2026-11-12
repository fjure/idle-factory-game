"""F006 T521: procedural Blender models of the EDGE family (railing, wall, gantry; `f006-edge-*`), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `<out-dir>/<model>.blend` for the 11 sprites of Art Bible 21.4. Flat emission materials in exact `f004-palette` 1.0.0
colours, one tone per element (rig-v2, 1 world unit = 24 art px). Every model is one 68 x 34 px deck diamond (half side H = 1,
`floor_0` top, grown to the 70 x 35 Art Bible diamond by the pixel pass) with a 12 px fascia on the outer edges and the stage
structure on those edges. Sides: `fl` = the front-left edge (left apex to front apex), `fr` = the front-right edge (front apex
to right apex), `corner` = both edges meeting at the front apex.

Stage 1 railing  : posts steel_2 (faces) with steel_3 caps, rail steel_1; the quiet variants drop the mid posts (A4: the post
                   pattern of every stage is fixed, it does not grow in stages 2 and 3).
Stage 2 wall     : one tile family, low wall: top steel_2, outer faces steel_1 (left) / steel_0 (right), floor_1 plinth.
Stage 3 gantry   : new art: posts, top beam, lower rail, braces; steel_0..3, dim_0 braces, copper_1 hazard dashes.
Allowance (Art Bible 21.2): no lit_*, no teal_*, no plate_*, no floor_2. The pixel pass (pipeline_prop_edge.py) snaps to the
allowed ids and outlines the silhouette with ink_0.
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
ZS = 24.0 * math.sin(math.radians(60.0))   # 20.78 screen px per world unit of height
H = 1.0                         # deck half side: 68 x 34 px diamond
SHIFT = 0.02945                 # +0.5 art px up the screen (T435): pixel-aligns the diamond rows
EPS = 0.004
T = 12.0                        # fascia thickness, px


def px(v):
    """World length of `v` screen px of horizontal extent along one axis (a box of half side p is 4 * SX * p px wide)."""
    return v / (4 * SX)


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
    """Quads grouped by tone; one mesh object per tone."""

    def __init__(self):
        self.quads = {}

    def q(self, cid, pts):
        """pts: four (x, y, z_px) corners."""
        self.quads.setdefault(cid, []).append([(x, y, z / ZS) for x, y, z in pts])

    def box(self, x0, x1, y0, y1, z0, z1, top, left, right):
        """Axis-aligned box; only the faces the iso camera sees: top (z1), the -y face (left) and the +x face (right)."""
        if top:
            self.q(top, [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)])
        if left:
            self.q(left, [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)])
        if right:
            self.q(right, [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)])

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


# --- tile parts --------------------------------------------------------------------------------------------------

def slab(m, left, right):
    """Deck top (floor_0) and the fascia of the requested outer edges. Left fascia: a 2 px ink_1 shadow band under the lip,
    floor_0 below; right fascia ink_1 (the key light is from the top left)."""
    m.q("floor_0", [(-H, -H, 0), (H, -H, 0), (H, H, 0), (-H, H, 0)])
    if left:
        m.q("ink_1", [(-H, -H - EPS, -2), (H, -H - EPS, -2), (H, -H - EPS, 0), (-H, -H - EPS, 0)])
        m.q("floor_0", [(-H, -H - EPS, -T), (H, -H - EPS, -T), (H, -H - EPS, -2), (-H, -H - EPS, -2)])
    if right:
        m.q("ink_1", [(H + EPS, -H, -T), (H + EPS, H, -T), (H + EPS, H, 0), (H + EPS, -H, 0)])


def lpos(u):
    """x of a point at parameter u (0 = left apex, 1 = front apex) on the left edge."""
    return -H + 2 * H * u


def rpos(u):
    """y of a point at parameter u (0 = right apex, 1 = front apex) on the right edge."""
    return H - 2 * H * u


def post(m, x, y, h, w, faces, cap):
    p = px(w)                  # half side: a box of half side p is 4 * SX * p px wide
    m.box(x - p, x + p, y - p, y + p, 0, h, cap, faces[0], faces[1])


# stage 1 -------------------------------------------------------------------------------------------------------------

RAIL_POST_H, RAIL_POST_W = 8.0, 3.6


def railing(m, left_u, right_u):
    p = RAIL_POST_W / (4 * SX)
    for u in left_u:
        x = lpos(u)
        post(m, min(max(x, -H + p), H - p), -H + p, RAIL_POST_H, RAIL_POST_W, ("steel_2", "steel_2"), "steel_3")
    for u in right_u:
        y = rpos(u)
        post(m, H - p, min(max(y, -H + p), H - p), RAIL_POST_H, RAIL_POST_W, ("steel_2", "steel_2"), "steel_3")
    if left_u:
        m.q("steel_1", [(-H + p, -H + p - EPS, 5.5), (H - p, -H + p - EPS, 5.5), (H - p, -H + p - EPS, 7.0), (-H + p, -H + p - EPS, 7.0)])
    if right_u:
        m.q("steel_1", [(H - p + EPS, -H + p, 5.5), (H - p + EPS, H - p, 5.5), (H - p + EPS, H - p, 7.0), (H - p + EPS, -H + p, 7.0)])


# stage 2 -------------------------------------------------------------------------------------------------------------

WALL_H, WALL_D = 14.0, 3.4


def wall(m, left, right):
    d = WALL_D / (4 * SX)
    if left:
        m.box(-H, H, -H, -H + d, 0, WALL_H, "steel_2", "steel_1", None)
        m.q("floor_1", [(-H, -H - EPS, 0), (H, -H - EPS, 0), (H, -H - EPS, 3), (-H, -H - EPS, 3)])
        for u in (0.0, 0.5):
            x = lpos(u) + px(0.4)
            m.q("ink_1", [(x, -H - 2 * EPS, 3), (x + px(1.2), -H - 2 * EPS, 3), (x + px(1.2), -H - 2 * EPS, WALL_H), (x, -H - 2 * EPS, WALL_H)])
    if right:
        m.box(H - d, H, -H, H, 0, WALL_H, "steel_2", None, "steel_0")
        m.q("floor_1", [(H + EPS, -H, 0), (H + EPS, H, 0), (H + EPS, H, 3), (H + EPS, -H, 3)])
        for u in (0.0, 0.5):
            y = rpos(u) - px(1.2)
            m.q("ink_1", [(H + 2 * EPS, y, 3), (H + 2 * EPS, y + px(1.2), 3), (H + 2 * EPS, y + px(1.2), WALL_H), (H + 2 * EPS, y, WALL_H)])


# stage 3 -------------------------------------------------------------------------------------------------------------

GANTRY_H, GANTRY_W, BEAM_Z0 = 28.0, 4.4, 23.0


def gantry(m, left_u, right_u):
    p = GANTRY_W / (4 * SX)
    bd = px(2.4)
    # posts
    for u in left_u:
        post(m, min(max(lpos(u), -H + p), H - p), -H + p, GANTRY_H, GANTRY_W, ("steel_1", "steel_0"), "steel_3")
    for u in right_u:
        post(m, H - p, min(max(rpos(u), -H + p), H - p), GANTRY_H, GANTRY_W, ("steel_1", "steel_0"), "steel_3")
    if left_u:
        yp = -H + p
        # top beam (outer face steel_2, top steel_3), lower rail steel_0, braces dim_0, hazard dashes copper_1
        m.box(-H + p, H - p, yp - bd, yp + bd, BEAM_Z0, GANTRY_H, "steel_3", "steel_2", None)
        m.q("steel_0", [(-H + p, yp - bd - EPS, 11), (H - p, yp - bd - EPS, 11), (H - p, yp - bd - EPS, 13), (-H + p, yp - bd - EPS, 13)])
        for k in (0.0, 0.5):                     # brace per bay: up in the left bay, down in the right bay
            xa, xb = lpos(k) + p, lpos(k + 0.5) - p
            za, zb = (2.0, BEAM_Z0 - 0.5) if k == 0.0 else (BEAM_Z0 - 0.5, 2.0)
            w = px(3.0)
            m.q("dim_0", [(xa, yp - bd - 2 * EPS, za), (xa + w, yp - bd - 2 * EPS, za), (xb, yp - bd - 2 * EPS, zb), (xb - w, yp - bd - 2 * EPS, zb)])
        for k in (0.2, 0.7):
            xd = lpos(k)
            m.q("copper_1", [(xd, yp - bd - 3 * EPS, BEAM_Z0 + 1), (xd + px(3.0), yp - bd - 3 * EPS, BEAM_Z0 + 1),
                             (xd + px(3.0), yp - bd - 3 * EPS, GANTRY_H - 1), (xd, yp - bd - 3 * EPS, GANTRY_H - 1)])
    if right_u:
        xp = H - p
        m.box(xp - bd, xp + bd, -H + p, H - p, BEAM_Z0, GANTRY_H, "steel_3", None, "steel_1")
        m.q("steel_0", [(xp + bd + EPS, -H + p, 11), (xp + bd + EPS, H - p, 11), (xp + bd + EPS, H - p, 13), (xp + bd + EPS, -H + p, 13)])
        for k in (0.0, 0.5):
            ya, yb = rpos(k) - p, rpos(k + 0.5) + p
            za, zb = (2.0, BEAM_Z0 - 0.5) if k == 0.0 else (BEAM_Z0 - 0.5, 2.0)
            w = px(3.0)
            m.q("dim_0", [(xp + bd + 2 * EPS, ya, za), (xp + bd + 2 * EPS, ya - w, za), (xp + bd + 2 * EPS, yb, zb), (xp + bd + 2 * EPS, yb + w, zb)])
        for k in (0.2, 0.7):
            yd = rpos(k)
            m.q("copper_1", [(xp + bd + 3 * EPS, yd, BEAM_Z0 + 1), (xp + bd + 3 * EPS, yd - px(3.0), BEAM_Z0 + 1),
                             (xp + bd + 3 * EPS, yd - px(3.0), GANTRY_H - 1), (xp + bd + 3 * EPS, yd, GANTRY_H - 1)])


# --- models --------------------------------------------------------------------------------------------------------

def model_railing(side, quiet=False):
    m = Model()
    mid = 0.0 if quiet else 0.5
    ups = {"fl": ([0.0] if quiet else [0.0, mid], []), "fr": ([], [0.0] if quiet else [0.0, mid]),
           "corner": ([0.0, 0.5, 1.0], [0.0, 0.5])}[side]
    slab(m, side in ("fl", "corner"), side in ("fr", "corner"))
    railing(m, *ups)
    return m


def model_wall(side):
    m = Model()
    slab(m, side in ("fl", "corner"), side in ("fr", "corner"))
    wall(m, side in ("fl", "corner"), side in ("fr", "corner"))
    return m


def model_gantry(side):
    m = Model()
    slab(m, side in ("fl", "corner"), side in ("fr", "corner"))
    ups = {"fl": ([0.0, 0.5], []), "fr": ([], [0.0, 0.5]), "corner": ([0.0, 0.5, 1.0], [0.0, 0.5])}[side]
    gantry(m, *ups)
    return m


MODELS = {
    "edge-railing-fl": lambda: model_railing("fl"),
    "edge-railing-fr": lambda: model_railing("fr"),
    "edge-railing-corner": lambda: model_railing("corner"),
    "edge-railing-quiet-fl": lambda: model_railing("fl", True),
    "edge-railing-quiet-fr": lambda: model_railing("fr", True),
    "edge-wall-fl": lambda: model_wall("fl"),
    "edge-wall-fr": lambda: model_wall("fr"),
    "edge-wall-corner": lambda: model_wall("corner"),
    "edge-gantry-fl": lambda: model_gantry("fl"),
    "edge-gantry-fr": lambda: model_gantry("fr"),
    "edge-gantry-corner": lambda: model_gantry("corner"),
}


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
