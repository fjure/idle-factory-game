"""Shared constants and the rig contract check for the F004 Blender route.

Pure bpy, no add-ons. Imported by build_rig.py, render.py and inspect_rig.py through
the script directory (sys.path insertion), so every consumer enforces the same contract.
"""
import os
import sys

RIG_VERSION = "rig-v2"
BLENDER_PIN = "5.2.1"
SEED = 0
SAMPLES = 16
RESOLUTION = 128          # px; ortho scale is RESOLUTION / D, so 1 world unit = D px
DENSITY_D = 24            # leading candidate (research R03); the spike evaluates 16/24/32
ORTHO_SCALE = RESOLUTION / DENSITY_D

# Projection candidates (research R02/S1, camera-scale contract). rot_x is the camera X rotation
# in degrees from straight-down. The orthographic vertical compression of a ground plane is cos(rot_x), so the
# Art Bible's 2:1 ground edges need rot_x = 60 (cos 60 = 0.5). rig-v1 used 63.4349488 (the atan(2) "true
# isometric-like" angle), which projects ground edges at sqrt(5) = 2.236:1; rig-v2 (T466) corrects that.
CAMERAS = {
    "oblique-3-4": {"name": "cam-oblique-3-4", "rot_x": 60.0, "rot_z": 0.0},
    "isometric-2-1": {"name": "cam-isometric-2-1", "rot_x": 60.0, "rot_z": 45.0},
}
# Key light direction in camera space: from the top left, travelling right/down/forward.
KEY_DIR_CAMERA = (0.5, -0.6, -0.6)


STAMP_FLAGS = (
    "use_stamp", "use_stamp_date", "use_stamp_time", "use_stamp_render_time", "use_stamp_frame",
    "use_stamp_frame_range", "use_stamp_memory", "use_stamp_hostname", "use_stamp_camera",
    "use_stamp_lens", "use_stamp_scene", "use_stamp_marker", "use_stamp_filename",
    "use_stamp_sequencer_strip", "use_stamp_note",
)


def key_name(projection):
    return "key-" + projection


def check_rig(scene, strict_cameras=True):
    """Return a list of contract violations (empty list = rig is valid)."""
    import bpy

    bad = []

    def expect(label, actual, wanted):
        if actual != wanted:
            bad.append("%s: %r != %r" % (label, actual, wanted))

    r = scene.render
    expect("engine", r.engine, "CYCLES")
    expect("cycles.device", scene.cycles.device, "CPU")
    expect("cycles.seed", scene.cycles.seed, SEED)
    expect("cycles.use_animated_seed", scene.cycles.use_animated_seed, False)
    expect("cycles.use_denoising", scene.cycles.use_denoising, False)
    expect("cycles.use_adaptive_sampling", scene.cycles.use_adaptive_sampling, False)
    expect("cycles.samples", scene.cycles.samples, SAMPLES)
    expect("cycles.filter_width", round(scene.cycles.filter_width, 4), 0.01)  # Cycles minimum; filter_size 0 has no Cycles equivalent
    expect("render.filter_size", round(r.filter_size, 4), 0.0)
    for flag in STAMP_FLAGS:
        if getattr(r, flag, False):
            bad.append("metadata stamp enabled: " + flag)
    expect("film_transparent", r.film_transparent, True)
    expect("view_transform", scene.view_settings.view_transform, "Standard")
    expect("look", scene.view_settings.look, "None")
    expect("exposure", scene.view_settings.exposure, 0.0)
    expect("gamma", scene.view_settings.gamma, 1.0)
    expect("display_device", scene.display_settings.display_device, "sRGB")
    expect("resolution", (r.resolution_x, r.resolution_y, r.resolution_percentage),
           (RESOLUTION, RESOLUTION, 100))
    expect("image format", (r.image_settings.file_format, r.image_settings.color_mode,
                            r.image_settings.color_depth), ("PNG", "RGBA", "8"))
    if strict_cameras:
        for proj, spec in CAMERAS.items():
            ob = scene.objects.get(spec["name"])
            if ob is None:
                bad.append("missing camera " + spec["name"])
                continue
            expect(spec["name"] + " type", ob.data.type, "ORTHO")
            expect(spec["name"] + " ortho_scale", round(ob.data.ortho_scale, 5), round(ORTHO_SCALE, 5))
            key = scene.objects.get(key_name(proj))
            if key is None or key.type != "LIGHT":
                bad.append("missing key light " + key_name(proj))
            elif key.data.type != "SUN":
                bad.append("key light is not a sun: " + key.name)
    for name in ("flat-base", "toon-base"):
        if name not in bpy.data.materials:
            bad.append("missing material " + name)
    return bad


def args_after_dashes():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def script_dir():
    return os.path.dirname(os.path.abspath(__file__))


def ground_edge_ratio(scene, projection="isometric-2-1"):
    """Screen width : height ratio of the ground edge along world +x for the given camera (2.0 = exact 2:1)."""
    from mathutils import Vector

    cam = scene.objects[CAMERAS[projection]["name"]]
    inv = cam.matrix_world.to_3x3().inverted()
    v = inv @ Vector((1.0, 0.0, 0.0))     # camera space: x right, y up on screen
    return abs(v.x) / abs(v.y)
