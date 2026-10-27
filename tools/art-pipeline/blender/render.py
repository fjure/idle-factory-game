"""Deterministic batch render for the F004 Blender route (T403, research R07/R09).

  "$BLENDER_EXE" -b <rig> --factory-startup --python-exit-code 1 -P render.py -- <asset> [options]

<asset>   "cube" (built-in smoke subject) or a path to .blend / .glb / .gltf / .obj / .stl.
          A .blend asset is appended (all its objects); other formats are imported.
Options   --projection oblique-3-4|isometric-2-1   (default oblique-3-4)
          --material flat|toon                      (applied to objects without a material; default toon)
          --out <dir>                               (default ./render-out)
          --name <stem>                             (default: asset stem)

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
import rig_common as rc  # noqa: E402


def parse(argv):
    opts = {"projection": "oblique-3-4", "material": "toon", "out": "render-out", "name": None}
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


def main():
    asset, opts = parse(rc.args_after_dashes())
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
