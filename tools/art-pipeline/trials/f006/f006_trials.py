"""Feature 006 trial tooling (T545, research R18): seed lists, capture plans, trial sheets and pass/fail oracles.

Three trials are covered; the parent (T546) runs them, an independent fresh session and the owner take them, and the producer of this tooling never judges one:

    SC-004  10 seeded mixed-state factory captures. The owner names the stopped machines and their problem category within 3.0 s, and the shared
            missing resource of the multi-machine stoppage within 5.0 s. Pass: >= 9/10 for each of the two questions.
    SC-005  final sheets: the nine F004 cue states plus the P3 cues (upgrade marker, production-attention markers, ghost preview, hub dot, the card next
            to its machine) in grayscale and the three colour-vision filters, and the owner's timed cue trial (10/10 within 2.0 s, median <= 1.077 s).
    SC-006  10 shuffled stage triptychs (Early, Partial, Established per FR-036, varied screen classes and machine mixes). Pass: >= 9/10 correct orders.

Blinding. `plan` derives everything from the integer seeds in `seeds.json` and writes the capture specification (which contains the answers) and the answer
keys to folders OUTSIDE the repository; the sheet builders take the raw captures and write neutral files (`trial_NN.png`, `triptych_NN.png`, numbered
sheets) with no stage, state or class in names or metadata. The taker is handed the output folder only, never the key folder.

    plan          capture specification + keys (both outside the repository)
    sc004         trial images + the timing page from raw captures
    sc005         numbered grayscale and CVD sheets from raw tile captures
    sc006         shuffled triptychs from raw captures
    filters       the grayscale and three CVD versions of any PNG (e.g. a trial image or a triptych)
    score-sc004 | score-sc005 | score-sc006 | score-timed    the oracles

Run from the repository root with the art-pipeline environment:
    uv run --frozen --offline --project tools/art-pipeline python tools/art-pipeline/trials/f006/f006_trials.py <command> ...
"""
from __future__ import annotations

import argparse
import contextlib
import json
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
TRIALS = HERE.parent
ROOT = TRIALS.parent
REPO = HERE.parents[3]
for p in (str(ROOT), str(TRIALS)):
    if p not in sys.path:
        sys.path.insert(0, p)

import final_readability as fr  # noqa: E402
import timed_trial as tt  # noqa: E402

SEEDS_PATH = HERE / "seeds.json"
SLOTS = 12
FILTERS = fr.FILTERS

# Capture classes: emuP is the emulator class (adb screencap on AVD IdleFactory_Test_API_36); the others are Windows batch/player captures only.
CAPTURE_CLASSES = {
    "emuP": {"w": 1080, "h": 2400, "dpi": 420.96, "source": "emulator", "layoutClass": "emuPwin"},
    "phoneP": {"w": 720, "h": 1280, "dpi": 320.64, "source": "windows-batch", "layoutClass": "phoneP"},
    "phoneL": {"w": 1280, "h": 720, "dpi": 320.64, "source": "windows-batch", "layoutClass": "phoneL"},
    "tabL": {"w": 2048, "h": 1536, "dpi": 298.72, "source": "windows-batch", "layoutClass": "tabL"},
    "tabP": {"w": 1536, "h": 2048, "dpi": 323.52, "source": "windows-batch", "layoutClass": "tabP"},
}
CLASS_NAMES = list(CAPTURE_CLASSES)

MACHINES = ["miner", "smelter", "press", "assembler"]
# The recipe input that a stopped (missing-input) machine of a type is short of. Verified against the real snapshot at capture time (the capture
# records the AttentionModel shortage of every SC-004 shot; a mismatch with the key blocks the trial before it is shown).
MACHINE_INPUT = {"smelter": "iron.ore", "press": "iron.bar", "assembler": "iron.plate"}
RESOURCES = ["iron.ore", "iron.bar", "iron.plate", "iron.gear"]
CATEGORIES = ["missing-input", "storage-full"]
BENIGN = ["paused", "inactive"]
RESOURCE_BY_CONSUMER = {v: k for k, v in MACHINE_INPUT.items()}


def load_seeds(path: Path = SEEDS_PATH) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def inside_repo(path: Path) -> bool:
    try:
        path.resolve().relative_to(REPO)
        return True
    except ValueError:
        return False


def write_json(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")


# --- SC-004: seeded mixed-state factories ---------------------------------------------------------------------------------

def _slot(cond="empty", machine="", state="", level=0) -> dict:
    return {"cond": cond, "machine": machine, "state": state, "level": level}


def make_sc004_trial(rng: random.Random, cls: str) -> dict:
    """One 12-slot mixed-state factory: a multi-machine stoppage sharing one missing resource (2 to 4 machines of one consumer type), 0 to 2 independent
    storage-full machines, 0 to 2 benign (paused or inactive) machines, running machines, and empty or locked locations."""
    resource = rng.choice(sorted(RESOURCE_BY_CONSUMER))
    consumer = RESOURCE_BY_CONSUMER[resource]
    group = rng.choice([2, 3, 4])
    full = rng.choice([0, 1, 1, 2])
    benign = rng.choice([0, 1, 2])
    others = [m for m in MACHINES if m != consumer]
    running = rng.randint(1, 3)
    occupied = group + full + benign + running
    spare = SLOTS - occupied
    order = list(range(SLOTS))
    rng.shuffle(order)
    slots = [None] * SLOTS
    cursor = 0

    def take(n):
        nonlocal cursor
        got = order[cursor:cursor + n]
        cursor += n
        return got

    group_slots = sorted(take(group))
    for s in group_slots:
        slots[s] = _slot("occupied", consumer, "missing-input", rng.randint(1, 3))
    full_slots = sorted(take(full))
    for s in full_slots:
        slots[s] = _slot("occupied", rng.choice(others), "storage-full", rng.randint(1, 3))
    benign_slots = sorted(take(benign))
    for s in benign_slots:
        slots[s] = _slot("occupied", rng.choice(others), rng.choice(BENIGN), rng.randint(1, 3))
    for s in take(running):
        slots[s] = _slot("occupied", rng.choice(others), "running", rng.randint(1, 3))
    for s in take(spare):
        slots[s] = _slot("locked", level=rng.choice([2, 3, 4, 5])) if rng.random() < 0.5 else _slot("empty")
    stopped = [{"slot": s, "category": "missing-input"} for s in group_slots] + [{"slot": s, "category": "storage-full"} for s in full_slots]
    return {"class": cls, "slots": slots, "stopped": sorted(stopped, key=lambda x: x["slot"]), "sharedResource": resource, "sharedSlots": group_slots,
            "benign": [{"slot": s, "state": slots[s]["state"]} for s in benign_slots]}


def sc004_trials(seed: int, count: int = 10) -> tuple[list[dict], dict]:
    """Ten scored trials (screen classes balanced over the five capture classes, twice each, in a seeded order) and one unscored practice screen from its
    own seeded stream, so the scored trials are identical with or without it."""
    rng = random.Random(seed)
    classes = (CLASS_NAMES * (-(-count // len(CLASS_NAMES))))[:count]
    rng.shuffle(classes)
    trials = []
    for number, cls in enumerate(classes, start=1):
        trial = make_sc004_trial(rng, cls)
        trial["trial"] = number
        trials.append(trial)
    prng = random.Random(seed + load_seeds()["sc004"]["practiceOffset"])
    practice = make_sc004_trial(prng, "emuP")
    practice["trial"] = 0
    return trials, practice


def score_sc004(key: dict, log: dict) -> dict:
    """Score a log {"trials": [{"trial", "stopped": [{"slot", "category"}], "stoppedMs", "resource", "resourceMs"}]} against the key.
    Question A (stopped machines and categories) passes when the named set equals the key exactly, category included, within stoppedLimitSeconds.
    Question B (shared missing resource) passes when the resource equals the key within resourceLimitSeconds. Each needs >= required of the trials."""
    answers = {t["trial"]: t for t in log["trials"]}
    rows = []
    for t in key["trials"]:
        got = answers.get(t["trial"])
        row = {"trial": t["trial"], "class": t["class"], "missing": got is None}
        if got is None:
            row |= {"stoppedCorrect": False, "stoppedInTime": False, "resourceCorrect": False, "resourceInTime": False}
            rows.append(row)
            continue
        named = sorted((int(s["slot"]), s["category"]) for s in got.get("stopped", []))
        expected = sorted((s["slot"], s["category"]) for s in t["stopped"])
        sec_a = got["stoppedMs"] / 1000.0 if got.get("stoppedMs") is not None else None
        sec_b = got["resourceMs"] / 1000.0 if got.get("resourceMs") is not None else None
        row["stoppedCorrect"] = named == expected
        row["stoppedSeconds"] = None if sec_a is None else round(sec_a, 3)
        row["stoppedInTime"] = row["stoppedCorrect"] and sec_a is not None and sec_a <= key["stoppedLimitSeconds"]
        row["resourceCorrect"] = got.get("resource") == t["sharedResource"]
        row["resourceSeconds"] = None if sec_b is None else round(sec_b, 3)
        row["resourceInTime"] = row["resourceCorrect"] and sec_b is not None and sec_b <= key["resourceLimitSeconds"]
        if not row["stoppedCorrect"]:
            row["stoppedDetail"] = {"named": named, "key": expected}
        rows.append(row)
    a = sum(r["stoppedInTime"] for r in rows)
    b = sum(r["resourceInTime"] for r in rows)
    complete = len(rows) == len(key["trials"]) and not any(r["missing"] for r in rows)
    return {"rows": rows, "stoppedInTime": a, "resourceInTime": b, "required": key["required"], "trials": len(key["trials"]),
            "passStopped": complete and a >= key["required"], "passResource": complete and b >= key["required"],
            "pass": complete and a >= key["required"] and b >= key["required"]}


def sc004_key(seed: int) -> dict:
    cfg = load_seeds()["sc004"]
    trials, practice = sc004_trials(seed, cfg["trials"])
    pick = ("trial", "class", "stopped", "sharedResource", "sharedSlots", "benign")
    return {"schema": 1, "seed": seed, "stoppedLimitSeconds": cfg["stoppedLimitSeconds"], "resourceLimitSeconds": cfg["resourceLimitSeconds"],
            "required": cfg["required"], "practice": {k: practice[k] for k in pick},
            "trials": [{k: t[k] for k in pick} | {"file": f"trial_{t['trial']:02d}.png"} for t in trials]}


# --- SC-006: shuffled stage triptychs ------------------------------------------------------------------------------------

STAGE_NAMES = ["Early", "Partial", "Established"]
# Unlocked locations per shipped factory level (12 of 12 at the maximum, u0 = 4): 4, 6 -> Early; 8 -> Partial; 10, 12 -> Established (DevelopmentStage.StageOf).
STAGE_UNLOCKED = {0: [4, 6], 1: [8], 2: [10, 12]}
MIXES = {"sparse": 0.45, "balanced": 0.75, "dense": 1.0}


def stage_of(unlocked: int, total: int = 12, base: int = 4) -> int:
    """Python mirror of DevelopmentStage.StageOf (pure integer arithmetic), kept in step by test_f006_trials.py against the shipped 4/6/8/10/12 table."""
    if total <= base or unlocked < base:
        return 0
    return max(0, min(2, 3 * (unlocked - base) // (total - base)))


def make_sc006_trial(rng: random.Random, cls: str, order: list[int]) -> dict:
    mix = rng.choice(list(MIXES))
    types = [rng.choice(MACHINES) for _ in range(SLOTS)]
    fill = [rng.random() < MIXES[mix] for _ in range(SLOTS)]
    base_level = rng.choice([1, 1, 2])
    stages = []
    for stage in range(3):
        unlocked = rng.choice(STAGE_UNLOCKED[stage])
        slots = []
        for s in range(SLOTS):
            if s >= unlocked:
                slots.append(_slot("locked", level=2 + s // 4))
            elif fill[s]:
                slots.append(_slot("occupied", types[s], "running", min(base_level + stage, 5)))
            else:
                slots.append(_slot("empty"))
        stages.append({"stage": stage, "name": STAGE_NAMES[stage], "unlocked": unlocked, "slots": slots})
    return {"class": cls, "mix": mix, "stages": stages, "presented": order}


def sc006_orders(rng: random.Random, count: int) -> list[list[int]]:
    """Presentation orders: all six permutations first (so every order is hit), then random non-identity ones, all shuffled together."""
    perms = [[a, b, c] for a in range(3) for b in range(3) for c in range(3) if len({a, b, c}) == 3]
    orders = [list(p) for p in perms]
    while len(orders) < count:
        p = list(range(3))
        rng.shuffle(p)
        if p != [0, 1, 2]:
            orders.append(p)
    orders = orders[:count]
    rng.shuffle(orders)
    return orders


def sc006_trials(seed: int, count: int = 10) -> list[dict]:
    rng = random.Random(seed)
    classes = (CLASS_NAMES * (-(-count // len(CLASS_NAMES))))[:count]
    rng.shuffle(classes)
    orders = sc006_orders(rng, count)
    trials = []
    for number, (cls, order) in enumerate(zip(classes, orders), start=1):
        t = make_sc006_trial(rng, cls, order)
        t["trial"] = number
        trials.append(t)
    return trials


def sc006_key(seed: int) -> dict:
    cfg = load_seeds()["sc006"]
    trials = sc006_trials(seed, cfg["trials"])
    # presented[i] is the stage shown in panel A, B, C (i = 0, 1, 2); the correct answer is the panel letters from Early to Established.
    return {"schema": 1, "seed": seed, "required": cfg["required"],
            "trials": [{"trial": t["trial"], "class": t["class"], "mix": t["mix"], "presented": t["presented"],
                        "answer": "".join("ABC"[t["presented"].index(stage)] for stage in range(3)), "file": f"triptych_{t['trial']:02d}.png"} for t in trials]}


def score_sc006(key: dict, log: dict) -> dict:
    """Score a log {"trials": [{"trial", "order": "BAC"}]}: the panel letters from the least to the most developed factory (Early, Partial, Established)."""
    answers = {t["trial"]: str(t.get("order", "")).replace(" ", "").replace(",", "").replace(">", "").upper() for t in log["trials"]}
    rows = []
    for t in key["trials"]:
        got = answers.get(t["trial"])
        rows.append({"trial": t["trial"], "class": t["class"], "mix": t["mix"], "named": got, "key": t["answer"], "correct": got == t["answer"], "missing": got is None})
    correct = sum(r["correct"] for r in rows)
    return {"rows": rows, "correct": correct, "required": key["required"], "trials": len(key["trials"]),
            "pass": len(rows) == len(key["trials"]) and not any(r["missing"] for r in rows) and correct >= key["required"]}


# --- SC-005 final sheets (F004 cue states plus the P3 cues) ----------------------------------------------------------------

P3_CUES = [
    {"id": "p3-upgrade-marker", "cue": "upgrade marker", "keywords": ["upgrade"], "capture": "slot crop of a running machine whose upgrade is affordable"},
    {"id": "p3-attention-shortage", "cue": "shortage marker", "keywords": ["shortage", "missing", "short of", "resource"], "capture": "trunk junction of the first affected row"},
    {"id": "p3-attention-store-full", "cue": "store full marker", "keywords": ["store full", "storage full", "full", "depot"], "capture": "region around the depot"},
    {"id": "p3-ghost-preview", "cue": "ghost preview", "keywords": ["ghost", "preview", "placement"], "capture": "empty location with the build card open and a type chosen"},
    {"id": "p3-hub-dot", "cue": "hub dot", "keywords": ["dot", "attention", "badge"], "capture": "hub entry with an attention flag set"},
    {"id": "p3-hub-no-dot", "cue": "no dot", "keywords": ["no dot", "none", "without", "no attention"], "capture": "hub entry with no attention flag (control)"},
    {"id": "p3-card-next-to-machine", "cue": "machine card", "keywords": ["card"], "capture": "docked machine card beside its selected machine"},
]
P3_BY_ID = {c["id"]: c for c in P3_CUES}


def sc005_tile_definitions() -> list[dict]:
    """The 22 F004 tiles (nine cue states, locks, markers, selection) followed by the P3 cue tiles. Every tile carries the attributes that are its key."""
    tiles = [dict(t) for t in fr.tile_definitions()]
    for cue in P3_CUES:
        tiles.append({"id": cue["id"], "cond": "p3", "machine": "", "state": "", "markers": [], "selected": False, "level": 0, "cue": cue["cue"]})
    return tiles


def norm_cue(text: str) -> str:
    """Map a free-text cue name to a P3 cue id; the longest matching keyword wins so that 'no dot' beats 'dot' and 'store full' beats 'full'."""
    low = text.lower()
    best, best_len = "", 0
    for cue in P3_CUES:
        for kw in cue["keywords"]:
            if kw in low and len(kw) > best_len:
                best, best_len = cue["id"], len(kw)
    return best


def parse_p3_naming(text: str) -> dict[tuple[str, str], dict[int, str]]:
    """The P3 rows of a naming transcript: under the same `## <set> / <filter>` headings, rows with 2 to 5 cells `| number | cue name | ...`."""
    out: dict[tuple[str, str], dict[int, str]] = {}
    current = None
    for line in text.splitlines():
        if line.startswith("## ") and " / " in line:
            name, flt = (x.strip() for x in line[3:].split(" / ", 1))
            current = (name, flt)
            out[current] = {}
        elif current and line.startswith("|"):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if 2 <= len(cells) <= 5 and cells[0].isdigit():
                out[current][int(cells[0])] = norm_cue(cells[1])
    return out


def score_sc005(key: dict, named: dict, p3_named: dict) -> dict:
    """The nine cue states and every F004 attribute are scored by final_readability.score_sc001 on the F004 tiles (binding: 9/9 per filter); the P3 cues are scored
    by id (binding for T546 as well: every P3 cue named). `pass` needs both for every filter."""
    base_key = {"set": key["set"], "filters": {f: [c for c in cells if "cue" not in c] for f, cells in key["filters"].items()}}
    result = fr.score_sc001(base_key, named)
    result["p3"] = {}
    for flt, cells in key["filters"].items():
        rows = p3_named.get((key["set"], flt), {})
        wrong = []
        total = 0
        for c in cells:
            if "cue" not in c:
                continue
            total += 1
            got = rows.get(c["number"])
            if got != c["id"]:
                wrong.append({"tile": c["number"], "id": c["id"], "named": got})
        result["p3"][flt] = {"tiles": total, "correct": total - len(wrong), "confusions": wrong}
    result["passP3"] = all(v["correct"] == v["tiles"] for v in result["p3"].values())
    result["pass"] = result["passGateFields"] and result["passP3"]
    return result


def build_sc005(raw_dir: Path, out_dir: Path, keys_dir: Path) -> dict:
    """Raw layout: <raw>/<set>/<tile id>.png for every sc005 tile. Sheets and meta go to <out>/<set>/, the key to <keys>/sc005-<set>-key.json."""
    cfg = load_seeds()["sc005"]
    tiles_by_id = {t["id"]: t for t in sc005_tile_definitions()}
    summary = {}
    for index, name in enumerate(cfg["sets"]):
        set_dir = raw_dir / name
        if not set_dir.is_dir():
            raise SystemExit(f"missing raw set folder {set_dir}")
        missing = [tid for tid in tiles_by_id if not (set_dir / f"{tid}.png").exists()]
        if missing:
            raise SystemExit(f"set {name}: missing tiles {missing}")
        tiles = {tid: Image.open(set_dir / f"{tid}.png").convert("RGB") for tid in tiles_by_id}
        seed = cfg["seedBase"] + 10 * index
        orders = fr.build_sheets(tiles, seed, out_dir / name)
        meta = {"set": name, "tileCount": len(tiles), "columns": fr.SC001_COLUMNS, "filters": FILTERS, "seed": seed,
                "shuffle": "per filter: random.Random(seed + filter index).shuffle(sorted tile ids)"}
        write_json(out_dir / name / "meta.json", meta)
        key = {"schema": 3, "set": name, "seed": seed, "columns": fr.SC001_COLUMNS,
               "filters": {mode: [{"number": i + 1} | {k: tiles_by_id[tid][k] for k in ("id", "cond", "machine", "state", "markers", "selected", "level")}
                                  | ({"cue": tiles_by_id[tid]["cue"]} if "cue" in tiles_by_id[tid] else {}) for i, tid in enumerate(order)]
                           for mode, order in orders.items()}}
        write_json(keys_dir / f"sc005-{name}-key.json", key)
        summary[name] = {"tiles": len(tiles), "seed": seed}
    return summary


def timed_key(variant: str) -> dict:
    cfg = load_seeds()["sc005"]
    seed = cfg["timedSeeds"][variant]
    trials, practice = fr.sc002_trials(seed)
    return {"schema": 2, "set": variant, "seed": seed, "limitSeconds": cfg["timedLimitSeconds"], "requiredCorrect": cfg["timedRequired"],
            "medianMaxSeconds": cfg["timedMedianMaxSeconds"],
            "practice": {k: practice[k] for k in ("problemSlot", "problemState", "problemMachine", "distractors")},
            "trials": [{k: t[k] for k in ("trial", "problemSlot", "problemState", "problemMachine", "distractors")} | {"file": f"trial_{t['trial']:02d}.png"} for t in trials]}


def score_timed(key: dict, log: dict) -> dict:
    """The SC-005 timed cue trial: fr.score rows, then the binding rule: 10/10 correct within 2.0 s and a median of the correct times of at most 1.077 s."""
    result = fr.score(key, log)
    result["medianMaxSeconds"] = key["medianMaxSeconds"]
    median = result["medianCorrectSeconds"]
    result["medianOk"] = median is not None and median <= key["medianMaxSeconds"]
    result["pass"] = result["pass"] and result["medianOk"]
    return result


@contextlib.contextmanager
def f006_timed_seeds():
    """The F004 SC-002 builders read the module constant SC002_SEEDS; swap in the F006 seeds for the call, then restore (F004 behaviour unchanged)."""
    saved = dict(fr.SC002_SEEDS)
    fr.SC002_SEEDS.clear()
    fr.SC002_SEEDS.update(load_seeds()["sc005"]["timedSeeds"])
    try:
        yield
    finally:
        fr.SC002_SEEDS.clear()
        fr.SC002_SEEDS.update(saved)


# --- capture plan ---------------------------------------------------------------------------------------------------------

def build_plan(spec_path: Path, keys_dir: Path) -> dict:
    """The capture specification (it contains the answers: work folder only) and every key. Jobs name the build source per class: the emulator class by adb
    screencap on AVD IdleFactory_Test_API_36 from the shipped APK, the Windows classes by the batch/player capture path, never an interactive session."""
    seeds = load_seeds()
    jobs = []
    t4, p4 = sc004_trials(seeds["sc004"]["seed"], seeds["sc004"]["trials"])
    for t in [p4] + t4:
        cls = CAPTURE_CLASSES[t["class"]]
        jobs.append({"id": "sc004-practice" if t["trial"] == 0 else f"sc004-{t['trial']:02d}", "trial": "sc004", "class": t["class"], **{k: cls[k] for k in ("w", "h", "dpi", "source")},
                     "factoryLevel": 5, "slots": t["slots"], "expectShortage": t["sharedResource"], "record": ["image", "geometry", "attention-model"]})
    for t in sc006_trials(seeds["sc006"]["seed"], seeds["sc006"]["trials"]):
        cls = CAPTURE_CLASSES[t["class"]]
        for st in t["stages"]:
            jobs.append({"id": f"sc006-{t['trial']:02d}-s{st['stage']}", "trial": "sc006", "class": t["class"], **{k: cls[k] for k in ("w", "h", "dpi", "source")},
                         "factoryUnlocked": st["unlocked"], "expectStage": st["stage"], "mix": t["mix"], "slots": st["slots"], "record": ["image", "geometry", "development-stage"]})
    for name in seeds["sc005"]["sets"]:
        cls = fr.CLASSES[name]
        src = CAPTURE_CLASSES.get(name, {"source": "windows-batch"})["source"]
        for tile in sc005_tile_definitions():
            shot = fr.tile_shot(tile, cls["slot"]) if "cue" not in tile else {"id": tile["id"], "p3": P3_BY_ID[tile["id"]]["capture"]}
            jobs.append({"id": f"sc005-{name}-{tile['id']}", "trial": "sc005", "set": name, "class": name, "w": cls["w"], "h": cls["h"], "dpi": cls["dpi"], "source": src, "shot": shot})
    keys_dir.mkdir(parents=True, exist_ok=True)
    write_json(keys_dir / "sc004-key.json", sc004_key(seeds["sc004"]["seed"]))
    write_json(keys_dir / "sc006-key.json", sc006_key(seeds["sc006"]["seed"]))
    for variant in seeds["sc005"]["timedSeeds"]:
        write_json(keys_dir / f"sc005-timed-{variant}-key.json", timed_key(variant))
    write_json(spec_path, {"schema": 1, "jobs": jobs})
    by_trial = {}
    for j in jobs:
        by_trial[j["trial"]] = by_trial.get(j["trial"], 0) + 1
    return {"jobs": len(jobs), "perTrial": by_trial}


# --- sheet builders -------------------------------------------------------------------------------------------------------

def _neutral(img: Image.Image) -> Image.Image:
    """Copy through raw pixels so no PNG text chunks or other metadata of the capture tooling travel with the image."""
    out = Image.new("RGB", img.size)
    out.putdata(list(img.convert("RGB").getdata()))
    return out


def build_sc006(raw_dir: Path, out_dir: Path, keys_dir: Path) -> dict:
    """Raw: <raw>/sc006-<NN>-s<stage>.png. Output: triptych_NN.png (panels A, B, C in the seeded order, a letter chip on each) and trials.json (file list only)."""
    key = json.loads((keys_dir / "sc006-key.json").read_text(encoding="utf-8"))
    out_dir.mkdir(parents=True, exist_ok=True)
    files = []
    for t in key["trials"]:
        panels = []
        for stage in t["presented"]:
            path = raw_dir / f"sc006-{t['trial']:02d}-s{stage}.png"
            if not path.exists():
                raise SystemExit(f"missing capture {path}")
            panels.append(_neutral(Image.open(path)))
        h = max(p.height for p in panels)
        gap = 12
        sheet = Image.new("RGB", (sum(p.width for p in panels) + gap * (len(panels) + 1), h + 2 * gap), fr.BACKGROUND)
        draw = ImageDraw.Draw(sheet)
        x = gap
        for letter, panel in zip("ABC", panels):
            sheet.paste(panel, (x, gap))
            draw.rectangle([x, gap, x + 40, gap + 30], fill=(0, 0, 0))
            draw.text((x + 12, gap + 6), letter, fill=(255, 255, 255), font=fr._font(18))
            x += panel.width + gap
        sheet.save(out_dir / t["file"], format="PNG", compress_level=9)
        files.append(t["file"])
    write_json(out_dir / "trials.json", {"files": files, "panels": ["A", "B", "C"],
                                         "task": "Order the three panels from the least to the most developed factory; answer as three letters, e.g. BAC."})
    return {"triptychs": len(files)}


def render_sc004_page(data: dict) -> str:
    return SC004_HTML.replace("__DATA__", json.dumps(data))


def build_sc004(raw_dir: Path, out_dir: Path, keys_dir: Path) -> dict:
    """Raw: <raw>/sc004-<NN>.png (and sc004-practice.png) with an optional <same>.json {w, h, n, centres, lift} recorded by the capture. Without it the slot centres
    come from trial_layouts.json (nmin) for the trial's class. Output: trial_NN.png, practice.png and trial.html; the page embeds slot centres but no answers."""
    key = json.loads((keys_dir / "sc004-key.json").read_text(encoding="utf-8"))
    layouts = json.loads((TRIALS / "trial_layouts.json").read_text(encoding="utf-8"))
    out_dir.mkdir(parents=True, exist_ok=True)

    def geometry(stem: str, cls: str, size: tuple[int, int]) -> dict:
        meta_path = raw_dir / f"{stem}.json"
        if meta_path.exists():
            m = json.loads(meta_path.read_text(encoding="utf-8"))
            return {"w": m["w"], "h": m["h"], "centres": m["centres"], "lift": m.get("lift", 12 * m.get("n", 1))}
        lay = next(L for L in layouts if L["class"] == CAPTURE_CLASSES[cls]["layoutClass"] and L["mode"] == "nmin")
        return {"w": lay["w"], "h": lay["h"], "centres": lay["slots"], "lift": 12 * lay["n"]}

    def put(stem: str, name: str, cls: str) -> dict:
        path = raw_dir / f"{stem}.png"
        if not path.exists():
            raise SystemExit(f"missing capture {path}")
        img = _neutral(Image.open(path))
        img.save(out_dir / name, format="PNG", compress_level=9)
        return {"file": name} | geometry(stem, cls, img.size)

    entries = [put(f"sc004-{t['trial']:02d}", t["file"], t["class"]) for t in key["trials"]]
    practice = put("sc004-practice", "practice.png", "emuP")
    write_json(out_dir / "trials.json", {"practice": practice, "trials": entries, "categories": CATEGORIES, "resources": RESOURCES})
    (out_dir / "trial.html").write_text(render_sc004_page({"practice": practice, "trials": entries, "categories": CATEGORIES, "resources": RESOURCES}), encoding="utf-8", newline="\n")
    return {"trials": len(entries)}


def apply_filters(src: Path, out_dir: Path) -> list[str]:
    """Grayscale and the three colour-vision filters of one PNG, `<stem>_<filter>.png` (the same transforms as the F004 sheets)."""
    base = Image.open(src).convert("RGB")
    out_dir.mkdir(parents=True, exist_ok=True)
    names = []
    for mode in FILTERS:
        name = f"{src.stem}_{mode}.png"
        fr._filtered(base, mode).save(out_dir / name, format="PNG", compress_level=9)
        names.append(name)
    return names


SC004_HTML = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>SC-004 problems at a glance</title>
<style>
 body{margin:0;background:#16161c;color:#e8e8ee;font:16px system-ui,sans-serif;text-align:center}
 #stage{display:inline-block;position:relative;margin-top:4px}
 #shot{display:block;height:84vh;max-width:100vw;object-fit:contain;cursor:crosshair}
 .tag{position:absolute;transform:translate(-50%,-50%);background:#000c;color:#fff;font-size:13px;padding:2px 6px;border-radius:4px;pointer-events:none}
 #panel,#bar{padding:14px} button{font:inherit;padding:8px 16px;margin:4px} textarea{width:90%;height:8em}
</style></head><body>
<div id="panel"><p id="msg"></p><button id="go">Start practice</button></div>
<div id="stage" hidden><img id="shot" alt="factory screen" draggable="false"></div>
<div id="bar" hidden><span>Click each stopped machine: one click = missing input, two = storage full, three = clear.</span><br>
<button id="doneA">Stopped machines named (Enter)</button><span id="resBtns" hidden></span></div>
<div id="result" hidden></div>
<script>
const D=__DATA__; const $=id=>document.getElementById(id); let i=-1,t0=0,live=false,marks={},tA=null,tB=null,res=null,cur=null; const log=[];
$("msg").textContent="Practice first (not scored). Then "+D.trials.length+" timed screens. Name the stopped machines and their problem, then the shared missing resource.";
function entry(){return i<0?D.practice:D.trials[i];}
function nearest(e,x,y){let b=0,bd=1e18;e.centres.forEach((c,j)=>{const d=(c[0]-x)**2+(c[1]-e.lift-y)**2;if(d<bd){bd=d;b=j;}});return b;}
function tags(){document.querySelectorAll(".tag").forEach(t=>t.remove());const e=entry(),r=$("shot").getBoundingClientRect(),k=r.width/e.w;
 Object.keys(marks).forEach(s=>{const c=e.centres[s],d=document.createElement("div");d.className="tag";d.textContent=marks[s]==="missing-input"?"missing input":"storage full";
  d.style.left=(c[0]*k)+"px";d.style.top=((c[1]-e.lift)*k)+"px";$("stage").appendChild(d);});}
function show(){$("panel").hidden=true;$("result").hidden=true;marks={};tA=tB=res=null;$("resBtns").hidden=true;$("doneA").hidden=false;live=false;const im=$("shot");
 im.onload=()=>requestAnimationFrame(()=>{$("stage").hidden=false;$("bar").hidden=false;t0=performance.now();live=true;tags();});im.src=entry().file;}
$("go").onclick=show;
$("shot").addEventListener("click",ev=>{if(!live||tA!==null)return;const e=entry(),r=$("shot").getBoundingClientRect(),k=e.w/r.width,s=nearest(e,(ev.clientX-r.left)*k,(ev.clientY-r.top)*k);
 const cur=marks[s];if(!cur)marks[s]="missing-input";else if(cur==="missing-input")marks[s]="storage-full";else delete marks[s];tags();});
function doneA(){if(!live||tA!==null)return;tA=performance.now()-t0;$("doneA").hidden=true;const box=$("resBtns");box.hidden=false;box.innerHTML="Shared missing resource: ";
 D.resources.concat(["none"]).forEach(rname=>{const b=document.createElement("button");b.textContent=rname==="none"?"no shared resource":rname;b.onclick=()=>pick(rname);box.appendChild(b);});}
$("doneA").onclick=doneA;addEventListener("keydown",ev=>{if(ev.key==="Enter")doneA();});
function pick(rname){if(tB!==null)return;tB=performance.now()-t0;res=rname;live=false;const stopped=Object.keys(marks).map(s=>({slot:+s,category:marks[s]})).sort((a,b)=>a.slot-b.slot);
 if(i>=0)log.push({trial:i+1,stopped:stopped,stoppedMs:Math.round(tA*10)/10,resource:res,resourceMs:Math.round(tB*10)/10});
 i++;$("stage").hidden=true;$("bar").hidden=true;
 if(i<D.trials.length){$("panel").hidden=false;$("msg").textContent=(i===0?"Practice recorded. ":"Recorded. ")+"Ready for the next one.";$("go").textContent="Start trial "+(i+1);}else finish();}
function finish(){const out={trials:log,env:{dpr:devicePixelRatio,innerWidth:innerWidth,innerHeight:innerHeight,ua:navigator.userAgent,finished:new Date().toISOString()}};
 const txt=JSON.stringify(out,null,2),res=$("result");res.hidden=false;res.innerHTML="<h2>Done</h2><button id='save'>Save log</button><br><textarea readonly></textarea>";
 res.querySelector("textarea").value=txt;$("save").onclick=()=>{const a=document.createElement("a");a.href=URL.createObjectURL(new Blob([txt],{type:"application/json"}));a.download="sc004-log.json";a.click();};}
</script></body></html>
"""


# --- CLI ------------------------------------------------------------------------------------------------------------------

def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _need_outside(*paths: Path) -> None:
    for p in paths:
        if inside_repo(p):
            raise SystemExit(f"{p} lies inside the repository: the capture spec and the answer keys must stay outside it (blinding)")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("plan")
    p.add_argument("--spec", type=Path, required=True)
    p.add_argument("--keys", type=Path, required=True)
    for name in ("sc004", "sc005", "sc006"):
        q = sub.add_parser(name)
        q.add_argument("--raw", type=Path, required=True)
        q.add_argument("--out", type=Path, required=True)
        q.add_argument("--keys", type=Path, required=True)
    t = sub.add_parser("timed", help="the SC-005 timed cue trial pages from raw sc002-<variant> captures (the F004 builder with the F006 seeds)")
    t.add_argument("--raw", type=Path, required=True)
    t.add_argument("--out", type=Path, required=True)
    t.add_argument("--keys", type=Path, required=True)
    f = sub.add_parser("filters")
    f.add_argument("--image", type=Path, nargs="+", required=True)
    f.add_argument("--out", type=Path, required=True)
    for name in ("score-sc004", "score-sc006", "score-timed"):
        s = sub.add_parser(name)
        s.add_argument("--key", type=Path, required=True)
        s.add_argument("--log", type=Path, required=True)
        s.add_argument("--json", type=Path, default=None)
    n = sub.add_parser("score-sc005")
    n.add_argument("--keys", type=Path, required=True)
    n.add_argument("--naming", type=Path, nargs="+", required=True)
    n.add_argument("--json", type=Path, default=None)
    args = ap.parse_args(argv)

    if args.cmd == "plan":
        _need_outside(args.spec, args.keys)
        result = build_plan(args.spec, args.keys)
    elif args.cmd in ("sc004", "sc005", "sc006", "timed"):
        _need_outside(args.keys)
        if args.cmd == "timed":
            with f006_timed_seeds():
                result = fr.build_sc002(args.raw, args.out, args.keys)
        else:
            result = {"sc004": build_sc004, "sc005": build_sc005, "sc006": build_sc006}[args.cmd](args.raw, args.out, args.keys)
    elif args.cmd == "filters":
        result = {img.name: apply_filters(img, args.out) for img in args.image}
    elif args.cmd == "score-sc005":
        named, p3 = {}, {}
        for path in args.naming:
            text = path.read_text(encoding="utf-8")
            named |= fr.parse_naming(text)
            p3 |= parse_p3_naming(text)
        result = {"sets": [score_sc005(_load(k), named, p3) for k in sorted(args.keys.glob("sc005-*-key.json")) if "timed" not in k.name]}
        result["pass"] = bool(result["sets"]) and all(s["pass"] for s in result["sets"])
    else:
        scorer = {"score-sc004": score_sc004, "score-sc006": score_sc006, "score-timed": score_timed}[args.cmd]
        result = scorer(_load(args.key), _load(args.log))
    text = json.dumps(result, indent=2)
    if getattr(args, "json", None):
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(text + "\n", encoding="utf-8", newline="\n")
    print(text)
    return 0 if result.get("pass", True) is not False else 1


if __name__ == "__main__":
    raise SystemExit(main())
