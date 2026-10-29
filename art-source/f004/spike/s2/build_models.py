"""F004 style spike S2 (T418): density variant of the S1 (T415) procedural model builder for Blender 5.2.1 (no add-ons).

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir> <D>

<D> is the art density (16 or 24). The geometry is the S1 geometry (authored for D = 24, 1 unit = 1 cell);
for D = 16 every vertex is scaled by 16/24 so that the unchanged rig-v1 cameras (128 px, ortho scale 128/24,
24 px per unit) render the model at 16 px per cell. The rig is not modified. D = 24 is the identity.

Writes one small .blend per asset state/frame (`<out-dir>/<asset-id>.blend`). The geometry is plain
prisms/polygons authored here (no external assets), colored from palette-v0.json (the spike input), so every
model is a pure function of this script. Units: 1 world unit = 1 cell = D px. A slot is 2x2 cells.

Silhouette source (F003-PAB v0.1 section 4, the F003 plan-view silhouettes that F004 restyles): Miner =
block with a hopper and a drill wedge pointing to the front; Assembler = disc with teeth. The pair with the
highest measured IoU (0.612 / 0.612) is Miner / Assembler; those are the two spike machines.

Height cap: the pipeline overhang rule is <= 0.5 cell above the footprint, so every machine feature stays
at or below 0.55 units.
"""
import hashlib
import json
import math
import os
import sys

import bmesh
import bpy
import mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
PALETTE = json.load(open(os.path.join(REPO, "tools", "art-pipeline", "palette", "palette-v0.json"), encoding="utf-8"))
HEX = {c["id"]: c["hex"] for g in PALETTE["groups"] for c in g["colors"]}
EMISSIVE = {c["id"] for g in PALETTE["groups"] for c in g["colors"] if c.get("emissive")}

LIT_GAIN = 2.0                  # compensates the toon term (measured: lit top faces render at ~0.25-0.5 of base)
AMBIENT = 0.25                  # emission fraction that keeps shadow sides readable
FRAMES = 6                      # running loop: 6 frames at 8 fps (research R16 / Art Bible v1 plan)
# Dimmed ramp used by the inactive still: every machine color maps to the dim ramp.
DIM_MAP = {"steel_0": "dim_0", "steel_1": "dim_0", "steel_2": "dim_1", "steel_3": "dim_1",
           "copper_0": "dim_0", "copper_1": "dim_0", "copper_2": "dim_1", "teal_0": "dim_0", "teal_1": "dim_1"}


def lin(c8):
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgba(cid, gain=1.0):
    h = HEX[cid].lstrip("#")
    return (lin(int(h[0:2], 16)) * gain, lin(int(h[2:4], 16)) * gain, lin(int(h[4:6], 16)) * gain, 1.0)


_MATS = {}


def mat(cid):
    if cid in _MATS and _MATS[cid].name in bpy.data.materials:
        return _MATS[cid]
    m = bpy.data.materials.new("pal-" + cid)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if cid in EMISSIVE:
        n = nt.nodes.new("ShaderNodeEmission")
        n.inputs["Color"].default_value = rgba(cid)
        nt.links.new(n.outputs["Emission"], out.inputs["Surface"])
    else:
        # Toon key-light term plus a small emission term that stands in for ambient light, so the shadow
        # sides keep a readable tone instead of near-black (the rig world is a dim 0.15 fill).
        n = nt.nodes.new("ShaderNodeBsdfToon")
        n.component = "DIFFUSE"
        n.inputs["Color"].default_value = rgba(cid, LIT_GAIN)
        n.inputs["Size"].default_value = 0.8
        n.inputs["Smooth"].default_value = 0.0
        e = nt.nodes.new("ShaderNodeEmission")
        e.inputs["Color"].default_value = rgba(cid)
        e.inputs["Strength"].default_value = AMBIENT
        add = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(n.outputs["BSDF"], add.inputs[0])
        nt.links.new(e.outputs["Emission"], add.inputs[1])
        nt.links.new(add.outputs["Shader"], out.inputs["Surface"])
    _MATS[cid] = m
    return m


def rot(points, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(x * c - y * s, x * s + y * c) for x, y in points]


def rect(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def ngon(r, n, start=0.0):
    return [(r * math.cos(start + 2 * math.pi * i / n), r * math.sin(start + 2 * math.pi * i / n)) for i in range(n)]


def clip_square(poly, lim):
    """Sutherland-Hodgman clip of a convex polygon to the square [-lim, lim]^2."""
    def clip(pts, inside, inter):
        out = []
        for i, p in enumerate(pts):
            q = pts[(i + 1) % len(pts)]
            pi, qi = inside(p), inside(q)
            if pi:
                out.append(p)
            if pi != qi:
                out.append(inter(p, q))
        return out
    pts = list(poly)
    for ax in (0, 1):
        for sign in (1, -1):
            if not pts:
                return []
            pts = clip(pts, lambda p, ax=ax, sign=sign: sign * p[ax] <= lim,
                       lambda p, q, ax=ax, sign=sign: _cut(p, q, ax, sign * lim))
    return pts


def _cut(p, q, ax, v):
    t = (v - p[ax]) / (q[ax] - p[ax])
    return (p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1]))


def prism(name, poly, z0, z1, cid, scene_col, remap=None):
    cid = (remap or {}).get(cid, cid)
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for x, y in poly]
    top = [bm.verts.new((x, y, z1)) for x, y in poly]
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    if area < 0:   # force counter-clockwise, then normals are recalculated anyway
        bot.reverse()
        top.reverse()
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([bot[i], bot[j], top[j], top[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    ob = bpy.data.objects.new(name, me)
    scene_col.objects.link(ob)
    return ob


def blob(name, center, r, squash, cid, scene_col):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    for v in bm.verts:
        v.co.z *= squash
        v.co.x += center[0]
        v.co.y += center[1]
        v.co.z += center[2]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    ob = bpy.data.objects.new(name, me)
    scene_col.objects.link(ob)
    return ob


# --- machines ---------------------------------------------------------------------------------

def miner(col, phase, lamp, remap=None):
    """Block body, hopper at the back, drill wedge pointing to the front (-y). `phase` in [0, 0.5) shifts the
    band pattern on the wedge toward the tip, which is the running animation."""
    P = lambda *a, **k: prism(*a, scene_col=col, remap=remap, **k)
    P("body", rect(-0.75, 0.75, -0.2, 0.45), 0.0, 0.4, "steel_1")
    P("body-top", rect(-0.62, 0.62, -0.08, 0.33), 0.4, 0.44, "steel_3")
    P("hopper", rect(-0.4, 0.4, 0.1, 0.42), 0.44, 0.55, "copper_1")
    P("hopper-in", rect(-0.28, 0.28, 0.17, 0.35), 0.55, 0.56, "copper_0")
    P("lamp", rect(0.48, 0.66, 0.0, 0.2), 0.44, 0.54, lamp)
    # drill wedge: apex (0, -0.94), base y = -0.2, half width 0.75; bands of width 0.25 along t, period 0.5
    def wedge_poly(ta, tb):
        def w(t):
            return 0.75 * (1.0 - t)
        y = lambda t: -0.2 - 0.74 * t
        return [(-w(ta), y(ta)), (w(ta), y(ta)), (w(tb), y(tb)), (-w(tb), y(tb))]
    k = -3
    while True:
        a, b = k * 0.25 + phase, (k + 1) * 0.25 + phase
        if a >= 1.0:
            break
        a, b = max(a, 0.0), min(b, 1.0)
        if b - a > 1e-6:
            tip = b >= 1.0 - 1e-9
            poly = wedge_poly(a, b)
            if tip:   # collapse the last band to the apex (degenerate edge removed)
                poly = [poly[0], poly[1], (0.0, -0.94)]
            P("drill-%d" % k, poly, 0.0, 0.3, "copper_2" if k % 2 == 0 else "steel_2")
        k += 1


def assembler(col, angle, lamp, remap=None):
    P = lambda *a, **k: prism(*a, scene_col=col, remap=remap, **k)
    P("disc", ngon(0.78, 24), 0.0, 0.3, "teal_0")
    for i in range(8):
        base = rect(0.58, 1.0, -0.14, 0.14)
        P("tooth-%d" % i, rot(base, angle + 45.0 * i), 0.0, 0.3, "copper_1" if i % 2 == 0 else "steel_2")
    P("face", ngon(0.62, 24), 0.3, 0.38, "teal_1")
    for i in range(4):
        P("spoke-%d" % i, rot(rect(0.26, 0.6, -0.05, 0.05), angle + 90.0 * i), 0.38, 0.395, "copper_0")
    P("hub", ngon(0.22, 16), 0.38, 0.55, lamp)


# --- slot plates and resources ------------------------------------------------------------------

def slot_occupied(col):
    prism("plate", rect(-1, 1, -1, 1), 0.0, 0.1, "plate_1", col)
    for i, (a, b) in enumerate([(rect(-1, 1, 0.9, 1), 0), (rect(-1, 1, -1, -0.9), 0),
                                (rect(-1, -0.9, -0.9, 0.9), 0), (rect(0.9, 1, -0.9, 0.9), 0)]):
        prism("rim-%d" % i, a, 0.1, 0.16, "plate_2", col)


def slot_empty(col):
    prism("plate", rect(-1, 1, -1, 1), 0.0, 0.06, "plate_0", col)
    arm, t, o = 0.45, 0.1, 0.12   # corner brackets inset by o
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (1 - o), sy * (1 - o)
            h = rect(min(cx, cx - sx * arm), max(cx, cx - sx * arm), min(cy, cy - sy * t), max(cy, cy - sy * t))
            v = rect(min(cx, cx - sx * t), max(cx, cx - sx * t), min(cy, cy - sy * arm), max(cy, cy - sy * arm))
            prism("br-h-%d%d" % (sx, sy), h, 0.06, 0.12, "plate_2", col)
            prism("br-v-%d%d" % (sx, sy), v, 0.06, 0.12, "plate_2", col)
    prism("plus-h", rect(-0.3, 0.3, -0.07, 0.07), 0.06, 0.12, "plate_2", col)
    prism("plus-v", rect(-0.07, 0.07, -0.3, 0.3), 0.06, 0.12, "plate_2", col)


def slot_locked(col):
    prism("plate", rect(-1, 1, -1, 1), 0.0, 0.1, "floor_2", col)
    k = -6
    while k * 0.34 < 3.0:   # 45 degree hatch bands, width 0.14, pitch 0.34 (clipped to the plate)
        c = k * 0.34
        band = [(-3.0, -3.0 + c - 0.07), (3.0, 3.0 + c - 0.07), (3.0, 3.0 + c + 0.07), (-3.0, -3.0 + c + 0.07)]
        poly = clip_square(band, 0.95)
        if len(poly) >= 3:
            prism("hatch-%d" % k, poly, 0.1, 0.15, "floor_0", col)
        k += 1
    prism("lock-body", rect(-0.25, 0.25, -0.3, 0.1), 0.15, 0.4, "steel_3", col)
    prism("shackle-l", rect(-0.17, -0.09, -0.2, 0.0), 0.4, 0.62, "steel_2", col)
    prism("shackle-r", rect(0.09, 0.17, -0.2, 0.0), 0.4, 0.62, "steel_2", col)
    prism("shackle-t", rect(-0.17, 0.17, -0.2, 0.0), 0.54, 0.62, "steel_2", col)


def slot_selected(col):
    for ring, (o, i) in enumerate([(1.0, 0.9), (0.8, 0.7)]):
        for j, r in enumerate([rect(-o, o, i, o), rect(-o, o, -o, -i), rect(-o, -i, -i, i), rect(i, o, -i, i)]):
            prism("sel-%d-%d" % (ring, j), r, 0.0, 0.06, "ui_accent", col)


def res_ore(col):
    blob("ore-a", (0.0, 0.0, 0.16), 0.24, 0.7, "res_ore_0", col)
    blob("ore-b", (0.26, -0.1, 0.12), 0.17, 0.7, "res_ore_1", col)
    blob("ore-c", (-0.22, -0.12, 0.1), 0.15, 0.7, "res_ore_0", col)


def res_ingot(col):
    prism("bar-a", rect(-0.3, 0.3, -0.04, 0.2), 0.0, 0.12, "res_gold", col)
    prism("bar-b", rect(-0.26, 0.3, -0.26, -0.06), 0.0, 0.12, "res_ore_1", col)
    prism("bar-c", rect(-0.2, 0.2, -0.0, 0.16), 0.12, 0.2, "res_gold", col)


def specs():
    out = {}
    for mid, fn, lamp_on in (("miner", miner, None), ("assembler", assembler, None)):
        for f in range(FRAMES):
            # loop frames: lamp lit on frames 0-3, dim on 4-5 (frame 0 is lights-on); pattern advances 1/6 period
            lamp = "lit_0" if f < 4 else "dim_1"
            if mid == "miner":
                arg = (f / FRAMES) * 0.5
            else:
                arg = f * 15.0
            out["s2-%s-run-%02d" % (mid, f)] = (fn, arg, lamp, None)
        out["s2-%s-paused-00" % mid] = (fn, 0.0, "dim_1", None)
        out["s2-%s-warning-00" % mid] = (fn, 0.0, "warn_0", None)
        out["s2-%s-inactive-00" % mid] = (fn, 0.0, "dim_0", DIM_MAP)
    return out


def scale_scene(density):
    """Scale all mesh vertices about the origin (the footprint center) by density / 24."""
    if density == 24:
        return
    f = density / 24.0
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            ob.data.transform(mathutils.Matrix.Scale(f, 4))


def main():
    argv = sys.argv
    out_dir = os.path.abspath(argv[argv.index("--") + 1])
    density = int(argv[argv.index("--") + 2])
    if density not in (16, 24):
        raise SystemExit("build_models.py: density must be 16 or 24")
    os.makedirs(out_dir, exist_ok=True)
    jobs = dict()
    for name, (fn, arg, lamp, remap) in specs().items():
        jobs[name] = lambda col, fn=fn, arg=arg, lamp=lamp, remap=remap: fn(col, arg, lamp, remap)
    jobs["s2-slot-empty-00"] = slot_empty
    jobs["s2-slot-locked-00"] = slot_locked
    jobs["s2-slot-occupied-00"] = slot_occupied
    jobs["s2-slot-selected-00"] = slot_selected
    jobs["s2-res-ore-00"] = res_ore
    jobs["s2-res-ingot-00"] = res_ingot
    written = {}
    for name in sorted(jobs):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _MATS.clear()
        scene = bpy.context.scene
        jobs[name](scene.collection)
        scale_scene(density)
        path = os.path.join(out_dir, name + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
        written[name] = path
    print("BUILD-OK", len(written))


main()
