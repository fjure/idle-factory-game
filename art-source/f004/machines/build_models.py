"""F004 T434: procedural models of the four production machines (Miner, Smelter, Press, Assembler), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir> [machine ...]

Writes one small .blend per machine and loop frame: `<out-dir>/<machine>-run-<NN>.blend` (NN = 00..05).
Every model is a pure function of this script and `palette-v1.json` (no external assets, no add-ons). Units:
1 world unit = 1 cell = 24 art px (D24); the rig-v2 isometric camera looks along (-x, +y, -z), so the visible
walls are the -y wall (screen left) and the +x wall (screen right).

Materials are flat (emission) palette colours, chosen per FACE ORIENTATION from a fixed ramp: top faces lightest,
left-facing walls mid, right-facing walls darkest (Art Bible section 2 and 6.2, lighting B: the body is flat and
carries no further baked highlight). This replaces the spike's toon + ambient material, which collapsed the
walls to a single dark tone. The ramp is a pure function of the face normal (`LIGHT_TOWARD`, the screen top-left).

Rim rule (Art Bible 9.3 / C5, T434 requirement): the ink outline reaches 3:1 only over light backdrops, so the
pixel just inside it (the rim) must be a tone that reaches 3:1 over `floor_0` and `floor_1`: `steel_2`,
`steel_3`, `copper_2`, `teal_1` or `lit_*`. The model therefore gives every part (a) a light trim skirt at its base,
(b) light corner strips on its vertical corners (square parts), and (c) light top faces. The ramp tones of the
walls stay darker inside that frame.

Height cap: the body starts at canvas row 34 or lower (30 rows including the outline), so every machine stays
within about 27 art px of silhouette height; `check_overlay_keepout` and `check_overhang` enforce it.
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

FRAMES = 6
# Direction toward the light in world space (screen top-left, from above): lambert bands pick the ramp tone.
LIGHT_TOWARD = (-0.4243, -0.4243, 0.8)
# Assembler wheel (T466): at the rig-v2 true 2:1 view the old wheel (disc radius 0.74, teeth half-width 0.12, tip end 0.92)
# put a 3 px ink run on the plate outline in the frame whose tooth points straight down the screen (rim share 0.75 over
# ink_0). Disc radius 0.70, teeth half-width 0.09 and tip end 0.94 (silhouette top stays at canvas row 34 or lower:
# overlay_keepout) keep every frame at or above 0.917 over the plates (spike stand-in and real occupied plate) and 0.94
# over bare floor. The result is sensitive to these three numbers (radius 0.66 to 0.78 flips frames), and turning the
# wheel start does not help (the bad window is wider than the 7.5 degree frame step), so the values are measured.
TOOTH_HALF_W = 0.09
TIP_END = 0.94
DISC_R = 0.70
TOOTH_END = 0.80
EDGE = 0.075          # width of the light corner strip on square walls (about 1 art px on screen)

# Ramps: (top, left wall, right wall, trim). trim = the light tone used for skirts and corner strips.
STEEL = ("steel_3", "steel_2", "steel_1", "steel_2")
STEEL_D = ("steel_2", "steel_1", "steel_0", "steel_2")
COPPER = ("copper_2", "copper_1", "copper_0", "copper_2")
TEAL = ("teal_1", "teal_1", "teal_0", "teal_1")
TEAL_D = ("teal_1", "teal_0", "teal_0", "teal_1")
LAMP_ON = "lit_0"
LAMP_HOT = "lit_1"
LAMP_OFF = "dim_1"


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


def band(normal):
    d = sum(a * b for a, b in zip(normal, LIGHT_TOWARD))
    return 0 if d > 0.55 else (1 if d > 0.08 else 2)   # top, left, right


class Part:
    """Accumulates quads of one object; materials are assigned per face."""

    def __init__(self, name, col):
        self.name, self.col = name, col
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, cid):
        m = mat(cid)
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def quad(self, pts, cid):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.mi(cid)

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


def ngon(r, n, start=0.0, cx=0.0, cy=0.0):
    return [(cx + r * math.cos(start + 2 * math.pi * i / n), cy + r * math.sin(start + 2 * math.pi * i / n))
            for i in range(n)]


def rot(points, deg, cx=0.0, cy=0.0):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in points]


def move(points, dx, dy):
    return [(x + dx, y + dy) for x, y in points]


def solid(col, name, poly, z0, z1, ramp, skirt=0.07, edge=True, top=None, walls=None):
    """Prism with a ramp-tinted top and walls, a light trim skirt at the base and light corner strips.

    `ramp` = (top, left, right, trim) palette ids. `top` overrides the top tone, `walls` the left/right wall pair.
    Corner strips are applied to walls longer than 2.5 strips (so curved, many-sided parts stay unstriped).
    """
    p = Part(name, col)
    t_id, l_id, r_id, trim = ramp
    t_id = top or t_id
    if walls:
        l_id, r_id = walls
    tones = (t_id, l_id, r_id)
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    if area < 0:
        poly = list(reversed(poly))
    p.quad([(x, y, z1) for x, y in poly], t_id)
    h = z1 - z0
    sk = min(skirt, h * 0.5) if skirt > 0 else 0.0
    for i in range(n):
        j = (i + 1) % n
        (x0, y0), (x1, y1) = poly[i], poly[j]
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy)
        if ln < 1e-6:
            continue
        nrm = (dy / ln, -dx / ln, 0.0)
        wall = tones[band(nrm)]
        use_edge = edge and ln > 2.5 * EDGE

        def seg(a, b, za, zb, cid):
            ax, ay = x0 + dx * a, y0 + dy * a
            bx, by = x0 + dx * b, y0 + dy * b
            p.quad([(ax, ay, za), (bx, by, za), (bx, by, zb), (ax, ay, zb)], cid)

        e = EDGE / ln if use_edge else 0.0
        spans = [(0.0, e, trim), (e, 1.0 - e, wall), (1.0 - e, 1.0, trim)] if use_edge else [(0.0, 1.0, wall)]
        for a, b, cid in spans:
            if sk > 0:
                seg(a, b, z0, z0 + sk, trim)
                seg(a, b, z0 + sk, z1, cid)
            else:
                seg(a, b, z0, z1, cid)
    return p.finish()


def flat_box(col, name, poly, z0, z1, cid):
    """Part in exactly one palette tone on every face (lamps, glow, openings)."""
    p = Part(name, col)
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    if area < 0:
        poly = list(reversed(poly))
    p.quad([(x, y, z1) for x, y in poly], cid)
    for i in range(n):
        j = (i + 1) % n
        (x0, y0), (x1, y1) = poly[i], poly[j]
        p.quad([(x0, y0, z0), (x1, y1, z0), (x1, y1, z1), (x0, y0, z1)], cid)
    return p.finish()


def lamp_tone(f, on=4, hot=(0,)):
    """Frame 0 is lights-on and never has fewer lit pixels than another frame."""
    if f in hot:
        return LAMP_HOT
    return LAMP_ON if f < on else LAMP_OFF


# --- machines ---------------------------------------------------------------------------------------

def miner(col, f):
    """Low steel block with a copper hopper and a striped drill wedge pointing to the front-left (-y).
    The wedge bands scroll toward the tip (1/6 of the band period per frame)."""
    phase = (f / FRAMES) * 0.5
    solid(col, "body", rect(-0.72, 0.72, -0.18, 0.42), 0.0, 0.36, STEEL_D)
    solid(col, "body-top", rect(-0.6, 0.6, -0.06, 0.3), 0.36, 0.40, STEEL, skirt=0.0)
    solid(col, "hopper", rect(-0.38, 0.38, 0.08, 0.40), 0.40, 0.50, COPPER)
    flat_box(col, "hopper-in", rect(-0.26, 0.26, 0.15, 0.33), 0.5, 0.51, "copper_0")
    flat_box(col, "lamp", rect(0.46, 0.64, -0.02, 0.18), 0.40, 0.48, lamp_tone(f))

    def w(t):
        return 0.72 * (1.0 - t)

    def y(t):
        return -0.18 - 0.74 * t

    k = -3
    while True:
        a, b = k * 0.25 + phase, (k + 1) * 0.25 + phase
        if a >= 1.0:
            break
        a, b = max(a, 0.0), min(b, 1.0)
        if b - a > 1e-6:
            poly = [(-w(a), y(a)), (w(a), y(a)), (w(b), y(b)), (-w(b), y(b))]
            if b >= 1.0 - 1e-9:
                poly = [poly[0], poly[1], (0.0, y(1.0))]
            solid(col, "drill-%d" % k, poly, 0.0, 0.26, COPPER if k % 2 == 0 else STEEL, skirt=0.06, edge=False)
        k += 1


def assembler(col, f):
    """Teal disc with 8 steel teeth that carry copper tips, a face plate with eight spokes and a hub lamp.
    The wheel turns 7.5 degrees per frame: 6 frames close the loop on the 45 degree symmetry of teeth and spokes,
    and the small step keeps the mean luminance change between frames under the flash threshold."""
    ang = 22.5 + 7.5 * f
    solid(col, "disc", ngon(DISC_R, 24), 0.0, 0.26, TEAL_D, edge=False)
    for i in range(8):
        a = ang + 45.0 * i
        solid(col, "tooth-%d" % i, rot(rect(0.52, TOOTH_END, -TOOTH_HALF_W, TOOTH_HALF_W), a), 0.0, 0.26, STEEL, skirt=0.06, edge=True)
        solid(col, "tip-%d" % i, rot(rect(TOOTH_END, TIP_END, -TOOTH_HALF_W, TOOTH_HALF_W), a), 0.0, 0.26, COPPER, skirt=0.06, edge=True,
              walls=("copper_2", "copper_2"))
    solid(col, "face", ngon(0.58, 24), 0.26, 0.33, TEAL, skirt=0.0, edge=False)
    for i in range(8):
        flat_box(col, "spoke-%d" % i, rot(rect(0.2, 0.56, -0.04, 0.04), ang + 45.0 * i), 0.33, 0.345, "steel_2")
    flat_box(col, "hub", ngon(0.21, 16), 0.33, 0.46, LAMP_ON)
    flat_box(col, "hub-core", ngon(0.1, 12), 0.46, 0.47, LAMP_HOT if f < 2 else LAMP_ON)


def smelter(col, f):
    """Wide furnace block, a glowing mouth on the left wall (-y) and a tall chimney at the back left.
    The flame tongues in the mouth flicker; one tongue and the pilot lamp keep frame 0 the brightest."""
    solid(col, "furnace", rect(-0.74, 0.74, -0.26, 0.26), 0.0, 0.30, COPPER)
    solid(col, "furnace-top", rect(-0.64, 0.64, -0.16, 0.16), 0.30, 0.34, STEEL, skirt=0.0)
    # mouth: dark opening with a copper door frame, flame tongues in front of it
    solid(col, "frame", rect(-0.34, 0.34, -0.33, -0.25), 0.03, 0.25, STEEL, skirt=0.0, edge=False)
    flat_box(col, "mouth", rect(-0.26, 0.26, -0.335, -0.30), 0.07, 0.21, "dim_0")
    tongues = [(0.9, 0.6, 0.45, 0.72, 0.55, 0.38)[f], (0.9, 0.55, 0.66, 0.4, 0.62, 0.6)[f],
               (0.9, 0.48, 0.58, 0.58, 0.42, 0.5)[f]]
    for i, hgt in enumerate(tongues):
        x0 = -0.2 + 0.2 * i
        top = 0.07 + 0.14 * hgt
        cid = LAMP_HOT if (hgt > 0.7 and f == 0) else (LAMP_ON if hgt > 0.45 else "copper_2")
        flat_box(col, "tongue-%d" % i, rect(x0, x0 + 0.13, -0.36, -0.336), 0.07, top, cid)
    flat_box(col, "lamp", rect(0.42, 0.56, -0.14, -0.05), 0.34, 0.41, lamp_tone(f))
    # chimney
    solid(col, "chimney", rect(-0.58, -0.28, 0.0, 0.24), 0.34, 0.56, STEEL_D)
    solid(col, "chimney-cap", rect(-0.62, -0.24, -0.04, 0.28), 0.56, 0.62, COPPER, skirt=0.0)


def press(col, f):
    """Small steel base with an anvil, a narrow copper stem and a long head bar that strokes down and back up.
    The head height follows 0.34 + 0.045 cos(2 pi f / 6): frame 0 is the raised head with the lamp on."""
    hz = 0.34 + 0.04 * math.cos(2 * math.pi * f / FRAMES)
    solid(col, "base", rect(-0.5, 0.5, -0.3, 0.3), 0.0, 0.10, STEEL_D)
    solid(col, "anvil", rect(-0.24, 0.24, -0.16, 0.16), 0.10, 0.16, STEEL, skirt=0.0)
    solid(col, "stem", rect(-0.12, 0.12, -0.12, 0.12), 0.16, hz + 0.02, COPPER, skirt=0.0, edge=False)
    solid(col, "head", rect(-0.62, 0.62, -0.22, 0.22), hz, hz + 0.15, STEEL)
    flat_box(col, "lamp", rect(0.36, 0.54, -0.24, -0.21), hz + 0.04, hz + 0.12, lamp_tone(f))
    solid(col, "head-top", rect(-0.5, 0.5, -0.14, 0.14), hz + 0.15, hz + 0.18, COPPER, skirt=0.0)


MACHINES = {"miner": miner, "smelter": smelter, "press": press, "assembler": assembler}


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir> [machine ...]")
    out_dir = os.path.abspath(args[0])
    wanted = args[1:] or sorted(MACHINES)
    os.makedirs(out_dir, exist_ok=True)
    n = 0
    for mid in wanted:
        for f in range(FRAMES):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            _MATS.clear()
            col = bpy.context.scene.collection
            MACHINES[mid](col, f)
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "%s-run-%02d.blend" % (mid, f)), compress=True)
            n += 1
    print("BUILD-OK", n)


main()
