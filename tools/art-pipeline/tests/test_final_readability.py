"""Tests for the T445 final readability tooling (SC-001 sheets from in-game tiles, SC-002 in-game timed trials, scoring)."""
import json
import sys
from collections import Counter
from pathlib import Path

import pytest
from PIL import Image

TRIALS_DIR = Path(__file__).resolve().parents[1] / "trials"
sys.path.insert(0, str(TRIALS_DIR))

import final_readability as fr  # noqa: E402


def test_tiles_are_unique_complete_and_cover_the_required_vocabulary():
    tiles = fr.tile_definitions()
    ids = [t["id"] for t in tiles]
    assert len(ids) == len(set(ids)) == 22
    for machine in ("miner", "assembler"):
        assert {t["state"] for t in tiles if t["machine"] == machine and not t["markers"] and not t["selected"]} == set(fr.STATES)
    assert {t["level"] for t in tiles if t["cond"] == "locked"} == {2, 3, 4, 5}
    assert {m for t in tiles for m in t["markers"]} == {"boosted", "overdrive"}
    assert any(t["selected"] and t["cond"] == "occupied" for t in tiles) and any(t["selected"] and t["cond"] == "empty" for t in tiles)
    assert any(len(t["markers"]) == 2 for t in tiles)


def test_tile_shot_isolates_one_slot():
    tile = fr.tile_definitions()[0]
    shot = fr.tile_shot(tile, 4)
    assert shot["crop"] and shot["cropSlot"] == 4 and len(shot["slots"]) == 12
    assert [i for i, s in enumerate(shot["slots"]) if not s["hidden"]] == [4]


def test_trials_are_seeded_balanced_and_single_problem():
    a, practice = fr.sc002_trials(4461)
    b, _ = fr.sc002_trials(4461)
    assert a == b
    assert Counter(t["problemState"] for t in a) == {"waiting": 4, "missing-input": 3, "storage-full": 3} or sorted(Counter(t["problemState"] for t in a).values()) == [3, 3, 4]
    for t in a + [practice]:
        slots = t["slots"]
        assert len(slots) == 12 and all(s is not None for s in slots)
        problems = [i for i, s in enumerate(slots) if s["cond"] == "occupied" and s["state"] in fr.PROBLEM_STATES]
        assert problems == [t["problemSlot"]]
        assert 1 <= len(t["distractors"]) <= 2
        assert all(slots[d["slot"]]["state"] in fr.DISTRACTOR_STATES for d in t["distractors"])
        assert all(not s["selected"] and not s["boosted"] and not s["overdrive"] for s in slots)
    assert fr.sc002_trials(4462)[0] != a


def test_plan_keeps_keys_out_of_the_spec_folder_and_rejects_repo_keys(tmp_path):
    spec, keys = tmp_path / "work" / "spec.json", tmp_path / "keys"
    summary = fr.build_plan(spec, keys)
    assert summary["jobs"] == len(fr.CLASSES) * 2 + 2
    jobs = json.loads(spec.read_text(encoding="utf-8"))["jobs"]
    standard = next(j for j in jobs if j["id"] == "sc002-standard")
    reduced = next(j for j in jobs if j["id"] == "sc002-reduced-motion")
    assert len(standard["shots"]) == 11 and all(s["frames"] == 6 and not s["frozen"] and s["composite"] for s in standard["shots"])
    assert all(s["frames"] == 1 and s["frozen"] for s in reduced["shots"])
    assert (keys / "sc002-standard-key.json").exists() and (keys / "sc002-reduced-motion-key.json").exists()
    with pytest.raises(SystemExit):
        fr.main(["plan", "--spec", str(spec), "--keys", str(fr.REPO / "specs" / "nokeys-here")])


def fake_tile_capture(raw: Path, job: str, scale: int) -> None:
    folder = raw / job
    folder.mkdir(parents=True)
    colour = 0
    for tile in fr.tile_definitions():
        colour += 5
        Image.new("RGBA", (80 * scale, 64 * scale), (colour, 120, 200 - colour, 255)).save(folder / f"{tile['id']}_f0.png")
    info = {"id": job, "scale": scale, "pixelsPerPoint": 2.0, "hasFloor": True, "belowFloor": False}
    (folder / "job.json").write_text(json.dumps(info), encoding="utf-8")


def test_sheets_are_shuffled_reproducible_and_keys_stay_outside(tmp_path):
    raw, out, keys = tmp_path / "raw", tmp_path / "out", tmp_path / "keys"
    fake_tile_capture(raw, "sc001-phoneP-nmin", 2)
    fake_tile_capture(raw, "sc001-phoneP-fit", 2)  # same scale: one set for both zooms
    fake_tile_capture(raw, "sc001-emuP-nmin", 3)
    fake_tile_capture(raw, "sc001-emuP-fit", 4)
    summary = fr.build_sc001(raw, out, keys)
    assert sorted(summary) == ["emuP-1080x2400-n3", "emuP-1080x2400-n4", "phoneP-720x1280-n2"]
    assert summary["phoneP-720x1280-n2"]["zooms"] == ["fit-all", "n_min"]
    for name in summary:
        files = sorted(p.name for p in (out / name).iterdir())
        assert files == ["meta.json", "sheet_deuteranopia.png", "sheet_grayscale.png", "sheet_protanopia.png", "sheet_tritanopia.png"]
        key = json.loads((keys / f"sc001-{name}-key.json").read_text(encoding="utf-8"))
        assert list(key["filters"]) == fr.FILTERS
        for cells in key["filters"].values():
            assert [c["number"] for c in cells] == list(range(1, 23))
            assert sorted(c["id"] for c in cells) == sorted(t["id"] for t in fr.tile_definitions())
            assert [c["id"] for c in cells] != [t["id"] for t in fr.tile_definitions()]  # shuffled
        orders = [[c["id"] for c in cells] for cells in key["filters"].values()]
        assert len({tuple(o) for o in orders}) == 4  # a different shuffle per filter
    assert not list(out.rglob("*key*")) and '"id"' not in (out / "emuP-1080x2400-n3" / "meta.json").read_text(encoding="utf-8")
    again = tmp_path / "again"
    fr.build_sc001(raw, again, tmp_path / "keys2")
    for name in summary:
        for f in (out / name).glob("sheet_*.png"):
            assert f.read_bytes() == (again / name / f.name).read_bytes()
    gray = Image.open(out / "emuP-1080x2400-n3" / "sheet_grayscale.png").convert("RGB")
    # outside the drawn number boxes every pixel of the grayscale sheet is neutral
    px = gray.load()
    assert all(px[x, y][0] == px[x, y][1] == px[x, y][2] for x in range(0, gray.width, 7) for y in range(0, gray.height, 7))


def fake_trial_capture(raw: Path, job: str, frames: int, scale: int = 3) -> None:
    folder = raw / job
    folder.mkdir(parents=True)
    shots = []
    for sid in ["practice"] + [f"trial_{i:02d}" for i in range(1, 11)]:
        for k in range(frames):
            Image.new("RGBA", (108, 240), (k * 20, 60, 90, 255)).save(folder / f"{sid}_f{k}.png")
        shots.append({"id": sid, "frames": frames, "width": 108, "height": 240})
    info = {"id": job, "w": 108, "h": 240, "scale": scale, "pixelsPerPoint": 2.63, "hasFloor": True, "fps": 10.0, "frameCount": frames, "shots": shots,
            "pivots": [{"x": 18.0 + 36.0 * (i % 3), "y": 90.0 + 18.0 * (i // 3)} for i in range(12)]}
    (folder / "job.json").write_text(json.dumps(info), encoding="utf-8")


def test_sc002_sets_animate_with_motion_and_stay_still_when_reduced(tmp_path):
    keys = tmp_path / "keys"
    fr.build_plan(tmp_path / "spec.json", keys)
    raw, out = tmp_path / "raw", tmp_path / "out"
    fake_trial_capture(raw, "sc002-standard", 6)
    fake_trial_capture(raw, "sc002-reduced-motion", 1)
    summary = fr.build_sc002(raw, out, keys)
    assert summary["standard"]["trials"] == summary["reduced-motion"]["trials"] == 10
    animated = Image.open(out / "standard" / "trial_01.png")
    assert getattr(animated, "n_frames", 1) == 6 and animated.info.get("duration") == 100
    assert getattr(Image.open(out / "reduced-motion" / "trial_01.png"), "n_frames", 1) == 1
    for variant in fr.SC002_SEEDS:
        html = (out / variant / "trial.html").read_text(encoding="utf-8")
        key_text = (keys / f"sc002-{variant}-key.json").read_text(encoding="utf-8")
        assert "problemState" not in html and "storage-full" not in html and "missing-input" not in html
        assert "key.json" not in html
        assert not list((out / variant).glob("*key*")) and key_text  # keys exist only in the keys folder
    assert json.loads((out / "reduced-motion" / "meta.json").read_text(encoding="utf-8"))["motion"].startswith("running machines frozen")


def test_scoring_counts_in_time_correct_and_breaks_down_by_state(tmp_path):
    key = {"set": "standard", "limitSeconds": 2.0, "requiredCorrect": 9,
           "trials": [{"trial": i, "problemSlot": i, "problemState": fr.PROBLEM_STATES[i % 3], "problemMachine": "miner"} for i in range(1, 11)]}
    log = {"trials": [{"trial": i, "slot": i, "ms": 1000 + 10 * i} for i in range(1, 11)]}
    res = fr.score(key, log)
    assert res["pass"] and res["correctInTime"] == 10 and res["medianSeconds"] == 1.055
    assert sum(v["trials"] for v in res["byState"].values()) == 10
    log["trials"][0]["slot"] = 99  # wrong machine
    log["trials"][1]["ms"] = 2001  # too slow
    res = fr.score(key, log)
    assert not res["pass"] and res["correctInTime"] == 8
    assert sum(v["correct"] for v in res["byState"].values()) == 9 and sum(v["inTime"] for v in res["byState"].values()) == 8
    missing = fr.score(key, {"trials": log["trials"][:5]})
    assert not missing["pass"] and sum(r["missing"] for r in missing["rows"]) == 5
    assert "| Problem state |" in fr.report_text(res)
