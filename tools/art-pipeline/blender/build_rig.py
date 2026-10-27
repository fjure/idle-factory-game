"""Regenerate rig-v1.blend (F004 T403). Not hand-crafted: this script is the source of truth.

  "$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_rig.py -- [output.blend]

The output defaults to rig-v1.blend next to this script.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig_common as rc  # noqa: E402

args = rc.args_after_dashes()
out = os.path.abspath(args[0]) if args else os.path.join(rc.script_dir(), rc.RIG_VERSION + ".blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = rc.RIG_VERSION

# Render settings: Cycles on CPU, fixed seed, no denoiser, no filtering, transparent film.
r = scene.render
r.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = rc.SAMPLES
scene.cycles.use_adaptive_sampling = False
scene.cycles.use_denoising = False
scene.cycles.seed = rc.SEED
scene.cycles.use_animated_seed = False
scene.cycles.filter_width = 0.0   # Cycles clamps to its 0.01 minimum (effectively unfiltered)
r.filter_size = 0.0
r.film_transparent = True
r.resolution_x = r.resolution_y = rc.RESOLUTION
r.resolution_percentage = 100
r.image_settings.file_format = "PNG"
r.image_settings.color_mode = "RGBA"
r.image_settings.color_depth = "8"
for flag in rc.STAMP_FLAGS:   # no Blender metadata (path, date, render time) in the PNG
    setattr(r, flag, False)
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.view_settings.exposure = 0.0
scene.view_settings.gamma = 1.0
scene.display_settings.display_device = "sRGB"

# Dim neutral world: a faint fill so the shadow side keeps a dark tone instead of pure black.
world = bpy.data.worlds.new("world-black")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.15
scene.world = world


def new_material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes.clear()
    m.use_fake_user = True
    return m


# flat-base: unlit emission. toon-base: Toon BSDF with a hard terminator.
flat = new_material("flat-base")
em = flat.node_tree.nodes.new("ShaderNodeEmission")
em.inputs["Color"].default_value = (0.5, 0.5, 0.5, 1.0)
o = flat.node_tree.nodes.new("ShaderNodeOutputMaterial")
flat.node_tree.links.new(em.outputs["Emission"], o.inputs["Surface"])

toon = new_material("toon-base")
tb = toon.node_tree.nodes.new("ShaderNodeBsdfToon")
tb.component = "DIFFUSE"
tb.inputs["Color"].default_value = (0.8, 0.8, 0.8, 1.0)
tb.inputs["Size"].default_value = 0.8
tb.inputs["Smooth"].default_value = 0.0
o = toon.node_tree.nodes.new("ShaderNodeOutputMaterial")
toon.node_tree.links.new(tb.outputs["BSDF"], o.inputs["Surface"])

# One orthographic camera per projection candidate, each with its own top-left key light
# parented to it (so the light stays constant in screen space).
rig = bpy.data.collections.new("rig")
scene.collection.children.link(rig)
for proj, spec in rc.CAMERAS.items():
    cam_data = bpy.data.cameras.new(spec["name"])
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = rc.ORTHO_SCALE
    cam_data.clip_start = 0.1
    cam_data.clip_end = 100.0
    cam = bpy.data.objects.new(spec["name"], cam_data)
    cam.rotation_euler = (math.radians(spec["rot_x"]), 0.0, math.radians(spec["rot_z"]))
    fwd = cam.rotation_euler.to_matrix() @ Vector((0.0, 0.0, -1.0))
    cam.location = -fwd * 20.0   # looks at the origin
    rig.objects.link(cam)

    light_data = bpy.data.lights.new(rc.key_name(proj), "SUN")
    light_data.energy = 2.0
    light_data.angle = 0.0       # hard shadow edge
    light = bpy.data.objects.new(rc.key_name(proj), light_data)
    rig.objects.link(light)
    light.parent = cam
    d = Vector(rc.KEY_DIR_CAMERA).normalized()
    light.rotation_euler = Vector((0.0, 0.0, -1.0)).rotation_difference(d).to_euler()

scene.camera = bpy.data.objects[rc.CAMERAS["oblique-3-4"]["name"]]
bpy.context.view_layer.update()

bad = rc.check_rig(scene)
if bad:
    print("RIG CONTRACT VIOLATIONS:", *bad, sep="\n  ")
    sys.exit(1)

bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
print("saved", out)
