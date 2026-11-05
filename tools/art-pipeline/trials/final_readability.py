"""Final readability evidence for Feature 004 (T445, SC-001 and SC-002) from in-game captures of the shipped build.

Three stages, run from the repository root with the art-pipeline project environment:

    plan         write the capture specification (one JSON, consumed by a temporary Play Mode capture test) and the SC-002 answer keys
    sc001        build the shuffled grayscale and colour-vision sheets from the captured tiles; the answer keys go outside the repository
    sc002        build the clickable timed trials (standard with motion, reduced-motion variant) from the captured screens; keys outside the repository
    score        score an owner log against an SC-002 key (per-state breakdown, 9 of 10 within 2.0 s)

The capture test itself (a temporary Play Mode class driving the production `SlotGridView`, `FactoryCameraController` and shell at named device
classes) is never committed; `specs/004-visual-identity/reviews/readability-final.md` records its provenance. Everything downstream of the raw
captures lives here and is tested in `tests/test_final_readability.py`.

Blinding: `sc001` and `sc002` write the answer keys only to the folder given by `--keys`, which must not lie inside the repository. The author of the
sets never opens them; the independent reviewer and the owner never see them.
"""
from __future__ import annotations

import argparse
import json
import random
import statistics
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[0]
REPO = HERE.parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(HERE))

from art_pipeline import checks  # noqa: E402
import timed_trial as tt  # noqa: E402

BACKGROUND = (40, 40, 48)
MACHINES = ["miner", "smelter", "press", "assembler"]
STATES = ["running", "waiting", "missing-input", "storage-full", "paused", "inactive"]
PROBLEM_STATES = tt.PROBLEM_STATES
DISTRACTOR_STATES = tt.DISTRACTOR_STATES
SLOTS = 12
LIMIT_SECONDS = tt.LIMIT_SECONDS
REQUIRED_CORRECT = tt.REQUIRED_CORRECT
FILTERS = ["grayscale", "deuteranopia", "protanopia", "tritanopia"]
SC001_COLUMNS = 5
CANVAS_W, CANVAS_H = 80, 64  # art px of a slot canvas; the tile is one canvas cropped around the slot pivot (40, 15 from the bottom)

# The screen classes of the sheets: name -> facts passed to the capture test (device pixel size, density, HUD layout class).
CLASSES = {
    "emuP": {"w": 1080, "h": 2400, "dpi": 420.0, "layout": "CompactPortrait", "label": "phone portrait 1080x2400, 420 dpi (emulator class)", "slot": 4},
    "phoneP": {"w": 720, "h": 1280, "dpi": 320.0, "layout": "CompactPortrait", "label": "phone portrait 720x1280, 320 dpi (DEV-2 class since owner decision F1)", "slot": 4},
    "phoneL": {"w": 1280, "h": 720, "dpi": 320.0, "layout": "CompactLandscape", "label": "phone landscape 1280x720, 320 dpi (DEV-2/DEV-4 class)", "slot": 5},
    "tabL": {"w": 2048, "h": 1536, "dpi": 264.0, "layout": "TabletLandscape", "label": "tablet landscape 2048x1536, 264 dpi", "slot": 5},
}
MODES = ["nmin", "fit"]
SC001_SEED_BASE = 4450
SC002_SEEDS = {"standard": 4461, "reduced-motion": 4462}
PRACTICE_SEED_OFFSET = 100000


def slot_spec(cond: str = "empty", machine: str = "", state: str = "", boosted: bool = False, overdrive: bool = False, selected: bool = False,
              level: int = 0, hidden: bool = False) -> dict:
    return {"cond": cond, "machine": machine, "state": state, "boosted": boosted, "overdrive": overdrive, "selected": selected, "level": level, "hidden": hidden}


def tile_definitions() -> list[dict]:
    """The 22 tiles of every SC-001 set: attributes are the answer key. State, slot condition, marker and selection are four independent attributes."""
    tiles = []
    for machine in ("miner", "assembler"):
        for state in STATES:
            tiles.append({"id": f"{machine}-{state}", "cond": "occupied", "machine": machine, "state": state, "markers": [], "selected": False, "level": 0})
    tiles.append({"id": "plate-empty", "cond": "empty", "machine": "", "state": "", "markers": [], "selected": False, "level": 0})
    for level in (2, 3, 4, 5):
        tiles.append({"id": f"plate-locked-L{level}", "cond": "locked", "machine": "", "state": "", "markers": [], "selected": False, "level": level})
    tiles.append({"id": "miner-running-boosted", "cond": "occupied", "machine": "miner", "state": "running", "markers": ["boosted"], "selected": False, "level": 0})
    tiles.append({"id": "miner-running-overdrive", "cond": "occupied", "machine": "miner", "state": "running", "markers": ["overdrive"], "selected": False, "level": 0})
    tiles.append({"id": "assembler-running-both-markers", "cond": "occupied", "machine": "assembler", "state": "running", "markers": ["boosted", "overdrive"],
                  "selected": False, "level": 0})
    tiles.append({"id": "assembler-running-selected", "cond": "occupied", "machine": "assembler", "state": "running", "markers": [], "selected": True, "level": 0})
    tiles.append({"id": "plate-empty-selected", "cond": "empty", "machine": "", "state": "", "markers": [], "selected": True, "level": 0})
    return tiles


def tile_slot(tile: dict, hidden: bool = False) -> dict:
    return slot_spec(tile["cond"], tile["machine"], tile["state"], "boosted" in tile["markers"], "overdrive" in tile["markers"], tile["selected"], tile["level"], hidden)


def tile_shot(tile: dict, target: int) -> dict:
    slots = [slot_spec(hidden=True) for _ in range(SLOTS)]
    slots[target] = tile_slot(tile)
    return {"id": tile["id"], "slots": slots, "cropSlot": target, "crop": True, "composite": False, "frames": 1, "frozen": False}


def make_trial(rng: random.Random, problem_state: str) -> dict:
    """One 12-slot screen: one problem machine, one or two benign distractors, the rest running machines (80 %), empty (10 %) and locked (10 %) plates."""
    order = list(range(SLOTS))
    rng.shuffle(order)
    problem = order[0]
    distractor_count = rng.choice([1, 2])
    distractors = order[1 : 1 + distractor_count]
    slots: list[dict | None] = [None] * SLOTS
    slots[problem] = slot_spec("occupied", rng.choice(MACHINES), problem_state)
    distractor_states = []
    for slot in distractors:
        state = rng.choice(DISTRACTOR_STATES)
        slots[slot] = slot_spec("occupied", rng.choice(MACHINES), state)
        distractor_states.append(state)
    for slot in order[1 + distractor_count :]:
        kind = rng.choices(["run", "empty", "locked"], weights=[8, 1, 1])[0]
        if kind == "run":
            slots[slot] = slot_spec("occupied", rng.choice(MACHINES), "running")
        elif kind == "empty":
            slots[slot] = slot_spec("empty")
        else:
            slots[slot] = slot_spec("locked", level=rng.choice([2, 3, 4, 5]))
    return {"problemSlot": problem, "problemState": problem_state, "problemMachine": slots[problem]["machine"],
            "distractors": [{"slot": s, "state": st} for s, st in zip(distractors, distractor_states)], "slots": slots}


def sc002_trials(seed: int) -> tuple[list[dict], dict]:
    """Ten scored trials (problem states balanced 4/3/3 in a seeded order) and one unscored practice screen from its own seeded stream."""
    rng = random.Random(seed)
    trials = []
    for number, state in enumerate(tt.problem_sequence(rng), start=1):
        trial = make_trial(rng, state)
        trial["trial"] = number
        trials.append(trial)
    prng = random.Random(seed + PRACTICE_SEED_OFFSET)
    practice = make_trial(prng, prng.choice(PROBLEM_STATES))
    practice["trial"] = 0
    return trials, practice


def build_plan(spec_path: Path, keys_dir: Path) -> dict:
    """The capture specification. It contains the answers, so it belongs in the work folder, never in the repository."""
    jobs = []
    for name, cls in CLASSES.items():
        for mode in MODES:
            shots = [tile_shot(tile, cls["slot"]) for tile in tile_definitions()]
            jobs.append({"id": f"sc001-{name}-{mode}", "w": cls["w"], "h": cls["h"], "dpi": cls["dpi"], "layout": cls["layout"], "mode": mode, "shots": shots})
    emu = CLASSES["emuP"]
    summary = {}
    for variant, seed in SC002_SEEDS.items():
        trials, practice = sc002_trials(seed)
        reduced = variant == "reduced-motion"
        shots = []
        for trial in [practice] + trials:
            shots.append({"id": "practice" if trial["trial"] == 0 else f"trial_{trial['trial']:02d}", "slots": trial["slots"], "cropSlot": 0, "crop": False,
                          "composite": True, "frames": 1 if reduced else 6, "frozen": reduced})
        jobs.append({"id": f"sc002-{variant}", "w": emu["w"], "h": emu["h"], "dpi": emu["dpi"], "layout": emu["layout"], "mode": "nmin", "shots": shots})
        keys_dir.mkdir(parents=True, exist_ok=True)
        key = {"schema": 2, "set": variant, "seed": seed, "limitSeconds": LIMIT_SECONDS, "requiredCorrect": REQUIRED_CORRECT,
               "practice": {k: practice[k] for k in ("problemSlot", "problemState", "problemMachine", "distractors")},
               "trials": [{k: t[k] for k in ("trial", "problemSlot", "problemState", "problemMachine", "distractors")} | {"file": f"trial_{t['trial']:02d}.png"} for t in trials]}
        (keys_dir / f"sc002-{variant}-key.json").write_text(json.dumps(key, indent=2) + "\n", encoding="utf-8", newline="\n")
        summary[variant] = {"seed": seed, "trials": len(trials)}
    spec_path.parent.mkdir(parents=True, exist_ok=True)
    spec_path.write_text(json.dumps({"jobs": jobs}) + "\n", encoding="utf-8", newline="\n")
    return {"jobs": len(jobs), "sc002": summary}


# --- SC-001 sheets -------------------------------------------------------------------------------------------------

def _font(size: int):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # very old Pillow without the size argument
        return ImageFont.load_default()


def _filtered(base: Image.Image, mode: str) -> Image.Image:
    cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    out = Image.new("RGB", base.size)
    data = []
    for px in base.getdata():
        got = cache.get(px)
        if got is None:
            got = checks._transform(px, mode)
            cache[px] = got
        data.append(got)
    out.putdata(data)
    return out


def shuffled_order(ids: list[str], seed: int) -> list[str]:
    order = sorted(ids)
    random.Random(seed).shuffle(order)
    return order


def filter_seed(seed: int, filter_name: str) -> int:
    return seed + FILTERS.index(filter_name)


def build_sheets(tiles: dict[str, Image.Image], seed: int, out_dir: Path, columns: int = SC001_COLUMNS) -> dict[str, list[str]]:
    """Numbered contact sheets, one per filter, each with its own seeded shuffle (`seed` + the filter's index), so a reviewer cannot carry tile numbers
    from one filter to the next. Tiles keep device pixels (scale 1); numbers are drawn after the filter. Returns the tile order per filter."""
    tw = max(t.width for t in tiles.values())
    th = max(t.height for t in tiles.values())
    gap, header = 6, 22
    count = len(tiles)
    rows = -(-count // columns)
    cell_w, cell_h = tw + gap, th + header + gap
    positions = [(gap + (i % columns) * cell_w, gap + (i // columns) * cell_h) for i in range(count)]
    out_dir.mkdir(parents=True, exist_ok=True)
    font = _font(17)
    orders = {}
    for mode in FILTERS:
        order = shuffled_order(list(tiles), filter_seed(seed, mode))
        orders[mode] = order
        base = Image.new("RGB", (columns * cell_w + gap, rows * cell_h + gap), BACKGROUND)
        for name, (x, y) in zip(order, positions):
            base.paste(tiles[name].convert("RGB"), (x, y + header))
        sheet = _filtered(base, mode)
        draw = ImageDraw.Draw(sheet)
        for i, (x, y) in enumerate(positions):
            draw.rectangle([x, y, x + 34, y + header - 2], fill=(0, 0, 0))
            draw.text((x + 4, y), str(i + 1), fill=(255, 255, 255), font=font)
        sheet.save(out_dir / f"sheet_{mode}.png", format="PNG", compress_level=9)
    return orders


def set_name(cls: str, scale: int) -> str:
    c = CLASSES[cls]
    return f"{cls}-{c['w']}x{c['h']}-n{scale}"


def collect_sets(raw_dir: Path) -> dict[str, dict]:
    """Group the captured sc001 jobs into sets: one per (class, n). A class whose n_min equals the fit-all n yields one set for both zooms."""
    sets: dict[str, dict] = {}
    for cls in CLASSES:
        for mode in MODES:
            job_dir = raw_dir / f"sc001-{cls}-{mode}"
            info_path = job_dir / "job.json"
            if not info_path.exists():
                continue
            info = json.loads(info_path.read_text(encoding="utf-8"))
            name = set_name(cls, info["scale"])
            entry = sets.setdefault(name, {"class": cls, "n": info["scale"], "zooms": [], "dir": job_dir, "info": info})
            entry["zooms"].append("n_min" if mode == "nmin" else "fit-all")
    return sets


def build_sc001(raw_dir: Path, out_dir: Path, keys_dir: Path) -> dict:
    sets = collect_sets(raw_dir)
    if not sets:
        raise SystemExit(f"no sc001 captures in {raw_dir}")
    tiles_by_id = {t["id"]: t for t in tile_definitions()}
    summary = {}
    for index, name in enumerate(sorted(sets)):
        entry = sets[name]
        tiles = {tid: Image.open(entry["dir"] / f"{tid}_f0.png").convert("RGB") for tid in tiles_by_id}
        seed = SC001_SEED_BASE + 10 * index
        orders = build_sheets(tiles, seed, out_dir / name)
        cls = CLASSES[entry["class"]]
        info = entry["info"]
        tw, th = next(iter(tiles.values())).size
        meta = {"set": name, "class": cls["label"], "n": entry["n"], "zooms": sorted(entry["zooms"]), "tileCount": len(tiles), "tilePx": [tw, th],
                "tileArtCanvas": [CANVAS_W, CANVAS_H], "columns": SC001_COLUMNS, "filters": FILTERS, "pixelsPerPoint": round(info["pixelsPerPoint"], 4),
                "floor": bool(info["hasFloor"]), "belowFloor": bool(info["belowFloor"]), "seed": seed,
                "shuffle": "per filter: random.Random(seed + filter index).shuffle(sorted tile ids), filter order grayscale, deuteranopia, protanopia, tritanopia"}
        (out_dir / name / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8", newline="\n")
        keys_dir.mkdir(parents=True, exist_ok=True)
        key = {"schema": 2, "set": name, "seed": seed, "columns": SC001_COLUMNS,
               "filters": {mode: [{"number": i + 1} | {k: tiles_by_id[tid][k] for k in ("id", "cond", "machine", "state", "markers", "selected", "level")} for i, tid in enumerate(order)]
                           for mode, order in orders.items()}}
        (keys_dir / f"sc001-{name}-key.json").write_text(json.dumps(key, indent=2) + "\n", encoding="utf-8", newline="\n")
        summary[name] = {"n": entry["n"], "zooms": sorted(entry["zooms"]), "tiles": len(tiles), "tilePx": [tw, th], "seed": seed}
    return summary


# --- SC-002 trials -------------------------------------------------------------------------------------------------

def save_shot(frames: list[Image.Image], path: Path, fps: float) -> None:
    """A static PNG for one frame, an animated PNG (APNG, loops forever, browsers play it natively) for several."""
    rgb = [f.convert("RGB") for f in frames]
    if len(rgb) == 1:
        rgb[0].save(path, format="PNG", compress_level=9, optimize=True)
        return
    delay = max(1, round(1000.0 / fps))
    rgb[0].save(path, format="PNG", save_all=True, append_images=rgb[1:], duration=delay, loop=0, compress_level=9, optimize=True)


def render_html(data: dict) -> str:
    """The clickable page. It embeds the screen size, the slot positions and the practice answer (for on-screen feedback) but no trial answers."""
    return HTML.replace("__DATA__", json.dumps(data))


def build_sc002(raw_dir: Path, out_dir: Path, keys_dir: Path) -> dict:
    emu = CLASSES["emuP"]
    summary = {}
    for variant, seed in SC002_SEEDS.items():
        job_dir = raw_dir / f"sc002-{variant}"
        info = json.loads((job_dir / "job.json").read_text(encoding="utf-8"))
        key = json.loads((keys_dir / f"sc002-{variant}-key.json").read_text(encoding="utf-8"))
        out = out_dir / variant
        out.mkdir(parents=True, exist_ok=True)
        reduced = variant == "reduced-motion"
        shots = {s["id"]: s for s in info["shots"]}
        fps = info["fps"] or 10.0
        files = []
        for trial in key["trials"]:
            shot = shots[f"trial_{trial['trial']:02d}"]
            frames = [Image.open(job_dir / f"{shot['id']}_f{k}.png") for k in range(shot["frames"])]
            save_shot(frames, out / trial["file"], fps)
            files.append(trial["file"])
        pframes = [Image.open(job_dir / f"practice_f{k}.png") for k in range(shots["practice"]["frames"])]
        save_shot(pframes, out / "practice.png", fps)
        centres = [[round(p["x"], 1), round(p["y"], 1)] for p in info["pivots"]]
        data = {"set": variant, "files": files, "w": info["w"], "h": info["h"], "n": info["scale"], "centres": centres, "lift": 12 * info["scale"],
                "practice": {"file": "practice.png", "slot": key["practice"]["problemSlot"]}, "reducedMotion": reduced}
        (out / "trial.html").write_text(render_html(data), encoding="utf-8", newline="\n")
        meta = {"set": variant, "class": emu["label"], "n": info["scale"], "screen": [info["w"], info["h"]], "pixelsPerPoint": round(info["pixelsPerPoint"], 4),
                "motion": "running machines frozen on frame 0 (reduced motion on)" if reduced else f"running machines animate: {info['frameCount']} frames at {fps:g} fps (APNG loop)",
                "seed": seed, "trialCount": len(files), "floor": bool(info["hasFloor"])}
        (out / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8", newline="\n")
        summary[variant] = {"n": info["scale"], "seed": seed, "trials": len(files), "motion": meta["motion"]}
    return summary


HTML = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>SC-002 timed trial (in-game screens)</title>
<style>
 body{margin:0;background:#16161c;color:#e8e8ee;font:16px system-ui,sans-serif;text-align:center}
 #stage{display:inline-block;position:relative;margin-top:4px}
 #shot{display:block;height:96vh;max-width:100vw;object-fit:contain;cursor:crosshair}
 #panel{padding:24px}
 button{font:inherit;padding:10px 22px;margin:8px}
 textarea{width:90%;height:8em}
</style></head><body>
<div id="panel"><p id="msg"></p><button id="go">Start</button></div>
<div id="stage" hidden><img id="shot" alt="factory screen" draggable="false"></div>
<div id="result" hidden></div>
<script>
const D=__DATA__; let i=-1, t0=0, shown=false; const log=[];
const $=id=>document.getElementById(id);
$("msg").textContent="Practice screen first (not scored): find the stuck machine and click it. Then "+D.files.length+" timed screens follow. Click the stuck machine as fast as you can.";
$("go").textContent="Start practice";
function show(){ $("panel").hidden=true; $("result").hidden=true; const im=$("shot"); shown=false;
  im.onload=()=>requestAnimationFrame(()=>{ $("stage").hidden=false; t0=performance.now(); shown=true; });
  im.src=(i<0?D.practice.file:D.files[i]); }
$("go").onclick=show;
function nearest(x,y){ let best=0,bd=1e18; D.centres.forEach((c,j)=>{const d=(c[0]-x)**2+(c[1]-D.lift-y)**2; if(d<bd){bd=d;best=j;}}); return best; }
$("shot").addEventListener("click",ev=>{ if(!shown) return; const dt=performance.now()-t0; shown=false;
  const r=$("shot").getBoundingClientRect(), k=D.w/r.width, x=(ev.clientX-r.left)*k, y=(ev.clientY-r.top)*k, best=nearest(x,y);
  if(i<0){ i=0; $("stage").hidden=true; $("panel").hidden=false; $("go").textContent="Start trial 1";
    $("msg").textContent=(best===D.practice.slot?"Practice: correct":"Practice: wrong machine")+" in "+(dt/1000).toFixed(2)+" s (not scored). The timed trials start next."; return; }
  log.push({trial:i+1,slot:best,ms:Math.round(dt*10)/10,x:Math.round(x),y:Math.round(y)}); i++; $("stage").hidden=true;
  if(i<D.files.length){ $("panel").hidden=false; $("msg").textContent="Recorded. Ready for the next one."; $("go").textContent="Start trial "+(i+1); }
  else finish(); });
function finish(){ const out={set:D.set,variant:D.set,reducedMotion:D.reducedMotion,trials:log,
    env:{dpr:window.devicePixelRatio,innerWidth:innerWidth,innerHeight:innerHeight,shotCssWidth:Math.round($("shot").getBoundingClientRect().width),shotCssHeight:Math.round($("shot").getBoundingClientRect().height),ua:navigator.userAgent,finished:new Date().toISOString()}};
  const txt=JSON.stringify(out,null,2); const res=$("result"); res.hidden=false;
  res.innerHTML="<h2>Done</h2><p>Save the log and give it to the evidence author (see OWNER-RUN.md).</p><button id='save'>Save log</button><br><textarea readonly></textarea>";
  res.querySelector("textarea").value=txt;
  $("save").onclick=()=>{const a=document.createElement("a"); a.href=URL.createObjectURL(new Blob([txt],{type:"application/json"})); a.download="sc002-log-"+D.set+".json"; a.click();}; }
</script></body></html>
"""


# --- scoring -------------------------------------------------------------------------------------------------------

def score(key: dict, log: dict) -> dict:
    """Score a log {"trials": [{"trial", "slot", "ms"}]} against an SC-002 key: correct within the limit, per-state breakdown, median time."""
    answers = {t["trial"]: t for t in log["trials"]}
    rows = []
    for t in key["trials"]:
        got = answers.get(t["trial"])
        if got is None:
            rows.append({"trial": t["trial"], "state": t["problemState"], "machine": t["problemMachine"], "correct": False, "seconds": None, "inTime": False, "missing": True})
            continue
        seconds = got["ms"] / 1000.0
        correct = got["slot"] == t["problemSlot"]
        rows.append({"trial": t["trial"], "state": t["problemState"], "machine": t["problemMachine"], "correct": correct, "seconds": round(seconds, 3),
                     "inTime": correct and seconds <= key["limitSeconds"], "missing": False})
    passed = sum(r["inTime"] for r in rows)
    times = [r["seconds"] for r in rows if r["seconds"] is not None]
    correct_times = [r["seconds"] for r in rows if r["correct"]]
    by_state = {}
    for state in PROBLEM_STATES:
        part = [r for r in rows if r["state"] == state]
        by_state[state] = {"trials": len(part), "correct": sum(r["correct"] for r in part), "inTime": sum(r["inTime"] for r in part),
                           "medianSeconds": round(statistics.median([r["seconds"] for r in part if r["seconds"] is not None]), 3) if any(r["seconds"] is not None for r in part) else None}
    return {"set": key.get("set"), "rows": rows, "correctInTime": passed, "required": key["requiredCorrect"], "trials": len(key["trials"]),
            "pass": passed >= key["requiredCorrect"] and len(rows) == len(key["trials"]), "byState": by_state,
            "medianSeconds": round(statistics.median(times), 3) if times else None,
            "medianCorrectSeconds": round(statistics.median(correct_times), 3) if correct_times else None}


def report_text(result: dict) -> str:
    lines = [f"SC-002 {result['set']}: {result['correctInTime']}/{result['trials']} correct within {LIMIT_SECONDS:g} s (need {result['required']}) -> {'PASS' if result['pass'] else 'FAIL'}",
             f"median {result['medianSeconds']} s (correct trials only: {result['medianCorrectSeconds']} s)", "", "| Trial | Problem state | Machine | Correct | Seconds | In time |", "|---|---|---|---|---|---|"]
    for r in result["rows"]:
        lines.append(f"| {r['trial']} | {r['state']} | {r['machine']} | {'yes' if r['correct'] else 'no'} | {r['seconds']} | {'yes' if r['inTime'] else 'no'} |")
    lines += ["", "| Problem state | Trials | Correct | Correct within limit | Median s |", "|---|---|---|---|---|"]
    for state, v in result["byState"].items():
        lines.append(f"| {state} | {v['trials']} | {v['correct']} | {v['inTime']} | {v['medianSeconds']} |")
    return "\n".join(lines) + "\n"


# --- SC-001 scoring of a blind naming transcript ------------------------------------------------------------------

STATE_NAMES = ["missing input", "storage full", "waiting", "running", "paused", "inactive"]
GATE_FIELDS = ["condition", "lock digit", "state", "markers", "selection"]  # the SC-001 fields; machine type is reported separately


def _norm_state(text: str) -> str:
    low = text.lower().replace("-", " ")
    for name in STATE_NAMES:
        if name in low:
            return name.replace(" ", "-")
    return ""


def _parse_row(cells: list[str]) -> dict:
    cond_text, machine_text, state_text, marker_text, select_text = (c.strip() for c in cells[1:6])
    low = cond_text.lower()
    cond = "empty" if low.startswith("empty") else "locked" if low.startswith("locked") else "occupied" if low.startswith("occupied") else low
    digit = None
    if cond == "locked":
        found = [ch for ch in cond_text if ch.isdigit()]
        digit = int(found[0]) if found else None
    mlow = machine_text.lower()
    machine = next((m for m in ("miner", "assembler", "smelter", "press") if m in mlow), "other" if "other" in mlow else "")
    mk = marker_text.lower()
    if mk.startswith("none") or not mk or mk == "-":
        markers: set[str] = set()
    elif mk.startswith("both"):
        markers = {"boosted", "overdrive"}
    else:
        head = mk.split("(")[0].split(":")[0]
        markers = {m for m in ("boosted", "overdrive") if m in head}
    return {"number": int(cells[0]), "condition": cond, "lock digit": digit, "machine": machine, "state": _norm_state(state_text) if cond == "occupied" else "",
            "markers": sorted(markers), "selection": select_text.lower().startswith("yes")}


def parse_naming(text: str) -> dict[tuple[str, str], dict[int, dict]]:
    """Parse a naming transcript: `## <set> / <filter>` headings, each followed by a table with one row per numbered tile."""
    out: dict[tuple[str, str], dict[int, dict]] = {}
    current = None
    for line in text.splitlines():
        if line.startswith("## ") and " / " in line:
            name, flt = (x.strip() for x in line[3:].split(" / ", 1))
            current = (name, flt)
            out[current] = {}
            continue
        if current and line.startswith("|"):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cells) >= 6 and cells[0].isdigit():
                row = _parse_row(cells)
                out[current][row["number"]] = row
    return out


def score_sc001(key: dict, named: dict[tuple[str, str], dict[int, dict]]) -> dict:
    """Field-by-field comparison per filter. Machine type is reported apart from the gate fields; a named `other` for a key of smelter or press is correct."""
    result = {"set": key["set"], "filters": {}}
    for flt, cells in key["filters"].items():
        rows = named.get((key["set"], flt))
        confusions, ok_all, ok_gate = [], 0, 0
        for cell in cells:
            got = rows.get(cell["number"]) if rows else None
            if got is None:
                confusions.append({"tile": cell["number"], "id": cell["id"], "field": "missing", "named": None, "key": None})
                continue
            bad = []
            if got["condition"] != cell["cond"]:
                bad.append(("condition", got["condition"], cell["cond"]))
            if cell["cond"] == "locked" and got["lock digit"] != cell["level"]:
                bad.append(("lock digit", got["lock digit"], cell["level"]))
            if cell["cond"] == "occupied" and got["state"] != cell["state"]:
                bad.append(("state", got["state"], cell["state"]))
            if got["markers"] != sorted(cell["markers"]):
                bad.append(("markers", got["markers"], sorted(cell["markers"])))
            if got["selection"] != cell["selected"]:
                bad.append(("selection", got["selection"], cell["selected"]))
            machine_bad = cell["cond"] == "occupied" and got["machine"] != cell["machine"] and not (got["machine"] == "other" and cell["machine"] in ("smelter", "press"))
            if machine_bad:
                bad.append(("machine type", got["machine"], cell["machine"]))
            gate_ok = not any(f in GATE_FIELDS for f, _, _ in bad)
            ok_gate += gate_ok
            ok_all += not bad
            confusions += [{"tile": cell["number"], "id": cell["id"], "field": f, "named": n, "key": k} for f, n, k in bad]
        result["filters"][flt] = {"tiles": len(cells), "correctAllFields": ok_all, "correctGateFields": ok_gate, "confusions": confusions}
    result["passAllFields"] = all(v["correctAllFields"] == v["tiles"] for v in result["filters"].values())
    result["passGateFields"] = all(v["correctGateFields"] == v["tiles"] for v in result["filters"].values())
    return result


def _inside_repo(path: Path) -> bool:
    try:
        path.resolve().relative_to(REPO)
        return True
    except ValueError:
        return False


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("plan", help="write the capture specification and the SC-002 keys")
    p.add_argument("--spec", type=Path, required=True)
    p.add_argument("--keys", type=Path, required=True)
    for name in ("sc001", "sc002"):
        q = sub.add_parser(name)
        q.add_argument("--raw", type=Path, required=True, help="folder with the captures (one subfolder per job)")
        q.add_argument("--out", type=Path, required=True)
        q.add_argument("--keys", type=Path, required=True)
    n = sub.add_parser("score-sc001", help="score blind naming transcripts against the SC-001 keys")
    n.add_argument("--keys", type=Path, required=True, help="folder with sc001-<set>-key.json")
    n.add_argument("--naming", type=Path, nargs="+", required=True)
    n.add_argument("--json", type=Path, default=None)
    s = sub.add_parser("score", help="score an owner log (SC-002)")
    s.add_argument("--key", type=Path, required=True)
    s.add_argument("--log", type=Path, required=True)
    s.add_argument("--json", type=Path, default=None, help="also write the result as JSON")
    args = ap.parse_args(argv)
    if args.cmd in ("plan", "sc001", "sc002") and _inside_repo(args.keys):
        raise SystemExit("the keys folder must lie outside the repository")
    if args.cmd == "plan":
        print(json.dumps(build_plan(args.spec, args.keys)))
        return 0
    if args.cmd == "sc001":
        print(json.dumps(build_sc001(args.raw, args.out, args.keys), indent=2))
        return 0
    if args.cmd == "sc002":
        print(json.dumps(build_sc002(args.raw, args.out, args.keys), indent=2))
        return 0
    if args.cmd == "score-sc001":
        named: dict = {}
        for path in args.naming:
            named.update(parse_naming(path.read_text(encoding="utf-8")))
        results = [score_sc001(json.loads(k.read_text(encoding="utf-8")), named) for k in sorted(args.keys.glob("sc001-*-key.json"))]
        for r in results:
            for flt, v in r["filters"].items():
                print(f"{r['set']} {flt}: gate fields {v['correctGateFields']}/{v['tiles']}, all fields {v['correctAllFields']}/{v['tiles']}, confusion entries {len(v['confusions'])}")
        if args.json:
            args.json.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8", newline="\n")
        return 0 if all(r["passAllFields"] for r in results) else 1
    result = score(json.loads(args.key.read_text(encoding="utf-8")), json.loads(args.log.read_text(encoding="utf-8")))
    print(report_text(result))
    if args.json:
        args.json.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
