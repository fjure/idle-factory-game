"""SC-001 sheets from in-game captures (T422, research R17).

Input: per-set folders of tile crops (`<tiles>/<set>/<tile-id>.png`) cut from isolated in-game captures at the
authoritative scale (same camera and integer scale as the full-lattice capture; one slot per render so neighbouring
plates do not bleed into the crop). Output per set, flat in `--out`: `<set>_grayscale.png`, `<set>_deuteranopia.png`,
`<set>_protanopia.png`, `<set>_tritanopia.png` and `<set>_key.json` (the answer key; withheld from the reviewer).
The tile order is a seeded shuffle (`--seed-base` + the set's index in sorted order), so sheets are reproducible.

    uv run --frozen --offline --project tools/art-pipeline python tools/art-pipeline/trials/make_capture_sheets.py \
        --tiles <dir> --out specs/004-visual-identity/reviews/style-spike/s3/sheets
"""
from __future__ import annotations

import argparse
import json
import shutil
import sys
import tempfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from art_pipeline import checks  # noqa: E402


def build_sets(tiles_dir: Path, out_dir: Path, seed_base: int) -> dict[str, dict]:
    out_dir.mkdir(parents=True, exist_ok=True)
    result = {}
    for index, set_dir in enumerate(sorted(p for p in tiles_dir.iterdir() if p.is_dir())):
        tiles = {f.stem: Image.open(f).convert("RGBA") for f in sorted(set_dir.glob("*.png"))}
        if not tiles:
            continue
        seed = seed_base + index
        with tempfile.TemporaryDirectory() as tmp:
            made = checks.make_review_sheets(tiles, seed, tmp, scale=1)
            for mode, path in made["sheets"].items():
                shutil.copyfile(path, out_dir / f"{set_dir.name}_{mode}.png")
            key = json.loads(Path(made["key"]).read_text(encoding="utf-8"))
        key["set"] = set_dir.name
        key["tileCount"] = len(tiles)
        key["tileSize"] = list(next(iter(tiles.values())).size)
        (out_dir / f"{set_dir.name}_key.json").write_text(json.dumps(key, indent=2) + "\n", encoding="utf-8", newline="\n")
        result[set_dir.name] = {"seed": seed, "tiles": len(tiles), "columns": key["columns"]}
    return result


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--tiles", required=True, type=Path)
    ap.add_argument("--out", required=True, type=Path)
    ap.add_argument("--seed-base", type=int, default=4220)
    args = ap.parse_args(argv)
    summary = build_sets(args.tiles, args.out, args.seed_base)
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
