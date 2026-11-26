"""T549: before/after sheets for the F006 visual acceptance (evidence only, no verdict).

Before = T502 baseline PNGs (reviews/baseline/png, rig) and, for the emulator classes, the device captures; after = fresh captures of the final F006 commit.
One sheet per (state, class): rows 100 % / 200 % text, columns BEFORE / AFTER, every cell labeled; one overview per class; the stage triptych per class.
Every output is scaled so that its long side stays at or below MAX_SIDE (C-102: oversize sheets downscale badly in viewers).

Usage: python t549_sheets.py --before-png DIR --after-rig DIR --out DIR --commit SHA --baseline SHA [--win DIR] [--device-before DIR --device-after DIR]
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

MAX_SIDE = 4000
CLASSES = [
    "phone-portrait-360x640", "phone-landscape-640x360", "phone-portrait-390x844", "phone-landscape-844x390",
    "tablet-portrait-768x1024", "tablet-landscape-1024x768", "desktop-1280x720", "desktop-1920x1080",
    "emulator-portrait-1080x2400", "emulator-landscape-2400x1080",
]
# state -> (before state in the T502 baseline, after state in the rig)
STATES = {
    "no-panel": ("no-panel", "no-panel"),
    "card": ("machine-sheet", "card"),
    "details": ("machine-sheet", "details"),
    "building": ("build-sheet", "building"),
    "hub": ("contracts", "hub"),
    "system-view": ("contracts", "system-view"),
    "offline-summary": ("offline-summary", "offline-summary"),
}
BG = (24, 26, 32)
FG = (235, 235, 235)
STAGES = ("Early", "Partial", "Established")


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # very old Pillow
        return ImageFont.load_default()


def save(image: Image.Image, path: Path) -> None:
    """Sheets are evidence overviews: a 256-colour palette keeps the repository small; the raw captures next to them stay lossless."""
    image.quantize(256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(path, optimize=True)


def fit(canvas: Image.Image) -> Image.Image:
    long_side = max(canvas.size)
    if long_side <= MAX_SIDE:
        return canvas
    k = MAX_SIDE / long_side
    return canvas.resize((max(1, round(canvas.width * k)), max(1, round(canvas.height * k))), Image.LANCZOS)


def load(path: Path | None) -> Image.Image | None:
    return Image.open(path).convert("RGB") if path and path.exists() else None


def labeled(image: Image.Image | None, label: str, cell: tuple[int, int], size: int) -> Image.Image:
    bar = size * 2
    out = Image.new("RGB", (cell[0], cell[1] + bar), BG)
    draw = ImageDraw.Draw(out)
    draw.text((6, size // 2), label, fill=FG, font=font(size))
    if image is None:
        draw.rectangle([0, bar, cell[0] - 1, cell[1] + bar - 1], outline=(120, 60, 60))
        draw.text((10, bar + 10), "no capture (see README: gaps)", fill=(220, 140, 140), font=font(size))
    else:
        out.paste(image, (0, bar))
    return out


def grid(cells: list[list[Image.Image]], title: str, size: int, gap: int = 12) -> Image.Image:
    bar = size * 2 + 8
    col_w = [max(row[c].width for row in cells if c < len(row)) for c in range(max(len(r) for r in cells))]
    row_h = [max(i.height for i in row) for row in cells]
    canvas = Image.new("RGB", (sum(col_w) + gap * (len(col_w) + 1), bar + sum(row_h) + gap * (len(row_h) + 1)), BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((gap, 4), title, fill=FG, font=font(size))
    y = bar + gap
    for r, row in enumerate(cells):
        x = gap
        for c, image in enumerate(row):
            canvas.paste(image, (x, y))
            x += col_w[c] + gap
        y += row_h[r] + gap
    return canvas


def size_of(w: int, h: int) -> tuple[int, int]:
    return w, h


def state_sheet(cls: str, state: str, args: argparse.Namespace) -> Image.Image:
    before_state, after_state = STATES[state]
    rows = []
    sample = None
    for pct in (100, 200):
        before = load(args.before_png / f"{cls}-{before_state}-{pct}.png")
        after = load(args.after_rig / f"{cls}-{after_state}-{pct}.png")
        sample = sample or before or after
        w, h = sample.size
        text_size = max(14, round(w / 40)) if w > 900 else 14
        rows.append([
            labeled(before, f"BEFORE {pct}% | {state} | {cls} | T502 baseline {args.baseline[:8]} ({before_state})", (w, h), text_size),
            labeled(after, f"AFTER {pct}% | {state} | {cls} | {args.commit[:8]} ({after_state})", (w, h), text_size),
        ])
    return fit(grid(rows, f"T549 {state} - {cls} - 100 % (top) and 200 % (bottom) text - rig capture, evidence only", rows[0][0].height // 40 + 12))


def overview(cls: str, args: argparse.Namespace) -> Image.Image:
    thumbs = []
    first = load(args.after_rig / f"{cls}-no-panel-100.png")
    w, h = first.size
    k = min(0.5, (MAX_SIDE - 100) / (len(STATES) * w) if True else 1, (MAX_SIDE - 200) / (4 * h))
    tw, th = max(1, round(w * k)), max(1, round(h * k))
    names = [("before", 100), ("after", 100), ("before", 200), ("after", 200)]
    for which, pct in names:
        row = []
        for state, (bs, a_s) in STATES.items():
            src = args.before_png / f"{cls}-{bs}-{pct}.png" if which == "before" else args.after_rig / f"{cls}-{a_s}-{pct}.png"
            image = load(src)
            if image is not None:
                image = image.resize((tw, th), Image.LANCZOS)
            row.append(labeled(image, f"{which.upper()} {pct}% {state}", (tw, th), 12))
        thumbs.append(row)
    return fit(grid(thumbs, f"T549 overview - {cls} - rows: before 100, after 100, before 200, after 200 - columns: " + ", ".join(STATES), 14))


def triptych(cls: str, args: argparse.Namespace) -> Image.Image | None:
    if not args.win:
        return None
    images = [load(args.win / f"sc006-{cls}-s{s}.png") for s in range(3)]
    if all(i is None for i in images):
        return None
    sample = next(i for i in images if i is not None)
    w, h = sample.size
    cells = [labeled(i, f"AFTER stage {s} {STAGES[s]} | {cls} | {args.commit[:8]} | no before (new in F006)", (w, h), 16) for s, i in enumerate(images)]
    return fit(grid([cells], f"T549 stage triptych (FR-036) - {cls} - real scene, Windows player", 16))


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--before-png", type=Path, required=True)
    p.add_argument("--after-rig", type=Path, required=True)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--commit", required=True)
    p.add_argument("--baseline", required=True)
    p.add_argument("--win", type=Path)
    args = p.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    count = 0
    for cls in CLASSES:
        for state in STATES:
            save(state_sheet(cls, state, args), args.out / f"sheet-{cls}-{state}.png")
            count += 1
        save(overview(cls, args), args.out / f"overview-{cls}.png")
        t = triptych(cls, args)
        if t is not None:
            save(t, args.out / f"triptych-{cls}.png")
            count += 1
    print(count)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
