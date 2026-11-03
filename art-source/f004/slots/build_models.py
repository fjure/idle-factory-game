"""F004 T435: procedural models of the slot plates (empty, locked, occupied base), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir> [plate ...]

Writes one small .blend per plate: `<out-dir>/<plate>.blend` with plate = empty | locked | occupied. Every model is a
pure function of this script and `palette-v1.json` (no external assets, no add-ons). Units as in the T434
machine models: 1 world unit = 1 cell = 24 art px (D24); the rig-v1 isometric camera looks along (-x, +y, -z), so
screen-right is (+x, +y), the visible walls are the -y wall (screen left) and the +x wall (screen right).

Materials are flat (emission) palette colours, exactly as the machines (T434 precedent): colours are chosen per
element, never shaded by the renderer. A plate is a 2 x 2 cell slab (the 70 x 35 art px plate diamond of Art Bible
3.2a once the pixel pass adds the outline) with

* a top face, a one-pixel `plate_2` ring on the top edge and a `plate_2` skirt on the base of both walls, so the
  pixel just inside the outline (the rim) is `plate_2` all around (Art Bible rule C2, 9.3: `plate_2` reaches 3:1
  over `floor_0` and `floor_1`),
* only tones of the allowed backdrop set A_OV (rule C1, 9.4): no `plate_0`, no `floor_2`, no `dim_1`, no `steel_0`.
  The occupied plate additionally uses only `plate_1` and `plate_2` (rule C5: the backdrop set for machine bodies).

Conditions (Art Bible 8.5): empty = open plate with corner marks, locked = dark plate with diagonal hatch and a
padlock (the level digit is the separate overlay label of T437, centred on the footprint), occupied = plain plate.
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

HALF = 1.0            # plate half side (2 x 2 cells)
THICK = 0.15          # slab thickness (about 3 art px of wall: plate silhouette 35 rows with the outline)
SKIRT = 0.06          # plate_2 skirt on the wall base (about 1.3 art px)
RING = 0.10           # width of the plate_2 ring on the top edge (about 1.5 art px vertical)
BEZEL = 0.23          # width of the lighter bezel band of the locked plate (the frame of T437 sits on it)
EPS = 0.002           # decal lift above its surface (0.04 art px; avoids z-fighting, no visible step)


def lin(c8):
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


_MATS = {}


def mat(cid):
    """Flat emission material in the exact palette colour (renders to the palette tone, no shading)."""
    if cid in _MATS and _MATS[cid].name in bpy.data.materials:
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


class Part:
    """Accumulates quads of one object; materials are assigned per face."""

    def __init__(self, name, col):
        self.name, self.col = name, col
        self.bm = bmesh.new()
        self.mats = []

    def quad(self, pts, cid):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        m = mat(cid)
        if m not in self.mats:
            self.mats.append(m)
        f.material_index = self.mats.index(m)

    def finish(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        self.col.objects.link(ob)
        return ob


def rect(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]       # counter-clockwise


def decal(col, name, poly, level, cid):
    """One flat polygon (top face only) in exactly one palette tone, `level` * EPS above the slab top. Every stacked
    layer has its own level (bezel 1, field 2, marks and hatch 3, padlock pad 4), so no two faces are coplanar."""
    p = Part(name, col)
    p.quad([(x, y, THICK + level * EPS) for x, y in poly], cid)
    return p.finish()


def block(col, name, poly, z0, z1, top, left, right, trim=None, skirt=0.0):
    """Prism with a flat tone per face orientation: `top`, `left` (-y wall) and `right` (+x wall). The optional
    `trim` tone covers the lowest `skirt` of every wall."""
    p = Part(name, col)
    p.quad([(x, y, z1) for x, y in poly], top)
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        (x0, y0), (x1, y1) = poly[i], poly[j]
        dx, dy = x1 - x0, y1 - y0
        nrm = (dy, -dx)                       # outward normal of a counter-clockwise polygon
        if nrm[1] < -1e-9:
            cid = left                        # faces -y: the screen-left wall
        elif nrm[0] > 1e-9:
            cid = right                       # faces +x: the screen-right wall
        else:
            continue                          # hidden walls (+y, -x)
        if trim and skirt > 0:
            p.quad([(x0, y0, z0), (x1, y1, z0), (x1, y1, z0 + skirt), (x0, y0, z0 + skirt)], trim)
            z0b = z0 + skirt
        else:
            z0b = z0
        p.quad([(x0, y0, z0b), (x1, y1, z0b), (x1, y1, z1), (x0, y0, z1)], cid)
    return p.finish()


def slab(col, top, ring, left, right, skirt_tone="plate_2", bezel=None, bezel_width=0.0):
    """The 2 x 2 cell slab: wall ramp, skirt, ring-framed top. With a `bezel` tone the top is a bezel band of
    `bezel_width` cells (inside the ring) around a `top` field."""
    block(col, "slab", rect(-HALF, HALF, -HALF, HALF), 0.0, THICK, ring, left, right, trim=skirt_tone, skirt=SKIRT)
    i0 = HALF - RING
    if bezel:
        decal(col, "bezel", rect(-i0, i0, -i0, i0), 1, bezel)
        i1 = i0 - bezel_width
        decal(col, "field", rect(-i1, i1, -i1, i1), 2, top)
    else:
        decal(col, "top", rect(-i0, i0, -i0, i0), 1, top)


def bracket(col, cx, cy, sx, sy, arm, t, level, cid):
    """L-shaped corner mark at the plate corner (cx, cy) pointing inward (sx, sy = +-1)."""
    xs = sorted((cx, cx - sx * arm))
    ys = sorted((cy, cy - sy * t))
    decal(col, "br-h", rect(xs[0], xs[1], ys[0], ys[1]), level, cid)
    xs = sorted((cx, cx - sx * t))
    ys = sorted((cy, cy - sy * arm))
    decal(col, "br-v", rect(xs[0], xs[1], ys[0], ys[1]), level, cid)


# --- plates -------------------------------------------------------------------------------------

def occupied(col):
    """Plain plate under a machine (never shown bare): `plate_1` top, `plate_2` ring and skirt, `plate_1` walls.
    Only `plate_1`, `plate_2` and the outline `ink_0` lie behind a machine outline (rule C5)."""
    slab(col, top="plate_1", ring="plate_2", left="plate_1", right="plate_1")


def empty(col):
    """Open plate: `steel_1` field, `plate_2` ring, `steel_3` L-shaped corner marks and a `plate_2` centre plus."""
    slab(col, top="steel_1", ring="plate_2", left="steel_1", right="steel_1")
    inner = HALF - RING
    o, arm, t = 0.10, 0.42, 0.095
    for sx in (-1, 1):
        for sy in (-1, 1):
            bracket(col, sx * (inner - o), sy * (inner - o), sx, sy, arm, t, 3, "steel_3")
    decal(col, "plus-h", rect(-0.26, 0.26, -0.065, 0.065), 3, "plate_2")
    decal(col, "plus-v", rect(-0.065, 0.065, -0.26, 0.26), 3, "plate_2")


def locked(col):
    """Dark plate in a `plate_1` bezel: `dim_0` field, `plate_2` ring and skirt, diagonal `ui_line` hatch, a `steel_3`
    padlock left of the footprint centre (the digit label of T437 sits on the centre). The bezel keeps the ink-framed
    selection frame of T437 over a tone that the ink outline carries (rule 9.3), the field stays dark."""
    slab(col, top="dim_0", ring="plate_2", left="plate_1", right="plate_1", bezel="plate_1", bezel_width=BEZEL)
    pitch, width = 0.30, 0.15
    k = -6
    inner = HALF - RING
    inner_f = inner - BEZEL
    while k * pitch < 2 * inner_f:
        x0 = -inner_f + k * pitch
        x1 = x0 + width
        a, b = max(x0, -inner_f), min(x1, inner_f)
        if b - a > 1e-6:
            decal(col, "hatch-%d" % k, rect(a, b, -inner_f, inner_f), 3, "ui_line")
        k += 1
    # padlock, a frontal one: the lock frame is rotated 45 degrees so its front faces the camera (screen right = u,
    # depth = v). Ink contact pad, steel body (front face and top), shackle as two posts and a bar.
    cx, cy = -0.28, -0.60

    def uv(u, v):
        s2 = math.sqrt(0.5)
        return (cx + (u - v) * s2, cy + (u + v) * s2)

    def urect(u0, u1, v0, v1):
        return [uv(u0, v0), uv(u1, v0), uv(u1, v1), uv(u0, v1)]     # counter-clockwise in world space

    decal(col, "lock-ink", urect(-0.22, 0.22, -0.2, 0.2), 4, "ink_0")
    zb = THICK + 5 * EPS
    bh = 0.26                                                         # body height
    block(col, "lock-body", urect(-0.26, 0.26, -0.12, 0.12), zb, zb + bh,
          top="steel_3", left="steel_1", right="steel_1")
    # keyhole on the front face (v = -0.12), ink_0
    kp = Part("keyhole", col)
    kv = -0.12 - EPS
    for (u0, u1, z0, z1) in ((-0.04, 0.04, 0.14, 0.205), (-0.016, 0.016, 0.06, 0.14)):
        x0, y0 = uv(u0, kv)
        x1, y1 = uv(u1, kv)
        kp.quad([(x0, y0, zb + z0), (x1, y1, zb + z0), (x1, y1, zb + z1), (x0, y0, zb + z1)], "ink_0")
    kp.finish()
    for nm, (u0, u1, z0, z1) in (("shackle-l", (-0.18, -0.08, bh, bh + 0.36)), ("shackle-r", (0.08, 0.18, bh, bh + 0.36)),
                                 ("shackle-t", (-0.18, 0.18, bh + 0.26, bh + 0.36))):
        block(col, nm, urect(u0, u1, -0.05, 0.05), zb + z0, zb + z1, top="steel_3", left="steel_3", right="steel_3")


PLATES = {"empty": empty, "locked": locked, "occupied": occupied}


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir> [plate ...]")
    out_dir = os.path.abspath(args[0])
    wanted = args[1:] or sorted(PLATES)
    os.makedirs(out_dir, exist_ok=True)
    for pid in wanted:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _MATS.clear()
        PLATES[pid](bpy.context.scene.collection)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "%s.blend" % pid), compress=True)
    print("BUILD-OK", len(wanted))


main()
