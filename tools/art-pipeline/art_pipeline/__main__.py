"""CLI: `python -m art_pipeline checks [--art-root DIR] [--report FILE]` (run from tools/art-pipeline).

Repository-level checks: palette definition, models.json schema, size budgets.
Per-sprite checks run through `art_pipeline.checks.run_asset_checks` on manifest entries.
"""
from __future__ import annotations

import argparse
import sys

from . import checks
from .palette import load_palette
from .provenance import load_models, validate_models


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="art_pipeline")
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("checks", help="run repository-level checks")
    c.add_argument("--art-root", default=None, help="art-source/f004 directory for size budgets")
    c.add_argument("--report", default=None, help="write the JSON report here")
    q = sub.add_parser("cues", help="regenerate the T437 scripted cue, marker, frame and label assets")
    q.add_argument("--out", required=True, help="art-source/f004 directory to write")
    u = sub.add_parser("ui-icons", help="regenerate the T438 scripted UI icon assets")
    u.add_argument("--out", required=True, help="art-source/f004 directory to write")
    k = sub.add_parser("mockup", help="composite the F006 direction mockups (T505) from the shared lattice geometry")
    k.add_argument("--direction", action="append", required=True, help="direction JSON (sprites by role); repeat for several directions")
    k.add_argument("--out", required=True, help="output directory; one subdirectory per direction")
    k.add_argument("--geometry", default=None, help="lattice-geometry.json (default tools/art-pipeline/f006/lattice-geometry.json)")
    k.add_argument("--palette", default=None, help="palette JSON for the input gates (default palette-v1)")
    k.add_argument("--strip-scale", type=int, default=None, help="integer device-pixel scale of the strips (default n_fit of the portrait sheet)")
    f6 = sub.add_parser("concept-f006", help="regenerate the F006 T506 concept sprites")
    f6.add_argument("--out", required=True, help="art-source/f006/concept directory to write")
    ct = sub.add_parser("cue-tail", help="regenerate the F006 T524 badge pointer-tail sprites (OD-12, K4)")
    ct.add_argument("--out", required=True, help="art-source/f006/cue directory to write")
    args = ap.parse_args(argv)
    if args.cmd == "concept-f006":
        from . import concept_f006
        build = concept_f006.write_all(args.out)
        ok = all(b["passed"] for b in build.values())
        sys.stdout.write("concept-f006: %s" % ("all checks pass" if ok else "FAILING"))
        sys.stdout.write(chr(10))
        return 0 if ok else 1
    if args.cmd == "cue-tail":
        from .f006 import cue_tail
        build = cue_tail.write_all(args.out)
        ok = build["report"]["passed"]
        sys.stdout.write("cue-tail: %d sprites, %s" % (len(build["sprites"]), "all checks pass" if ok else "FAILING"))
        sys.stdout.write(chr(10))
        return 0 if ok else 1
    if args.cmd == "mockup":
        from . import mockup
        return mockup.main(args)
    if args.cmd == "ui-icons":
        from . import ui_icons
        build = ui_icons.write_all(args.out)
        bad = [(n, r["name"]) for n, rs in build["report"]["assets"].items() for r in rs if not r["passed"]]
        bad += [("global", r["name"]) for r in build["report"]["global"] if not r["passed"]]
        sys.stdout.write("ui-icons: %d icons, %s" % (len(build["icons"]), "all checks pass" if not bad else "FAILING %s" % bad))
        sys.stdout.write(chr(10))
        return 0 if build["report"]["passed"] else 1
    if args.cmd == "cues":
        from . import cues_production
        build = cues_production.write_all(args.out)
        bad = [(f, n) for f, r in build["reports"].items() for n, rs in r["assets"].items() for x in rs if not x["passed"]]
        ok = all(r["passed"] for r in build["reports"].values())
        sys.stdout.write("cues: %d sprites, %s" % (len(build["sprites"]), "all checks pass" if ok else "FAILING %s" % bad))
        sys.stdout.write(chr(10))
        return 0 if ok else 1

    results = [checks.check_palette_definition(load_palette())]
    problems = validate_models(load_models())
    results.append(checks.CheckResult("models", not problems, {"problems": problems}))
    if args.art_root:
        results.append(checks.check_size_budgets(args.art_root))
    report = checks.build_report({}, results)
    text = checks.report_json(report)
    if args.report:
        checks.write_report(args.report, report)
    sys.stdout.write(text)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
