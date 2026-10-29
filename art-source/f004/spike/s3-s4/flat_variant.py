"""F004 spike S3/S4 (T421), lighting variant B: unlit base model for Blender 5.2.1 (no add-ons).

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P flat_variant.py -- <in.blend> <out.blend>

Variant A is the S2 model as built (toon key light plus emissive frames, baked into the render). Variant B removes the
baked key-light term: every non-emissive palette material becomes a plain emission of its palette colour, so the render
carries no light direction and the shading comes from 2D-light accent sprites at runtime. Geometry, rig and palette are
unchanged; this script only rewires material outputs, so it is a pure function of its input.
"""
import sys

import bpy

argv = sys.argv
src, dst = argv[argv.index("--") + 1], argv[argv.index("--") + 2]
bpy.ops.wm.open_mainfile(filepath=src)
changed = 0
for m in bpy.data.materials:
    if not m.use_nodes:
        continue
    nt = m.node_tree
    toon = [n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfToon"]
    if not toon:
        continue
    out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputMaterial")
    emi = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeEmission")
    emi.inputs["Strength"].default_value = 1.0
    for link in list(out.inputs["Surface"].links):
        nt.links.remove(link)
    nt.links.new(emi.outputs["Emission"], out.inputs["Surface"])
    changed += 1
bpy.ops.wm.save_as_mainfile(filepath=dst, compress=True)
print("FLAT-OK", changed)
