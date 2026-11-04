"""F004 T435: procedural model of the floor tile source (`F004-FLOOR-TILE`), Blender 5.2.1.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_models.py -- <out-dir>

Writes `<out-dir>/floor-tile.blend`: a flat ground patch in `floor_0` that is much larger than one tile (so every
pixel the tile mask keeps is rendered, never anti-aliased against transparency) with the detail clusters in
`floor_1` (Art Bible 5, 6.2; brief section 6): at most two clusters per tile, each at least 2 art px from the tile
edge, no `floor_2` (that tone is forbidden behind badges, rule C1, so the floor does not use it at all), no
`plate_*` tone, no baked shadow or gradient (lighting B, flat emission materials as the T434 machines and the plates).

The tile itself is cut by `pipeline_floor.py` with the exact 2:1 diamond mask of the lattice period; this script only
supplies the tones. Units: 1 world unit = 24 art px; the rig-v2 camera maps world (x, y) to the screen as
sx = 16.97 (x + y) px right and sy = 8.49 (y - x) px up from the image centre (the tile centre).
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

PX_X = 24.0 * math.sqrt(0.5)                       # screen px per world unit along (1, 1)/sqrt(2) * ... see below
SX = 24.0 * math.sqrt(0.5)                         # 16.97: screen x px per unit of (x + y)
SY = SX * 0.5                                      # 8.49: screen y px per unit of (y - x); exact 2:1 at rig-v2 (cos 60 = 0.5)
EPS = 0.002


def lin(c8):
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mat(cid):
    h = HEX[cid].lstrip("#")
    m = bpy.data.materials.new("pal-" + cid)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    n = nt.nodes.new("ShaderNodeEmission")
    n.inputs["Color"].default_value = (lin(int(h[0:2], 16)), lin(int(h[2:4], 16)), lin(int(h[4:6], 16)), 1.0)
    nt.links.new(n.outputs["Emission"], out.inputs["Surface"])
    return m


def flat(col, name, poly, z, cid):
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x, y, z)) for x, y in poly])
    f.material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(cid))
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)


def from_screen(sx_px, sy_px_up):
    """World (x, y) whose ground point projects to the given offset from the tile centre."""
    a, b = sx_px / SX, sy_px_up / SY
    return ((a - b) / 2.0, (a + b) / 2.0)


def rect(cx, cy, hx, hy):
    return [(cx - hx, cy - hy), (cx + hx, cy - hy), (cx + hx, cy + hy), (cx - hx, cy + hy)]


# Detail clusters: (screen offset from the tile centre in px, half sizes in world units). Cluster A is a scuffed panel,
# cluster B a pair of rivet dots; everything is far inside the 60 x 30 tile.
CLUSTER_A = ((-9.0, 1.5), (0.24, 0.09))
CLUSTER_B = [((11.0, 3.5), 0.10)]


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        raise SystemExit("build_models.py: expected <out-dir>")
    out_dir = os.path.abspath(args[0])
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.context.scene.collection
    flat(col, "field", rect(0.0, 0.0, 4.0, 4.0), 0.0, "floor_0")
    (sx, sy), (hx, hy) = CLUSTER_A
    cx, cy = from_screen(sx, sy)
    flat(col, "cluster-a", rect(cx, cy, hx, hy), EPS, "floor_1")
    for i, ((sx, sy), h) in enumerate(CLUSTER_B):
        cx, cy = from_screen(sx, sy)
        flat(col, "cluster-b-%d" % i, rect(cx, cy, h, h), EPS, "floor_1")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "floor-tile.blend"), compress=True)
    print("BUILD-OK 1")


main()
