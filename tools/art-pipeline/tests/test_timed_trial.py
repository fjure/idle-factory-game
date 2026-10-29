"""Tests for the SC-002 timed-trial harness and the SC-001 capture-sheet builder (T422)."""
import json
import sys
from collections import Counter
from pathlib import Path

import pytest
from PIL import Image, ImageChops

TRIALS_DIR = Path(__file__).resolve().parents[1] / "trials"
sys.path.insert(0, str(TRIALS_DIR))

import make_capture_sheets as sheets  # noqa: E402
import timed_trial as tt  # noqa: E402

REPO = TRIALS_DIR.parents[2]
SHOTS = REPO / "specs/004-visual-identity/reviews/style-spike/s3/shots"
LATTICE = ["miner-running", "waiting", "missing-input", "storage-full", "paused", "inactive",
           "plate-empty", "plate-locked", "assembler-running", "plate-occupied", "lightB-running", "ore"]


@pytest.fixture(scope="module")
def generated(tmp_path_factory):
    out = tmp_path_factory.mktemp("trial")
    key = tt.generate("a", 23, "emuPwin", "nmin", 4220, out)
    return out, key


def bytes_of(folder: Path) -> dict[str, bytes]:
    return {p.name: p.read_bytes() for p in sorted(folder.iterdir())}


def test_same_seed_gives_identical_files(tmp_path):
    tt.generate("b", 23, "emuPwin", "nmin", 7, tmp_path / "one")
    tt.generate("b", 23, "emuPwin", "nmin", 7, tmp_path / "two")
    assert bytes_of(tmp_path / "one") == bytes_of(tmp_path / "two")


def test_different_seed_changes_the_trials(tmp_path):
    k1 = tt.generate("a", 23, "emuPwin", "nmin", 1, tmp_path / "one", write_html=False)
    k2 = tt.generate("a", 23, "emuPwin", "nmin", 2, tmp_path / "two", write_html=False)
    assert [t["slotKinds"] for t in k1["trials"]] != [t["slotKinds"] for t in k2["trials"]]


def test_ten_trials_with_balanced_problem_states(generated):
    out, key = generated
    assert len(key["trials"]) == 10
    counts = Counter(t["problemState"] for t in key["trials"])
    assert set(counts) == set(tt.PROBLEM_STATES)
    assert sorted(counts.values()) == [3, 3, 4]
    for seed in range(50):  # balance holds for every seed, not only the default
        states = tt.problem_sequence(__import__("random").Random(seed))
        assert sorted(Counter(states).values()) == [3, 3, 4]


def test_every_screen_has_exactly_one_problem_machine_and_one_or_two_benign_distractors(generated):
    out, key = generated
    for t in key["trials"]:
        kinds = t["slotKinds"]
        assert len(kinds) == tt.SLOTS
        problems = [i for i, k in enumerate(kinds) if k in tt.PROBLEM_STATES]
        assert problems == [t["problemSlot"]]
        assert kinds[t["problemSlot"]] == t["problemState"]
        distractors = [i for i, k in enumerate(kinds) if k in tt.DISTRACTOR_STATES]
        assert sorted(distractors) == sorted(d["slot"] for d in t["distractors"])
        assert 1 <= len(distractors) <= 2
        assert {k for k in kinds if k not in tt.PROBLEM_STATES + tt.DISTRACTOR_STATES} <= {n for n, _ in tt.FILLERS}


def test_images_match_the_key_and_the_slot_geometry(generated):
    out, key = generated
    layout = tt.load_layout("emuPwin", "nmin")
    assert key["n"] == layout["n"] == 3 and key["screen"] == [1080, 2400]
    for t in key["trials"]:
        recomposed = tt.compose(layout, t["slotKinds"], "a", 23).convert("RGB")
        shown = Image.open(out / t["file"]).convert("RGB")
        assert shown.size == (1080, 2400)
        assert ImageChops.difference(recomposed, shown).getbbox() is None


def test_key_has_the_trial_files_and_the_page_has_no_answers(generated):
    out, key = generated
    for t in key["trials"]:
        assert (out / t["file"]).is_file()
    html = (out / "trial.html").read_text(encoding="utf-8")
    assert "problemSlot" not in html and "problemState" not in html and "slotKinds" not in html
    assert "trial_01.png" in html and "trial_10.png" in html


@pytest.mark.parametrize("variant,size", [("a", 17), ("a", 23), ("b", 17), ("b", 23)])
def test_every_variant_and_size_has_its_sprites(variant, size):
    for kind in tt.PROBLEM_STATES + tt.DISTRACTOR_STATES + [n for n, _ in tt.FILLERS]:
        for sprite in tt.layers_for(kind, variant, size):
            assert sprite.is_file(), sprite


def test_nearest_slot_maps_a_click_on_a_machine_to_its_slot():
    layout = tt.load_layout("emuPwin", "nmin")
    n = layout["n"]
    for i, (cx, cy) in enumerate(layout["slots"]):
        assert tt.nearest_slot(layout["slots"], n, cx, cy - 12 * n) == i
        assert tt.nearest_slot(layout["slots"], n, cx + 5 * n, cy - 8 * n) == i


def test_score_rule_is_nine_of_ten_within_two_seconds(generated):
    out, key = generated

    def log(ms_for, wrong=()):
        return {"trials": [{"trial": t["trial"], "slot": (t["problemSlot"] + 1) % 12 if t["trial"] in wrong else t["problemSlot"],
                            "ms": ms_for(t["trial"])} for t in key["trials"]]}

    assert tt.score(key, log(lambda i: 900))["pass"]
    assert tt.score(key, log(lambda i: 2100 if i == 3 else 900))["pass"]  # 9 of 10
    assert not tt.score(key, log(lambda i: 2100 if i in (3, 4) else 900))["pass"]
    assert not tt.score(key, log(lambda i: 900, wrong=(2, 5)))["pass"]
    assert tt.score(key, {"trials": log(lambda i: 900)["trials"][:9]})["pass"]  # a missing trial counts as one failure: 9 of 10
    assert not tt.score(key, {"trials": log(lambda i: 900)["trials"][:8]})["pass"]
    assert tt.score(key, log(lambda i: 2000))["pass"]  # exactly 2.0 s is in time


@pytest.mark.skipif(not (SHOTS / "phoneP-a23-nmin.png").is_file(), reason="in-game captures not committed yet")
@pytest.mark.parametrize("name,cls,mode,variant,size", [
    ("phoneP-a23-nmin", "phoneP", "nmin", "a", 23), ("tabL-b17-fit", "tabL", "fit", "b", 17), ("tabP-a17-nmin", "tabP", "nmin", "a", 17)])
def test_composition_is_pixel_identical_to_the_in_game_capture(name, cls, mode, variant, size):
    layout = tt.load_layout(cls, mode)
    shot = Image.open(SHOTS / f"{name}.png").convert("RGB")
    mine = tt.compose(layout, LATTICE, variant, size).convert("RGB")
    # The ore (slot 11) has a half-art-pixel pivot (y 4.5, a T424 finding): at odd n Unity places it on a fractional
    # position and rounds differently, so its box is excluded; every machine and plate must match exactly.
    n, (cx, cy) = layout["n"], layout["slots"][11]
    box = (round(cx) - 20 * n, round(cy) - 24 * n, round(cx) + 20 * n, round(cy) + 4 * n)
    for img in (mine, shot):
        img.paste((0, 0, 0), box)
    assert ImageChops.difference(mine, shot).getbbox() is None


def test_capture_sheets_are_reproducible_and_keep_the_key_apart(tmp_path):
    tiles = tmp_path / "tiles"
    for name in ("s1", "s2"):
        (tiles / name).mkdir(parents=True)
        for i, colour in enumerate([(200, 40, 40), (40, 200, 40), (40, 40, 200), (200, 200, 40)]):
            Image.new("RGB", (24, 16), colour).save(tiles / name / f"tile-{i}.png")
    first = sheets.build_sets(tiles, tmp_path / "o1", 100)
    second = sheets.build_sets(tiles, tmp_path / "o2", 100)
    assert first == second
    assert bytes_of(tmp_path / "o1") == bytes_of(tmp_path / "o2")
    names = sorted(p.name for p in (tmp_path / "o1").iterdir())
    assert names == sorted([f"{s}_{m}.png" for s in ("s1", "s2") for m in ("grayscale", "deuteranopia", "protanopia", "tritanopia")] + ["s1_key.json", "s2_key.json"])
    key = json.loads((tmp_path / "o1" / "s1_key.json").read_text(encoding="utf-8"))
    assert sorted(key["order"]) == [f"tile-{i}" for i in range(4)] and key["tileCount"] == 4
    assert first["s1"]["seed"] == 100 and first["s2"]["seed"] == 101


def test_practice_screen_is_unscored_and_does_not_change_the_ten_trials(tmp_path):
    with_p = tmp_path / "with"
    without = tmp_path / "without"
    key_p = tt.generate("b", 23, "emuPwin", "nmin", 4602, with_p)
    key_n = tt.generate("b", 23, "emuPwin", "nmin", 4602, without, practice=False)
    assert (with_p / "practice.png").is_file() and not (without / "practice.png").exists()
    assert key_p["trials"] == key_n["trials"] and "practice" not in key_n
    for t in key_p["trials"]:
        assert (with_p / t["file"]).read_bytes() == (without / t["file"]).read_bytes()
    html = (with_p / "trial.html").read_text(encoding="utf-8")
    assert "practice.png" in html and "problemSlot" not in html and "problemState" not in html
    assert "(not scored)" in html
    assert "practice" not in (without / "trial.html").read_text(encoding="utf-8").split("const D=")[1].split(";")[0]
    # scoring only looks at the ten trials
    log = {"trials": [{"trial": t["trial"], "slot": t["problemSlot"], "ms": 900} for t in key_p["trials"]]}
    assert tt.score(key_p, log)["pass"]


def test_b23r2_sheets_are_reproduced_from_the_sprites(tmp_path):
    sheets.compose_tiles("b", 23, tmp_path / "tiles" / "b23r2")
    tile = Image.open(tmp_path / "tiles" / "b23r2" / "waiting.png")
    assert tile.size == (240, 192)
    sheets.build_sets(tmp_path / "tiles", tmp_path / "out", 4602)
    committed = REPO / "specs/004-visual-identity/reviews/style-spike/s3/sheets"
    for mode in ("grayscale", "deuteranopia", "protanopia", "tritanopia"):
        assert (tmp_path / "out" / f"b23r2_{mode}.png").read_bytes() == (committed / f"b23r2_{mode}.png").read_bytes()
