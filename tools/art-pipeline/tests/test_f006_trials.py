"""Tests for the T545 F006 trial tooling: seeds, plans, sheets and the SC-004, SC-005 and SC-006 oracles (research R18)."""
import json
import sys
from collections import Counter
from pathlib import Path

import pytest
from PIL import Image

F006_DIR = Path(__file__).resolve().parents[1] / "trials" / "f006"
sys.path.insert(0, str(F006_DIR))

import f006_trials as ft  # noqa: E402
import final_readability as fr  # noqa: E402

SEEDS = ft.load_seeds()


# --- seeds ------------------------------------------------------------------------------------------------------------

def test_seed_file_holds_only_seeds_and_limits_never_answers():
    text = (F006_DIR / "seeds.json").read_text(encoding="utf-8").lower()
    for forbidden in ("stopped\"", "sharedresource", "presented", "answer\"", "slots\""):
        assert forbidden not in text
    assert SEEDS["sc004"]["stoppedLimitSeconds"] == 3.0 and SEEDS["sc004"]["resourceLimitSeconds"] == 5.0
    assert SEEDS["sc005"]["timedLimitSeconds"] == 2.0 and SEEDS["sc005"]["timedMedianMaxSeconds"] == 1.077
    assert SEEDS["sc004"]["required"] == SEEDS["sc006"]["required"] == 9


def test_f006_seeds_differ_from_the_f004_seeds():
    f006 = set(SEEDS["sc005"]["timedSeeds"].values()) | {SEEDS["sc004"]["seed"], SEEDS["sc006"]["seed"], SEEDS["sc005"]["seedBase"]}
    assert not f006 & (set(fr.SC002_SEEDS.values()) | {fr.SC001_SEED_BASE})


# --- SC-004 -----------------------------------------------------------------------------------------------------------

def test_sc004_is_deterministic_with_ten_trials_and_a_separate_practice_stream():
    a, pa = ft.sc004_trials(SEEDS["sc004"]["seed"])
    b, pb = ft.sc004_trials(SEEDS["sc004"]["seed"])
    assert a == b and pa == pb and len(a) == 10 and pa["trial"] == 0
    assert a != ft.sc004_trials(SEEDS["sc004"]["seed"] + 1)[0]
    assert [t["trial"] for t in a] == list(range(1, 11))


def test_sc004_trials_are_mixed_state_with_one_shared_multi_machine_stoppage():
    trials, practice = ft.sc004_trials(SEEDS["sc004"]["seed"])
    for t in trials + [practice]:
        assert len(t["slots"]) == 12 and all(t["slots"])
        group = t["sharedSlots"]
        assert 2 <= len(group) <= 4
        consumer = ft.RESOURCE_BY_CONSUMER[t["sharedResource"]]
        assert [i for i, s in enumerate(t["slots"]) if s["machine"] == consumer] == group
        assert all(t["slots"][i]["state"] == "missing-input" for i in group)
        stopped = {s["slot"]: s["category"] for s in t["stopped"]}
        assert set(group) <= set(stopped)
        assert {i for i, s in enumerate(t["slots"]) if s["state"] in ft.CATEGORIES} == set(stopped)
        assert all(t["slots"][i]["state"] == c for i, c in stopped.items())
        assert all(b["state"] in ft.BENIGN for b in t["benign"])
        assert not {b["slot"] for b in t["benign"]} & set(stopped)
    assert Counter(t["class"] for t in trials) == {c: 2 for c in ft.CLASS_NAMES}
    assert len({t["sharedResource"] for t in trials}) >= 2
    assert any(c["category"] == "storage-full" for t in trials for c in t["stopped"])


def _sc004_perfect_log(key, a_ms=1500.0, b_ms=2500.0):
    return {"trials": [{"trial": t["trial"], "stopped": t["stopped"], "stoppedMs": a_ms, "resource": t["sharedResource"], "resourceMs": b_ms} for t in key["trials"]]}


def test_sc004_oracle_passes_a_perfect_log_and_applies_the_time_limits_inclusively():
    key = ft.sc004_key(SEEDS["sc004"]["seed"])
    log = _sc004_perfect_log(key, 3000.0, 5000.0)
    res = ft.score_sc004(key, log)
    assert res["pass"] and res["stoppedInTime"] == res["resourceInTime"] == 10
    log["trials"][0]["stoppedMs"] = 3000.1
    log["trials"][1]["resourceMs"] = 5000.1
    res = ft.score_sc004(key, log)
    assert res["stoppedInTime"] == 9 and res["resourceInTime"] == 9 and res["pass"]


def test_sc004_oracle_needs_nine_of_ten_for_each_question_independently():
    key = ft.sc004_key(SEEDS["sc004"]["seed"])
    log = _sc004_perfect_log(key)
    for i in (0, 1):
        log["trials"][i]["stoppedMs"] = 9000.0
    res = ft.score_sc004(key, log)
    assert res["stoppedInTime"] == 8 and not res["passStopped"] and res["passResource"] and not res["pass"]
    log = _sc004_perfect_log(key)
    log["trials"][2]["resource"] = "iron.gear"
    log["trials"][3]["resource"] = "none"
    res = ft.score_sc004(key, log)
    assert res["resourceInTime"] == 8 and not res["passResource"] and res["passStopped"]


def test_sc004_oracle_demands_the_exact_set_with_categories_and_a_complete_log():
    key = ft.sc004_key(SEEDS["sc004"]["seed"])
    log = _sc004_perfect_log(key)
    log["trials"][0]["stopped"] = log["trials"][0]["stopped"][:-1]  # one machine not named
    log["trials"][1]["stopped"] = [dict(s, category="storage-full" if s["category"] == "missing-input" else "missing-input") for s in log["trials"][1]["stopped"]]
    log["trials"][2]["stopped"] = log["trials"][2]["stopped"] + [{"slot": 99, "category": "missing-input"}]
    res = ft.score_sc004(key, log)
    assert res["stoppedInTime"] == 7 and not res["pass"]
    assert "stoppedDetail" in res["rows"][0]
    short = _sc004_perfect_log(key)
    short["trials"].pop()
    res = ft.score_sc004(key, short)
    assert not res["pass"] and res["rows"][-1]["missing"]


# --- SC-006 -----------------------------------------------------------------------------------------------------------

def test_stage_mirror_matches_the_shipped_table_and_other_layout_sizes():
    assert [ft.stage_of(u) for u in (4, 6, 8, 10, 12)] == [0, 0, 1, 2, 2]
    assert [ft.stage_of(u, 7, 2) for u in range(2, 8)] == [0, 0, 1, 1, 2, 2]
    assert ft.stage_of(3, 12, 4) == 0 and ft.stage_of(4, 4, 4) == 0
    src = (ft.REPO / "Assets/IdleFactory/Unity/Presentation/World/DevelopmentStage.cs").read_text(encoding="utf-8")
    assert "3L * (unlocked - baseUnlocked) / (total - baseUnlocked)" in src  # the formula the mirror copies


def test_sc006_triptychs_are_seeded_varied_and_each_stage_is_really_that_stage():
    a = ft.sc006_trials(SEEDS["sc006"]["seed"])
    assert a == ft.sc006_trials(SEEDS["sc006"]["seed"]) and len(a) == 10
    for t in a:
        assert [s["stage"] for s in t["stages"]] == [0, 1, 2]
        for st in t["stages"]:
            assert ft.stage_of(st["unlocked"]) == st["stage"]
            assert sum(s["cond"] != "locked" for s in st["slots"]) == st["unlocked"]
        for i in range(12):  # the same factory: a slot's machine type never changes between stages
            kinds = {st["slots"][i]["machine"] for st in t["stages"] if st["slots"][i]["cond"] == "occupied"}
            assert len(kinds) <= 1
        levels = [max([s["level"] for s in st["slots"] if s["cond"] == "occupied"] or [0]) for st in t["stages"]]
        assert levels == sorted(levels)
    assert len({t["mix"] for t in a}) >= 2
    assert Counter(t["class"] for t in a) == {c: 2 for c in ft.CLASS_NAMES}
    assert len({tuple(t["presented"]) for t in a}) == 6  # every presentation order occurs


def test_sc006_answers_follow_the_presented_order_and_the_oracle_counts_correct_orders():
    key = ft.sc006_key(SEEDS["sc006"]["seed"])
    for t in key["trials"]:
        assert sorted(t["answer"]) == ["A", "B", "C"]
        for stage, letter in enumerate(t["answer"]):
            assert t["presented"]["ABC".index(letter)] == stage
    log = {"trials": [{"trial": t["trial"], "order": t["answer"]} for t in key["trials"]]}
    assert ft.score_sc006(key, log)["pass"]
    log["trials"][0]["order"] = "".join(reversed(key["trials"][0]["answer"]))
    res = ft.score_sc006(key, log)
    assert res["correct"] == 9 and res["pass"]
    log["trials"][1]["order"] = "".join(reversed(key["trials"][1]["answer"]))
    res = ft.score_sc006(key, log)
    assert res["correct"] == 8 and not res["pass"]
    spaced = {"trials": [{"trial": t["trial"], "order": " > ".join(t["answer"].lower())} for t in key["trials"]]}
    assert ft.score_sc006(key, spaced)["pass"]


# --- SC-005 -----------------------------------------------------------------------------------------------------------

def test_sc005_tiles_extend_the_f004_tiles_with_every_p3_cue():
    tiles = ft.sc005_tile_definitions()
    ids = [t["id"] for t in tiles]
    assert len(ids) == len(set(ids)) == 22 + len(ft.P3_CUES)
    assert [t["id"] for t in tiles[:22]] == [t["id"] for t in fr.tile_definitions()]
    cues = " ".join(c["cue"] for c in ft.P3_CUES)
    for needed in ("upgrade", "shortage", "store full", "ghost", "hub dot", "card"):
        assert needed in cues
    assert {t["id"] for t in tiles if t["id"].startswith("p3-")} == set(ft.P3_BY_ID)


def test_cue_names_map_to_ids_with_the_most_specific_keyword_winning():
    assert ft.norm_cue("Upgrade marker (arrow)") == "p3-upgrade-marker"
    assert ft.norm_cue("a store full marker at the depot") == "p3-attention-store-full"
    assert ft.norm_cue("shortage marker with an ore icon") == "p3-attention-shortage"
    assert ft.norm_cue("ghost preview of a machine") == "p3-ghost-preview"
    assert ft.norm_cue("no dot") == "p3-hub-no-dot" and ft.norm_cue("hub dot") == "p3-hub-dot"
    assert ft.norm_cue("the machine card docked beside it") == "p3-card-next-to-machine"
    assert ft.norm_cue("a purple banana") == ""


def _synthetic_raw_sc005(raw: Path):
    for name in SEEDS["sc005"]["sets"]:
        (raw / name).mkdir(parents=True)
        for i, t in enumerate(ft.sc005_tile_definitions()):
            Image.new("RGB", (30 + 4 * (i % 3), 20), (10 * (i % 20), 40, 90)).save(raw / name / f"{t['id']}.png")


def _transcript(keys: Path, flip_p3: bool = False, flip_state: bool = False) -> str:
    lines = []
    for path in sorted(keys.glob("sc005-*-key.json")):
        if "timed" in path.name:
            continue
        key = json.loads(path.read_text(encoding="utf-8"))
        for flt, cells in key["filters"].items():
            lines.append(f"## {key['set']} / {flt}")
            for cell in cells:
                if "cue" in cell:
                    cue = "ghost preview" if flip_p3 and cell["id"] == "p3-upgrade-marker" else cell["cue"]
                    lines.append(f"| {cell['number']} | {cue} |")
                    continue
                cond = {"occupied": "occupied", "empty": "empty", "locked": f"locked {cell['level']}"}[cell["cond"]]
                state = cell["state"].replace("-", " ") if cell["cond"] == "occupied" else ""
                if flip_state and cell["id"] == "miner-running":
                    state = "paused"
                markers = "none" if not cell["markers"] else ("both" if len(cell["markers"]) == 2 else cell["markers"][0])
                lines.append(f"| {cell['number']} | {cond} | {cell['machine']} | {state} | {markers} | {'yes' if cell['selected'] else 'no'} |")
    return "\n".join(lines)


def _score_set(keys: Path, text: str, name: str = "emuP") -> dict:
    return ft.score_sc005(json.loads((keys / f"sc005-{name}-key.json").read_text(encoding="utf-8")), fr.parse_naming(text), ft.parse_p3_naming(text))


def test_sc005_sheets_cover_four_filters_and_the_oracle_scores_a_blind_transcript(tmp_path):
    raw, out, keys = tmp_path / "raw", tmp_path / "out", tmp_path / "keys"
    _synthetic_raw_sc005(raw)
    summary = ft.build_sc005(raw, out, keys)
    assert set(summary) == set(SEEDS["sc005"]["sets"])
    for name in summary:
        assert sorted(p.name for p in (out / name).glob("sheet_*.png")) == sorted(f"sheet_{m}.png" for m in ft.FILTERS)
    assert not list(out.rglob("*key*"))
    res = _score_set(keys, _transcript(keys))
    assert res["pass"] and res["passP3"] and res["passGateFields"]
    res = _score_set(keys, _transcript(keys, flip_p3=True))
    assert not res["pass"] and not res["passP3"] and res["passGateFields"]
    res = _score_set(keys, _transcript(keys, flip_state=True))
    assert not res["pass"] and not res["passGateFields"] and res["passP3"]


def test_sc005_cli_scores_all_sets_from_one_transcript(tmp_path):
    raw, out, keys = tmp_path / "raw", tmp_path / "out", tmp_path / "keys"
    _synthetic_raw_sc005(raw)
    ft.build_sc005(raw, out, keys)
    naming = tmp_path / "naming.md"
    naming.write_text(_transcript(keys), encoding="utf-8")
    assert ft.main(["score-sc005", "--keys", str(keys), "--naming", str(naming), "--json", str(tmp_path / "r.json")]) == 0
    assert json.loads((tmp_path / "r.json").read_text(encoding="utf-8"))["pass"]
    naming.write_text(_transcript(keys, flip_p3=True), encoding="utf-8")
    assert ft.main(["score-sc005", "--keys", str(keys), "--naming", str(naming)]) == 1


def test_sc005_sheet_builder_reports_missing_tiles(tmp_path):
    raw = tmp_path / "raw"
    _synthetic_raw_sc005(raw)
    (raw / "emuP" / "p3-hub-dot.png").unlink()
    with pytest.raises(SystemExit):
        ft.build_sc005(raw, tmp_path / "out", tmp_path / "keys")


def test_timed_cue_trial_needs_ten_of_ten_within_two_seconds_and_the_median_bound():
    key = ft.timed_key("standard")
    assert key["requiredCorrect"] == 10 and key["limitSeconds"] == 2.0 and len(key["trials"]) == 10
    assert key == ft.timed_key("standard") and key != ft.timed_key("reduced-motion")
    log = {"trials": [{"trial": t["trial"], "slot": t["problemSlot"], "ms": 1000.0} for t in key["trials"]]}
    assert ft.score_timed(key, log)["pass"]
    log["trials"][0]["ms"] = 2001.0
    assert not ft.score_timed(key, log)["pass"]
    slow = {"trials": [{"trial": t["trial"], "slot": t["problemSlot"], "ms": 1500.0} for t in key["trials"]]}
    res = ft.score_timed(key, slow)
    assert res["correctInTime"] == 10 and not res["medianOk"] and not res["pass"]
    edge = {"trials": [{"trial": t["trial"], "slot": t["problemSlot"], "ms": 1077.0} for t in key["trials"]]}
    assert ft.score_timed(key, edge)["pass"]


def test_f004_timed_seeds_are_restored_after_the_f006_swap():
    before = dict(fr.SC002_SEEDS)
    with ft.f006_timed_seeds():
        assert fr.SC002_SEEDS == SEEDS["sc005"]["timedSeeds"]
    assert fr.SC002_SEEDS == before


# --- plan, sheets, filters, blinding -----------------------------------------------------------------------------------

def test_plan_writes_the_spec_and_keys_and_refuses_paths_inside_the_repository(tmp_path):
    summary = ft.build_plan(tmp_path / "spec.json", tmp_path / "keys")
    assert summary["perTrial"]["sc004"] == 11 and summary["perTrial"]["sc006"] == 30
    assert summary["perTrial"]["sc005"] == len(SEEDS["sc005"]["sets"]) * (22 + len(ft.P3_CUES))
    spec = json.loads((tmp_path / "spec.json").read_text(encoding="utf-8"))
    sources = {j["class"]: j["source"] for j in spec["jobs"] if j["trial"] in ("sc004", "sc006")}
    assert sources["emuP"] == "emulator" and {v for k, v in sources.items() if k != "emuP"} == {"windows-batch"}
    assert {p.name for p in (tmp_path / "keys").iterdir()} >= {"sc004-key.json", "sc006-key.json", "sc005-timed-standard-key.json"}
    inside = ft.REPO / "specs" / "x-never-written"
    assert ft.inside_repo(inside) and not ft.inside_repo(tmp_path)
    with pytest.raises(SystemExit):
        ft.main(["plan", "--spec", str(inside / "spec.json"), "--keys", str(tmp_path / "k2")])
    with pytest.raises(SystemExit):
        ft.main(["plan", "--spec", str(tmp_path / "s2.json"), "--keys", str(inside)])
    assert not inside.exists()


def test_sc006_triptychs_are_neutral_and_carry_no_stage_information(tmp_path):
    ft.build_plan(tmp_path / "spec.json", tmp_path / "keys")
    raw, out = tmp_path / "raw", tmp_path / "out"
    raw.mkdir()
    for j in json.loads((tmp_path / "spec.json").read_text(encoding="utf-8"))["jobs"]:
        if j["trial"] == "sc006":
            Image.new("RGB", (60, 100), (20 + 80 * int(j["id"][-1]), 30, 40)).save(raw / f"{j['id']}.png")
    ft.build_sc006(raw, out, tmp_path / "keys")
    key = json.loads((tmp_path / "keys" / "sc006-key.json").read_text(encoding="utf-8"))
    assert sorted(p.name for p in out.glob("*.png")) == [t["file"] for t in key["trials"]]
    for p in out.iterdir():
        assert not any(w in p.name.lower() for w in ("stage", "early", "partial", "established", "key"))
    meta = json.loads((out / "trials.json").read_text(encoding="utf-8"))
    assert set(meta) == {"files", "panels", "task"}
    first = key["trials"][0]
    img = Image.open(out / first["file"])
    assert not img.info.get("text") and img.size[0] == 3 * 60 + 4 * 12
    # panel colour encodes the stage in this synthetic capture: the panels must appear in the presented order
    for letter_index, stage in enumerate(first["presented"]):
        x = 12 + letter_index * 72 + 50
        assert img.getpixel((x, 80))[0] == 20 + 80 * stage


def test_sc004_page_embeds_slot_centres_but_no_answers(tmp_path):
    ft.build_plan(tmp_path / "spec.json", tmp_path / "keys")
    raw, out = tmp_path / "raw", tmp_path / "out"
    raw.mkdir()
    key = json.loads((tmp_path / "keys" / "sc004-key.json").read_text(encoding="utf-8"))
    Image.new("RGB", (1080, 2400), (40, 40, 48)).save(raw / "sc004-practice.png")
    for t in key["trials"]:
        Image.new("RGB", (100, 200), (40, 40, 48)).save(raw / f"sc004-{t['trial']:02d}.png")
    (raw / "sc004-03.json").write_text(json.dumps({"w": 100, "h": 200, "n": 2, "centres": [[i * 8, 100] for i in range(12)]}), encoding="utf-8")
    assert ft.build_sc004(raw, out, tmp_path / "keys") == {"trials": 10}
    html = (out / "trial.html").read_text(encoding="utf-8")
    data = json.loads((out / "trials.json").read_text(encoding="utf-8"))
    blob = json.dumps(data)
    assert len(data["trials"][0]["centres"]) == 12 and data["trials"][2]["lift"] == 24 and data["trials"][2]["w"] == 100
    for leak in ("sharedResource", "sharedSlots", "benign", "class", "expectShortage"):
        assert leak not in blob
    assert "__DATA__" not in html and "SC-004" in html
    assert sorted(p.name for p in out.glob("*.png")) == sorted(["practice.png"] + [t["file"] for t in key["trials"]])


def test_filters_make_grayscale_and_three_cvd_versions_of_any_image(tmp_path):
    src = tmp_path / "trial_01.png"
    img = Image.new("RGB", (8, 8))
    for x in range(8):
        for y in range(8):
            img.putpixel((x, y), (30 * x, 255 - 30 * y, 40 * ((x + y) % 5)))
    img.save(src)
    names = ft.apply_filters(src, tmp_path / "f")
    assert names == [f"trial_01_{m}.png" for m in ("grayscale", "deuteranopia", "protanopia", "tritanopia")]
    outs = [Image.open(tmp_path / "f" / n).convert("RGB") for n in names]
    assert all(o.size == img.size for o in outs)
    assert all(abs(r - g) <= 1 and abs(g - b) <= 1 for r, g, b in outs[0].getdata())
    assert len({tuple(o.getdata()) for o in outs}) == 4
    assert ft.main(["filters", "--image", str(src), "--out", str(tmp_path / "g")]) == 0
