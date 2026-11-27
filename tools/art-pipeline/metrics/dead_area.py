"""SC-006 dead-area share (research R18).

The dead share is the part of the safe area that is neither world art nor interface. In a capture build the world camera clears to the reserved key
colour, so "neither" is exactly the key-coloured pixels inside the safe area that no UI rectangle covers. UI rectangles come from the layout dump
(UI Toolkit world bounds); areas hidden by the machine card count as UI (plan review UX-6, PO-6).

    python -m metrics.dead_area <capture.png> <layout.json>
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image, ImageChops

from . import METRICS_VERSION
from .common import KEY_COLOUR, count, key_mask, load_layout, rect_mask


def dead_area(image: Image.Image, layout: dict, key: tuple[int, int, int] = KEY_COLOUR) -> dict:
    layout = load_layout(layout)
    if image.size != (layout["width"], layout["height"]):
        raise ValueError(f"capture is {image.size} but the layout dump says {(layout['width'], layout['height'])}")
    safe = rect_mask(image.size, [layout["safeArea"]])
    ui = rect_mask(image.size, list(layout.get("uiRects", [])) + list(layout.get("cardHiddenRects", [])))
    key_px = key_mask(image, key)
    safe_px = count(safe)
    ui_in_safe = count(ImageChops.multiply(safe, ui))
    dead = count(ImageChops.multiply(ImageChops.multiply(safe, key_px), ImageChops.invert(ui)))
    return {
        "metricsVersion": METRICS_VERSION,
        "safeAreaPx": safe_px,
        "uiInSafePx": ui_in_safe,
        "keyPxInSafe": count(ImageChops.multiply(safe, key_px)),
        "deadPx": dead,
        # SC-006: the share of the safe area (the whole safe area is the denominator).
        "deadAreaShare": dead / safe_px if safe_px else 0.0,
        # Secondary view: the share of the part that UI does not cover.
        "deadShareOfNonUi": dead / (safe_px - ui_in_safe) if safe_px > ui_in_safe else 0.0,
    }


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__)
        return 2
    print(json.dumps(dead_area(Image.open(argv[0]), Path(argv[1])), indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
