"""Capture plan of the F006 baseline cells (research R08, FR-011, FR-011a, FR-012) for the T603 capture driver.

    python -m metrics.capture_plan <fixtures.json> <plan.json> [--repeat <stem>]

The plan is the driver's input (`F007BaselineCapturePlayerTests`, environment variable IDLEFACTORY_T603_SPEC). One job per class variant and factory state:
emuP, emuL, tabL, phoneP-card at 100 % and 200 % text (machine card open), phoneL640 at 100 % and 200 % text. `--repeat` adds a second capture of one cell
(stem + "-repeat") for the determinism check; the repeat is compared and never committed.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

LOOK = "F006"
GEOMETRY = "same"

# class key -> (width, height, dpi, input, text scales, card open, profile description)
CLASSES = {
    "emuP": (1080, 2400, 420.0, "touch", (100,), False, "emulator class, touch 420 dpi (R08)"),
    "emuL": (2400, 1080, 420.0, "touch", (100,), False, "emulator class landscape, touch 420 dpi"),
    "tabL": (2560, 1600, 276.0, "touch", (100,), False, "tablet class, touch 276 dpi (assumed: R08 gives no dpi; the 7 mm rule decides the panel scale)"),
    "phoneP-card": (720, 1280, 320.0, "touch", (100, 200), True, "phone portrait, touch 320 dpi (R08), machine card open"),
    "phoneL640": (640, 360, 160.0, "touch", (100, 200), False, "phone landscape minimum-height class, touch 160 dpi (1 pt per px)"),
}


def stem_of(cls: str, text: int, state: str) -> str:
    return f"{LOOK}-{GEOMETRY}-{cls}-t{text}-{state}"


def build_plan(fixtures: dict, repeat: str | None = None) -> dict:
    jobs = []
    for cls, (w, h, dpi, source, texts, card, profile) in CLASSES.items():
        for text in texts:
            for state in ("partial", "dense"):
                fx = fixtures["states"][state]
                job = {
                    "stem": stem_of(cls, text, state), "look": LOOK, "geometry": GEOMETRY, "cls": cls, "w": w, "h": h, "dpi": dpi, "input": source,
                    "textScale": text, "state": state, "seed": fx["seed"], "stage": fx["stage"], "cardSlot": fx["cardSlot"] if card else -1,
                    "commit": fixtures["captureCommit"], "profile": profile,
                    "route": "Unity 6000.6.0f1 editor Play Mode (batch, GPU), real scene CoreGameFoundation.unity; Game view custom rendering resolution at the class size (a Windows window cannot exceed the 1920x1080 display); shipped RunShellDocument.ApplyDeviceProfile (touch, class dpi) and ApplyTextScale; forced per-slot visuals (T545 method); simulation, animation clock and attention markers frozen; world camera clears to the key colour",
                    "notes": f"fixtures.json seed {fx['seed']}; {fx['occupied']} occupied, stage {fx['stage']}",
                    "slots": fx["slots"],
                }
                jobs.append(job)
                if repeat == job["stem"]:
                    jobs.append({**job, "stem": job["stem"] + "-repeat"})
    if repeat and not any(j["stem"] == repeat + "-repeat" for j in jobs):
        raise SystemExit(f"--repeat {repeat!r} is not a cell of the plan")
    return {"schema": 1, "jobs": jobs}


def main(argv: list[str]) -> int:
    p = argparse.ArgumentParser()
    p.add_argument("fixtures")
    p.add_argument("plan")
    p.add_argument("--repeat")
    args = p.parse_args(argv)
    fixtures = json.loads(Path(args.fixtures).read_text(encoding="utf-8"))
    plan = build_plan(fixtures, args.repeat)
    Path(args.plan).write_text(json.dumps(plan, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(plan['jobs'])} jobs")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
