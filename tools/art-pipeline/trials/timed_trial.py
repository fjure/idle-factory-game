"""SC-002 harness: the owner's timed at-a-glance trial (T422 builds it, T423 runs it; research R17).

Ten seeded 12-slot factory screenshots per cue variant. Every screenshot has exactly one machine in a problem
state (missing input, storage full or waiting; balanced across the ten trials, 4/3/3 in a seeded order), one or two
benign distractors (paused or inactive, per the salience rule) and otherwise running machines, empty and locked
plates. The owner points out the stuck machine; the pass rule is at least 9 of 10 correct within 2.0 seconds.

The screenshots are composed from the committed S3/S4 sprites (byte-identical to the imported Unity sprites) on the
slot centres of the F003 rectangular lattice that the temporary in-game viewer measured at the authoritative integer
scale for a named screen class (`trial_layouts.json`, default: the 1080x2400 / 420 dpi emulator portrait at `n_min`).
Composition is pixel-for-pixel what the viewer rendered (`test_timed_trial.py` compares it with the committed
in-game capture).

Commands (run from the repository root; Python 3.12+ with Pillow, i.e. the art-pipeline project environment):

    uv run --frozen --offline --project tools/art-pipeline python tools/art-pipeline/trials/timed_trial.py generate --variant a --out specs/004-visual-identity/reviews/style-spike/s3/trial/a
    ... generate --variant b --out .../trial/b
    ... serve|open the page:   open .../trial/a/trial.html in a browser (no network, no server; click the stuck machine)
    ... run-cli --dir .../trial/a          (typed alternative: Enter to start, then type "row col" of the stuck machine)
    ... score --dir .../trial/a --log trial-log-a.json

`generate` writes `trial_01.png` .. `trial_10.png`, `key.json` (the answer key; do not read it before the run),
`trial.html` (the clickable timing page; it contains slot positions but no answers) and `README.txt`.
"""
from __future__ import annotations

import argparse
import json
import random
import statistics
import sys
import time
import webbrowser
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
S3 = REPO / "art-source/f004/spike/s3-s4/pixel"
S2 = REPO / "art-source/f004/spike/s2/pixel/D24"
LAYOUTS = HERE / "trial_layouts.json"

BACKGROUND = (40, 40, 48, 255)
PROBLEM_STATES = ["waiting", "missing-input", "storage-full"]
DISTRACTOR_STATES = ["paused", "inactive"]
TRIALS = 10
SLOTS = 12
LIMIT_SECONDS = 2.0
REQUIRED_CORRECT = 9
# Sprite canvas 80x64 art px, pivot (40, 15) from the bottom-left, so the top-left corner sits 40 px left and 49 px above the slot centre.
CANVAS_W, CANVAS_H, PIVOT_X, PIVOT_FROM_TOP = 80, 64, 40, 49
# Filler kinds (weights): running miner, running assembler, empty plate, locked plate.
FILLERS = [("miner-running", 4), ("assembler-running", 4), ("plate-empty", 1), ("plate-locked", 1)]


def layers_for(kind: str, variant: str, size: int) -> list[Path]:
    """Sprite files (bottom to top) that make up one slot of the given kind."""
    if kind == "miner-running":
        return [S3 / "s3-comp-running_00.png"]
    if kind == "assembler-running":
        return [S2 / "s2-slot-occupied_00.png", S2 / "s2-assembler-run_00.png"]
    if kind in ("plate-empty", "plate-locked", "plate-occupied"):
        return [S3 / f"s3-{kind}_00.png"]
    if kind == "lightB-running":  # only used to reproduce the viewer's full lattice (fidelity test)
        return [S3 / "s3-lightB-comp-run_00.png"]
    if kind == "ore":
        return [S2 / "s2-res-ore_00.png"]
    return [S3 / f"s3{variant}-comp-{kind}-{size}_00.png"]


def load_layout(screen_class: str, mode: str, path: Path = LAYOUTS) -> dict:
    for entry in json.loads(path.read_text(encoding="utf-8")):
        if entry["class"] == screen_class and entry["mode"] == mode:
            return entry
    raise SystemExit(f"no layout for class={screen_class} mode={mode} in {path}")


def compose(layout: dict, kinds: list[str], variant: str, size: int) -> Image.Image:
    """Compose one screenshot: the slots in index order (later slots draw over earlier ones), nearest-neighbour scaled by n."""
    n = layout["n"]
    img = Image.new("RGBA", (layout["w"], layout["h"]), BACKGROUND)
    cache: dict[Path, Image.Image] = {}
    for index, kind in enumerate(kinds):
        cx, cy = layout["slots"][index]
        for sprite in layers_for(kind, variant, size):
            if sprite not in cache:
                src = Image.open(sprite).convert("RGBA")
                cache[sprite] = src.resize((src.width * n, src.height * n), Image.Resampling.NEAREST)
            scaled = cache[sprite]
            # Machine/plate canvas 80x64, pivot (40, 15 from the bottom); ore canvas 32x24, pivot (16, 4.5 from the bottom).
            left = round(cx) - scaled.width // 2
            top = round(cy - (PIVOT_FROM_TOP * n if scaled.height == CANVAS_H * n else (24 - 4.5) * n))
            img.paste(scaled, (left, top), scaled)
    return img


def problem_sequence(rng: random.Random, trials: int = TRIALS) -> list[str]:
    """Balanced problem states: each state trials//3 times, the remainder drawn from a seeded shuffle of the states."""
    states = PROBLEM_STATES * (trials // len(PROBLEM_STATES))
    extra = PROBLEM_STATES[:]
    rng.shuffle(extra)
    states += extra[: trials - len(states)]
    rng.shuffle(states)
    return states


def make_trial(rng: random.Random, problem_state: str) -> dict:
    slots = list(range(SLOTS))
    rng.shuffle(slots)
    problem = slots[0]
    distractor_count = rng.choice([1, 2])
    distractors = slots[1 : 1 + distractor_count]
    kinds = [""] * SLOTS
    kinds[problem] = problem_state
    distractor_states = []
    for slot in distractors:
        state = rng.choice(DISTRACTOR_STATES)
        kinds[slot] = state
        distractor_states.append(state)
    names, weights = zip(*FILLERS)
    for slot in slots[1 + distractor_count :]:
        kinds[slot] = rng.choices(names, weights=weights)[0]
    return {"problemSlot": problem, "problemState": problem_state,
            "distractors": [{"slot": s, "state": st} for s, st in zip(distractors, distractor_states)], "slotKinds": kinds}


def generate(variant: str, size: int, screen_class: str, mode: str, seed: int, out: Path,
             layouts: Path = LAYOUTS, write_html: bool = True) -> dict:
    layout = load_layout(screen_class, mode, layouts)
    rng = random.Random(seed)
    out.mkdir(parents=True, exist_ok=True)
    trials = []
    for number, state in enumerate(problem_sequence(rng), start=1):
        trial = make_trial(rng, state)
        trial["trial"] = number
        trial["file"] = f"trial_{number:02d}.png"
        compose(layout, trial["slotKinds"], variant, size).convert("RGB").save(out / trial["file"], format="PNG", compress_level=9)
        trials.append(trial)
    columns = len({round(x) for x, _ in layout["slots"]})
    key = {"schema": 1, "variant": variant, "iconSize": size, "class": screen_class, "mode": mode, "seed": seed,
           "screen": [layout["w"], layout["h"]], "n": layout["n"], "columns": columns, "slotCentres": layout["slots"],
           "limitSeconds": LIMIT_SECONDS, "requiredCorrect": REQUIRED_CORRECT, "trials": trials}
    (out / "key.json").write_text(json.dumps(key, indent=2) + "\n", encoding="utf-8", newline="\n")
    if write_html:
        (out / "trial.html").write_text(render_html(key), encoding="utf-8", newline="\n")
        (out / "README.txt").write_text(
            f"SC-002 timed trial, variant {variant}, icon {size} px, class {screen_class} at {mode}.\n"
            "Open trial.html in a browser, make the window phone-shaped, and click the stuck machine as fast as you can.\n"
            "Save the log with the button at the end, then run: timed_trial.py score --dir <this folder> --log <log.json>\n"
            "Do not open key.json before the run.\n", encoding="utf-8", newline="\n")
    return key


def nearest_slot(centres: list[list[float]], n: int, x: float, y: float) -> int:
    """Slot whose machine centre (slot centre raised by 12 art px) is nearest to the clicked screen point."""
    return min(range(len(centres)), key=lambda i: (centres[i][0] - x) ** 2 + (centres[i][1] - 12 * n - y) ** 2)


def score(key: dict, log: dict) -> dict:
    """Score a timing log {"trials": [{"trial": 1, "slot": 4, "ms": 1234.5}, ...]} against the key."""
    answers = {t["trial"]: t for t in log["trials"]}
    rows = []
    for t in key["trials"]:
        got = answers.get(t["trial"])
        if got is None:
            rows.append({"trial": t["trial"], "state": t["problemState"], "correct": False, "seconds": None, "inTime": False, "missing": True})
            continue
        correct = got["slot"] == t["problemSlot"]
        seconds = got["ms"] / 1000.0
        rows.append({"trial": t["trial"], "state": t["problemState"], "correct": correct, "seconds": round(seconds, 3),
                     "inTime": correct and seconds <= key["limitSeconds"], "missing": False})
    passed = sum(r["inTime"] for r in rows)
    times = [r["seconds"] for r in rows if r["seconds"] is not None]
    return {"variant": key["variant"], "rows": rows, "correctInTime": passed, "required": key["requiredCorrect"],
            "pass": passed >= key["requiredCorrect"] and len(rows) == len(key["trials"]),
            "medianSeconds": round(statistics.median(times), 3) if times else None}


def render_html(key: dict) -> str:
    """The clickable page. It embeds the screen size, scale and slot centres (needed to map a click to a slot) but no answers."""
    data = {"variant": key["variant"], "files": [t["file"] for t in key["trials"]], "w": key["screen"][0], "h": key["screen"][1],
            "n": key["n"], "centres": key["slotCentres"]}
    return HTML.replace("__DATA__", json.dumps(data))


HTML = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>SC-002 timed trial</title>
<style>
 body{margin:0;background:#16161c;color:#e8e8ee;font:16px system-ui,sans-serif;text-align:center}
 #stage{display:inline-block;position:relative;margin-top:8px}
 #shot{display:block;height:92vh;image-rendering:pixelated;cursor:crosshair}
 #panel{padding:24px}
 button{font:inherit;padding:10px 22px;margin:8px}
 table{margin:12px auto;border-collapse:collapse} td,th{padding:3px 12px;border-bottom:1px solid #444}
 textarea{width:90%;height:8em}
</style></head><body>
<div id="panel"><p id="msg">Trial of <span id="count"></span> screenshots. Find the stuck machine (a problem cue at the machine) and click it as fast as you can.</p>
<button id="go">Start trial 1</button></div>
<div id="stage" hidden><img id="shot" alt="factory screenshot"></div>
<div id="result" hidden></div>
<script>
const D=__DATA__; let i=0, t0=0, shown=false; const log=[];
const $=id=>document.getElementById(id);
$("count").textContent=D.files.length;
function show(){ $("panel").hidden=true; $("result").hidden=true; const im=$("shot"); shown=false;
  im.onload=()=>requestAnimationFrame(()=>{ $("stage").hidden=false; t0=performance.now(); shown=true; });
  im.src=D.files[i]; }
$("go").onclick=show;
$("shot").addEventListener("click",ev=>{ if(!shown) return; const dt=performance.now()-t0; shown=false;
  const r=$("shot").getBoundingClientRect(), k=D.w/r.width, x=(ev.clientX-r.left)*k, y=(ev.clientY-r.top)*k;
  let best=0,bd=1e18; D.centres.forEach((c,j)=>{const d=(c[0]-x)**2+(c[1]-12*D.n-y)**2; if(d<bd){bd=d;best=j;}});
  log.push({trial:i+1,slot:best,ms:Math.round(dt*10)/10}); i++; $("stage").hidden=true;
  if(i<D.files.length){ $("panel").hidden=false; $("msg").textContent="Recorded. Ready for the next one."; $("go").textContent="Start trial "+(i+1); }
  else finish(); });
function finish(){ const out={variant:D.variant,trials:log}; const txt=JSON.stringify(out,null,2); const res=$("result"); res.hidden=false;
  res.innerHTML="<h2>Done</h2><p>Save the log, then score it with <code>timed_trial.py score</code>.</p><button id='save'>Save log</button><br><textarea readonly></textarea>";
  res.querySelector("textarea").value=txt;
  $("save").onclick=()=>{const a=document.createElement("a"); a.href=URL.createObjectURL(new Blob([txt],{type:"application/json"})); a.download="trial-log-"+D.variant+".json"; a.click();}; }
</script></body></html>
"""


def run_cli(directory: Path) -> dict:
    key = json.loads((directory / "key.json").read_text(encoding="utf-8"))
    cols = key["columns"]
    log = {"variant": key["variant"], "trials": []}
    print(f"{len(key['trials'])} trials. Each opens a screenshot; press Enter again when you see the stuck machine, then type 'row col' (1-based, {cols} columns).")
    for t in key["trials"]:
        input(f"Trial {t['trial']}: press Enter to show the screenshot ")
        webbrowser.open((directory / t["file"]).resolve().as_uri())
        start = time.perf_counter()
        input("  Enter when found ")
        elapsed = (time.perf_counter() - start) * 1000
        row, col = (int(v) for v in input("  stuck machine row col: ").split())
        log["trials"].append({"trial": t["trial"], "slot": (row - 1) * cols + (col - 1), "ms": round(elapsed, 1)})
    return log


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="SC-002 timed trial harness")
    sub = ap.add_subparsers(dest="cmd", required=True)
    g = sub.add_parser("generate", help="write the ten screenshots, the key and the timing page")
    g.add_argument("--variant", choices=["a", "b"], required=True)
    g.add_argument("--size", type=int, choices=[17, 23], default=23, help="cue icon art px")
    g.add_argument("--class", dest="screen_class", default="emuPwin", help="screen class in trial_layouts.json (default emuPwin = emulator portrait)")
    g.add_argument("--mode", choices=["nmin", "fit"], default="nmin")
    g.add_argument("--seed", type=int, default=4220)
    g.add_argument("--out", type=Path, required=True)
    r = sub.add_parser("run-cli", help="typed alternative to the HTML page")
    r.add_argument("--dir", type=Path, required=True)
    r.add_argument("--log", type=Path, default=None, help="where to write the log (default <dir>/trial-log.json)")
    s = sub.add_parser("score", help="score a timing log against the key")
    s.add_argument("--dir", type=Path, required=True)
    s.add_argument("--log", type=Path, required=True)
    args = ap.parse_args(argv)
    if args.cmd == "generate":
        key = generate(args.variant, args.size, args.screen_class, args.mode, args.seed, args.out)
        print(f"wrote {len(key['trials'])} trials to {args.out}")
        return 0
    if args.cmd == "run-cli":
        log = run_cli(args.dir)
        target = args.log or args.dir / "trial-log.json"
        target.write_text(json.dumps(log, indent=2) + "\n", encoding="utf-8")
        print(f"log written to {target}")
        return 0
    key = json.loads((args.dir / "key.json").read_text(encoding="utf-8"))
    result = score(key, json.loads(args.log.read_text(encoding="utf-8")))
    for row in result["rows"]:
        print(f"trial {row['trial']:2d}  {row['state']:14s} correct={row['correct']!s:5s} {row['seconds']} s  in time={row['inTime']}")
    print(f"correct within {key['limitSeconds']} s: {result['correctInTime']}/{len(key['trials'])} (need {result['required']}), median {result['medianSeconds']} s -> {'PASS' if result['pass'] else 'FAIL'}")
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
