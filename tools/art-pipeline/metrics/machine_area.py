"""SC-004 machine body area (research R18, plan review PO-5).

Input is the body-mask frame of a capture: the same camera and framing, but only the machine body renderers are drawn, on the reserved key colour.
The shadow, the plate, the deck and every cue are absent from it, so the body area excludes the shadow by construction. Each machine in the layout
dump has a cell rectangle and the screen bounds of its body renderer. The machine's body area is the number of non-key pixels inside its body
bounds. The overhang is the part of those pixels outside the machine's own cell rectangle, reported separately. A pixel that lies inside the bounds of
two machines is counted for neither and reported as `ambiguousPx`, so the measurement never double counts.

    python -m metrics.machine_area <body-mask.png> <layout.json>
"""
from __future__ import annotations

import json
import statistics
import sys
from pathlib import Path

from PIL import Image, ImageChops

from . import METRICS_VERSION
from .common import KEY_COLOUR, count, key_mask, load_layout, rect_mask


def machine_areas(mask_image: Image.Image, layout: dict, key: tuple[int, int, int] = KEY_COLOUR) -> dict:
    layout = load_layout(layout)
    if mask_image.size != (layout["width"], layout["height"]):
        raise ValueError(f"mask is {mask_image.size} but the layout dump says {(layout['width'], layout['height'])}")
    body = ImageChops.invert(key_mask(mask_image, key))  # 255 where something is drawn
    machines = layout.get("machines", [])
    bounds = [rect_mask(mask_image.size, [m["body"]]) for m in machines]
    # Where two bounds overlap, a pixel belongs to no machine.
    shared = Image.new("L", mask_image.size, 0)
    for i, a in enumerate(bounds):
        for b in bounds[i + 1:]:
            shared = ImageChops.lighter(shared, ImageChops.multiply(a, b))
    rows = []
    for m, own in zip(machines, bounds):
        mine = ImageChops.multiply(ImageChops.multiply(body, own), ImageChops.invert(shared))
        in_cell = ImageChops.multiply(mine, rect_mask(mask_image.size, [m["cell"]]))
        area = count(mine)
        rows.append({
            "slot": m["slot"], "machine": m["machine"], "bodyAreaPx": area,
            "overhangPx": area - count(in_cell),
            "ambiguousPx": count(ImageChops.multiply(ImageChops.multiply(body, own), shared)),
        })
    areas = [r["bodyAreaPx"] for r in rows if r["bodyAreaPx"] > 0]
    return {
        "metricsVersion": METRICS_VERSION,
        "machines": rows,
        "machineCount": len(rows),
        "medianBodyAreaPx": statistics.median(areas) if areas else 0,
        "medianOverhangPx": statistics.median([r["overhangPx"] for r in rows]) if rows else 0,
        "emptyBodies": [r["slot"] for r in rows if r["bodyAreaPx"] == 0],
    }


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__)
        return 2
    print(json.dumps(machine_areas(Image.open(argv[0]), Path(argv[1])), indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
