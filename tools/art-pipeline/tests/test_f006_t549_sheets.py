"""Tests for the T549 capture spec and the sheet size rule (C-102: at most 4000 px on the long side)."""
import json
import sys
from pathlib import Path

from PIL import Image

F006_DIR = Path(__file__).resolve().parents[1] / "trials" / "f006"
sys.path.insert(0, str(F006_DIR))

import f006_trials as ft  # noqa: E402
import t549_sheets as ts  # noqa: E402
import t549_spec as sp  # noqa: E402


def test_spec_has_three_stages_and_five_p3_shots_per_class(tmp_path):
    path = tmp_path / "spec.json"
    count = sp.build(path)
    jobs = json.loads(path.read_text())["jobs"]
    assert count == len(jobs) == len(sp.CLASSES) * 8
    for name in sp.CLASSES:
        stages = sorted(j["expectStage"] for j in jobs if j["id"].startswith(f"sc006-{name}-"))
        assert stages == [0, 1, 2]
    for j in jobs:
        if j["trial"] == "sc006":
            assert ft.stage_of(j["factoryUnlocked"]) == j["expectStage"]
            assert len(j["slots"]) == ft.SLOTS


def test_emulator_spec_uses_the_device_window(tmp_path):
    path = tmp_path / "spec.json"
    sp.build(path, emulator=True)
    jobs = json.loads(path.read_text())["jobs"]
    assert {j["cls"] for j in jobs} == {"emulator"} and all(j["source"] == "emulator" for j in jobs)


def test_fit_keeps_the_long_side_at_or_below_the_limit():
    big = Image.new("RGB", (6000, 2000))
    assert max(ts.fit(big).size) == ts.MAX_SIDE
    small = Image.new("RGB", (800, 600))
    assert ts.fit(small).size == (800, 600)
