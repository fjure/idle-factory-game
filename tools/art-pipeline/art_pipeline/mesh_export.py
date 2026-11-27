"""`mesh-export` route of art pipeline v2 (Direction C; contracts/art-pipeline-v2.md, TA-8).

Headless Blender (`blender/export_mesh.py`) exports the subject to GLB with applied transforms and writes the sidecar. This module
then reads the GLB, runs the mesh-export checks (unit and scale, pivot, triangle count, material and variant counts, shadow-caster
flag), and writes the report, a manifest row and a provenance record with `route: mesh-export`. A failing check blocks both.
The silhouette IoU for C needs a Unity render step; `checks_v2.check_silhouette_iou_at_scale` is the library function for it and is
not part of this run.

CLI: python -m art_pipeline mesh-export --params asset.json --out dir [--blender exe]
"""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

from . import checks_v2 as c2
from .checks import CheckResult, build_report, write_report
from .provenance import file_sha256, v2_provenance_problems
from .render_hd import BLENDER_DIR, RIG_V3, blender_exe

EXPORT_SCRIPT = BLENDER_DIR / "export_mesh.py"


def run_blender_export(params_path: Path, out_dir: Path, exe: str) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)
    cmd = [exe, "-b", str(RIG_V3), "--factory-startup", "--python-exit-code", "1", "-P", str(EXPORT_SCRIPT), "--",
           "--params", str(params_path), "--out", str(out_dir)]
    proc = subprocess.run(cmd, capture_output=True, text=True, timeout=1800)
    if proc.returncode != 0 or "EXPORT-OK" not in proc.stdout:
        raise RuntimeError("Blender export failed (exit %s):\n%s\n%s" % (proc.returncode, proc.stdout[-2000:], proc.stderr[-2000:]))
    name = json.loads(Path(params_path).read_text(encoding="utf-8"))["assetId"]
    return json.loads((out_dir / (name + ".mesh-info.json")).read_text(encoding="utf-8"))


def mesh_manifest_row(params: dict, summary: dict, sidecar: dict, mesh_file: str, provenance_ref: str) -> dict:
    """Manifest row of a mesh asset (the mesh counterpart of MachineArtEntry): ids, look, stage, the GLB, its counts and flags."""
    return {
        "machineDefinitionId": params["machineDefinitionId"],
        "look": params["look"],
        "stage": params.get("stage", 0),
        "mesh": mesh_file,
        "triangles": summary["triangles"],
        "materials": summary["materials"],
        "variants": sidecar["variants"],
        "unitsPerWorldUnit": sidecar["unitsPerWorldUnit"],
        "shadowCaster": sidecar["shadowCaster"],
        "boundsMin": [round(v, 5) for v in summary["min"]],
        "boundsMax": [round(v, 5) for v in summary["max"]],
        "textureFormat": params.get("textureFormat", {}),
        "provenanceRef": provenance_ref,
    }


def build_mesh_provenance(params: dict, info: dict) -> dict:
    return {
        "look": params["look"],
        "pipeline": params.get("pipeline", "v2-prototype"),
        "route": "mesh-export",
        "renderPasses": [],
        "rigVersion": {"version": "rig-v3", "sha256": file_sha256(RIG_V3)},
        "textureFormat": params.get("textureFormat", {}),
        "generatedTextures": params.get("generatedTextures", []),
        "artDirectionBrief": params.get("artDirectionBrief", {"path": "none", "version": "none (prototype brief)"}),
        "artBibleVersion": params.get("artBibleVersion", "none (prototype brief)"),
        "tools": info.get("tools", {}),
        "parameters": info.get("parameters", {}),
        "outputs": info.get("outputs", []),
    }


def check_export(params: dict, glb: Path, sidecar: dict) -> list[CheckResult]:
    summary = c2.read_gltf_summary(glb)
    results = c2.check_mesh_export(summary, sidecar, params.get("limits"))
    for rec in params.get("generatedInputs", []):
        results.append(c2.check_prompt_lint(rec))
    return results


def run(params_path: Path, out_dir: Path, exe: str | None = None) -> int:
    params = json.loads(Path(params_path).read_text(encoding="utf-8"))
    asset_id = params["assetId"]
    info = run_blender_export(Path(params_path), out_dir, blender_exe(exe))
    glb = out_dir / (asset_id + ".glb")
    sidecar = json.loads((out_dir / (asset_id + ".mesh-sidecar.json")).read_text(encoding="utf-8"))
    summary = c2.read_gltf_summary(glb)
    prov_name = asset_id + ".provenance.json"
    prov = build_mesh_provenance(params, info)
    results = check_export(params, glb, sidecar)
    problems = v2_provenance_problems(prov)
    results.append(CheckResult("provenance_v2", not problems, {"problems": problems}))
    report = build_report({asset_id: results}, [])
    write_report(str(out_dir / (asset_id + ".check-report.json")), report)
    if report["passed"]:
        row = mesh_manifest_row(params, summary, sidecar, glb.name, prov_name)
        (out_dir / (asset_id + ".mesh-manifest.json")).write_text(json.dumps(row, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        (out_dir / prov_name).write_text(json.dumps(prov, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    sys.stdout.write("mesh-export %s: %s\n" % (asset_id, "all checks pass" if report["passed"] else "FAILING (manifest blocked)"))
    return 0 if report["passed"] else 1


def main(args) -> int:
    return run(Path(args.params), Path(args.out), args.blender)
