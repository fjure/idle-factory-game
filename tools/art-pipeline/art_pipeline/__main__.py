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
    args = ap.parse_args(argv)
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
