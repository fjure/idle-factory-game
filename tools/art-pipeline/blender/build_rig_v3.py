"""Regenerate rig-v3.blend (F007 T613): rig-v2 with the same cameras and a three-light dusk set. Not hand-crafted.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_rig_v3.py -- [output.blend]

Starts from rig-v2.blend next to this script, removes the per-camera key lights, and adds key, fill and rim suns per camera,
parented to the camera so the lighting stays constant in screen space. The world becomes a dim cool dusk ambient.
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig_common as rc  # noqa: E402

args = rc.args_after_dashes()
out = os.path.abspath(args[0]) if args else os.path.join(rc.script_dir(), rc.RIG_V3_VERSION + ".blend")
bpy.ops.wm.open_mainfile(filepath=os.path.join(rc.script_dir(), "rig-v2.blend"))
scene = bpy.context.scene
scene.name = rc.RIG_V3_VERSION
rig = bpy.data.collections["rig"]

for proj in rc.CAMERAS:
    key = bpy.data.objects.get(rc.key_name(proj))
    if key is not None:
        data = key.data
        bpy.data.objects.remove(key, do_unlink=True)
        bpy.data.lights.remove(data)
    cam = bpy.data.objects[rc.CAMERAS[proj]["name"]]
    for role, spec in rc.DUSK.items():
        name = rc.dusk_name(role, proj)
        data = bpy.data.lights.new(name, "SUN")
        data.color = spec["color"]
        data.energy = spec["energy"]
        data.angle = spec["angle"]
        light = bpy.data.objects.new(name, data)
        rig.objects.link(light)
        light.parent = cam
        d = Vector(spec["dir"]).normalized()
        light.rotation_euler = Vector((0.0, 0.0, -1.0)).rotation_difference(d).to_euler()

bg = scene.world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = rc.DUSK_WORLD["color"]
bg.inputs["Strength"].default_value = rc.DUSK_WORLD["strength"]
scene.world.name = "world-dusk"
bpy.context.view_layer.update()

bad = rc.check_rig_v3(scene)
if bad:
    print("RIG-V3 CONTRACT VIOLATIONS:", *bad, sep="\n  ")
    sys.exit(1)
bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
print("saved", out)
