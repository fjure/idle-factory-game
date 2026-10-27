"""Read the saved rig back and print its settings as JSON; exit 1 on a contract violation.

  "$BLENDER_EXE" -b <rig> --factory-startup --python-exit-code 1 -P inspect_rig.py
"""
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig_common as rc  # noqa: E402

s = bpy.context.scene
info = {
    "blender": bpy.app.version_string,
    "engine": s.render.engine,
    "cycles": {"device": s.cycles.device, "seed": s.cycles.seed, "animated_seed": s.cycles.use_animated_seed,
               "samples": s.cycles.samples, "denoising": s.cycles.use_denoising,
               "adaptive": s.cycles.use_adaptive_sampling, "filter_width": s.cycles.filter_width},
    "filter_size": s.render.filter_size,
    "film_transparent": s.render.film_transparent,
    "view_transform": s.view_settings.view_transform,
    "look": s.view_settings.look,
    "display_device": s.display_settings.display_device,
    "resolution": [s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage],
    "image": [s.render.image_settings.file_format, s.render.image_settings.color_mode,
              s.render.image_settings.color_depth],
    "cameras": {o.name: {"type": o.data.type, "ortho_scale": round(o.data.ortho_scale, 5),
                         "rotation_deg": [round(math.degrees(v), 4) for v in o.rotation_euler]}
                for o in s.objects if o.type == "CAMERA"},
    "lights": {o.name: {"type": o.data.type, "energy": o.data.energy,
                        "parent": o.parent.name if o.parent else None}
               for o in s.objects if o.type == "LIGHT"},
    "materials": sorted(m.name for m in bpy.data.materials),
}
info["violations"] = rc.check_rig(s)
print("RIG-JSON " + json.dumps(info, sort_keys=True))
sys.exit(1 if info["violations"] else 0)
