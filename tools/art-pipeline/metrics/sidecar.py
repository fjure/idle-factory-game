"""Validator of the comparison-capture sidecar (data-model "ComparisonCapture", plan review QA-9, contracts/evidence-and-decision.md "Capture rules").

A capture without a valid sidecar is not evidence. The sidecar sits next to the PNG with the same stem and the extension `.json`; the stem is
`<look>-<geometry>-<class>-t<textScale>-<factoryState>`, so a sheet generator can reject a sidecar that does not belong to its file name.

    python -m metrics.sidecar <captures-dir> [fixtures.json]
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

CLASSES = ("emuP", "emuL", "tabL", "phoneP-card", "phoneL640")
GEOMETRIES = ("same", "proposed")
STATES = ("partial", "dense")
BUILD_KINDS = ("apk-dev", "windows-player", "editor-playmode")
LOOKS = ("F006", "A", "B", "C")
# The fields of data-model ComparisonCapture plus the QA-9 additions (profile, build id, measured framing rectangle) and the provenance fields.
REQUIRED = (
    "look", "geometry", "class", "textScale", "factoryState", "seed", "commit", "buildKind", "device", "resolution", "capturedAt",
    "deadAreaShare", "medianMachineAreaPx", "profile", "buildId", "framingRect", "route", "layout", "bodyMask", "metricsVersion",
)
SHA = re.compile(r"^[0-9a-f]{40}$")


def stem_of(side: dict) -> str:
    return f"{side['look']}-{side['geometry']}-{side['class']}-t{side['textScale']}-{side['factoryState']}"


def validate_sidecar(side: dict, png_name: str | None = None, fixtures: dict | None = None) -> list[str]:
    """Returns the problems (an empty list means the sidecar is valid)."""
    problems: list[str] = []
    missing = [k for k in REQUIRED if k not in side]
    if missing:
        return [f"missing fields: {', '.join(missing)}"]
    if side["look"] not in LOOKS:
        problems.append(f"look {side['look']!r} not in {LOOKS}")
    if side["geometry"] not in GEOMETRIES:
        problems.append(f"geometry {side['geometry']!r} not in {GEOMETRIES}")
    if side["class"] not in CLASSES:
        problems.append(f"class {side['class']!r} not in {CLASSES}")
    if side["factoryState"] not in STATES:
        problems.append(f"factoryState {side['factoryState']!r} not in {STATES}")
    if side["buildKind"] not in BUILD_KINDS:
        problems.append(f"buildKind {side['buildKind']!r} not in {BUILD_KINDS}")
    if side["textScale"] not in (100, 200):
        problems.append("textScale must be 100 or 200")
    if not isinstance(side["seed"], int) or isinstance(side["seed"], bool):
        problems.append("seed must be an integer")
    if not isinstance(side["commit"], str) or not SHA.match(side["commit"]):
        problems.append("commit must be a 40-character lower-case sha")
    res = side["resolution"]
    if not (isinstance(res, dict) and isinstance(res.get("w"), int) and isinstance(res.get("h"), int) and res["w"] > 0 and res["h"] > 0):
        problems.append("resolution must be {w, h} with positive integers")
    if not (isinstance(side["deadAreaShare"], (int, float)) and 0.0 <= side["deadAreaShare"] <= 1.0):
        problems.append("deadAreaShare must be a number between 0 and 1")
    if not (isinstance(side["medianMachineAreaPx"], (int, float)) and side["medianMachineAreaPx"] >= 0):
        problems.append("medianMachineAreaPx must be a non-negative number")
    fr = side["framingRect"]
    if not (isinstance(fr, dict) and all(isinstance(fr.get(k), (int, float)) for k in ("x", "y", "w", "h"))):
        problems.append("framingRect must be {x, y, w, h} (the measured unobstructed world rectangle in capture pixels)")
    for key in ("device", "profile", "buildId", "route", "capturedAt"):
        if not isinstance(side[key], str) or not side[key]:
            problems.append(f"{key} must be a non-empty string")
    if png_name is not None and Path(png_name).stem != stem_of(side):
        problems.append(f"file name {Path(png_name).stem!r} does not match the sidecar fields ({stem_of(side)!r})")
    if fixtures is not None:
        state = fixtures.get("states", {}).get(side["factoryState"])
        if state is None:
            problems.append(f"fixtures.json has no state {side['factoryState']!r}")
        else:
            if state.get("seed") != side["seed"]:
                problems.append(f"seed {side['seed']} differs from fixtures.json ({state.get('seed')})")
            if fixtures.get("captureCommit") != side["commit"]:
                problems.append(f"commit {side['commit']} differs from fixtures.json captureCommit ({fixtures.get('captureCommit')})")
    return problems


def validate_directory(directory: Path, fixtures: dict | None = None) -> dict[str, list[str]]:
    """Every PNG needs a valid sidecar; every sidecar needs its PNG. Returns file name -> problems (only the failing ones)."""
    failures: dict[str, list[str]] = {}
    pngs = {p.stem: p for p in directory.glob("*.png") if not p.stem.endswith("-bodymask")}
    sidecars = {p.stem: p for p in directory.glob("*.json") if p.stem not in ("baseline-metrics",) and not p.stem.endswith("-layout")}
    for stem, png in pngs.items():
        if stem not in sidecars:
            failures[png.name] = ["no JSON sidecar"]
    for stem, path in sidecars.items():
        if stem not in pngs:
            failures[path.name] = ["sidecar without a PNG"]
            continue
        problems = validate_sidecar(json.loads(path.read_text(encoding="utf-8")), pngs[stem].name, fixtures)
        side = json.loads(path.read_text(encoding="utf-8"))
        for ref in ("layout", "bodyMask"):
            if isinstance(side.get(ref), str) and not (directory / side[ref]).exists():
                problems.append(f"{ref} file {side[ref]!r} is missing")
        if problems:
            failures[path.name] = problems
    return failures


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 2
    fixtures = json.loads(Path(argv[1]).read_text(encoding="utf-8")) if len(argv) > 1 else None
    failures = validate_directory(Path(argv[0]), fixtures)
    for name, problems in sorted(failures.items()):
        print(name, *problems, sep="\n  ")
    print(f"{len(failures)} failing")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
