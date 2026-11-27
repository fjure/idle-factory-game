"""mesh-export route (Direction C, TA-8): known answers on the committed reference GLB, manifest and provenance rows, and a
Blender-gated end-to-end export."""
import json
from pathlib import Path

import pytest

from art_pipeline import checks_v2 as c2
from art_pipeline import mesh_export as me
from art_pipeline import render_hd as rh
from art_pipeline.provenance import v2_provenance_problems

ROOT = Path(__file__).resolve().parent.parent
GOLDEN = ROOT / "f007" / "reference"
PARAMS = ROOT / "f007" / "reference-mesh.params.json"


def golden_paths():
    return GOLDEN / "reference-machine-mesh.glb", GOLDEN / "reference-machine-mesh.mesh-sidecar.json"


def test_reference_glb_known_answers():
    glb, side = golden_paths()
    s = c2.read_gltf_summary(glb)
    assert s["triangles"] == 116 and s["materials"] == 3
    assert [round(v, 3) for v in s["min"]] == [-0.8, 0.0, -0.6]
    assert [round(v, 3) for v in s["max"]] == [0.8, 1.6, 0.63]      # glTF Y-up: height 1.6 on y, the vent protrudes 0.03 on z
    assert all(sc == [1, 1, 1] for sc in s["nodeScales"])
    sidecar = json.loads(side.read_text(encoding="utf-8"))
    assert sidecar["unitsPerWorldUnit"] == 1.0 and sidecar["shadowCaster"] is True and sidecar["variants"] == 1
    results = c2.check_mesh_export(s, sidecar)
    assert all(r.passed for r in results), [(r.name, r.details) for r in results if not r.passed]


def test_reference_glb_fails_when_the_sidecar_lies_or_limits_tighten():
    glb, side = golden_paths()
    s = c2.read_gltf_summary(glb)
    sidecar = json.loads(side.read_text(encoding="utf-8"))
    by = {r.name: r for r in c2.check_mesh_export(s, {**sidecar, "shadowCaster": "yes"})}
    assert not by["mesh_shadow_caster"].passed
    by = {r.name: r for r in c2.check_mesh_export(s, sidecar, {"maxTriangles": 100})}
    assert not by["mesh_triangles"].passed
    by = {r.name: r for r in c2.check_mesh_export(s, sidecar, {"pivotTolerance": 0.001})}
    assert not by["mesh_pivot"].passed          # the vent shifts the footprint centre by 0.015


def test_manifest_row_and_provenance_for_mesh_export():
    glb, side = golden_paths()
    params = json.loads(PARAMS.read_text(encoding="utf-8"))
    summary = c2.read_gltf_summary(glb)
    sidecar = json.loads(side.read_text(encoding="utf-8"))
    row = me.mesh_manifest_row(params, summary, sidecar, glb.name, "p.json")
    assert row["triangles"] == 116 and row["materials"] == 3 and row["look"] == "F007-C" and row["shadowCaster"] is True
    assert row["boundsMax"] == [0.8, 1.6, 0.63] and row["provenanceRef"] == "p.json"
    prov = me.build_mesh_provenance(params, {"tools": {}, "parameters": {}, "outputs": []})
    assert prov["route"] == "mesh-export" and prov["rigVersion"]["version"] == "rig-v3"
    assert v2_provenance_problems(prov) == []
    assert v2_provenance_problems({**prov, "route": "pixel"})


def test_check_export_runs_the_prompt_lint_for_generated_inputs():
    glb, side = golden_paths()
    sidecar = json.loads(side.read_text(encoding="utf-8"))
    good = {"generatedInputs": [{"prompt": "riveted boiler", "seed": 1}]}
    assert all(r.passed for r in me.check_export(good, glb, sidecar))
    bad = {"generatedInputs": [{"prompt": "factorio boiler", "seed": 1}]}
    assert not all(r.passed for r in me.check_export(bad, glb, sidecar))


@pytest.mark.skipif(not Path(rh.blender_exe()).exists(), reason="Blender 5.2.1 not installed; the known-answer tests above still run")
def test_end_to_end_export_matches_the_golden_glb_and_writes_the_manifest(tmp_path):
    assert me.run(PARAMS, tmp_path / "out") == 0
    out = tmp_path / "out"
    glb, _ = golden_paths()
    assert c2.read_gltf_summary(out / "reference-machine-mesh.glb") == c2.read_gltf_summary(glb)
    row = json.loads((out / "reference-machine-mesh.mesh-manifest.json").read_text(encoding="utf-8"))
    assert row["triangles"] == 116
    prov = json.loads((out / "reference-machine-mesh.provenance.json").read_text(encoding="utf-8"))
    assert prov["route"] == "mesh-export" and prov["tools"]["blender"]["version"].startswith(rh.BLENDER_PIN)
    report = json.loads((out / "reference-machine-mesh.check-report.json").read_text(encoding="utf-8"))
    assert report["passed"] is True
