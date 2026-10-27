"""Generate the committed golden-reproduction fixtures (deterministic, seeded).

Run from tools/art-pipeline:  uv run --frozen --offline python tests/make_fixtures.py

Writes per route: raw inputs (what the Blender render / ComfyUI output / scripted drawing would
hand to the pixel pass) and golden pixel-pass outputs, plus fixtures/goldens.json with pixel hashes.
"""
from __future__ import annotations

import json
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from art_pipeline import cues  # noqa: E402
from art_pipeline.palette import load_palette  # noqa: E402
from art_pipeline.pixel_pass import PassConfig, pixel_hash, run_pixel_pass  # noqa: E402

FIX = Path(__file__).resolve().parent / "fixtures"

BLENDER_CFG = PassConfig(route="blender", canvas=(48, 60), footprint=(48, 48), supersample=3, method="mode",
                         quant_groups={"machine", "ink"})
COMFY_CFG = PassConfig(route="comfy", canvas=(32, 32), footprint=(28, 24), quant_groups={"resources", "ink"})
SCRIPTED_CFG = PassConfig(route="scripted", canvas=(14, 14), footprint=(14, 14), quant_groups={"state_cues", "ink"})


def blender_raw(frame: int) -> Image.Image:
    """Supersampled (3x) oblique machine; antialiased edges like a real render; window lit on frame 0."""
    s, ss = 3, 12  # 3x supersample of a 40x44 art-px body, drawn at 12x and box-reduced
    w, h = 40, 44
    big = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    k = ss
    d.polygon([(2 * k, 12 * k), (22 * k, 4 * k), (38 * k, 12 * k), (18 * k, 20 * k)], fill=(194, 200, 214, 255))  # top
    d.polygon([(2 * k, 12 * k), (18 * k, 20 * k), (18 * k, 43 * k), (2 * k, 35 * k)], fill=(92, 100, 122, 255))  # left
    d.polygon([(18 * k, 20 * k), (38 * k, 12 * k), (38 * k, 35 * k), (18 * k, 43 * k)], fill=(60, 66, 86, 255))  # right
    window = (255, 214, 110, 255) if frame == 0 else (74, 79, 92, 255)
    d.polygon([(22 * k, 26 * k), (34 * k, 21 * k), (34 * k, 28 * k), (22 * k, 33 * k)], fill=window)
    return big.resize((w * s, h * s), Image.Resampling.BOX)


def comfy_raw() -> Image.Image:
    """Off-grid generative-looking output: a 4x-upscaled 24x24 gem with per-pixel jitter and 3 % outliers."""
    art = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    d.polygon([(12, 2), (21, 9), (12, 22), (3, 9)], fill=(90, 182, 230, 255))
    d.polygon([(12, 2), (21, 9), (12, 9)], fill=(160, 215, 245, 255))
    d.polygon([(12, 9), (21, 9), (12, 22)], fill=(60, 125, 205, 255))
    for y in range(10, 20):  # fine detail so coarser factors are not blockwise uniform
        d.line([(8 + y % 2, y), (15 - y % 3, y)], fill=(230, 245, 255, 255))
    big = art.resize((96, 96), Image.Resampling.NEAREST)
    rng = random.Random(7)
    px = big.load()
    for y in range(96):
        for x in range(96):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            j = [min(255, max(0, c + rng.randint(-8, 8))) for c in (r, g, b)]
            if rng.random() < 0.03:
                j = [rng.randint(0, 255) for _ in range(3)]
            px[x, y] = (j[0], j[1], j[2], 255)
    return big


def scripted_raw(palette) -> Image.Image:
    return cues.render_cue("missing_input", palette)


def main() -> None:
    palette = load_palette()
    raw = {
        "blender": [blender_raw(0), blender_raw(1)],
        "comfy": [comfy_raw()],
        "scripted": [scripted_raw(palette)],
    }
    cfgs = {"blender": BLENDER_CFG, "comfy": COMFY_CFG, "scripted": SCRIPTED_CFG}
    goldens = {}
    for route, frames in raw.items():
        out = FIX / route
        out.mkdir(parents=True, exist_ok=True)
        for i, f in enumerate(frames):
            f.save(out / f"raw_{i:02d}.png", compress_level=9, optimize=False)
        result, _placement = run_pixel_pass(frames, palette, cfgs[route])
        goldens[route] = {"raw": [pixel_hash(f) for f in frames], "golden": [pixel_hash(f) for f in result]}
        for i, f in enumerate(result):
            f.save(out / f"golden_{i:02d}.png", compress_level=9, optimize=False)
    (FIX / "goldens.json").write_text(json.dumps(goldens, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(goldens, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
