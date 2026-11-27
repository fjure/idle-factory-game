"""Deterministic batch render for the F004 Blender route (T403, research R07/R09).

  "$BLENDER_EXE" -b <rig> --factory-startup --python-exit-code 1 -P render.py -- <asset> [options]

<asset>   "cube" (built-in smoke subject) or a path to .blend / .glb / .gltf / .obj / .stl.
          A .blend asset is appended (all its objects); other formats are imported.
Options   --projection oblique-3-4|isometric-2-1   (default oblique-3-4)
          --material flat|toon                      (applied to objects without a material; default toon)
          --out <dir>                               (default ./render-out)
          --name <stem>                             (default: asset stem)

HD route (art pipeline v2, rig-v3): `-- --params <asset.json> --out <dir> [--threads N]` renders the passes named in the parameter
file (beauty, shadow, emissiveMask, optional normal) to <out>/<assetId>.<pass>.png plus <assetId>.render-info.json. Asset
"reference-machine" is the procedural reference subject of the reproducibility test (see `render_hd_main`).

Writes <out>/<name>.png and <out>/<name>.provenance.json, a provenance stub with the fields of
data-model "Asset provenance record" that this step knows: tools, parameters, rigVersion,
renderEnv (GPU, driver), render time and output hash. The render time is informational and is
excluded from the reproducibility comparison. Only committed scripts run here; no add-ons.
"""
import hashlib
import json
import os
import subprocess
import sys
import time

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import reference_asset as ra  # noqa: E402
import rig_common as rc  # noqa: E402


def parse(argv):
    opts = {"projection": "oblique-3-4", "material": "toon", "out": "render-out", "name": None, "params": None, "threads": None}
    pos = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a.startswith("--"):
            key = a[2:]
            if key not in opts or i + 1 >= len(argv):
                raise SystemExit("render.py: unknown or incomplete option " + a)
            opts[key] = argv[i + 1]
            i += 2
        else:
            pos.append(a)
            i += 1
    if opts["params"]:
        return None, opts
    if len(pos) != 1:
        raise SystemExit("render.py: expected exactly one <asset>")
    if opts["projection"] not in rc.CAMERAS:
        raise SystemExit("render.py: projection must be one of " + ", ".join(rc.CAMERAS))
    if opts["material"] not in ("flat", "toon"):
        raise SystemExit("render.py: material must be flat or toon")
    return pos[0], opts


def gpu_env():
    """GPU and driver for provenance (R09). Headless Blender cannot query the GPU, so use nvidia-smi."""
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=name,driver_version", "--format=csv,noheader"],
            capture_output=True, text=True, timeout=15, check=True).stdout.strip().splitlines()[0]
        name, driver = [p.strip() for p in out.split(",", 1)]
        return {"gpu": name, "driver": driver}
    except Exception as exc:  # noqa: BLE001 - provenance must not break a render
        return {"gpu": "unavailable", "driver": "unavailable", "note": type(exc).__name__}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        h.update(f.read())
    return h.hexdigest()


def load_asset(asset, scene):
    """Return the list of mesh objects of the subject, linked into the scene."""
    before = set(scene.objects)
    if asset == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.5))
        bpy.context.object.name = "subject-cube"
    else:
        path = os.path.abspath(asset)
        ext = os.path.splitext(path)[1].lower()
        if ext == ".blend":
            with bpy.data.libraries.load(path, link=False) as (src, dst):
                dst.objects = list(src.objects)
            for ob in dst.objects:
                if ob is not None and ob.type != "CAMERA" and ob.type != "LIGHT":
                    scene.collection.objects.link(ob)
        elif ext in (".glb", ".gltf"):
            bpy.ops.import_scene.gltf(filepath=path)
        elif ext == ".obj":
            bpy.ops.wm.obj_import(filepath=path)
        elif ext == ".stl":
            bpy.ops.wm.stl_import(filepath=path)
        else:
            raise SystemExit("render.py: unsupported asset type " + ext)
    return [o for o in scene.objects if o not in before and o.type == "MESH"]


def _holdout(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes.clear()
    h = m.node_tree.nodes.new("ShaderNodeHoldout")
    out = m.node_tree.nodes.new("ShaderNodeOutputMaterial")
    m.node_tree.links.new(h.outputs["Holdout"], out.inputs["Surface"])
    return m


def _normal_mat():
    m = bpy.data.materials.new("pass-normal")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    mul = nt.nodes.new("ShaderNodeVectorMath")
    mul.operation = "MULTIPLY_ADD"
    mul.inputs[1].default_value = (0.5, 0.5, 0.5)
    mul.inputs[2].default_value = (0.5, 0.5, 0.5)
    em = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(geo.outputs["Normal"], mul.inputs[0])
    nt.links.new(mul.outputs["Vector"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return m


def _swap_materials(subject, picker):
    saved = []
    for ob in subject:
        saved.append((ob, list(ob.data.materials)))
        for i, m in enumerate(list(ob.data.materials)):
            ob.data.materials[i] = picker(m)
    return saved


def _restore_materials(saved):
    for ob, mats in saved:
        for i, m in enumerate(mats):
            ob.data.materials[i] = m


def _oidn_info():
    v = getattr(bpy.app, "oidn", None)
    if v:
        return ".".join(str(x) for x in v)
    return "bundled with Blender %s (version not exposed); denoiser is off" % bpy.app.version_string


def render_hd_main(opts):
    params = json.load(open(opts["params"], encoding="utf-8"))
    scene = bpy.context.scene
    rig_path = bpy.data.filepath
    v3 = rc.rig_is_v3(rig_path)
    bad = rc.check_rig_v3(scene) if v3 else rc.check_rig(scene)
    if bad:
        raise SystemExit("render.py: rig violates the contract:\n  " + "\n  ".join(bad))
    proj = params.get("projection", "isometric-2-1")
    if proj not in rc.CAMERAS:
        raise SystemExit("render.py: unknown projection " + proj)
    passes = params.get("passes", ["beauty", "shadow", "emissiveMask"])
    resolution = int(params.get("resolution", 256))
    ppu = float(params.get("ppu", 128))
    samples = int(params.get("samples", rc.SAMPLES))
    asset_id = params["assetId"]

    # Camera and lights of the selected projection only.
    cam = scene.objects[rc.CAMERAS[proj]["name"]]
    scene.camera = cam
    cam.data.ortho_scale = resolution / ppu
    lights = {}
    for p in rc.CAMERAS:
        for role in rc.DUSK:
            ob = scene.objects[rc.dusk_name(role, p)] if v3 else None
            if ob is not None:
                ob.hide_render = p != proj
                if p == proj:
                    lights[role] = ob
    scene.render.resolution_x = scene.render.resolution_y = resolution
    scene.cycles.samples = samples
    if opts["threads"]:
        scene.render.threads_mode = "FIXED"
        scene.render.threads = int(opts["threads"])

    asset = params.get("asset", "reference-machine")
    subject = ra.build_reference_machine(scene) if asset == "reference-machine" else load_asset(asset, scene)
    if not subject:
        raise SystemExit("render.py: asset produced no mesh objects")
    ground = None
    if "shadow" in passes:
        bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0.0, 0.0, 0.0))
        ground = bpy.context.object
        ground.name = "shadow-catcher"
        ground.is_shadow_catcher = True
        ground.hide_render = True
    holdout, white, normal_mat = _holdout("pass-holdout"), ra._emit("pass-mask", (1.0, 1.0, 1.0, 1.0), 1.0), _normal_mat()
    bg = scene.world.node_tree.nodes["Background"]
    world_strength = bg.inputs["Strength"].default_value
    out_dir = os.path.abspath(opts["out"])
    os.makedirs(out_dir, exist_ok=True)
    t0 = time.perf_counter()
    outputs = []
    for name in passes:
        saved = None
        scene.view_settings.view_transform = "Standard"
        if name == "beauty":
            # AD-9: AgX is only a recorded one-asset comparison parameter; data passes always use Standard.
            scene.view_settings.view_transform = params.get("viewTransform", "Standard")
        elif name == "shadow":
            for role, ob in lights.items():
                ob.hide_render = role != "key"
            bg.inputs["Strength"].default_value = 0.0
            ground.hide_render = False
            for ob in subject:
                ob.is_holdout = True
        elif name == "emissiveMask":
            saved = _swap_materials(subject, lambda m: white if m.name.startswith("emit") else holdout)
        elif name == "normal":
            saved = _swap_materials(subject, lambda m: normal_mat)
        else:
            raise SystemExit("render.py: unknown pass " + name)
        png = os.path.join(out_dir, "%s.%s.png" % (asset_id, name))
        scene.render.filepath = png
        bpy.ops.render.render(write_still=True)
        outputs.append({"pass": name, "path": os.path.basename(png), "sha256": sha256(png)})
        if saved:
            _restore_materials(saved)
        if name == "shadow":
            for ob in lights.values():
                ob.hide_render = False
            bg.inputs["Strength"].default_value = world_strength
            ground.hide_render = True
            for ob in subject:
                ob.is_holdout = False
    seconds = round(time.perf_counter() - t0, 3)
    info = {
        "assetId": asset_id,
        "tools": {"blender": {"version": bpy.app.version_string, "hash": bpy.app.build_hash.decode(), "addons": []},
                  "oidn": _oidn_info()},
        "parameters": {
            "rigVersion": rc.RIG_V3_VERSION if v3 else rc.RIG_VERSION, "rigFile": os.path.basename(rig_path),
            "rigSha256": sha256(rig_path), "renderScript": "tools/art-pipeline/blender/render.py",
            "renderScriptSha256": sha256(os.path.abspath(__file__)), "projection": proj, "engine": "CYCLES/CPU",
            "seed": rc.SEED, "samples": samples, "denoiser": False, "resolution": resolution, "ppu": ppu,
            "passes": passes, "threads": opts["threads"] or "auto",
            "viewTransform": params.get("viewTransform", "Standard"),
        },
        "renderEnv": gpu_env(), "renderSeconds": seconds, "outputs": outputs,
    }
    with open(os.path.join(out_dir, asset_id + ".render-info.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=2, sort_keys=True)
        f.write("\n")
    print("RENDER-OK", out_dir, "%.3fs" % seconds)


def main():
    asset, opts = parse(rc.args_after_dashes())
    if opts["params"]:
        render_hd_main(opts)
        return
    scene = bpy.context.scene
    bad = rc.check_rig(scene)
    if bad:
        raise SystemExit("render.py: rig violates the contract:\n  " + "\n  ".join(bad))

    # Select the projection: its camera becomes active and only its key light renders.
    for proj, spec in rc.CAMERAS.items():
        scene.objects[rc.key_name(proj)].hide_render = proj != opts["projection"]
    scene.camera = scene.objects[rc.CAMERAS[opts["projection"]]["name"]]

    subject = load_asset(asset, scene)
    if not subject:
        raise SystemExit("render.py: asset produced no mesh objects")
    mat = bpy.data.materials[opts["material"] + "-base"]
    for ob in subject:
        if not ob.data.materials:
            ob.data.materials.append(mat)

    name = opts["name"] or (os.path.splitext(os.path.basename(asset))[0] if asset != "cube" else "cube")
    out_dir = os.path.abspath(opts["out"])
    os.makedirs(out_dir, exist_ok=True)
    png = os.path.join(out_dir, name + ".png")
    scene.render.filepath = png

    t0 = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    seconds = round(time.perf_counter() - t0, 3)

    rig_path = bpy.data.filepath
    stub = {
        "assetId": name,
        "revision": None,
        "route": "blender",
        "tools": {"blender": {"version": bpy.app.version_string, "hash": bpy.app.build_hash.decode(),
                              "addons": []}},
        "parameters": {
            "rigVersion": rc.RIG_VERSION,
            "rigFile": os.path.basename(rig_path),
            "rigSha256": sha256(rig_path) if rig_path else None,
            "renderScript": "tools/art-pipeline/blender/render.py",
            "renderScriptSha256": sha256(os.path.abspath(__file__)),
            "projection": opts["projection"],
            "material": opts["material"],
            "engine": "CYCLES/CPU",
            "seed": rc.SEED,
            "samples": rc.SAMPLES,
            "denoiser": False,
            "resolution": rc.RESOLUTION,
            "densityD": rc.DENSITY_D,
        },
        "renderEnv": gpu_env(),
        "renderSeconds": seconds,
        "outputs": [{"path": os.path.basename(png), "sha256": sha256(png)}],
        "todo": "Fill sources, license, touchUp, pixelPass, reviewedBy, artBibleVersion, briefVersion.",
    }
    with open(os.path.join(out_dir, name + ".provenance.json"), "w", encoding="utf-8") as f:
        json.dump(stub, f, indent=2, sort_keys=True)
        f.write("\n")
    print("RENDER-OK", png, "%.3fs" % seconds)


main()
