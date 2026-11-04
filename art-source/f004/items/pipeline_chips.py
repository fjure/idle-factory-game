"""F004 T464 driver, tier chips (brief rows 27 to 29). Scripted; no GPU, Blender or ComfyUI needed.

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/items/pipeline_chips.py

Writes next to the T436 files, under their own names: `f003-item-tier-{1,2,3}/` (pixel PNG and provenance.json),
`manifest-t464.json`, `check-report-t464.json`, `sheets-t464/` and `answer-key/sheet_key_t464.json`.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[2] / "tools" / "art-pipeline"))

from art_pipeline import tier_chips  # noqa: E402


def main() -> int:
    b = tier_chips.write_all(HERE)
    rep = b["report"]
    bad = [(a, r["name"]) for a, rs in rep["assets"].items() for r in rs if not r["passed"]]
    bad += [("global", r["name"]) for r in rep["global"] if not r["passed"]]
    print("CHECKS-PASSED" if rep["passed"] else "CHECKS-FAILED %s" % bad)
    return 0 if rep["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
