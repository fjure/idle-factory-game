"""Headless mesh export for the `mesh-export` route (Direction C, art pipeline v2, TA-8).

  "$BLENDER_EXE" -b <rig-v3.blend> --factory-startup --python-exit-code 1 -P export_mesh.py -- --params <asset.json> --out <dir>

Exports the subject (the procedural "reference-machine", or a .blend path) to <out>/<assetId>.glb with all transforms applied,
so the glTF holds unit-scale nodes, the footprint centred on the origin and the ground at y = 0 (glTF is Y-up; Blender's
exporter converts Z-up). Also writes <assetId>.mesh-sidecar.json (unit, variants, shadowCaster, ...) and
<assetId>.mesh-info.json (tool versions, rig hash, output hash). The checks run afterwards in `art_pipeline.mesh_export`.
Only committed scripts run here; no add-ons.
"""
import hashlib
import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import reference_asset as ra  # noqa: E402
import rig_common as rc  # noqa: E402


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def parse(argv):
    opts = {"params": None, "out": "mesh-out"}
    i = 0
    while i < len(argv):
        key = argv[i][2:] if argv[i].startswith("--") else None
        if key not in opts or i + 1 >= len(argv):
            raise SystemExit("export_mesh.py: unknown or incomplete option " + argv[i])
        opts[key] = argv[i + 1]
        i += 2
    if not opts["params"]:
        raise SystemExit("export_mesh.py: --params is required")
    return opts


def main():
    opts = parse(rc.args_after_dashes())
    params = json.load(open(opts["params"], encoding="utf-8"))
    scene = bpy.context.scene
    asset_id = params["assetId"]
    asset = params.get("asset", "reference-machine")
    if asset != "reference-machine":
        raise SystemExit("export_mesh.py: only the procedural reference-machine is wired; .blend subjects arrive with the first mesh asset")
    subject = ra.build_reference_machine(scene)

    # Apply location, rotation and scale so every node is the identity (the checks read the accessor bounds as world bounds), then select exactly the subject.
    bpy.ops.object.select_all(action="DESELECT")
    for ob in subject:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = subject[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    out_dir = os.path.abspath(opts["out"])
    os.makedirs(out_dir, exist_ok=True)
    glb = os.path.join(out_dir, asset_id + ".glb")
    bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
                              export_cameras=False, export_lights=False, export_extras=False)

    # Unit: one Blender unit is one world unit when the scene unit scale is 1.
    units = round(scene.unit_settings.scale_length, 6)
    sidecar = {
        "assetId": asset_id,
        "unitsPerWorldUnit": units,
        "variants": int(params.get("variants", 1)),
        "shadowCaster": bool(params.get("shadowCaster", True)),
        "lods": int(params.get("lods", 1)),
        "format": "glb",
    }
    with open(os.path.join(out_dir, asset_id + ".mesh-sidecar.json"), "w", encoding="utf-8") as f:
        json.dump(sidecar, f, indent=2, sort_keys=True)
        f.write("\n")
    rig_path = bpy.data.filepath
    info = {
        "assetId": asset_id,
        "tools": {"blender": {"version": bpy.app.version_string, "hash": bpy.app.build_hash.decode(), "addons": []}},
        "parameters": {"rigVersion": rc.RIG_V3_VERSION, "rigFile": os.path.basename(rig_path), "rigSha256": sha256(rig_path),
                       "exportScript": "tools/art-pipeline/blender/export_mesh.py",
                       "exportScriptSha256": sha256(os.path.abspath(__file__)), "yUp": True, "transformsApplied": True},
        "outputs": [{"path": os.path.basename(glb), "sha256": sha256(glb)}],
    }
    with open(os.path.join(out_dir, asset_id + ".mesh-info.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=2, sort_keys=True)
        f.write("\n")
    print("EXPORT-OK", glb)


main()
