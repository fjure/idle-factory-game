"""Turns the raw output of the T603 capture driver into evidence: measures every capture, writes the sidecars and freezes the baseline values.

    python -m metrics.finalize_captures <scratch-dir> <destination-dir> <fixtures.json>

For every `<stem>-raw.json` in the scratch directory this copies `<stem>.png`, `<stem>-bodymask.png` and `<stem>-layout.json` to the destination, writes the sidecar
`<stem>.json` (the driver's fields plus deadAreaShare, medianMachineAreaPx and the metrics version) and validates it. A `-repeat` capture is compared with its original
byte for byte (the determinism check) and is not copied. `baseline-metrics.json` freezes every value together with METRICS_VERSION and the sha256 of the metric scripts.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import statistics
import sys
from pathlib import Path

from PIL import Image

from . import METRICS_VERSION
from .capture_iou import capture_iou
from .common import KEY_COLOUR, load_layout
from .dead_area import dead_area
from .machine_area import machine_areas
from .sidecar import stem_of, validate_sidecar

SCRIPTS = ("__init__.py", "common.py", "dead_area.py", "machine_area.py", "capture_iou.py")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def finalize(scratch: Path, dest: Path, fixtures: dict) -> dict:
    dest.mkdir(parents=True, exist_ok=True)
    cells = {}
    determinism = []
    for raw_path in sorted(scratch.glob("*-raw.json")):
        stem = raw_path.name[: -len("-raw.json")]
        if stem.endswith("-repeat"):
            original = stem[: -len("-repeat")]
            determinism.append({
                "cell": original,
                "png": {"first": sha256(scratch / f"{original}.png"), "repeat": sha256(scratch / f"{stem}.png")},
                "bodymask": {"first": sha256(scratch / f"{original}-bodymask.png"), "repeat": sha256(scratch / f"{stem}-bodymask.png")},
            })
            continue
        raw = json.loads(raw_path.read_text(encoding="utf-8"))
        layout = load_layout(scratch / f"{stem}-layout.json")
        capture = Image.open(scratch / f"{stem}.png")
        mask = Image.open(scratch / f"{stem}-bodymask.png")
        dead = dead_area(capture, layout)
        area = machine_areas(mask, layout)
        iou = capture_iou(mask, layout)
        side = {k: v for k, v in raw.items() if k in (
            "look", "geometry", "class", "textScale", "factoryState", "seed", "commit", "buildKind", "device", "resolution", "capturedAt", "profile", "buildId",
            "framingRect", "route", "layout", "bodyMask", "notes", "pixelsPerPoint", "dpi", "scaleReason", "columns", "cardOpen", "realStage")}
        side["deadAreaShare"] = round(dead["deadAreaShare"], 6)
        side["medianMachineAreaPx"] = area["medianBodyAreaPx"]
        side["metricsVersion"] = METRICS_VERSION
        problems = validate_sidecar(side, f"{stem}.png", fixtures)
        if problems:
            raise SystemExit(f"{stem}: " + "; ".join(problems))
        for name in (f"{stem}.png", f"{stem}-bodymask.png", f"{stem}-layout.json"):
            shutil.copyfile(scratch / name, dest / name)
        (dest / f"{stem}.json").write_text(json.dumps(side, indent=2) + "\n", encoding="utf-8", newline="\n")
        cells[stem] = {
            "class": side["class"], "textScale": side["textScale"], "factoryState": side["factoryState"], "resolution": side["resolution"],
            "deadAreaShare": side["deadAreaShare"], "deadPx": dead["deadPx"], "safeAreaPx": dead["safeAreaPx"], "uiInSafePx": dead["uiInSafePx"],
            "machineCount": area["machineCount"], "medianMachineAreaPx": area["medianBodyAreaPx"], "medianOverhangPx": area["medianOverhangPx"],
            "machineAreasPx": {str(m["slot"]): m["bodyAreaPx"] for m in area["machines"]}, "emptyBodies": area["emptyBodies"],
            "silhouetteWorstIou": iou["worst"], "silhouetteTypes": iou["types"],
        }
    here = Path(__file__).resolve().parent
    frozen = {
        "schema": 1,
        "look": "F006",
        "metricsVersion": METRICS_VERSION,
        "keyColour": list(KEY_COLOUR),
        "scriptSha256": {name: sha256(here / name) for name in SCRIPTS},
        "captureCommit": fixtures["captureCommit"],
        "fixtures": {name: {"seed": s["seed"], "occupied": s["occupied"], "stage": s["stage"]} for name, s in fixtures["states"].items()},
        "determinism": determinism,
        "cells": cells,
        "perClass": _per_class(cells),
    }
    (dest / "baseline-metrics.json").write_text(json.dumps(frozen, indent=2) + "\n", encoding="utf-8", newline="\n")
    return frozen


def _per_class(cells: dict) -> dict:
    """SC-004 and SC-006 summary per class variant and state: the median machine body area (all machines of the cell) and the dead-area share."""
    out = {}
    for stem, c in sorted(cells.items()):
        out[f"{c['class']}-t{c['textScale']}-{c['factoryState']}"] = {"medianMachineAreaPx": c["medianMachineAreaPx"], "deadAreaShare": c["deadAreaShare"]}
    return out


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__)
        return 2
    fixtures = json.loads(Path(argv[2]).read_text(encoding="utf-8"))
    frozen = finalize(Path(argv[0]), Path(argv[1]), fixtures)
    bad = [d for d in frozen["determinism"] if d["png"]["first"] != d["png"]["repeat"] or d["bodymask"]["first"] != d["bodymask"]["repeat"]]
    print(f"{len(frozen['cells'])} cells, determinism checks: {len(frozen['determinism'])}, not identical: {len(bad)}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
