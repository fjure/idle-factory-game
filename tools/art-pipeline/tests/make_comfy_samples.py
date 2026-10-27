"""Generate the committed generative-style sample outputs for the `comfy` route (task T404).

Run from tools/art-pipeline:  uv run --frozen --offline python tests/make_comfy_samples.py

PROVENANCE: these images are SYNTHETIC. They are not output of Z-Image-Turbo or of any other model
(no model is downloaded or run in T404). A seeded Python script upscales small hand-drawn art and
degrades it the way diffusion pixel-art output typically deviates from an exact grid: soft
(blurred, anti-aliased) block edges, per-pixel colour noise, a few random outlier pixels, a grid
phase offset, wobbling rows, and upscale factors that are not integers. Replace or extend them with
real outputs only after the owner-approved model download (T405) and a recorded provenance entry.

Writes tests/fixtures/comfy/samples/<name>_raw.png and <name>_golden.png (no text metadata, so the
bytes are reproducible) plus tests/fixtures/comfy/samples.json (description, expectation, hashes).
"""
from __future__ import annotations

import json
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from art_pipeline.palette import load_palette  # noqa: E402
from art_pipeline.pixel_pass import PassConfig, pixel_hash, run_pixel_pass  # noqa: E402

OUT = Path(__file__).resolve().parent / "fixtures" / "comfy"
SAMPLES_DIR = OUT / "samples"
CFG = dict(route="comfy", canvas=(32, 32), footprint=(28, 24), quant_groups={"resources", "ink"})


def gem() -> Image.Image:
    a = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(a)
    d.polygon([(12, 2), (21, 9), (12, 22), (3, 9)], fill=(90, 182, 230, 255))
    d.polygon([(12, 2), (21, 9), (12, 9)], fill=(160, 215, 245, 255))
    d.polygon([(12, 9), (21, 9), (12, 22)], fill=(60, 125, 205, 255))
    for y in range(10, 20):
        d.line([(8 + y % 2, y), (15 - y % 3, y)], fill=(230, 245, 255, 255))
    return a


def coin() -> Image.Image:
    a = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(a)
    d.ellipse([1, 1, 18, 18], fill=(240, 180, 50, 255))
    d.ellipse([4, 4, 15, 15], fill=(200, 140, 40, 255))
    d.rectangle([9, 6, 10, 13], fill=(255, 225, 130, 255))
    d.line([(3, 6), (6, 3)], fill=(255, 240, 180, 255))
    return a


def bars() -> Image.Image:
    a = Image.new("RGBA", (22, 18), (0, 0, 0, 0))
    d = ImageDraw.Draw(a)
    d.rectangle([2, 10, 19, 16], fill=(150, 150, 160, 255))
    d.rectangle([4, 5, 17, 10], fill=(190, 190, 200, 255))
    d.rectangle([6, 2, 15, 5], fill=(225, 225, 235, 255))
    for x in range(3, 19, 4):
        d.line([(x, 11), (x, 15)], fill=(110, 110, 120, 255))
    return a


def noisy(im: Image.Image, seed: int, amp: int, outliers: float) -> Image.Image:
    rng = random.Random(seed)
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                continue
            c = [min(255, max(0, v + rng.randint(-amp, amp))) for v in (r, g, b)]
            if rng.random() < outliers:
                c = [rng.randint(0, 255) for _ in range(3)]
            px[x, y] = (c[0], c[1], c[2], a)
    return im


def soften(im: Image.Image, radius: float, alpha_radius: float) -> Image.Image:
    r, g, b, a = im.split()
    rgb = Image.merge("RGB", (r, g, b)).filter(ImageFilter.GaussianBlur(radius))
    if alpha_radius:
        a = a.filter(ImageFilter.GaussianBlur(alpha_radius))
    return Image.merge("RGBA", (*rgb.split(), a))


def row_wobble(im: Image.Image, seed: int, prob: float) -> Image.Image:
    """Shift a fraction of pixel rows by +-1 px, as if block rows were not perfectly straight."""
    rng = random.Random(seed)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    for y in range(im.height):
        shift = rng.choice((-1, 1)) if rng.random() < prob else 0
        out.paste(ImageChops.offset(im.crop((0, y, im.width, y + 1)), shift, 0), (0, y))
    return out


def build(spec: dict) -> Image.Image:
    art = spec["art"]()
    factor = spec["factor"]
    size = (round(art.width * factor), round(art.height * factor))
    im = art.resize(size, spec.get("resample", Image.Resampling.NEAREST))
    if spec.get("phase"):
        im = ImageChops.offset(im, spec["phase"], spec["phase"])
    if spec.get("wobble"):
        im = row_wobble(im, spec["seed"] + 1, spec["wobble"])
    if spec.get("blur"):
        im = soften(im, spec["blur"], spec.get("alpha_blur", 0))
    return noisy(im, spec["seed"], spec.get("amp", 8), spec.get("outliers", 0.0))


R = Image.Resampling
SAMPLES = {
    "s01_gem_k4_soft": dict(
        art=gem, factor=4, seed=11, blur=0.6, alpha_blur=0.5, amp=8, outliers=0.02,
        expect_grid=4, expect_size=(24, 24),
        note="4x nearest upscale, softened block edges and alpha, colour noise, 2 % outliers"),
    "s02_gem_k5_softer": dict(
        art=gem, factor=5, seed=12, blur=0.8, alpha_blur=0.6, amp=10, outliers=0.03,
        expect_grid=5, expect_size=(24, 24), note="5x upscale, softer edges, more noise"),
    "s03_coin_k8": dict(
        art=coin, factor=8, seed=13, blur=1.0, alpha_blur=0.8, amp=10, outliers=0.03,
        expect_grid=8, expect_size=(20, 20), min_agreement=0.97, note="8x upscale of a second motif with strong softening"),
    "s04_gem_k4_phase1": dict(
        art=gem, factor=4, seed=14, blur=0.6, alpha_blur=0.5, amp=8, outliers=0.02, phase=1,
        expect_grid=4, expect_size=(24, 24), min_agreement=0.97,
        note="grid phase offset of 1 px so every block straddles the true grid"),
    "s05_bars_k6_wobble": dict(
        art=bars, factor=6, seed=15, blur=0.7, alpha_blur=0.5, amp=6, outliers=0.02, wobble=0.15,
        expect_grid=6, expect_size=(22, 18), min_agreement=0.95,
        note="6x upscale where 15 % of pixel rows are shifted by 1 px (off-grid jitter)"),
    "s06_gem_nonint_4p5": dict(
        art=gem, factor=4.5, seed=16, blur=0.0, amp=8, outliers=0.02,
        expect_grid=None, expect_size=(24, 24),
        note="4.5x nearest upscale: no integer grid exists, the route must refuse instead of guessing"),
    "s07_gem_bilinear4": dict(
        art=gem, factor=4, seed=17, resample=R.BILINEAR, amp=8, outliers=0.0,
        expect_grid=None, pinned_grid=4, expect_size=(24, 24), min_agreement=0.8,
        note="4x bilinear upscale: every pixel is blended, auto detection is not trusted, the brief pins the grid"),
}


def main() -> None:
    palette = load_palette()
    SAMPLES_DIR.mkdir(parents=True, exist_ok=True)
    index = {}
    for name, spec in SAMPLES.items():
        raw = build(spec)
        raw.save(SAMPLES_DIR / f"{name}_raw.png", compress_level=9, optimize=False)
        entry = {"note": spec["note"], "rawHash": pixel_hash(raw), "rawSize": list(raw.size),
                 "expectGrid": spec["expect_grid"], "pinnedGrid": spec.get("pinned_grid"),
                 "expectSize": list(spec["expect_size"])}
        if spec["expect_grid"] is not None or spec.get("pinned_grid"):
            cfg = PassConfig(grid=spec.get("pinned_grid"), **CFG)
            frames, _ = run_pixel_pass([raw], palette, cfg)
            frames[0].save(SAMPLES_DIR / f"{name}_golden.png", compress_level=9, optimize=False)
            entry["goldenHash"] = pixel_hash(frames[0])
        index[name] = entry
    (OUT / "samples.json").write_text(json.dumps(index, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(index, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
