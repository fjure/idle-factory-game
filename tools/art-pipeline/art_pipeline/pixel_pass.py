"""Deterministic pixel pass (research R07).

Fixed order: (1) route-specific reduction to the art grid, (2) alpha 0/255,
(3) CIELAB palette quantization (deterministic tie-break, no dithering),
(4) 1 px outline generation, (5) one fixed footprint-anchored canvas and pivot
per asset shared by all frames, (6) frames and manifest entry.
"""
from __future__ import annotations

import hashlib
import io
import json
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from . import MANIFEST_SCHEMA_VERSION
from .color import RGB, lab_distance_sq, rgb_to_lab
from .palette import Palette
from .provenance import missing_provenance_fields

ALPHA_CUTOFF = 128
TRANSPARENT = (0, 0, 0, 0)


class PixelPassError(ValueError):
    pass


# --- (1) route-specific reduction ------------------------------------------------

def make_mapper(palette: Palette, groups: set[str] | None = None):
    """Cached RGB -> nearest palette RGB function (CIELAB, lowest-index tie-break)."""
    rgbs = palette.rgbs(groups)
    if not rgbs:
        raise PixelPassError("no palette colors available for quantization")
    cache: dict[RGB, RGB] = {}

    def mapper(rgb: RGB) -> RGB:
        if rgb not in cache:
            cache[rgb] = rgbs[nearest_palette_index(rgb, rgbs)]
        return cache[rgb]

    return mapper


def reduce_mode(img: Image.Image, k: int, mapper=None) -> Image.Image:
    """Reduce by an integer factor taking the most frequent value per k*k block.

    With a `mapper` (palette quantizer) pixels are mapped first, so diffusion noise and jitter
    vote for the palette entry they belong to. Ties resolve to the smallest RGBA tuple;
    transparent pixels count as one value (0,0,0,0).
    """
    img = img.convert("RGBA")
    w, h = img.size
    if k < 1 or w % k or h % k:
        raise PixelPassError(f"image {w}x{h} is not divisible by factor {k}")
    src = img.load()
    out = Image.new("RGBA", (w // k, h // k), TRANSPARENT)
    dst = out.load()
    for by in range(h // k):
        for bx in range(w // k):
            counts: Counter = Counter()
            for y in range(by * k, by * k + k):
                for x in range(bx * k, bx * k + k):
                    p = src[x, y]
                    if p[3] < ALPHA_CUTOFF:
                        counts[TRANSPARENT] += 1
                    else:
                        c = mapper((p[0], p[1], p[2])) if mapper else (p[0], p[1], p[2])
                        counts[c + (255,)] += 1
            dst[bx, by] = min(counts.items(), key=lambda kv: (-kv[1], kv[0]))[0]
    return out


def reduce_box(img: Image.Image, k: int) -> Image.Image:
    """Reduce by an integer factor with an alpha-weighted box average (integer rounding)."""
    img = img.convert("RGBA")
    w, h = img.size
    if k < 1 or w % k or h % k:
        raise PixelPassError(f"image {w}x{h} is not divisible by factor {k}")
    src = img.load()
    out = Image.new("RGBA", (w // k, h // k), TRANSPARENT)
    dst = out.load()
    n = k * k
    for by in range(h // k):
        for bx in range(w // k):
            sa = sr = sg = sb = 0
            for y in range(by * k, by * k + k):
                for x in range(bx * k, bx * k + k):
                    r, g, b, a = src[x, y]
                    sa += a
                    sr += r * a
                    sg += g * a
                    sb += b * a
            if sa == 0:
                continue
            dst[bx, by] = ((2 * sr + sa) // (2 * sa), (2 * sg + sa) // (2 * sa),
                           (2 * sb + sa) // (2 * sa), (2 * sa + n) // (2 * n))
    return out


def grid_uniformity(img: Image.Image, k: int, mapper=None) -> float:
    """Fraction of pixels equal to the modal value of their k*k block."""
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    same = 0
    for by in range(h // k):
        for bx in range(w // k):
            counts: Counter = Counter()
            for y in range(by * k, by * k + k):
                for x in range(bx * k, bx * k + k):
                    p = px[x, y]
                    if p[3] < ALPHA_CUTOFF:
                        counts[TRANSPARENT] += 1
                    else:
                        counts[(mapper(p[:3]) if mapper else p[:3]) + (255,)] += 1
            same += max(counts.values())
    return same / float(w * h)


def detect_grid(img: Image.Image, max_factor: int = 16, threshold: float = 0.9, margin: float = 0.02,
                mapper=None) -> int:
    """Largest integer factor k (dividing both sides) at which the image is blockwise uniform.

    A factor qualifies when its uniformity is at least `threshold` and within `margin` of the best
    uniformity over all factors (multiples of the true grid lose uniformity, so the true grid wins
    over larger factors). Returns 1 when none qualifies. Deterministic. Flat art that is blocky at a
    multiple of the true grid is inherently ambiguous, so a brief may pin `grid`.
    """
    w, h = img.size
    scores = {k: grid_uniformity(img, k, mapper) for k in range(2, min(max_factor, w, h) + 1)
              if w % k == 0 and h % k == 0}
    if not scores:
        return 1
    best = max(scores.values())
    for k in sorted(scores, reverse=True):
        if scores[k] >= threshold and scores[k] >= best - margin:
            return k
    return 1


def reduce_for_route(img: Image.Image, route: str, *, supersample: int = 1, method: str = "mode",
                     grid: int | None = None, expected_size: tuple[int, int] | None = None,
                     mapper=None) -> Image.Image:
    """Route-specific first step. `blender`: final size or integer supersample (mode/box);
    `comfy`: grid detection + mode reduction; `scripted`: already on the grid."""
    img = img.convert("RGBA")
    if route == "blender":
        if supersample == 1:
            out = img
        elif method == "mode":
            out = reduce_mode(img, supersample, mapper)
        elif method == "box":
            out = reduce_box(img, supersample)
        else:
            raise PixelPassError(f"unknown reduction method {method!r}")
    elif route == "comfy":
        k = grid if grid is not None else detect_grid(img, mapper=mapper)
        out = reduce_mode(img, k, mapper) if k > 1 else img
    elif route == "scripted":
        out = img
    else:
        raise PixelPassError(f"unknown route {route!r}")
    if expected_size is not None and out.size != tuple(expected_size):
        raise PixelPassError(f"reduced size {out.size} != expected {tuple(expected_size)}")
    return out


# --- (2) alpha ------------------------------------------------------------------

def threshold_alpha(img: Image.Image, cutoff: int = ALPHA_CUTOFF) -> Image.Image:
    """Alpha becomes exactly 0 or 255; fully transparent pixels get RGB 0."""
    img = img.convert("RGBA")
    out = Image.new("RGBA", img.size, TRANSPARENT)
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            if a >= cutoff:
                dst[x, y] = (r, g, b, 255)
    return out


# --- (3) CIELAB quantization -----------------------------------------------------

def nearest_palette_index(rgb: RGB, palette_rgbs: list[RGB]) -> int:
    """Nearest palette entry by squared CIELAB distance; exact ties go to the lowest index."""
    lab = rgb_to_lab(rgb)
    best_i, best_d = 0, None
    for i, p in enumerate(palette_rgbs):
        d = round(lab_distance_sq(lab, rgb_to_lab(p)), 9)
        if best_d is None or d < best_d:
            best_i, best_d = i, d
    return best_i


def quantize(img: Image.Image, palette: Palette, groups: set[str] | None = None) -> Image.Image:
    """Map every opaque pixel to its nearest palette color. No dithering."""
    rgbs = palette.rgbs(groups)
    if not rgbs:
        raise PixelPassError("no palette colors available for quantization")
    cache: dict[RGB, RGB] = {}
    img = img.convert("RGBA")
    out = Image.new("RGBA", img.size, TRANSPARENT)
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            if key not in cache:
                cache[key] = rgbs[nearest_palette_index(key, rgbs)]
            dst[x, y] = cache[key] + (255,)
    return out


# --- (4) outline -----------------------------------------------------------------

def add_outline(img: Image.Image, ink: RGB) -> Image.Image:
    """Generate a 1 px outline (4-connected) outside the silhouette. Output is 2 px larger per axis."""
    w, h = img.size
    out = Image.new("RGBA", (w + 2, h + 2), TRANSPARENT)
    out.paste(img, (1, 1))
    src, dst = img.load(), out.load()
    for y in range(-1, h + 1):
        for x in range(-1, w + 1):
            if 0 <= x < w and 0 <= y < h and src[x, y][3] == 255:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] == 255:
                    dst[x + 1, y + 1] = ink + (255,)
                    break
    return out


# --- (5) canvas, footprint, pivot ------------------------------------------------

@dataclass(frozen=True)
class Placement:
    canvas: tuple[int, int]
    footprint: tuple[int, int]
    footprint_rect: tuple[int, int, int, int]  # left, top, width, height (canvas px, y down)
    pivot: tuple[float, float]  # normalized to the canvas, y up (Unity convention)
    content_offset: tuple[int, int]


def footprint_rect(canvas: tuple[int, int], footprint: tuple[int, int]) -> tuple[int, int, int, int]:
    """The footprint is horizontally centered and rests on the canvas bottom."""
    cw, ch = canvas
    fw, fh = footprint
    if fw > cw or fh > ch:
        raise PixelPassError(f"footprint {footprint} larger than canvas {canvas}")
    return ((cw - fw) // 2, ch - fh, fw, fh)


def pivot_for(canvas: tuple[int, int], footprint: tuple[int, int]) -> tuple[float, float]:
    """Pivot = footprint center, normalized to the canvas, y up."""
    cw, ch = canvas
    left, _top, fw, fh = footprint_rect(canvas, footprint)
    return (round((left + fw / 2) / cw, 6), round((fh / 2) / ch, 6))


def opaque_bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    return img.getchannel("A").point(lambda a: 255 if a else 0).getbbox()


def place_on_canvas(frames: list[Image.Image], canvas: tuple[int, int],
                    footprint: tuple[int, int]) -> tuple[list[Image.Image], Placement]:
    """Place all frames with ONE shared offset: the union bbox of all frames rests on the
    footprint bottom and is centered on the footprint. Never trimmed per frame (loops would jitter)."""
    if not frames:
        raise PixelPassError("no frames")
    if len({f.size for f in frames}) != 1:
        raise PixelPassError("frames must share one size before placement")
    boxes = [b for b in (opaque_bbox(f) for f in frames) if b]
    if not boxes:
        raise PixelPassError("all frames are fully transparent")
    x0, y0 = min(b[0] for b in boxes), min(b[1] for b in boxes)
    x1, y1 = max(b[2] for b in boxes), max(b[3] for b in boxes)
    bw, bh = x1 - x0, y1 - y0
    cw, ch = canvas
    fl, ft, fw, fh = footprint_rect(canvas, footprint)
    left = fl + (fw - bw) // 2
    top = ch - bh
    if left < 0 or left + bw > cw or top < 0:
        raise PixelPassError(f"content {bw}x{bh} does not fit canvas {cw}x{ch} anchored on the footprint")
    placed = []
    for f in frames:
        c = Image.new("RGBA", canvas, TRANSPARENT)
        c.paste(f.crop((x0, y0, x1, y1)), (left, top))
        placed.append(c)
    return placed, Placement(canvas, footprint, (fl, ft, fw, fh), pivot_for(canvas, footprint), (left - x0, top - y0))


# --- complete pass ---------------------------------------------------------------

@dataclass
class PassConfig:
    route: str
    canvas: tuple[int, int]
    footprint: tuple[int, int]
    supersample: int = 1
    method: str = "mode"
    grid: int | None = None
    quant_groups: set[str] | None = None
    outline: bool = True
    ink_id: str = "ink_0"


def run_pixel_pass(raw_frames: list[Image.Image], palette: Palette,
                   cfg: PassConfig) -> tuple[list[Image.Image], Placement]:
    frames = []
    mapper = make_mapper(palette, cfg.quant_groups)
    for raw in raw_frames:
        f = reduce_for_route(raw, cfg.route, supersample=cfg.supersample, method=cfg.method, grid=cfg.grid,
                             mapper=mapper)
        f = threshold_alpha(f)
        f = quantize(f, palette, cfg.quant_groups)
        if cfg.outline:
            f = add_outline(f, palette.rgb(cfg.ink_id))
        frames.append(f)
    return place_on_canvas(frames, cfg.canvas, cfg.footprint)


# --- (6) frames and manifest -----------------------------------------------------

def png_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.convert("RGBA").save(buf, format="PNG", compress_level=9, optimize=False)
    return buf.getvalue()


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def pixel_hash(img: Image.Image) -> str:
    """Hash of size + raw RGBA pixels: encoder-independent golden value."""
    img = img.convert("RGBA")
    return sha256_hex(f"{img.width}x{img.height}:".encode() + img.tobytes())


def build_manifest_entry(asset_id: str, route: str, frames: list[Image.Image], placement: Placement,
                         palette: Palette, ppu: int, fps: int, provenance: dict,
                         frame_dir: str | None = None) -> dict:
    """Manifest entry with PNG hashes. Refuses an asset whose provenance is incomplete (SC-004)."""
    missing = missing_provenance_fields(provenance, route, for_manifest=True)
    if missing:
        raise PixelPassError(f"provenance incomplete for {asset_id!r}: {', '.join(missing)}")
    entries = []
    for i, f in enumerate(frames):
        name = f"{asset_id}_{i:02d}.png"
        entries.append({"file": f"{frame_dir}/{name}" if frame_dir else name, "sha256": sha256_hex(png_bytes(f))})
    return {
        "assetId": asset_id,
        "route": route,
        "ppu": ppu,
        "fps": fps,
        "canvas": list(placement.canvas),
        "footprintPx": list(placement.footprint),
        "pivot": list(placement.pivot),
        "palette": {"id": palette.palette_id, "version": palette.version},
        "frames": entries,
    }


def build_manifest(entries: list[dict]) -> dict:
    return {"schemaVersion": MANIFEST_SCHEMA_VERSION, "entries": sorted(entries, key=lambda e: e["assetId"])}


def manifest_json(manifest: dict) -> str:
    return json.dumps(manifest, indent=2, sort_keys=True) + "\n"


def write_frames(frames: list[Image.Image], asset_id: str, out_dir: Path | str) -> list[Path]:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    paths = []
    for i, f in enumerate(frames):
        p = out / f"{asset_id}_{i:02d}.png"
        p.write_bytes(png_bytes(f))
        paths.append(p)
    return paths
