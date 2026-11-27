"""Shared pieces of the F007 metric scripts: the reserved key colour, layout-dump loading and rectangle masks."""
from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

# The reserved clear colour of capture builds (research R18, plan review QA-8). It is pure green; no art PNG under art-source/ or Assets/ contains it
# (tests/metrics/test_metrics_key_colour_reserved.py scans every one), and capture rigs set it as the world camera's clear colour only.
KEY_COLOUR = (0, 255, 0)

LAYOUT_SCHEMA = 1


class LayoutError(ValueError):
    """The layout dump does not match the schema."""


def _rect(raw: dict, what: str) -> tuple[int, int, int, int]:
    try:
        x, y, w, h = (float(raw[k]) for k in ("x", "y", "w", "h"))
    except (KeyError, TypeError, ValueError) as exc:
        raise LayoutError(f"{what}: needs numeric x, y, w, h") from exc
    if w < 0 or h < 0:
        raise LayoutError(f"{what}: negative size")
    # Outward rounding: a rectangle that touches a pixel covers it.
    return (math.floor(x), math.floor(y), math.ceil(x + w), math.ceil(y + h))


def load_layout(source) -> dict:
    """Loads and validates a layout dump (a path or an already parsed dict). Rectangles are top-left origin, in capture pixels."""
    data = json.loads(Path(source).read_text(encoding="utf-8")) if isinstance(source, (str, Path)) else dict(source)
    if data.get("schema") != LAYOUT_SCHEMA:
        raise LayoutError(f"layout schema must be {LAYOUT_SCHEMA}")
    for key in ("width", "height"):
        if not isinstance(data.get(key), int) or data[key] <= 0:
            raise LayoutError(f"layout needs a positive integer {key}")
    if "safeArea" not in data:
        raise LayoutError("layout needs safeArea")
    _rect(data["safeArea"], "safeArea")
    for group in ("uiRects", "cardHiddenRects"):
        for i, r in enumerate(data.get(group, [])):
            _rect(r, f"{group}[{i}]")
    for i, m in enumerate(data.get("machines", [])):
        for k in ("slot", "machine", "cell", "body"):
            if k not in m:
                raise LayoutError(f"machines[{i}] needs {k}")
        _rect(m["cell"], f"machines[{i}].cell")
        _rect(m["body"], f"machines[{i}].body")
    return data


def rect_px(raw: dict, width: int, height: int) -> tuple[int, int, int, int]:
    """The rectangle as integer (left, top, right, bottom) clipped to the image."""
    l, t, r, b = _rect(raw, "rect")
    return (max(0, l), max(0, t), min(width, r), min(height, b))


def rect_mask(size: tuple[int, int], rects: list[dict]) -> Image.Image:
    """An L mask (255 inside the union of the rectangles)."""
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    for raw in rects:
        l, t, r, b = rect_px(raw, *size)
        if r > l and b > t:
            draw.rectangle((l, t, r - 1, b - 1), fill=255)
    return mask


def key_mask(image: Image.Image, key: tuple[int, int, int] = KEY_COLOUR) -> Image.Image:
    """An L mask (255 where the pixel equals the key colour exactly)."""
    mask = None
    for band, value in zip(image.convert("RGB").split(), key):
        hit = band.point(lambda v, value=value: 255 if v == value else 0)
        mask = hit if mask is None else ImageChops.multiply(mask, hit)
    return mask


def count(mask: Image.Image) -> int:
    """The number of set (255) pixels of an L mask."""
    return mask.histogram()[255]
