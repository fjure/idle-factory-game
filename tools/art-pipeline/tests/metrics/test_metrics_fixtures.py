"""The pinned factory states (research R08, plan review QA-9): determinism, the R08 constraints and the committed fixtures.json."""
import json
from pathlib import Path

import pytest

from metrics.capture_plan import CLASSES, build_plan
from metrics.fixtures import SEEDS_RELATIVE, build_fixtures, make_state, seeds_sha256

REPO = Path(__file__).resolve().parents[4]
FIXTURES = REPO / "specs/007-stylized-industrial-visual-reboot/reviews/comparison/fixtures.json"


def occupied(state):
    return [s for s in state["slots"] if s["cond"] == "occupied"]


def test_states_are_deterministic_per_seed():
    assert make_state("partial") == make_state("partial")
    assert make_state("dense") == make_state("dense")
    assert make_state("dense", 1) != make_state("dense", 2)


def test_partial_state_matches_r08():
    s = make_state("partial")
    occ = occupied(s)
    assert len(occ) == 5 and s["stage"] == 1 and len(s["slots"]) == 12
    assert [o["state"] for o in occ].count("missing-input") == 1 and [o["state"] for o in occ].count("running") == 4
    assert all(o["machine"] != "miner" for o in occ if o["state"] == "missing-input")
    assert sum(1 for x in s["slots"] if x["cond"] == "locked") == 4  # 8 locations unlocked at stage 1


def test_dense_state_matches_r08():
    s = make_state("dense")
    occ = occupied(s)
    assert len(occ) == 12 and s["stage"] == 2
    states = {o["state"] for o in occ}
    assert {"running", "missing-input", "storage-full", "paused"} <= states
    assert any(o["upgrade"] and o["state"] == "running" for o in occ)
    assert all(o["machine"] != "miner" for o in occ if o["state"] == "missing-input")
    assert {o["machine"] for o in occ} == {"miner", "smelter", "press", "assembler"}


def test_card_slot_is_a_running_machine():
    for name in ("partial", "dense"):
        s = make_state(name)
        assert s["slots"][s["cardSlot"]]["state"] == "running"


def test_committed_fixtures_equal_the_generator_output():
    committed = json.loads(FIXTURES.read_text(encoding="utf-8"))
    rebuilt = build_fixtures(REPO, committed["captureCommit"], committed["trialFixtures"]["commit"])
    assert committed == rebuilt
    assert committed["trialFixtures"]["sha256"] == seeds_sha256(REPO)
    assert (REPO / SEEDS_RELATIVE).exists()
    for sha in (committed["captureCommit"], committed["trialFixtures"]["commit"]):
        assert len(sha) == 40 and sha == sha.lower()


def test_plan_has_one_job_per_cell_and_a_repeat():
    fixtures = json.loads(FIXTURES.read_text(encoding="utf-8"))
    plan = build_plan(fixtures, "F006-same-emuP-t100-dense")
    stems = [j["stem"] for j in plan["jobs"]]
    variants = sum(len(c[4]) for c in CLASSES.values())
    assert variants == 7 and len(stems) == variants * 2 + 1 == len(set(stems))
    assert stems.count("F006-same-emuP-t100-dense-repeat") == 1
    card = {j["stem"]: j["cardSlot"] for j in plan["jobs"]}
    assert card["F006-same-phoneP-card-t200-dense"] >= 0 and card["F006-same-emuP-t100-dense"] == -1
    with pytest.raises(SystemExit):
        build_plan(fixtures, "no-such-cell")
    everything = build_plan(fixtures, "all")["jobs"]
    assert len(everything) == 28 and sum(j["stem"].endswith("-repeat") for j in everything) == 14
