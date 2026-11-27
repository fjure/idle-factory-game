"""The comparison-capture sidecar validator (plan review QA-9) and the committed F006 baseline."""
import copy
import json
from pathlib import Path

import pytest

from metrics import METRICS_VERSION
from metrics.sidecar import validate_directory, validate_sidecar

REPO = Path(__file__).resolve().parents[4]
COMPARISON = REPO / "specs/007-stylized-industrial-visual-reboot/reviews/comparison"
BASELINE = COMPARISON / "F006"
COMMIT = "83812cac46d88d2c58267c2ad894e5bdc4d27e3f"

GOOD = {
    "look": "F006", "geometry": "same", "class": "emuP", "textScale": 100, "factoryState": "dense", "seed": 6040, "commit": COMMIT, "buildKind": "windows-player",
    "device": "Windows | GPU | 1080x2400", "resolution": {"w": 1080, "h": 2400}, "capturedAt": "2026-11-27T01:00:00Z", "profile": "emulator class",
    "buildId": "windows-test-player 6000.6.0f1", "framingRect": {"x": 0, "y": 100, "w": 1080, "h": 2000}, "route": "windows-test-player real scene",
    "layout": "F006-same-emuP-t100-dense-layout.json", "bodyMask": "F006-same-emuP-t100-dense-bodymask.png", "metricsVersion": METRICS_VERSION,
    "deadAreaShare": 0.2, "medianMachineAreaPx": 1234,
}
FIXTURES = {"captureCommit": COMMIT, "states": {"dense": {"seed": 6040}, "partial": {"seed": 6060}}}
NAME = "F006-same-emuP-t100-dense.png"


def test_a_complete_sidecar_is_valid():
    assert validate_sidecar(GOOD, NAME, FIXTURES) == []


@pytest.mark.parametrize("field", sorted(GOOD))
def test_every_required_field_is_required(field):
    side = copy.deepcopy(GOOD)
    del side[field]
    assert validate_sidecar(side, NAME, FIXTURES)


@pytest.mark.parametrize("field,value", [
    ("look", "Z"), ("geometry", "wide"), ("class", "phoneX"), ("factoryState", "empty"), ("buildKind", "emulator"), ("textScale", 150), ("seed", "6040"),
    ("commit", "83812cac"), ("resolution", {"w": 0, "h": 5}), ("deadAreaShare", 1.5), ("medianMachineAreaPx", -1), ("framingRect", {"x": 0}), ("device", ""),
])
def test_invalid_values_are_rejected(field, value):
    side = copy.deepcopy(GOOD)
    side[field] = value
    assert validate_sidecar(side, NAME, FIXTURES)


def test_file_name_must_match_the_fields():
    assert validate_sidecar(GOOD, "F006-same-emuP-t100-partial.png", FIXTURES)


def test_seed_and_commit_must_match_the_fixture_list():
    other_seed = {**GOOD, "seed": 1}
    assert any("seed" in p for p in validate_sidecar(other_seed, NAME, FIXTURES))
    other_commit = {**GOOD, "commit": "a" * 40}
    assert any("commit" in p for p in validate_sidecar(other_commit, NAME, FIXTURES))


def test_directory_needs_a_sidecar_for_every_png_and_a_png_for_every_sidecar(tmp_path):
    (tmp_path / "F006-same-emuP-t100-dense.png").write_bytes(b"x")
    assert validate_directory(tmp_path)["F006-same-emuP-t100-dense.png"] == ["no JSON sidecar"]
    (tmp_path / "F006-same-emuL-t100-dense.json").write_text("{}", encoding="utf-8")
    assert validate_directory(tmp_path)["F006-same-emuL-t100-dense.json"] == ["sidecar without a PNG"]


def test_the_committed_f006_baseline_validates_against_the_fixture_list():
    fixtures = json.loads((COMPARISON / "fixtures.json").read_text(encoding="utf-8"))
    assert validate_directory(BASELINE, fixtures) == {}
    sidecars = sorted(p for p in BASELINE.glob("*.json") if not p.stem.endswith("-layout") and p.stem != "baseline-metrics")
    assert len(sidecars) == 14  # 7 class variants x 2 factory states
    frozen = json.loads((BASELINE / "baseline-metrics.json").read_text(encoding="utf-8"))
    assert frozen["metricsVersion"] == METRICS_VERSION and len(frozen["cells"]) == 14
    assert len(frozen["crossSession"]) >= 2 and all(c["allIdentical"] for c in frozen["crossSession"])
    assert len(frozen["determinism"]) == 14 and all(d["identical"] and all(d[k]["first"] == d[k]["repeat"] for k in ("png", "bodymask", "layout")) for d in frozen["determinism"])


def test_repeat_comparison_covers_png_mask_and_layout(tmp_path):
    from metrics.finalize_captures import compare_repeats

    def put(stem, png=b"a", mask=b"m", layout=b"{}"):
        (tmp_path / f"{stem}.png").write_bytes(png)
        (tmp_path / f"{stem}-bodymask.png").write_bytes(mask)
        (tmp_path / f"{stem}-layout.json").write_bytes(layout)
        (tmp_path / f"{stem}-raw.json").write_text("{}", encoding="utf-8")

    put("c1"), put("c1-repeat")
    put("c2"), put("c2-repeat", layout=b'{"x":1}')
    put("c3"), put("c3-repeat", png=b"b")
    put("c4"), put("c4-repeat", mask=b"n")
    rows = {r["cell"]: r for r in compare_repeats(tmp_path)}
    assert rows["c1"]["identical"] and not rows["c2"]["identical"] and not rows["c3"]["identical"] and not rows["c4"]["identical"]


def test_cross_session_comparison_flags_any_difference(tmp_path):
    from metrics.finalize_captures import compare_sessions

    a, b = tmp_path / "a", tmp_path / "b"
    for d in (a, b):
        d.mkdir()
        (d / "c-raw.json").write_text("{}", encoding="utf-8")
        (d / "c.png").write_bytes(b"p")
        (d / "c-bodymask.png").write_bytes(b"m")
        (d / "c-layout.json").write_bytes(b"{}")
    assert compare_sessions(a, [b])[0]["allIdentical"]
    (b / "c.png").write_bytes(b"q")
    assert not compare_sessions(a, [b])[0]["allIdentical"]
