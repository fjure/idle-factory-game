"""The two seeded factory states of the F007 comparison captures (research R08, plan review QA-9, FR-012).

The seeds are the F006 trial seeds of `tools/art-pipeline/trials/f006/seeds.json` (sc006 `seed` 6060 for the stage-1 factory, sc004 `seed` 6040 for the mixed-state
factory). The slot format is the one of the F006 capture driver (`cond`, `machine`, `state`, `level`, `upgrade`), so the same driver forces the same visuals.

    partial  5 occupied locations, stage 1 (8 locations unlocked), mixed running and exactly one missing-input machine
    dense    12 occupied locations, the latest stage (2), mixed states: missing input, storage full, paused and an upgrade available

    python -m metrics.fixtures --write <fixtures.json> --capture-commit <sha> --trials-commit <sha>
"""
from __future__ import annotations

import argparse
import hashlib
import json
import random
import sys
from pathlib import Path

SLOTS = 12
MACHINES = ["miner", "smelter", "press", "assembler"]
CONSUMERS = ["smelter", "press", "assembler"]  # the types that can be short of an input
SEEDS_RELATIVE = "tools/art-pipeline/trials/f006/seeds.json"
STATE_SEEDS = {"partial": 6060, "dense": 6040}
STAGES = {"partial": 1, "dense": 2}
UNLOCKED = {"partial": 8, "dense": 12}  # the F006 stage thresholds (t549_spec.UNLOCKED = 4, 8, 12)


def _slot(cond: str = "empty", machine: str = "", state: str = "", level: int = 0, upgrade: bool = False) -> dict:
    return {"cond": cond, "machine": machine, "state": state, "level": level, "upgrade": upgrade}


def make_state(state: str, seed: int | None = None) -> dict:
    seed = STATE_SEEDS[state] if seed is None else seed
    rng = random.Random(seed)
    unlocked = UNLOCKED[state]
    slots = [_slot("locked", level=2 + s // 4) if s >= unlocked else _slot() for s in range(SLOTS)]
    if state == "partial":
        occupied = sorted(rng.sample(range(unlocked), 5))
        states = ["running"] * 4 + ["missing-input"]
    else:
        occupied = list(range(SLOTS))
        states = ["missing-input", "missing-input", "storage-full", "storage-full", "paused", "running", "running", "running", "running", "running", "running", "running"]
    rng.shuffle(states)
    # Every machine type appears; a missing-input machine is a consumer type.
    types = (MACHINES * 4)[:len(occupied)]
    rng.shuffle(types)
    for i, slot in enumerate(occupied):
        if states[i] == "missing-input" and types[i] == "miner":
            j = next(k for k, t in enumerate(types) if t in CONSUMERS and states[k] != "missing-input")
            types[i], types[j] = types[j], types[i]
    for i, slot in enumerate(occupied):
        slots[slot] = _slot("occupied", types[i], states[i], rng.randint(1, 3) if state == "partial" else rng.randint(2, 5))
    if state == "dense":
        running = [s for s in occupied if slots[s]["state"] == "running"]
        for s in rng.sample(running, 2):
            slots[s]["upgrade"] = True
    running = [s for s in occupied if slots[s]["state"] == "running"]
    middle = sorted(occupied)[len(occupied) // 2]
    # The slot whose machine card is opened for the phoneP-card class: the running machine nearest the middle of the occupied locations.
    card_slot = min(running, key=lambda s: (abs(s - middle), s))
    return {"seed": seed, "stage": STAGES[state], "unlocked": unlocked, "occupied": len(occupied), "cardSlot": card_slot, "slots": slots}


def seeds_sha256(repo: Path) -> str:
    return hashlib.sha256((repo / SEEDS_RELATIVE).read_bytes()).hexdigest()


def build_fixtures(repo: Path, capture_commit: str, trials_commit: str) -> dict:
    seeds = json.loads((repo / SEEDS_RELATIVE).read_text(encoding="utf-8"))
    assert seeds["sc006"]["seed"] == STATE_SEEDS["partial"] and seeds["sc004"]["seed"] == STATE_SEEDS["dense"], "the F006 seeds changed"
    return {
        "schema": 1,
        "feature": "007-stylized-industrial-visual-reboot",
        "note": "Pinned fixtures of the F007 comparison captures (R08, QA-9). Seeds are the F006 trial seeds; the slot lists are derived from them by tools/art-pipeline/metrics/fixtures.py and are the driver's input.",
        "captureCommit": capture_commit,
        "trialFixtures": {"source": SEEDS_RELATIVE, "sha256": seeds_sha256(repo), "commit": trials_commit,
                          "seedIds": {"partial": "sc006.seed", "dense": "sc004.seed"}},
        "states": {name: make_state(name) for name in ("partial", "dense")},
    }


def main(argv: list[str]) -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--write", required=True)
    p.add_argument("--capture-commit", required=True)
    p.add_argument("--trials-commit", required=True)
    args = p.parse_args(argv)
    repo = Path(__file__).resolve().parents[3]
    data = build_fixtures(repo, args.capture_commit, args.trials_commit)
    Path(args.write).write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
